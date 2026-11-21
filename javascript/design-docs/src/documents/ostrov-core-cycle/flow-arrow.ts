import { PALETTE } from "./palette";
import type { TPoint } from "../../core/types";

// A wide band arrow for the main flow of the sheet: a thick shaft and a large solid head, one filled shape.
// A paper outline keeps its edges crisp over the section colors and the dashed splits.
// All sizes are in world units.
const FLOW_ARROW = {
  shaftWidth: 16,
  headWidth: 44,
  headLength: 34,
  outline: 3,
};

const traceFlowArrow = (ctx: CanvasRenderingContext2D, from: TPoint, to: TPoint) => {
  const length = Math.hypot(to.x - from.x, to.y - from.y);
  const along = { x: (to.x - from.x) / length, y: (to.y - from.y) / length };
  const across = { x: -along.y, y: along.x };
  const point = (distance: number, offset: number): TPoint => ({
    x: from.x + along.x * distance + across.x * offset,
    y: from.y + along.y * distance + across.y * offset,
  });
  const neck = length - FLOW_ARROW.headLength;
  const shaft = FLOW_ARROW.shaftWidth / 2;
  const head = FLOW_ARROW.headWidth / 2;
  const corners = [
    point(0, -shaft),
    point(neck, -shaft),
    point(neck, -head),
    point(length, 0),
    point(neck, head),
    point(neck, shaft),
    point(0, shaft),
  ];
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

// `from` is the middle of the shaft end, `to` is the tip of the head.
const drawFlowArrow = (ctx: CanvasRenderingContext2D, from: TPoint, to: TPoint) => {
  traceFlowArrow(ctx, from, to);
  ctx.lineJoin = "round";
  ctx.strokeStyle = PALETTE.paper;
  ctx.lineWidth = FLOW_ARROW.outline * 2;
  ctx.stroke();
  ctx.fillStyle = PALETTE.ink;
  ctx.fill();
};

export { drawFlowArrow };
