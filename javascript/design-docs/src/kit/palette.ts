// The colors of an Excalidraw wireframe.
// A document is always drawn on white paper, in light and dark themes alike.
const PALETTE = {
  paper: "#ffffff",
  sheetBorder: "#ced4da",
  ink: "#1e1e1e",
  muted: "#868e96",
  positive: "#2f9e44",
  negative: "#e03131",
  positiveFill: "#b2f2bb",
  negativeFill: "#ffc9c9",
  blockedFill: "#ced4da",
  // Units in battle: the player's and the enemy's.
  ally: "#1971c2",
  enemy: "#e03131",
  // The toxicity tint of a hex in ostrov-prototype (island-canvas.tsx).
  toxic: "#9bff4f",
  // Player actions.
  action: "#7048e8",
  actionFill: "#d0bfff",
  // The backgrounds of the sections of a document.
  zoneBuild: "#fff9db",
  zoneTax: "#ebfbee",
  zoneScout: "#e7f5ff",
  zoneBattle: "#fff5f5",
  // The background of a table header.
  headerFill: "#f1f3f5",
} as const;

const FONT_FAMILY = 'system-ui, -apple-system, "Segoe UI", sans-serif';
// Identifiers from code, for example biome ids.
const MONO_FONT_FAMILY = 'ui-monospace, "SF Mono", Menlo, monospace';

export { FONT_FAMILY, MONO_FONT_FAMILY, PALETTE };
