import { drawText, drawTextCenteredAt } from "../../kit/shapes";
import { PALETTE } from "./palette";
import { STYLE, circle, rect } from "./wire";
import type { TRect } from "../../core/types";

// The four blocks next to the HUD: the conditions to build a building or a module,
// and the characteristics of buildings and modules. The texts come from the user word for word.
// "токсичность" is "заражение" in all documents now.
// A resource name has its icon in front, as in the resource legend of ostrov-core-cycle.
// "ресурсов" of the fixed income has no icon: a building can pay any resource.
// The price lists the icons of the building resources, as block 1.
// An item with "\n" wraps: its next lines are indented under the first one.

type TBlock = {
  number: number;
  title: string;
  items: string[];
};

const CONDITIONS = [
  "кол-во ресурса 🪨 камень",
  "кол-во ресурса 🪵 дерево",
  "кол-во ресурса ⚙️ механизмы",
  "кол-во ресурса 🖥️ микропроцессоры",
  "технологии",
  "артефакты",
];

const BUILDING_CONDITIONS: TBlock = {
  number: 1,
  title: "Возможные условия постройки зданий",
  items: CONDITIONS,
};

const MODULE_CONDITIONS: TBlock = {
  number: 2,
  title: "Возможные условия постройки модулей",
  items: CONDITIONS,
};

const BUILDING_STATS: TBlock = {
  number: 3,
  title: "Характеристики зданий",
  items: [
    "название",
    "сколько ХП",
    "на каких биомах может строиться",
    "значения смен для каждого варианта\n(крайне плохая, плохая, обычная, хорошая, отличная)",
    "коэффициент изменения значений смен из-за биома",
    "уровень атаки",
    "скорость атаки",
    "дальность атаки",
    "существует ли триггер мин?",
    "уровень атаки мин",
    "значения фикс дохода ресурсов каждый ход",
    "значения фикс прироста ☣️ заражения каждый ход",
    "сколько 🧍 населения может вмещать",
    "диапазон 🧍 населения, которое может дать за раз",
    "сколько 🧍 рабочих в здании находится",
    "сколько 🔭 электричества надо",
    "какие модули есть",
    "цена в ресурсах 🪨 🪵 ⚙️ 🖥️",
    "необходимые артефакты для постройки",
    "необходимые технологии для постройки",
  ],
};

const MODULE_STATS: TBlock = {
  number: 4,
  title: "Характеристики модулей",
  items: [
    "название",
    "в каких зданиях может достраиваться",
    "добавочные значения смен\n(крайне плохая, плохая, обычная, хорошая, отличная)",
    "уровень атаки",
    "скорость атаки",
    "дальность атаки",
    "существует ли триггер мин?",
    "уровень атаки мин",
    "значения фикс дохода ресурсов каждый ход",
    "значения фикс прироста ☣️ заражения каждый ход",
  ],
};

const PADDING = 20;
const BADGE_RADIUS = 15;
const TITLE = { size: 20, weight: 700 };
const TITLE_HEIGHT = 32;
const TITLE_GAP = 12;
const ITEM = { size: 18, lineHeight: 27 };
const BULLET_INDENT = 22;

const lineCount = (block: TBlock) => block.items.reduce((sum, item) => sum + item.split("\n").length, 0);

// The height of the block box with all its lines.
const blockHeight = (block: TBlock) => PADDING * 2 + TITLE_HEIGHT + TITLE_GAP + lineCount(block) * ITEM.lineHeight;

// The box with the number badge and the title. Returns the top of the content under the title.
const drawBlockFrame = (ctx: CanvasRenderingContext2D, number: number, title: string, area: TRect) => {
  rect(ctx, area, 8, STYLE.panel);
  const titleMiddle = area.y + PADDING + TITLE_HEIGHT / 2;
  const badge = { x: area.x + PADDING + BADGE_RADIUS, y: titleMiddle };
  circle(ctx, badge, BADGE_RADIUS, { fill: PALETTE.ink });
  drawTextCenteredAt(ctx, String(number), badge.x, badge.y, { ...TITLE, size: 17, color: PALETTE.paper, align: "center" });
  drawTextCenteredAt(ctx, title, badge.x + BADGE_RADIUS + 12, titleMiddle, TITLE);
  return area.y + PADDING + TITLE_HEIGHT + TITLE_GAP;
};

// A list item with its bullet. Returns the top of the next item.
const drawItem = (ctx: CanvasRenderingContext2D, item: string, left: number, top: number) => {
  drawText(ctx, "•", left + 4, top, ITEM);
  drawText(ctx, item, left + BULLET_INDENT, top, ITEM);
  return top + item.split("\n").length * ITEM.lineHeight;
};

// The number badge, the title and the bulleted list in a box.
const drawBlock = (ctx: CanvasRenderingContext2D, block: TBlock, area: TRect) => {
  let top = drawBlockFrame(ctx, block.number, block.title, area);
  for (const item of block.items) {
    top = drawItem(ctx, item, area.x + PADDING, top);
  }
};

// The space the frame takes: the padding on every side and the title on top.
const BLOCK_FRAME = { padding: PADDING, top: PADDING + TITLE_HEIGHT + TITLE_GAP, lineHeight: ITEM.lineHeight };

export {
  BLOCK_FRAME,
  BUILDING_CONDITIONS,
  BUILDING_STATS,
  MODULE_CONDITIONS,
  MODULE_STATS,
  blockHeight,
  drawBlock,
  drawBlockFrame,
  drawItem,
};
export type { TBlock };
