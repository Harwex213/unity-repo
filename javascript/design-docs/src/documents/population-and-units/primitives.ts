import { FONT_FAMILY } from "../../kit/palette";
import { drawText, drawTextInRect } from "../../kit/shapes";
import { PALETTE } from "./palette";
import type { TRect } from "../../core/types";

// Wireframe parts of the prototype's interface: panels, section headers and buttons.

type TPanelOptions = {
  fill?: string;
  color?: string;
  width?: number;
  radius?: number;
  dashed?: boolean;
};

const drawPanel = (ctx: CanvasRenderingContext2D, rect: TRect, options: TPanelOptions = {}) => {
  ctx.beginPath();
  ctx.roundRect(rect.x, rect.y, rect.width, rect.height, options.radius ?? 8);
  ctx.fillStyle = options.fill ?? PALETTE.panelFill;
  ctx.fill();
  ctx.save();
  if (options.dashed) {
    ctx.setLineDash([6, 5]);
  }
  ctx.strokeStyle = options.color ?? PALETTE.ink;
  ctx.lineWidth = options.width ?? 1.5;
  ctx.stroke();
  ctx.restore();
};

// The section title of a building panel: small capitals and a line to the right edge.
const SECTION_HEADER_SIZE = 13;

const drawSectionHeader = (ctx: CanvasRenderingContext2D, title: string, x: number, y: number, right: number) => {
  drawText(ctx, title, x, y, { size: SECTION_HEADER_SIZE, weight: 700 });
  ctx.font = `700 ${SECTION_HEADER_SIZE}px ${FONT_FAMILY}`;
  const lineX = x + ctx.measureText(title).width + 10;
  const lineY = y + SECTION_HEADER_SIZE * 0.7;
  ctx.beginPath();
  ctx.moveTo(lineX, lineY);
  ctx.lineTo(right, lineY);
  ctx.strokeStyle = PALETTE.muted;
  ctx.lineWidth = 1;
  ctx.stroke();
};

const drawButton = (
  ctx: CanvasRenderingContext2D,
  rect: TRect,
  text: string,
  options: { disabled?: boolean; size?: number } = {},
) => {
  const color = options.disabled ? PALETTE.muted : PALETTE.ink;
  drawPanel(ctx, rect, { fill: PALETTE.paper, color, radius: 6, width: options.disabled ? 1 : 1.5 });
  drawTextInRect(ctx, text, rect, { size: options.size ?? 15, weight: 600, color });
};

export { drawButton, drawPanel, drawSectionHeader };
