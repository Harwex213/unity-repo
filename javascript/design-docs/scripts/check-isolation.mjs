// Checks the import rules that keep documents isolated.
//
// - A document imports only from its own folder, src/kit and src/core.
// - Shared code (everything outside src/documents) never imports a document.
//   The only bridge is src/documents/registry.ts, which finds documents at build time.
//
// Usage: node scripts/check-isolation.mjs [document-id]
// Without an id, every document and the shared code are checked.
import { readFileSync, readdirSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { DOCUMENTS_DIR, ROOT, listDocumentIds, requireDocumentId } from "./lib.mjs";

const SRC = join(ROOT, "src");
const ALLOWED_SHARED = [join(SRC, "kit"), join(SRC, "core")];
const IMPORT_PATTERN = /(?:import|export)\s[^'"]*?from\s*["']([^"']+)["']|import\s*\(\s*["']([^"']+)["']\s*\)|import\s+["']([^"']+)["']/g;

const walk = (dir) =>
  readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      return walk(path);
    }
    return /\.(ts|tsx)$/.test(entry.name) ? [path] : [];
  });

const importsOf = (file) =>
  [...readFileSync(file, "utf8").matchAll(IMPORT_PATTERN)].map((match) => match[1] ?? match[2] ?? match[3]);

const isInside = (path, dir) => path === dir || path.startsWith(dir + sep);
const show = (path) => relative(ROOT, path);

const checkDocument = (id) => {
  const own = join(DOCUMENTS_DIR, id);
  const problems = [];
  for (const file of walk(own)) {
    for (const specifier of importsOf(file)) {
      if (!specifier.startsWith(".")) {
        problems.push(`${show(file)}: imports the package "${specifier}". A document imports only from its folder, src/kit and src/core.`);
        continue;
      }
      const target = resolve(dirname(file), specifier);
      if (isInside(target, own) || ALLOWED_SHARED.some((dir) => isInside(target, dir))) {
        continue;
      }
      problems.push(`${show(file)}: imports "${specifier}" (${show(target)}). A document imports only from its folder, src/kit and src/core.`);
    }
  }
  return problems;
};

const checkSharedCode = () => {
  const problems = [];
  const registry = join(DOCUMENTS_DIR, "registry.ts");
  const shared = walk(SRC).filter((file) => !isInside(file, DOCUMENTS_DIR) || file === registry);
  for (const file of shared) {
    for (const specifier of importsOf(file)) {
      if (!specifier.startsWith(".")) {
        continue;
      }
      const target = resolve(dirname(file), specifier);
      if (isInside(target, DOCUMENTS_DIR) && target !== registry.replace(/\.ts$/, "")) {
        problems.push(`${show(file)}: imports a document (${show(target)}). Shared code must not depend on documents.`);
      }
    }
  }
  return problems;
};

const id = process.argv[2];
const problems = id
  ? checkDocument(requireDocumentId(id))
  : [...listDocumentIds().flatMap(checkDocument), ...checkSharedCode()];

if (problems.length > 0) {
  console.error(problems.join("\n"));
  process.exit(1);
}
console.log(id ? `Isolation of "${id}": ok` : "Isolation of all documents and shared code: ok");
