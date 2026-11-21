import { traceHex } from "../../kit/hex";
import { createImageAsset, drawImageContain } from "../../kit/image";
import { drawText } from "../../kit/shapes";
import { PALETTE } from "./palette";
import deadArt from "./assets/dead.png";
import type { TPoint, TRect } from "../../core/types";

// The contamination of the whole island in ostrov-prototype (src/core/toxic-slot.ts, toxic-panel.tsx).
// Every building adds the waste of its rolled face to one meter of the island, 0..1000.
// The meter has four zones. From the second zone on, a slot of three reels spins
// when the tax phase ends, and the zone sets the chance of a bad symbol on each reel.

const METER_MAX = 1000;
// The borders of the zones "Малые", "Значительные" and "Катастрофические".
const METER_TICKS = [250, 500, 800];
// The meter of the document stands in the zone "Значительные". The zone sets the strength of the slot.
const METER_VALUE = 560;
const METER_ZONE = "Значительные";

// The prototype draws the meter as a vertical flask with ticks at the zone borders,
// the counter and the zone under it. `rect` is the body of the flask.
// The counter and the zone are right-aligned on `textRight`, so they stay inside a narrow strip.
const drawIslandMeter = (ctx: CanvasRenderingContext2D, rect: TRect, textRight: number) => {
  const neck = { width: rect.width * 0.5, height: 8 };
  ctx.lineWidth = 1.5;
  ctx.strokeStyle = PALETTE.ink;
  ctx.fillStyle = PALETTE.paper;
  ctx.beginPath();
  ctx.rect(rect.x + (rect.width - neck.width) / 2, rect.y - neck.height, neck.width, neck.height);
  ctx.fill();
  ctx.stroke();

  ctx.beginPath();
  ctx.roundRect(rect.x, rect.y, rect.width, rect.height, 4);
  ctx.fill();
  const fillHeight = (rect.height * METER_VALUE) / METER_MAX;
  ctx.save();
  ctx.clip();
  ctx.fillStyle = PALETTE.toxic;
  ctx.fillRect(rect.x, rect.y + rect.height - fillHeight, rect.width, fillHeight);
  ctx.restore();
  ctx.beginPath();
  ctx.roundRect(rect.x, rect.y, rect.width, rect.height, 4);
  ctx.stroke();

  // The ticks stick out on the left of the flask.
  ctx.beginPath();
  for (const tick of METER_TICKS) {
    const y = rect.y + rect.height - (rect.height * tick) / METER_MAX;
    ctx.moveTo(rect.x - 4, y);
    ctx.lineTo(rect.x + rect.width * 0.5, y);
  }
  ctx.lineWidth = 1;
  ctx.stroke();

  drawText(ctx, `${METER_VALUE}/${METER_MAX}\n${METER_ZONE}`, textRight, rect.y + rect.height + 3, {
    size: 10,
    lineHeight: 13,
    color: PALETTE.negative,
    align: "right",
  });
};

// A dashed cubic curve with an open head along its last direction.
const drawDashedCurve = (
  ctx: CanvasRenderingContext2D,
  points: { start: TPoint; control1: TPoint; control2: TPoint; end: TPoint },
  color: string,
) => {
  const { start, control1, control2, end } = points;
  ctx.strokeStyle = color;
  ctx.lineWidth = 1.5;
  ctx.setLineDash([3, 3]);
  ctx.beginPath();
  ctx.moveTo(start.x, start.y);
  ctx.bezierCurveTo(control1.x, control1.y, control2.x, control2.y, end.x, end.y);
  ctx.stroke();
  ctx.setLineDash([]);
  const angle = Math.atan2(end.y - control2.y, end.x - control2.x);
  const head = 9;
  ctx.beginPath();
  ctx.moveTo(end.x - head * Math.cos(angle - 0.6), end.y - head * Math.sin(angle - 0.6));
  ctx.lineTo(end.x, end.y);
  ctx.lineTo(end.x - head * Math.cos(angle + 0.6), end.y - head * Math.sin(angle + 0.6));
  ctx.stroke();
};

// ---------- Hex decay ----------

// A hex dies at 100% toxicity in the prototype (src/core/tax.ts, DEAD_TOXICITY_PCT).
// The island canvas tints a hex by toxicity / 160 and puts a skull on a dead hex: hex-buildings/dead.png.
const DEAD_ART = createImageAsset(deadArt);
const DECAY_STAGES = [0, 35, 70, 100];
const DECAY_HEX_SIZE = 13;
const DECAY_STEP = 40;

// A row of hexes from clean to dead, with small arrows between them and the toxicity under each hex.
// `center` is the middle of the row.
const drawHexDecay = (ctx: CanvasRenderingContext2D, center: TPoint) => {
  const firstX = center.x - ((DECAY_STAGES.length - 1) * DECAY_STEP) / 2;
  DECAY_STAGES.forEach((percent, index) => {
    const hex = { x: firstX + index * DECAY_STEP, y: center.y };
    traceHex(ctx, hex, DECAY_HEX_SIZE);
    ctx.fillStyle = PALETTE.paper;
    ctx.fill();
    ctx.globalAlpha = Math.min(1, percent / 160);
    ctx.fillStyle = PALETTE.toxic;
    ctx.fill();
    ctx.globalAlpha = 1;
    ctx.strokeStyle = PALETTE.ink;
    ctx.lineWidth = 1.25;
    ctx.stroke();
    if (percent >= 100) {
      const side = DECAY_HEX_SIZE * 1.3;
      drawImageContain(ctx, DEAD_ART, { x: hex.x - side / 2, y: hex.y - side / 2, width: side, height: side });
    }
    drawText(ctx, `${percent}%`, hex.x, hex.y + DECAY_HEX_SIZE + 2, {
      size: 10,
      color: percent >= 100 ? PALETTE.negative : PALETTE.muted,
      align: "center",
    });

    if (index > 0) {
      const halfWidth = (Math.sqrt(3) / 2) * DECAY_HEX_SIZE;
      const from = hex.x - DECAY_STEP + halfWidth + 3;
      const to = hex.x - halfWidth - 3;
      const head = 3;
      ctx.beginPath();
      ctx.moveTo(from, hex.y);
      ctx.lineTo(to, hex.y);
      ctx.moveTo(to - head, hex.y - head);
      ctx.lineTo(to, hex.y);
      ctx.lineTo(to - head, hex.y + head);
      ctx.strokeStyle = PALETTE.ink;
      ctx.lineWidth = 1.25;
      ctx.stroke();
    }
  });
};

// ---------- Slot ----------

// The slot of the prototype (slot-modal.tsx) as a mini window: the title, the zone of the meter,
// three reels and the event. The event "Налёт нечисти" comes from the catalog
// of the zone "Значительные" with two bad symbols (a misfortune).
const SLOT_WINDOW_SIZE = { width: 196, height: 118 };
const SLOT_REEL_SIZE = 28;
const SLOT_REEL_GAP = 8;
type TReelSymbol = "skull" | "poison" | "coin";
const SLOT_REELS: TReelSymbol[] = ["skull", "coin", "poison"];

const drawReelSymbol = (ctx: CanvasRenderingContext2D, symbol: TReelSymbol, center: TPoint) => {
  ctx.strokeStyle = symbol === "coin" ? PALETTE.ink : PALETTE.negative;
  ctx.fillStyle = symbol === "coin" ? PALETTE.ink : PALETTE.negative;
  ctx.lineWidth = 1.5;
  if (symbol === "skull") {
    ctx.beginPath();
    ctx.arc(center.x, center.y - 1.5, 6.5, Math.PI * 0.85, Math.PI * 0.15);
    ctx.lineTo(center.x + 3.5, center.y + 7);
    ctx.lineTo(center.x - 3.5, center.y + 7);
    ctx.closePath();
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(center.x - 2.6, center.y - 1, 1.6, 0, Math.PI * 2);
    ctx.arc(center.x + 2.6, center.y - 1, 1.6, 0, Math.PI * 2);
    ctx.fill();
    return;
  }
  if (symbol === "poison") {
    ctx.beginPath();
    ctx.moveTo(center.x, center.y - 8);
    ctx.bezierCurveTo(center.x + 7, center.y, center.x + 6, center.y + 7, center.x, center.y + 7);
    ctx.bezierCurveTo(center.x - 6, center.y + 7, center.x - 7, center.y, center.x, center.y - 8);
    ctx.stroke();
    return;
  }
  ctx.beginPath();
  ctx.arc(center.x, center.y, 7, 0, Math.PI * 2);
  ctx.stroke();
  ctx.beginPath();
  ctx.arc(center.x, center.y, 3.5, 0, Math.PI * 2);
  ctx.lineWidth = 1;
  ctx.stroke();
};

const drawSlotWindow = (ctx: CanvasRenderingContext2D, topLeft: TPoint) => {
  const { x, y } = topLeft;
  const { width, height } = SLOT_WINDOW_SIZE;
  ctx.beginPath();
  ctx.roundRect(x, y, width, height, 6);
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.5;
  ctx.stroke();
  drawText(ctx, "Слот заражения", x + 8, y + 6, { size: 11, weight: 700 });
  drawText(ctx, METER_ZONE, x + width - 8, y + 7, { size: 10, color: PALETTE.negative, align: "right" });

  const reelsLeft = x + (width - (SLOT_REEL_SIZE * 3 + SLOT_REEL_GAP * 2)) / 2;
  SLOT_REELS.forEach((symbol, index) => {
    const reel: TRect = {
      x: reelsLeft + index * (SLOT_REEL_SIZE + SLOT_REEL_GAP),
      y: y + 26,
      width: SLOT_REEL_SIZE,
      height: SLOT_REEL_SIZE,
    };
    ctx.beginPath();
    ctx.roundRect(reel.x, reel.y, reel.width, reel.height, 4);
    ctx.fillStyle = PALETTE.negativeFill;
    ctx.fill();
    ctx.strokeStyle = PALETTE.negative;
    ctx.lineWidth = 1.25;
    ctx.stroke();
    drawReelSymbol(ctx, symbol, { x: reel.x + reel.width / 2, y: reel.y + reel.height / 2 });
  });

  const event: TRect = { x: x + 8, y: y + 62, width: width - 16, height: 48 };
  ctx.strokeStyle = PALETTE.negative;
  ctx.lineWidth = 1;
  ctx.strokeRect(event.x, event.y, event.width, event.height);
  drawText(ctx, "Несчастье: Налёт нечисти", event.x + 6, event.y + 4, {
    size: 10,
    weight: 700,
    color: PALETTE.negative,
  });
  drawText(ctx, "−2 жителя, −2 бойца", event.x + 6, event.y + 20, { size: 10 });
  drawText(ctx, "хуже при большем ☣️ заражении", event.x + 6, event.y + 33, { size: 9, color: PALETTE.muted });
};

const CONTAMINATION_ART_ASSETS = [DEAD_ART];

export {
  CONTAMINATION_ART_ASSETS,
  SLOT_WINDOW_SIZE,
  drawDashedCurve,
  drawHexDecay,
  drawIslandMeter,
  drawSlotWindow,
};
