import type { THex, TPlayer } from "./types";

/**
 * The population model. Every person of `resources.population` is in exactly
 * one of three states:
 *
 * - in the army: `player.army` people. They fight in the clearing phase.
 * - in a building: `hex.workers` people on a built hex. A building works only
 *   when it has all the workers it needs. A building without them rolls no die
 *   in the tax phase: it pays nothing and leaves no toxicity.
 * - idle: everyone else. Idle people are never stored; they are the remainder.
 *
 * So `total = idle + army + workers`. When the population drops below
 * `army + workers` (deaths, madness), the buildings lose workers first, then
 * the army loses people. The stronghold needs no workers.
 */

/** People one building needs to work. */
const WORKERS_PER_BUILDING = 1;

/** People the hex needs: a building needs workers, an empty hex does not. */
const workersNeeded = (hex: THex) => {
  return hex.building === null ? 0 : WORKERS_PER_BUILDING;
};

/** People who really work on the hex. Workers of a removed building count as 0. */
const workersOn = (hex: THex) => {
  return Math.min(workersNeeded(hex), Math.max(0, hex.workers ?? 0));
};

/** A building works when all its workers are in place. An empty hex counts as staffed. */
const isStaffed = (hex: THex) => {
  return workersOn(hex) >= workersNeeded(hex);
};

type TPopulation = {
  readonly total: number;
  readonly army: number;
  /** People in buildings. */
  readonly workers: number;
  readonly idle: number;
};

const populationOf = (player: TPlayer): TPopulation => {
  const workers = player.island.hexes.reduce((sum, hex) => sum + workersOn(hex), 0);
  const total = player.resources.population;

  return {
    total,
    army: player.army,
    workers,
    idle: Math.max(0, total - player.army - workers),
  };
};

/** The hex with exactly `count` workers. Nothing changes if the count is already right. */
const withHexWorkers = (hex: THex, count: number): THex => {
  if ((hex.workers ?? 0) === count) {
    return hex;
  }

  if (count === 0) {
    const { workers: _workers, ...rest } = hex;

    return rest;
  }

  return { ...hex, workers: count };
};

/**
 * Restores the rule `army + workers <= total`. A hex without a building loses
 * its stale workers. On a shortage the buildings release people from the last
 * hex up, then the army does. Returns the same player when nothing is wrong.
 */
const settlePopulation = (player: TPlayer): TPlayer => {
  const total = Math.max(0, player.resources.population);
  let army = Math.max(0, Math.min(player.army, total));
  let changed = total !== player.resources.population || army !== player.army;
  const hexes = player.island.hexes.map((hex) => {
    const next = withHexWorkers(hex, workersOn(hex));
    changed = changed || next !== hex;

    return next;
  });

  let excess = army + hexes.reduce((sum, hex) => sum + workersOn(hex), 0) - total;
  for (let index = hexes.length - 1; index >= 0 && excess > 0; index -= 1) {
    const hex = hexes[index] as THex;
    const released = Math.min(excess, workersOn(hex));
    if (released > 0) {
      hexes[index] = withHexWorkers(hex, workersOn(hex) - released);
      excess -= released;
      changed = true;
    }
  }

  if (excess > 0) {
    army -= excess;
    changed = true;
  }

  if (!changed) {
    return player;
  }

  return {
    ...player,
    army,
    resources: { ...player.resources, population: total },
    island: { hexes },
  };
};

/** `count` people die. Idle people die first, then workers, then soldiers. */
const withPeopleLost = (player: TPlayer, count: number): TPlayer => {
  const lost = Math.min(Math.max(0, count), player.resources.population);

  return settlePopulation({
    ...player,
    resources: { ...player.resources, population: player.resources.population - lost },
  });
};

/** `count` soldiers die. They leave the army and the population together. */
const withSoldiersLost = (player: TPlayer, count: number): TPlayer => {
  const lost = Math.min(Math.max(0, count), player.army);

  return settlePopulation({
    ...player,
    army: player.army - lost,
    resources: { ...player.resources, population: player.resources.population - lost },
  });
};

/**
 * Why the hex cannot get `count` workers, or `null` when it can. Only idle
 * people can be assigned.
 */
const staffRefusal = (player: TPlayer, hexId: string, count: number): string | null => {
  const hex = player.island.hexes.find((candidate) => candidate.id === hexId);
  if (!hex || hex.building === null) {
    return "Здесь нет здания";
  }

  if (count < 0 || count > workersNeeded(hex)) {
    return `Зданию нужно рабочих: ${workersNeeded(hex)}`;
  }

  if (count - workersOn(hex) > populationOf(player).idle) {
    return "Нет свободных жителей";
  }

  return null;
};

/** The hex with `count` workers. The caller checks `staffRefusal` first. */
const withStaff = (player: TPlayer, hexId: string, count: number): TPlayer => {
  return settlePopulation({
    ...player,
    island: {
      hexes: player.island.hexes.map((hex) => (hex.id === hexId ? withHexWorkers(hex, count) : hex)),
    },
  });
};

/** Idle people fill the buildings that lack workers, in hex order. */
const withAutoStaff = (player: TPlayer): TPlayer => {
  let idle = populationOf(player).idle;
  if (idle <= 0) {
    return player;
  }

  const hexes = player.island.hexes.map((hex) => {
    const missing = workersNeeded(hex) - workersOn(hex);
    const added = Math.min(missing, idle);
    if (added <= 0) {
      return hex;
    }

    idle -= added;

    return withHexWorkers(hex, workersOn(hex) + added);
  });

  return settlePopulation({ ...player, island: { hexes } });
};

/** The army with `size` people. It takes only idle people and never drops below 0. */
const withArmySize = (player: TPlayer, size: number): TPlayer => {
  const population = populationOf(player);
  const army = Math.max(0, Math.min(size, population.army + population.idle));

  return army === player.army ? player : { ...player, army };
};

export type { TPopulation };
export {
  isStaffed,
  populationOf,
  settlePopulation,
  staffRefusal,
  withArmySize,
  withAutoStaff,
  withPeopleLost,
  withSoldiersLost,
  withStaff,
  workersNeeded,
  workersOn,
  WORKERS_PER_BUILDING,
};
