import { axialToPoint } from "../../kit/hex";
import {
  WHEEL_PANEL,
  bottomOf,
  centerXOf,
  drawFortress,
  drawPhaseWheel,
  drawTurnBox,
  middleYOf,
  rightOf,
  turnBox,
} from "./hud";
import { bottomCallout, leftCallout, rightCallout, topCallout } from "./scheme";
import { STYLE, TEXT, circle, hatchHex, hex, line, paint, polygon, rect, text } from "./wire";
import type { TPhaseScheme } from "./scheme";
import type { TAxial } from "../../kit/hex";
import type { TPoint, TRect } from "../../core/types";

// Phase 4, battle: docs/spec/ui-phase-4-battle.html.
// All coordinates are screen units.

const TURN = turnBox(698, 204);

// ---------- Clouds ----------

// Poison clouds close the right and the bottom edge of the map.
// The right column has a gap: the window to retreat through.
const CLOUD_COLUMN = { x: 1296, firstY: 70, step: 62, radius: 62, shift: 16, count: 13 };
const WINDOW_CLOUDS = [4, 5];
const CLOUD_ROW = { firstX: 330, step: 95, upperY: 742, lowerY: 756, radius: 56, count: 11 };

const drawClouds = (ctx: CanvasRenderingContext2D) => {
  for (let index = 0; index < CLOUD_COLUMN.count; index++) {
    if (WINDOW_CLOUDS.includes(index)) {
      continue;
    }
    const center = {
      x: CLOUD_COLUMN.x + (index % 3) * CLOUD_COLUMN.shift,
      y: CLOUD_COLUMN.firstY + index * CLOUD_COLUMN.step,
    };
    circle(ctx, center, CLOUD_COLUMN.radius, STYLE.cloud);
  }
  for (let index = 0; index < CLOUD_ROW.count; index++) {
    const center = {
      x: CLOUD_ROW.firstX + index * CLOUD_ROW.step,
      y: index % 2 === 0 ? CLOUD_ROW.upperY : CLOUD_ROW.lowerY,
    };
    circle(ctx, center, CLOUD_ROW.radius, STYLE.cloud);
  }
};

// ---------- Islands ----------

const OWN_CENTER: TPoint = { x: 800, y: 430 };
const OWN_HEX_SIZE = 58;
const OWN_CELLS: TAxial[] = [
  { q: 0, r: 0 },
  { q: 1, r: 0 },
  { q: -1, r: 0 },
  { q: 0, r: 1 },
  { q: -1, r: 1 },
  { q: 0, r: -1 },
  { q: 1, r: -1 },
  { q: -1, r: 2 },
  { q: 0, r: 2 },
  { q: 1, r: 1 },
  { q: -2, r: 1 },
  { q: 2, r: -1 },
];
const FORTRESS_CELL: TAxial = { q: 0, r: 1 };
const OWN_UNIT_CELLS: TAxial[] = [
  { q: 0, r: 0 },
  { q: 1, r: 0 },
  { q: -1, r: 1 },
  { q: 0, r: -1 },
];
const UNIT_RADIUS = 13;
const OWN_LEFT_CELL: TAxial = { q: -2, r: 1 };

const ENEMY_CENTER: TPoint = { x: 330, y: 330 };
const ENEMY_HEX_SIZE = 46;
const ENEMY_CELLS: TAxial[] = [
  { q: 0, r: 0 },
  { q: 1, r: 0 },
  { q: 0, r: 1 },
  { q: -1, r: 1 },
  { q: 0, r: -1 },
  { q: 1, r: -1 },
  { q: -1, r: 0 },
  { q: 1, r: 1 },
];
const ENEMY_UNIT_CELLS: TAxial[] = [
  { q: 0, r: 0 },
  { q: 1, r: -1 },
  { q: -1, r: 1 },
];
const ENEMY_LEFT_CELL: TAxial = { q: -1, r: 0 };

const cellOf = (center: TPoint, size: number) => (cell: TAxial): TPoint => {
  const offset = axialToPoint(cell, size);
  return { x: center.x + offset.x, y: center.y + offset.y };
};
const ownHex = cellOf(OWN_CENTER, OWN_HEX_SIZE);
const enemyHex = cellOf(ENEMY_CENTER, ENEMY_HEX_SIZE);

// An enemy unit is a triangle.
const drawEnemyUnit = (ctx: CanvasRenderingContext2D, center: TPoint) => {
  polygon(
    ctx,
    [
      { x: center.x, y: center.y - 15 },
      { x: center.x + 14, y: center.y + 11 },
      { x: center.x - 14, y: center.y + 11 },
    ],
    STYLE.ink,
  );
};

// ---------- Field marks ----------

const ROCKS: TPoint[][] = [
  [
    { x: 1090, y: 330 },
    { x: 1190, y: 310 },
    { x: 1240, y: 400 },
    { x: 1190, y: 490 },
    { x: 1100, y: 450 },
  ],
  [
    { x: 880, y: 660 },
    { x: 960, y: 620 },
    { x: 1020, y: 680 },
    { x: 990, y: 740 },
    { x: 900, y: 740 },
  ],
];
const ROCK_TIP: TPoint = { x: 1240, y: 400 };

// The range of the selected skill around the island.
const SKILL_RANGE = 300;
const SKILL_TIP_ANGLE = (-136 * Math.PI) / 180;

// The arrow to the window in the clouds.
const WINDOW_ARROW = { from: { x: 1320, y: 340 }, to: { x: 1380, y: 340 } };
// The arrow at the edge of the screen points to the enemy beyond it.
const EDGE_ARROW = { from: { x: 500, y: 62 }, to: { x: 456, y: 96 } };

const drawArrows = (ctx: CanvasRenderingContext2D) => {
  const { from, to } = WINDOW_ARROW;
  paint(ctx, STYLE.line, () => {
    ctx.moveTo(from.x, from.y);
    ctx.lineTo(to.x, to.y);
    ctx.moveTo(to.x - 20, to.y - 16);
    ctx.lineTo(to.x, to.y);
    ctx.lineTo(to.x - 20, to.y + 16);
  });
  text(ctx, "окно", from.x + 20, from.y - 30, TEXT.bold, "center");

  const edge = EDGE_ARROW;
  paint(ctx, STYLE.line, () => {
    ctx.moveTo(edge.from.x, edge.from.y);
    ctx.lineTo(edge.to.x, edge.to.y);
    ctx.moveTo(edge.to.x, edge.to.y - 22);
    ctx.lineTo(edge.to.x, edge.to.y);
    ctx.lineTo(edge.to.x + 22, edge.to.y);
  });
};

// ---------- Panels ----------

const ARMY: TRect = { x: 13, y: 13, width: 300, height: 200 };
const ENEMY_ISLANDS: TRect = { x: 1378, y: 13, width: 208, height: 95 };
const BATTLE_CONTROL: TRect = { x: 13, y: 665, width: 317, height: 222 };
const SPEED_BUTTONS = [
  { label: "1×", x: 32, width: 46 },
  { label: "2×", x: 86, width: 46 },
  { label: "Пауза", x: 140, width: 84 },
];
const SKILLS: TRect = { x: 667, y: 775, width: 267, height: 112 };
const MANA_ICON: TRect = { x: 690, y: 800, width: 34, height: 34 };
const RESULTS: TRect = { x: 608, y: 170, width: 384, height: 560 };

const drawPanels = (ctx: CanvasRenderingContext2D) => {
  rect(ctx, ARMY, 8, STYLE.panel);
  text(ctx, "Армия", 30, 50, TEXT.bold);
  text(ctx, "6 / 6", 296, 50, TEXT.bold, "right");
  text(ctx, "Постройки · твердыня 100%", 30, 84, TEXT.small);
  rect(ctx, { x: 30, y: 104, width: 50, height: 50 }, 6, STYLE.icon);
  text(ctx, "Ополченец 6", 92, 136, TEXT.normal);

  drawTurnBox(ctx, TURN, "Фаза зачистки");

  rect(ctx, ENEMY_ISLANDS, 8, STYLE.panel);
  text(ctx, "Острова врага 1/1", 1394, 44, TEXT.small);
  text(ctx, "Остров 1", 1394, 76, TEXT.bold);
  text(ctx, "3/3", 1570, 76, TEXT.normal, "right");
  text(ctx, "8 гекс.", 1570, 98, TEXT.small, "right");

  rect(ctx, BATTLE_CONTROL, 8, STYLE.panel);
  SPEED_BUTTONS.forEach((button) => {
    rect(ctx, { x: button.x, y: 680, width: button.width, height: 34 }, 5, STYLE.panel);
    text(ctx, button.label, button.x + button.width / 2, 704, TEXT.small, "center");
  });
  for (let index = 0; index < 5; index++) {
    const y = 738 + index * 28;
    line(ctx, { x: 32, y }, { x: 310 - index * 22, y }, STYLE.textLine);
  }

  rect(ctx, SKILLS, 8, STYLE.panel);
  rect(ctx, MANA_ICON, 5, STYLE.icon);
  text(ctx, "мана", centerXOf(MANA_ICON), 862, TEXT.small, "center");
  rect(ctx, { x: 760, y: 786, width: 66, height: 66 }, 6, STYLE.strong);
  text(ctx, "Q", 793, 828, TEXT.bold, "center");
  text(ctx, "Разрушить землю", centerXOf(SKILLS), 876, TEXT.small, "center");

  drawPhaseWheel(ctx, "Готов", "Зачистка", true);

  rect(ctx, RESULTS, 8, STYLE.overlayOutline);
  text(ctx, "Итоги боя: по центру экрана", centerXOf(RESULTS), RESULTS.y + 36, TEXT.overlayBold, "center");
};

const drawScreen = (ctx: CanvasRenderingContext2D) => {
  drawClouds(ctx);
  OWN_CELLS.forEach((cell) => hex(ctx, ownHex(cell), OWN_HEX_SIZE, STYLE.hexFilled));
  drawFortress(ctx, ownHex(FORTRESS_CELL));
  ENEMY_CELLS.forEach((cell) => {
    hatchHex(ctx, enemyHex(cell), ENEMY_HEX_SIZE);
    hex(ctx, enemyHex(cell), ENEMY_HEX_SIZE, STYLE.hexFog);
  });
  OWN_UNIT_CELLS.forEach((cell) => circle(ctx, ownHex(cell), UNIT_RADIUS, STYLE.ink));
  ENEMY_UNIT_CELLS.forEach((cell) => drawEnemyUnit(ctx, enemyHex(cell)));
  ROCKS.forEach((rock) => polygon(ctx, rock, STYLE.building));
  circle(ctx, OWN_CENTER, SKILL_RANGE, STYLE.dashed);
  drawArrows(ctx);
  drawPanels(ctx);
};

const ENEMY_LEFT = enemyHex(ENEMY_LEFT_CELL);
const OWN_LEFT = ownHex(OWN_LEFT_CELL);
const SKILL_TIP: TPoint = {
  x: OWN_CENTER.x + SKILL_RANGE * Math.cos(SKILL_TIP_ANGLE),
  y: OWN_CENTER.y + SKILL_RANGE * Math.sin(SKILL_TIP_ANGLE),
};
const EDGE_ARROW_MIDDLE: TPoint = {
  x: (EDGE_ARROW.from.x + EDGE_ARROW.to.x) / 2,
  y: (EDGE_ARROW.from.y + EDGE_ARROW.to.y) / 2,
};
// The badge of "Стрелки у края" stands left of the badge of "Круг навыка".
const EDGE_ARROWS_BADGE_X = 570;
// The results callout points at the lower part of the window, under the clouds of the column.
const RESULTS_CALLOUT_Y = 700;

const battlePhase: TPhaseScheme = {
  summary:
    "Бой идёт в реальном времени. Игрок ведёт весь остров, а войска сражаются сами. Цель: зачистить " +
    "вражеские острова и пристыковать их к своему. Если на клетке нет врагов, бой пропускается.",
  drawScreen,
  callouts: [
    leftCallout(1, "Армия", { x: ARMY.x, y: middleYOf(ARMY) }),
    topCallout(2, "Ход и фаза", { x: rightOf(TURN) - 2, y: TURN.y }),
    leftCallout(3, "Вражеский остров", { x: ENEMY_LEFT.x - (ENEMY_HEX_SIZE * Math.sqrt(3)) / 2, y: ENEMY_LEFT.y }),
    leftCallout(4, "Свой остров", { x: OWN_LEFT.x - (OWN_HEX_SIZE * Math.sqrt(3)) / 2, y: OWN_LEFT.y }),
    topCallout(5, "Круг навыка", SKILL_TIP),
    rightCallout(6, "Скалы", ROCK_TIP),
    rightCallout(7, "Окно в облаках", WINDOW_ARROW.to),
    rightCallout(8, "Острова врага", { x: rightOf(ENEMY_ISLANDS), y: middleYOf(ENEMY_ISLANDS) }),
    bottomCallout(9, "Управление боем", { x: centerXOf(BATTLE_CONTROL), y: bottomOf(BATTLE_CONTROL) }),
    bottomCallout(10, "Навыки и мана", { x: MANA_ICON.x, y: bottomOf(SKILLS) }),
    rightCallout(11, "Колесо закрыто", { x: rightOf(WHEEL_PANEL), y: middleYOf(WHEEL_PANEL) }),
    bottomCallout(12, "Итоги боя", { x: rightOf(RESULTS), y: RESULTS_CALLOUT_Y }, { onAction: true }),
    topCallout(13, "Стрелки у края", EDGE_ARROW_MIDDLE, { badgeAt: EDGE_ARROWS_BADGE_X }),
  ],
};

export { battlePhase };
