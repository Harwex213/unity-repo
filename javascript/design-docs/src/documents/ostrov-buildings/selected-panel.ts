import { onScreen } from "./screen";
import {
  BRIGHT_TONE,
  DIM_TONE,
  SECTION_HEIGHT,
  SECTION_WIDTH,
  drawDice,
  drawHeader,
  drawModules,
  drawVillage,
  drawWorkers,
} from "./sections";
import { STYLE, bottomOf, rect } from "./wire";
import type { TRect } from "../../core/types";

// The panel of the selected building, as the hex card of ostrov-prototype (src/ui/components/hex-modal.tsx)
// shows the village: the header, the die faces, the village launch and the workers.
// The panel is split into sections. The modules section is new: the prototype has no modules yet.
// It stands between the village section and the workers.
// Only the modules section is bright. The other sections are muted, as the rest of the HUD.

const PANEL_X = 1180;
const PANEL_TOP = 13;
const PADDING = 12;
const SECTION_GAP = 10;

type TSectionId = "header" | "dice" | "village" | "modules" | "workers";

const SECTION_ORDER: TSectionId[] = ["header", "dice", "village", "modules", "workers"];

const PANEL_HEIGHT =
  PADDING * 2 +
  SECTION_ORDER.reduce((sum, id) => sum + SECTION_HEIGHT[id], 0) +
  SECTION_GAP * (SECTION_ORDER.length - 1);

const SELECTED_PANEL: TRect = onScreen(PANEL_X, PANEL_TOP, SECTION_WIDTH + PADDING * 2, PANEL_HEIGHT);

const SECTIONS = SECTION_ORDER.reduce(
  (sections, id, index) => {
    const previous = SECTION_ORDER[index - 1];
    const top = previous ? bottomOf(sections[previous]) + SECTION_GAP : SELECTED_PANEL.y + PADDING;
    sections[id] = { x: SELECTED_PANEL.x + PADDING, y: top, width: SECTION_WIDTH, height: SECTION_HEIGHT[id] };
    return sections;
  },
  {} as Record<TSectionId, TRect>,
);

const drawSelectedPanel = (ctx: CanvasRenderingContext2D) => {
  rect(ctx, SELECTED_PANEL, 10, STYLE.panel);
  drawHeader(ctx, SECTIONS.header, DIM_TONE);
  drawDice(ctx, SECTIONS.dice, DIM_TONE);
  drawVillage(ctx, SECTIONS.village, DIM_TONE);
  drawModules(ctx, SECTIONS.modules, BRIGHT_TONE);
  drawWorkers(ctx, SECTIONS.workers, DIM_TONE);
};

const MODULES_SECTION = SECTIONS.modules;
const HEADER_SECTION = SECTIONS.header;

export { HEADER_SECTION, MODULES_SECTION, SELECTED_PANEL, drawSelectedPanel };
