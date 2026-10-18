import { populationOf, staffRefusal, withArmySize, withStaff } from "../core/population";
import { isBuildingAllowed } from "./build-actions";
import { replacePlayer } from "./player-updates";
import { showNotice } from "./ui-actions";
import type { TStore } from "../store/store";

/**
 * The player moves people between the three states: idle, army and buildings.
 * Only the build phase allows it: the tax phase has already rolled the dice of
 * the working buildings, and the clearing phase has already fielded the army.
 */

const PHASE_NOTICE = "Жителей можно перераспределять только в фазе строительства";

/** Puts `count` workers into the building on the hex, or takes them out. */
const setHexWorkersAction = (store: TStore, hexId: string, count: number) => {
  const player = store.derived.humanPlayer.peek();
  if (!player || store.derived.isReadonly.peek()) {
    return;
  }

  if (!isBuildingAllowed(store)) {
    showNotice(store, PHASE_NOTICE);

    return;
  }

  const refusal = staffRefusal(player, hexId, count);
  if (refusal) {
    showNotice(store, refusal);

    return;
  }

  replacePlayer(store, withStaff(player, hexId, count));
};

/** Moves `delta` idle people into the army, or soldiers back to idle when negative. */
const changeArmyAction = (store: TStore, delta: number) => {
  const player = store.derived.humanPlayer.peek();
  if (!player) {
    return;
  }

  if (!isBuildingAllowed(store)) {
    showNotice(store, PHASE_NOTICE);

    return;
  }

  if (delta > 0 && populationOf(player).idle <= 0) {
    showNotice(store, "Нет свободных жителей");

    return;
  }

  replacePlayer(store, withArmySize(player, player.army + delta));
};

export { changeArmyAction, setHexWorkersAction };
