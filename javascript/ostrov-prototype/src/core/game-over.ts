import { hasConverter } from "./buildings";
import { isRuinedStronghold } from "./structure-hp";
import type { TPlayer } from "./types";

/**
 * How a session ends. The rules are pure, so the turn loop and the tests read
 * the same answer.
 *
 * - Victory. A player who has built the central converter has beaten the
 *   poison and wins. Players who build it in the same turn win together.
 * - Defeat. A player is defeated when their stronghold lies in ruins, or when
 *   they have lost every building they ever had.
 * - Precedence. A player who has built the converter wins, even if the same
 *   turn has ruined them. A rival's victory ends the game in the human's
 *   defeat. A defeated rival is eliminated and the game goes on; once every
 *   rival is eliminated, the human wins by elimination.
 */

type TGameOutcomeKind = "victory" | "defeat" | "shared";

type TGameOutcomeReason = "converter" | "shared-converter" | "elimination" | "ruined" | "no-buildings" | "rival-converter";

type TGameOutcome = {
  readonly kind: TGameOutcomeKind;
  readonly reason: TGameOutcomeReason;
  /** The players who won, the human included when the human won. */
  readonly winnerIds: readonly string[];
  readonly turn: number;
};

type TGameOverCheck = {
  /** The players with `hasHadBuildings` and `eliminated` brought up to date. */
  readonly players: readonly TPlayer[];
  /** `null` while the game goes on. */
  readonly outcome: TGameOutcome | null;
};

const buildingCount = (player: TPlayer) => {
  return player.island.hexes.filter((hex) => hex.building !== null).length;
};

const isStrongholdRuined = (player: TPlayer) => {
  const hex = player.island.hexes.find((candidate) => candidate.id === player.strongholdHexId);

  return hex !== undefined && isRuinedStronghold(player, hex);
};

/** Why the player is defeated, or `null` while the player still stands. */
const defeatReason = (player: TPlayer): "ruined" | "no-buildings" | null => {
  if (isStrongholdRuined(player)) {
    return "ruined";
  }

  if (player.hasHadBuildings && buildingCount(player) === 0) {
    return "no-buildings";
  }

  return null;
};

const checkGameOver = (players: readonly TPlayer[], humanId: string, turn: number): TGameOverCheck => {
  // A building seen once makes its later loss count.
  const seen = players.map((player) => {
    return player.hasHadBuildings || buildingCount(player) === 0 ? player : { ...player, hasHadBuildings: true };
  });

  const active = seen.filter((player) => !player.eliminated);
  const winners = active.filter(hasConverter);
  const human = active.find((player) => player.id === humanId);
  const winnerIds = winners.map((player) => player.id);

  if (human && winnerIds.includes(humanId)) {
    const isShared = winners.length > 1;
    const outcome: TGameOutcome = {
      kind: isShared ? "shared" : "victory",
      reason: isShared ? "shared-converter" : "converter",
      winnerIds,
      turn,
    };

    return { players: seen, outcome };
  }

  const humanDefeat = human ? defeatReason(human) : null;
  if (humanDefeat) {
    return { players: seen, outcome: { kind: "defeat", reason: humanDefeat, winnerIds, turn } };
  }

  if (winners.length > 0) {
    return { players: seen, outcome: { kind: "defeat", reason: "rival-converter", winnerIds, turn } };
  }

  // A defeated rival leaves the game. The game goes on without it.
  const next = seen.map((player) => {
    if (player.id === humanId || player.eliminated || defeatReason(player) === null) {
      return player;
    }

    return { ...player, eliminated: true };
  });

  const rivalsLeft = next.filter((player) => player.id !== humanId && !player.eliminated);
  const hadRivals = next.some((player) => player.id !== humanId);
  if (hadRivals && rivalsLeft.length === 0) {
    return { players: next, outcome: { kind: "victory", reason: "elimination", winnerIds: [humanId], turn } };
  }

  return { players: next, outcome: null };
};

export type { TGameOutcome, TGameOutcomeKind, TGameOutcomeReason, TGameOverCheck };
export { buildingCount, checkGameOver, defeatReason };
