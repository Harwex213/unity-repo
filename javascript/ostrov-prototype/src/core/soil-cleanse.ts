import { isConnected } from "./cleanup-attach";
import type { THex, TPlayer } from "./types";

/**
 * Soil cleansing (очистка почвы), the stronghold's action in the build phase.
 * The player destroys one hex of their island, and the toxicity of another
 * hex drops to 0.
 *
 * - The sacrificed hex is removed from the island, with whatever stands on it.
 *   Any hex can be sacrificed, empty, built or dead, except the stronghold hex.
 *   The island must stay one piece, as it does when the cleanup phase grows it
 *   (see `core/cleanup-attach.ts`). A later growth may fill the hole again.
 * - The purified hex is any other hex of the island with toxicity above 0,
 *   the stronghold hex and a dead hex included. A dead hex comes back to life.
 * - The action costs no resources. The hex is the price. It works once per turn.
 */

type TSoilRefusal = {
  readonly message: string;
};

/** A hex that can take the cleansing while `sacrificeHexId` is sacrificed. */
const isPurifiable = (hex: THex, sacrificeHexId: string) => {
  return hex.id !== sacrificeHexId && hex.toxicity > 0;
};

/** The island without the hex. */
const withoutHex = (player: TPlayer, hexId: string) => {
  return player.island.hexes.filter((hex) => hex.id !== hexId);
};

/** Returns `null` when the hex can be sacrificed, or the reason why not. */
const sacrificeRefusal = (player: TPlayer, hex: THex): TSoilRefusal | null => {
  if (player.strongholdHexId === hex.id) {
    return { message: "Гекс твердыни уничтожить нельзя" };
  }

  if (!isConnected(withoutHex(player, hex.id))) {
    return { message: "Без этого гекса остров распадётся на части" };
  }

  // A sacrifice with nothing left to purify would only shrink the island.
  if (!player.island.hexes.some((other) => isPurifiable(other, hex.id))) {
    return { message: "Кроме этого гекса, на острове нет токсичной почвы" };
  }

  return null;
};

/** Returns `null` when the hex can be purified after the sacrifice, or the reason why not. */
const purifyRefusal = (hex: THex, sacrificeHexId: string): TSoilRefusal | null => {
  if (hex.id === sacrificeHexId) {
    return { message: "Этот гекс будет уничтожен" };
  }

  if (hex.toxicity <= 0) {
    return { message: "Почва здесь уже чистая" };
  }

  return null;
};

/** Returns `null` when the stronghold can cleanse soil now, or the reason why not. */
const soilCleanseRefusal = (player: TPlayer, usedThisTurn: boolean): TSoilRefusal | null => {
  if (usedThisTurn) {
    return { message: "Почву уже очищали в этот ход" };
  }

  if (!player.island.hexes.some((hex) => hex.toxicity > 0)) {
    return { message: "На острове нет токсичной почвы" };
  }

  if (!player.island.hexes.some((hex) => sacrificeRefusal(player, hex) === null)) {
    return { message: "Нет гекса, который можно уничтожить ради очистки" };
  }

  return null;
};

/**
 * The player after the cleansing: the sacrificed hex is gone with its building,
 * and the target is clean.
 */
const cleanseSoil = (player: TPlayer, sacrificeHexId: string, purifyHexId: string): TPlayer => {
  const hexes = withoutHex(player, sacrificeHexId).map((hex) => {
    return hex.id === purifyHexId ? { ...hex, toxicity: 0 } : hex;
  });

  return { ...player, island: { hexes } };
};

export type { TSoilRefusal };
export { cleanseSoil, isPurifiable, purifyRefusal, sacrificeRefusal, soilCleanseRefusal };
