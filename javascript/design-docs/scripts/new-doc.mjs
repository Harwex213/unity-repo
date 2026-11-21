// Creates a new document folder from a template.
//
// Usage: npm run new-doc -- <document-id> "<title>" "<description>" [order]
// Without `order` the document goes to the end of the list.
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { DOCUMENTS_DIR, DOC_ID_PATTERN, fail, listDocumentIds } from "./lib.mjs";

const [id, title, description = "", orderArgument] = process.argv.slice(2);
if (!id || !DOC_ID_PATTERN.test(id) || !title) {
  fail('Usage: npm run new-doc -- <document-id> "<title>" "<description>" [order]. The id is kebab-case.');
}
if (orderArgument !== undefined && !/^\d+$/.test(orderArgument)) {
  fail(`The order must be a whole number, got "${orderArgument}".`);
}
const dir = join(DOCUMENTS_DIR, id);
if (existsSync(dir)) {
  fail(`The document "${id}" already exists.`);
}

const order = orderArgument === undefined ? listDocumentIds().length + 1 : Number(orderArgument);
const quote = (text) => JSON.stringify(text);

mkdirSync(dir, { recursive: true });
writeFileSync(
  join(dir, "index.ts"),
  `import { drawSheet, drawText } from "../../kit/shapes";
import type { TDocument, TRect } from "../../core/types";

// ${title}.
// All coordinates are world units.

const SHEET: TRect = { x: 0, y: 0, width: 1600, height: 1000 };

const doc: TDocument = {
  id: ${quote(id)},
  order: ${order},
  title: ${quote(title)},
  description: ${quote(description)},
  bounds: SHEET,
  draw: (ctx) => {
    drawSheet(ctx, SHEET);
    drawText(ctx, ${quote(title)}, 50, 50, { size: 36, weight: 500 });
  },
};

export { doc };
`,
);
console.log(`Created src/documents/${id}/index.ts`);
