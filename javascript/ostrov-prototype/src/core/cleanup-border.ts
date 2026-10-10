import { createRng } from "./rng";
import type { TLevelBounds } from "./cleanup-level";
import type { TRng } from "./rng";

/**
 * The edge of the cleanup map. A band of toxic plumes lines the map border,
 * with rocks rising from under the clouds. The plumes push an island back and
 * poison the player's island. The player leaves the battle only through a
 * window: a gap in the plumes between two rocks. Windows open at random
 * slots, stay open for a random time, warn before they close, and new ones
 * open elsewhere.
 *
 * Everything here is pure geometry and a seeded schedule. The sim owns the
 * state and calls these functions every tick.
 */

/** Depth of the plume band, inward from the map border. */
const BORDER_DEPTH = 300;
/** No window opens this close to a corner of the map. */
const CORNER_CLEAR = BORDER_DEPTH + 220;
const ROCK_RADIUS_MIN = 105;
const ROCK_RADIUS_SPREAD = 45;
/** The narrowest window. A wide island gets a wider one. */
const WINDOW_HALF_MIN = 330;
/** Room on each side of the island inside a window. */
const WINDOW_SIDE_ROOM = 120;
const WINDOW_OPEN_MIN = 14;
const WINDOW_OPEN_SPREAD = 12;
/** The window warns this many seconds before it closes. */
const WINDOW_WARN_SECONDS = 4;
const WINDOW_GAP_MIN = 3;
const WINDOW_GAP_SPREAD = 7;
const MIN_WINDOWS = 1;
const MAX_WINDOWS = 3;

/** 0 top, 1 right, 2 bottom, 3 left. */
type TBorderSide = 0 | 1 | 2 | 3;

type TBorderRock = {
  readonly x: number;
  readonly y: number;
  readonly radius: number;
  readonly side: TBorderSide;
  /** Shapes the outline of the rock in the renderer. */
  readonly seed: number;
};

/** A place on the border where a window can open: the gap between two rocks. */
type TBorderSlot = {
  readonly side: TBorderSide;
  /** Coordinate of the slot centre along its side: x on top and bottom, y on left and right. */
  readonly at: number;
  /** Half the free room between the two rocks. */
  readonly room: number;
};

type TBorderWindow = {
  readonly id: number;
  readonly side: TBorderSide;
  readonly at: number;
  readonly half: number;
  readonly openTick: number;
  readonly closeTick: number;
};

type TBorder = {
  readonly depth: number;
  readonly rocks: readonly TBorderRock[];
  readonly slots: readonly TBorderSlot[];
  windows: TBorderWindow[];
  nextSpawnTick: number;
  nextWindowId: number;
  readonly rng: TRng;
};

const sideLength = (bounds: TLevelBounds, side: TBorderSide) => {
  return side % 2 === 0 ? bounds.halfWidth : bounds.halfHeight;
};

/** A point on a side, `along` the side and `inward` from the border line. */
const sidePoint = (bounds: TLevelBounds, side: TBorderSide, along: number, inward: number) => {
  if (side === 0) {
    return { x: along, y: -bounds.halfHeight + inward };
  }

  if (side === 1) {
    return { x: bounds.halfWidth - inward, y: along };
  }

  if (side === 2) {
    return { x: along, y: bounds.halfHeight - inward };
  }

  return { x: -bounds.halfWidth + inward, y: along };
};

/** The unit vector into the map from a side. */
const inwardNormal = (side: TBorderSide) => {
  return [
    { x: 0, y: 1 },
    { x: -1, y: 0 },
    { x: 0, y: -1 },
    { x: 1, y: 0 },
  ][side] as { x: number; y: number };
};

/** How far a point is inside the map from a side. Negative is past the border. */
const depthFrom = (bounds: TLevelBounds, side: TBorderSide, x: number, y: number) => {
  if (side === 0) {
    return y + bounds.halfHeight;
  }

  if (side === 1) {
    return bounds.halfWidth - x;
  }

  if (side === 2) {
    return bounds.halfHeight - y;
  }

  return x + bounds.halfWidth;
};

const alongOf = (side: TBorderSide, x: number, y: number) => (side % 2 === 0 ? x : y);

/** The half width of a window that the player's island fits through. */
const windowHalfFor = (islandRadius: number) => Math.max(WINDOW_HALF_MIN, islandRadius + WINDOW_SIDE_ROOM);

/**
 * Lays out the rocks and the window slots. The rocks stand at even steps
 * along each side; the slots are the gaps between them. The step leaves room
 * for a window that the player's island fits through.
 */
const createBorder = (bounds: TLevelBounds, seed: number, islandRadius: number): TBorder => {
  const rng = createRng(seed ^ 0x5bd1e995);
  const rocks: TBorderRock[] = [];
  const slots: TBorderSlot[] = [];
  const wantedHalf = windowHalfFor(islandRadius * 1.25);
  const minStep = (wantedHalf + ROCK_RADIUS_MIN + ROCK_RADIUS_SPREAD + 40) * 2;

  for (const side of [0, 1, 2, 3] as const) {
    const half = sideLength(bounds, side);
    const usable = (half - CORNER_CLEAR) * 2;
    const gaps = Math.max(1, Math.floor(usable / minStep));
    const step = usable / gaps;
    const start = -half + CORNER_CLEAR;

    for (let index = 0; index <= gaps; index += 1) {
      const along = start + index * step + (index === 0 || index === gaps ? 0 : (rng() - 0.5) * 60);
      const radius = ROCK_RADIUS_MIN + rng() * ROCK_RADIUS_SPREAD;
      const point = sidePoint(bounds, side, along, BORDER_DEPTH * 0.45);
      rocks.push({ x: point.x, y: point.y, radius, side, seed: rng() });
    }

    for (let index = 0; index < gaps; index += 1) {
      const a = start + index * step;
      const b = a + step;
      slots.push({ side, at: (a + b) / 2, room: step / 2 - ROCK_RADIUS_MIN - ROCK_RADIUS_SPREAD - 20 });
    }
  }

  // Lone rocks in the corners, where no window opens, so the band never looks bare.
  for (const sx of [-1, 1]) {
    for (const sy of [-1, 1]) {
      const radius = ROCK_RADIUS_MIN + rng() * ROCK_RADIUS_SPREAD;
      rocks.push({
        x: sx * (bounds.halfWidth - BORDER_DEPTH * 0.55),
        y: sy * (bounds.halfHeight - BORDER_DEPTH * 0.55),
        radius,
        side: sy < 0 ? 0 : 2,
        seed: rng(),
      });
    }
  }

  return { depth: BORDER_DEPTH, rocks, slots, windows: [], nextSpawnTick: 0, nextWindowId: 1, rng };
};

const isWindowOpen = (window: TBorderWindow, tick: number) => tick >= window.openTick && tick < window.closeTick;

/** Opens a window at a free slot. Returns false when every slot is taken. */
const openWindow = (border: TBorder, tick: number, tickHz: number, islandRadius: number) => {
  const taken = new Set(border.windows.map((window) => `${window.side}:${window.at}`));
  const free = border.slots.filter((slot) => !taken.has(`${slot.side}:${slot.at}`));
  if (free.length === 0) {
    return false;
  }

  const slot = free[Math.floor(border.rng() * free.length)] as TBorderSlot;
  const seconds = WINDOW_OPEN_MIN + border.rng() * WINDOW_OPEN_SPREAD;
  border.windows.push({
    id: border.nextWindowId,
    side: slot.side,
    at: slot.at,
    half: Math.min(slot.room, windowHalfFor(islandRadius)),
    openTick: tick,
    closeTick: tick + Math.round(seconds * tickHz),
  });
  border.nextWindowId += 1;

  return true;
};

/**
 * The window schedule. Closed windows go; at least one window is always
 * open; more open at random gaps, up to three.
 */
const stepBorder = (border: TBorder, tick: number, tickHz: number, islandRadius: number) => {
  border.windows = border.windows.filter((window) => tick < window.closeTick);

  while (border.windows.length < MIN_WINDOWS && openWindow(border, tick, tickHz, islandRadius)) {
    border.nextSpawnTick = tick + Math.round((WINDOW_GAP_MIN + border.rng() * WINDOW_GAP_SPREAD) * tickHz);
  }

  if (tick >= border.nextSpawnTick && border.windows.length < MAX_WINDOWS) {
    openWindow(border, tick, tickHz, islandRadius);
    border.nextSpawnTick = tick + Math.round((WINDOW_GAP_MIN + border.rng() * WINDOW_GAP_SPREAD) * tickHz);
  }
};

/** The open window on `side` whose span holds `along`, or `null`. */
const windowAt = (border: TBorder, side: TBorderSide, along: number, tick: number) => {
  return border.windows.find((window) => window.side === side && isWindowOpen(window, tick) && Math.abs(along - window.at) <= window.half) ?? null;
};

/**
 * The side whose plumes cover the point, or -1. A point in the band of an
 * open window is clear of that side's plumes. `margin` widens the band, so a
 * hex counts as soon as its rim touches the plumes.
 */
const plumeSideAt = (border: TBorder, bounds: TLevelBounds, x: number, y: number, tick: number, margin = 0): TBorderSide | -1 => {
  for (const side of [0, 1, 2, 3] as const) {
    const depth = depthFrom(bounds, side, x, y);
    if (depth >= border.depth + margin) {
      continue;
    }

    if (!windowAt(border, side, alongOf(side, x, y), tick)) {
      return side;
    }
  }

  return -1;
};

/** Seconds until a window closes. */
const windowSecondsLeft = (window: TBorderWindow, tick: number, tickHz: number) => Math.max(0, (window.closeTick - tick) / tickHz);

const isWindowClosing = (window: TBorderWindow, tick: number, tickHz: number) => {
  return windowSecondsLeft(window, tick, tickHz) <= WINDOW_WARN_SECONDS;
};

/** The centre of a window on the border line. */
const windowCenter = (bounds: TLevelBounds, window: TBorderWindow, inward = 0) => sidePoint(bounds, window.side, window.at, inward);

export type { TBorder, TBorderRock, TBorderSide, TBorderSlot, TBorderWindow };
export {
  alongOf,
  BORDER_DEPTH,
  createBorder,
  depthFrom,
  inwardNormal,
  isWindowClosing,
  isWindowOpen,
  plumeSideAt,
  sidePoint,
  stepBorder,
  windowAt,
  windowCenter,
  windowSecondsLeft,
  WINDOW_WARN_SECONDS,
};
