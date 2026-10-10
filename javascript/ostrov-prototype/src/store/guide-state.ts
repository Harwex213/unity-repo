import { signal } from "@preact/signals-react";
import type { TGuideStepId } from "../core/guide";

/**
 * The learn guide. It lives for the session: a new game does not show the
 * cards again.
 */
const createGuideState = () => ({
  /** Set at boot from the `?guide` flag. Off means the guide never shows. */
  enabled: signal<boolean>(false),
  /** The card on screen, or `null` when the guide is closed. */
  openStep: signal<TGuideStepId | null>(null),
  /** The cards the player has already seen. Each one opens by itself only once. */
  seen: signal<readonly TGuideStepId[]>([]),
  /** The guide paused the battle to show its card, so closing it resumes the battle. */
  pausedBattle: signal<boolean>(false),
});

type TGuideState = ReturnType<typeof createGuideState>;

export type { TGuideState };
export { createGuideState };
