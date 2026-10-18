import { settlePopulation } from "../core/population";
import type { TStore } from "../store/store";
import type { THex, TPlayer } from "../core/types";

/**
 * Shared writes over the player list. Actions in the other domain files use
 * these instead of reaching into `store.game.players` by hand.
 */

/**
 * Every write of a player goes through here, so the population rule holds
 * after any change: the army and the workers never outnumber the population.
 */
const replacePlayer = (store: TStore, next: TPlayer) => {
  const settled = settlePopulation(next);

  store.game.players.value = store.game.players
    .peek()
    .map((player) => (player.id === settled.id ? settled : player));
};

/** The player with a new set of hexes. Toxicity lives on the hexes themselves. */
const withIslandHexes = (player: TPlayer, hexes: readonly THex[]): TPlayer => ({
  ...player,
  island: { hexes },
});

/** Applies `change` to one hex of a player's island and writes the player back. */
const updateHex = (store: TStore, player: TPlayer, hexId: string, change: (hex: THex) => THex) => {
  const hexes = player.island.hexes.map((hex) => (hex.id === hexId ? change(hex) : hex));

  replacePlayer(store, withIslandHexes(player, hexes));
};

export { replacePlayer, updateHex, withIslandHexes };
