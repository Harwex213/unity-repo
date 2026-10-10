import { addressWithoutMenu, MENU_FADE_OUT_MS } from "../core/main-menu";
import { createSession } from "./game-actions";
import { enableGuide } from "./guide-actions";
import type { TDifficulty } from "../core/main-menu";
import type { TStore } from "../store/store";

/**
 * The main menu. It opens at boot with the `?menu` flag. While it is open the
 * game page is not mounted, so the canvases and the hotkeys of the game do not
 * run. A start fades the menu out over the freshly created game.
 */

/** One timer for the fade-out: a second start cannot stack another. */
let fadeTimeoutId: ReturnType<typeof setTimeout> | null = null;

/** Opens the menu and closes everything the game had open on top of the page. */
const openMainMenuAction = (store: TStore) => {
  if (fadeTimeoutId !== null) {
    clearTimeout(fadeTimeoutId);
    fadeTimeoutId = null;
  }

  store.ui.techModalOpen.value = false;
  store.ui.factionsModalFactionId.value = null;
  store.ui.menuCreditsOpen.value = false;
  store.guide.openStep.value = null;
  store.ui.mainMenu.value = "open";
};

type TStartFromMenuOptions = {
  /** Turns the learn guide on for this game. Off keeps the `?guide` flag's choice. */
  readonly guide: boolean;
};

/**
 * Starts a new game from the menu. The session is created again, so the rival
 * strongholds land after the menu has gone, not while the player read it. The
 * learn guide's intro opens once the fade-out ends: the guide waits for the
 * menu to close.
 */
const startFromMenuAction = (store: TStore, options: TStartFromMenuOptions) => {
  if (store.ui.mainMenu.peek() !== "open") {
    return;
  }

  if (options.guide) {
    enableGuide(store, true);
    store.guide.seen.value = [];
  }

  store.ui.techModalOpen.value = false;
  store.ui.factionsModalFactionId.value = null;
  store.ui.menuCreditsOpen.value = false;

  createSession(store);
  store.ui.mainMenu.value = "leaving";

  fadeTimeoutId = setTimeout(() => {
    fadeTimeoutId = null;
    store.ui.mainMenu.value = "closed";
  }, MENU_FADE_OUT_MS);
};

/**
 * "Выход". A browser tab cannot close itself, and the game has nothing behind
 * the menu, so the page reloads without the menu flag and boots the usual way.
 */
const exitMenuAction = (store: TStore) => {
  if (store.ui.mainMenu.peek() !== "open") {
    return;
  }

  window.location.replace(addressWithoutMenu(window.location.href));
};

const setDifficultyAction = (store: TStore, difficulty: TDifficulty) => {
  store.ui.difficulty.value = difficulty;
};

const openMenuCreditsAction = (store: TStore) => {
  store.ui.menuCreditsOpen.value = true;
};

const closeMenuCreditsAction = (store: TStore) => {
  store.ui.menuCreditsOpen.value = false;
};

export type { TStartFromMenuOptions };
export {
  closeMenuCreditsAction,
  exitMenuAction,
  openMainMenuAction,
  openMenuCreditsAction,
  setDifficultyAction,
  startFromMenuAction,
};
