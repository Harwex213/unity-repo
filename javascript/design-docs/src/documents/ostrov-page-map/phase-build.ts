import {
  FLASK_PANEL,
  ISLAND_HEX_SIZE,
  MAIN_RESOURCES,
  MINI_MAP,
  PLAYERS,
  SPECIAL_RESOURCES,
  WHEEL_PANEL,
  bottomOf,
  bottomPanel,
  centerXOf,
  drawBuildingCard,
  drawFlask,
  drawFortress,
  drawHouse,
  drawIslandField,
  drawMiniGlobe,
  drawPhaseWheel,
  drawPlate,
  drawPlayersPanel,
  drawResourcePanels,
  drawTurnBox,
  islandHex,
  middleYOf,
  plateOf,
  rightOf,
  turnBox,
} from "./hud";
import { bottomCallout, leftCallout, rightCallout, topCallout } from "./scheme";
import { STYLE, TEXT, hex, line, rect, text } from "./wire";
import type { TPhaseScheme } from "./scheme";
import type { TRect } from "../../core/types";

// Phase 1, build: docs/spec/ui-phase-1-build.html.
// All coordinates are screen units.

const TURN = turnBox(673, 255);

// The fortress stands on the selected hex. Two houses have full staff.
const FORTRESS_HEX = islandHex(4, 2);
const HOUSE_HEXES = [islandHex(1, 2), islandHex(3, 3)];
// The ghost of the selected building follows the cursor.
const GHOST_HEX = islandHex(2, 4);

const TOOLS = bottomPanel(439, 60);
const BUILDINGS = bottomPanel(506, 692);
const CARD = { firstX: 515, step: 85, top: 785, width: 80, height: 93 };
// Seven buildings and one empty slot. The second card is selected.
const CARD_COUNT = 8;
const SELECTED_CARD = 1;

const cardAt = (index: number): TRect => ({
  x: CARD.firstX + index * CARD.step,
  y: CARD.top,
  width: CARD.width,
  height: CARD.height,
});

// The hex card covers the flask while it is open.
const HEX_CARD: TRect = { x: 1246, y: 13, width: 341, height: 590 };
const HEX_CARD_ROWS = ["шапка и прочность", "кубик", "что построить", "рабочие", "армия", "очистка почвы"];
const HEX_CARD_ROW_STEP = 82;

const HEX_TIP: TRect = { x: GHOST_HEX.x - 125, y: 144, width: 250, height: 100 };

const drawScreen = (ctx: CanvasRenderingContext2D) => {
  drawIslandField(ctx);
  hex(ctx, FORTRESS_HEX, ISLAND_HEX_SIZE, STYLE.hexSelected);
  drawFortress(ctx, FORTRESS_HEX);
  drawPlate(ctx, FORTRESS_HEX, "3 0%");
  HOUSE_HEXES.forEach((center) => {
    drawHouse(ctx, center);
    drawPlate(ctx, center, "1/1 0%");
  });
  hex(ctx, GHOST_HEX, ISLAND_HEX_SIZE, STYLE.hexDashed);
  drawHouse(ctx, GHOST_HEX, 0.55);

  drawPlayersPanel(ctx);
  drawTurnBox(ctx, TURN, "Фаза строительства");
  drawFlask(ctx);
  drawResourcePanels(ctx);

  rect(ctx, TOOLS, 8, STYLE.panel);
  rect(ctx, { x: 449, y: 789, width: 40, height: 40 }, 6, STYLE.panel);
  rect(ctx, { x: 449, y: 835, width: 40, height: 40 }, 6, STYLE.panel);

  rect(ctx, BUILDINGS, 8, STYLE.panel);
  for (let index = 0; index < CARD_COUNT; index++) {
    const card = cardAt(index);
    if (index === CARD_COUNT - 1) {
      rect(ctx, card, 6, STYLE.dashed);
    } else if (index === SELECTED_CARD) {
      rect(ctx, card, 6, STYLE.strong);
    } else {
      rect(ctx, card, 6, STYLE.panel);
      drawBuildingCard(ctx, card.x);
    }
  }

  drawMiniGlobe(ctx);
  drawPhaseWheel(ctx, "Готов", "Строительство");

  rect(ctx, HEX_CARD, 10, STYLE.overlayOutline);
  text(ctx, "Карточка гекса", HEX_CARD.x + 16, HEX_CARD.y + 39, TEXT.overlayBold);
  HEX_CARD_ROWS.forEach((row, index) => {
    const y = HEX_CARD.y + 67 + index * HEX_CARD_ROW_STEP;
    line(ctx, { x: HEX_CARD.x + 16, y }, { x: HEX_CARD.x + 204, y }, STYLE.overlayLine);
    text(ctx, row, HEX_CARD.x + 16, y + 34, TEXT.overlay);
  });

  rect(ctx, HEX_TIP, 8, STYLE.overlayBox);
  text(ctx, "подсказка гекса", centerXOf(HEX_TIP), HEX_TIP.y + 40, TEXT.overlayBold, "center");
  text(ctx, "грани, цена, запрет", centerXOf(HEX_TIP), HEX_TIP.y + 74, TEXT.overlay, "center");
};

const ISLAND_LEFT_CORNER = { x: islandHex(1, 0).x - (ISLAND_HEX_SIZE * Math.sqrt(3)) / 2, y: islandHex(1, 0).y };
// The middle of the lower left side of the ghost hex.
const GHOST_SIDE = {
  x: GHOST_HEX.x - (ISLAND_HEX_SIZE * Math.sqrt(3)) / 4,
  y: GHOST_HEX.y + (ISLAND_HEX_SIZE * 3) / 4,
};
const FORTRESS_PLATE = plateOf(FORTRESS_HEX);
// The flask callout stands low, clear of the hex card callout.
const FLASK_CALLOUT_Y = 400;
const HEX_CARD_CALLOUT_Y = 120;

const buildPhase: TPhaseScheme = {
  summary: "Игрок развивает остров: строит здания, распределяет людей на нужды острова и убирает заражение.",
  drawScreen,
  callouts: [
    leftCallout(1, "Панель игроков", { x: PLAYERS.x, y: middleYOf(PLAYERS) }),
    topCallout(2, "Ход и фаза", { x: TURN.x + 27, y: TURN.y }),
    leftCallout(3, "Поле острова", ISLAND_LEFT_CORNER),
    leftCallout(4, "Призрак здания", GHOST_SIDE),
    leftCallout(5, "Плашка гекса", { x: FORTRESS_PLATE.x, y: middleYOf(FORTRESS_PLATE) }),
    rightCallout(6, "Заражение острова", { x: rightOf(FLASK_PANEL), y: FLASK_CALLOUT_Y }),
    bottomCallout(7, "Основные ресурсы", { x: MAIN_RESOURCES.x + 32, y: bottomOf(MAIN_RESOURCES) }),
    bottomCallout(8, "Особые ресурсы", { x: centerXOf(SPECIAL_RESOURCES), y: bottomOf(SPECIAL_RESOURCES) }, {
      secondLine: true,
    }),
    bottomCallout(9, "Инструменты", { x: centerXOf(TOOLS), y: bottomOf(TOOLS) }),
    bottomCallout(10, "Панель зданий", { x: centerXOf(BUILDINGS), y: bottomOf(BUILDINGS) }),
    bottomCallout(11, "Мини-глобус", { x: centerXOf(MINI_MAP), y: bottomOf(MINI_MAP) }),
    rightCallout(12, "Колесо фаз", { x: rightOf(WHEEL_PANEL), y: middleYOf(WHEEL_PANEL) }),
    rightCallout(13, "Карточка гекса", { x: rightOf(HEX_CARD), y: HEX_CARD_CALLOUT_Y }, { onAction: true }),
    topCallout(14, "Подсказка гекса", { x: centerXOf(HEX_TIP), y: HEX_TIP.y }, { onAction: true }),
  ],
};

export { buildPhase };
