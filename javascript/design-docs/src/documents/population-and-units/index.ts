import { drawImageContain } from "../../kit/image";
import { drawBox, drawConnector, drawSheet, drawText } from "../../kit/shapes";
import { ART_ASSETS, ICON_ART, MAD_PERSON_ART } from "./art";
import { cellRect, drawHud } from "./hud";
import { BARRACKS, FARM, ISLAND_SIZE, VILLAGE, drawIsland, hexBottom, workersPlateRect } from "./island";
import { PALETTE } from "./palette";
import { drawWrappedText } from "./text";
import {
  BARRACKS_CARD_SIZE,
  BECOME_MAD_SIZE,
  FARM_CARD_SIZE,
  RESIDENTS_SECTION_SIZE,
  POPULATION_TOOLTIP_SIZE,
  SLOT_MODAL_SIZE,
  VILLAGE_SECTION_SIZE,
  drawBarracksCard,
  drawBecomeMad,
  drawFarmCard,
  drawResidentsSection,
  drawPopulationTooltip,
  drawSlotModal,
  drawVillageSection,
  slotMadLine,
} from "./widgets";
import type { TDocument, TPoint, TRect } from "../../core/types";
import type { TImageAsset } from "../../kit/image";
import type { THudState } from "./hud";

// Система населения и юнитов.
// The top of the sheet shows the population on two phase screens of the game: build and collection.
// Each property of a unit of population is a fragment around the screens, with an arrow to its place.
// The bottom of the sheet shows how a unit of population turns mad and what the mad people do.
// All coordinates are world units.

const rightOf = (rect: TRect) => rect.x + rect.width;
const bottomOf = (rect: TRect) => rect.y + rect.height;
const middleYOf = (rect: TRect) => rect.y + rect.height / 2;

// Arrow tips stop this far from the drawing they point at.
const GAP = 8;

// ---------- Phase screens ----------

// The screens stand right under the heading of the population.
const POPULATION_HEADING_Y = 120;
const SCREEN_SIZE = { width: 1060, height: 640 };
const SCREENS_Y = POPULATION_HEADING_Y + 110;
const BUILD_SCREEN: TRect = { x: 80, y: SCREENS_Y, ...SCREEN_SIZE };
const COLLECT_SCREEN: TRect = { x: rightOf(BUILD_SCREEN) + 160, y: SCREENS_Y, ...SCREEN_SIZE };
// The resources block stands in the top left corner of a screen, as in the prototype.
const HUD_OFFSET = { x: 24, y: 60 };
const ISLAND_Y = 190;

const hudTopLeft = (screen: TRect): TPoint => ({ x: screen.x + HUD_OFFSET.x, y: screen.y + HUD_OFFSET.y });

// The build screen has the island in the middle.
const BUILD_ISLAND: TPoint = {
  x: BUILD_SCREEN.x + (BUILD_SCREEN.width - ISLAND_SIZE.width) / 2,
  y: BUILD_SCREEN.y + ISLAND_Y,
};
// The collection screen has the island on the left and the toxicity slot on the right.
const COLLECT_ISLAND: TPoint = { x: COLLECT_SCREEN.x + 40, y: COLLECT_SCREEN.y + ISLAND_Y };
const SLOT_MODAL: TPoint = {
  x: rightOf(COLLECT_SCREEN) - 30 - SLOT_MODAL_SIZE.width,
  y: COLLECT_SCREEN.y + 90,
};

// The resources of the build screen: only food and population are bright.
// The amounts are the starting pool of ostrov-prototype (src/core/resources.ts).
const BUILD_HUD: THudState = {
  amounts: { food: 10, wood: 12, stone: 12, population: 6, mad: 0, hammers: 6, power: 2, scouting: 2, science: 0, mana: 4 },
  bright: ["food", "population"],
};
// The collection screen after the slot: two people went mad, and the food to eat waits next to the food.
const COLLECT_HUD: THudState = {
  amounts: { ...BUILD_HUD.amounts, population: 4, mad: 2 },
  bright: ["food", "population", "mad"],
  foodPending: "−N",
};

const SCREENS: { rect: TRect; caption: string; draw: (ctx: CanvasRenderingContext2D) => void }[] = [
  {
    rect: BUILD_SCREEN,
    caption: "Фаза строительства",
    draw: (ctx) => {
      drawHud(ctx, hudTopLeft(BUILD_SCREEN), BUILD_HUD);
      // The village is selected: its panel has the population section of its fragment.
      drawIsland(ctx, BUILD_ISLAND, { selected: VILLAGE, staffed: [FARM] });
    },
  },
  {
    rect: COLLECT_SCREEN,
    caption: "Фаза сбора ресурсов",
    draw: (ctx) => {
      drawHud(ctx, hudTopLeft(COLLECT_SCREEN), COLLECT_HUD);
      // The village is selected: its panel has the section of fragment 1.
      drawIsland(ctx, COLLECT_ISLAND, { selected: VILLAGE, staffed: [FARM] });
      drawSlotModal(ctx, SLOT_MODAL);
    },
  },
];

// ---------- Fragments ----------

// A fragment: the text of one property at the top, the wireframe under it.
const CAPTION_TOP = 18;
const CAPTION_STYLE = { size: 18, weight: 600, lineHeight: 25 };
// Two lines of caption and a gap.
const CONTENT_TOP = CAPTION_TOP + CAPTION_STYLE.lineHeight * 2 + 16;
const FRAGMENT_PADDING = 20;
const FRAGMENT_BOTTOM = 20;

type TFragment = {
  rect: TRect;
  caption: string;
  contentWidth: number;
  draw: (ctx: CanvasRenderingContext2D, topLeft: TPoint) => void;
  // The arrow from the fragment to its place on a screen.
  arrow: TPoint[];
};

const fragmentHeight = (contentHeight: number) => CONTENT_TOP + contentHeight + FRAGMENT_BOTTOM;

const FRAGMENTS_Y = bottomOf(BUILD_SCREEN) + 100;
const FRAGMENT_WIDTH = 480;

// The arrows from the fragments under the screens go up to a building of the bottom row.
// An arrow that starts beside its target goes up to `turnY`, then along to the target, then up.
const upArrow = (start: TPoint, tip: TPoint, turnY?: number): TPoint[] =>
  turnY === undefined || start.x === tip.x
    ? [{ x: tip.x, y: start.y }, { x: tip.x, y: tip.y + GAP }]
    : [start, { x: start.x, y: turnY }, { x: tip.x, y: turnY }, { x: tip.x, y: tip.y + GAP }];

const SECTION_NOTE = "Секция в выбранном здании";
const SECTION_NOTE_HEIGHT = 34;

const drawSectionNote = (ctx: CanvasRenderingContext2D, topLeft: TPoint, sectionHeight: number) => {
  drawText(ctx, SECTION_NOTE, topLeft.x, topLeft.y + sectionHeight + 10, { size: 15, color: PALETTE.muted });
};

// The farm arrow ends under the workers plate: the plate hangs below the hex.
const FARM_TIP: TPoint = { x: hexBottom(BUILD_ISLAND, FARM).x, y: bottomOf(workersPlateRect(BUILD_ISLAND, FARM)) };
const BUILD_VILLAGE_TIP = hexBottom(BUILD_ISLAND, VILLAGE);
const BARRACKS_TIP = hexBottom(BUILD_ISLAND, BARRACKS);
const COLLECT_VILLAGE_TIP = hexBottom(COLLECT_ISLAND, VILLAGE);

// Three fragments stand under the build screen in the order of their buildings in the bottom row:
// farm, village, barracks. The farm and the residents fragments stand right under their buildings.
// The barracks fragment starts further right, so its arrow turns above the fragments,
// right of the village arrow, and crosses no other arrow.
const FARM_FRAGMENT: TRect = {
  x: BUILD_SCREEN.x,
  y: FRAGMENTS_Y,
  width: FRAGMENT_WIDTH,
  height: fragmentHeight(FARM_CARD_SIZE.height),
};
const RESIDENTS_FRAGMENT: TRect = {
  x: rightOf(FARM_FRAGMENT) + 20,
  y: FRAGMENTS_Y,
  width: RESIDENTS_SECTION_SIZE.width + FRAGMENT_PADDING * 2,
  height: fragmentHeight(RESIDENTS_SECTION_SIZE.height + SECTION_NOTE_HEIGHT),
};
const BARRACKS_FRAGMENT: TRect = {
  x: rightOf(RESIDENTS_FRAGMENT) + 20,
  y: FRAGMENTS_Y,
  width: BARRACKS_CARD_SIZE.width + FRAGMENT_PADDING * 2,
  height: fragmentHeight(BARRACKS_CARD_SIZE.height),
};
const BARRACKS_TURN_Y = FRAGMENTS_Y - 40;

// The launch fragment stands under the village of the collection screen, right of the barracks fragment.
const LAUNCH_FRAGMENT: TRect = {
  x: Math.max(COLLECT_VILLAGE_TIP.x - FRAGMENT_WIDTH / 2, rightOf(BARRACKS_FRAGMENT) + 40),
  y: FRAGMENTS_Y,
  width: FRAGMENT_WIDTH,
  height: fragmentHeight(VILLAGE_SECTION_SIZE.height + SECTION_NOTE_HEIGHT),
};

// The fragments right of the collection screen: the food one at the top, the madness one level with the slot.
const RIGHT_COLUMN_X = rightOf(COLLECT_SCREEN) + 80;
const RIGHT_COLUMN_WIDTH = POPULATION_TOOLTIP_SIZE.width + FRAGMENT_PADDING * 2 + 100;

// The food arrow goes up, left above the collection screen, down between the screens
// and enters the population cell from the left, so it crosses no other cell.
const POPULATION_CELL = cellRect(hudTopLeft(COLLECT_SCREEN), "population");
const BETWEEN_SCREENS_X = (rightOf(BUILD_SCREEN) + COLLECT_SCREEN.x) / 2;
const ABOVE_SCREENS_Y = SCREENS_Y - 30;
const FOOD_FRAGMENT: TRect = {
  x: RIGHT_COLUMN_X,
  y: SCREENS_Y,
  width: RIGHT_COLUMN_WIDTH,
  height: fragmentHeight(POPULATION_TOOLTIP_SIZE.height),
};
const FOOD_ARROW_X = FOOD_FRAGMENT.x + 40;

// The madness fragment stands level with the madness line of the slot.
// Its arrow starts at the caption, 50 units under the top of the fragment.
const MAD_LINE = slotMadLine(SLOT_MODAL);
const MAD_FRAGMENT: TRect = {
  x: RIGHT_COLUMN_X,
  y: middleYOf(MAD_LINE) - 50,
  width: RIGHT_COLUMN_WIDTH,
  height: fragmentHeight(BECOME_MAD_SIZE.height),
};
// The event block ends 8 units right of the madness line.
const MAD_TIP: TPoint = { x: rightOf(MAD_LINE) + 8 + GAP, y: middleYOf(MAD_LINE) };

// The properties of a unit of population, from the user's list, word for word.
// The residents fragment is the section the user asked for later: people live in the building.
const FRAGMENTS: TFragment[] = [
  {
    rect: LAUNCH_FRAGMENT,
    caption: "появляется принудительно в здании где может располагаться население",
    contentWidth: VILLAGE_SECTION_SIZE.width,
    draw: (ctx, topLeft) => {
      drawVillageSection(ctx, topLeft);
      drawSectionNote(ctx, topLeft, VILLAGE_SECTION_SIZE.height);
    },
    arrow: upArrow({ x: COLLECT_VILLAGE_TIP.x, y: FRAGMENTS_Y }, COLLECT_VILLAGE_TIP),
  },
  {
    rect: FOOD_FRAGMENT,
    caption: "потребляет N количества 🍗 еды после фазы сбора ресурсов",
    contentWidth: POPULATION_TOOLTIP_SIZE.width,
    draw: drawPopulationTooltip,
    arrow: [
      { x: FOOD_ARROW_X, y: FOOD_FRAGMENT.y },
      { x: FOOD_ARROW_X, y: ABOVE_SCREENS_Y },
      { x: BETWEEN_SCREENS_X, y: ABOVE_SCREENS_Y },
      { x: BETWEEN_SCREENS_X, y: middleYOf(POPULATION_CELL) },
      { x: POPULATION_CELL.x, y: middleYOf(POPULATION_CELL) },
    ],
  },
  {
    rect: MAD_FRAGMENT,
    caption: "может превратиться в сумасшедшего навсегда",
    contentWidth: BECOME_MAD_SIZE.width,
    draw: drawBecomeMad,
    arrow: [
      { x: MAD_FRAGMENT.x, y: MAD_TIP.y },
      MAD_TIP,
    ],
  },
  {
    rect: FARM_FRAGMENT,
    caption: "назначается в здание как рабочий чтобы здание начинало приносить пользу",
    contentWidth: FARM_CARD_SIZE.width,
    draw: drawFarmCard,
    arrow: upArrow({ x: FARM_TIP.x, y: FRAGMENTS_Y }, FARM_TIP),
  },
  {
    rect: RESIDENTS_FRAGMENT,
    caption: "в здании располагается население (макс и текущее)",
    contentWidth: RESIDENTS_SECTION_SIZE.width,
    draw: (ctx, topLeft) => {
      drawResidentsSection(ctx, topLeft);
      drawSectionNote(ctx, topLeft, RESIDENTS_SECTION_SIZE.height);
    },
    arrow: upArrow({ x: BUILD_VILLAGE_TIP.x, y: FRAGMENTS_Y }, BUILD_VILLAGE_TIP),
  },
  {
    rect: BARRACKS_FRAGMENT,
    caption: "конвертируется в юнита в здании найма юнитов",
    contentWidth: BARRACKS_CARD_SIZE.width,
    draw: drawBarracksCard,
    arrow: upArrow({ x: BARRACKS_FRAGMENT.x + 40, y: FRAGMENTS_Y }, BARRACKS_TIP, BARRACKS_TURN_Y),
  },
];

// ---------- Mad people ----------

// The mad person and the description of the mad people.
const MAD_TOP = Math.max(...FRAGMENTS.map((fragment) => bottomOf(fragment.rect))) + 140;

// The image is 541×881 px; the rect keeps its aspect.
const MAD_PICTURE_WIDTH = 500;
const MAD_PICTURE_HEIGHT = Math.round((MAD_PICTURE_WIDTH * 881) / 541);
const MAD_PICTURE: TRect = { x: 80, y: MAD_TOP, width: MAD_PICTURE_WIDTH, height: MAD_PICTURE_HEIGHT };
// The description wraps at draw time. Its ten items take about 1000 units, more than the picture.
const MAD_DESCRIPTION_HEIGHT = 1000;

// ---------- Description of the mad people ----------

// A description block: an icon with a heading, and a bullet list under them.
type TDescription = {
  // The top left corner and the width of the block.
  rect: TRect;
  icon: TImageAsset;
  heading: string;
  items: string[];
};

const ICON_SIZE = 56;
const ITEM_STYLE = { size: 20, lineHeight: 29 };
const ITEM_GAP = 18;
const BULLET_INDENT = 22;

// The description of the mad people stands right of the mad picture, from its top.
// The user's list, word for word except the typos: "в качественно" → "в качестве",
// "общее количества" → "общего количества", the trailing ";" of the ghetto item.
// The user renamed "токсичность" to "заражение" everywhere.
// A no-break space keeps an emoji on the line of its word.
const MAD_DESCRIPTION: TDescription = {
  rect: { x: rightOf(MAD_PICTURE) + 60, y: MAD_PICTURE.y, width: 760, height: 0 },
  icon: ICON_ART.mad,
  heading: "Сумасшедшие",
  items: [
    "сумасшедшие это люди которые не сумели преодолеть невзгоды жизни и потеряли контроль над собственной волей, свалившись в нескончаемый гедонизм",
    "они появляются из-за негативного ивента (прокрут слота фортуны) и не могут быть излечены до конца игры",
    "сумасшедший может быть назначен в производственный посёлок или призван в армию как обычный человек",
    'сумасшедшие самопроизвольно строят модуль "гедонистический рай" в здании где живут, который всегда даёт гексу ☣️ заражение',
    "сумасшедший в качестве призванного юнита не будет стремиться к приказам к атаке, но будет атаковать врагов (защищать себя)",
    "сумасшедшие будут увеличивать количество вырабатываемого ☣️ заражения у гекса",
    'Сумасшедшие строят модуль "гедонистический рай" по формуле (кол-во сумасшедших в здании / общего количества населения * коэффициент появления = шанс появления), если есть свободные места под модули, появляется автоматически в фазе строительства, игроку должно прийти об этом уведомление',
    '"гедонистический рай" можно разогнать за N ресурса власти',
    'можно построить "гетто киберпсихов", куда можно селить только сумасшедших; они не строят при этом гедонистический рай и вырабатывают меньше фикс ☣️ заражения',
    "сумасшедшие будут добавлять зданию в котором живут фикс N ☣️ заражения",
  ],
};

const SHEET: TRect = {
  x: 0,
  y: 0,
  width: Math.max(rightOf(MAD_FRAGMENT), rightOf(LAUNCH_FRAGMENT)) + 80,
  height: MAD_TOP + Math.max(MAD_PICTURE_HEIGHT, MAD_DESCRIPTION_HEIGHT) + 80,
};

// ---------- Drawing ----------

const drawHeading = (ctx: CanvasRenderingContext2D, icon: TImageAsset, text: string, x: number, y: number) => {
  const iconRect: TRect = { x, y, width: ICON_SIZE, height: ICON_SIZE };
  drawImageContain(ctx, icon, iconRect);
  drawText(ctx, text, rightOf(iconRect) + 14, middleYOf(iconRect) - 18, { size: 26, weight: 600 });
};

const drawDescription = (ctx: CanvasRenderingContext2D, description: TDescription) => {
  const { rect } = description;
  drawHeading(ctx, description.icon, description.heading, rect.x, rect.y);

  let top = rect.y + ICON_SIZE + 24;
  for (const item of description.items) {
    drawText(ctx, "•", rect.x + 4, top, ITEM_STYLE);
    top = drawWrappedText(ctx, item, rect.x + BULLET_INDENT, top, rect.width - BULLET_INDENT, ITEM_STYLE) + ITEM_GAP;
  }
};

const drawScreen = (ctx: CanvasRenderingContext2D, screen: (typeof SCREENS)[number]) => {
  drawBox(ctx, screen.rect, { width: 2.5 });
  drawText(ctx, screen.caption, screen.rect.x + 24, screen.rect.y + 16, { size: 20, weight: 700 });
  screen.draw(ctx);
};

const drawFragment = (ctx: CanvasRenderingContext2D, fragment: TFragment) => {
  const { rect } = fragment;
  drawBox(ctx, rect, { color: PALETTE.muted, width: 1.5 });
  drawWrappedText(ctx, fragment.caption, rect.x + FRAGMENT_PADDING, rect.y + CAPTION_TOP, rect.width - FRAGMENT_PADDING * 2, CAPTION_STYLE);
  fragment.draw(ctx, { x: rect.x + (rect.width - fragment.contentWidth) / 2, y: rect.y + CONTENT_TOP });
};

const drawPopulationSection = (ctx: CanvasRenderingContext2D) => {
  drawHeading(ctx, ICON_ART.population, "Единица ресурса население", BUILD_SCREEN.x, POPULATION_HEADING_Y);

  for (const screen of SCREENS) {
    drawScreen(ctx, screen);
  }
  // The build phase comes before the collection phase.
  drawConnector(
    ctx,
    [
      { x: rightOf(BUILD_SCREEN) + 14, y: middleYOf(BUILD_SCREEN) },
      { x: COLLECT_SCREEN.x - 14, y: middleYOf(COLLECT_SCREEN) },
    ],
    { width: 4 },
  );

  for (const fragment of FRAGMENTS) {
    drawFragment(ctx, fragment);
  }
  for (const fragment of FRAGMENTS) {
    drawConnector(ctx, fragment.arrow);
  }
};

const drawMadSection = (ctx: CanvasRenderingContext2D) => {
  drawImageContain(ctx, MAD_PERSON_ART, MAD_PICTURE);
  drawDescription(ctx, MAD_DESCRIPTION);
};

const doc: TDocument = {
  id: "population-and-units",
  order: 4,
  title: "Ostrov 4 - Население",
  description: "Население и сумасшедшие",
  bounds: SHEET,
  draw: (ctx) => {
    drawSheet(ctx, SHEET);
    drawText(ctx, "Ostrov 4 - Население", 50, 50, { size: 36, weight: 500 });
    drawPopulationSection(ctx);
    drawMadSection(ctx);
  },
  subscribe: (onChange) => {
    const unsubscribers = ART_ASSETS.map((asset) => asset.subscribe(onChange));
    return () => {
      for (const unsubscribe of unsubscribers) {
        unsubscribe();
      }
    };
  },
};

export { doc };
