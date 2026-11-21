import { offsetToPoint } from "../../kit/hex";
import { drawImageContain } from "../../kit/image";
import { BUILDING_ART, STRONGHOLD_ART } from "./art";
import { onScreen } from "./screen";
import { STYLE, hex, squareAround } from "./wire";
import type { TImageAsset } from "../../kit/image";
import type { TOffset } from "../../kit/hex";
import type { TPoint, TRect } from "../../core/types";

// The island field of the build phase: hexes with the buildings of the player.
// Each string is one row of hexes: "o" is a hex, "." is water.
// Odd rows are shifted right by half a hex, as in ostrov-prototype.
const ISLAND_ROWS = ["ooooo", "oooooo", "oooooo", "ooooo", ".ooo"];

// The island stands in the middle of the screen, between the players panel and the selected building panel.
const ISLAND_AREA: TRect = onScreen(320, 110, 800, 540);

// The selected hex: its panel is open on the right. It is the last hex of the top row,
// so the link from it to the panel runs right over the water and crosses no hex.
const SELECTED: TOffset = { col: 4, row: 0 };

const BUILDINGS: { cell: TOffset; art: TImageAsset }[] = [
  { cell: SELECTED, art: BUILDING_ART.village },
  { cell: { col: 1, row: 0 }, art: BUILDING_ART.mine },
  { cell: { col: 2, row: 0 }, art: BUILDING_ART.sawmill },
  { cell: { col: 0, row: 2 }, art: BUILDING_ART.farm },
  { cell: { col: 2, row: 2 }, art: STRONGHOLD_ART },
  { cell: { col: 4, row: 2 }, art: BUILDING_ART.university },
  { cell: { col: 3, row: 3 }, art: BUILDING_ART.masonsGuild },
  { cell: { col: 5, row: 1 }, art: BUILDING_ART.observatory },
];

const ISLAND_CELLS: TOffset[] = ISLAND_ROWS.flatMap((line, row) =>
  [...line].flatMap((mark, col) => (mark === "o" ? [{ col, row }] : [])),
);

type TIslandLayout = {
  size: number;
  centerOf: (cell: TOffset) => TPoint;
};

// The island is centered in its area at the largest hex size that fits.
const islandLayout = (): TIslandLayout => {
  // The island bounds in units of one hex size.
  const unitPoints = ISLAND_CELLS.map((cell) => offsetToPoint(cell, 1));
  const xs = unitPoints.map((point) => point.x);
  const ys = unitPoints.map((point) => point.y);
  const halfWidth = Math.sqrt(3) / 2;
  const minX = Math.min(...xs) - halfWidth;
  const maxX = Math.max(...xs) + halfWidth;
  const minY = Math.min(...ys) - 1;
  const maxY = Math.max(...ys) + 1;
  const size = Math.min(ISLAND_AREA.width / (maxX - minX), ISLAND_AREA.height / (maxY - minY));

  const origin: TPoint = {
    x: ISLAND_AREA.x + ISLAND_AREA.width / 2 - ((minX + maxX) / 2) * size,
    y: ISLAND_AREA.y + ISLAND_AREA.height / 2 - ((minY + maxY) / 2) * size,
  };
  return {
    size,
    centerOf: (cell) => {
      const point = offsetToPoint(cell, size);
      return { x: origin.x + point.x, y: origin.y + point.y };
    },
  };
};

// The art covers most of the hex and leaves its corners free. The side is in hex sizes.
const ART_SIDE = 1.45;
const ART_LIFT = 0.05;

const drawIsland = (ctx: CanvasRenderingContext2D) => {
  const { size, centerOf } = islandLayout();
  for (const cell of ISLAND_CELLS) {
    hex(ctx, centerOf(cell), size, STYLE.hex);
  }
  for (const building of BUILDINGS) {
    const center = centerOf(building.cell);
    drawImageContain(ctx, building.art, squareAround({ x: center.x, y: center.y - ART_LIFT * size }, ART_SIDE * size));
  }
  // The bold outline goes over the art, so it stays whole.
  hex(ctx, centerOf(SELECTED), size, STYLE.selectedHex);
};

// The right corner of the selected hex, a little outside its bold outline.
const selectedHexRight = (gap: number): TPoint => {
  const { size, centerOf } = islandLayout();
  const center = centerOf(SELECTED);
  return { x: center.x + (Math.sqrt(3) / 2) * size + gap, y: center.y };
};

export { drawIsland, selectedHexRight };
