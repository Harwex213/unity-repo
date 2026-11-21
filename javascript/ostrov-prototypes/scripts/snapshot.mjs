// Screenshots of one prototype, to look at a change without opening a browser.
//
// Usage: npm run snapshot -- <id> [--size 1600x900] [--wait 1500] [view ...]
//
// - A view is a suffix of the prototype link: "?t=3.4", "#settings", "". Default: one view "".
// - Writes .snapshots/<id>/shot-<n>.png per view, n from 1.
// - Exits with code 1 when the page throws or logs an error.
// - Uses a real GPU (ANGLE on Metal), so WebGL works.
import { mkdirSync, rmSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright";
import { ROOT, fail, requirePrototypeId } from "./lib.mjs";
import { serveOne } from "./serve-one.mjs";

const args = process.argv.slice(2);
const option = (name, fallback) => {
  const index = args.indexOf(name);
  return index === -1 ? fallback : args.splice(index, 2)[1];
};
const size = option("--size", "1600x900");
const wait = Number(option("--wait", "1500"));
const [idArg, ...viewArgs] = args;
const id = requirePrototypeId(idArg);
const views = viewArgs.length > 0 ? viewArgs : [""];
const [width, height] = size.split("x").map(Number);
if (!width || !height || !Number.isFinite(wait)) {
  fail('--size is "<width>x<height>", --wait is milliseconds');
}

const outDir = join(ROOT, ".snapshots", id);
rmSync(outDir, { recursive: true, force: true });
mkdirSync(outDir, { recursive: true });

const { server, url } = await serveOne(id);
const browser = await chromium.launch({
  headless: true,
  ...(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {}),
  args: ["--use-angle=metal", "--enable-gpu", "--ignore-gpu-blocklist", "--autoplay-policy=no-user-gesture-required"],
});
const errors = [];

try {
  const page = await browser.newPage({ viewport: { width, height } });
  page.on("pageerror", (error) => errors.push(String(error).slice(0, 1500)));
  page.on("console", (message) => {
    if (message.type() === "error") {
      errors.push(message.text().slice(0, 1500));
    }
  });
  for (const [index, view] of views.entries()) {
    await page.goto(`${url}/${view}`);
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(wait);
    const file = join(outDir, `shot-${index + 1}.png`);
    await page.screenshot({ path: file });
    console.log(file);
  }
} finally {
  await browser.close();
  server.close();
}

if (errors.length > 0) {
  fail(`The page reported errors:\n${errors.join("\n")}`);
}
