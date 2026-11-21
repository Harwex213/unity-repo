import { STYLE, TEXT, circle, hex, line, paint, polygon, rect, text } from "./wire";
import type { TPoint, TRect } from "../../core/types";

// The parts of the HUD that several phases share.
// All coordinates are screen units: the game screen is 1600 × 900, as in the SVG of the specs.

const SCREEN = { width: 1600, height: 900 };

const rightOf = (area: TRect) => area.x + area.width;
const bottomOf = (area: TRect) => area.y + area.height;
const centerXOf = (area: TRect) => area.x + area.width / 2;
const middleYOf = (area: TRect) => area.y + area.height / 2;

// ---------- Players panel ----------

const PLAYERS: TRect = { x: 13, y: 13, width: 300, height: 259 };
const PLAYER_NAMES = ["Вы", "Соперник 1", "Соперник 2", "Соперник 3"];
const PLAYER_ROW_STEP = 61;

// `readyRows` lists the rows of the players who pressed "Готов": they show a check mark.
const drawPlayersPanel = (ctx: CanvasRenderingContext2D, readyRows: number[] = []) => {
  rect(ctx, PLAYERS, 8, STYLE.panel);
  rect(ctx, { x: 22, y: 22, width: 282, height: 58 }, 6, STYLE.soft);
  PLAYER_NAMES.forEach((name, index) => {
    const top = 30 + index * PLAYER_ROW_STEP;
    rect(ctx, { x: 30, y: top, width: 11, height: 42 }, 3, STYLE.ink);
    text(ctx, name, 52, top + 18, TEXT.bold);
    [52, 110, 170].forEach((x) => line(ctx, { x, y: top + 34 }, { x: x + 40, y: top + 34 }, STYLE.textLine));
    if (readyRows.includes(index)) {
      polygon(
        ctx,
        [
          { x: 272, y: top + 22 },
          { x: 281, y: top + 31 },
          { x: 297, y: top + 11 },
        ],
        STYLE.check,
        false,
      );
    } else {
      circle(ctx, { x: 283, y: top + 22 }, 13, STYLE.line);
    }
  });
};

// ---------- Turn and phase ----------

const TURN_TOP = 13;
const TURN_HEIGHT = 91;

const turnBox = (x: number, width: number): TRect => ({ x, y: TURN_TOP, width, height: TURN_HEIGHT });

const drawTurnBox = (ctx: CanvasRenderingContext2D, area: TRect, phase: string) => {
  rect(ctx, area, 8, STYLE.panel);
  text(ctx, "Ход 1", centerXOf(area), area.y + 41, TEXT.heading, "center");
  text(ctx, phase, centerXOf(area), area.y + 73, TEXT.normal, "center");
};

// ---------- Infection flask ----------

const FLASK_PANEL: TRect = { x: 1463, y: 13, width: 123, height: 515 };

// The flask holds 1000 points of infection and has marks at 250, 500 and 800.
const drawFlask = (ctx: CanvasRenderingContext2D) => {
  rect(ctx, FLASK_PANEL, 8, STYLE.panel);
  rect(ctx, { x: 1508, y: 80, width: 34, height: 388 }, 16, STYLE.panel);
  rect(ctx, { x: 1516, y: 68, width: 18, height: 12 }, 3, STYLE.panel);
  [157, 271, 368].forEach((y) => line(ctx, { x: 1510, y }, { x: 1540, y }, STYLE.textLine));
  rect(ctx, { x: 1511, y: 440, width: 28, height: 26 }, 8, STYLE.soft);
  text(ctx, "0/1000", 1525, 492, TEXT.small, "center");
  text(ctx, "статус зоны", 1525, 514, TEXT.small, "center");
};

// ---------- Resources ----------

const BOTTOM_BAR_TOP = 777;
const BOTTOM_BAR_HEIGHT = 110;
const bottomPanel = (x: number, width: number): TRect => ({ x, y: BOTTOM_BAR_TOP, width, height: BOTTOM_BAR_HEIGHT });

const MAIN_RESOURCES = bottomPanel(13, 242);
const SPECIAL_RESOURCES = bottomPanel(262, 167);

// A resource is an icon and a placeholder of its number.
const drawResourceGrid = (
  ctx: CanvasRenderingContext2D,
  area: TRect,
  inset: number,
  columns: number,
  step: number,
) => {
  rect(ctx, area, 8, STYLE.panel);
  for (let column = 0; column < columns; column++) {
    [800, 842].forEach((top) => {
      const x = area.x + inset + column * step;
      rect(ctx, { x, y: top, width: 26, height: 26 }, 5, STYLE.icon);
      line(ctx, { x: x + 34, y: top + 16 }, { x: x + 58, y: top + 16 }, STYLE.textLine);
    });
  }
};

const drawResourcePanels = (ctx: CanvasRenderingContext2D) => {
  drawResourceGrid(ctx, MAIN_RESOURCES, 15, 3, 70);
  drawResourceGrid(ctx, SPECIAL_RESOURCES, 16, 2, 74);
};

// ---------- Mini globe ----------

const MINI_MAP = bottomPanel(1261, 110);

const drawMiniGlobe = (ctx: CanvasRenderingContext2D, disabled = false) => {
  const style = disabled ? STYLE.disabled : STYLE.line;
  const center = { x: centerXOf(MINI_MAP), y: middleYOf(MINI_MAP) };
  rect(ctx, MINI_MAP, 8, disabled ? STYLE.disabled : STYLE.panel);
  circle(ctx, center, 45, style);
  paint(ctx, disabled ? style : STYLE.textLine, () => {
    ctx.moveTo(center.x - 45, center.y);
    ctx.quadraticCurveTo(center.x, center.y + 22, center.x + 45, center.y);
    ctx.moveTo(center.x, center.y - 45);
    ctx.quadraticCurveTo(center.x - 22, center.y, center.x, center.y + 45);
  });
};

// ---------- Phase wheel ----------

const WHEEL_PANEL: TRect = { x: 1381, y: 614, width: 205, height: 273 };
const WHEEL_CENTER: TPoint = { x: 1484, y: 758 };
const WHEEL_RADIUS = 80;

// Four phases on a circle. The current phase is the top quarter.
// A disabled wheel has a dotted outline: the button does not work while the phase lasts.
const drawPhaseWheel = (ctx: CanvasRenderingContext2D, button: string, caption: string, disabled = false) => {
  rect(ctx, WHEEL_PANEL, 8, disabled ? STYLE.disabled : STYLE.panel);
  rect(ctx, { x: 1428, y: 625, width: 112, height: 36 }, 7, disabled ? STYLE.disabled : STYLE.strong);
  text(ctx, button, 1484, 651, TEXT.bold, "center");
  circle(ctx, WHEEL_CENTER, WHEEL_RADIUS, disabled ? STYLE.disabled : STYLE.panel);
  paint(ctx, STYLE.soft, () => {
    ctx.moveTo(WHEEL_CENTER.x, WHEEL_CENTER.y);
    ctx.arc(WHEEL_CENTER.x, WHEEL_CENTER.y, WHEEL_RADIUS, (-3 * Math.PI) / 4, -Math.PI / 4);
    ctx.closePath();
  });
  const reach = WHEEL_RADIUS * Math.SQRT1_2;
  line(
    ctx,
    { x: WHEEL_CENTER.x - reach, y: WHEEL_CENTER.y - reach },
    { x: WHEEL_CENTER.x + reach, y: WHEEL_CENTER.y + reach },
    STYLE.line,
  );
  line(
    ctx,
    { x: WHEEL_CENTER.x + reach, y: WHEEL_CENTER.y - reach },
    { x: WHEEL_CENTER.x - reach, y: WHEEL_CENTER.y + reach },
    STYLE.line,
  );
  text(ctx, caption, 1484, 876, TEXT.normal, "center");
};

// ---------- Island field ----------

// The island of phases 1 and 2: rows of pointy-top hexes, each row with its own start.
const ISLAND_HEX_SIZE = 63;
const ISLAND_HEX_STEP = ISLAND_HEX_SIZE * Math.sqrt(3);
const ISLAND_ROW_STEP = 95;
const ISLAND_TOP_ROW_Y = 150;
const ISLAND_ROWS = [
  { firstX: 522, count: 5 },
  { firstX: 460, count: 6 },
  { firstX: 522, count: 6 },
  { firstX: 554, count: 5 },
  { firstX: 585, count: 4 },
  { firstX: 695, count: 3 },
];

const islandHex = (row: number, column: number): TPoint => ({
  x: (ISLAND_ROWS[row]?.firstX ?? 0) + column * ISLAND_HEX_STEP,
  y: ISLAND_TOP_ROW_Y + row * ISLAND_ROW_STEP,
});

const drawIslandField = (ctx: CanvasRenderingContext2D) => {
  ISLAND_ROWS.forEach((row, rowIndex) => {
    for (let column = 0; column < row.count; column++) {
      hex(ctx, islandHex(rowIndex, column), ISLAND_HEX_SIZE, STYLE.hex);
    }
  });
};

// ---------- Buildings on a hex ----------

// The fortress: a wall with three teeth. `center` is the center of its hex.
const drawFortress = (ctx: CanvasRenderingContext2D, center: TPoint) => {
  const left = center.x - 28;
  const bottom = center.y + 14;
  polygon(
    ctx,
    [
      { x: left, y: bottom },
      { x: left, y: bottom - 34 },
      { x: left + 12, y: bottom - 34 },
      { x: left + 12, y: bottom - 24 },
      { x: left + 22, y: bottom - 24 },
      { x: left + 22, y: bottom - 34 },
      { x: left + 34, y: bottom - 34 },
      { x: left + 34, y: bottom - 24 },
      { x: left + 44, y: bottom - 24 },
      { x: left + 44, y: bottom - 34 },
      { x: left + 56, y: bottom - 34 },
      { x: left + 56, y: bottom },
    ],
    STYLE.building,
  );
};

// A house: the shape of every other building.
const housePoints = (left: number, bottom: number, half: number, wall: number, roof: number): TPoint[] => [
  { x: left, y: bottom },
  { x: left, y: bottom - wall },
  { x: left + half, y: bottom - wall - roof },
  { x: left + half * 2, y: bottom - wall },
  { x: left + half * 2, y: bottom },
];

const drawHouse = (ctx: CanvasRenderingContext2D, center: TPoint, alpha = 1) => {
  polygon(ctx, housePoints(center.x - 24, center.y + 14, 24, 24, 18), { ...STYLE.building, alpha });
};

const PLATE = { width: 100, height: 18, top: 26 };

// The plate under a building: workers or soldiers, infection of the hex.
// A plate with a forecast is wider, so its text stays inside.
const plateOf = (center: TPoint, width: number = PLATE.width): TRect => ({
  x: center.x - width / 2,
  y: center.y + PLATE.top,
  width,
  height: PLATE.height,
});

const drawPlate = (ctx: CanvasRenderingContext2D, center: TPoint, value: string, width: number = PLATE.width) => {
  rect(ctx, plateOf(center, width), 3, STYLE.plate);
  text(ctx, value, center.x, center.y + PLATE.top + 14, TEXT.small, "center");
};

// A small plate above a building, `lift` units above the center of its hex.
const drawTag = (ctx: CanvasRenderingContext2D, center: TPoint, value: string, width: number, lift: number) => {
  rect(ctx, { x: center.x - width / 2, y: center.y - lift, width, height: 24 }, 4, STYLE.plate);
  text(ctx, value, center.x, center.y - lift + 18, TEXT.small, "center");
};

const drawBuildingCard = (ctx: CanvasRenderingContext2D, x: number) => {
  polygon(ctx, housePoints(x + 22, 847, 18, 22, 13), STYLE.line);
};

export {
  BOTTOM_BAR_TOP,
  FLASK_PANEL,
  ISLAND_HEX_SIZE,
  MAIN_RESOURCES,
  MINI_MAP,
  PLAYERS,
  SCREEN,
  SPECIAL_RESOURCES,
  WHEEL_PANEL,
  bottomOf,
  bottomPanel,
  centerXOf,
  drawBuildingCard,
  drawFlask,
  drawFortress,
  drawHouse,
  drawIslandField,
  drawMiniGlobe,
  drawPhaseWheel,
  drawPlate,
  drawPlayersPanel,
  drawResourcePanels,
  drawTag,
  drawTurnBox,
  islandHex,
  middleYOf,
  plateOf,
  rightOf,
  turnBox,
};
