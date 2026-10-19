import { resolveRiot } from "../core/riot";
import { createRng, hashSeed } from "../core/rng";
import { isStarving } from "../core/tax";
import { replacePlayer } from "./player-updates";
import { showNotice } from "./ui-actions";
import type { TPlayer } from "../core/types";
import type { TStore } from "../store/store";

/**
 * The starvation riot of the human player. It runs at the start of the
 * clearing phase, before the battle level is built, so a battle fights with
 * the army and the island the riot left. The rules live in `core/riot.ts`.
 * The rivals riot through `playRivalRiot` in `rival-actions.ts`.
 */

/** What runs when the player closes the riot modal. A new riot replaces it. */
let onClosed: (() => void) | null = null;

const riotRng = (store: TStore, player: TPlayer) => {
  return createRng(hashSeed(`${store.game.nickname.peek()}:riot:${store.game.turn.peek()}:${player.id}`));
};

/**
 * Riots the starving player. Returns `true` when the riot modal is open: the
 * caller then waits for `closeRiotAction`, which runs `whenClosed`.
 */
const startRiotAction = (store: TStore, whenClosed: () => void): boolean => {
  const player = store.derived.humanPlayer.peek();
  if (!player || !isStarving(player)) {
    return false;
  }

  const { player: next, report } = resolveRiot(player, riotRng(store, player));
  replacePlayer(store, next);

  if (report.rioters.total === 0) {
    showNotice(store, "Голод, но бунтовать некому");

    return false;
  }

  onClosed = whenClosed;
  store.battle.riot.value = report;

  return true;
};

/** "Ясно" in the riot modal. The turn goes on from where the riot held it. */
const closeRiotAction = (store: TStore) => {
  if (!store.battle.riot.peek()) {
    return;
  }

  store.battle.riot.value = null;
  const done = onClosed;
  onClosed = null;
  done?.();
};

/** Drops a riot left open, for a new session or the end of the turn. */
const clearRiotState = (store: TStore) => {
  store.battle.riot.value = null;
  onClosed = null;
};

export { clearRiotState, closeRiotAction, riotRng, startRiotAction };
