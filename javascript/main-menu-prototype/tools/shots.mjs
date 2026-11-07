// Renders chosen moments of the intro through a real GPU into art/shots.
// Usage: node tools/shots.mjs <url> <t1,t2,...> [widthxheight] [hudSeconds]
import { chromium } from "playwright";
import { mkdirSync } from "node:fs";

const [url = "http://localhost:8080", list = "0.8", size = "1920x1080", hud = ""] = process.argv.slice(2);
const [width, height] = size.split("x").map(Number);
const browser = await chromium.launch({
  headless: true,
  ...(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {}),
  args: ["--use-angle=metal", "--enable-gpu", "--ignore-gpu-blocklist"],
});
const page = await browser.newPage({ viewport: { width, height } });
page.on("console", (message) => {
  if (message.type() === "error" || message.type() === "warning") {
    console.log(`[${message.type()}]`, message.text().slice(0, 1500));
  }
});
page.on("pageerror", (error) => console.log("[pageerror]", String(error).slice(0, 1500)));
mkdirSync("art/shots", { recursive: true });
await page.goto(`${url}/?t=0`);
await page.waitForFunction(() => window.__engine !== undefined, null, { timeout: 60000 });
for (const t of list.split(",").map(Number)) {
  await page.evaluate((value) => window.__engine.freeze(value), t);
  await page.screenshot({ path: `art/shots/t${t.toFixed(2).padStart(5, "0")}.png` });
}
if (hud) {
  await page.evaluate(() => window.__showHud());
  for (const delay of hud.split(",").map(Number)) {
    await page.waitForTimeout(delay * 1000);
    await page.evaluate((value) => window.__engine.freeze(value), 9);
    await page.screenshot({ path: `art/shots/hud-${String(delay).replace(".", "_")}.png` });
  }
}
await browser.close();
