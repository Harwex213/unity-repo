import { averageYieldOn, getBuilding } from "./buildings";
import { withSoldiersLost, workersOn } from "./population";
import { FOOD_PER_IDLE, FOOD_PER_SOLDIER, FOOD_PER_WORKER, foodDemand } from "./tax";
import { awaitsLaunch, planFoodIncome, planPayouts, withLaunch } from "./tax-plan";
import { distancesFrom, getCell } from "./world-gen";
import type { TRng } from "./rng";
import type { TTaxPlan } from "./tax-plan";
import type { TTechEffects } from "./techs";
import type { THex, TPlayer } from "./types";
import type { TWorld } from "./world-gen";

/**
 * The rivals' minimal plan for the endgame. A rival does not scout or fight
 * the way the player does. From `HUNT_START_TURN` on, it flies one cell per
 * turn toward the boss's lair: it senses the lair, so it needs no scouting.
 * In the lair its fight is resolved by a seeded roll that its army improves.
 * With the trophy in hand it builds the central converter as soon as it can
 * pay for it.
 */

/** The rivals build up first and set out for the lair on this turn. */
const HUNT_START_TURN = 4;
const BOSS_WIN_BASE_CHANCE = 0.3;
const BOSS_WIN_CHANCE_PER_ARMY = 0.025;
const BOSS_WIN_MAX_CHANCE = 0.85;
/** The army a rival loses in a lost fight with the boss. */
const BOSS_LOSS_ARMY = 2;

/**
 * The neighbour of `fromId` that lies on a shortest flight to `targetId`.
 * Cells with another island in them are skipped. Returns `null` when the
 * island is already there or every way is blocked.
 */
const stepToward = (world: TWorld, fromId: string, targetId: string): string | null => {
  const from = getCell(world, fromId);
  const target = getCell(world, targetId);
  if (!from || !target || from.id === target.id) {
    return null;
  }

  const distances = distancesFrom(world.cells, target.index);
  let best: string | null = null;
  let bestDistance = Number.MAX_SAFE_INTEGER;

  for (const neighborId of from.neighbors) {
    const neighbor = getCell(world, neighborId);
    if (!neighbor || neighbor.ownerId !== null) {
      continue;
    }

    const distance = distances[neighbor.index] ?? -1;
    if (distance >= 0 && distance < bestDistance) {
      best = neighbor.id;
      bestDistance = distance;
    }
  }

  return best;
};

/** The chance that a rival with this army defeats the boss in one fight. */
const bossWinChance = (army: number) => {
  return Math.min(BOSS_WIN_MAX_CHANCE, BOSS_WIN_BASE_CHANCE + army * BOSS_WIN_CHANCE_PER_ARMY);
};

/** One seeded fight of a rival against the boss. */
const fightBoss = (player: TPlayer, rng: TRng): TPlayer => {
  if (rng() < bossWinChance(player.army)) {
    return { ...player, bossSlain: true, techs: player.techs + 1 };
  }

  return withSoldiersLost(player, BOSS_LOSS_ARMY);
};

/**
 * Where a rival puts its converter: the cleanest free hex. An island with no
 * free hex gives up its weakest building. The stronghold is never touched.
 */
const pickConverterHex = (player: TPlayer): string | null => {
  const hexes = player.island.hexes.filter((hex) => hex.id !== player.strongholdHexId);
  const free = hexes.filter((hex) => hex.building === null);
  if (free.length > 0) {
    return [...free].sort((left, right) => left.toxicity - right.toxicity || left.id.localeCompare(right.id))[0]?.id ?? null;
  }

  const worth = (hexId: string) => {
    const hex = hexes.find((candidate) => candidate.id === hexId);

    return hex?.building ? averageYieldOn(getBuilding(hex.building), hex.biome) : 0;
  };

  return [...hexes].sort((left, right) => worth(left.id) - worth(right.id) || left.id.localeCompare(right.id))[0]?.id ?? null;
};

/**
 * The tax phase: a rival launches every village that waits for a launch. A
 * launch is free and a village only adds people, and idle people eat no food,
 * so launching never hurts a rival.
 */
const withRivalLaunches = (player: TPlayer, plan: TTaxPlan, seed: string): TTaxPlan => {
  return player.island.hexes.reduce((current, hex) => {
    return awaitsLaunch(player, current, hex) ? withLaunch(player, current, hex.id, seed) : current;
  }, plan);
};

/**
 * The tax phase, just before a rival is paid: a rival that cannot feed its
 * people sends them home, where they eat nothing (`FOOD_PER_IDLE`).
 *
 * 1. Soldiers go home first: each one saves `FOOD_PER_SOLDIER`.
 * 2. If food is still short, workers leave the buildings whose roll this phase
 *    pays no food, from the last hex up. Each one saves `FOOD_PER_WORKER`. An
 *    unstaffed building rolls no die, so its roll leaves the plan too.
 *
 * The food stock plus this phase's food income sets what the rival can feed.
 * The bots staff their buildings again at the next tax phase (`withAutoStaff`).
 * A shortage that remains (the mad, the farmers) still starves the rival.
 */
const withRivalRations = (
  player: TPlayer,
  plan: TTaxPlan,
  effects: TTechEffects,
): { readonly player: TPlayer; readonly plan: TTaxPlan } => {
  const available = player.resources.food + planFoodIncome(player, plan, effects);
  let missing = foodDemand(player, effects.madCuredPerTurn) - available;
  if (missing <= 0) {
    return { player, plan };
  }

  const disbanded = Math.min(player.army, Math.ceil(missing / (FOOD_PER_SOLDIER - FOOD_PER_IDLE)));
  missing -= disbanded * (FOOD_PER_SOLDIER - FOOD_PER_IDLE);

  const paysFood = new Set(
    planPayouts(player, plan, effects)
      .filter((payout) => payout.resource === "food" && payout.amount > 0)
      .map((payout) => payout.hexId),
  );
  const sentHome = new Set<string>();
  const hexes = [...player.island.hexes];
  for (let index = hexes.length - 1; index >= 0 && missing > 0; index -= 1) {
    const hex = hexes[index] as THex;
    if (workersOn(hex) === 0 || paysFood.has(hex.id)) {
      continue;
    }

    missing -= workersOn(hex) * (FOOD_PER_WORKER - FOOD_PER_IDLE);
    sentHome.add(hex.id);
    const { workers: _workers, ...rest } = hex;
    hexes[index] = rest;
  }

  return {
    player: { ...player, army: player.army - disbanded, island: { hexes } },
    plan: { ...plan, rolls: plan.rolls.filter((roll) => !sentHome.has(roll.hexId)) },
  };
};

export { bossWinChance, fightBoss, HUNT_START_TURN, pickConverterHex, stepToward, withRivalLaunches, withRivalRations };
