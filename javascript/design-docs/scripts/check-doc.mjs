// Checks one document: its import rules and its types.
// The typecheck covers only this document and the shared code it imports,
// so an unfinished edit of another document does not fail it.
//
// Usage: npm run check -- <document-id>
import { execFileSync } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { ROOT, requireDocumentId } from "./lib.mjs";

const id = requireDocumentId(process.argv[2]);

const run = (command, args) => {
  try {
    execFileSync(command, args, { cwd: ROOT, stdio: "inherit" });
  } catch {
    process.exit(1);
  }
};

run(process.execPath, [join(ROOT, "scripts", "check-isolation.mjs"), id]);

// A tsconfig per document, so parallel checks of different documents do not share a file.
const cacheDir = join(ROOT, ".cache");
mkdirSync(cacheDir, { recursive: true });
const configPath = join(cacheDir, `tsconfig.${id}.json`);
writeFileSync(
  configPath,
  JSON.stringify(
    {
      extends: "../tsconfig.json",
      include: [`../src/documents/${id}/**/*`, "../src/types/**/*"],
    },
    null,
    2,
  ),
);
run(join(ROOT, "node_modules", ".bin", "tsc"), ["--noEmit", "-p", configPath]);
console.log(`Types of "${id}": ok`);
