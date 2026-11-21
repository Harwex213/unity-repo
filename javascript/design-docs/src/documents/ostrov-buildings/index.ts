import { drawConnector, drawSheet, drawText } from "../../kit/shapes";
import { ART_ASSETS } from "./art";
import {
  BUILDING_CONDITIONS,
  BUILDING_STATS,
  MODULE_CONDITIONS,
  MODULE_STATS,
  blockHeight,
  drawBlock,
} from "./blocks";
import { BUILDINGS_PANEL, drawBuildingsPanel } from "./buildings-panel";
import { drawDimHud } from "./hud";
import { drawIsland, selectedHexRight } from "./island";
import { PALETTE } from "./palette";
import { SCREEN } from "./screen";
import { SECTIONS_BLOCK_HEIGHT, SECTIONS_BLOCK_WIDTH, drawSectionsBlock } from "./sections-block";
import { HEADER_SECTION, MODULES_SECTION, SELECTED_PANEL, drawSelectedPanel } from "./selected-panel";
import { STYLE, bottomOf, centerXOf, dashedArrow, middleYOf, rect, rightOf } from "./wire";
import type { TDocument, TRect } from "../../core/types";

// Ostrov 3 - Здания.
// One HUD wireframe of the build phase and five blocks around it.
// Bright: the buildings panel, the island with hexes and buildings, the modules section of the selected building panel.
// The rest of the HUD is light grey.
// Block 1 points at the buildings panel, block 2 at the modules section of the selected building panel.
// Block 3 stands next to block 1, block 4 next to block 2.
// A line goes from the selected building panel to block 5, the list of the possible sections of a panel.
// All coordinates are world units.

const MARGIN = 60;
const BLOCK_GAP = 40;
// The free strip between the screen and the blocks. The arrows of blocks 1 and 2 cross it.
const ARROW_SPAN = 110;
const ARROW_GAP = 6;

const CONDITIONS_WIDTH = 520;
const STATS_WIDTH = 550;

// Block 1 stands under the buildings panel, so its arrow goes straight up.
const BLOCK_1: TRect = {
  x: centerXOf(BUILDINGS_PANEL) - CONDITIONS_WIDTH / 2,
  y: bottomOf(SCREEN) + ARROW_SPAN,
  width: CONDITIONS_WIDTH,
  height: blockHeight(BUILDING_CONDITIONS),
};

// Block 3 stands left of block 1, under the resources of the HUD.
const BLOCK_3: TRect = {
  x: SCREEN.x,
  y: BLOCK_1.y,
  width: STATS_WIDTH,
  height: blockHeight(BUILDING_STATS),
};

// Block 2 stands right of the screen, level with the modules section, so its arrow goes straight left.
const BLOCK_2_HEIGHT = blockHeight(MODULE_CONDITIONS);
const BLOCK_2: TRect = {
  x: rightOf(SCREEN) + ARROW_SPAN,
  y: middleYOf(MODULES_SECTION) - BLOCK_2_HEIGHT / 2,
  width: CONDITIONS_WIDTH,
  height: BLOCK_2_HEIGHT,
};

// Block 4 stands under block 2.
const BLOCK_4: TRect = {
  x: BLOCK_2.x,
  y: bottomOf(BLOCK_2) + BLOCK_GAP,
  width: STATS_WIDTH,
  height: blockHeight(MODULE_STATS),
};

// Block 5 stands right of blocks 2 and 4, level with the top of the screen.
// The line to it runs above block 2, through the free space right of the screen.
const SECTIONS_LINE_SPAN = 200;
const BLOCK_5: TRect = {
  x: Math.max(rightOf(BLOCK_2), rightOf(BLOCK_4)) + SECTIONS_LINE_SPAN / 2,
  y: SCREEN.y,
  width: SECTIONS_BLOCK_WIDTH,
  height: SECTIONS_BLOCK_HEIGHT,
};

const SHEET: TRect = {
  x: 0,
  y: 0,
  width: rightOf(BLOCK_5) + MARGIN,
  height: Math.max(bottomOf(BLOCK_3), bottomOf(BLOCK_1), bottomOf(BLOCK_4), bottomOf(BLOCK_5)) + MARGIN,
};

const drawTitle = (ctx: CanvasRenderingContext2D) => {
  drawText(ctx, "Ostrov 3 - Здания", MARGIN, 40, { size: 36, weight: 500 });
};

const drawHud = (ctx: CanvasRenderingContext2D) => {
  rect(ctx, SCREEN, 6, STYLE.screen);
  drawDimHud(ctx);
  drawIsland(ctx);
  drawBuildingsPanel(ctx);
  drawSelectedPanel(ctx);

  // The selected hex is linked to its panel.
  const from = selectedHexRight(ARROW_GAP);
  dashedArrow(ctx, from, { x: SELECTED_PANEL.x - ARROW_GAP, y: from.y }, { color: PALETTE.selected, width: 2.5 });
};

const drawArrows = (ctx: CanvasRenderingContext2D) => {
  const buildingsX = centerXOf(BLOCK_1);
  drawConnector(ctx, [
    { x: buildingsX, y: BLOCK_1.y - ARROW_GAP },
    { x: buildingsX, y: bottomOf(BUILDINGS_PANEL) + ARROW_GAP },
  ]);
  const modulesY = middleYOf(BLOCK_2);
  drawConnector(ctx, [
    { x: BLOCK_2.x - ARROW_GAP, y: modulesY },
    { x: rightOf(MODULES_SECTION) + ARROW_GAP, y: modulesY },
  ]);

  // The panel is put together from sections: a line from the right edge of the panel,
  // level with its header, to block 5. The caption stands above the line, between the screen and block 5.
  const sectionsY = middleYOf(HEADER_SECTION);
  drawConnector(ctx, [
    { x: rightOf(SELECTED_PANEL) + ARROW_GAP, y: sectionsY },
    { x: BLOCK_5.x - ARROW_GAP, y: sectionsY },
  ]);
  drawText(ctx, "Панель здания собирается из секций", (rightOf(SCREEN) + BLOCK_5.x) / 2, sectionsY - 34, {
    size: 18,
    weight: 500,
    align: "center",
  });
};

const doc: TDocument = {
  id: "ostrov-buildings",
  order: 3,
  title: "Ostrov 3 - Здания",
  description: "Здания острова",
  bounds: SHEET,
  draw: (ctx) => {
    drawSheet(ctx, SHEET);
    drawTitle(ctx);
    drawHud(ctx);
    drawBlock(ctx, BUILDING_CONDITIONS, BLOCK_1);
    drawBlock(ctx, MODULE_CONDITIONS, BLOCK_2);
    drawBlock(ctx, BUILDING_STATS, BLOCK_3);
    drawBlock(ctx, MODULE_STATS, BLOCK_4);
    drawSectionsBlock(ctx, BLOCK_5);
    drawArrows(ctx);
  },
  subscribe: (onChange) => {
    const stops = ART_ASSETS.map((asset) => asset.subscribe(onChange));
    return () => {
      for (const stop of stops) {
        stop();
      }
    };
  },
};

export { doc };
