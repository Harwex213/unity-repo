import { hexId, hexNeighbors } from "./hex";
import { populationOf, settlePopulation, withPeopleLost, withSoldiersLost } from "./population";
import { structureHp, withStructureHp } from "./structure-hp";
import { DEAD_TOXICITY_PCT, isStarving } from "./tax";
import type { TRng } from "./rng";
import type { TBuildingId, THex, TPlayer } from "./types";

/**
 * The starvation riot. A tax phase that could not feed everyone leaves the
 * player starving (`starvingShortfall`, see `core/tax.ts`). At the start of the
 * next clearing phase the hungry rise:
 *
 * - The rioters are all the mad and part of the residents: one resident per
 *   `RIOT_SHORTFALL_PER_RESIDENT` food missing. Idle people join first, then
 *   workers. Soldiers never riot.
 * - Soldiers meet the mob first, one soldier against one rioter. A soldier wins
 *   a duel with `RIOT_SOLDIER_WIN_CHANCE`. The loser of a duel dies. The mad
 *   die before the residents.
 * - The survivors walk across the island from a random building, hex by hex
 *   in breadth-first order. Every rioter carries `RIOT_DAMAGE_PER_RIOTER` damage
 *   into one shared pool. Each building on the way takes damage from the pool
 *   until the pool is empty. A building at 0 hp is razed, and its hex burns:
 *   `RIOT_FIRE_TOXICITY` more toxicity.
 * - The stronghold is never attacked. The last standing building is never
 *   razed: it keeps 1 hp, so a riot alone never ends the game.
 *
 * The riot ends the starvation: `starvingShortfall` is removed. The surviving
 * rioters go back to normal life; the mad stay mad.
 */

/** Food missing per one resident who joins the riot. */
const RIOT_SHORTFALL_PER_RESIDENT = 2;
/** The chance that a soldier wins a duel against one rioter. */
const RIOT_SOLDIER_WIN_CHANCE = 0.65;
/** Damage one surviving rioter adds to the mob's pool. */
const RIOT_DAMAGE_PER_RIOTER = 30;
/** Toxicity in percent a razed building leaves on its hex. */
const RIOT_FIRE_TOXICITY = 10;

type TRiotSize = {
  readonly mad: number;
  readonly residents: number;
  readonly total: number;
};

/** One building the mob reached. */
type TRiotHit = {
  readonly hexId: string;
  readonly building: TBuildingId;
  readonly hpBefore: number;
  readonly hpAfter: number;
  readonly max: number;
  readonly razed: boolean;
};

type TRiotReport = {
  readonly playerId: string;
  readonly shortfall: number;
  readonly rioters: TRiotSize;
  readonly soldiersKilled: number;
  readonly madKilled: number;
  readonly residentsKilled: number;
  /** The building where the mob started, or `null` when the island had none. */
  readonly startHexId: string | null;
  readonly hits: readonly TRiotHit[];
};

/** Who riots now: all the mad and one resident per 2 food missing. Zero when the player is fed. */
const riotSize = (player: TPlayer): TRiotSize => {
  if (!isStarving(player)) {
    return { mad: 0, residents: 0, total: 0 };
  }

  const people = populationOf(player);
  const residents = Math.min(people.idle + people.workers, Math.ceil((player.starvingShortfall ?? 0) / RIOT_SHORTFALL_PER_RESIDENT));
  const mad = Math.max(0, player.resources.mad);

  return { mad, residents, total: mad + residents };
};

const isTarget = (player: TPlayer, hex: THex) => {
  return hex.building !== null && hex.id !== player.strongholdHexId;
};

/** The mob's path: every hex of the island in breadth-first order from `start`. */
const riotPath = (hexes: readonly THex[], start: THex): readonly THex[] => {
  const byId = new Map(hexes.map((hex) => [hex.id, hex]));
  const seen = new Set<string>([start.id]);
  const path: THex[] = [];
  const queue: THex[] = [start];

  while (queue.length > 0) {
    const hex = queue.shift() as THex;
    path.push(hex);

    for (const next of hexNeighbors(hex.q, hex.r)) {
      const neighbor = byId.get(hexId(next.q, next.r));
      if (neighbor && !seen.has(neighbor.id)) {
        seen.add(neighbor.id);
        queue.push(neighbor);
      }
    }
  }

  return path;
};

/** The mob walks the island and spends its damage pool on the buildings on its way. */
const walkRiot = (player: TPlayer, rioters: number, rng: TRng) => {
  const targets = player.island.hexes.filter((hex) => isTarget(player, hex));
  if (rioters <= 0 || targets.length === 0) {
    return { hexes: player.island.hexes, hits: [] as TRiotHit[], startHexId: null };
  }

  const start = targets[Math.floor(rng() * targets.length)] as THex;
  const changed = new Map<string, THex>();
  const hits: TRiotHit[] = [];
  let pool = rioters * RIOT_DAMAGE_PER_RIOTER;
  // Every building counts here, the converter too: losing all of them is a defeat.
  let standing = player.island.hexes.filter((hex) => hex.building !== null).length;

  for (const hex of riotPath(player.island.hexes, start)) {
    if (pool <= 0) {
      break;
    }

    const state = isTarget(player, hex) ? structureHp(player, hex) : null;
    if (!state || state.kind === "stronghold") {
      continue;
    }

    const isLast = standing <= 1;
    const damage = Math.min(pool, isLast ? state.hp - 1 : state.hp);
    if (damage <= 0) {
      continue;
    }

    pool -= damage;
    const hpAfter = state.hp - damage;
    const razed = hpAfter <= 0;
    hits.push({ hexId: hex.id, building: state.kind, hpBefore: state.hp, hpAfter, max: state.max, razed });

    if (razed) {
      standing -= 1;
      const { damaged: _damage, ...rest } = hex;
      changed.set(hex.id, { ...rest, building: null, toxicity: Math.min(DEAD_TOXICITY_PCT, hex.toxicity + RIOT_FIRE_TOXICITY) });
    } else {
      changed.set(hex.id, withStructureHp(hex, state.kind, hpAfter));
    }
  }

  return {
    hexes: player.island.hexes.map((hex) => changed.get(hex.id) ?? hex),
    hits,
    startHexId: start.id,
  };
};

/**
 * The whole riot in one pure step. A player who is not starving comes back
 * unchanged with an empty report.
 */
const resolveRiot = (player: TPlayer, rng: TRng): { readonly player: TPlayer; readonly report: TRiotReport } => {
  const size = riotSize(player);
  const shortfall = player.starvingShortfall ?? 0;

  // Duels: soldiers against rioters, one pair at a time.
  const engaged = Math.min(player.army, size.total);
  let soldiersKilled = 0;
  let rioterKilled = 0;
  for (let index = 0; index < engaged; index += 1) {
    if (rng() < RIOT_SOLDIER_WIN_CHANCE) {
      rioterKilled += 1;
    } else {
      soldiersKilled += 1;
    }
  }

  const madKilled = Math.min(size.mad, rioterKilled);
  const residentsKilled = rioterKilled - madKilled;
  const walk = walkRiot(player, size.total - rioterKilled, rng);

  // The riot ends the starvation, whatever it did.
  const { starvingShortfall: _shortfall, ...fed } = player;
  const afterDuels = withSoldiersLost({ ...fed, island: { hexes: walk.hexes } }, soldiersKilled);
  const withoutMad: TPlayer = {
    ...afterDuels,
    resources: { ...afterDuels.resources, mad: Math.max(0, afterDuels.resources.mad - madKilled) },
  };
  // The workers of razed buildings become idle here: `settlePopulation` drops them.
  const next = settlePopulation(withPeopleLost(withoutMad, residentsKilled));

  return {
    player: next,
    report: {
      playerId: player.id,
      shortfall,
      rioters: size,
      soldiersKilled,
      madKilled,
      residentsKilled,
      startHexId: walk.startHexId,
      hits: walk.hits,
    },
  };
};

export type { TRiotHit, TRiotReport, TRiotSize };
export {
  resolveRiot,
  RIOT_DAMAGE_PER_RIOTER,
  RIOT_FIRE_TOXICITY,
  RIOT_SHORTFALL_PER_RESIDENT,
  RIOT_SOLDIER_WIN_CHANCE,
  riotPath,
  riotSize,
};
