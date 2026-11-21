// Builds one prototype in development mode into .cache/build/<id>/ and serves it on a free port.
// Used by snapshot.mjs and with-server.mjs. Parallel runs for different prototypes do not share files.
import { execFileSync } from "node:child_process";
import { join } from "node:path";
import { ROOT, fail, serveStatic } from "./lib.mjs";

const serveOne = async (id) => {
  const outRoot = join(ROOT, ".cache", "build", id);
  try {
    execFileSync(process.execPath, [join(ROOT, "scripts", "build.mjs"), "--out", outRoot, "--dev", id], {
      cwd: ROOT,
      stdio: ["ignore", "ignore", "inherit"],
    });
  } catch {
    fail(`The build of "${id}" failed. Run \`npm run check -- ${id}\` and \`node scripts/build.mjs --out .cache/build/${id} --dev ${id}\`.`);
  }
  const server = await serveStatic(outRoot);
  // No trailing slash: `${url}/?t=1` and `${url}/` both work, the server redirects `/<id>` to `/<id>/`.
  return { server, url: `http://127.0.0.1:${server.address().port}/${id}` };
};

export { serveOne };
