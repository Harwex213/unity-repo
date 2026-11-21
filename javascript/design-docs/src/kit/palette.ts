// The base colors of an Excalidraw wireframe, shared by all documents.
// A document is always drawn on white paper, in light and dark themes alike.
// A document keeps its own colors in its own folder and spreads this palette into them.
const PALETTE = {
  paper: "#ffffff",
  sheetBorder: "#ced4da",
  ink: "#1e1e1e",
  muted: "#868e96",
  positive: "#2f9e44",
  negative: "#e03131",
  positiveFill: "#b2f2bb",
  negativeFill: "#ffc9c9",
} as const;

const FONT_FAMILY = 'system-ui, -apple-system, "Segoe UI", sans-serif';
// Identifiers from code, for example biome ids.
const MONO_FONT_FAMILY = 'ui-monospace, "SF Mono", Menlo, monospace';

export { FONT_FAMILY, MONO_FONT_FAMILY, PALETTE };
