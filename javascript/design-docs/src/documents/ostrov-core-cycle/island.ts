import { offsetToPoint, traceHex } from "../../kit/hex";
import { createImageAsset, drawImageContain } from "../../kit/image";
import { FONT_FAMILY } from "../../kit/palette";
import { drawText } from "../../kit/shapes";
import { PALETTE } from "./palette";
import converterArt from "./assets/converter.png";
import farmArt from "./assets/farm.png";
import masonsGuildArt from "./assets/masons-guild.png";
import mineArt from "./assets/mine.png";
import observatoryArt from "./assets/observatory.png";
import sawmillArt from "./assets/sawmill.png";
import strongholdArt from "./assets/stronghold.png";
import universityArt from "./assets/university.png";
import villageArt from "./assets/village.png";
import type { TImageAsset } from "../../kit/image";
import type { TPoint, TRect } from "../../core/types";
import type { TOffset } from "../../kit/hex";

// The island of the build phase in ostrov-prototype, with the seed "Mom010".
// Each string is one row of hexes: "o" is a hex, "." is water.
// Odd rows are shifted right by half a hex.
const ISLAND_ROWS = ["ooooo", "ooooo", "oooooo", "ooooo", ".ooo", "oo.o"];

// The art comes from ostrov-prototype (src/assets/hex-buildings): the 256px sprites
// the island canvas of the prototype draws on hexes. Every resource has its building:
// the sawmill gives wood, the mine stone, the farm food, the village population,
// the masons guild mechanisms (молотки in the prototype), the university science,
// the observatory electricity (разведка in the prototype, 🔭 in the resource list),
// the converter mana. The converter is the only mana building of the prototype.
const BUILDING_ART = {
  sawmill: createImageAsset(sawmillArt),
  mine: createImageAsset(mineArt),
  farm: createImageAsset(farmArt),
  village: createImageAsset(villageArt),
  masonsGuild: createImageAsset(masonsGuildArt),
  university: createImageAsset(universityArt),
  observatory: createImageAsset(observatoryArt),
  converter: createImageAsset(converterArt),
};
type TBuildingArt = keyof typeof BUILDING_ART;
// The stronghold is not one of the buildings in the prototype: it cannot be built or demolished.
const STRONGHOLD_ART = createImageAsset(strongholdArt);

const ISLAND_ART_ASSETS = [...Object.values(BUILDING_ART), STRONGHOLD_ART];

const STRONGHOLD: TOffset = { col: 2, row: 2 };
// The village of the prototype grows the population when the player launches it.
// It has no die, so in the tax phase it shows no payout.
const VILLAGE: TOffset = { col: 0, row: 0 };
// The farm stands next to the village: the population eats the food.
const FARM: TOffset = { col: 0, row: 1 };
// The building whose module wheel the document shows. It stands in the top row,
// so the arrow from it to the wheel above crosses no other hex.
const MODULE_BUILDING: TOffset = { col: 4, row: 0 };
// The toxic building that the toxicity label points at. It stands in the left column,
// so the arrow from the left crosses no other hex.
const TOXIC_BUILDING: TOffset = { col: 0, row: 2 };
// The electricity building stands at the right edge of the island,
// between the mechanisms building above and the science building below.
const ELECTRICITY: TOffset = { col: 5, row: 2 };
const MECHANISMS: TOffset = { col: 4, row: 1 };
const SCIENCE: TOffset = { col: 4, row: 3 };

// Battle damage in ostrov-prototype (src/core/structure-hp.ts): monsters of the cleanup phase
// hit buildings. The repair action of the build phase points at the village.
const DAMAGED_BUILDING: TOffset = VILLAGE;

// Workers in ostrov-prototype: a building works only with all its workers in place.
// This building has 2 workers of 3.
const STAFFED_BUILDING: TOffset = { col: 3, row: 1 };
const WORKERS = { present: 2, needed: 3 };

// The buildings of the island. The village and the stronghold are drawn apart.
const BUILDINGS: { cell: TOffset; art: TBuildingArt }[] = [
  { cell: MODULE_BUILDING, art: "mine" },
  { cell: FARM, art: "farm" },
  { cell: STAFFED_BUILDING, art: "converter" },
  { cell: MECHANISMS, art: "masonsGuild" },
  { cell: TOXIC_BUILDING, art: "sawmill" },
  { cell: ELECTRICITY, art: "observatory" },
  { cell: SCIENCE, art: "university" },
];

// Buildings leave toxicity on their hexes. The tint is the acid green of the prototype.
// The prototype sets its opacity by the toxicity level; here every toxic hex has one level.
const TOXIC: TOffset[] = [TOXIC_BUILDING, ELECTRICITY, { col: 0, row: 3 }];

// Soil cleansing in the prototype: the player destroys one hex,
// and the toxicity of another hex drops to 0.
const TOXIC_OPACITY = 0.75;

const SACRIFICE: TOffset = { col: 0, row: 5 };
const PURIFY: TOffset = { col: 0, row: 3 };

// The new building in the build phase. The prototype shows a building being placed
// as a ghost: its art at 60% opacity on the hovered hex. The new building is a second sawmill.
const NEW_BUILDING: TOffset = { col: 1, row: 0 };
const NEW_BUILDING_ART: TBuildingArt = "sawmill";

// The tax phase of the prototype rolls the die of each building.
// The rolled face pays a resource and leaves toxicity on the hex.
// Power (власть) changes a rolled face.
// The new building of the build phase is built by now, and its rolled face was changed.
const FACE_CHANGED: TOffset = NEW_BUILDING;
// The module building pays a resource, and the toxicity of its hex grows.
const PAYOUT: TOffset = MODULE_BUILDING;

type TPayout = { cell: TOffset; resource: number; toxicity: number };

// The rolled faces of the other buildings, from their dice in the prototype (src/core/buildings.ts).
// The converter leaves no toxicity.
const PAYOUTS: TPayout[] = [
  { cell: PAYOUT, resource: 2, toxicity: 1 },
  { cell: FARM, resource: 5, toxicity: 3 },
  { cell: STAFFED_BUILDING, resource: 2, toxicity: 0 },
  { cell: MECHANISMS, resource: 1, toxicity: 1 },
  { cell: TOXIC_BUILDING, resource: 3, toxicity: 1 },
  { cell: ELECTRICITY, resource: 2, toxicity: 2 },
  { cell: SCIENCE, resource: 3, toxicity: 3 },
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

// The art of a building fills a square around a point of its hex.
// `x`, `y` and `size` are in hex sizes: the offset of the square center from the hex center
// and the side of the square. The sprites have a few transparent pixels on every side.
type TArtPlace = { x: number; y: number; size: number };

// The usual place: the art covers most of the hex and leaves its corners free.
const ART_PLACE: TArtPlace = { x: 0, y: -0.05, size: 1.45 };
// The ghost and the stronghold move left, so the plus sign and the recruit fit on the right.
const GHOST_PLACE: TArtPlace = { x: -0.14, y: 0.02, size: 1.25 };
const RECRUIT_STRONGHOLD_PLACE: TArtPlace = { x: -0.2, y: -0.05, size: 1.3 };
// The staffed building moves up, so the worker dots fit under it.
const STAFFED_PLACE: TArtPlace = { x: 0, y: -0.12, size: 1.15 };

const drawArt = (ctx: CanvasRenderingContext2D, asset: TImageAsset, center: TPoint, size: number, place: TArtPlace) => {
  const side = place.size * size;
  drawImageContain(ctx, asset, {
    x: center.x + place.x * size - side / 2,
    y: center.y + place.y * size - side / 2,
    width: side,
    height: side,
  });
};

// A dashed arrow between two hexes. It bends to one side, over the water, so it crosses no hex.
// `bend` is signed: below 0 the curve bends to the left, above 0 to the right.
// The curve ends heading horizontally, so the head points right or left.
const drawDashedLink = (
  ctx: CanvasRenderingContext2D,
  start: TPoint,
  end: TPoint,
  bend: number,
  options: { size: number; color: string },
) => {
  ctx.strokeStyle = options.color;
  ctx.lineWidth = 1.5;
  ctx.setLineDash([3, 3]);
  ctx.beginPath();
  ctx.moveTo(start.x, start.y);
  ctx.bezierCurveTo(start.x + bend, start.y, end.x + bend, end.y, end.x, end.y);
  ctx.stroke();
  ctx.setLineDash([]);
  const head = options.size * 0.32 * Math.sign(bend);
  ctx.beginPath();
  ctx.moveTo(end.x + head, end.y - Math.abs(head) * 0.7);
  ctx.lineTo(end.x, end.y);
  ctx.lineTo(end.x + head, end.y + Math.abs(head) * 0.7);
  ctx.stroke();
  ctx.strokeStyle = PALETTE.ink;
};

// The distance from the hex center to a point just outside its left or right edge.
const sideEdge = (size: number) => size * (Math.sqrt(3) / 2) + size * 0.15;

// A red dashed arrow from the sacrificed hex to the purified hex, over the water on the left.
const drawSacrificeLink = (ctx: CanvasRenderingContext2D, from: TPoint, to: TPoint, size: number) => {
  const edge = sideEdge(size);
  drawDashedLink(
    ctx,
    { x: from.x - edge, y: from.y - size * 0.3 },
    { x: to.x - edge, y: to.y + size * 0.3 },
    -size * 1.6,
    { size, color: PALETTE.negative },
  );
};

const LINK_CAPTION = { size: 12, lineHeight: 15 };

// The population eats the food: a dashed arrow from the farm up to the village,
// over the water left of the island. The caption stands left of the curve.
const drawFoodLink = (ctx: CanvasRenderingContext2D, farm: TPoint, village: TPoint, size: number) => {
  const edge = sideEdge(size);
  const start = { x: farm.x - edge, y: farm.y - size * 0.25 };
  const end = { x: village.x - edge, y: village.y + size * 0.25 };
  const bend = -size;
  drawDashedLink(ctx, start, end, bend, { size, color: PALETTE.ink });
  // The curve reaches about 3/4 of the bend left of its ends. The caption stands close to the curve,
  // so its widest line stays clear of the phase box on the left.
  const text = "🧍 население\nпотребляет\n🍗 еду";
  const lines = text.split("\n").length;
  drawText(ctx, text, end.x + bend * 0.75 + 1, (start.y + end.y) / 2 - (lines * LINK_CAPTION.lineHeight) / 2, {
    ...LINK_CAPTION,
    align: "right",
  });
};

// Electricity is needed for a building to work: dashed arrows from the electricity building
// to the mechanisms building above and the science building below, over the water right of the island.
// The caption stands under the arrows, in the water at the lower right of the island,
// low enough to leave room for the tech tree arrow that leaves the science building.
const drawElectricityLinks = (
  ctx: CanvasRenderingContext2D,
  electricity: TPoint,
  targets: { mechanisms: TPoint; science: TPoint },
  size: number,
) => {
  const edge = sideEdge(size);
  const bend = size * 1.3;
  for (const target of [targets.mechanisms, targets.science]) {
    const side = Math.sign(target.y - electricity.y);
    drawDashedLink(
      ctx,
      { x: electricity.x + edge, y: electricity.y + side * size * 0.2 },
      { x: target.x + edge, y: target.y },
      bend,
      { size, color: PALETTE.ink },
    );
  }
  drawText(ctx, "🔭 электричество нужно\nдля работоспособности\nздания", electricity.x + size * 1.05, targets.science.y + size * 1.75, {
    ...LINK_CAPTION,
    align: "center",
  });
};

// The ghost of the new building at 60% opacity, as in the prototype, with a plus sign at its right.
const drawNewBuilding = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  ctx.save();
  ctx.globalAlpha = 0.6;
  drawArt(ctx, BUILDING_ART[NEW_BUILDING_ART], center, size, GHOST_PLACE);
  ctx.restore();

  const plus = { x: center.x + size * 0.5, y: center.y - size * 0.3 };
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
// and the art moves down and shrinks. All sizes are in hex sizes.
const PLATE_Y = -0.38;
const TAX_HOUSE_Y = 0.32;
const TAX_HOUSE_SIZE = 1;
const TAX_ART_PLACE: TArtPlace = { x: 0, y: TAX_HOUSE_Y, size: TAX_HOUSE_SIZE };
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

  drawArt(ctx, BUILDING_ART.village, centerOf(VILLAGE), size, ART_PLACE);

  if (isBuild) {
    for (const building of BUILDINGS) {
      const place = sameCell(building.cell, STAFFED_BUILDING) ? STAFFED_PLACE : ART_PLACE;
      drawArt(ctx, BUILDING_ART[building.art], centerOf(building.cell), size, place);
    }
    drawArt(ctx, STRONGHOLD_ART, centerOf(STRONGHOLD), size, RECRUIT_STRONGHOLD_PLACE);
    ctx.strokeStyle = PALETTE.ink;
    drawSacrificeLink(ctx, centerOf(SACRIFICE), centerOf(PURIFY), size);
    drawFoodLink(ctx, centerOf(FARM), centerOf(VILLAGE), size);
    drawElectricityLinks(ctx, centerOf(ELECTRICITY), { mechanisms: centerOf(MECHANISMS), science: centerOf(SCIENCE) }, size);
    drawNewBuilding(ctx, centerOf(NEW_BUILDING), size);
    drawRecruit(ctx, centerOf(STRONGHOLD), size);
    drawWorkers(ctx, centerOf(STAFFED_BUILDING), size);
  }

  if (isTax) {
    // Every building has rolled its die. The new building is built by now.
    // The stronghold has no plate here, so it keeps the usual place.
    drawArt(ctx, STRONGHOLD_ART, centerOf(STRONGHOLD), size, ART_PLACE);
    for (const building of [...BUILDINGS, { cell: NEW_BUILDING, art: NEW_BUILDING_ART }]) {
      drawArt(ctx, BUILDING_ART[building.art], centerOf(building.cell), size, TAX_ART_PLACE);
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
  toxic: TOXIC_BUILDING,
  newBuilding: NEW_BUILDING,
  sacrifice: SACRIFICE,
  stronghold: STRONGHOLD,
  staffedBuilding: STAFFED_BUILDING,
  moduleBuilding: MODULE_BUILDING,
  village: VILLAGE,
  damagedBuilding: DAMAGED_BUILDING,
  science: SCIENCE,
  payoutBuilding: PAYOUT,
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

export { ISLAND_ART_ASSETS, ISLAND_TARGETS, drawHexIsland, islandPoint, taxPlateTips };
