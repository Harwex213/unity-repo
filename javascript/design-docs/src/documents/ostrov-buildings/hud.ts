import { drawText, drawTextInRect } from "../../kit/shapes";
import { PALETTE } from "./palette";
import { bottomPanel, onScreen } from "./screen";
import { STYLE, centerXOf, circle, line, middleYOf, paint, rect } from "./wire";
import type { TRect } from "../../core/types";

// The parts of the HUD the document is not about. They are light grey.
// Their places follow the build phase scheme in docs/spec/ui-phase-1-build.html.
// The contamination flask stands under the selected building panel: the open panel covers it,
// as the hex card does in the prototype.

const DIM_TEXT = { color: PALETTE.dimText };

// ---------- Players panel ----------

const PLAYERS = onScreen(13, 13, 280, 236);
const PLAYER_NAMES = ["Вы", "Соперник 1", "Соперник 2", "Соперник 3"];
const PLAYER_ROW_STEP = 56;

const drawPlayersPanel = (ctx: CanvasRenderingContext2D) => {
  rect(ctx, PLAYERS, 8, STYLE.dimPanel);
  PLAYER_NAMES.forEach((name, index) => {
    const top = PLAYERS.y + 14 + index * PLAYER_ROW_STEP;
    rect(ctx, { x: PLAYERS.x + 14, y: top, width: 9, height: 40 }, 3, STYLE.dimFill);
    drawText(ctx, name, PLAYERS.x + 36, top, { ...DIM_TEXT, size: 18, weight: 600 });
    [36, 92, 148].forEach((x) =>
      line(ctx, { x: PLAYERS.x + x, y: top + 33 }, { x: PLAYERS.x + x + 40, y: top + 33 }, STYLE.dimLine),
    );
    circle(ctx, { x: PLAYERS.x + 250, y: top + 20 }, 11, STYLE.dimLine);
  });
};

// ---------- Turn and phase ----------

const TURN = onScreen(600, 13, 240, 80);

const drawTurnBox = (ctx: CanvasRenderingContext2D) => {
  rect(ctx, TURN, 8, STYLE.dimPanel);
  drawText(ctx, "Ход 1", centerXOf(TURN), TURN.y + 10, { ...DIM_TEXT, size: 26, weight: 700, align: "center" });
  drawText(ctx, "Фаза строительства", centerXOf(TURN), TURN.y + 48, { ...DIM_TEXT, size: 16, align: "center" });
};

// ---------- Resources and tools ----------

const MAIN_RESOURCES = bottomPanel(13, 242);
const SPECIAL_RESOURCES = bottomPanel(262, 167);
const TOOLS = bottomPanel(436, 50);

// A resource is an icon and a placeholder of its number.
const drawResourceGrid = (ctx: CanvasRenderingContext2D, area: TRect, columns: number, step: number) => {
  rect(ctx, area, 8, STYLE.dimPanel);
  for (let column = 0; column < columns; column++) {
    [23, 65].forEach((top) => {
      const x = area.x + 15 + column * step;
      rect(ctx, { x, y: area.y + top, width: 24, height: 24 }, 5, STYLE.dimLine);
      line(ctx, { x: x + 32, y: area.y + top + 14 }, { x: x + 54, y: area.y + top + 14 }, STYLE.dimLine);
    });
  }
};

// The tools: "Снести здание" and "Технологии".
const drawTools = (ctx: CanvasRenderingContext2D) => {
  rect(ctx, TOOLS, 8, STYLE.dimPanel);
  [12, 60].forEach((top) => rect(ctx, { x: TOOLS.x + 9, y: TOOLS.y + top, width: 32, height: 38 }, 5, STYLE.dimLine));
};

// ---------- Mini globe ----------

const MINI_GLOBE = bottomPanel(1237, 110);

const drawMiniGlobe = (ctx: CanvasRenderingContext2D) => {
  const center = { x: centerXOf(MINI_GLOBE), y: middleYOf(MINI_GLOBE) };
  const radius = 40;
  rect(ctx, MINI_GLOBE, 8, STYLE.dimPanel);
  circle(ctx, center, radius, STYLE.dimLine);
  paint(ctx, STYLE.dimLine, () => {
    ctx.moveTo(center.x - radius, center.y);
    ctx.quadraticCurveTo(center.x, center.y + radius / 2, center.x + radius, center.y);
    ctx.moveTo(center.x, center.y - radius);
    ctx.quadraticCurveTo(center.x - radius / 2, center.y, center.x, center.y + radius);
  });
};

// ---------- Phase wheel ----------

const WHEEL_PANEL = onScreen(1380, 668, 207, 219);
const WHEEL_RADIUS = 62;

// Four phases on a circle. The current phase is the top quarter.
const drawPhaseWheel = (ctx: CanvasRenderingContext2D) => {
  rect(ctx, WHEEL_PANEL, 8, STYLE.dimPanel);
  const button: TRect = { x: centerXOf(WHEEL_PANEL) - 50, y: WHEEL_PANEL.y + 10, width: 100, height: 30 };
  rect(ctx, button, 6, STYLE.dimLine);
  drawTextInRect(ctx, "Готов", button, { ...DIM_TEXT, size: 16, weight: 700 });
  const center = { x: centerXOf(WHEEL_PANEL), y: WHEEL_PANEL.y + 112 };
  circle(ctx, center, WHEEL_RADIUS, STYLE.dimLine);
  paint(ctx, STYLE.dimFill, () => {
    ctx.moveTo(center.x, center.y);
    ctx.arc(center.x, center.y, WHEEL_RADIUS, (-3 * Math.PI) / 4, -Math.PI / 4);
    ctx.closePath();
  });
  const reach = WHEEL_RADIUS * Math.SQRT1_2;
  line(ctx, { x: center.x - reach, y: center.y - reach }, { x: center.x + reach, y: center.y + reach }, STYLE.dimLine);
  line(ctx, { x: center.x + reach, y: center.y - reach }, { x: center.x - reach, y: center.y + reach }, STYLE.dimLine);
  drawText(ctx, "Строительство", center.x, WHEEL_PANEL.y + 186, { ...DIM_TEXT, size: 15, align: "center" });
};

const drawDimHud = (ctx: CanvasRenderingContext2D) => {
  drawPlayersPanel(ctx);
  drawTurnBox(ctx);
  drawResourceGrid(ctx, MAIN_RESOURCES, 3, 72);
  drawResourceGrid(ctx, SPECIAL_RESOURCES, 2, 76);
  drawTools(ctx);
  drawMiniGlobe(ctx);
  drawPhaseWheel(ctx);
};

export { drawDimHud };
