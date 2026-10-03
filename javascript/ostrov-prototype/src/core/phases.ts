import { ICONS } from "./icons";
import type { TPhase } from "./types";

/**
 * The four phases of the core loop, in the order the spec lists them. The
 * end-turn wheel draws them in this order, the turn panel names them, and the
 * turn counter walks them.
 */

type TPhaseMeta = {
  readonly id: TPhase;
  readonly label: string;
  readonly short: string;
  readonly icon: string;
};

const PHASES: readonly TPhaseMeta[] = [
  { id: "build", label: "Фаза строительства", short: "Строительство", icon: ICONS.hammers },
  { id: "tax", label: "Фаза сбора налогов", short: "Налоги", icon: ICONS.food },
  { id: "scout", label: "Фаза разведки", short: "Разведка", icon: ICONS.scouting },
  { id: "clear", label: "Фаза зачистки", short: "Зачистка", icon: ICONS.army },
];

/**
 * The phases in which the player may switch between the own island and the
 * world map. The tax phase stays on the island: its motes fly to the island's
 * HUD. The clearing phase owns the battle page.
 */
const isPageSwitchPhase = (id: TPhase) => id === "build" || id === "scout";

const phaseIndex = (id: TPhase) => PHASES.findIndex((phase) => phase.id === id);

const getPhase = (id: TPhase) => {
  const phase = PHASES.find((candidate) => candidate.id === id);
  if (!phase) {
    throw new Error(`Unknown phase: ${id}`);
  }

  return phase;
};

export type { TPhaseMeta };
export { getPhase, isPageSwitchPhase, phaseIndex, PHASES };
