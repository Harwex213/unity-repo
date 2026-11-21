import { FONT_FAMILY } from "../../kit/palette";
import { drawSheet, drawText } from "../../kit/shapes";
import { PALETTE } from "./palette";
import { SCHEME, drawScheme } from "./scheme";
import type { TPhaseScheme } from "./scheme";
import type { TRect } from "../../core/types";

// A frame is a separate white sheet with its title above it, like a frame in Figma.
// All coordinates are world units.

const TITLE_SIZE = 22;
// The top of the title line stands this far above the sheet.
const TITLE_LIFT = 38;

const drawFrame = (ctx: CanvasRenderingContext2D, rect: TRect, title: string) => {
  drawSheet(ctx, rect);
  drawText(ctx, title, rect.x, rect.y - TITLE_LIFT, { size: TITLE_SIZE, weight: 600 });
};

// ---------- Menu frames ----------

// A menu frame is a text wireframe: the list of its buttons, inputs and selects.
const MENU_TEXT = { size: 18, lineHeight: 26 };
const MENU_PADDING = 24;

const menuFrameHeight = (lines: string[]) => lines.length * MENU_TEXT.lineHeight + MENU_PADDING * 2;

const drawMenuFrame = (ctx: CanvasRenderingContext2D, rect: TRect, title: string, lines: string[]) => {
  drawFrame(ctx, rect, title);
  if (lines.length > 0) {
    drawText(ctx, lines.join("\n"), rect.x + MENU_PADDING, rect.y + MENU_PADDING, MENU_TEXT);
  }
};

// ---------- Phase frames ----------

// World units per scheme unit. A callout label of the scheme is about as large as the menu text.
const PHASE_SCALE = 0.8;
const PHASE_PADDING = 40;
const SUMMARY_TEXT = { size: 18, lineHeight: 26, color: PALETTE.muted };
const SUMMARY_LINES = 2;
const SCHEME_TOP = PHASE_PADDING + SUMMARY_LINES * SUMMARY_TEXT.lineHeight;
const LEGEND_HEIGHT = 44;
const LEGEND_DOT_RADIUS = 6;

const PHASE_FRAME_SIZE = {
  width: SCHEME.width * PHASE_SCALE,
  height: SCHEME_TOP + SCHEME.height * PHASE_SCALE + LEGEND_HEIGHT,
};

// Splits the text into lines not wider than `maxWidth`.
const wrapLines = (ctx: CanvasRenderingContext2D, value: string, maxWidth: number, size: number) => {
  ctx.font = `400 ${size}px ${FONT_FAMILY}`;
  const lines: string[] = [];
  let current = "";
  value.split(" ").forEach((word) => {
    const next = current ? `${current} ${word}` : word;
    if (current && ctx.measureText(next).width > maxWidth) {
      lines.push(current);
      current = word;
    } else {
      current = next;
    }
  });
  if (current) {
    lines.push(current);
  }
  return lines;
};

const LEGEND = [
  { color: PALETTE.always, label: "есть на экране всю фазу" },
  { color: PALETTE.onAction, label: "появляется по действию, пунктир на схеме" },
];
const LEGEND_ITEM_WIDTH = 300;

const drawLegend = (ctx: CanvasRenderingContext2D, x: number, y: number) => {
  LEGEND.forEach((item, index) => {
    const left = x + index * LEGEND_ITEM_WIDTH;
    ctx.beginPath();
    ctx.arc(left + LEGEND_DOT_RADIUS, y, LEGEND_DOT_RADIUS, 0, Math.PI * 2);
    ctx.fillStyle = item.color;
    ctx.fill();
    drawText(ctx, item.label, left + LEGEND_DOT_RADIUS * 2 + 10, y - 11, { size: 16, color: PALETTE.muted });
  });
};

const drawPhaseFrame = (ctx: CanvasRenderingContext2D, rect: TRect, title: string, scheme: TPhaseScheme) => {
  drawFrame(ctx, rect, title);
  const summary = wrapLines(ctx, scheme.summary, rect.width - PHASE_PADDING * 2, SUMMARY_TEXT.size);
  drawText(ctx, summary.join("\n"), rect.x + PHASE_PADDING, rect.y + PHASE_PADDING - 12, SUMMARY_TEXT);
  drawScheme(ctx, scheme, { x: rect.x, y: rect.y + SCHEME_TOP }, PHASE_SCALE);
  drawLegend(ctx, rect.x + PHASE_PADDING, rect.y + rect.height - LEGEND_HEIGHT / 2 - 6);
};

export { PHASE_FRAME_SIZE, drawFrame, drawMenuFrame, drawPhaseFrame, menuFrameHeight };
