import { drawSheet, drawText } from "../../kit/shapes";
import type { TDocument, TRect } from "../../core/types";

// Ostrov 5 - Юниты.
// All coordinates are world units.

const SHEET: TRect = { x: 0, y: 0, width: 1600, height: 1000 };

const doc: TDocument = {
  id: "ostrov-units",
  order: 5,
  title: "Ostrov 5 - Юниты",
  description: "Юниты",
  bounds: SHEET,
  draw: (ctx) => {
    drawSheet(ctx, SHEET);
    drawText(ctx, "Ostrov 5 - Юниты", 50, 50, { size: 36, weight: 500 });
  },
};

export { doc };
