import { averageYieldOn, getBuilding } from "./buildings";
import { distancesFrom, getCell } from "./world-gen";
import type { TRng } from "./rng";
import type { TPlayer } from "./types";
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

  return { ...player, army: Math.max(0, player.army - BOSS_LOSS_ARMY) };
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

export { bossWinChance, fightBoss, HUNT_START_TURN, pickConverterHex, stepToward };
