import { GUIDE_STEPS, guideStepIndex } from "../core/guide";
import type { TGuideStepId } from "../core/guide";
import type { TStore } from "../store/store";

/**
 * The battle runs in real time, so a guide card over it pauses the battle.
 * Closing the guide resumes the battle only if the guide paused it.
 */
const pauseBattleForGuide = (store: TStore) => {
  const isBattleRunning = store.route.page.peek() === "battle"
    && store.battle.sim.peek() !== null
    && store.battle.result.peek() === null;

  if (!isBattleRunning || store.battle.paused.peek()) {
    return;
  }

  store.battle.paused.value = true;
  store.guide.pausedBattle.value = true;
};

const openGuideStep = (store: TStore, stepId: TGuideStepId) => {
  const seen = store.guide.seen.peek();
  if (!seen.includes(stepId)) {
    store.guide.seen.value = [...seen, stepId];
  }

  store.guide.openStep.value = stepId;
  pauseBattleForGuide(store);
};

const closeGuide = (store: TStore) => {
  store.guide.openStep.value = null;

  if (store.guide.pausedBattle.peek()) {
    store.guide.pausedBattle.value = false;
    if (store.battle.sim.peek() !== null) {
      store.battle.paused.value = false;
    }
  }
};

/** Boot only: the `?guide` flag of the address turns the guide on. */
const enableGuide = (store: TStore, enabled: boolean) => {
  store.guide.enabled.value = enabled;
};

/**
 * Opens the card of the current page and phase, once. The guide UI calls it
 * whenever that card changes. A phase reached out of order still shows its own
 * card: the skipped cards are not forced on the player.
 */
const reachGuideStepAction = (store: TStore) => {
  const stepId = store.derived.guideContextStep.peek();
  if (!stepId || store.guide.openStep.peek() !== null || store.guide.seen.peek().includes(stepId)) {
    return;
  }

  openGuideStep(store, stepId);
};

/**
 * Goes to the next card if the player has seen it already or its phase is on
 * screen now. Otherwise the guide closes, and the next card opens by itself
 * when its phase arrives.
 */
const nextGuideStepAction = (store: TStore) => {
  const current = store.guide.openStep.peek();
  if (!current) {
    return;
  }

  const next = GUIDE_STEPS[guideStepIndex(current) + 1];
  const isNextAvailable = next !== undefined
    && (store.guide.seen.peek().includes(next.id) || store.derived.guideContextStep.peek() === next.id);

  if (isNextAvailable) {
    openGuideStep(store, next.id);

    return;
  }

  closeGuide(store);
};

/** Opens the closest earlier card the player has already seen. */
const prevGuideStepAction = (store: TStore) => {
  const current = store.guide.openStep.peek();
  if (!current) {
    return;
  }

  const seen = store.guide.seen.peek();
  const previous = GUIDE_STEPS
    .slice(0, guideStepIndex(current))
    .reverse()
    .find((step) => seen.includes(step.id));

  if (previous) {
    openGuideStep(store, previous.id);
  }
};

/** "Пропустить" turns the guide off for the rest of the session. */
const skipGuideAction = (store: TStore) => {
  closeGuide(store);
  store.guide.enabled.value = false;
};

export { enableGuide, nextGuideStepAction, prevGuideStepAction, reachGuideStepAction, skipGuideAction };
