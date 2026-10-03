import { cleanseSoil, purifyRefusal, sacrificeRefusal, soilCleanseRefusal } from "../core/soil-cleanse";
import { isBuildingAllowed } from "./build-actions";
import { replacePlayer } from "./player-updates";
import { showNotice } from "./ui-actions";
import type { TStore } from "../store/store";

/**
 * The stronghold's soil cleansing (rules in `core/soil-cleanse.ts`). The
 * button in the stronghold's hex panel arms it. The first click picks the
 * hex to destroy, the second click picks the hex to purify, and only
 * then does the island change. Esc, a right click or the button cancels.
 */

/** How long the purified hex's old tint takes to fade out. */
const SOIL_FX_MS = 1200;

let fxId = 0;

/** Why the stronghold cannot cleanse soil right now, or `null` when it can. */
const soilCleanseBlock = (store: TStore) => {
  const player = store.derived.humanPlayer.peek();
  if (!player || !isBuildingAllowed(store)) {
    return { message: "Очистка почвы доступна только в фазе строительства" };
  }

  return soilCleanseRefusal(player, store.game.soilCleansedTurn.peek() === store.game.turn.peek());
};

const cancelSoilCleanseAction = (store: TStore) => {
  store.ui.soilCleanse.value = null;
};

/** The button arms the mode, and a second press cancels it. */
const toggleSoilCleanseAction = (store: TStore) => {
  if (store.ui.soilCleanse.peek()) {
    cancelSoilCleanseAction(store);

    return;
  }

  const block = soilCleanseBlock(store);
  if (block) {
    showNotice(store, block.message);

    return;
  }

  store.ui.armedBuilding.value = null;
  store.ui.demolishMode.value = false;
  store.ui.demolishTargetHexId.value = null;
  store.ui.soilCleanse.value = { sacrificeHexId: null };
};

/** A hex click while the mode is armed: the sacrifice first, then the target. */
const pickSoilHexAction = (store: TStore, hexId: string) => {
  const mode = store.ui.soilCleanse.peek();
  const player = store.derived.humanPlayer.peek();
  if (!mode || !player || !isBuildingAllowed(store)) {
    return;
  }

  const hex = player.island.hexes.find((candidate) => candidate.id === hexId);
  if (!hex) {
    return;
  }

  if (mode.sacrificeHexId === null) {
    const refusal = sacrificeRefusal(player, hex);
    if (refusal) {
      showNotice(store, refusal.message);

      return;
    }

    store.ui.soilCleanse.value = { sacrificeHexId: hexId };

    return;
  }

  // A second click on the marked hex takes the mark back.
  if (hexId === mode.sacrificeHexId) {
    store.ui.soilCleanse.value = { sacrificeHexId: null };

    return;
  }

  const refusal = purifyRefusal(hex, mode.sacrificeHexId);
  if (refusal) {
    showNotice(store, refusal.message);

    return;
  }

  replacePlayer(store, cleanseSoil(player, mode.sacrificeHexId, hexId));
  store.game.soilCleansedTurn.value = store.game.turn.peek();
  store.ui.soilCleanse.value = null;

  // The destroyed hex is gone: nothing may point at it any more.
  if (store.ui.hoveredHexId.peek() === mode.sacrificeHexId) {
    store.ui.hoveredHexId.value = null;
    store.ui.hoverAnchor.value = null;
  }

  if (store.ui.selectedHexId.peek() === mode.sacrificeHexId) {
    store.ui.selectedHexId.value = null;
  }

  fxId += 1;
  const id = fxId;
  store.ui.soilCleanseFx.value = { id, hexId, toxicity: hex.toxicity };
  setTimeout(() => {
    if (store.ui.soilCleanseFx.peek()?.id === id) {
      store.ui.soilCleanseFx.value = null;
    }
  }, SOIL_FX_MS);
};

export { cancelSoilCleanseAction, pickSoilHexAction, toggleSoilCleanseAction };
