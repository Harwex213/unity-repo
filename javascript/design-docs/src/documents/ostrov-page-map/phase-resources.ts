import { PALETTE } from "./palette";
import {
  FLASK_PANEL,
  ISLAND_HEX_SIZE,
  MAIN_RESOURCES,
  MINI_MAP,
  PLAYERS,
  WHEEL_PANEL,
  bottomOf,
  bottomPanel,
  centerXOf,
  drawFlask,
  drawFortress,
  drawHouse,
  drawIslandField,
  drawMiniGlobe,
  drawPhaseWheel,
  drawPlate,
  drawPlayersPanel,
  drawResourcePanels,
  drawTag,
  drawTurnBox,
  islandHex,
  middleYOf,
  plateOf,
  rightOf,
  turnBox,
} from "./hud";
import { bottomCallout, leftCallout, rightCallout, topCallout } from "./scheme";
import { STYLE, TEXT, circle, hatch, hex, paint, rect, text } from "./wire";
import type { TPhaseScheme } from "./scheme";
import type { TPoint, TRect } from "../../core/types";

// Phase 2, resources: docs/spec/ui-phase-2-resources.html.
// All coordinates are screen units.

const TURN = turnBox(673, 255);

const FORTRESS_HEX = islandHex(4, 2);
// The house that pays out: its payout plates stand above it, and the payout flies from it.
const PAYING_HOUSE_HEX = islandHex(1, 2);
// The village waits for the player to press "Запустить".
const VILLAGE_HEX = islandHex(3, 3);

// Small plates above a building, by their distance above the center of the hex.
const TAG_LIFT = 62;
const FORECAST_PLATE_WIDTH = 124;
const UPPER_TAG_LIFT = 92;
// The tag of the fortress stays clear of the plate of the village next to it.
const TAG_WIDTH = 52;
const UPPER_TAG_WIDTH = 88;

const LAUNCH: TRect = { x: VILLAGE_HEX.x - 66, y: VILLAGE_HEX.y - 64, width: 132, height: 26 };
// The food forecast over the food icon.
const FOOD_FORECAST: TRect = { x: 70, y: 783, width: 42, height: 18 };
// The tools and the building panel slide down for the phase.
const STOWED = bottomPanel(439, 759);
// The window of the dice faces opens in the middle of the screen.
const FACETS: TRect = { x: 417, y: 190, width: 766, height: 532 };

type TFlight = { from: TPoint; control: TPoint; to: TPoint };

// The payout flies on an arc: resources to their icons, infection to the flask.
const FLIGHTS: TFlight[] = [
  { from: { x: PAYING_HOUSE_HEX.x, y: 205 }, control: { x: 300, y: 300 }, to: { x: 58, y: 800 } },
  { from: { x: PAYING_HOUSE_HEX.x + 20, y: 225 }, control: { x: 1200, y: 100 }, to: { x: 1525, y: 300 } },
];
// A particle on each arc, as a share of the way.
const PARTICLE_PROGRESS = [0.7, 0.5];

const pointOnFlight = ({ from, control, to }: TFlight, progress: number): TPoint => {
  const rest = 1 - progress;
  return {
    x: rest * rest * from.x + 2 * progress * rest * control.x + progress * progress * to.x,
    y: rest * rest * from.y + 2 * progress * rest * control.y + progress * progress * to.y,
  };
};

const PARTICLES = FLIGHTS.map((flight, index) => pointOnFlight(flight, PARTICLE_PROGRESS[index] ?? 0.5));

const drawScreen = (ctx: CanvasRenderingContext2D) => {
  drawIslandField(ctx);
  hex(ctx, FORTRESS_HEX, ISLAND_HEX_SIZE, STYLE.hexFilled);
  drawFortress(ctx, FORTRESS_HEX);
  drawPlate(ctx, FORTRESS_HEX, "3 0% +12%", FORECAST_PLATE_WIDTH);
  drawTag(ctx, FORTRESS_HEX, "+3", TAG_WIDTH, TAG_LIFT);

  drawHouse(ctx, PAYING_HOUSE_HEX);
  drawPlate(ctx, PAYING_HOUSE_HEX, "1/1 0% +20%", FORECAST_PLATE_WIDTH);
  drawTag(ctx, PAYING_HOUSE_HEX, "+1", TAG_WIDTH, TAG_LIFT);
  drawTag(ctx, PAYING_HOUSE_HEX, "+20%", UPPER_TAG_WIDTH, UPPER_TAG_LIFT);

  drawHouse(ctx, VILLAGE_HEX);
  drawPlate(ctx, VILLAGE_HEX, "1/1 0%");
  rect(ctx, LAUNCH, 5, STYLE.strong);
  text(ctx, "▶ Запустить", centerXOf(LAUNCH), LAUNCH.y + 19, TEXT.small, "center");

  drawPlayersPanel(ctx, [1]);
  drawTurnBox(ctx, TURN, "Фаза сбора налогов");
  drawFlask(ctx);
  drawResourcePanels(ctx);
  rect(ctx, FOOD_FORECAST, 8, STYLE.warning);
  text(ctx, "−8", centerXOf(FOOD_FORECAST), FOOD_FORECAST.y + 14, { ...TEXT.small, weight: 700, color: PALETTE.always }, "center");

  hatch(ctx, STOWED, () => ctx.roundRect(STOWED.x, STOWED.y, STOWED.width, STOWED.height, 8));
  rect(ctx, STOWED, 8, STYLE.stowed);
  text(ctx, "инструменты и здания уехали вниз", centerXOf(STOWED), STOWED.y + 63, TEXT.ghost, "center");

  drawMiniGlobe(ctx, true);
  drawPhaseWheel(ctx, "Собрать", "Налоги");

  FLIGHTS.forEach(({ from, control, to }) =>
    paint(ctx, STYLE.flight, () => {
      ctx.moveTo(from.x, from.y);
      ctx.quadraticCurveTo(control.x, control.y, to.x, to.y);
    }),
  );
  PARTICLES.forEach((particle) => circle(ctx, particle, 9, { fill: PALETTE.onAction }));

  rect(ctx, FACETS, 10, STYLE.overlayOutline);
  text(ctx, "Окно граней: по центру экрана", centerXOf(FACETS), FACETS.y + 520, TEXT.overlayBold, "center");
};

const FORTRESS_PLATE = plateOf(FORTRESS_HEX, FORECAST_PLATE_WIDTH);
const FLASK_CALLOUT_Y = 400;
// The facets callout runs between the flask and the wheel.
const FACETS_CALLOUT_Y = 570;
// The infection forecast badge stands lower than its tip, under the particle callout.
const FORECAST_BADGE_Y = 615.6;

const resourcesPhase: TPhaseScheme = {
  summary:
    "Здания бросают кубики и приносят ресурсы и заражение. Игрок видит результат заранее, " +
    "меняет грани за власть и запускает деревни. В конце фазы крутится слот заражения.",
  drawScreen,
  callouts: [
    leftCallout(1, "Панель игроков", { x: PLAYERS.x, y: middleYOf(PLAYERS) }),
    topCallout(2, "Ход и фаза", { x: rightOf(TURN) - 28, y: TURN.y }),
    leftCallout(3, "Плашки выплаты", {
      x: PAYING_HOUSE_HEX.x - TAG_WIDTH / 2,
      y: PAYING_HOUSE_HEX.y - TAG_LIFT + 12,
    }),
    leftCallout(4, "Запустить деревню", { x: LAUNCH.x, y: middleYOf(LAUNCH) }),
    leftCallout(5, "Прогноз заражения", { x: FORTRESS_PLATE.x, y: middleYOf(FORTRESS_PLATE) }, {
      badgeAt: FORECAST_BADGE_Y,
    }),
    rightCallout(6, "Заражение острова", { x: rightOf(FLASK_PANEL), y: FLASK_CALLOUT_Y }),
    bottomCallout(7, "Прогноз еды", { x: centerXOf(FOOD_FORECAST), y: bottomOf(MAIN_RESOURCES) }),
    bottomCallout(8, "Панели спрятаны", { x: centerXOf(STOWED), y: bottomOf(STOWED) }),
    bottomCallout(9, "Мини-глобус выключен", { x: centerXOf(MINI_MAP), y: bottomOf(MINI_MAP) }),
    rightCallout(10, "Колесо фаз", { x: rightOf(WHEEL_PANEL), y: middleYOf(WHEEL_PANEL) }),
    leftCallout(11, "Частицы выплаты", PARTICLES[0] ?? { x: 0, y: 0 }, { onAction: true }),
    rightCallout(12, "Окно граней", { x: rightOf(FACETS), y: FACETS_CALLOUT_Y }, { onAction: true }),
  ],
};

export { resourcesPhase };
