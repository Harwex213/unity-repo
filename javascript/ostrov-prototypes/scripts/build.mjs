// Production build.
//
// Usage:
//   npm run build [-- <id> ...]          dist/<id>/ per prototype and dist/index.html with the list
//   node scripts/build.mjs --out <dir> --dev <id>
//                                        one prototype into <dir>/<id>/, used by snapshot and with-server
//
// Each prototype is a separate compilation: a broken prototype fails only its own build.
import { rspack } from "@rspack/core";
import { mkdirSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { prototypeConfig } from "./config.mjs";
import { ROOT, launcherHtml, listPrototypeIds, listPrototypes, requirePrototypeId } from "./lib.mjs";

const args = process.argv.slice(2);
const option = (name) => {
  const index = args.indexOf(name);
  return index === -1 ? undefined : args.splice(index, 2)[1];
};
const flag = (name) => {
  const index = args.indexOf(name);
  return index === -1 ? false : args.splice(index, 1).length > 0;
};

const outArg = option("--out");
const dev = flag("--dev");
const outRoot = resolve(outArg ?? join(ROOT, "dist"));
const ids = args.length > 0 ? args.map(requirePrototypeId) : listPrototypeIds();

const build = (id) =>
  new Promise((done) => {
    const compiler = rspack(prototypeConfig({ id, mode: dev ? "development" : "production", outRoot }));
    compiler.run((error, stats) => {
      compiler.close(() => {});
      if (error) {
        console.error(`[${id}] ${error.message}`);
        done(false);
        return;
      }
      if (stats.hasErrors() || stats.hasWarnings()) {
        console.log(stats.toString({ preset: "errors-warnings", colors: process.stdout.isTTY }));
      }
      console.log(`[${id}] ${stats.hasErrors() ? "failed" : "built"}: ${join(outRoot, id)}`);
      done(!stats.hasErrors());
    });
  });

const results = [];
for (const id of ids) {
  results.push(await build(id));
}

if (!outArg) {
  mkdirSync(outRoot, { recursive: true });
  const built = listPrototypes().filter((item) => ids.includes(item.id));
  writeFileSync(join(outRoot, "index.html"), launcherHtml(built, (id) => `./${id}/`));
}

if (results.includes(false)) {
  process.exit(1);
}
