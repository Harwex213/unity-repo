import { FONT_FAMILY, PALETTE } from "./palette";
import type { TPoint, TRect } from "../core/types";

// Wireframe primitives. Every function draws in world units.

type TTextOptions = {
  size?: number;
  color?: string;
  align?: CanvasTextAlign;
  weight?: number;
  lineHeight?: number;
  family?: string;
};

type TStrokeOptions = {
  color?: string;
  width?: number;
};

const ARROW_HEAD_LENGTH = 14;
const ARROW_HEAD_ANGLE = Math.PI / 7;
const CONNECTOR_RADIUS = 18;

const lineHeightOf = (options: TTextOptions) => options.lineHeight ?? Math.round((options.size ?? 16) * 1.35);

// "\n" splits the text into lines. `y` is the top of the first line.
const drawText = (ctx: CanvasRenderingContext2D, text: string, x: number, y: number, options: TTextOptions = {}) => {
  const size = options.size ?? 16;
  const lineHeight = lineHeightOf(options);
  ctx.font = `${options.weight ?? 400} ${size}px ${options.family ?? FONT_FAMILY}`;
  ctx.fillStyle = options.color ?? PALETTE.ink;
  ctx.textAlign = options.align ?? "left";
  ctx.textBaseline = "middle";
  text.split("\n").forEach((line, index) => {
    ctx.fillText(line, x, y + lineHeight * (index + 0.5));
  });
};

// The text is centered in the rect on both axes.
const drawTextInRect = (ctx: CanvasRenderingContext2D, text: string, rect: TRect, options: TTextOptions = {}) => {
  const height = text.split("\n").length * lineHeightOf(options);
  drawText(ctx, text, rect.x + rect.width / 2, rect.y + (rect.height - height) / 2, { ...options, align: "center" });
};

// The text block is centered on `y`.
const drawTextCenteredAt = (
  ctx: CanvasRenderingContext2D,
  text: string,
  x: number,
  y: number,
  options: TTextOptions = {},
) => {
  const height = text.split("\n").length * lineHeightOf(options);
  drawText(ctx, text, x, y - height / 2, options);
};

const drawSheet = (ctx: CanvasRenderingContext2D, rect: TRect) => {
  ctx.beginPath();
  ctx.roundRect(rect.x, rect.y, rect.width, rect.height, 8);
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.strokeStyle = PALETTE.sheetBorder;
  ctx.lineWidth = 1.5;
  ctx.stroke();
};

const drawBox = (ctx: CanvasRenderingContext2D, rect: TRect, options: TStrokeOptions = {}) => {
  ctx.strokeStyle = options.color ?? PALETTE.ink;
  ctx.lineWidth = options.width ?? 2;
  ctx.strokeRect(rect.x, rect.y, rect.width, rect.height);
};

const drawSwatch = (ctx: CanvasRenderingContext2D, rect: TRect, fill: string) => {
  ctx.fillStyle = fill;
  ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
  drawBox(ctx, rect);
};

type TConnectorOptions = TStrokeOptions & {
  // A paper-colored band under the line keeps it readable over a drawing.
  // The band appears only inside this rect, so the line still crosses box borders cleanly.
  haloArea?: TRect;
};

const HALO_WIDTH = 5;

const traceArrowHead = (ctx: CanvasRenderingContext2D, from: TPoint, tip: TPoint) => {
  const angle = Math.atan2(tip.y - from.y, tip.x - from.x);
  ctx.moveTo(
    tip.x - ARROW_HEAD_LENGTH * Math.cos(angle - ARROW_HEAD_ANGLE),
    tip.y - ARROW_HEAD_LENGTH * Math.sin(angle - ARROW_HEAD_ANGLE),
  );
  ctx.lineTo(tip.x, tip.y);
  ctx.lineTo(
    tip.x - ARROW_HEAD_LENGTH * Math.cos(angle + ARROW_HEAD_ANGLE),
    tip.y - ARROW_HEAD_LENGTH * Math.sin(angle + ARROW_HEAD_ANGLE),
  );
};

// The line with rounded corners and the open arrow head, as one path.
const traceConnector = (ctx: CanvasRenderingContext2D, points: TPoint[], first: TPoint, beforeLast: TPoint, last: TPoint) => {
  ctx.beginPath();
  ctx.moveTo(first.x, first.y);
  for (let index = 1; index < points.length - 1; index++) {
    const previous = points[index - 1] as TPoint;
    const corner = points[index] as TPoint;
    const next = points[index + 1] as TPoint;
    // A corner radius larger than half a segment would bend the line backwards.
    const radius = Math.min(
      CONNECTOR_RADIUS,
      Math.hypot(corner.x - previous.x, corner.y - previous.y) / 2,
      Math.hypot(next.x - corner.x, next.y - corner.y) / 2,
    );
    ctx.arcTo(corner.x, corner.y, next.x, next.y, radius);
  }
  ctx.lineTo(last.x, last.y);
  traceArrowHead(ctx, beforeLast, last);
};

// A polyline with rounded corners and an open arrow head at the last point.
const drawConnector = (ctx: CanvasRenderingContext2D, points: TPoint[], options: TConnectorOptions = {}) => {
  const first = points[0];
  const last = points[points.length - 1];
  const beforeLast = points[points.length - 2];
  if (!first || !last || !beforeLast) {
    return;
  }

  const width = options.width ?? 2;
  ctx.lineJoin = "round";
  ctx.lineCap = "round";

  if (options.haloArea) {
    const area = options.haloArea;
    ctx.save();
    ctx.beginPath();
    ctx.rect(area.x, area.y, area.width, area.height);
    ctx.clip();
    traceConnector(ctx, points, first, beforeLast, last);
    ctx.strokeStyle = PALETTE.paper;
    ctx.lineWidth = width + HALO_WIDTH;
    ctx.stroke();
    ctx.restore();
  }

  traceConnector(ctx, points, first, beforeLast, last);
  ctx.strokeStyle = options.color ?? PALETTE.ink;
  ctx.lineWidth = width;
  ctx.stroke();
};

export { drawBox, drawConnector, drawSheet, drawSwatch, drawText, drawTextCenteredAt, drawTextInRect };
export type { TConnectorOptions, TStrokeOptions, TTextOptions };
