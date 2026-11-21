// Helpers shared by the scripts in this folder.
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { createServer } from "node:http";
import { dirname, extname, join, normalize, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const PROTOTYPES_DIR = join(ROOT, "prototypes");
const SHARED_DIR = join(ROOT, "shared");
const ID_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const ENTRY_NAMES = ["main.tsx", "main.ts"];

const fail = (message) => {
  console.error(message);
  process.exit(1);
};

const listPrototypeIds = () =>
  existsSync(PROTOTYPES_DIR)
    ? readdirSync(PROTOTYPES_DIR, { withFileTypes: true })
        .filter((entry) => entry.isDirectory() && existsSync(join(PROTOTYPES_DIR, entry.name, "prototype.json")))
        .map((entry) => entry.name)
        .sort()
    : [];

// Exits with a message when `id` is not an existing prototype folder.
const requirePrototypeId = (id) => {
  const known = () => `Known: ${listPrototypeIds().join(", ") || "none"}`;
  if (!id || !ID_PATTERN.test(id)) {
    fail(`Pass a prototype id (a folder in prototypes/). ${known()}`);
  }
  if (!existsSync(join(PROTOTYPES_DIR, id, "prototype.json"))) {
    fail(`No prototype "${id}". ${known()}`);
  }
  return id;
};

// prototype.json: { title, description, order, devServer?: { hot, liveReload } }.
const readMeta = (id) => {
  const meta = JSON.parse(readFileSync(join(PROTOTYPES_DIR, id, "prototype.json"), "utf8"));
  return {
    title: meta.title ?? id,
    description: meta.description ?? "",
    order: meta.order ?? 1000,
    devServer: { hot: false, liveReload: true, ...meta.devServer },
  };
};

const listPrototypes = () =>
  listPrototypeIds()
    .map((id) => ({ id, ...readMeta(id) }))
    .sort((a, b) => a.order - b.order || a.id.localeCompare(b.id));

const findEntry = (id) => {
  const name = ENTRY_NAMES.find((file) => existsSync(join(PROTOTYPES_DIR, id, "src", file)));
  if (!name) {
    throw new Error(`prototypes/${id}/src has no ${ENTRY_NAMES.join(" or ")}`);
  }
  return join(PROTOTYPES_DIR, id, "src", name);
};

const escapeHtml = (text) =>
  String(text).replace(/[&<>"]/g, (char) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[char]);

// The start page: a list of links, one per prototype. `href` maps an id to its link.
const launcherHtml = (prototypes, href) => `<!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<link rel="icon" href="data:," />
<title>Прототипы Острова</title>
<style>
  :root { color-scheme: dark; }
  body { margin: 0; background: #0d0f12; color: #e6e2d8; font: 16px/1.5 system-ui, sans-serif; }
  main { max-width: 760px; margin: 0 auto; padding: 48px 16px; }
  h1 { font-weight: 500; font-size: 28px; margin: 0 0 24px; }
  a { display: block; padding: 16px 20px; margin-bottom: 12px; border: 1px solid #2a2f37; border-radius: 10px;
      color: inherit; text-decoration: none; background: #14171c; }
  a:hover { border-color: #c9a86a; }
  .title { font-size: 18px; }
  .id { color: #7d8590; font: 13px ui-monospace, monospace; margin-left: 8px; }
  .description { color: #a5a9b0; margin-top: 4px; }
</style>
</head>
<body>
<main>
<h1>Прототипы Острова</h1>
${prototypes
  .map(
    (item) => `<a href="${escapeHtml(href(item.id))}">
  <div><span class="title">${escapeHtml(item.title)}</span><span class="id">${escapeHtml(item.id)}</span></div>
  <div class="description">${escapeHtml(item.description)}</div>
</a>`,
  )
  .join("\n")}
</main>
</body>
</html>
`;

const CONTENT_TYPES = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript",
  ".mjs": "text/javascript",
  ".css": "text/css",
  ".json": "application/json",
  ".map": "application/json",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".webp": "image/webp",
  ".avif": "image/avif",
  ".gif": "image/gif",
  ".svg": "image/svg+xml",
  ".woff": "font/woff",
  ".woff2": "font/woff2",
  ".ttf": "font/ttf",
  ".otf": "font/otf",
  ".aac": "audio/aac",
  ".mp3": "audio/mpeg",
  ".ogg": "audio/ogg",
  ".wav": "audio/wav",
  ".m4a": "audio/mp4",
  ".mp4": "video/mp4",
  ".webm": "video/webm",
  ".glb": "model/gltf-binary",
  ".gltf": "model/gltf+json",
  ".wasm": "application/wasm",
};

// Serves a built folder (dist/ or .cache/build/<id>/) on a free port of 127.0.0.1.
// `/<id>` redirects to `/<id>/`, because the page loads its files by relative paths.
const serveStatic = (dir) =>
  new Promise((done) => {
    const root = resolve(dir);
    const server = createServer((request, response) => {
      const url = new URL(request.url ?? "/", "http://localhost");
      const path = decodeURIComponent(url.pathname);
      let file = normalize(join(root, path));
      if (!file.startsWith(root)) {
        response.writeHead(403).end();
        return;
      }
      if (existsSync(join(file, "index.html"))) {
        if (!path.endsWith("/")) {
          response.writeHead(301, { location: `${path}/${url.search}` }).end();
          return;
        }
        file = join(file, "index.html");
      }
      if (!existsSync(file) || !file.startsWith(root + sep)) {
        response.writeHead(404).end();
        return;
      }
      response.writeHead(200, { "content-type": CONTENT_TYPES[extname(file)] ?? "application/octet-stream" });
      response.end(readFileSync(file));
    });
    server.listen(0, "127.0.0.1", () => done(server));
  });

export {
  ID_PATTERN,
  PROTOTYPES_DIR,
  ROOT,
  SHARED_DIR,
  fail,
  findEntry,
  launcherHtml,
  listPrototypeIds,
  listPrototypes,
  readMeta,
  requirePrototypeId,
  serveStatic,
};
