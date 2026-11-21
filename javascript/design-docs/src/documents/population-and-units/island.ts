import { offsetToPoint, traceHex } from "../../kit/hex";
import { drawImageContain } from "../../kit/image";
import { BUILDING_ART } from "./art";
import { DIM_ALPHA, PALETTE } from "./palette";
import { MAP_PLATE, drawWorkersPlate } from "./workers-plate";
import type { TBuildingId } from "./art";
import type { TPoint, TRect } from "../../core/types";
import type { TOffset } from "../../kit/hex";

// The island of both phase screens: the same hexes and buildings.
// Each string is one row of hexes: "o" is a hex, "." is water. Odd rows are shifted right by half a hex.
const ISLAND_ROWS = ["ooooo", "ooooo", "ooooo", ".ooo"];

// The hex size: the distance from the center to a corner.
const HEX_SIZE = 56;
const HALF_HEX_WIDTH = (Math.sqrt(3) / 2) * HEX_SIZE;

// The buildings of the population system stand in the bottom row,
// so the arrows from the fragments under a screen reach them without crossing other hexes.
const FARM: TOffset = { col: 1, row: 3 };
const VILLAGE: TOffset = { col: 2, row: 3 };
const BARRACKS: TOffset = { col: 3, row: 3 };

const BUILDINGS: { cell: TOffset; id: TBuildingId; isDim: boolean }[] = [
  { cell: FARM, id: "farm", isDim: false },
  { cell: VILLAGE, id: "village", isDim: false },
  { cell: BARRACKS, id: "barracks", isDim: false },
  // Buildings of other systems.
  { cell: { col: 0, row: 0 }, id: "sawmill", isDim: true },
  { cell: { col: 2, row: 0 }, id: "mine", isDim: true },
  { cell: { col: 1, row: 1 }, id: "university", isDim: true },
  { cell: { col: 4, row: 1 }, id: "masonsGuild", isDim: true },
  { cell: { col: 0, row: 2 }, id: "sawmill", isDim: true },
  { cell: { col: 3, row: 2 }, id: "observatory", isDim: true },
];

const ISLAND_CELLS: TOffset[] = ISLAND_ROWS.flatMap((line, row) =>
  [...line].flatMap((mark, col) => (mark === "o" ? [{ col, row }] : [])),
);

// The island bounds around the center of the hex (0, 0).
const unitPoints = ISLAND_CELLS.map((cell) => offsetToPoint(cell, HEX_SIZE));
const MIN_X = Math.min(...unitPoints.map((point) => point.x)) - HALF_HEX_WIDTH;
const MAX_X = Math.max(...unitPoints.map((point) => point.x)) + HALF_HEX_WIDTH;
const MIN_Y = Math.min(...unitPoints.map((point) => point.y)) - HEX_SIZE;
const MAX_Y = Math.max(...unitPoints.map((point) => point.y)) + HEX_SIZE;
const ISLAND_SIZE = { width: MAX_X - MIN_X, height: MAX_Y - MIN_Y };

// The center of a hex of the island whose bounds start at `topLeft`.
const hexCenter = (topLeft: TPoint, cell: TOffset): TPoint => {
  const point = offsetToPoint(cell, HEX_SIZE);
  return { x: topLeft.x - MIN_X + point.x, y: topLeft.y - MIN_Y + point.y };
};

// The bottom corner of a hex: arrows from below end here.
const hexBottom = (topLeft: TPoint, cell: TOffset): TPoint => {
  const center = hexCenter(topLeft, cell);
  return { x: center.x, y: center.y + HEX_SIZE };
};

// The workers plate of a building, as the prototype draws it: at the lower edge of the hex.
// It is a bit wider than the hex, but it stays clear of the hex to the right:
// the lower left edge of that hex starts right of the plate.
const PLATE_WIDTH = 116;
const PLATE_TOP = 0.68 * HEX_SIZE;

const workersPlateRect = (topLeft: TPoint, cell: TOffset): TRect => {
  const center = hexCenter(topLeft, cell);
  return { x: center.x - PLATE_WIDTH / 2, y: center.y + PLATE_TOP, width: PLATE_WIDTH, height: MAP_PLATE.height };
};

// The art covers most of the hex and leaves its corners free, as in the Ostrov core cycle.
const ART_SIDE = 1.45 * HEX_SIZE;

const drawBuildingOnHex = (ctx: CanvasRenderingContext2D, id: TBuildingId, center: TPoint, size: number) => {
  const side = (ART_SIDE / HEX_SIZE) * size;
  drawImageContain(ctx, BUILDING_ART[id], {
    x: center.x - side / 2,
    y: center.y - 0.05 * size - side / 2,
    width: side,
    height: side,
  });
};

// `selected` gets a bold outline: the player has opened the panel of this building.
// `staffed` buildings show their workers plate.
const drawIsland = (
  ctx: CanvasRenderingContext2D,
  topLeft: TPoint,
  options: { selected?: TOffset; staffed?: TOffset[] } = {},
) => {
  for (const cell of ISLAND_CELLS) {
    traceHex(ctx, hexCenter(topLeft, cell), HEX_SIZE);
    ctx.fillStyle = PALETTE.paper;
    ctx.fill();
    ctx.strokeStyle = PALETTE.ink;
    ctx.lineWidth = 1.5;
    ctx.stroke();
  }

  for (const building of BUILDINGS) {
    ctx.save();
    ctx.globalAlpha = building.isDim ? DIM_ALPHA : 1;
    drawBuildingOnHex(ctx, building.id, hexCenter(topLeft, building.cell), HEX_SIZE);
    ctx.restore();
  }

  if (options.selected) {
    traceHex(ctx, hexCenter(topLeft, options.selected), HEX_SIZE);
    ctx.strokeStyle = PALETTE.ink;
    ctx.lineWidth = 4;
    ctx.stroke();
  }
  for (const cell of options.staffed ?? []) {
    drawWorkersPlate(ctx, workersPlateRect(topLeft, cell), MAP_PLATE);
  }
};

export { BARRACKS, FARM, ISLAND_SIZE, VILLAGE, drawBuildingOnHex, drawIsland, hexBottom, workersPlateRect };
