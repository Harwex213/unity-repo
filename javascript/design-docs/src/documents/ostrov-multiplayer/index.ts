import { drawSheet, drawText } from "../../kit/shapes";
import type { TDocument, TRect } from "../../core/types";

// Ostrov 10 - Мультиплеер.
// All coordinates are world units.

const SHEET: TRect = { x: 0, y: 0, width: 1600, height: 1000 };

const doc: TDocument = {
  id: "ostrov-multiplayer",
  order: 10,
  title: "Ostrov 10 - Мультиплеер",
  description: "Мультиплеер",
  bounds: SHEET,
  draw: (ctx) => {
    drawSheet(ctx, SHEET);
    drawText(ctx, "Ostrov 10 - Мультиплеер", 50, 50, { size: 36, weight: 500 });
  },
};

export { doc };
