import { PALETTE } from "./palette";
import type { TPoint } from "../../core/types";

// Small line pictograms. `size` is the glyph height in world units.

const strokeAndFill = (ctx: CanvasRenderingContext2D) => {
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.strokeStyle = PALETTE.ink;
  ctx.stroke();
};

// A building: a square body under a gable roof.
const drawHouse = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  const half = size / 2;
  const bodyTop = center.y - half * 0.1;
  ctx.lineWidth = 1.25;
  ctx.lineJoin = "round";
  ctx.beginPath();
  ctx.moveTo(center.x - half * 0.7, center.y + half);
  ctx.lineTo(center.x - half * 0.7, bodyTop);
  ctx.lineTo(center.x, center.y - half);
  ctx.lineTo(center.x + half * 0.7, bodyTop);
  ctx.lineTo(center.x + half * 0.7, center.y + half);
  ctx.closePath();
  strokeAndFill(ctx);
};

// The stronghold: a tower with three merlons.
const drawTower = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  const half = size / 2;
  const width = half * 1.1;
  const left = center.x - width / 2;
  const top = center.y - half;
  const merlon = width / 5;
  ctx.lineWidth = 1.25;
  ctx.lineJoin = "round";
  ctx.beginPath();
  ctx.moveTo(left, center.y + half);
  ctx.lineTo(left, top);
  for (let index = 0; index < 5; index++) {
    const x = left + merlon * index;
    const y = index % 2 === 0 ? top : top + merlon;
    ctx.lineTo(x, y);
    ctx.lineTo(x + merlon, y);
  }
  ctx.lineTo(left + width, center.y + half);
  ctx.closePath();
  strokeAndFill(ctx);
};

export { drawHouse, drawTower };
