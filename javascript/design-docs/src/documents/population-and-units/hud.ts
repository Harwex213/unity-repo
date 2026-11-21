import { drawImageContain } from "../../kit/image";
import { FONT_FAMILY } from "../../kit/palette";
import { drawText, drawTextCenteredAt } from "../../kit/shapes";
import { ICON_ART } from "./art";
import { DIM_ALPHA, PALETTE } from "./palette";
import { drawPanel } from "./primitives";
import type { TPoint, TRect } from "../../core/types";

// The resources block of ostrov-prototype (src/ui/components/resources-panel.tsx):
// two panels, two rows each. Toxicity has its own meter there, so it is not in the block.

type TResourceId =
  | "food"
  | "wood"
  | "stone"
  | "population"
  | "mad"
  | "hammers"
  | "power"
  | "scouting"
  | "science"
  | "mana";

const PANEL_LAYOUT: TResourceId[][][] = [
  [
    ["food", "wood", "stone"],
    ["population", "mad", "hammers"],
  ],
  [
    ["power", "scouting"],
    ["science", "mana"],
  ],
];

const CELL = { width: 88, height: 40 };
const PANEL_PADDING = 8;
const PANEL_GAP = 12;
const ICON_SIZE = 28;

type THudState = {
  amounts: Record<TResourceId, number>;
  // The resources of the population system. The others are dim.
  bright: TResourceId[];
  // The food the population eats when the collection phase ends, as the prototype shows it.
  foodPending?: string;
};

const panelRect = (topLeft: TPoint, panelIndex: number): TRect => {
  let x = topLeft.x;
  for (let index = 0; index < panelIndex; index++) {
    const columns = PANEL_LAYOUT[index]?.[0]?.length ?? 0;
    x += columns * CELL.width + PANEL_PADDING * 2 + PANEL_GAP;
  }
  const rows = PANEL_LAYOUT[panelIndex] ?? [];
  const columns = rows[0]?.length ?? 0;
  return {
    x,
    y: topLeft.y,
    width: columns * CELL.width + PANEL_PADDING * 2,
    height: rows.length * CELL.height + PANEL_PADDING * 2,
  };
};

const cellRect = (topLeft: TPoint, id: TResourceId): TRect => {
  for (const [panelIndex, rows] of PANEL_LAYOUT.entries()) {
    for (const [rowIndex, ids] of rows.entries()) {
      const columnIndex = ids.indexOf(id);
      if (columnIndex >= 0) {
        const panel = panelRect(topLeft, panelIndex);
        return {
          x: panel.x + PANEL_PADDING + columnIndex * CELL.width,
          y: panel.y + PANEL_PADDING + rowIndex * CELL.height,
          ...CELL,
        };
      }
    }
  }
  throw new Error(`Unknown resource: ${id}`);
};

const HUD_SIZE = {
  width: panelRect({ x: 0, y: 0 }, 1).x + panelRect({ x: 0, y: 0 }, 1).width,
  height: panelRect({ x: 0, y: 0 }, 0).height,
};

const drawResourceCell = (ctx: CanvasRenderingContext2D, rect: TRect, id: TResourceId, state: THudState) => {
  ctx.save();
  ctx.globalAlpha = state.bright.includes(id) ? 1 : DIM_ALPHA;
  const middle = rect.y + rect.height / 2;
  drawImageContain(ctx, ICON_ART[id], {
    x: rect.x + 4,
    y: middle - ICON_SIZE / 2,
    width: ICON_SIZE,
    height: ICON_SIZE,
  });
  const amount = String(state.amounts[id]);
  drawTextCenteredAt(ctx, amount, rect.x + ICON_SIZE + 10, middle, { size: 17, weight: 600 });
  if (id === "food" && state.foodPending) {
    ctx.font = `600 17px ${FONT_FAMILY}`;
    const amountWidth = ctx.measureText(amount).width;
    drawText(ctx, state.foodPending, rect.x + ICON_SIZE + 14 + amountWidth, middle - 10, {
      size: 15,
      weight: 600,
      color: PALETTE.negative,
    });
  }
  ctx.restore();
};

const drawHud = (ctx: CanvasRenderingContext2D, topLeft: TPoint, state: THudState) => {
  for (const [panelIndex, rows] of PANEL_LAYOUT.entries()) {
    drawPanel(ctx, panelRect(topLeft, panelIndex));
    for (const ids of rows) {
      for (const id of ids) {
        drawResourceCell(ctx, cellRect(topLeft, id), id, state);
      }
    }
  }
};

export { HUD_SIZE, cellRect, drawHud, drawResourceCell };
export type { THudState, TResourceId };
