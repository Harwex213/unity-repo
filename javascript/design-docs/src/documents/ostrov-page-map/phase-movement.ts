import { axialToPoint, hexArea } from "../../kit/hex";
import {
  FLASK_PANEL,
  MAIN_RESOURCES,
  MINI_MAP,
  PLAYERS,
  SPECIAL_RESOURCES,
  WHEEL_PANEL,
  bottomOf,
  bottomPanel,
  centerXOf,
  drawFlask,
  drawFortress,
  drawPhaseWheel,
  drawPlayersPanel,
  drawResourcePanels,
  drawTurnBox,
  middleYOf,
  rightOf,
  turnBox,
} from "./hud";
import { bottomCallout, leftCallout, rightCallout, topCallout } from "./scheme";
import { STYLE, TEXT, circle, hatchHex, hex, line, paint, rect, text } from "./wire";
import type { TPhaseScheme } from "./scheme";
import type { TAxial } from "../../kit/hex";
import type { TPoint, TRect } from "../../core/types";

// Phase 3, movement (the world map): docs/spec/ui-phase-3-movement.html.
// All coordinates are screen units.

const TURN = turnBox(697, 206);

// The globe: hexes around the hex of the player's island, cut by the circle of the globe.
const GLOBE_CENTER: TPoint = { x: 800, y: 450 };
const GLOBE_RADIUS = 452;
const GLOBE_HEX_SIZE = 100;
const GLOBE_HEX_RANGE = 3;

const globeHex = (cell: TAxial): TPoint => {
  const offset = axialToPoint(cell, GLOBE_HEX_SIZE);
  return { x: GLOBE_CENTER.x + offset.x, y: GLOBE_CENTER.y + offset.y };
};

const distanceOf = ({ q, r }: TAxial) => Math.max(Math.abs(q), Math.abs(r), Math.abs(q + r));

// The ring of a hex decides how it looks: the island's own hex, the hexes to fly to (dashed),
// the fog (hatched) and the plain hexes further away.
const GLOBE_CELLS = hexArea(GLOBE_HEX_RANGE);
const ringOf = (distance: number) => GLOBE_CELLS.filter((cell) => distanceOf(cell) === distance);

// Two neighbor hexes show a faction.
const FACTION_CELLS: TAxial[] = [
  { q: 0, r: 1 },
  { q: -1, r: 1 },
];
const FACTION_BADGE_RADIUS = 26;
const FOG_CELL: TAxial = { q: -1, r: -1 };

const COMPASS_CENTER: TPoint = { x: 72, y: 450 };
const COMPASS_RADIUS = 40;

const FACTIONS = bottomPanel(439, 60);
const HEX_PANEL = bottomPanel(506, 716);
const HEX_PANEL_SPLIT_X = 760;
const SCOUT_BUTTON: TRect = { x: 1000, y: 788, width: 206, height: 40 };
const FLY_BUTTON: TRect = { x: 1000, y: 836, width: 206, height: 40 };

// The mini island: five small hexes in two rows.
const MINI_HEX_SIZE = 21;
const MINI_HEXES: TPoint[] = [
  { x: 1298, y: 812 },
  { x: 1334, y: 812 },
  { x: 1280, y: 843 },
  { x: 1316, y: 843 },
  { x: 1352, y: 843 },
];

const drawGlobe = (ctx: CanvasRenderingContext2D) => {
  ctx.save();
  ctx.beginPath();
  ctx.arc(GLOBE_CENTER.x, GLOBE_CENTER.y, GLOBE_RADIUS, 0, Math.PI * 2);
  ctx.clip();
  ringOf(3).forEach((cell) => hex(ctx, globeHex(cell), GLOBE_HEX_SIZE, STYLE.hex));
  ringOf(2).forEach((cell) => {
    hatchHex(ctx, globeHex(cell), GLOBE_HEX_SIZE);
    hex(ctx, globeHex(cell), GLOBE_HEX_SIZE, STYLE.hexFog);
  });
  ringOf(1).forEach((cell) => hex(ctx, globeHex(cell), GLOBE_HEX_SIZE, STYLE.hexDashed));
  hex(ctx, GLOBE_CENTER, GLOBE_HEX_SIZE, STYLE.hexSelected);
  ctx.restore();
  circle(ctx, GLOBE_CENTER, GLOBE_RADIUS, STYLE.line);

  drawFortress(ctx, GLOBE_CENTER);
  FACTION_CELLS.forEach((cell) => {
    const center = globeHex(cell);
    circle(ctx, center, FACTION_BADGE_RADIUS, STYLE.panel);
    text(ctx, "Ф", center.x, center.y + 7, TEXT.bold, "center");
  });
};

// A decoration of an old map: a circle and a four-pointed star.
const drawCompass = (ctx: CanvasRenderingContext2D) => {
  const { x, y } = COMPASS_CENTER;
  circle(ctx, COMPASS_CENTER, COMPASS_RADIUS, STYLE.line);
  paint(ctx, STYLE.line, () => {
    ctx.moveTo(x, y - 52);
    ctx.lineTo(x + 12, y);
    ctx.lineTo(x, y + 52);
    ctx.lineTo(x - 12, y);
    ctx.closePath();
    ctx.moveTo(x - 52, y);
    ctx.lineTo(x, y - 12);
    ctx.lineTo(x + 52, y);
    ctx.lineTo(x, y + 12);
    ctx.closePath();
  });
  text(ctx, "N", x, y - 58, TEXT.small, "center");
};

const drawHexPanel = (ctx: CanvasRenderingContext2D) => {
  rect(ctx, HEX_PANEL, 8, STYLE.panel);
  text(ctx, "Гекс: что здесь", 520, 812, TEXT.bold);
  line(ctx, { x: 520, y: 832 }, { x: 730, y: 832 }, STYLE.textLine);
  line(ctx, { x: 520, y: 860 }, { x: 690, y: 860 }, STYLE.textLine);
  line(ctx, { x: HEX_PANEL_SPLIT_X, y: 790 }, { x: HEX_PANEL_SPLIT_X, y: 874 }, STYLE.textLine);
  text(ctx, "Фракция, шлейф", 780, 812, TEXT.normal);
  line(ctx, { x: 780, y: 835 }, { x: 950, y: 835 }, STYLE.textLine);
  line(ctx, { x: 780, y: 860 }, { x: 930, y: 860 }, STYLE.textLine);
  rect(ctx, SCOUT_BUTTON, 8, STYLE.panel);
  text(ctx, "Разведать гекс", centerXOf(SCOUT_BUTTON), SCOUT_BUTTON.y + 28, TEXT.normal, "center");
  rect(ctx, FLY_BUTTON, 8, STYLE.strong);
  text(ctx, "Перелететь сюда", centerXOf(FLY_BUTTON), FLY_BUTTON.y + 28, TEXT.normal, "center");
};

const drawScreen = (ctx: CanvasRenderingContext2D) => {
  drawGlobe(ctx);
  drawCompass(ctx);
  drawPlayersPanel(ctx, [1]);
  drawTurnBox(ctx, TURN, "Фаза разведки");
  drawFlask(ctx);
  drawResourcePanels(ctx);

  rect(ctx, FACTIONS, 8, STYLE.panel);
  rect(ctx, { x: 449, y: 812, width: 40, height: 40 }, 6, STYLE.panel);
  drawHexPanel(ctx);

  rect(ctx, MINI_MAP, 8, STYLE.panel);
  MINI_HEXES.forEach((center) => hex(ctx, center, MINI_HEX_SIZE, STYLE.hex));
  drawPhaseWheel(ctx, "Готов", "Разведка");
};

const HALF_HEX_WIDTH = (GLOBE_HEX_SIZE * Math.sqrt(3)) / 2;
// The middle of the upper left side of the island's own hex.
const OWN_HEX_SIDE: TPoint = {
  x: GLOBE_CENTER.x - HALF_HEX_WIDTH / 2,
  y: GLOBE_CENTER.y - (GLOBE_HEX_SIZE * 3) / 4,
};
const FLIGHT_CELL = globeHex(FACTION_CELLS[0] ?? { q: 0, r: 1 });
const FACTION_BADGE = globeHex(FACTION_CELLS[1] ?? { q: -1, r: 1 });
const FLASK_CALLOUT_Y = 300;
// The badge of "Ваш гекс" stands left of the badge of "Ход и фаза".
const OWN_HEX_BADGE_X = 864;
// The resources callout points between the main and the special resources.
const RESOURCES_TIP_X = (MAIN_RESOURCES.x + rightOf(SPECIAL_RESOURCES)) / 2;

const movementPhase: TPhaseScheme = {
  summary:
    "Игрок разведывает мир и решает, куда лететь. За ход остров делает один перелёт в соседний гекс. " +
    "Остров, который остался на месте, сбрасывает заражение в клетку.",
  drawScreen,
  callouts: [
    leftCallout(1, "Панель игроков", { x: PLAYERS.x, y: middleYOf(PLAYERS) }),
    topCallout(2, "Ход и фаза", { x: rightOf(TURN) - 3, y: TURN.y }),
    topCallout(3, "Ваш гекс", OWN_HEX_SIDE, { badgeAt: OWN_HEX_BADGE_X }),
    rightCallout(4, "Гексы для перелёта", { x: FLIGHT_CELL.x + HALF_HEX_WIDTH, y: FLIGHT_CELL.y }),
    leftCallout(5, "Туман", globeHex(FOG_CELL)),
    leftCallout(6, "Значок фракции", { x: FACTION_BADGE.x - FACTION_BADGE_RADIUS, y: FACTION_BADGE.y }),
    leftCallout(7, "Компас", { x: COMPASS_CENTER.x - COMPASS_RADIUS, y: COMPASS_CENTER.y }),
    rightCallout(8, "Заражение острова", { x: rightOf(FLASK_PANEL), y: FLASK_CALLOUT_Y }),
    bottomCallout(9, "Ресурсы", { x: RESOURCES_TIP_X, y: bottomOf(MAIN_RESOURCES) }),
    bottomCallout(10, "Фракции", { x: centerXOf(FACTIONS), y: bottomOf(FACTIONS) }),
    bottomCallout(11, "Панель гекса", { x: HEX_PANEL_SPLIT_X, y: bottomOf(HEX_PANEL) }),
    bottomCallout(12, "Кнопки действий", { x: centerXOf(FLY_BUTTON), y: bottomOf(HEX_PANEL) }, { secondLine: true }),
    bottomCallout(13, "Мини-остров", { x: centerXOf(MINI_MAP), y: bottomOf(MINI_MAP) }),
    rightCallout(14, "Колесо фаз", { x: rightOf(WHEEL_PANEL), y: middleYOf(WHEEL_PANEL) }),
  ],
};

export { movementPhase };
