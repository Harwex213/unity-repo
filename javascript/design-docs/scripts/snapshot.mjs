// Renders one document to PNG files, to look at a change without a browser.
//
// Usage: npm run snapshot -- <document-id> [x,y,width,height ...]
//
// - Bundles only this document into .snapshots/<id>/dist, so parallel snapshots
//   of different documents do not touch each other.
// - Writes .snapshots/<id>/full.png with the whole document,
//   and .snapshots/<id>/view-<n>.png for each rect (world units) passed after the id.
// - Exits with code 1 when the page throws.
//
// Playwright: the script uses `playwright-core` from this project when it is installed,
// else the module at PLAYWRIGHT_MODULE, else a copy in a sibling project.
// Chromium: CHROMIUM_PATH, else the newest headless shell in the Playwright cache.
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync } from "node:fs";
import { createServer } from "node:http";
import { homedir } from "node:os";
import { extname, join, normalize, resolve, sep } from "node:path";
import { pathToFileURL } from "node:url";
import { ROOT, fail, requireDocumentId } from "./lib.mjs";

const VIEWPORT = { width: 1600, height: 1000 };
const SETTLE_MS = 400;

const [idArg, ...viewArgs] = process.argv.slice(2);
const id = requireDocumentId(idArg);
const views = viewArgs.map((text) => {
  const numbers = text.split(",").map(Number);
  if (numbers.length !== 4 || numbers.some((value) => !Number.isFinite(value))) {
    fail(`A view is "x,y,width,height" in world units, got "${text}"`);
  }
  return numbers.join(",");
});

// Copies of Playwright in the sibling projects of this one, for a machine
// where this project has no playwright-core installed.
const siblingPlaywrights = () => {
  const parent = join(ROOT, "..");
  return readdirSync(parent, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .flatMap((entry) => [
      join(parent, entry.name, "node_modules", "playwright-core", "index.mjs"),
      join(parent, entry.name, "node_modules", "playwright", "index.mjs"),
    ])
    .filter((path) => existsSync(path));
};

const loadPlaywright = async () => {
  const candidates = ["playwright-core", process.env.PLAYWRIGHT_MODULE, ...siblingPlaywrights()].filter(Boolean);
  for (const candidate of candidates) {
    try {
      const specifier = candidate.includes(sep) ? pathToFileURL(candidate).href : candidate;
      const module = await import(specifier);
      return module.chromium ?? module.default.chromium;
    } catch {
      // Try the next candidate.
    }
  }
  return fail("Playwright was not found. Install playwright-core or set PLAYWRIGHT_MODULE.");
};

const findChromium = () => {
  if (process.env.CHROMIUM_PATH) {
    return process.env.CHROMIUM_PATH;
  }
  const cache = join(homedir(), "Library", "Caches", "ms-playwright");
  const shells = existsSync(cache)
    ? readdirSync(cache)
        .filter((name) => name.startsWith("chromium_headless_shell-"))
        .sort((a, b) => Number(b.split("-")[1]) - Number(a.split("-")[1]))
    : [];
  for (const shell of shells) {
    const binary = join(cache, shell, "chrome-headless-shell-mac-arm64", "chrome-headless-shell");
    if (existsSync(binary)) {
      return binary;
    }
  }
  return undefined;
};

const CONTENT_TYPES = {
  ".html": "text/html",
  ".js": "text/javascript",
  ".css": "text/css",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".webp": "image/webp",
};

const serve = (dir) =>
  new Promise((done) => {
    const server = createServer((request, response) => {
      const path = decodeURIComponent(new URL(request.url ?? "/", "http://localhost").pathname);
      const file = normalize(join(dir, path === "/" ? "index.html" : path));
      if (!file.startsWith(dir + sep) || !existsSync(file)) {
        response.writeHead(404).end();
        return;
      }
      response.writeHead(200, { "content-type": CONTENT_TYPES[extname(file)] ?? "application/octet-stream" });
      response.end(readFileSync(file));
    });
    server.listen(0, "127.0.0.1", () => done(server));
  });

const outDir = join(ROOT, ".snapshots", id);
const distDir = join(outDir, "dist");
rmSync(outDir, { recursive: true, force: true });
mkdirSync(outDir, { recursive: true });

try {
  execFileSync(join(ROOT, "node_modules", ".bin", "rspack"), ["build"], {
    cwd: ROOT,
    env: { ...process.env, DOC_ONLY: id, DOCS_OUT_DIR: distDir },
    stdio: ["ignore", "ignore", "inherit"],
  });
} catch {
  fail(`The build of "${id}" failed.`);
}

const chromium = await loadPlaywright();
const server = await serve(resolve(distDir));
const { port } = server.address();
const browser = await chromium.launch({ executablePath: findChromium() });
const errors = [];

try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  page.on("pageerror", (error) => errors.push(String(error)));
  page.on("console", (message) => {
    // The static server has no notes API (see scripts/notes-store.mjs). The viewer then shows the notes of build time.
    const fromNotesApi = message.location().url.includes("/api/notes/");
    if (message.type() === "error" && !fromNotesApi) {
      errors.push(message.text());
    }
  });

  const shoot = async (hash, file) => {
    await page.goto(`http://127.0.0.1:${port}/#${hash}`);
    await page.waitForLoadState("networkidle");
    // The zoom buttons and the notes cover parts of the document.
    await page.addStyleTag({ content: ".zoom-controls, .notes-toggle, .notes-panel { display: none; }" });
    await page.waitForTimeout(SETTLE_MS);
    await page.locator("canvas").screenshot({ path: join(outDir, file) });
    console.log(join(outDir, file));
  };

  await shoot(id, "full.png");
  for (const [index, view] of views.entries()) {
    await shoot(`${id}@${view}`, `view-${index + 1}.png`);
  }
} finally {
  await browser.close();
  server.close();
}

if (errors.length > 0) {
  fail(`The page reported errors:\n${errors.join("\n")}`);
}
