import { drawConnector } from "../../kit/shapes";
import { PHASE_FRAME_SIZE, drawFrame, drawMenuFrame, drawPhaseFrame, menuFrameHeight } from "./frames";
import { battlePhase } from "./phase-battle";
import { buildPhase } from "./phase-build";
import { movementPhase } from "./phase-movement";
import { resourcesPhase } from "./phase-resources";
import type { TDocument, TPoint, TRect } from "../../core/types";
import type { TPhaseScheme } from "./scheme";

// Ostrov 11 - Карта страниц: the pages of the game and the ways between them.
// Every page is a separate white frame. The menu pages are text wireframes;
// the four phases show the game screen from docs/spec/ui-phase-*.html.
// All coordinates are world units.

const rightOf = (rect: TRect) => rect.x + rect.width;
const bottomOf = (rect: TRect) => rect.y + rect.height;
const middleYOf = (rect: TRect) => rect.y + rect.height / 2;
const centerXOf = (rect: TRect) => rect.x + rect.width / 2;

// ---------- Texts of the menu pages ----------

const MAIN_LINES = [
  '- Кнопка "Новая Игра"',
  '- Кнопка "Продолжить Игру"',
  '- Кнопка "Сетевая Игра"',
  '- Кнопка "Настройки"',
  '- Кнопка "Выход"',
];

const NEW_GAME_LINES = [
  '- Кнопки "Вперёд", "Назад" для выбора флага',
  "- Color Picker бэкграунда флага и цвета в игре",
  '- Input "Название фракции"',
  '- Input "Название острова"',
  "",
  '- Кнопка "Добавить ИИ"',
  '- Select "Выбрать фракцию ИИ"',
  "",
  '- Кнопка "Назад"',
  '- Кнопка "Начать"',
];

const CONTINUE_LINES = [
  "- Список сохранённых игр",
  '- Кнопка "Выбрать" у сохранённой игры',
  "- Превью острова у сохранённой игры",
  '- Кнопка "Назад"',
  '- Кнопка "Продолжить"',
];

const NETWORK_LINES = [
  "- Список Текущих Игр (Название игры, Кол-во",
  "  игроков, Есть ли пароль)",
  '- Кнопка "Назад"',
  '- Кнопка "Создать Комнату"',
];

const SETTINGS_LINES = [
  "- Select Языка",
  "- Range Громкости Звука",
  "- Range Громкости Музыки",
  "- Настройки Графики",
  "    - Select Разрешения",
  "    - Select Качества текстур",
  "    - Select Качества теней",
  "    - Select Качества спецэффектов",
];

// ---------- Layout ----------

// The gap between two frames joined by an arrow.
const ARROW_GAP = 160;
// The vertical gap between the stacked menu frames leaves room for the title of the next frame.
const STACK_GAP = 80;

const MAIN_WIDTH = 300;
const MENU_WIDTH = 440;

// The menu column: "Новая Игра" on top, the other pages under it.
const MENU_X = MAIN_WIDTH + ARROW_GAP;
const NEW_GAME: TRect = { x: MENU_X, y: 0, width: MENU_WIDTH, height: menuFrameHeight(NEW_GAME_LINES) };
const CONTINUE: TRect = {
  x: MENU_X,
  y: bottomOf(NEW_GAME) + STACK_GAP,
  width: MENU_WIDTH,
  height: menuFrameHeight(CONTINUE_LINES),
};
const NETWORK: TRect = {
  x: MENU_X,
  y: bottomOf(CONTINUE) + STACK_GAP,
  width: MENU_WIDTH,
  height: menuFrameHeight(NETWORK_LINES),
};
const SETTINGS: TRect = {
  x: MENU_X,
  y: bottomOf(NETWORK) + STACK_GAP,
  width: MENU_WIDTH,
  height: menuFrameHeight(SETTINGS_LINES),
};

// The main row: the main page, "Новая Игра", "Начало Игры" and the phases share its middle line.
const ROW_Y = middleYOf(NEW_GAME);

const MAIN_HEIGHT = menuFrameHeight(MAIN_LINES);
const MAIN: TRect = { x: 0, y: ROW_Y - MAIN_HEIGHT / 2, width: MAIN_WIDTH, height: MAIN_HEIGHT };

// "Начало Игры" has no content yet. It has the size of "Новая Игра".
const GAME_START: TRect = { ...NEW_GAME, x: rightOf(NEW_GAME) + ARROW_GAP };

const phaseFrame = (x: number): TRect => ({ x, y: ROW_Y - PHASE_FRAME_SIZE.height / 2, ...PHASE_FRAME_SIZE });

const BUILD = phaseFrame(rightOf(GAME_START) + ARROW_GAP);
const RESOURCES = phaseFrame(rightOf(BUILD) + ARROW_GAP);
const SCOUTING = phaseFrame(rightOf(RESOURCES) + ARROW_GAP);
const BATTLE = phaseFrame(rightOf(SCOUTING) + ARROW_GAP);

const PHASES: { rect: TRect; title: string; scheme: TPhaseScheme }[] = [
  { rect: BUILD, title: "Фаза Строительства", scheme: buildPhase },
  { rect: RESOURCES, title: "Фаза Сбора Ресурсов", scheme: resourcesPhase },
  { rect: SCOUTING, title: "Фаза Разведки", scheme: movementPhase },
  { rect: BATTLE, title: "Фаза Боя", scheme: battlePhase },
];

// ---------- Arrows ----------

// The menu arrows split on this vertical line, between the main page and the menu column.
const BRANCH_X = rightOf(MAIN) + ARROW_GAP / 2;

const menuArrow = (target: TRect): TPoint[] => {
  const start = { x: rightOf(MAIN), y: ROW_Y };
  const tip = { x: target.x, y: middleYOf(target) };
  if (tip.y === start.y) {
    return [start, tip];
  }
  return [start, { x: BRANCH_X, y: start.y }, { x: BRANCH_X, y: tip.y }, tip];
};

const rowArrow = (from: TRect, to: TRect): TPoint[] => [
  { x: rightOf(from), y: ROW_Y },
  { x: to.x, y: ROW_Y },
];

// After the battle the next turn starts with the build phase. The arrow runs above the phase titles.
const RETURN_Y = BUILD.y - 110;
const RETURN_ARROW: TPoint[] = [
  { x: centerXOf(BATTLE), y: BATTLE.y },
  { x: centerXOf(BATTLE), y: RETURN_Y },
  { x: centerXOf(BUILD), y: RETURN_Y },
  { x: centerXOf(BUILD), y: BUILD.y },
];

const ARROWS: TPoint[][] = [
  ...[NEW_GAME, CONTINUE, NETWORK, SETTINGS].map(menuArrow),
  rowArrow(NEW_GAME, GAME_START),
  rowArrow(GAME_START, BUILD),
  rowArrow(BUILD, RESOURCES),
  rowArrow(RESOURCES, SCOUTING),
  rowArrow(SCOUTING, BATTLE),
  RETURN_ARROW,
];

const MARGIN = 60;
const BOUNDS: TRect = {
  x: -MARGIN,
  y: RETURN_Y - MARGIN,
  width: rightOf(BATTLE) + MARGIN * 2,
  height: Math.max(bottomOf(SETTINGS), bottomOf(BATTLE)) - RETURN_Y + MARGIN * 2,
};

const doc: TDocument = {
  id: "ostrov-page-map",
  order: 11,
  title: "Ostrov 11 - Карта страниц",
  description: "Карта страниц",
  bounds: BOUNDS,
  draw: (ctx) => {
    drawMenuFrame(ctx, MAIN, "Главная Страница", MAIN_LINES);
    drawMenuFrame(ctx, NEW_GAME, "Новая Игра", NEW_GAME_LINES);
    drawMenuFrame(ctx, CONTINUE, "Продолжить игру", CONTINUE_LINES);
    drawMenuFrame(ctx, NETWORK, "Сетевая Игра", NETWORK_LINES);
    drawMenuFrame(ctx, SETTINGS, "Настройки", SETTINGS_LINES);
    drawFrame(ctx, GAME_START, "Начало Игры");
    PHASES.forEach((phase) => drawPhaseFrame(ctx, phase.rect, phase.title, phase.scheme));
    ARROWS.forEach((points) => drawConnector(ctx, points));
  },
};

export { doc };
