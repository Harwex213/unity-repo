import {
  drawBox,
  drawConnector,
  drawSheet,
  drawSwatch,
  drawText,
  drawTextCenteredAt,
  drawTextInRect,
} from "../../kit/shapes";
import { createImageAsset, drawImageContain } from "../../kit/image";
import { PALETTE } from "./palette";
import bossArt from "./assets/boss-helios.png";
import converterTechArt from "./assets/converter-tech.png";
import converterArt from "./assets/converter.png";
import flyingIslandArt from "./assets/flying-island.webp";
import { battleSkillTip, drawBattleIslands, drawSkillsLegend } from "./battle";
import {
  CONTAMINATION_ART_ASSETS,
  SLOT_WINDOW_SIZE,
  drawDashedCurve,
  drawHexDecay,
  drawIslandMeter,
  drawSlotWindow,
} from "./contamination";
import { drawFlowArrow } from "./flow-arrow";
import { cellPoint, drawBlockedSwatch, drawEnemyIslands, drawGlobe, drawUnknown, globeSphere } from "./globe";
import { ISLAND_ART_ASSETS, ISLAND_TARGETS, drawHexIsland, islandPoint, taxPlateTips } from "./island";
import { cellMiddle, drawModuleWheel } from "./module-wheel";
import { drawScoutScreen } from "./scout-screen";
import { SHIFT_WINDOW_HEIGHT, drawShiftWindow } from "./shift";
import { drawTechTree } from "./tech-tree";
import type { TDocument, TPoint, TRect } from "../../core/types";
import type { TAxial } from "../../kit/hex";
import type { TModuleWheel } from "./module-wheel";

// The core cycle of Ostrov: four phases in a loop and the path to the goal of the game.
// All coordinates are world units. The texts come from the wireframe.

const SHEET: TRect = { x: 0, y: 0, width: 2680, height: 1880 };

const rightOf = (rect: TRect) => rect.x + rect.width;
const bottomOf = (rect: TRect) => rect.y + rect.height;
const middleYOf = (rect: TRect) => rect.y + rect.height / 2;
const centerXOf = (rect: TRect) => rect.x + rect.width / 2;

// The frame around the four phases.
// Its top stands 40 units above the phases, under the stack of the build actions.
const CYCLE: TRect = { x: 400, y: 640, width: 1460, height: 980 };

const PHASE_SIZE = { width: 520, height: 330 };
const BUILD: TRect = { x: 540, y: 680, ...PHASE_SIZE };
const TAX: TRect = { x: 1220, y: 680, ...PHASE_SIZE };
const SCOUT: TRect = { x: 1220, y: 1150, ...PHASE_SIZE };
const CLEAR: TRect = { x: 540, y: 1150, ...PHASE_SIZE };

// The drawing area inside a phase box, under the caption.
const phaseArea = (rect: TRect): TRect => ({
  x: rect.x + 16,
  y: rect.y + 34,
  width: rect.width - 32,
  height: rect.height - 48,
});

// Each phase has a section with its own background: the phase, its labels and its pictures.
// Dashed lines split the sections. The splits run between the phase boxes.
const ZONES = { left: 20, top: 280, right: 1900, bottom: 1850 };
const SPLIT_X = (rightOf(BUILD) + TAX.x) / 2;
const SPLIT_Y = (bottomOf(BUILD) + CLEAR.y) / 2;

// Arrow tips stop this far from the box they point at.
const GAP = 8;

// ---------- Build ----------

// The arrows end on hexes of the build island.
const BUILD_AREA = phaseArea(BUILD);
// An arrow from the left stops near the left edge of the hex, so its head does not cover the building.
const TOXIC_TIP = islandPoint(BUILD_AREA, ISLAND_TARGETS.toxic, { x: -0.74, y: 0 });
// The sacrifice arrow enters low, under the start of the red dashed link to the purified hex.
const SACRIFICE_TIP = islandPoint(BUILD_AREA, ISLAND_TARGETS.sacrifice, { x: -0.6, y: 0.25 });
// The new building stands in the top row, so the arrow from above crosses no other hex.
const NEW_BUILDING_TIP = islandPoint(BUILD_AREA, ISLAND_TARGETS.newBuilding, { x: 0, y: -0.55 });
// The recruit and workers arrows come from above and cross hexes of the upper rows.
// Each arrow runs a quarter of a hex width left of the target center,
// so it does not run along the vertical edges of the rows above.
const QUARTER_HEX_WIDTH = Math.sqrt(3) / 4;
const RECRUIT_TIP = islandPoint(BUILD_AREA, ISLAND_TARGETS.stronghold, { x: -QUARTER_HEX_WIDTH, y: -0.6 });
const WORKERS_TIP = islandPoint(BUILD_AREA, ISLAND_TARGETS.staffedBuilding, { x: -QUARTER_HEX_WIDTH, y: -0.66 });
// The damaged building stands in the top row, so the arrow from above crosses no other hex.
const REPAIR_TIP = islandPoint(BUILD_AREA, ISLAND_TARGETS.damagedBuilding, { x: 0, y: -0.76 });

// The module wheel stands right above its building, which is in the top row of the island.
// The arrow from the building to the wheel goes straight up and crosses no hex.
// The arrow starts at the top of the building art.
const MODULE_ARROW_START = islandPoint(BUILD_AREA, ISLAND_TARGETS.moduleBuilding, { x: 0, y: -0.6 });
const MODULE_WHEEL: TModuleWheel = { center: { x: MODULE_ARROW_START.x, y: 380 }, radius: 70 };
// The modules label stands left of the wheel. Its arrow ends in the free top left cell.
const MODULE_SLOT = cellMiddle(MODULE_WHEEL, 0);
const MODULE_LABEL_X = MODULE_WHEEL.center.x - MODULE_WHEEL.radius - 40;
const MODULE_LABEL =
  "Ставить в здания дополнительные\nмодули за 🪨 камень 🪵 дерево\n⚙️ механизмы или 🖥️ микропроцессоры";

// The tech tree stands right of the module wheel, above the cycle frame, with its purple action above it.
// The arrow leaves the science building through its lower right edge, where no other arrow enters,
// goes right under the electricity links and then up along the right side of the build island.
const TECH_TREE: TRect = { x: 975, y: 442, width: 150, height: 150 };
const TECH_TREE_TEXT = "Изучать новые\nтехнологии за 📖\nнауку, чтобы\nоткрывать новые\nздания, модули\nи юниты";
const TECH_TREE_TEXT_TOP = 300;
const TECH_ARROW_START = islandPoint(BUILD_AREA, ISLAND_TARGETS.science, { x: 0.49, y: 0.85 });
const TECH_ARROW_X = rightOf(BUILD_AREA) + 2;

// ---------- Tax ----------

const TAX_AREA = phaseArea(TAX);
// The tax island moves left and leaves a strip on the right for the contamination meter of the island.
const TAX_METER_STRIP = 110;
const taxIslandArea = (area: TRect): TRect => ({ ...area, width: area.width - TAX_METER_STRIP });
const TAX_ISLAND_AREA = taxIslandArea(TAX_AREA);
const TAX_TIPS = taxPlateTips(TAX_ISLAND_AREA, 4);
// The village stands in the top row, so the arrow from above crosses no other hex.
const VILLAGE_TIP = islandPoint(TAX_ISLAND_AREA, ISLAND_TARGETS.village, { x: 0, y: -0.76 });

// The work shift of a building: two mini windows above the tax phase.
// The random shift stands right above the payout label.
// The shift that power forces stands above the face plate, and its arrow leads to the plate.
const shiftWindow = (centerX: number, bottom: number, width: number): TRect => ({
  x: centerX - width / 2,
  y: bottom - SHIFT_WINDOW_HEIGHT,
  width,
  height: SHIFT_WINDOW_HEIGHT,
});
const RANDOM_SHIFT_WINDOW = shiftWindow(TAX_TIPS.resource.x, 500, 124);
// The forced window is wider for its title. It ends 8 units above the label of the population growth,
// and stands a bit left of the face plate, so it keeps a gap to the random window.
const FORCED_SHIFT_WINDOW = shiftWindow(TAX_TIPS.face.x - 4, 490, 146);

// The meter of the whole island: a flask at the right side of the tax phase, its title above it.
const METER_BODY: TRect = { x: rightOf(TAX_AREA) - 26, y: 808, width: 16, height: 140 };
const METER_TITLE = "☣️ общее\nзаражение\nострова";
// A dashed link from the payout building to the side of the flask: the waste of the building
// also goes into the meter of the whole island. It bends over the water right of the island.
const WASTE_LINK_START = islandPoint(TAX_ISLAND_AREA, ISLAND_TARGETS.payoutBuilding, { x: 1.02, y: 0.25 });
const WASTE_LINK_END: TPoint = { x: METER_BODY.x - 8, y: METER_BODY.y + METER_BODY.height / 2 };
const WASTE_CAPTION = "отходы здания идут\nв ☣️ заражение\nвсего острова, а не\nтолько его гекса";
// The slot spins when the tax phase ends. Its mini window stands right of the arrow to the scout phase,
// under the meter, and a dashed link from the meter leads to it.
const SLOT_WINDOW_TOP_LEFT: TPoint = { x: rightOf(TAX_AREA) - SLOT_WINDOW_SIZE.width, y: bottomOf(TAX) + 12 };
// The decay of the own hex stands under the label of the toxicity, above its arrow.
const DECAY_CENTER: TPoint = { x: 1760, y: 556 };
const DECAY_CAPTION = "☣️ заражение постепенно ухудшает гекс\nи делает его непригодным к жизни навсегда";
const DECAY_CAPTION_TOP = 588;
// The toxicity arrow comes down right of the tax phase and enters the island from the right.
const TOXICITY_ARROW_X = rightOf(TAX) + 20;

// ---------- Scout ----------

// The scout arrows end on hexes of the globe:
// exploring leads to an unknown hex, movement leads to a neighbor hex with enemy islands,
// no movement points at the player's own hex.
const SCOUT_AREA = phaseArea(SCOUT);
const SCOUT_SPHERE = globeSphere(SCOUT_AREA);
const HOME_CELL: TAxial = { q: 0, r: 0 };
const MOVE_CELL: TAxial = { q: -1, r: 1 };
// The unknown hex is at the lower left edge of the globe. No hex lies under it,
// so the arrow from below crosses only the outline of the globe.
const EXPLORE_CELL: TAxial = { q: -3, r: 2 };
const GLOBE_MARKS = {
  bold: [MOVE_CELL],
  enemies: [MOVE_CELL, { q: 2, r: -1 }, { q: -1, r: -1 }],
  blocked: [
    { q: 0, r: -1 },
    { q: -2, r: 1 },
    { q: 1, r: 1 },
  ],
  unknown: [EXPLORE_CELL],
};
const EXPLORE_TIP = cellPoint(SCOUT_SPHERE, EXPLORE_CELL, { x: 0, y: 0.5 });
// The movement arrow comes from below, a quarter of a hex width left of the cell center,
// and stops under the enemy islands of the cell.
const MOVE_TIP = cellPoint(SCOUT_SPHERE, MOVE_CELL, { x: -QUARTER_HEX_WIDTH, y: 0.62 });
// The no-movement arrow comes from the right along the row of the home cell
// and stops to the right of the tower.
const HOME_TIP = cellPoint(SCOUT_SPHERE, HOME_CELL, { x: 0.5, y: 0 });
// The vertical part of the no-movement arrow, between the globe and the right side of the phase.
const HOME_ARROW_X = rightOf(SCOUT) - 60;

// ---------- Clear ----------

// The skills arrow ends under the middle of the skills bar.
const SKILL_TIP = battleSkillTip(phaseArea(CLEAR), 4);
// The top left corner of the skills legend.
const SKILLS_LEGEND: TPoint = { x: 40, y: 1430 };

// The phase names come from ostrov-prototype (src/core/phases.ts).
const PHASES: {
  rect: TRect;
  caption: string;
  zoneFill: string;
  draw: (ctx: CanvasRenderingContext2D, area: TRect) => void;
}[] = [
  {
    rect: BUILD,
    caption: "Строительство",
    zoneFill: PALETTE.zoneBuild,
    draw: (ctx, area) => drawHexIsland(ctx, area, { phase: "build" }),
  },
  {
    rect: TAX,
    caption: "Налоги",
    zoneFill: PALETTE.zoneTax,
    draw: (ctx, area) => {
      drawHexIsland(ctx, taxIslandArea(area), { phase: "tax" });
      drawTaxMeter(ctx);
    },
  },
  {
    rect: SCOUT,
    caption: "Разведка",
    zoneFill: PALETTE.zoneScout,
    draw: (ctx, area) => {
      drawGlobe(ctx, area, GLOBE_MARKS);
      drawScoutLegend(ctx, area);
    },
  },
  { rect: CLEAR, caption: "Зачистка", zoneFill: PALETTE.zoneBattle, draw: drawBattleIslands },
];

// ---------- Goal ----------

const GOAL_COLUMN_X = 1980;
const GOAL_WIDTH = 240;
const GOAL: TRect = { x: GOAL_COLUMN_X, y: 680, width: GOAL_WIDTH, height: 150 };
const GOAL_NOTE: TRect = { x: 2270, y: 690, width: 370, height: 130 };

// The art comes from ostrov-prototype (src/assets).
// The goal of the game is the converter building: hex-buildings/converter.png.
const GOAL_ART = createImageAsset(converterArt);
// The technology "Центральная конверсия" builds the converter. Its icon is icons/converter.png.
const TECH_ART = createImageAsset(converterTechArt);
// The boss is the island-city ruled by an AI. Its emblem is factions/large/helios.png.
const BOSS_ART = createImageAsset(bossArt);
const ART_PADDING = 6;

const STEP_PADDING = 10;
const STEP_PICTURE_HEIGHTS = { icon: 96, screen: 160 };

type TGoalStep = {
  rect: TRect;
  pictureHeight: number;
  drawPicture: (ctx: CanvasRenderingContext2D, area: TRect) => void;
  text: string;
  detail?: string;
};

// The steps to the goal, from the top down. Each step leads up to the one above it.
const GOAL_STEPS: TGoalStep[] = [
  {
    rect: { x: GOAL_COLUMN_X, y: 900, width: GOAL_WIDTH, height: 180 },
    pictureHeight: STEP_PICTURE_HEIGHTS.icon,
    drawPicture: (ctx, area) => drawImageContain(ctx, TECH_ART, area),
    text: "Нужная технология",
    detail: "«Центральная конверсия»",
  },
  {
    rect: { x: GOAL_COLUMN_X, y: 1150, width: GOAL_WIDTH, height: 180 },
    pictureHeight: STEP_PICTURE_HEIGHTS.icon,
    drawPicture: (ctx, area) => drawImageContain(ctx, BOSS_ART, area),
    text: "Победа над боссом",
    detail: "Остров ИИ «Иерихион»",
  },
  {
    rect: { x: GOAL_COLUMN_X, y: 1400, width: GOAL_WIDTH, height: 236 },
    pictureHeight: STEP_PICTURE_HEIGHTS.screen,
    drawPicture: (ctx, area) => {
      drawScoutScreen(ctx, area, BOSS_ART);
      drawBox(ctx, area, { width: 1 });
    },
    text: "Разведка необходимого\nгекса",
  },
];

// ---------- Intro ----------

// The intro stands in the top right corner, right of the resources and above the goal column.
// A box with the picture of the flying island at the top and the text under it.
const INTRO: TRect = { x: GOAL_COLUMN_X, y: 40, width: rightOf(GOAL_NOTE) - GOAL_COLUMN_X, height: 540 };
const INTRO_PADDING = 10;
const INTRO_PICTURE_HEIGHT = 420;
// The art comes from ostrov-prototypes (prototypes/main-menu/src/assets/island.webp).
const INTRO_ART = createImageAsset(flyingIslandArt);
const INTRO_TEXT =
  "Центральная сущность, которой управляет игрок, -\nлетающий остров, который состоит из гексов,\nкаждый из которых представляет собой один\nиз уникальных биомов";

const ART_ASSETS = [INTRO_ART, GOAL_ART, TECH_ART, BOSS_ART, ...ISLAND_ART_ASSETS, ...CONTAMINATION_ART_ASSETS];

// ---------- Labels ----------

// Labels to the left of the cycle are right-aligned on this x.
const LEFT_LABEL_X = 370;

type TLabel = {
  text: string;
  color: string;
  at: TPoint;
  arrow: TPoint[];
  haloArea?: TRect;
};

// Texts above the phases. `at` is the center of the text.
// An arrow starts under its text: one-line texts end 11 units under `at.y`, two-line texts 22 units.
// Player actions are purple.
// In each section the labels follow the order of their targets from left to right.
// The more left a label stands, the lower its arrow turns, so the arrows nest and do not cross.
const TOP_LABELS: TLabel[] = [
  {
    text: "Запускать рост\n🧍 жителей",
    color: PALETTE.action,
    // The tax island stands left in its phase, so the label stands left too, clear of the face label.
    at: { x: 1212, y: 520 },
    arrow: [{ x: 1212, y: 550 }, { x: 1212, y: 600 }, { x: VILLAGE_TIP.x, y: 600 }, VILLAGE_TIP],
  },
  {
    text: "Даёт ресурсы",
    color: PALETTE.positive,
    at: { x: TAX_TIPS.resource.x, y: 520 },
    arrow: [{ x: TAX_TIPS.resource.x, y: 538 }, TAX_TIPS.resource],
  },
  {
    text: "Порождает ☣️ заражение",
    color: PALETTE.negative,
    at: { x: TOXICITY_ARROW_X, y: 520 },
    // The arrow starts under the decay of the hex and its caption.
    arrow: [
      { x: TOXICITY_ARROW_X, y: DECAY_CAPTION_TOP + 38 },
      { x: TOXICITY_ARROW_X, y: TAX_TIPS.toxicity.y },
      TAX_TIPS.toxicity,
    ],
  },
];

// The player actions on the build island. The labels stand in a stack over the top left corner
// of the build phase, right-aligned on one x. Each arrow goes right and then down to its hex.
// The upper a label stands, the more right its target is, so the arrows nest and do not cross.
const BUILD_ACTION_X = BUILD.x - 40;
// A long text takes two lines. The blocks stand 12 units apart; the stack ends above the cycle frame.
// The top label's arrow passes under the module wheel.
const BUILD_ACTIONS: { text: string; y: number; tip: TPoint; overIsland: boolean }[] = [
  {
    text: "Распределять 🧍 жителей в рабочее здание\nчтобы оно приносило ресурсы",
    y: 446,
    tip: WORKERS_TIP,
    overIsland: true,
  },
  { text: "Нанимать войска переводя 🧍 жителей в ⚔️ юнитов", y: 492, tip: RECRUIT_TIP, overIsland: true },
  {
    text: "Строить новые здания за 🪨 камень 🪵 дерево\n⚙️ механизмы или 🖥️ микропроцессоры",
    y: 537,
    tip: NEW_BUILDING_TIP,
    overIsland: false,
  },
  {
    text: "Чинить здания за 🪨 камень 🪵 дерево\n⚙️ механизмы или 🖥️ микропроцессоры",
    y: 593,
    tip: REPAIR_TIP,
    overIsland: false,
  },
];

// `action` is a purple player action above the text. The text is the result of the action.
type TLeftLabel = Omit<TLabel, "arrow"> & { arrows: TPoint[][]; action?: string };

// Texts to the left of the cycle. `at.y` is the middle of the text block.
const LEFT_LABELS: TLeftLabel[] = [
  {
    text: "☣️ Заражение ухудшает\nпроизводительность всего гекса",
    color: PALETTE.negative,
    at: { x: LEFT_LABEL_X, y: TOXIC_TIP.y },
    arrows: [[{ x: LEFT_LABEL_X + 10, y: TOXIC_TIP.y }, TOXIC_TIP]],
  },
  {
    // The arrow leads to the sacrificed hex. The island shows the rest:
    // a red dashed link from that hex to the hex whose toxicity drops.
    text: "Жертвовать землей для\nуменьшения ☣️ заражения",
    color: PALETTE.action,
    at: { x: LEFT_LABEL_X, y: SACRIFICE_TIP.y },
    arrows: [[{ x: LEFT_LABEL_X + 10, y: SACRIFICE_TIP.y }, SACRIFICE_TIP]],
  },
  {
    action: "Вести остров, чтобы стыковать\nострова или отступать",
    text: "Стыкуешься с врагом - твои и\nюниты противника начинают\nAuto Battle",
    color: PALETTE.ink,
    at: { x: LEFT_LABEL_X, y: middleYOf(CLEAR) },
    arrows: [[{ x: LEFT_LABEL_X + 10, y: middleYOf(CLEAR) }, { x: CLEAR.x - GAP, y: middleYOf(CLEAR) }]],
  },
];

// The top of the scout labels. It stands right under the cycle frame,
// so the long labels fit in the scout section.
const SCOUT_LABELS_Y = 1648;

// Texts under the phases. `at.y` is the top of the text.
// Each arrow starts above its text and goes into a phase.
// `action` is a purple player action above the text. The text is the result of the action.
const BOTTOM_LABELS: (TLabel & { action?: string })[] = [
  {
    // The skills label stands right under the skills bar, so its arrow is straight.
    action: "Применять активные скиллы за 💠 ману",
    text: "Активные скиллы для\nвспомогательных действий",
    color: PALETTE.ink,
    at: { x: SKILL_TIP.x, y: 1510 },
    arrow: [{ x: SKILL_TIP.x, y: 1502 }, SKILL_TIP],
  },
  {
    // The text takes two lines, so it does not reach the movement label on its right.
    text: "Исследовать новые гексы\nблагодаря зданиям",
    color: PALETTE.action,
    at: { x: 1260, y: SCOUT_LABELS_Y },
    arrow: [{ x: 1260, y: SCOUT_LABELS_Y - 8 }, { x: 1260, y: 1560 }, { x: EXPLORE_TIP.x, y: 1560 }, EXPLORE_TIP],
    haloArea: SCOUT_AREA,
  },
  {
    action: "Передвигать остров",
    // The long text wraps into narrow lines, so it stays between the labels on its left and right.
    text: "Игрок берёт риск на свой\nостров провоцируя бой за\nглавный ресурс игры -\nземлю, чтобы его остров\nмог выжить",
    color: PALETTE.positive,
    at: { x: 1480, y: SCOUT_LABELS_Y },
    arrow: [{ x: 1480, y: SCOUT_LABELS_Y - 8 }, { x: 1480, y: 1540 }, { x: MOVE_TIP.x, y: 1540 }, MOVE_TIP],
    haloArea: SCOUT_AREA,
  },
  {
    action: "Оставаться в текущем гексе",
    // The long text wraps into narrow lines, so it stays between the movement label and the right side of the section.
    text: "Отсутствие движения - мнимая\nбезопасность, ведь отходы\nострова игрока усиливают\nзаражение окружающей среды\nи спустя некоторое время люди\nзадохнутся от самих себя\nв прямом смысле",
    color: PALETTE.negative,
    at: { x: 1740, y: SCOUT_LABELS_Y },
    arrow: [
      { x: 1740, y: SCOUT_LABELS_Y - 8 },
      { x: 1740, y: 1560 },
      { x: HOME_ARROW_X, y: 1560 },
      { x: HOME_ARROW_X, y: HOME_TIP.y },
      HOME_TIP,
    ],
    haloArea: SCOUT_AREA,
  },
];

// The order of the phases: build → tax → scout → clear → build.
// The cycle is the main flow of the sheet, so its arrows are wide band arrows.
// They stop farther from the phase boxes than the label arrows, so the paper outline does not touch a box.
const FLOW_GAP = 14;
const CYCLE_ARROWS: [TPoint, TPoint][] = [
  [
    { x: rightOf(BUILD) + FLOW_GAP, y: middleYOf(BUILD) },
    { x: TAX.x - FLOW_GAP, y: middleYOf(TAX) },
  ],
  [
    { x: centerXOf(TAX), y: bottomOf(TAX) + FLOW_GAP },
    { x: centerXOf(SCOUT), y: SCOUT.y - FLOW_GAP },
  ],
  [
    { x: SCOUT.x - FLOW_GAP, y: middleYOf(SCOUT) },
    { x: rightOf(CLEAR) + FLOW_GAP, y: middleYOf(CLEAR) },
  ],
  [
    { x: centerXOf(CLEAR), y: CLEAR.y - FLOW_GAP },
    { x: centerXOf(BUILD), y: bottomOf(BUILD) + FLOW_GAP },
  ],
];

// ---------- Drawing ----------

// The legend of the globe marks, in the bottom left corner of the scout phase, clear of the globe.
const drawScoutLegend = (ctx: CanvasRenderingContext2D, area: TRect) => {
  const swatchX = area.x + 9;
  const textX = area.x + 22;
  const top = area.y + area.height - 56;
  drawEnemyIslands(ctx, { x: swatchX, y: top + 8 }, 22);
  drawText(ctx, "враги", textX, top, { size: 12 });
  drawBlockedSwatch(ctx, { x: swatchX, y: top + 28 }, 6);
  drawText(ctx, "непроходимо", textX, top + 20, { size: 12 });
  drawUnknown(ctx, { x: swatchX, y: top + 48 }, 18);
  drawText(ctx, "не исследовано", textX, top + 40, { size: 12 });
};

// The list of resources, right of the legend and above the sections: one column per group.
const RESOURCES_TOP = 60;
const RESOURCES_ITEMS_TOP = 100;
// Ten resources: the step keeps the longest column above the sections (ZONES.top).
const RESOURCES_ROW_STEP = 24;
const RESOURCE_GROUPS: { x: number; title: string; color: string; items: string[] }[] = [
  {
    x: 560,
    title: "Ресурсы:",
    color: PALETTE.ink,
    items: ["1) 🍗 еда (даёт население)", "2) 🪨 камень (даёт здания)", "3) 🪵 дерево (даёт здания)"],
  },
  {
    x: 940,
    title: "Спец ресурсы:",
    color: PALETTE.ink,
    items: [
      "4)🧍 население (даёт работу зданиям и армию)",
      "5) ⚙️ механизмы (даёт здания)",
      "6) 🖥️ микропроцессоры (даёт здания)",
      "7) 📖 наука (даёт технологии)",
      "8) 🔭 электричество (даёт разведку, работу зданиям и активные скиллы)",
      "9) 💠 мана (даёт активные скиллы)",
      // Power (власть) of ostrov-prototype: the stronghold pays it every turn,
      // and the tax phase spends it to change a rolled face.
      "10) 👑 власть (даёт смену выпавшей грани здания)",
    ],
  },
  {
    x: 1580,
    title: "Негативный ресурс:",
    color: PALETTE.negative,
    items: ["- ☣️ заражение", "- 🤖 сумасшедшее население"],
  },
];

const drawResources = (ctx: CanvasRenderingContext2D) => {
  for (const group of RESOURCE_GROUPS) {
    drawText(ctx, group.title, group.x, RESOURCES_TOP, { size: 18, weight: 600, color: group.color });
    group.items.forEach((item, index) => {
      drawText(ctx, item, group.x, RESOURCES_ITEMS_TOP + index * RESOURCES_ROW_STEP, { size: 16 });
    });
  }
};

const drawHeader = (ctx: CanvasRenderingContext2D) => {
  drawText(ctx, "Ostrov 1 - Core Cycle", 50, 50, { size: 36, weight: 500 });

  const legend = [
    { fill: PALETTE.actionFill, text: "Действия игрока" },
    { fill: PALETTE.positiveFill, text: "Положительные состояния и события игры" },
    { fill: PALETTE.negativeFill, text: "Негативные состояния и события игры" },
  ];
  legend.forEach((row, index) => {
    const top = 120 + index * 42;
    drawSwatch(ctx, { x: 50, y: top, width: 32, height: 30 }, row.fill);
    drawTextCenteredAt(ctx, row.text, 98, top + 15, { size: 15 });
  });

  drawResources(ctx);
};

// The sections behind the phases and the dashed lines between them.
const drawZones = (ctx: CanvasRenderingContext2D) => {
  const [build, tax, scout, clear] = PHASES;
  const zones: { fill: string | undefined; rect: TRect }[] = [
    {
      fill: build?.zoneFill,
      rect: { x: ZONES.left, y: ZONES.top, width: SPLIT_X - ZONES.left, height: SPLIT_Y - ZONES.top },
    },
    {
      fill: tax?.zoneFill,
      rect: { x: SPLIT_X, y: ZONES.top, width: ZONES.right - SPLIT_X, height: SPLIT_Y - ZONES.top },
    },
    {
      fill: scout?.zoneFill,
      rect: { x: SPLIT_X, y: SPLIT_Y, width: ZONES.right - SPLIT_X, height: ZONES.bottom - SPLIT_Y },
    },
    {
      fill: clear?.zoneFill,
      rect: { x: ZONES.left, y: SPLIT_Y, width: SPLIT_X - ZONES.left, height: ZONES.bottom - SPLIT_Y },
    },
  ];
  for (const zone of zones) {
    ctx.fillStyle = zone.fill ?? PALETTE.paper;
    ctx.fillRect(zone.rect.x, zone.rect.y, zone.rect.width, zone.rect.height);
  }

  ctx.strokeStyle = PALETTE.muted;
  ctx.lineWidth = 1.5;
  ctx.setLineDash([10, 8]);
  ctx.beginPath();
  ctx.moveTo(SPLIT_X, ZONES.top);
  ctx.lineTo(SPLIT_X, ZONES.bottom);
  ctx.moveTo(ZONES.left, SPLIT_Y);
  ctx.lineTo(ZONES.right, SPLIT_Y);
  ctx.stroke();
  ctx.setLineDash([]);
};

const drawModules = (ctx: CanvasRenderingContext2D) => {
  drawModuleWheel(ctx, MODULE_WHEEL);
  drawConnector(ctx, [
    MODULE_ARROW_START,
    { x: MODULE_WHEEL.center.x, y: MODULE_WHEEL.center.y + MODULE_WHEEL.radius + GAP },
  ]);
  // The cost of a module is the cost of a new building.
  drawTextCenteredAt(ctx, MODULE_LABEL, MODULE_LABEL_X, MODULE_SLOT.y, {
    color: PALETTE.action,
    align: "right",
  });
  drawConnector(ctx, [
    { x: MODULE_LABEL_X + 10, y: MODULE_SLOT.y },
    { x: MODULE_SLOT.x + 4, y: MODULE_SLOT.y },
  ]);
};

// The meter of the island inside the tax phase, the dashed link of the waste to it and the caption.
const drawTaxMeter = (ctx: CanvasRenderingContext2D) => {
  drawText(ctx, METER_TITLE, rightOf(TAX_AREA) - 2, METER_BODY.y - 60, {
    size: 11,
    lineHeight: 14,
    color: PALETTE.negative,
    align: "right",
  });
  drawIslandMeter(ctx, METER_BODY, rightOf(TAX_AREA) - 2);
  drawDashedCurve(
    ctx,
    {
      start: WASTE_LINK_START,
      control1: { x: WASTE_LINK_START.x + 110, y: WASTE_LINK_START.y },
      control2: { x: WASTE_LINK_END.x - 70, y: WASTE_LINK_END.y },
      end: WASTE_LINK_END,
    },
    PALETTE.negative,
  );
  // The caption stands under the end of the link, clear of the ticks of the flask.
  drawText(ctx, WASTE_CAPTION, METER_BODY.x - 12, WASTE_LINK_END.y + 16, {
    size: 11,
    lineHeight: 14,
    color: PALETTE.negative,
    align: "right",
  });
};

// The slot under the tax phase: a dashed link from the meter, the mini window,
// and a short line from the arrow to the scout phase, so the slot reads as a step of that transition.
const drawTaxSlot = (ctx: CanvasRenderingContext2D) => {
  const meterX = METER_BODY.x + METER_BODY.width / 2;
  drawDashedCurve(
    ctx,
    {
      start: { x: meterX, y: bottomOf(METER_BODY) + 32 },
      control1: { x: meterX, y: bottomOf(METER_BODY) + 40 },
      control2: { x: meterX, y: SLOT_WINDOW_TOP_LEFT.y - 14 },
      end: { x: meterX, y: SLOT_WINDOW_TOP_LEFT.y - 3 },
    },
    PALETTE.negative,
  );
  drawSlotWindow(ctx, SLOT_WINDOW_TOP_LEFT);
  const lineY = SLOT_WINDOW_TOP_LEFT.y + 40;
  ctx.beginPath();
  ctx.moveTo(centerXOf(TAX) + 12, lineY);
  ctx.lineTo(SLOT_WINDOW_TOP_LEFT.x, lineY);
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.5;
  ctx.stroke();
};

const drawShiftWindows = (ctx: CanvasRenderingContext2D) => {
  drawShiftWindow(ctx, RANDOM_SHIFT_WINDOW, {
    title: "Рабочая смена 🎲",
    subtitle: "случайный исход",
    titleColor: PALETTE.ink,
    mode: "random",
  });
  drawShiftWindow(ctx, FORCED_SHIFT_WINDOW, {
    title: "Заставить за 👑 власть",
    subtitle: "обычная или лучше",
    titleColor: PALETTE.action,
    mode: "forced",
  });
  drawConnector(ctx, [{ x: TAX_TIPS.face.x, y: bottomOf(FORCED_SHIFT_WINDOW) + 4 }, TAX_TIPS.face]);
};

// The decay of the own hex under the toxicity label.
const drawHexDecayNote = (ctx: CanvasRenderingContext2D) => {
  drawHexDecay(ctx, DECAY_CENTER);
  drawText(ctx, DECAY_CAPTION, DECAY_CENTER.x, DECAY_CAPTION_TOP, {
    size: 12,
    lineHeight: 16,
    color: PALETTE.negative,
    align: "center",
  });
};

// The tech tree window, its purple action above it and the arrow from the science building.
const drawTechTreeNote = (ctx: CanvasRenderingContext2D) => {
  drawText(ctx, TECH_TREE_TEXT, centerXOf(TECH_TREE), TECH_TREE_TEXT_TOP, { color: PALETTE.action, align: "center" });
  drawTechTree(ctx, TECH_TREE);
  drawConnector(ctx, [
    TECH_ARROW_START,
    { x: TECH_ARROW_X, y: TECH_ARROW_START.y },
    { x: TECH_ARROW_X, y: bottomOf(TECH_TREE) + GAP },
  ]);
};

const drawCycle = (ctx: CanvasRenderingContext2D) => {
  drawBox(ctx, CYCLE);

  for (const phase of PHASES) {
    const { rect } = phase;
    ctx.fillStyle = PALETTE.paper;
    ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
    drawBox(ctx, rect);
    drawText(ctx, phase.caption, rect.x + 12, rect.y + 8, { size: 13, color: PALETTE.muted });
    phase.draw(ctx, phaseArea(rect));
  }

  for (const [from, to] of CYCLE_ARROWS) {
    drawFlowArrow(ctx, from, to);
  }
  drawTaxSlot(ctx);
  drawHexDecayNote(ctx);
  drawShiftWindows(ctx);
  drawTechTreeNote(ctx);

  // The names of the skills in the skills bar, left of the cycle under the clear labels.
  drawText(ctx, "Активные скиллы", SKILLS_LEGEND.x, SKILLS_LEGEND.y - 26, { size: 13, color: PALETTE.muted });
  drawSkillsLegend(ctx, SKILLS_LEGEND);

  for (const action of BUILD_ACTIONS) {
    drawTextCenteredAt(ctx, action.text, BUILD_ACTION_X, action.y, { color: PALETTE.action, align: "right" });
    drawConnector(
      ctx,
      [{ x: BUILD_ACTION_X + 10, y: action.y }, { x: action.tip.x, y: action.y }, action.tip],
      { haloArea: action.overIsland ? BUILD_AREA : undefined },
    );
  }
  for (const label of TOP_LABELS) {
    drawTextCenteredAt(ctx, label.text, label.at.x, label.at.y, { color: label.color, align: "center" });
    drawConnector(ctx, label.arrow, { haloArea: label.haloArea });
  }
  for (const label of LEFT_LABELS) {
    drawTextCenteredAt(ctx, label.text, label.at.x, label.at.y, { color: label.color, align: "right" });
    if (label.action) {
      // The action block ends 6 units above the text block. A line is 22 units high.
      const textTop = label.at.y - (label.text.split("\n").length * 22) / 2;
      const actionHeight = label.action.split("\n").length * 22;
      drawText(ctx, label.action, label.at.x, textTop - 6 - actionHeight, { color: PALETTE.action, align: "right" });
    }
    for (const arrow of label.arrows) {
      drawConnector(ctx, arrow);
    }
  }
  for (const label of BOTTOM_LABELS) {
    let textTop = label.at.y;
    if (label.action) {
      drawText(ctx, label.action, label.at.x, textTop, { color: PALETTE.action, align: "center" });
      textTop += 22;
    }
    drawText(ctx, label.text, label.at.x, textTop, { color: label.color, align: "center" });
    drawConnector(ctx, label.arrow, { haloArea: label.haloArea });
  }
};

// A picture at the top of the box, the text under it, then an optional muted detail.
const drawGoalStep = (ctx: CanvasRenderingContext2D, step: TGoalStep) => {
  const { rect } = step;
  drawBox(ctx, rect);
  step.drawPicture(ctx, {
    x: rect.x + STEP_PADDING,
    y: rect.y + STEP_PADDING,
    width: rect.width - STEP_PADDING * 2,
    height: step.pictureHeight,
  });
  const textTop = rect.y + STEP_PADDING + step.pictureHeight + 8;
  drawText(ctx, step.text, centerXOf(rect), textTop, { align: "center" });
  if (step.detail) {
    const lines = step.text.split("\n").length;
    drawText(ctx, step.detail, centerXOf(rect), textTop + lines * 22 + 2, {
      size: 13,
      color: PALETTE.muted,
      align: "center",
    });
  }
};

const drawIntro = (ctx: CanvasRenderingContext2D) => {
  drawBox(ctx, INTRO);
  drawImageContain(ctx, INTRO_ART, {
    x: INTRO.x + INTRO_PADDING,
    y: INTRO.y + INTRO_PADDING,
    width: INTRO.width - INTRO_PADDING * 2,
    height: INTRO_PICTURE_HEIGHT,
  });
  const textTop = INTRO.y + INTRO_PADDING + INTRO_PICTURE_HEIGHT;
  drawTextInRect(
    ctx,
    INTRO_TEXT,
    { x: INTRO.x, y: textTop, width: INTRO.width, height: bottomOf(INTRO) - textTop },
    { size: 16 },
  );
};

const drawGoal = (ctx: CanvasRenderingContext2D) => {
  drawText(ctx, "ЦЕЛЬ ИГРЫ", centerXOf(GOAL), GOAL.y - 34, { size: 20, align: "center" });
  drawImageContain(ctx, GOAL_ART, {
    x: GOAL.x + ART_PADDING,
    y: GOAL.y + ART_PADDING,
    width: GOAL.width - ART_PADDING * 2,
    height: GOAL.height - ART_PADDING * 2,
  });
  drawBox(ctx, GOAL);

  // The cycle leads to the goal.
  const exitY = middleYOf(BUILD);
  const turnX = rightOf(CYCLE) + 60;
  drawConnector(ctx, [
    { x: rightOf(CYCLE) + GAP, y: exitY },
    { x: turnX, y: exitY },
    { x: turnX, y: middleYOf(GOAL) },
    { x: GOAL.x - GAP, y: middleYOf(GOAL) },
  ]);

  // Each step leads up to the one above it.
  let above = GOAL;
  for (const step of GOAL_STEPS) {
    drawGoalStep(ctx, step);
    drawConnector(ctx, [
      { x: centerXOf(step.rect), y: step.rect.y - GAP },
      { x: centerXOf(above), y: bottomOf(above) + GAP },
    ]);
    above = step.rect;
  }

  drawBox(ctx, GOAL_NOTE);
  drawTextInRect(
    ctx,
    "Позволяет избавиться от заражения\nраз и навсегда. Экзистенциальная\nпроблема игрока решена и он\nпобедил, остальные проиграли",
    GOAL_NOTE,
    { size: 15 },
  );
};

const doc: TDocument = {
  order: 1,
  id: "ostrov-core-cycle",
  title: "Ostrov 1 - Core Cycle",
  description: "Основной цикл и цель игры",
  bounds: SHEET,
  subscribe: (onChange) => {
    const unsubscribers = ART_ASSETS.map((asset) => asset.subscribe(onChange));
    return () => {
      for (const unsubscribe of unsubscribers) {
        unsubscribe();
      }
    };
  },
  draw: (ctx) => {
    drawSheet(ctx, SHEET);
    drawZones(ctx);
    drawHeader(ctx);
    drawIntro(ctx);
    drawCycle(ctx);
    // The module arrow starts inside the build phase, so it is drawn over the phase box.
    drawModules(ctx);
    drawGoal(ctx);
  },
};

export { doc };
