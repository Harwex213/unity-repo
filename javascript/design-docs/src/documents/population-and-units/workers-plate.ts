import { drawImageContain } from "../../kit/image";
import { FONT_FAMILY } from "../../kit/palette";
import { drawText } from "../../kit/shapes";
import { ICON_ART } from "./art";
import { PALETTE } from "./palette";
import { drawPanel } from "./primitives";
import type { TRect } from "../../core/types";

// The plate under a building of ostrov-prototype (hex-plates.tsx): workers present of needed,
// then the growth of the hex toxicity as a bar and a percent.
// The parts go from left to right; the bar takes the width that is left.

type TPlateStyle = {
  height: number;
  padding: number;
  gap: number;
  icon: number;
  toxicityIcon: number;
  text: number;
  percentText: number;
  barHeight: number;
};

// The plate of the farm card in the fragment.
const CARD_PLATE: TPlateStyle = {
  height: 30,
  padding: 6,
  gap: 6,
  icon: 22,
  toxicityIcon: 18,
  text: 15,
  percentText: 14,
  barHeight: 8,
};

// The plate on a hex of the island: smaller, so it stays inside the hex and the water under it.
const MAP_PLATE: TPlateStyle = {
  height: 24,
  padding: 4,
  gap: 4,
  icon: 18,
  toxicityIcon: 14,
  text: 13,
  percentText: 11,
  barHeight: 6,
};

const WORKERS = "1/1";
const PERCENT = "0%";

const measure = (ctx: CanvasRenderingContext2D, text: string, size: number) => {
  ctx.font = `700 ${size}px ${FONT_FAMILY}`;
  return ctx.measureText(text).width;
};

const drawWorkersPlate = (ctx: CanvasRenderingContext2D, rect: TRect, style: TPlateStyle) => {
  drawPanel(ctx, rect, { fill: PALETTE.highlightFill, radius: style.height / 5, width: 1.5 });
  const middle = rect.y + rect.height / 2;
  const right = rect.x + rect.width - style.padding;

  let x = rect.x + style.padding;
  drawImageContain(ctx, ICON_ART.population, { x, y: middle - style.icon / 2, width: style.icon, height: style.icon });
  x += style.icon + style.gap;
  drawText(ctx, WORKERS, x, middle - (style.text * 1.35) / 2, { size: style.text, weight: 700 });
  x += measure(ctx, WORKERS, style.text) + style.gap * 2;
  drawImageContain(ctx, ICON_ART.toxicity, {
    x,
    y: middle - style.toxicityIcon / 2,
    width: style.toxicityIcon,
    height: style.toxicityIcon,
  });
  x += style.toxicityIcon + style.gap;

  const percentX = right - measure(ctx, PERCENT, style.percentText);
  const bar: TRect = { x, y: middle - style.barHeight / 2, width: percentX - style.gap - x, height: style.barHeight };
  drawPanel(ctx, bar, { fill: PALETTE.paper, radius: style.barHeight / 2, width: 1 });
  drawText(ctx, PERCENT, right, middle - (style.percentText * 1.35) / 2, {
    size: style.percentText,
    weight: 700,
    align: "right",
  });
};

export { CARD_PLATE, MAP_PLATE, drawWorkersPlate };
export type { TPlateStyle };
