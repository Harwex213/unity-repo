import { createLevel, joinAnnexed } from "../core/cleanup-level";
import {
  castSkill,
  cleanupResult,
  createCleanup,
  retreatCleanup,
  setCleanupInput,
  skillRefusal,
  stepCleanup,
  summarizeCleanup,
} from "../core/cleanup-sim";
import { createRng, hashSeed } from "../core/rng";
import { pendingIslands } from "../core/world-rules";
import { repairIsland, structureHp, withStructureHp } from "../core/structure-hp";
import { buildRoster, getUnit } from "../core/units";
import { BOSS_TECH_ID } from "../core/techs";
import { getCell } from "../core/world-gen";
import { replacePlayer, withIslandHexes } from "./player-updates";
import { showNotice } from "./ui-actions";
import type { TCleanupResult, TSkillTarget } from "../core/cleanup-sim";
import type { TSkillId } from "../core/skills";
import type { TPlayer } from "../core/types";
import type { TStore } from "../store/store";
import type { TCleanupSpeed } from "../store/battle-state";

/**
 * The cleanup phase. The sim is pure and lives in `core/cleanup-sim.ts`. The
 * canvas owns the frame loop and asks for whole ticks through
 * `stepCleanupAction`. These actions build the level, feed it input and hand
 * its result back to the island.
 */

/** A frame never runs more ticks than this, so a slow frame cannot snowball. */
const MAX_TICKS_PER_CALL = 600;

let lastHudKey = "";

const publishHud = (store: TStore) => {
  const sim = store.battle.sim.peek();
  if (!sim) {
    return;
  }

  const hud = summarizeCleanup(sim);
  const key = JSON.stringify(hud);
  if (key !== lastHudKey) {
    lastHudKey = key;
    store.battle.hud.value = hud;
  }

  if (sim.status !== "running" && !store.battle.result.peek()) {
    store.battle.result.value = cleanupResult(sim);
  }
};

/** Builds the level from the cell the island is over and levies the army. */
const enterClearingAction = (store: TStore) => {
  const player = store.derived.humanPlayer.peek();
  if (!player) {
    return;
  }

  const cell = store.derived.currentCell.peek();
  const turn = store.game.turn.peek();
  const nickname = store.game.nickname.peek();
  const rng = createRng(hashSeed(`${nickname}:cleanup:${turn}`));
  const roster = buildRoster(player.resources.population, store.derived.techEffects.peek().unlockedUnits, rng);
  const level = createLevel(
    {
      player,
      turn,
      islandCount: pendingIslands(cell, player),
      cellBiome: cell?.biome ?? "swamp",
      toxicTrail: cell?.toxicTrail ?? 0,
      roster,
      boss: cell?.boss === true,
    },
    rng,
  );

  lastHudKey = "";
  store.battle.roster.value = roster;
  store.battle.result.value = null;
  store.battle.resolved.value = false;
  store.battle.paused.value = false;
  store.battle.speed.value = 1;
  store.battle.targeting.value = null;
  store.battle.sim.value = createCleanup(level, hashSeed(`${nickname}:cleanup-sim:${turn}`));
  publishHud(store);
};

/** The steering of the island, each axis in -1..1. */
const setCleanupInputAction = (store: TStore, x: number, y: number) => {
  const sim = store.battle.sim.peek();
  // A ready player waits for the rivals: the island no longer takes the helm.
  // The canvas still sends a stop (0, 0) when it unmounts.
  if (sim && (!store.derived.isHumanReady.peek() || (x === 0 && y === 0))) {
    setCleanupInput(sim, x, y);
  }
};

const stepCleanupAction = (store: TStore, ticks: number) => {
  const sim = store.battle.sim.peek();
  if (!sim) {
    return;
  }

  const count = Math.min(MAX_TICKS_PER_CALL, Math.max(0, Math.floor(ticks)));
  for (let index = 0; index < count && sim.status === "running"; index += 1) {
    stepCleanup(sim);
  }

  if (sim.status !== "running") {
    store.battle.targeting.value = null;
  }

  publishHud(store);
};

/**
 * Picks a skill for targeting, or drops it when it is picked already. A skill
 * that cannot be cast now says why and is not picked.
 */
const armSkillAction = (store: TStore, skillId: TSkillId) => {
  const sim = store.battle.sim.peek();
  if (!sim || store.derived.isHumanReady.peek()) {
    return;
  }

  if (store.battle.targeting.peek() === skillId) {
    store.battle.targeting.value = null;

    return;
  }

  const refusal = skillRefusal(sim, skillId);
  if (refusal) {
    showNotice(store, refusal);

    return;
  }

  store.battle.targeting.value = skillId;
};

const cancelSkillAction = (store: TStore) => {
  store.battle.targeting.value = null;
};

/** Casts the picked skill at a hex. A refused cast keeps the skill picked and says why. */
const castSkillAction = (store: TStore, target: TSkillTarget) => {
  const sim = store.battle.sim.peek();
  const skillId = store.battle.targeting.peek();
  if (!sim || !skillId || store.derived.isHumanReady.peek()) {
    return;
  }

  const refusal = castSkill(sim, skillId, target);
  if (refusal) {
    showNotice(store, refusal);

    return;
  }

  store.battle.targeting.value = null;
  publishHud(store);
};

const setCleanupSpeedAction = (store: TStore, speed: TCleanupSpeed) => {
  if (store.derived.isHumanReady.peek()) {
    return;
  }

  store.battle.speed.value = speed;
};

const toggleCleanupPauseAction = (store: TStore) => {
  if (store.derived.isHumanReady.peek()) {
    return;
  }

  store.battle.paused.value = !store.battle.paused.peek();
};

/**
 * The battle's damage on the player's island. A razed building leaves an
 * empty hex; a razed stronghold stays as ruins at 0 hp. Returns the hexes and
 * the ids of strongholds ruined in this battle, which skip this turn's repair.
 */
const applyStructureDamage = (player: TPlayer, result: TCleanupResult) => {
  const byHex = new Map(result.structures.map((entry) => [entry.hexId, entry]));
  const ruinedNow = new Set<string>();

  const hexes = player.island.hexes.map((hex) => {
    const entry = byHex.get(hex.id);
    const state = structureHp(player, hex);
    if (!entry || !state) {
      return hex;
    }

    if (entry.hp <= 0 && state.kind !== "stronghold") {
      const { damaged: _damage, ...rest } = hex;

      return { ...rest, building: null };
    }

    if (entry.hp <= 0 && entry.startHp > 0) {
      ruinedNow.add(hex.id);
    }

    return withStructureHp(hex, state.kind, entry.hp);
  });

  return { hexes, ruinedNow };
};

/** The player has defeated the boss and holds its trophy. */
const withBossSlain = (player: TPlayer): TPlayer => ({
  ...player,
  bossSlain: true,
  techs: player.bossSlain ? player.techs : player.techs + 1,
});

/** The trophy joins the player's researched technologies, once. */
const grantBossTech = (store: TStore) => {
  const researched = store.game.researched.peek();
  if (!researched.includes(BOSS_TECH_ID)) {
    store.game.researched.value = [...researched, BOSS_TECH_ID];
  }
};

/**
 * Hands the result back to the island and closes the turn. The dead cost
 * their people, the battle's damage stays on the buildings, the annexed hexes
 * join the rim with the toxicity they carried, and the cell keeps only the
 * islands that still stand. Every turn, battle or not, ends with the free
 * repair of damaged buildings.
 */
const finishClearingAction = (store: TStore) => {
  const sim = store.battle.sim.peek();
  const player = store.derived.humanPlayer.peek();
  if (!player) {
    store.battle.sim.value = null;

    return;
  }

  let next: TPlayer = player;
  let ruinedNow: ReadonlySet<string> = new Set<string>();

  if (sim && !store.battle.resolved.peek()) {
    retreatCleanup(sim);
    const result = store.battle.result.peek() ?? cleanupResult(sim);
    const peopleLost = result.lost.reduce((sum, unitId) => sum + getUnit(unitId).upkeep, 0);
    const damage = applyStructureDamage(player, result);
    ruinedNow = damage.ruinedNow;
    // Hexes destroyed in battle leave the island; the plumes' poison stays on the rest.
    const destroyed = new Set(result.destroyedHexIds);
    const poison = new Map(result.poisoned.map((entry) => [entry.hexId, entry.toxicity]));
    const damaged = withIslandHexes(
      player,
      damage.hexes
        .filter((hex) => !destroyed.has(hex.id))
        .map((hex) => (poison.has(hex.id) ? { ...hex, toxicity: Math.max(hex.toxicity, poison.get(hex.id) as number) } : hex)),
    );
    // The islands that joined in battle keep their exact battle coordinates.
    const grown = joinAnnexed(damaged.island, result.annexed);

    next = {
      ...withIslandHexes(damaged, grown.hexes),
      army: result.survivors.length,
      resources: {
        ...player.resources,
        population: Math.max(0, player.resources.population - peopleLost),
        mana: Math.max(0, player.resources.mana - result.manaSpent),
      },
    };

    const world = store.world.world.peek();
    const lair = world ? getCell(world, player.cellId) : null;

    // The lair never clears: the boss waits for every other player. Its
    // defeat gives this player the trophy technology instead.
    if (lair?.boss) {
      if (result.totalIslands > 0 && result.clearedIslands >= result.totalIslands) {
        next = withBossSlain(next);
        grantBossTech(store);
        showNotice(store, "Повелитель Мора повержен! Открыт «Центральный конвертер»");
      }
    } else if (world && result.totalIslands > 0) {
      const standing = result.totalIslands - result.clearedIslands;
      store.world.world.value = {
        ...world,
        cells: world.cells.map((cell) => {
          if (cell.id !== player.cellId || cell.kind !== "island") {
            return cell;
          }

          return standing === 0 ? { ...cell, cleared: true, islandCount: 0 } : { ...cell, islandCount: standing };
        }),
      };
    }

    store.battle.resolved.value = true;
  }

  replacePlayer(store, withIslandHexes(next, repairIsland(next, ruinedNow)));

  store.battle.sim.value = null;
  store.battle.hud.value = null;
  store.battle.result.value = null;
  store.battle.targeting.value = null;
};

export {
  armSkillAction,
  cancelSkillAction,
  castSkillAction,
  enterClearingAction,
  finishClearingAction,
  setCleanupInputAction,
  setCleanupSpeedAction,
  stepCleanupAction,
  toggleCleanupPauseAction,
};
