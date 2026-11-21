import { PALETTE as BASE_PALETTE } from "../../kit/palette";

// The base wireframe colors plus the colors of this document.
const PALETTE = {
  ...BASE_PALETTE,
  // The rest of the HUD is light grey: it stands nearby, but the document is not about it.
  dim: "#ced4da",
  dimText: "#adb5bd",
  // The quiet fill of a button, a chip, a filled slot.
  soft: "#f1f3f5",
  // The selected hex and its link to the selected building panel.
  selected: "#1971c2",
  // The full hp bar of the selected building panel in ostrov-prototype.
  hp: "#40c057",
} as const;

export { PALETTE };
