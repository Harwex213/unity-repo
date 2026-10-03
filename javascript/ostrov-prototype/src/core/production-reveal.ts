import { HEX_SIZE, hexToPixel } from "./hex";
import type { THex } from "./types";

/**
 * The timing of the production reveal at the start of the tax phase. Every
 * building with a die plays a short pulse, and its rolled payout pops up above
 * it as small plates. The buildings go top to bottom, ties left to right. Each
 * building starts `REVEAL_STAGGER_MS` after the previous one started, so the
 * pulses overlap like an avalanche. The island layer only translates and
 * scales, so the order on the canvas is the order on screen.
 */

/** The gap between the starts of two buildings in a row. */
const REVEAL_STAGGER_MS = 110;
/** The building's squash-and-stretch pulse. */
const PULSE_MS = 420;
/** The first plate pops when the pulse releases, this long after the start. */
const PLATE_AT_MS = 220;
/** Each next plate of the same building pops this much later. */
const PLATE_STEP_MS = 70;
/** One plate's pop-in. */
const PLATE_POP_MS = 260;
/** A building shows at most this many plates: the yield and the toxicity or power. */
const MAX_PLATES = 2;
/** The plates fade out this fast when the collection takes them away. */
const PLATE_LEAVE_MS = 220;

/** With reduced motion the plates only fade in, at the building's turn. */
const REDUCED_PLATE_AT_MS = 0;
const REDUCED_PLATE_STEP_MS = 40;
const REDUCED_PLATE_POP_MS = 150;

type TRevealTiming = {
  readonly plateAtMs: number;
  readonly plateStepMs: number;
  readonly platePopMs: number;
};

const FULL_TIMING: TRevealTiming = { plateAtMs: PLATE_AT_MS, plateStepMs: PLATE_STEP_MS, platePopMs: PLATE_POP_MS };
const REDUCED_TIMING: TRevealTiming = {
  plateAtMs: REDUCED_PLATE_AT_MS,
  plateStepMs: REDUCED_PLATE_STEP_MS,
  platePopMs: REDUCED_PLATE_POP_MS,
};

const prefersReducedMotion = () => {
  return typeof window !== "undefined" && window.matchMedia?.("(prefers-reduced-motion: reduce)").matches === true;
};

const revealTiming = (): TRevealTiming => (prefersReducedMotion() ? REDUCED_TIMING : FULL_TIMING);

/** When each hex starts its pulse: top to bottom, ties left to right. */
const revealDelays = (hexes: readonly THex[]): Record<string, number> => {
  const sorted = hexes
    .map((hex) => ({ hex, point: hexToPixel(hex.q, hex.r, HEX_SIZE) }))
    .sort((a, b) => a.point.y - b.point.y || a.point.x - b.point.x);
  const delays: Record<string, number> = {};

  sorted.forEach((entry, index) => {
    delays[entry.hex.id] = index * REVEAL_STAGGER_MS;
  });

  return delays;
};

/** How long the whole reveal lasts: the last building's last plate has landed. */
const revealDurationMs = (delays: Readonly<Record<string, number>>) => {
  const timing = revealTiming();
  const lastStart = Object.values(delays).reduce((latest, delay) => Math.max(latest, delay), 0);
  const plates = lastStart + timing.plateAtMs + (MAX_PLATES - 1) * timing.plateStepMs + timing.platePopMs;

  return Math.max(plates, prefersReducedMotion() ? 0 : lastStart + PULSE_MS);
};

export type { TRevealTiming };
export { PLATE_LEAVE_MS, PULSE_MS, revealDelays, revealDurationMs, revealTiming };
