import { PALETTE as BASE_PALETTE } from "../../kit/palette";

// The base wireframe colors plus the colors of this document.
const PALETTE = {
  ...BASE_PALETTE,
  // The light fill of the prototype's panels, drawn as a wireframe.
  panelFill: "#f8f9fa",
  // A highlighted row of a panel.
  highlightFill: "#fff3bf",
} as const;

// Things of other systems are drawn at this opacity: they stand "near", but the sheet is about the population.
const DIM_ALPHA = 0.28;

export { DIM_ALPHA, PALETTE };
