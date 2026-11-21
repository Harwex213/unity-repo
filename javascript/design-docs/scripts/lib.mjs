// Helpers shared by the scripts in this folder.
import { existsSync, readdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const DOCUMENTS_DIR = join(ROOT, "src", "documents");
const DOC_ID_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;

const listDocumentIds = () =>
  readdirSync(DOCUMENTS_DIR, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort();

// Exits with a message when `id` is not an existing document folder.
const requireDocumentId = (id) => {
  if (!id || !DOC_ID_PATTERN.test(id)) {
    fail(`Pass a document id (a folder in src/documents). Known: ${listDocumentIds().join(", ")}`);
  }
  if (!existsSync(join(DOCUMENTS_DIR, id, "index.ts"))) {
    fail(`No document "${id}". Known: ${listDocumentIds().join(", ")}`);
  }
  return id;
};

const fail = (message) => {
  console.error(message);
  process.exit(1);
};

export { DOCUMENTS_DIR, DOC_ID_PATTERN, ROOT, fail, listDocumentIds, requireDocumentId };
