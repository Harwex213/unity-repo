import type { TRect } from "../../core/types";

// The game screen of the build phase. It is 1600 × 900, as the HUD schemes in docs/spec/ui-phase-*.html.
// The HUD modules keep their numbers in screen units: (0, 0) is the top left corner of the screen.
// `onScreen` moves a screen rect to world units.

// The screen stands under the title of the sheet.
const SCREEN: TRect = { x: 60, y: 120, width: 1600, height: 900 };

const onScreen = (x: number, y: number, width: number, height: number): TRect => ({
  x: SCREEN.x + x,
  y: SCREEN.y + y,
  width,
  height,
});

// The bottom bar of the HUD: resources, tools, buildings and the mini globe.
const BOTTOM_BAR = { top: 777, height: 110 };
const bottomPanel = (x: number, width: number): TRect => onScreen(x, BOTTOM_BAR.top, width, BOTTOM_BAR.height);

export { SCREEN, bottomPanel, onScreen };
