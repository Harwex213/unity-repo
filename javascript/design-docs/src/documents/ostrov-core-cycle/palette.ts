import { PALETTE as BASE_PALETTE } from "../../kit/palette";

// The base wireframe colors plus the colors of this document.
const PALETTE = {
  ...BASE_PALETTE,
  // Globe cells the player cannot fly through.
  blockedFill: "#ced4da",
  // Units in battle: the player's and the enemy's.
  ally: "#1971c2",
  enemy: "#e03131",
  // The toxicity tint of a hex in ostrov-prototype (island-canvas.tsx).
  toxic: "#9bff4f",
  // Player actions.
  action: "#7048e8",
  actionFill: "#d0bfff",
  // The backgrounds of the sections of the cycle.
  zoneBuild: "#fff9db",
  zoneTax: "#ebfbee",
  zoneScout: "#e7f5ff",
  zoneBattle: "#fff5f5",
} as const;

export { PALETTE };
