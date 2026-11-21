import type { TDocument } from "../core/types";

// The bundler finds every `src/documents/<id>/index.ts` at build time.
// A new document is a new folder: no shared file changes.
// A snapshot build of one document narrows this context to that folder (see rspack.config.mjs).
const context = import.meta.webpackContext("./", { recursive: true, regExp: /^\.\/[^/]+\/index\.ts$/ });

const loadDocument = (key: string): TDocument => {
  const module = context(key) as { doc?: TDocument };
  if (!module.doc) {
    throw new Error(`${key} must export its document as \`doc\``);
  }
  const folder = key.split("/")[1];
  if (module.doc.id !== folder) {
    throw new Error(`${key}: the document id "${module.doc.id}" must equal its folder name "${folder}"`);
  }
  return module.doc;
};

const DOCUMENTS: TDocument[] = context
  .keys()
  .map(loadDocument)
  .sort((a, b) => a.order - b.order || a.id.localeCompare(b.id));

export { DOCUMENTS };
