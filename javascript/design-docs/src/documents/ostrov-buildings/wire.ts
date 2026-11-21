import { hexCorners } from "../../kit/hex";
import { PALETTE } from "./palette";
import type { TPoint, TRect } from "../../core/types";

// Drawing primitives of the HUD wireframe.

type TStyle = {
  fill?: string;
  stroke?: string;
  width?: number;
  dash?: number[];
};

const STYLE = {
  // A bright panel: the part of the HUD the document is about.
  panel: { fill: PALETTE.paper, stroke: PALETTE.ink, width: 2.5 },
  // A section of the selected building panel.
  section: { fill: PALETTE.paper, stroke: PALETTE.ink, width: 1.5 },
  button: { fill: PALETTE.soft, stroke: PALETTE.ink, width: 1.5 },
  chip: { fill: PALETTE.paper, stroke: PALETTE.ink, width: 1.5 },
  // The extra face that the biome adds, and a free slot.
  dashed: { fill: PALETTE.paper, stroke: PALETTE.ink, width: 1.5, dash: [5, 4] },
  hex: { stroke: PALETTE.ink, width: 1.5 },
  selectedHex: { stroke: PALETTE.selected, width: 5 },
  // The rest of the HUD.
  dimPanel: { fill: PALETTE.paper, stroke: PALETTE.dim, width: 2 },
  dimLine: { stroke: PALETTE.dim, width: 2 },
  dimFill: { fill: PALETTE.dim },
  // The edge of the game screen.
  screen: { fill: PALETTE.paper, stroke: PALETTE.ink, width: 2 },
} as const satisfies Record<string, TStyle>;

const rightOf = (rect: TRect) => rect.x + rect.width;
const bottomOf = (rect: TRect) => rect.y + rect.height;
const centerXOf = (rect: TRect) => rect.x + rect.width / 2;
const middleYOf = (rect: TRect) => rect.y + rect.height / 2;

// Fills and strokes the path that `trace` adds to the context.
const paint = (ctx: CanvasRenderingContext2D, style: TStyle, trace: () => void) => {
  ctx.save();
  ctx.lineJoin = "round";
  ctx.lineCap = "round";
  ctx.beginPath();
  trace();
  if (style.fill) {
    ctx.fillStyle = style.fill;
    ctx.fill();
  }
  if (style.stroke) {
    ctx.strokeStyle = style.stroke;
    ctx.lineWidth = style.width ?? 1;
    ctx.setLineDash(style.dash ?? []);
    ctx.stroke();
  }
  ctx.restore();
};

const rect = (ctx: CanvasRenderingContext2D, area: TRect, radius: number, style: TStyle) => {
  paint(ctx, style, () => ctx.roundRect(area.x, area.y, area.width, area.height, radius));
};

const circle = (ctx: CanvasRenderingContext2D, center: TPoint, radius: number, style: TStyle) => {
  paint(ctx, style, () => ctx.arc(center.x, center.y, radius, 0, Math.PI * 2));
};

const line = (ctx: CanvasRenderingContext2D, from: TPoint, to: TPoint, style: TStyle) => {
  paint(ctx, style, () => {
    ctx.moveTo(from.x, from.y);
    ctx.lineTo(to.x, to.y);
  });
};

// A pointy-top hex. `size` is the distance from the center to a corner.
const hex = (ctx: CanvasRenderingContext2D, center: TPoint, size: number, style: TStyle) => {
  paint(ctx, style, () => {
    hexCorners(center, size).forEach((corner, index) =>
      index === 0 ? ctx.moveTo(corner.x, corner.y) : ctx.lineTo(corner.x, corner.y),
    );
    ctx.closePath();
  });
};

// A square of side `side` around `center`.
const squareAround = (center: TPoint, side: number): TRect => ({
  x: center.x - side / 2,
  y: center.y - side / 2,
  width: side,
  height: side,
});

// A dashed line with a solid open head at `to`.
const dashedArrow = (ctx: CanvasRenderingContext2D, from: TPoint, to: TPoint, style: { color: string; width: number }) => {
  const head = 12;
  const spread = Math.PI / 7;
  const angle = Math.atan2(to.y - from.y, to.x - from.x);
  line(ctx, from, to, { stroke: style.color, width: style.width, dash: [7, 6] });
  paint(ctx, { stroke: style.color, width: style.width }, () => {
    ctx.moveTo(to.x - head * Math.cos(angle - spread), to.y - head * Math.sin(angle - spread));
    ctx.lineTo(to.x, to.y);
    ctx.lineTo(to.x - head * Math.cos(angle + spread), to.y - head * Math.sin(angle + spread));
  });
};

export { STYLE, bottomOf, centerXOf, circle, dashedArrow, hex, line, middleYOf, paint, rect, rightOf, squareAround };
export type { TStyle };
