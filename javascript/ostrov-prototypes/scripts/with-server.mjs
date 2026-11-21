// Runs a command of a prototype against a fresh build of that prototype.
//
// Usage: npm run with-server -- <id> <command> [args ...]
//
// - Builds the prototype, serves it on a free port and runs the command in prototypes/<id>/.
// - "{url}" in the arguments becomes the link of the prototype, without a trailing slash.
//   The same link is in the PROTOTYPE_URL variable.
// - Exits with the exit code of the command.
//
// Example: npm run with-server -- main-menu node tools/check.mjs {url}
import { spawn } from "node:child_process";
import { join } from "node:path";
import { PROTOTYPES_DIR, fail, requirePrototypeId } from "./lib.mjs";
import { serveOne } from "./serve-one.mjs";

const [idArg, command, ...commandArgs] = process.argv.slice(2);
const id = requirePrototypeId(idArg);
if (!command) {
  fail("Usage: npm run with-server -- <id> <command> [args ...]");
}

const { server, url } = await serveOne(id);
console.log(`[${id}] served at ${url}/`);
const child = spawn(command, commandArgs.map((arg) => arg.replaceAll("{url}", url)), {
  cwd: join(PROTOTYPES_DIR, id),
  env: { ...process.env, PROTOTYPE_URL: url },
  stdio: "inherit",
});
child.on("exit", (code) => {
  server.close();
  process.exit(code ?? 1);
});
