// Runs the page in real time and checks what a player does: the timing, the keys, the replay.
import { chromium } from "playwright";

const url = process.argv[2];
const browser = await chromium.launch({
  headless: true,
  ...(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {}),
  args: ["--use-angle=metal", "--enable-gpu", "--ignore-gpu-blocklist"],
});

const errors = [];
const open = async (width, height, motion) => {
  const context = await browser.newContext({ viewport: { width, height }, reducedMotion: motion });
  const page = await context.newPage();
  page.on("console", (m) => m.type() === "error" && !m.text().includes("404") && errors.push(m.text().slice(0, 600)));
  page.on("pageerror", (e) => errors.push(String(e).slice(0, 600)));
  await page.goto(url);
  await page.waitForFunction(() => window.__engine !== undefined, null, { timeout: 30000 });

  return page;
};

const frames = (page, ms) => page.evaluate((duration) => new Promise((resolve) => {
  const times = [];
  const step = (now) => {
    times.push(now);
    if (now - times[0] < duration) {
      requestAnimationFrame(step);
    } else {
      const deltas = times.slice(1).map((value, index) => value - times[index]);
      resolve({
        fps: Math.round((deltas.length / (times.at(-1) - times[0])) * 1000),
        worst: Math.round(Math.max(...deltas)),
      });
    }
  };
  requestAnimationFrame(step);
}), ms);

const page = await open(2560, 1440, "no-preference");
console.log("intro 1440p", await frames(page, 2500));
console.log("blast 1440p", await frames(page, 3000));
await page.waitForSelector(".hud--visible", { timeout: 6000 });
console.log("hud at t =", (await page.evaluate(() => window.__engine.getTime())).toFixed(2));
await page.waitForTimeout(3600);
console.log("menu 1440p", await frames(page, 2000));
const active = () => page.evaluate(() => document.querySelector(".hud-entry--active .hud-entry__title").textContent);
console.log("start entry:", await active());
await page.keyboard.press("ArrowDown");
console.log("after down:", await active());
await page.keyboard.press("ArrowUp");
await page.keyboard.press("ArrowUp");
await page.keyboard.press("ArrowUp");
console.log("after 3 up (wraps):", await active());
await page.keyboard.press("ArrowRight");
console.log("difficulty:", await page.textContent(".hud-difficulty__value"));
await page.keyboard.press("Enter");
await page.waitForTimeout(300);
console.log("card:", await page.textContent(".hud-card__title"));
await page.waitForTimeout(2300);
console.log("card closed:", (await page.$(".hud-card")) === null);
await page.keyboard.press("r");
await page.waitForTimeout(400);
console.log("replay t =", (await page.evaluate(() => window.__engine.getTime())).toFixed(2), "hud hidden:", (await page.$(".hud--visible")) === null);
await page.keyboard.press("Space");
await page.waitForTimeout(300);
console.log("skip t =", (await page.evaluate(() => window.__engine.getTime())).toFixed(2), "hud:", (await page.$(".hud--visible")) !== null);
await page.context().close();

const calm = await open(1280, 720, "reduce");
await calm.waitForSelector(".hud--visible", { timeout: 3000 });
console.log("reduced motion starts in the menu, t =", (await calm.evaluate(() => window.__engine.getTime())).toFixed(2));
await calm.context().close();

console.log("errors:", errors.length ? errors : "none");
await browser.close();
