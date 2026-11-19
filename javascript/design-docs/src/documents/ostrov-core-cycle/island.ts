import { drawHouse, drawTower, drawVillage } from "../../kit/glyphs";
import { offsetToPoint, traceHex } from "../../kit/hex";
import { FONT_FAMILY, PALETTE } from "../../kit/palette";
import type { TPoint, TRect } from "../../core/types";
import type { TOffset } from "../../kit/hex";

// The island of the build phase in ostrov-prototype, with the seed "Mom010".
// Each string is one row of hexes: "o" is a hex, "." is water.
// Odd rows are shifted right by half a hex.
const ISLAND_ROWS = ["ooooo", "ooooo", "oooooo", "ooooo", ".ooo", "oo.o"];

const STRONGHOLD: TOffset = { col: 2, row: 2 };
// The building whose module wheel the document shows. It stands in the top row,
// so the arrow from it to the wheel above crosses no other hex.
const MODULE_BUILDING: TOffset = { col: 4, row: 0 };
const BUILDINGS: TOffset[] = [
  MODULE_BUILDING,
  { col: 0, row: 1 },
  { col: 3, row: 1 },
  { col: 1, row: 3 },
  { col: 4, row: 2 },
];

// Buildings leave toxicity on their hexes. The tint is the acid green of the prototype.
// The prototype sets its opacity by the toxicity level; here every toxic hex has one level.
const TOXIC: TOffset[] = [
  { col: 0, row: 1 },
  { col: 4, row: 2 },
  { col: 0, row: 3 },
];

// Soil cleansing in the prototype: the player destroys one hex,
// and the toxicity of another hex drops to 0.
const TOXIC_OPACITY = 0.75;

const SACRIFICE: TOffset = { col: 0, row: 5 };
const PURIFY: TOffset = { col: 0, row: 3 };

// The new building in the build phase. The prototype shows a building being placed
// as a ghost: its art at 60% opacity on the hovered hex.
const NEW_BUILDING: TOffset = { col: 1, row: 0 };

// The village of the prototype grows the population when the player launches it.
// It has no die, so in the tax phase it shows no payout.
const VILLAGE: TOffset = { col: 0, row: 0 };

// Workers in ostrov-prototype: a building works only with all its workers in place.
// This building has 2 workers of 3.
const STAFFED_BUILDING: TOffset = { col: 3, row: 1 };
const WORKERS = { present: 2, needed: 3 };

// The tax phase of the prototype rolls the die of each building.
// The rolled face pays a resource and leaves toxicity on the hex.
// Power (власть) changes a rolled face.
// The new building of the build phase is built by now, and its rolled face was changed.
const FACE_CHANGED: TOffset = NEW_BUILDING;
// The module building pays a resource, and the toxicity of its hex grows.
const PAYOUT: TOffset = MODULE_BUILDING;

type TPayout = { cell: TOffset; resource: number; toxicity: number };

// The rolled faces of the other buildings.
const PAYOUTS: TPayout[] = [
  { cell: PAYOUT, resource: 2, toxicity: 1 },
  { cell: { col: 0, row: 1 }, resource: 1, toxicity: 1 },
  { cell: { col: 3, row: 1 }, resource: 2, toxicity: 1 },
  { cell: { col: 1, row: 3 }, resource: 3, toxicity: 2 },
  { cell: { col: 4, row: 2 }, resource: 1, toxicity: 1 },
];

// "build" shows the player's actions of the build phase: the new building,
// the sacrificed hex, the purified hex, the recruit and the workers.
// "tax" shows the payouts of the tax phase.
type TIslandState = {
  phase: "build" | "tax";
};

const ISLAND_CELLS: TOffset[] = ISLAND_ROWS.flatMap((line, row) =>
  [...line].flatMap((mark, col) => (mark === "o" ? [{ col, row }] : [])),
);

const sameCell = (a: TOffset, b: TOffset) => a.col === b.col && a.row === b.row;
const isIn = (cells: TOffset[], cell: TOffset) => cells.some((other) => sameCell(other, cell));

type TIslandLayout = {
  size: number;
  centerOf: (cell: TOffset) => TPoint;
};

// The island is centered in `area` at the largest hex size that fits.
const islandLayout = (area: TRect): TIslandLayout => {
  // The island bounds in units of one hex size.
  const unitPoints = ISLAND_CELLS.map((cell) => offsetToPoint(cell, 1));
  const xs = unitPoints.map((point) => point.x);
  const ys = unitPoints.map((point) => point.y);
  const halfWidth = Math.sqrt(3) / 2;
  const minX = Math.min(...xs) - halfWidth;
  const maxX = Math.max(...xs) + halfWidth;
  const minY = Math.min(...ys) - 1;
  const maxY = Math.max(...ys) + 1;
  const size = Math.min(area.width / (maxX - minX), area.height / (maxY - minY));

  const origin: TPoint = {
    x: area.x + area.width / 2 - ((minX + maxX) / 2) * size,
    y: area.y + area.height / 2 - ((minY + maxY) / 2) * size,
  };
  return {
    size,
    centerOf: (cell) => {
      const point = offsetToPoint(cell, size);
      return { x: origin.x + point.x, y: origin.y + point.y };
    },
  };
};

// A red dashed hex with a red cross: the hex leaves the island.
const drawSacrificed = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  traceHex(ctx, center, size);
  ctx.fillStyle = PALETTE.negativeFill;
  ctx.fill();
  ctx.setLineDash([3, 3]);
  ctx.strokeStyle = PALETTE.negative;
  ctx.lineWidth = 1.5;
  ctx.stroke();
  ctx.setLineDash([]);
  const arm = size * 0.35;
  ctx.beginPath();
  ctx.moveTo(center.x - arm, center.y - arm);
  ctx.lineTo(center.x + arm, center.y + arm);
  ctx.moveTo(center.x + arm, center.y - arm);
  ctx.lineTo(center.x - arm, center.y + arm);
  ctx.stroke();
  ctx.strokeStyle = PALETTE.ink;
};

// The toxicity of the hex changes. A line from the top corner to the bottom corner
// splits the hex in two halves: the left half has the toxicity before the change,
// the right half has the toxicity after it.
// The purified hex gets lighter. The hex of a payout gets darker.
const LOW_TOXIC_OPACITY = 0.25;

const drawToxicChange = (
  ctx: CanvasRenderingContext2D,
  center: TPoint,
  size: number,
  before: number,
  after: number,
) => {
  ctx.save();
  traceHex(ctx, center, size);
  ctx.clip();
  ctx.fillStyle = PALETTE.toxic;
  ctx.globalAlpha = before;
  ctx.fillRect(center.x - size, center.y - size, size, size * 2);
  ctx.globalAlpha = after;
  ctx.fillRect(center.x, center.y - size, size, size * 2);
  ctx.restore();

  ctx.beginPath();
  ctx.moveTo(center.x, center.y - size);
  ctx.lineTo(center.x, center.y + size);
  ctx.lineWidth = 1;
  ctx.stroke();
  traceHex(ctx, center, size);
  ctx.lineWidth = 1.5;
  ctx.stroke();
};

// A red dashed arrow from the sacrificed hex to the purified hex.
// It bends to the left, over the water, so it crosses no hex.
const drawSacrificeLink = (ctx: CanvasRenderingContext2D, from: TPoint, to: TPoint, size: number) => {
  const edge = size * (Math.sqrt(3) / 2) + size * 0.15;
  const start = { x: from.x - edge, y: from.y - size * 0.3 };
  const end = { x: to.x - edge, y: to.y + size * 0.3 };
  const bend = size * 1.6;
  ctx.strokeStyle = PALETTE.negative;
  ctx.lineWidth = 1.5;
  ctx.setLineDash([3, 3]);
  ctx.beginPath();
  ctx.moveTo(start.x, start.y);
  ctx.bezierCurveTo(start.x - bend, start.y, end.x - bend, end.y, end.x, end.y);
  ctx.stroke();
  ctx.setLineDash([]);
  // The curve ends heading right, so the head points right.
  const head = size * 0.32;
  ctx.beginPath();
  ctx.moveTo(end.x - head, end.y - head * 0.7);
  ctx.lineTo(end.x, end.y);
  ctx.lineTo(end.x - head, end.y + head * 0.7);
  ctx.stroke();
  ctx.strokeStyle = PALETTE.ink;
};

// A dashed house at 60% opacity, as the ghost in the prototype, with a plus sign at its right.
const drawNewBuilding = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  ctx.save();
  ctx.globalAlpha = 0.6;
  ctx.setLineDash([2, 2]);
  drawHouse(ctx, { x: center.x - size * 0.12, y: center.y + size * 0.05 }, size * 0.75);
  ctx.restore();

  const plus = { x: center.x + size * 0.42, y: center.y - size * 0.22 };
  const arm = size * 0.16;
  ctx.beginPath();
  ctx.moveTo(plus.x - arm, plus.y);
  ctx.lineTo(plus.x + arm, plus.y);
  ctx.moveTo(plus.x, plus.y - arm);
  ctx.lineTo(plus.x, plus.y + arm);
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.5;
  ctx.stroke();
};

// The stronghold recruits the army in the prototype: an idle person becomes a militiaman.
// A blue unit with a plus sign stands right of the tower.
const drawRecruit = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  const unit = { x: center.x + size * 0.48, y: center.y + size * 0.25 };
  ctx.beginPath();
  ctx.arc(unit.x, unit.y, size * 0.16, 0, Math.PI * 2);
  ctx.fillStyle = PALETTE.ally;
  ctx.fill();

  const plus = { x: unit.x, y: center.y - size * 0.22 };
  const arm = size * 0.13;
  ctx.beginPath();
  ctx.moveTo(plus.x - arm, plus.y);
  ctx.lineTo(plus.x + arm, plus.y);
  ctx.moveTo(plus.x, plus.y - arm);
  ctx.lineTo(plus.x, plus.y + arm);
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.25;
  ctx.stroke();
};

// The worker places under the house: a filled dot is a worker, an empty dot is a free place.
const drawWorkers = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  const radius = size * 0.09;
  const step = size * 0.3;
  const y = center.y + size * 0.58;
  for (let index = 0; index < WORKERS.needed; index++) {
    const x = center.x + (index - (WORKERS.needed - 1) / 2) * step;
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, Math.PI * 2);
    if (index < WORKERS.present) {
      ctx.fillStyle = PALETTE.ink;
      ctx.fill();
    } else {
      ctx.strokeStyle = PALETTE.ink;
      ctx.lineWidth = 1;
      ctx.stroke();
    }
  }
};

// In the tax phase a building shows its rolled face in the upper half of its hex,
// and the house moves down. All sizes are in hex sizes.
const PLATE_Y = -0.38;
const TAX_HOUSE_Y = 0.3;
const TAX_HOUSE_SIZE = 0.6;
const CHIP_WIDTH = 0.6;
const CHIP_HEIGHT = 0.42;
// The payout plate has two chips: the resource on the left, the toxicity on the right.
const PAYOUT_CHIP_X = { resource: -0.33, toxicity: 0.33 };
// The face plate has the old face on the left and the new face on the right.
const FACE_CHIP_X = { old: -0.5, new: 0.5 };
const FACE_CHIP_WIDTH = 0.46;

const drawChip = (
  ctx: CanvasRenderingContext2D,
  center: TPoint,
  size: number,
  text: string,
  options: { fill: string; width?: number; color?: string; struck?: boolean },
) => {
  const width = (options.width ?? CHIP_WIDTH) * size;
  const height = CHIP_HEIGHT * size;
  ctx.beginPath();
  ctx.roundRect(center.x - width / 2, center.y - height / 2, width, height, height * 0.25);
  ctx.fillStyle = options.fill;
  ctx.fill();
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1;
  ctx.stroke();
  ctx.font = `600 ${size * 0.28}px ${FONT_FAMILY}`;
  ctx.fillStyle = options.color ?? PALETTE.ink;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(text, center.x, center.y + size * 0.015);
  if (options.struck) {
    ctx.beginPath();
    ctx.moveTo(center.x - width * 0.34, center.y);
    ctx.lineTo(center.x + width * 0.34, center.y);
    ctx.strokeStyle = options.color ?? PALETTE.ink;
    ctx.stroke();
  }
};

// The rolled face: a resource chip and a green toxicity chip.
const drawPayoutPlate = (ctx: CanvasRenderingContext2D, center: TPoint, size: number, payout: TPayout) => {
  const y = center.y + PLATE_Y * size;
  drawChip(ctx, { x: center.x + PAYOUT_CHIP_X.resource * size, y }, size, `+${payout.resource}`, {
    fill: PALETTE.paper,
  });
  drawChip(ctx, { x: center.x + PAYOUT_CHIP_X.toxicity * size, y }, size, `+${payout.toxicity}`, {
    fill: PALETTE.toxic,
  });
};

// Power changed the rolled face: the old face is struck out, the new face pays more.
const drawFacePlate = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  const y = center.y + PLATE_Y * size;
  drawChip(ctx, { x: center.x + FACE_CHIP_X.old * size, y }, size, "+1", {
    fill: PALETTE.paper,
    width: FACE_CHIP_WIDTH,
    color: PALETTE.muted,
    struck: true,
  });
  drawChip(ctx, { x: center.x + FACE_CHIP_X.new * size, y }, size, "+3", {
    fill: PALETTE.paper,
    width: FACE_CHIP_WIDTH,
  });
  const half = size * 0.14;
  const head = size * 0.08;
  ctx.beginPath();
  ctx.moveTo(center.x - half, y);
  ctx.lineTo(center.x + half, y);
  ctx.moveTo(center.x + half - head, y - head);
  ctx.lineTo(center.x + half, y);
  ctx.lineTo(center.x + half - head, y + head);
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.25;
  ctx.stroke();
};

const taxHouseCenter = (center: TPoint, size: number): TPoint => ({ x: center.x, y: center.y + TAX_HOUSE_Y * size });

const drawHexIsland = (ctx: CanvasRenderingContext2D, area: TRect, state: TIslandState) => {
  const { size, centerOf } = islandLayout(area);
  const isBuild = state.phase === "build";
  const isTax = state.phase === "tax";

  ctx.strokeStyle = PALETTE.ink;
  ctx.lineJoin = "round";
  ctx.lineCap = "round";
  for (const cell of ISLAND_CELLS) {
    const center = centerOf(cell);
    if (isBuild && sameCell(cell, SACRIFICE)) {
      drawSacrificed(ctx, center, size);
      continue;
    }
    if (isBuild && sameCell(cell, PURIFY)) {
      drawToxicChange(ctx, center, size, TOXIC_OPACITY, LOW_TOXIC_OPACITY);
      continue;
    }
    if (isTax && sameCell(cell, PAYOUT)) {
      drawToxicChange(ctx, center, size, LOW_TOXIC_OPACITY, TOXIC_OPACITY);
      continue;
    }
    traceHex(ctx, center, size);
    if (isIn(TOXIC, cell)) {
      ctx.globalAlpha = TOXIC_OPACITY;
      ctx.fillStyle = PALETTE.toxic;
      ctx.fill();
      ctx.globalAlpha = 1;
    }
    ctx.lineWidth = 1.5;
    ctx.stroke();
  }

  drawTower(ctx, centerOf(STRONGHOLD), size * 0.9);
  drawVillage(ctx, centerOf(VILLAGE), size * 0.75);

  if (isBuild) {
    for (const cell of BUILDINGS) {
      drawHouse(ctx, centerOf(cell), size * 0.75);
    }
    ctx.strokeStyle = PALETTE.ink;
    drawSacrificeLink(ctx, centerOf(SACRIFICE), centerOf(PURIFY), size);
    drawNewBuilding(ctx, centerOf(NEW_BUILDING), size);
    drawRecruit(ctx, centerOf(STRONGHOLD), size);
    drawWorkers(ctx, centerOf(STAFFED_BUILDING), size);
  }

  if (isTax) {
    // Every building has rolled its die. The new building is built by now.
    for (const cell of [...BUILDINGS, NEW_BUILDING]) {
      drawHouse(ctx, taxHouseCenter(centerOf(cell), size), size * TAX_HOUSE_SIZE);
    }
    for (const payout of PAYOUTS) {
      drawPayoutPlate(ctx, centerOf(payout.cell), size, payout);
    }
    drawFacePlate(ctx, centerOf(FACE_CHANGED), size);
  }
};

// A point of the hex. `offset` is measured in hex sizes from the hex center.
const islandPoint = (area: TRect, cell: TOffset, offset: TPoint): TPoint => {
  const { size, centerOf } = islandLayout(area);
  const center = centerOf(cell);
  return { x: center.x + offset.x * size, y: center.y + offset.y * size };
};

const ISLAND_TARGETS = {
  toxic: { col: 0, row: 1 },
  newBuilding: NEW_BUILDING,
  sacrifice: SACRIFICE,
  stronghold: STRONGHOLD,
  staffedBuilding: STAFFED_BUILDING,
  moduleBuilding: MODULE_BUILDING,
  village: VILLAGE,
} satisfies Record<string, TOffset>;

// The arrow tips of the tax plates.
const taxPlateTips = (area: TRect, gap: number) => {
  const { size, centerOf } = islandLayout(area);
  const face = centerOf(FACE_CHANGED);
  const payout = centerOf(PAYOUT);
  const plateY = (center: TPoint) => center.y + PLATE_Y * size;
  const chipTop = (center: TPoint) => plateY(center) - (CHIP_HEIGHT / 2) * size - gap;
  return {
    // Above the middle of the face plate, over its arrow.
    face: { x: face.x, y: chipTop(face) },
    // Above the resource chip.
    resource: { x: payout.x + PAYOUT_CHIP_X.resource * size, y: chipTop(payout) },
    // Right of the toxicity chip.
    toxicity: { x: payout.x + (PAYOUT_CHIP_X.toxicity + CHIP_WIDTH / 2) * size + gap, y: plateY(payout) },
  };
};

export { ISLAND_TARGETS, drawHexIsland, islandPoint, taxPlateTips };
