import { drawTower } from "../../kit/glyphs";
import { axialToPoint, hatchHex, hexArea, hexCorners, traceHex } from "../../kit/hex";
import { FONT_FAMILY, PALETTE } from "../../kit/palette";
import type { TPoint, TRect } from "../../core/types";
import type { TAxial } from "../../kit/hex";

// The world map of ostrov-prototype is a sphere covered with hexes.
// Here a flat hex grid is wrapped onto the visible half of a sphere:
// a grid point at angular distance θ from the center lands at radius R·sin θ.

type TSphere = {
  center: TPoint;
  radius: number;
  // The hex size as an angle on the sphere, in radians.
  cellAngle: number;
};

// Cells beyond this angle are too squeezed near the edge to read, so they are not drawn.
const MAX_ANGLE = 1.42;
const EDGE_STEPS = 6;
const GRID_RADIUS = 10;

const project = (sphere: TSphere, point: TPoint): TPoint => {
  const angle = Math.hypot(point.x, point.y);
  const k = angle === 0 ? 0 : (Math.sin(angle) / angle) * sphere.radius;
  return { x: sphere.center.x + point.x * k, y: sphere.center.y + point.y * k };
};

const cellCenter = (sphere: TSphere, cell: TAxial): TPoint =>
  project(sphere, axialToPoint(cell, sphere.cellAngle));

// Each edge is split into steps, so it bends with the sphere.
// Returns false for a cell too close to the edge of the sphere.
const traceCell = (ctx: CanvasRenderingContext2D, sphere: TSphere, cell: TAxial) => {
  const corners = hexCorners(axialToPoint(cell, sphere.cellAngle), sphere.cellAngle);
  if (corners.some((corner) => Math.hypot(corner.x, corner.y) > MAX_ANGLE)) {
    return false;
  }
  ctx.beginPath();
  corners.forEach((corner, index) => {
    const next = corners[(index + 1) % corners.length] as TPoint;
    for (let step = 0; step < EDGE_STEPS; step++) {
      const t = step / EDGE_STEPS;
      const point = project(sphere, { x: corner.x + (next.x - corner.x) * t, y: corner.y + (next.y - corner.y) * t });
      if (index === 0 && step === 0) {
        ctx.moveTo(point.x, point.y);
      } else {
        ctx.lineTo(point.x, point.y);
      }
    }
  });
  ctx.closePath();
  return true;
};

const sameCell = (a: TAxial, b: TAxial) => a.q === b.q && a.r === b.r;
const isIn = (cells: TAxial[], cell: TAxial) => cells.some((other) => sameCell(other, cell));

type TGlobeMarks = {
  // Cells with a bolder outline.
  bold?: TAxial[];
  // Cells with wild enemy islands, as `island` cells of the prototype.
  enemies?: TAxial[];
  // Cells the player cannot fly through.
  blocked?: TAxial[];
  // Cells the player has not explored yet.
  unknown?: TAxial[];
};

// An unexplored cell: a question mark.
const drawUnknown = (ctx: CanvasRenderingContext2D, center: TPoint, cellSize: number) => {
  ctx.font = `600 ${cellSize * 0.75}px ${FONT_FAMILY}`;
  ctx.fillStyle = PALETTE.ink;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText("?", center.x, center.y + cellSize * 0.04);
};

// The small enemy islands of one cell: three hatched hexes around `center`.
const ENEMY_ISLAND_CELLS: TAxial[] = [
  { q: 0, r: 0 },
  { q: 1, r: 0 },
  { q: 0, r: 1 },
];

const drawEnemyIslands = (ctx: CanvasRenderingContext2D, center: TPoint, cellSize: number) => {
  const size = cellSize * 0.27;
  // The three hexes are centered on their middle point.
  const shift = axialToPoint({ q: 1 / 3, r: 1 / 3 }, size);
  ctx.strokeStyle = PALETTE.ink;
  for (const cell of ENEMY_ISLAND_CELLS) {
    const point = axialToPoint(cell, size);
    const hexCenter = { x: center.x + point.x - shift.x, y: center.y + point.y - shift.y };
    hatchHex(ctx, hexCenter, size, size * 0.45);
    traceHex(ctx, hexCenter, size);
    ctx.lineWidth = 1;
    ctx.stroke();
  }
};

const fillBlocked = (ctx: CanvasRenderingContext2D) => {
  ctx.fillStyle = PALETTE.blockedFill;
  ctx.fill();
};

// The grid, the outline of the sphere and the player's island in the center cell.
const drawSphere = (ctx: CanvasRenderingContext2D, sphere: TSphere, marks: TGlobeMarks = {}) => {
  const { bold = [], enemies = [], blocked = [], unknown = [] } = marks;
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineJoin = "round";
  for (const cell of hexArea(GRID_RADIUS)) {
    if (traceCell(ctx, sphere, cell)) {
      if (isIn(blocked, cell)) {
        fillBlocked(ctx);
      }
      const isHome = cell.q === 0 && cell.r === 0;
      ctx.lineWidth = isHome ? 2.5 : isIn(bold, cell) ? 2 : 1;
      ctx.stroke();
    }
  }
  for (const cell of enemies) {
    drawEnemyIslands(ctx, cellCenter(sphere, cell), sphere.radius * sphere.cellAngle);
  }
  for (const cell of unknown) {
    drawUnknown(ctx, cellCenter(sphere, cell), sphere.radius * sphere.cellAngle);
  }

  ctx.beginPath();
  ctx.arc(sphere.center.x, sphere.center.y, sphere.radius, 0, Math.PI * 2);
  ctx.lineWidth = 1.5;
  ctx.stroke();

  drawTower(ctx, sphere.center, sphere.radius * sphere.cellAngle * 0.9);
};

// The whole globe, centered in `area`.
const globeSphere = (area: TRect): TSphere => ({
  center: { x: area.x + area.width / 2, y: area.y + area.height / 2 },
  radius: Math.min(area.width, area.height) / 2,
  cellAngle: 0.24,
});

const drawGlobe = (ctx: CanvasRenderingContext2D, area: TRect, marks: TGlobeMarks = {}) => {
  drawSphere(ctx, globeSphere(area), marks);
};

// The legend swatch of a blocked cell: a flat hex.
const drawBlockedSwatch = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  traceHex(ctx, center, size);
  fillBlocked(ctx);
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1;
  ctx.stroke();
};

// A point of the cell. `offset` is measured in hex sizes from the cell center.
const cellPoint = (sphere: TSphere, cell: TAxial, offset: TPoint): TPoint => {
  const center = cellCenter(sphere, cell);
  const size = sphere.radius * sphere.cellAngle;
  return { x: center.x + offset.x * size, y: center.y + offset.y * size };
};

export { cellCenter, cellPoint, drawBlockedSwatch, drawEnemyIslands, drawUnknown, drawGlobe, drawSphere, globeSphere, traceCell };
export type { TGlobeMarks, TSphere };
