import { readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";

/**
 * The dev server side of `prototype.saveStoreSnapshot()` and
 * `prototype.loadStoreSnapshot()`. POST writes the snapshot to one file in the
 * project, and GET reads it back. `src/domain/snapshot-actions.ts` calls this
 * route.
 */

const SNAPSHOT_ROUTE = "/__store-snapshot";
const SNAPSHOT_PATH = fileURLToPath(new URL("../store-snapshot.json", import.meta.url));

const sendJson = (response, statusCode, body) => {
  response.statusCode = statusCode;
  response.setHeader("Content-Type", "application/json");
  response.end(body);
};

const sendError = (response, statusCode, message) => {
  sendJson(response, statusCode, JSON.stringify({ error: message }));
};

const readBody = async (request) => {
  const chunks = [];
  for await (const chunk of request) {
    chunks.push(chunk);
  }

  return Buffer.concat(chunks).toString("utf8");
};

const saveSnapshot = async (request, response) => {
  // The parse refuses a broken body before it can replace a good file.
  const snapshot = JSON.parse(await readBody(request));
  await writeFile(SNAPSHOT_PATH, `${JSON.stringify(snapshot, null, 2)}\n`);
  sendJson(response, 200, JSON.stringify({ path: SNAPSHOT_PATH }));
};

const loadSnapshot = async (response) => {
  sendJson(response, 200, await readFile(SNAPSHOT_PATH, "utf8"));
};

const handleSnapshotRequest = async (request, response) => {
  try {
    if (request.method === "POST") {
      await saveSnapshot(request, response);
    } else if (request.method === "GET") {
      await loadSnapshot(response);
    } else {
      sendError(response, 405, `Method ${request.method} is not allowed`);
    }
  } catch (error) {
    if (error.code === "ENOENT") {
      sendError(response, 404, "No store snapshot has been saved yet");
    } else {
      sendError(response, 500, error.message);
    }
  }
};

const storeSnapshotMiddleware = {
  name: "store-snapshot",
  path: SNAPSHOT_ROUTE,
  middleware: (request, response) => {
    void handleSnapshotRequest(request, response);
  },
};

export { storeSnapshotMiddleware };
