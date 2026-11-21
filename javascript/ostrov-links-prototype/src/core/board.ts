/**
 * A board is a canvas with zoom and pan. The designer places nodes on it by hand
 * and, on some boards, links them. The layout lives in `data/<board>.json`, see
 * `scripts/boards.mjs`. Nothing here knows which nodes a board shows.
 */

type TPoint = {
  readonly x: number;
  readonly y: number;
};

type TSize = {
  readonly width: number;
  readonly height: number;
};

type TRect = TPoint & TSize;

/** A link between two nodes. The ids are sorted, so a pair has one spelling. */
type TBoardEdge = readonly [string, string];

type TBoardLayout = {
  /** The top-left corner of every node, in board units. */
  readonly positions: Readonly<Record<string, TPoint>>;
  readonly edges: readonly TBoardEdge[];
};

/** `screen = board * scale + (x, y)`. */
type TCamera = TPoint & {
  readonly scale: number;
};

const BOARD_FORMAT = "ostrov-board";
const BOARD_VERSION = 1;

const MIN_SCALE = 0.15;
const MAX_SCALE = 3;

const isRecord = (value: unknown): value is Record<string, unknown> => {
  return typeof value === "object" && value !== null && !Array.isArray(value);
};

const isPoint = (value: unknown): value is TPoint => {
  return isRecord(value) && Number.isFinite(value.x) && Number.isFinite(value.y);
};

const makeEdge = (a: string, b: string): TBoardEdge => (a < b ? [a, b] : [b, a]);

const sameEdge = (left: TBoardEdge, right: TBoardEdge) => left[0] === right[0] && left[1] === right[1];

/** Adds the link between `a` and `b`, or removes it if it exists. */
const toggleEdge = (edges: readonly TBoardEdge[], a: string, b: string): readonly TBoardEdge[] => {
  const edge = makeEdge(a, b);

  return edges.some((item) => sameEdge(item, edge)) ? edges.filter((item) => !sameEdge(item, edge)) : [...edges, edge];
};

/** The nodes linked to `id`. */
const neighbours = (edges: readonly TBoardEdge[], id: string): readonly string[] => {
  return edges.flatMap(([a, b]) => (a === id ? [b] : b === id ? [a] : []));
};

/**
 * Places the nodes the file does not know in a column right of the other nodes,
 * so a new building or technology shows up without a manual edit.
 */
const withFallbackPositions = (
  known: Readonly<Record<string, TPoint>>,
  nodeIds: readonly string[],
  size: TSize,
): Readonly<Record<string, TPoint>> => {
  const placed = Object.values(known);
  const right = placed.length === 0 ? 0 : Math.max(...placed.map((point) => point.x)) + size.width + 80;
  const top = placed.length === 0 ? 0 : Math.min(...placed.map((point) => point.y));
  const positions: Record<string, TPoint> = { ...known };
  let row = 0;

  for (const id of nodeIds) {
    if (!positions[id]) {
      positions[id] = { x: right, y: top + row * (size.height + 24) };
      row += 1;
    }
  }

  return positions;
};

/** Reads a board file. Unknown node ids and links to them are dropped: a removed item leaves nothing. */
const parseBoardLayout = (value: unknown, nodeIds: readonly string[], size: TSize): TBoardLayout => {
  if (!isRecord(value) || value.format !== BOARD_FORMAT || value.version !== BOARD_VERSION) {
    throw new Error(`Это не файл доски: нужен "format": "${BOARD_FORMAT}", "version": ${BOARD_VERSION}.`);
  }

  if (!isRecord(value.nodes) || !Array.isArray(value.edges)) {
    throw new Error("Поле nodes должно быть объектом, поле edges — массивом.");
  }

  const ids = new Set(nodeIds);
  const known: Record<string, TPoint> = {};
  for (const id of nodeIds) {
    const position = value.nodes[id];
    if (isPoint(position)) {
      known[id] = { x: Math.round(position.x), y: Math.round(position.y) };
    }
  }

  const edges: TBoardEdge[] = [];
  for (const item of value.edges) {
    if (Array.isArray(item) && item.length === 2 && ids.has(item[0]) && ids.has(item[1]) && item[0] !== item[1]) {
      const edge = makeEdge(item[0], item[1]);
      if (!edges.some((existing) => sameEdge(existing, edge))) {
        edges.push(edge);
      }
    }
  }

  return { positions: withFallbackPositions(known, nodeIds, size), edges };
};

/** The body of a POST. The dev server formats the file itself. */
const serializeBoardLayout = (layout: TBoardLayout, nodeIds: readonly string[]) => {
  return JSON.stringify({
    format: BOARD_FORMAT,
    version: BOARD_VERSION,
    nodes: Object.fromEntries(nodeIds.map((id) => [id, layout.positions[id]])),
    edges: layout.edges,
  });
};

const boundsOf = (positions: Readonly<Record<string, TPoint>>, size: TSize): TRect => {
  const points = Object.values(positions);
  if (points.length === 0) {
    return { x: 0, y: 0, ...size };
  }

  const left = Math.min(...points.map((point) => point.x));
  const top = Math.min(...points.map((point) => point.y));
  const right = Math.max(...points.map((point) => point.x + size.width));
  const bottom = Math.max(...points.map((point) => point.y + size.height));

  return { x: left, y: top, width: right - left, height: bottom - top };
};

const clampScale = (scale: number) => Math.min(MAX_SCALE, Math.max(MIN_SCALE, scale));

/** Zooms by `factor` and keeps the board point under `screen` in place. */
const zoomAt = (camera: TCamera, screen: TPoint, factor: number): TCamera => {
  const scale = clampScale(camera.scale * factor);
  const ratio = scale / camera.scale;

  return {
    scale,
    x: screen.x - (screen.x - camera.x) * ratio,
    y: screen.y - (screen.y - camera.y) * ratio,
  };
};

/** The camera that shows the whole rect in a viewport. */
const fitRect = (rect: TRect, viewport: TSize, padding = 32): TCamera => {
  const scale = clampScale(
    Math.min((viewport.width - padding * 2) / rect.width, (viewport.height - padding * 2) / rect.height, 1.5),
  );

  return {
    scale,
    x: (viewport.width - rect.width * scale) / 2 - rect.x * scale,
    y: (viewport.height - rect.height * scale) / 2 - rect.y * scale,
  };
};

/** Moves the camera so the board point is in the middle of the viewport. The zoom stays. */
const centerOn = (camera: TCamera, point: TPoint, viewport: TSize): TCamera => ({
  scale: camera.scale,
  x: viewport.width / 2 - point.x * camera.scale,
  y: viewport.height / 2 - point.y * camera.scale,
});

export type { TBoardEdge, TBoardLayout, TCamera, TPoint, TRect, TSize };
export {
  boundsOf,
  centerOn,
  fitRect,
  neighbours,
  parseBoardLayout,
  serializeBoardLayout,
  toggleEdge,
  withFallbackPositions,
  zoomAt,
};
