import { BLOCK_FRAME, drawBlockFrame, drawItem } from "./blocks";
import {
  BRIGHT_TONE,
  SECTION_HEIGHT,
  SECTION_WIDTH,
  drawActiveDefense,
  drawHeader,
  drawModules,
  drawPassiveDefense,
  drawPopulation,
  drawResources,
  drawVillage,
  drawWorkers,
} from "./sections";
import { rect } from "./wire";
import type { TRect } from "../../core/types";

// Block 5: the possible sections of a building panel. The panel of a building is put together from them.
// Each item has a small wireframe of its section on the right, in the bright tone.
// The texts come from the user word for word, with the names of all documents:
// "токсичность" is "заражение", and the contamination of every turn is "прироста", as in blocks 3 and 4.
// A resource name has its icon in front, as in block 3.
// The header, the modules, the village launch, the workers and the dice come from the panel.
// The population, the resources with the income of every turn and both defenses are new.

type TSectionItem = {
  text: string;
  height: number;
  draw: (ctx: CanvasRenderingContext2D, area: TRect) => void;
};

const SECTION_ITEMS: TSectionItem[] = [
  {
    text: "Секция заголовка (название, сколько ХП)",
    height: SECTION_HEIGHT.header,
    // The header has no frame in the panel. Here a frame shows where the section ends.
    draw: (ctx, area) => {
      rect(ctx, area, 8, BRIGHT_TONE.frame);
      drawHeader(ctx, area, BRIGHT_TONE);
    },
  },
  {
    text: "Секция модулей (какие модули есть)",
    height: SECTION_HEIGHT.modules,
    draw: (ctx, area) => drawModules(ctx, area, BRIGHT_TONE),
  },
  {
    text: "Секция запуска прироста 🧍 жителей",
    height: SECTION_HEIGHT.village,
    draw: (ctx, area) => drawVillage(ctx, area, BRIGHT_TONE),
  },
  {
    text: "Секция отображения количества рабочих\n(сколько 🧍 рабочих в здании находится)",
    height: SECTION_HEIGHT.workers,
    draw: (ctx, area) => drawWorkers(ctx, area, BRIGHT_TONE),
  },
  {
    text: "Секция отображения населения\n(сколько 🧍 населения живёт,\nсколько 🧍 населения может вмещать)",
    height: SECTION_HEIGHT.population,
    draw: (ctx, area) => drawPopulation(ctx, area, BRIGHT_TONE),
  },
  {
    text:
      "Секция ресурсов (значения смен для каждого\n" +
      "варианта с уже учтённым коэффициентом\n" +
      "изменения значений смен из-за биома,\n" +
      "значения фикс дохода ресурсов каждый ход,\n" +
      "значения фикс прироста ☣️ заражения\n" +
      "каждый ход)",
    height: SECTION_HEIGHT.resources,
    draw: (ctx, area) => drawResources(ctx, area, BRIGHT_TONE),
  },
  {
    text: "Секция пассивной обороны\n(уровень атаки мин)",
    height: SECTION_HEIGHT.passiveDefense,
    draw: (ctx, area) => drawPassiveDefense(ctx, area, BRIGHT_TONE),
  },
  {
    text: "Секция активной обороны\n(уровень атаки, скорость атаки,\nдальность атаки)",
    height: SECTION_HEIGHT.activeDefense,
    draw: (ctx, area) => drawActiveDefense(ctx, area, BRIGHT_TONE),
  },
];

const SECTIONS_BLOCK = { number: 5, title: "Возможные секции здания" };

// The text column on the left, the wireframe column on the right.
const TEXT_WIDTH = 440;
const COLUMN_GAP = 30;
const ROW_GAP = 18;

const rowHeight = (item: TSectionItem) =>
  Math.max(item.text.split("\n").length * BLOCK_FRAME.lineHeight, item.height);

const SECTIONS_BLOCK_WIDTH = BLOCK_FRAME.padding * 2 + TEXT_WIDTH + COLUMN_GAP + SECTION_WIDTH;
const SECTIONS_BLOCK_HEIGHT =
  BLOCK_FRAME.top +
  SECTION_ITEMS.reduce((sum, item) => sum + rowHeight(item), 0) +
  ROW_GAP * (SECTION_ITEMS.length - 1) +
  BLOCK_FRAME.padding;

const drawSectionsBlock = (ctx: CanvasRenderingContext2D, area: TRect) => {
  let top = drawBlockFrame(ctx, SECTIONS_BLOCK.number, SECTIONS_BLOCK.title, area);
  const left = area.x + BLOCK_FRAME.padding;
  const sectionX = left + TEXT_WIDTH + COLUMN_GAP;
  for (const item of SECTION_ITEMS) {
    drawItem(ctx, item.text, left, top);
    item.draw(ctx, { x: sectionX, y: top, width: SECTION_WIDTH, height: item.height });
    top += rowHeight(item) + ROW_GAP;
  }
};

export { SECTIONS_BLOCK_HEIGHT, SECTIONS_BLOCK_WIDTH, drawSectionsBlock };
