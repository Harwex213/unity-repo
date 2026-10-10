import { signal } from "@preact/signals-react";
import type { TFactionId } from "../core/factions";
import type { TBuildingId, TResourceId } from "../core/types";

/**
 * Everything the shell shows that is not game truth: which building card is
 * armed, which hex the pointer is over, which modal is open.
 */

/** A point on screen: the popup anchor, a HUD icon, the end of a flight. */
type TPointerAnchor = {
  readonly x: number;
  readonly y: number;
};

/** Where the island layer sits, so the tax phase can find a hex on screen. */
type TCamera = {
  readonly x: number;
  readonly y: number;
  readonly scale: number;
};

/** What a mote can fly to: a resource icon, or the toxicity meter's flask. */
type THudAnchorId = TResourceId | "meter";

/**
 * One mote of resource or toxicity flying from a building to its HUD icon. The
 * tax phase creates them all at once with staggered delays, and each one is
 * dropped from the list when it lands and its effect is applied.
 */
type TFlight = {
  readonly id: string;
  readonly kind: "yield" | "toxicity";
  readonly icon: string;
  readonly amount: number;
  /** The hex the mote came from, for a toxicity mote that has to go back. */
  readonly hexId: string | null;
  readonly resource: TResourceId | null;
  readonly fromX: number;
  readonly fromY: number;
  readonly toX: number;
  readonly toY: number;
  readonly delayMs: number;
};

/**
 * The production reveal at the start of the tax phase. Each building plays a
 * short "production" pulse, then its rolled payout pops up above it. The
 * buildings start top to bottom, one after another, and the pulses overlap.
 */
type TProductionReveal = {
  /** When each building starts, in ms after the phase opened. */
  readonly delays: Readonly<Record<string, number>>;
  /** The reveal is over (or skipped): the plates stand still. */
  readonly done: boolean;
  /**
   * Set when the collection starts: when each building's plates fade out, in
   * ms after the collection started. A building fades as its first mote leaves.
   */
  readonly leaving: Readonly<Record<string, number>> | null;
};

/**
 * The stronghold's soil cleansing in progress. `sacrificeHexId` is `null`
 * while the player picks the hex to destroy. Once it is set, the
 * player picks the hex to purify. Nothing changes on the island until then.
 */
type TSoilCleanseMode = {
  readonly sacrificeHexId: string | null;
};

/** The short fade of the toxicity tint on a hex that was just purified. */
type TSoilCleanseFx = {
  readonly id: number;
  readonly hexId: string;
  /** The toxicity the hex had before, for the tint that fades out. */
  readonly toxicity: number;
};

const createUiState = () => ({
  /** The building card clicked in the buildings panel, waiting for a hex. */
  armedBuilding: signal<TBuildingId | null>(null),
  demolishMode: signal<boolean>(false),
  hoveredHexId: signal<string | null>(null),
  hoverAnchor: signal<TPointerAnchor | null>(null),
  /** The hex whose biome modal is open on the right. */
  selectedHexId: signal<string | null>(null),
  techModalOpen: signal<boolean>(false),
  /** The faction shown in the open factions modal. `null` while the modal is closed. */
  factionsModalFactionId: signal<TFactionId | null>(null),
  /** The building whose face popup is open in the tax phase. */
  taxPickHexId: signal<string | null>(null),
  /** The hex waiting for a "вы уверены?" answer. */
  demolishTargetHexId: signal<string | null>(null),
  /** The toggle inside that modal, remembered for the rest of the session. */
  skipDemolishConfirm: signal<boolean>(false),
  soilCleanse: signal<TSoilCleanseMode | null>(null),
  soilCleanseFx: signal<TSoilCleanseFx | null>(null),
  /** One-line feedback over the canvas: why the last click did nothing. */
  notice: signal<string | null>(null),
  /** The island layer's transform, mirrored here for the flight animation. */
  camera: signal<TCamera>({ x: 0, y: 0, scale: 1 }),
  /**
   * Where each resource icon and the meter sit on screen. The resources panel
   * and the meter measure their own parts, and the action merges them.
   */
  hudAnchors: signal<Readonly<Partial<Record<THudAnchorId, TPointerAnchor>>>>({}),
  flights: signal<readonly TFlight[]>([]),
  productionReveal: signal<TProductionReveal | null>(null),
  /** The turn is owned by an animation: end-turn is refused while this is set. */
  busy: signal<boolean>(false),
});

type TUiState = ReturnType<typeof createUiState>;

export type {
  TCamera,
  TFlight,
  THudAnchorId,
  TPointerAnchor,
  TProductionReveal,
  TSoilCleanseFx,
  TSoilCleanseMode,
  TUiState,
};
export { createUiState };
