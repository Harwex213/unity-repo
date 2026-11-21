import { PALETTE as BASE_PALETTE } from "../../kit/palette";

// The base wireframe colors plus the colors of this document.
const PALETTE = {
  ...BASE_PALETTE,
  // The callouts of a phase scheme, as in docs/spec/ui-phase-*.html:
  // red marks a part that stays on screen for the whole phase,
  // blue marks a part that appears after an action of the player.
  always: "#e03131",
  onAction: "#1971c2",
  // The game screen under the panels.
  screen: "#f8f9fa",
  // The quiet fill of a selected row, a resource icon, a hex with a building.
  soft: "#e9ecef",
  // The hatching of the fog and of the hidden panels.
  hatch: "#ced4da",
} as const;

export { PALETTE };
