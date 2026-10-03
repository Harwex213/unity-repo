import type { THex, TPlayer } from "./types";

/**
 * The demolish rules. The demolish tool and the soil cleansing of the
 * stronghold both remove a building through them, so the two cannot drift.
 */

type TDemolishRefusalCode = "stronghold" | "empty";

type TDemolishRefusal = {
  readonly code: TDemolishRefusalCode;
  readonly message: string;
};

/** Returns `null` when the hex has a building that can be demolished, or the reason why not. */
const demolishRefusal = (player: TPlayer, hex: THex): TDemolishRefusal | null => {
  if (player.strongholdHexId === hex.id) {
    return { code: "stronghold", message: "Твердыню снести нельзя" };
  }

  if (hex.building === null) {
    return { code: "empty", message: "Здесь нечего сносить" };
  }

  return null;
};

/** The hex after a demolish: no building, and its toxicity is wiped. */
const demolishedHex = (hex: THex): THex => ({ ...hex, building: null, toxicity: 0 });

export type { TDemolishRefusal, TDemolishRefusalCode };
export { demolishedHex, demolishRefusal };
