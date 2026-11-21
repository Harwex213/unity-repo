// Notes of documents: src/documents/<id>/notes.json, a list of free-form entries.
//
// The dev server serves the notes at /api/notes/<id>:
// - GET returns the list;
// - PUT replaces the list with the request body.
// A production build embeds the notes as they were at build time, and the viewer shows them read-only.
import { existsSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { DOCUMENTS_DIR, DOC_ID_PATTERN, listDocumentIds } from "./lib.mjs";

const NOTES_FILE = "notes.json";
const MAX_BODY_BYTES = 2 * 1024 * 1024;
const ROUTE = /^\/api\/notes\/([^/?#]+)\/?$/;

const notesPath = (id) => join(DOCUMENTS_DIR, id, NOTES_FILE);

const isDocument = (id) => DOC_ID_PATTERN.test(id) && existsSync(join(DOCUMENTS_DIR, id, "index.ts"));

const isNote = (value) =>
  typeof value === "object" &&
  value !== null &&
  typeof value.id === "string" &&
  typeof value.text === "string" &&
  typeof value.createdAt === "string";

const readNotes = (id) => {
  const path = notesPath(id);
  if (!existsSync(path)) {
    return [];
  }
  const notes = JSON.parse(readFileSync(path, "utf8"));
  if (!Array.isArray(notes) || !notes.every(isNote)) {
    throw new Error(`${path} must be a list of { id, text, createdAt }`);
  }
  return notes;
};

// The file is replaced in one step, so a crash never leaves half a file.
const writeNotes = (id, notes) => {
  const path = notesPath(id);
  const temporary = `${path}.tmp`;
  writeFileSync(temporary, `${JSON.stringify(notes, null, 2)}\n`);
  renameSync(temporary, path);
};

// The notes of every document that has any, keyed by document id.
const readAllNotes = () =>
  Object.fromEntries(
    listDocumentIds()
      .map((id) => [id, readNotes(id)])
      .filter(([, notes]) => notes.length > 0),
  );

const readBody = (request) =>
  new Promise((done, reject) => {
    const chunks = [];
    let size = 0;
    request.on("data", (chunk) => {
      size += chunk.length;
      if (size > MAX_BODY_BYTES) {
        reject(new Error("The notes are too large"));
        request.destroy();
        return;
      }
      chunks.push(chunk);
    });
    request.on("end", () => done(Buffer.concat(chunks).toString("utf8")));
    request.on("error", reject);
  });

const sendJson = (response, status, body) => {
  response.writeHead(status, { "content-type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(body));
};

// A connect-style middleware for the dev server.
const notesMiddleware = async (request, response, next) => {
  const match = ROUTE.exec(request.url ?? "");
  if (!match) {
    next();
    return;
  }
  const id = decodeURIComponent(match[1] ?? "");
  if (!isDocument(id)) {
    sendJson(response, 404, { error: `No document "${id}"` });
    return;
  }
  try {
    if (request.method === "GET") {
      sendJson(response, 200, readNotes(id));
      return;
    }
    if (request.method === "PUT") {
      const notes = JSON.parse(await readBody(request));
      if (!Array.isArray(notes) || !notes.every(isNote)) {
        sendJson(response, 400, { error: "The body must be a list of { id, text, createdAt }" });
        return;
      }
      writeNotes(id, notes);
      sendJson(response, 200, notes);
      return;
    }
    response.writeHead(405, { allow: "GET, PUT" }).end();
  } catch (error) {
    sendJson(response, 500, { error: String(error) });
  }
};

export { NOTES_FILE, notesMiddleware, readAllNotes };
