// Checks the isolation rules and the types.
//
// Usage:
//   npm run check -- <id>    one prototype: its imports and its types only
//   npm run check            every prototype and the shared code (main session)
//
// Rules:
// - A prototype imports only from its own folder, from `@shared/…` and from packages of package.json.
// - Shared code (shared/) never imports a prototype.
//
// The typecheck of one prototype covers only its folder and the shared code it imports,
// so an unfinished edit of another prototype does not fail it.
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { builtinModules } from "node:module";
import { dirname, join, relative, resolve, sep } from "node:path";
import { PROTOTYPES_DIR, ROOT, SHARED_DIR, fail, listPrototypeIds, requirePrototypeId } from "./lib.mjs";

const SOURCE_PATTERN = /\.(ts|tsx|js|mjs|cjs)$/;
const SKIPPED_DIRS = new Set(["node_modules", "dist", "art"]);
const IMPORT_PATTERN =
  /(?:import|export)\s[^'"]*?from\s*["']([^"']+)["']|import\s*\(\s*["']([^"']+)["']\s*\)|import\s+["']([^"']+)["']|require\s*\(\s*["']([^"']+)["']\s*\)/g;

const pkg = JSON.parse(readFileSync(join(ROOT, "package.json"), "utf8"));
const PACKAGES = new Set([...Object.keys(pkg.dependencies ?? {}), ...Object.keys(pkg.devDependencies ?? {})]);
const BUILTINS = new Set(builtinModules);

const walk = (dir) =>
  readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      return SKIPPED_DIRS.has(entry.name) ? [] : walk(path);
    }
    return SOURCE_PATTERN.test(entry.name) && !entry.name.endsWith(".d.ts") ? [path] : [];
  });

const importsOf = (file) =>
  [...readFileSync(file, "utf8").matchAll(IMPORT_PATTERN)].map((match) => match[1] ?? match[2] ?? match[3] ?? match[4]);

const isInside = (path, dir) => path === dir || path.startsWith(dir + sep);
const show = (path) => relative(ROOT, path);
const packageOf = (specifier) => specifier.split("/").slice(0, specifier.startsWith("@") ? 2 : 1).join("/");

const checkPrototype = (id) => {
  const own = join(PROTOTYPES_DIR, id);
  const problems = [];
  for (const file of walk(own)) {
    for (const specifier of importsOf(file)) {
      if (specifier.startsWith(".")) {
        const target = resolve(dirname(file), specifier);
        if (!isInside(target, own)) {
          problems.push(`${show(file)}: imports "${specifier}" outside its folder. Use @shared/… for shared code.`);
        }
        continue;
      }
      if (specifier === "@shared" || specifier.startsWith("@shared/") || specifier.startsWith("node:")) {
        continue;
      }
      const name = packageOf(specifier);
      if (!PACKAGES.has(name) && !BUILTINS.has(name)) {
        problems.push(`${show(file)}: imports the package "${name}", which is not in package.json. Ask the main session to add it.`);
      }
    }
  }
  return problems;
};

const checkSharedCode = () =>
  walk(SHARED_DIR).flatMap((file) =>
    importsOf(file)
      .filter((specifier) => specifier.startsWith(".") && isInside(resolve(dirname(file), specifier), PROTOTYPES_DIR))
      .map((specifier) => `${show(file)}: imports a prototype ("${specifier}"). Shared code must not depend on prototypes.`),
  );

const typecheck = (name, include) => {
  // A tsconfig per run, so parallel checks of different prototypes do not share a file.
  const cacheDir = join(ROOT, ".cache");
  mkdirSync(cacheDir, { recursive: true });
  const configPath = join(cacheDir, `tsconfig.${name}.json`);
  writeFileSync(configPath, JSON.stringify({ extends: "../tsconfig.json", include }, null, 2));
  try {
    execFileSync(join(ROOT, "node_modules", ".bin", "tsc"), ["--noEmit", "-p", configPath], { cwd: ROOT, stdio: "inherit" });
  } catch {
    return false;
  }
  return true;
};

const id = process.argv[2];
const problems = id ? checkPrototype(requirePrototypeId(id)) : [...listPrototypeIds().flatMap(checkPrototype), ...checkSharedCode()];
if (problems.length > 0) {
  fail(problems.join("\n"));
}
console.log(id ? `Isolation of "${id}": ok` : "Isolation of all prototypes and shared code: ok");

const typesOk = id
  ? typecheck(id, [`../prototypes/${id}/src/**/*`, "../types/**/*"])
  : typecheck("all", ["../prototypes/*/src/**/*", "../shared/**/*", "../types/**/*"]);
if (!typesOk) {
  process.exit(1);
}
console.log(id ? `Types of "${id}": ok` : "Types of everything: ok");
