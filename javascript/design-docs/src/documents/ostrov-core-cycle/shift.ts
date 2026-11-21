import { drawText } from "../../kit/shapes";
import { PALETTE } from "./palette";
import type { TRect } from "../../core/types";

// The work shift of a building in the tax phase. The shift ends with one of five outcomes at random.
// The player can spend power (власть) to force a normal, good or excellent shift.
// Both mini windows show the same scale of outcomes, the best one on top.

type TShiftOutcome = { text: string; fill: string; color: string; bold: boolean; forcible: boolean };

const SHIFT_OUTCOMES: TShiftOutcome[] = [
  { text: "отличная", fill: PALETTE.positiveFill, color: PALETTE.positive, bold: true, forcible: true },
  { text: "хорошая", fill: PALETTE.positiveFill, color: PALETTE.positive, bold: false, forcible: true },
  { text: "обычная", fill: PALETTE.paper, color: PALETTE.ink, bold: false, forcible: true },
  { text: "плохая", fill: PALETTE.negativeFill, color: PALETTE.negative, bold: false, forcible: false },
  { text: "крайне плохая", fill: PALETTE.negativeFill, color: PALETTE.negative, bold: true, forcible: false },
];

// The title and a muted subtitle under it.
const SHIFT_TITLE_HEIGHT = 35;
const SHIFT_PADDING = 8;
const SHIFT_CHIP = { height: 15, gap: 3 };

// The height of a window that holds the title and the five outcomes.
const SHIFT_WINDOW_HEIGHT =
  SHIFT_TITLE_HEIGHT + SHIFT_OUTCOMES.length * (SHIFT_CHIP.height + SHIFT_CHIP.gap) - SHIFT_CHIP.gap + SHIFT_PADDING;

// "random": the shift picks any outcome. "forced": the two bad outcomes are greyed out,
// and a purple frame marks the three outcomes that power can force.
const drawShiftWindow = (
  ctx: CanvasRenderingContext2D,
  rect: TRect,
  options: { title: string; subtitle: string; titleColor: string; mode: "random" | "forced" },
) => {
  ctx.beginPath();
  ctx.roundRect(rect.x, rect.y, rect.width, rect.height, 6);
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.5;
  ctx.stroke();
  drawText(ctx, options.title, rect.x + rect.width / 2, rect.y + 4, {
    size: 11,
    weight: 700,
    color: options.titleColor,
    align: "center",
  });
  drawText(ctx, options.subtitle, rect.x + rect.width / 2, rect.y + 17, {
    size: 10,
    color: PALETTE.muted,
    align: "center",
  });

  const chipX = rect.x + SHIFT_PADDING;
  const chipWidth = rect.width - SHIFT_PADDING * 2;
  const chipTop = (index: number) => rect.y + SHIFT_TITLE_HEIGHT + index * (SHIFT_CHIP.height + SHIFT_CHIP.gap);
  SHIFT_OUTCOMES.forEach((outcome, index) => {
    const top = chipTop(index);
    const greyed = options.mode === "forced" && !outcome.forcible;
    ctx.globalAlpha = greyed ? 0.3 : 1;
    ctx.beginPath();
    ctx.roundRect(chipX, top, chipWidth, SHIFT_CHIP.height, 3);
    ctx.fillStyle = outcome.fill;
    ctx.fill();
    ctx.strokeStyle = outcome.color;
    ctx.lineWidth = 1;
    ctx.stroke();
    drawText(ctx, outcome.text, chipX + chipWidth / 2, top + 1, {
      size: 10,
      lineHeight: 13,
      weight: outcome.bold ? 700 : 400,
      color: outcome.color,
      align: "center",
    });
    ctx.globalAlpha = 1;
  });

  if (options.mode === "forced") {
    const forcible = SHIFT_OUTCOMES.filter((outcome) => outcome.forcible).length;
    const top = chipTop(0) - 2;
    const bottom = chipTop(forcible - 1) + SHIFT_CHIP.height + 2;
    ctx.beginPath();
    ctx.roundRect(chipX - 3, top, chipWidth + 6, bottom - top, 4);
    ctx.strokeStyle = PALETTE.action;
    ctx.lineWidth = 2;
    ctx.stroke();
  }
};

export { SHIFT_WINDOW_HEIGHT, drawShiftWindow };
