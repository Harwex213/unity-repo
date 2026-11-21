import { traceHex } from "../../kit/hex";
import { createImageAsset } from "../../kit/image";
import { MONO_FONT_FAMILY, PALETTE } from "../../kit/palette";
import { drawSheet, drawText } from "../../kit/shapes";
import badlandsTexture from "./assets/badlands.webp";
import cliffsTexture from "./assets/cliffs.webp";
import craterTexture from "./assets/crater.webp";
import desertTexture from "./assets/desert.webp";
import forrestTexture from "./assets/forrest.webp";
import grasslandTexture from "./assets/grassland.webp";
import hillsTexture from "./assets/hills.webp";
import mountainsTexture from "./assets/mountains.webp";
import plainsTexture from "./assets/plains.webp";
import polarDesertTexture from "./assets/polar_desert.webp";
import rainforestTexture from "./assets/rainforest.webp";
import savannaTexture from "./assets/savanna.webp";
import swampTexture from "./assets/swamp.webp";
import taigaTexture from "./assets/taiga.webp";
import tundraTexture from "./assets/tundra.webp";
import volcanoTexture from "./assets/volcano.webp";
import type { TDocument, TRect } from "../../core/types";
import type { TImageAsset } from "../../kit/image";

// The 16 hex biomes. Each biome is one combination of four properties.
// Each property has two values: "+" and "−".
// The table puts R and F on the columns, U and L on the rows,
// so each cell of the 4×4 table holds exactly one biome.

type TSign = "+" | "−";
type TAxisId = "R" | "F" | "L" | "U";

type TAxis = {
  plus: string;
  minus: string;
};

const AXES: Record<TAxisId, TAxis> = {
  R: { plus: "Много полезных ископаемых", minus: "Мало полезных ископаемых" },
  F: { plus: "Много еды", minus: "Еда отсутствует" },
  L: { plus: "Легкий ландшафт\nдля жизни", minus: "Сложный ландшафт\nдля жизни" },
  U: { plus: "Частый", minus: "Редкий" },
};

type TBiome = {
  // The id of the biome in ostrov-prototype (src/core/biomes.ts).
  id: string;
  name: string;
  // The ground texture and the flat color of the biome in ostrov-prototype.
  texture: TImageAsset;
  color: string;
  signs: Record<TAxisId, TSign>;
};

const biome = (id: string, name: string, texture: string, color: string, code: string): TBiome => {
  const [r, f, l, u] = [...code].map((mark) => (mark === "+" ? "+" : "−")) as [TSign, TSign, TSign, TSign];
  return { id, name, texture: createImageAsset(texture), color, signs: { R: r, F: f, L: l, U: u } };
};

// The order of the spec list. The code is the signs of R, F, L and U.
// The spec calls the R+ F+ L+ U+ biome "степи"; the list and the prototype call it plains.
const BIOMES: TBiome[] = [
  biome("grassland", "Умеренные луга", grasslandTexture, "#6fae4f", "-++-"),
  biome("plains", "Равнина", plainsTexture, "#a8bd5e", "++++"),
  biome("forrest", "Лес", forrestTexture, "#3f7d43", "++-+"),
  biome("savanna", "Саванна", savannaTexture, "#c2a24c", "-+++"),
  biome("rainforest", "Джунгли", rainforestTexture, "#2f7d5c", "-+-+"),
  biome("taiga", "Хвойный лес", taigaTexture, "#356b55", "+++-"),
  biome("tundra", "Тундра", tundraTexture, "#8fa79b", "+-++"),
  biome("desert", "Пустыня", desertTexture, "#dcc179", "+-+-"),
  biome("polar_desert", "Ледяная пустыня", polarDesertTexture, "#c9dbe4", "--+-"),
  biome("swamp", "Заболоченный биом", swampTexture, "#5c6b3a", "++--"),
  biome("badlands", "Бесплодные земли", badlandsTexture, "#a2603f", "--++"),
  biome("crater", "Кратер", craterTexture, "#7a6f66", "----"),
  biome("volcano", "Вулкан", volcanoTexture, "#6b3733", "-+--"),
  biome("hills", "Холмы", hillsTexture, "#8a9a4e", "+--+"),
  biome("mountains", "Горы", mountainsTexture, "#8d8f96", "+---"),
  biome("cliffs", "Утёсы", cliffsTexture, "#7e8894", "---+"),
];

// The background of a table header.
const HEADER_FILL = "#f1f3f5";

// Layout in world units.
const MARGIN = 40;
const TABLE_TOP = 160;
const GROUP_HEADER_SIZE = 130;
const SUB_HEADER_SIZE = 180;
const COLUMN_GROUP_HEIGHT = 64;
const COLUMN_SUB_HEIGHT = 64;
const CELL_WIDTH = 230;
const CELL_HEIGHT = 220;
const HEX_SIZE = 50;

const CELLS: TRect = {
  x: MARGIN + GROUP_HEADER_SIZE + SUB_HEADER_SIZE,
  y: TABLE_TOP + COLUMN_GROUP_HEIGHT + COLUMN_SUB_HEIGHT,
  width: CELL_WIDTH * 4,
  height: CELL_HEIGHT * 4,
};

const SHEET: TRect = {
  x: 0,
  y: 0,
  width: CELLS.x + CELLS.width + MARGIN,
  height: CELLS.y + CELLS.height + MARGIN,
};

const signIndex = (sign: TSign) => (sign === "+" ? 0 : 1);

// Columns: R+F+, R+F−, R−F+, R−F−. Rows: U+L+, U+L−, U−L+, U−L−.
const cellRect = (column: number, row: number): TRect => ({
  x: CELLS.x + column * CELL_WIDTH,
  y: CELLS.y + row * CELL_HEIGHT,
  width: CELL_WIDTH,
  height: CELL_HEIGHT,
});

const biomeCell = ({ signs }: TBiome): TRect =>
  cellRect(signIndex(signs.R) * 2 + signIndex(signs.F), signIndex(signs.U) * 2 + signIndex(signs.L));

const axisLabel = (axis: TAxisId, sign: TSign) => (sign === "+" ? AXES[axis].plus : AXES[axis].minus);

const CODE_SIZE = 18;
const CODE_LINE = 24;
const LABEL_SIZE = 15;
const LABEL_LINE = 20;

// A header: the code on the first line, the meaning of the code below.
const drawHeader = (ctx: CanvasRenderingContext2D, rect: TRect, axis: TAxisId, sign: TSign) => {
  ctx.fillStyle = HEADER_FILL;
  ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
  ctx.strokeStyle = PALETTE.sheetBorder;
  ctx.lineWidth = 1;
  ctx.strokeRect(rect.x, rect.y, rect.width, rect.height);

  const label = axisLabel(axis, sign);
  const height = CODE_LINE + label.split("\n").length * LABEL_LINE;
  const x = rect.x + rect.width / 2;
  const top = rect.y + (rect.height - height) / 2;
  drawText(ctx, `${axis}${sign}`, x, top, { size: CODE_SIZE, weight: 700, align: "center", lineHeight: CODE_LINE });
  drawText(ctx, label, x, top + CODE_LINE, { size: LABEL_SIZE, align: "center", lineHeight: LABEL_LINE });
};

const SIGNS: TSign[] = ["+", "−"];

const drawHeaders = (ctx: CanvasRenderingContext2D) => {
  SIGNS.forEach((outer, outerIndex) => {
    // R over two columns, F over each column.
    const columnX = CELLS.x + outerIndex * CELL_WIDTH * 2;
    drawHeader(ctx, { x: columnX, y: TABLE_TOP, width: CELL_WIDTH * 2, height: COLUMN_GROUP_HEIGHT }, "R", outer);
    // U over two rows, L over each row.
    const rowY = CELLS.y + outerIndex * CELL_HEIGHT * 2;
    drawHeader(ctx, { x: MARGIN, y: rowY, width: GROUP_HEADER_SIZE, height: CELL_HEIGHT * 2 }, "U", outer);

    SIGNS.forEach((inner, innerIndex) => {
      drawHeader(
        ctx,
        {
          x: columnX + innerIndex * CELL_WIDTH,
          y: TABLE_TOP + COLUMN_GROUP_HEIGHT,
          width: CELL_WIDTH,
          height: COLUMN_SUB_HEIGHT,
        },
        "F",
        inner,
      );
      drawHeader(
        ctx,
        {
          x: MARGIN + GROUP_HEADER_SIZE,
          y: rowY + innerIndex * CELL_HEIGHT,
          width: SUB_HEADER_SIZE,
          height: CELL_HEIGHT,
        },
        "L",
        inner,
      );
    });
  });
};

// The hex shows the ground texture of the prototype.
// The flat biome color stands in until the texture has loaded.
const drawBiomeHex = (ctx: CanvasRenderingContext2D, biome: TBiome, x: number, y: number) => {
  const center = { x, y };
  ctx.save();
  traceHex(ctx, center, HEX_SIZE);
  ctx.fillStyle = biome.color;
  ctx.fill();
  ctx.clip();
  const { image } = biome.texture;
  if (image.complete && image.naturalWidth > 0) {
    ctx.imageSmoothingQuality = "high";
    ctx.drawImage(image, x - HEX_SIZE, y - HEX_SIZE, HEX_SIZE * 2, HEX_SIZE * 2);
  }
  ctx.restore();

  traceHex(ctx, center, HEX_SIZE);
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.5;
  ctx.lineJoin = "round";
  ctx.stroke();
};

const drawBiomeCell = (ctx: CanvasRenderingContext2D, biome: TBiome) => {
  const cell = biomeCell(biome);
  const x = cell.x + cell.width / 2;
  drawBiomeHex(ctx, biome, x, cell.y + 24 + HEX_SIZE);
  drawText(ctx, biome.name, x, cell.y + 136, { size: 17, weight: 600, align: "center" });
  drawText(ctx, biome.id, x, cell.y + 162, { size: 13, color: PALETTE.muted, align: "center", family: MONO_FONT_FAMILY });
  const code = (["R", "F", "L", "U"] as const).map((axis) => `${axis}${biome.signs[axis]}`).join(" ");
  drawText(ctx, code, x, cell.y + 186, { size: 13, color: PALETTE.muted, align: "center" });
};

const drawGrid = (ctx: CanvasRenderingContext2D) => {
  ctx.strokeStyle = PALETTE.sheetBorder;
  ctx.lineWidth = 1;
  ctx.beginPath();
  for (let index = 1; index < 4; index++) {
    ctx.moveTo(CELLS.x + index * CELL_WIDTH, CELLS.y);
    ctx.lineTo(CELLS.x + index * CELL_WIDTH, CELLS.y + CELLS.height);
    ctx.moveTo(CELLS.x, CELLS.y + index * CELL_HEIGHT);
    ctx.lineTo(CELLS.x + CELLS.width, CELLS.y + index * CELL_HEIGHT);
  }
  ctx.stroke();

  // Bold lines split the table into the R+ and R− halves and into the U+ and U− halves.
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(CELLS.x + CELL_WIDTH * 2, TABLE_TOP);
  ctx.lineTo(CELLS.x + CELL_WIDTH * 2, CELLS.y + CELLS.height);
  ctx.moveTo(MARGIN, CELLS.y + CELL_HEIGHT * 2);
  ctx.lineTo(CELLS.x + CELLS.width, CELLS.y + CELL_HEIGHT * 2);
  ctx.stroke();
  ctx.strokeRect(CELLS.x, CELLS.y, CELLS.width, CELLS.height);
};

const draw = (ctx: CanvasRenderingContext2D) => {
  drawSheet(ctx, SHEET);

  drawText(ctx, "Ostrov 2 - Biomes", MARGIN, 40, { size: 36, weight: 700 });
  drawText(
    ctx,
    "Биом — сочетание четырёх свойств. У каждого свойства два значения: «+» и «−». Каждое сочетание даёт ровно один биом.",
    MARGIN,
    96,
    { size: 16, color: PALETTE.muted },
  );

  drawHeaders(ctx);
  for (const biome of BIOMES) {
    drawBiomeCell(ctx, biome);
  }
  drawGrid(ctx);
};

const doc: TDocument = {
  order: 2,
  id: "biomes",
  title: "Ostrov 2 - Biomes",
  description: "16 биомов как сочетания R, F, L, U",
  bounds: SHEET,
  draw,
  subscribe: (onChange) => {
    const unsubscribers = BIOMES.map((biome) => biome.texture.subscribe(onChange));
    return () => {
      for (const unsubscribe of unsubscribers) {
        unsubscribe();
      }
    };
  },
};

export { doc };
