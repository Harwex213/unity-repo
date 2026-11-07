import { batch } from "@preact/signals-react";
import { decodeSnapshot, encodeSnapshot } from "../core/snapshot-codec";
import { resumeRivals } from "./game-actions";
import { clearMenuFade } from "./menu-actions";
import { resetReadiness } from "./ready-actions";
import { clearRiotState } from "./riot-actions";
import { writeRouteToAddress } from "./route-actions";
import { clearSetupTimers } from "./setup-actions";
import { resetSlot } from "./slot-actions";
import { clearTaxState } from "./tax-actions";
import { showNotice } from "./ui-actions";
import type { Signal } from "@preact/signals-react";
import type { TStore } from "../store/store";

/**
 * The console commands `prototype.saveStoreSnapshot()` and
 * `prototype.loadStoreSnapshot()`. The dev server keeps the snapshot in one
 * file: see `dev-middleware/store-snapshot-middleware.mjs`.
 */

const SNAPSHOT_URL = "/__store-snapshot";

/** The slices of signals. `derived` is computed from them, so it is not saved. */
const SLICES = ["route", "game", "ui", "world", "battle", "guide"] as const;

/** These values describe the current screen, so a load keeps them. The HUD measures them. */
const SCREEN_KEYS: ReadonlySet<string> = new Set(["camera", "hudAnchors"]);

type TSavedState = Readonly<Record<string, Readonly<Record<string, unknown>>>>;

const signalsOf = (store: TStore, slice: (typeof SLICES)[number]) => {
  return Object.entries(store[slice]) as [string, Signal<unknown>][];
};

const readStoreState = (store: TStore): TSavedState => {
  return Object.fromEntries(
    SLICES.map((slice) => [slice, Object.fromEntries(signalsOf(store, slice).map(([key, item]) => [key, item.peek()]))]),
  );
};

/** A signal the snapshot does not know keeps its value, so an older file still loads. */
const writeStoreState = (store: TStore, saved: TSavedState) => {
  for (const slice of SLICES) {
    const savedSlice = saved[slice] ?? {};

    for (const [key, item] of signalsOf(store, slice)) {
      if (key in savedSlice && !SCREEN_KEYS.has(key)) {
        item.value = savedSlice[key];
      }
    }
  }
};

/**
 * An animation runs on timers, and the riot modal waits with a callback. A file
 * cannot hold either, so a game restored at such a moment would never move on.
 */
const findSaveRefusal = (store: TStore) => {
  const stage = store.game.stage.peek();
  const isAnimating = store.ui.busy.peek()
    || stage === "starting"
    || stage === "entering"
    || store.game.slot.peek().status === "spinning"
    || store.ui.mainMenu.peek() === "leaving";

  if (isAnimating) {
    return "Wait for the animation to end";
  }

  return store.battle.riot.peek() ? "Close the riot modal first" : null;
};

const readError = async (response: Response) => {
  const body = (await response.json().catch(() => null)) as { error?: string } | null;

  return body?.error ?? `The dev server answered ${response.status}. The command works only on the dev server`;
};

const saveStoreSnapshotAction = async (store: TStore) => {
  const refusal = findSaveRefusal(store);
  if (refusal) {
    throw new Error(`The store snapshot was not saved. ${refusal}`);
  }

  const response = await fetch(SNAPSHOT_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(encodeSnapshot(readStoreState(store))),
  });

  if (!response.ok) {
    throw new Error(await readError(response));
  }

  const { path } = (await response.json()) as { path: string };
  console.info(`Store snapshot saved: ${path}`);
};

/**
 * Replaces the running game with the snapshot. The timers of the old game stop
 * first, because they would otherwise write into the restored state.
 */
const applyStoreSnapshot = (store: TStore, saved: TSavedState) => {
  batch(() => {
    clearSetupTimers();
    clearMenuFade();
    clearTaxState(store);
    clearRiotState(store);
    resetSlot(store);
    resetReadiness(store);

    writeStoreState(store, saved);

    // The pointer is somewhere else now, and the timer of this fade has gone.
    store.ui.hoveredHexId.value = null;
    store.ui.hoverAnchor.value = null;
    store.ui.soilCleanseFx.value = null;
  });

  writeRouteToAddress(store);
  resumeRivals(store);

  // The timer that clears the notice has gone too.
  const notice = store.ui.notice.peek();
  if (notice !== null) {
    showNotice(store, notice);
  }
};

const loadStoreSnapshotAction = async (store: TStore) => {
  const response = await fetch(SNAPSHOT_URL);

  if (!response.ok) {
    throw new Error(await readError(response));
  }

  applyStoreSnapshot(store, decodeSnapshot(await response.json()) as TSavedState);
  console.info("Store snapshot loaded");
};

export { loadStoreSnapshotAction, saveStoreSnapshotAction };
