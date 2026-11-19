import { drawHouse } from "../../kit/glyphs";
import { PALETTE } from "../../kit/palette";
import type { TPoint } from "../../core/types";

// The module wheel of a building: a ring of four cells around the building.
// Each cell takes one module, an annex of the building.
// The top right cell holds a module. The top left cell is free:
// the player puts the next module there. The two lower cells are free too.
// ostrov-prototype has no modules yet, so the wheel is schematic.

type TModuleWheel = {
  center: TPoint;
  radius: number;
};

const HUB_RATIO = 0.36;

// The middle of a cell. `index` counts the cells clockwise from the top left one.
const cellMiddle = (wheel: TModuleWheel, index: number): TPoint => {
  const angle = -Math.PI * 0.75 + (index * Math.PI) / 2;
  const distance = wheel.radius * (1 + HUB_RATIO) * 0.5;
  return { x: wheel.center.x + Math.cos(angle) * distance, y: wheel.center.y + Math.sin(angle) * distance };
};

// A small annex: a box with a lean-to roof and a door.
const drawModule = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  const half = size / 2;
  const left = center.x - half;
  const right = center.x + half;
  const bottom = center.y + half * 0.8;
  ctx.beginPath();
  ctx.moveTo(left, bottom);
  ctx.lineTo(left, center.y - half * 0.2);
  ctx.lineTo(right, center.y - half * 0.8);
  ctx.lineTo(right, bottom);
  ctx.closePath();
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.lineWidth = 1.5;
  ctx.stroke();
  ctx.strokeRect(center.x - half * 0.2, bottom - half * 0.7, half * 0.4, half * 0.7);
};

const drawModuleWheel = (ctx: CanvasRenderingContext2D, wheel: TModuleWheel) => {
  const { center, radius } = wheel;
  const hub = radius * HUB_RATIO;
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineJoin = "round";
  ctx.lineWidth = 2;

  ctx.beginPath();
  ctx.arc(center.x, center.y, radius, 0, Math.PI * 2);
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.stroke();

  // The borders of the four cells, from the hub to the rim.
  ctx.beginPath();
  ctx.moveTo(center.x, center.y - radius);
  ctx.lineTo(center.x, center.y - hub);
  ctx.moveTo(center.x, center.y + hub);
  ctx.lineTo(center.x, center.y + radius);
  ctx.moveTo(center.x - radius, center.y);
  ctx.lineTo(center.x - hub, center.y);
  ctx.moveTo(center.x + hub, center.y);
  ctx.lineTo(center.x + radius, center.y);
  ctx.stroke();

  ctx.beginPath();
  ctx.arc(center.x, center.y, hub, 0, Math.PI * 2);
  ctx.stroke();
  drawHouse(ctx, center, hub * 1.1);

  drawModule(ctx, cellMiddle(wheel, 1), radius * 0.3);
};

export { cellMiddle, drawModuleWheel };
export type { TModuleWheel };
