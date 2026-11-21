import { drawSheet, drawText } from "../../kit/shapes";
import type { TDocument, TRect } from "../../core/types";

// Ostrov 9 - Артефакты.
// All coordinates are world units.

const SHEET: TRect = { x: 0, y: 0, width: 1600, height: 1000 };

const doc: TDocument = {
  id: "ostrov-artifacts",
  order: 9,
  title: "Ostrov 9 - Артефакты",
  description: "Артефакты",
  bounds: SHEET,
  draw: (ctx) => {
    drawSheet(ctx, SHEET);
    drawText(ctx, "Ostrov 9 - Артефакты", 50, 50, { size: 36, weight: 500 });
  },
};

export { doc };
