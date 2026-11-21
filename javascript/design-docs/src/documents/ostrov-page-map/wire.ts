import { FONT_FAMILY } from "../../kit/palette";
import { hexCorners } from "../../kit/hex";
import { PALETTE } from "./palette";
import type { TPoint, TRect } from "../../core/types";

// Drawing primitives of the phase schemes.
// The schemes keep the geometry of the inline SVG in docs/spec/ui-phase-*.html,
// so these primitives take the same numbers as the SVG: corner radii, stroke widths, dashes
// and text baselines.

type TStyle = {
  fill?: string;
  stroke?: string;
  width?: number;
  dash?: number[];
  alpha?: number;
};

// The style classes of the SVG.
const STYLE = {
  // A panel of the HUD.
  panel: { fill: PALETTE.paper, stroke: PALETTE.ink, width: 2.5 },
  // The selected or main button.
  strong: { fill: PALETTE.paper, stroke: PALETTE.ink, width: 4 },
  // An empty slot.
  dashed: { stroke: PALETTE.ink, width: 2, dash: [7, 6] },
  // A disabled control.
  disabled: { stroke: PALETTE.muted, width: 2, dash: [3, 6] },
  soft: { fill: PALETTE.soft },
  ink: { fill: PALETTE.ink },
  icon: { fill: PALETTE.soft, stroke: PALETTE.ink, width: 1.5 },
  line: { stroke: PALETTE.ink, width: 2 },
  // A placeholder for a line of text.
  textLine: { stroke: PALETTE.muted, width: 2.5 },
  check: { stroke: PALETTE.ink, width: 4 },
  hex: { stroke: PALETTE.ink, width: 1.5, alpha: 0.6 },
  hexFilled: { fill: PALETTE.soft, stroke: PALETTE.ink, width: 2 },
  hexDashed: { stroke: PALETTE.ink, width: 2.5, dash: [9, 6] },
  hexSelected: { fill: PALETTE.soft, stroke: PALETTE.always, width: 6 },
  hexFog: { stroke: PALETTE.muted, width: 1.5 },
  building: { fill: PALETTE.paper, stroke: PALETTE.ink, width: 2.5 },
  plate: { fill: PALETTE.paper, stroke: PALETTE.ink, width: 1.5 },
  warning: { fill: PALETTE.paper, stroke: PALETTE.always, width: 2 },
  cloud: { fill: PALETTE.soft, stroke: PALETTE.muted, width: 2 },
  // A window that opens after an action: a dashed blue outline.
  overlayBox: { fill: PALETTE.paper, stroke: PALETTE.onAction, width: 3, dash: [12, 8] },
  overlayOutline: { stroke: PALETTE.onAction, width: 3, dash: [12, 8] },
  overlayLine: { stroke: PALETTE.onAction, width: 2 },
  // The path of the flying payout.
  flight: { stroke: PALETTE.onAction, width: 2.5, dash: [4, 7] },
  stowed: { stroke: PALETTE.muted, width: 2, dash: [8, 7] },
} as const satisfies Record<string, TStyle>;

// Fills and strokes the path that `trace` adds to the context.
const paint = (ctx: CanvasRenderingContext2D, style: TStyle, trace: () => void) => {
  ctx.save();
  ctx.globalAlpha = style.alpha ?? 1;
  ctx.lineJoin = "round";
  ctx.lineCap = "butt";
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

// A closed polygon, or an open polyline when the style has no fill.
const polygon = (ctx: CanvasRenderingContext2D, points: TPoint[], style: TStyle, closed = true) => {
  paint(ctx, style, () => {
    points.forEach((point, index) => (index === 0 ? ctx.moveTo(point.x, point.y) : ctx.lineTo(point.x, point.y)));
    if (closed) {
      ctx.closePath();
    }
  });
};

// A pointy-top hex. `size` is the distance from the center to a corner.
const hex = (ctx: CanvasRenderingContext2D, center: TPoint, size: number, style: TStyle) => {
  polygon(ctx, hexCorners(center, size), style);
};

const HATCH_STEP = 10;

// Diagonal hatching inside the path of `trace`. `area` is the bounding box of the path.
const hatch = (ctx: CanvasRenderingContext2D, area: TRect, trace: () => void) => {
  ctx.save();
  ctx.beginPath();
  trace();
  ctx.clip();
  ctx.beginPath();
  const step = HATCH_STEP * Math.SQRT2;
  for (let x = area.x - area.height; x <= area.x + area.width; x += step) {
    ctx.moveTo(x, area.y + area.height);
    ctx.lineTo(x + area.height, area.y);
  }
  ctx.strokeStyle = PALETTE.hatch;
  ctx.lineWidth = 2;
  ctx.stroke();
  ctx.restore();
};

const hatchHex = (ctx: CanvasRenderingContext2D, center: TPoint, size: number) => {
  const area = { x: center.x - size, y: center.y - size, width: size * 2, height: size * 2 };
  hatch(ctx, area, () => {
    hexCorners(center, size).forEach((corner, index) =>
      index === 0 ? ctx.moveTo(corner.x, corner.y) : ctx.lineTo(corner.x, corner.y),
    );
    ctx.closePath();
  });
};

type TTextStyle = {
  size: number;
  weight?: number;
  color?: string;
};

// The text classes of the SVG.
const TEXT = {
  normal: { size: 21 },
  bold: { size: 21, weight: 700 },
  heading: { size: 32, weight: 700 },
  small: { size: 17 },
  ghost: { size: 24, color: PALETTE.muted },
  overlay: { size: 21, color: PALETTE.onAction },
  overlayBold: { size: 21, weight: 700, color: PALETTE.onAction },
} as const satisfies Record<string, TTextStyle>;

// `y` is the baseline, as in SVG.
const text = (
  ctx: CanvasRenderingContext2D,
  value: string,
  x: number,
  y: number,
  style: TTextStyle,
  align: CanvasTextAlign = "left",
) => {
  ctx.font = `${style.weight ?? 400} ${style.size}px ${FONT_FAMILY}`;
  ctx.fillStyle = style.color ?? PALETTE.ink;
  ctx.textAlign = align;
  ctx.textBaseline = "alphabetic";
  ctx.fillText(value, x, y);
};

export { STYLE, TEXT, circle, hatch, hatchHex, hex, line, paint, polygon, rect, text };
export type { TStyle, TTextStyle };
