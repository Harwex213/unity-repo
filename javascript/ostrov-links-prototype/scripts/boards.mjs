/**
 * The boards of the editor ("Граф" of buildings, the technology tree) keep their
 * layout in `data/<board>.json`, in git. So the layout survives a restart of the
 * dev server and reaches other machines.
 *
 * The dev server serves `/data/<board>.json` from disk and writes it on POST.
 * The build copies `data/` next to `index.html`; there the boards are read-only.
 */
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";

const BOARD_FORMAT = "ostrov-board";
const BOARD_VERSION = 1;
/** Only these files can be read or written. */
const BOARD_FILES = new Set(["buildings-graph.json", "tech-tree.json"]);
const NODE_ID = /^[a-z]+:[a-z0-9_]+$/;
/** A board file is small; anything bigger is a mistake. */
const MAX_BODY_BYTES = 256 * 1024;

const isRecord = (value) => typeof value === "object" && value !== null && !Array.isArray(value);

/** Throws on a body that is not a board. Returns whole coordinates and sorted edge pairs. */
const parseBoard = (text) => {
  const value = JSON.parse(text);
  if (!isRecord(value) || value.format !== BOARD_FORMAT || value.version !== BOARD_VERSION) {
    throw new Error(`expected { "format": "${BOARD_FORMAT}", "version": ${BOARD_VERSION} }`);
  }

  if (!isRecord(value.nodes) || !Array.isArray(value.edges)) {
    throw new Error("nodes must be an object, edges must be an array");
  }

  const nodes = [];
  for (const [id, position] of Object.entries(value.nodes)) {
    if (!NODE_ID.test(id) || !isRecord(position) || !Number.isFinite(position.x) || !Number.isFinite(position.y)) {
      throw new Error(`bad node ${JSON.stringify(id)}`);
    }

    nodes.push([id, Math.round(position.x), Math.round(position.y)]);
  }

  const edges = value.edges.map((edge) => {
    if (!Array.isArray(edge) || edge.length !== 2 || !edge.every((id) => typeof id === "string" && NODE_ID.test(id))) {
      throw new Error(`bad edge ${JSON.stringify(edge)}`);
    }

    return [...edge].sort();
  });

  return { nodes, edges };
};

/** One node and one edge per line: moving a card changes one line of the git diff. */
const formatBoard = ({ nodes, edges }) => {
  const nodeLines = nodes.map(([id, x, y]) => `    ${JSON.stringify(id)}: { "x": ${x}, "y": ${y} }`);
  const edgeLines = edges.map(([a, b]) => `    [${JSON.stringify(a)}, ${JSON.stringify(b)}]`);
  const edgesText = edgeLines.length === 0 ? "[]" : `[\n${edgeLines.join(",\n")}\n  ]`;

  return `{\n  "format": "${BOARD_FORMAT}",\n  "version": ${BOARD_VERSION},\n  "nodes": {\n${nodeLines.join(",\n")}\n  },\n  "edges": ${edgesText}\n}\n`;
};

const boardPath = (root, file) => path.join(root, "data", file);

const readBody = (req) =>
  new Promise((resolve, reject) => {
    let size = 0;
    const chunks = [];
    req.on("data", (chunk) => {
      size += chunk.length;
      if (size > MAX_BODY_BYTES) {
        reject(new Error("body is too big"));
        req.destroy();
        return;
      }
      chunks.push(chunk);
    });
    req.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    req.on("error", reject);
  });

const send = (res, status, body) => {
  res.statusCode = status;
  res.setHeader("Content-Type", "application/json; charset=utf-8");
  res.setHeader("Cache-Control", "no-store");
  res.end(body);
};

/** A connect middleware for `devServer.setupMiddlewares`. */
const boardsMiddleware = (root) => async (req, res, next) => {
  const match = /^\/data\/([a-z-]+\.json)$/.exec((req.url ?? "").split("?")[0]);
  if (!match || !BOARD_FILES.has(match[1])) {
    next();
    return;
  }

  const file = boardPath(root, match[1]);
  try {
    if (req.method === "GET") {
      send(res, 200, await readFile(file, "utf8"));
      return;
    }

    if (req.method === "POST") {
      const board = parseBoard(await readBody(req));
      await writeFile(file, formatBoard(board));
      send(res, 200, JSON.stringify({ ok: true }));
      return;
    }

    send(res, 405, JSON.stringify({ ok: false, error: "method not allowed" }));
  } catch (error) {
    send(res, 400, JSON.stringify({ ok: false, error: error instanceof Error ? error.message : String(error) }));
  }
};

export { boardPath, boardsMiddleware, formatBoard };
