import { drawSheet, drawText } from "../../kit/shapes";
import type { TDocument, TRect } from "../../core/types";

// Ostrov 6 - Смены.
// All coordinates are world units.

const SHEET: TRect = { x: 0, y: 0, width: 1600, height: 1000 };

const doc: TDocument = {
  id: "ostrov-shifts",
  order: 6,
  title: "Ostrov 6 - Смены",
  description: "Смены",
  bounds: SHEET,
  draw: (ctx) => {
    drawSheet(ctx, SHEET);
    drawText(ctx, "Ostrov 6 - Смены", 50, 50, { size: 36, weight: 500 });
  },
};

export { doc };
