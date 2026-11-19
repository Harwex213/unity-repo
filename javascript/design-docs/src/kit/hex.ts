import type { TPoint } from "../core/types";

// Pointy-top hexes, as in ostrov-prototype (src/core/hex.ts).
// `size` is the distance from the center to a corner.

const SQRT_3 = Math.sqrt(3);

type TAxial = {
  q: number;
  r: number;
};

type TOffset = {
  col: number;
  row: number;
};

const axialToPoint = ({ q, r }: TAxial, size: number): TPoint => ({
  x: size * SQRT_3 * (q + r / 2),
  y: size * 1.5 * r,
});

// "Odd-r" offset coordinates: odd rows shift right by half a hex.
const offsetToPoint = ({ col, row }: TOffset, size: number): TPoint => ({
  x: size * SQRT_3 * (col + (row & 1) / 2),
  y: size * 1.5 * row,
});

// The first corner is at -30°, so the top of the hex is a corner.
const hexCorners = (center: TPoint, size: number): TPoint[] =>
  Array.from({ length: 6 }, (_, index) => {
    const angle = ((60 * index - 30) * Math.PI) / 180;
    return { x: center.x + size * Math.cos(angle), y: center.y + size * Math.sin(angle) };
  });

const traceHex = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  const corners = hexCorners(center, size);
  ctx.beginPath();
  corners.forEach((corner, index) => {
    if (index === 0) {
      ctx.moveTo(corner.x, corner.y);
    } else {
      ctx.lineTo(corner.x, corner.y);
    }
  });
  ctx.closePath();
};

// Diagonal hatching inside the hex. The wireframe marks enemy land this way.
const hatchHex = (ctx: CanvasRenderingContext2D, center: TPoint, size: number, step: number) => {
  ctx.save();
  traceHex(ctx, center, size);
  ctx.clip();
  ctx.beginPath();
  for (let offset = -size * 2; offset <= size * 2; offset += step) {
    ctx.moveTo(center.x + offset - size, center.y + size);
    ctx.lineTo(center.x + offset + size, center.y - size);
  }
  ctx.lineWidth = Math.max(0.5, step * 0.15);
  ctx.stroke();
  ctx.restore();
};

// All axial coordinates within `radius` steps of the origin.
const hexArea = (radius: number): TAxial[] => {
  const area: TAxial[] = [];
  for (let q = -radius; q <= radius; q++) {
    for (let r = Math.max(-radius, -q - radius); r <= Math.min(radius, -q + radius); r++) {
      area.push({ q, r });
    }
  }
  return area;
};

export { axialToPoint, hatchHex, hexArea, hexCorners, offsetToPoint, traceHex };
export type { TAxial, TOffset };
