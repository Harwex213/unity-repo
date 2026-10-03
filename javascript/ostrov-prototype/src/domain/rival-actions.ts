import { canAfford, effectiveCost, getBuilding, hasConverter } from "../core/buildings";
import { createRng, hashSeed } from "../core/rng";
import { fightBoss, HUNT_START_TURN, pickConverterHex, stepToward } from "../core/rival-ai";
import { findBossCell, getCell } from "../core/world-gen";
import { replacePlayer } from "./player-updates";
import { showNotice } from "./ui-actions";
import type { TPlayer } from "../core/types";
import type { TStore } from "../store/store";

/**
 * The rivals' side of the endgame, one step per phase: the flight to the
 * boss's lair in the scout phase, the fight with the boss in the clearing
 * phase, and the central converter in the build phase. The plan itself lives
 * in `core/rival-ai.ts`. An eliminated rival does nothing.
 */

const findRival = (store: TStore, rivalId: string): TPlayer | null => {
  const player = store.game.players.peek().find((candidate) => candidate.id === rivalId);

  return player && !player.isHuman && !player.eliminated ? player : null;
};

/** The build phase: a rival with the trophy builds the converter once it can pay. */
const playRivalBuild = (store: TStore, rivalId: string) => {
  const rival = findRival(store, rivalId);
  if (!rival || !rival.bossSlain || hasConverter(rival)) {
    return;
  }

  const converter = getBuilding("converter");
  // The rivals have no technologies of their own, so no discount either.
  if (!canAfford(rival.resources, converter, 0)) {
    return;
  }

  const hexId = pickConverterHex(rival);
  if (!hexId) {
    return;
  }

  const cost = effectiveCost(converter, 0);
  replacePlayer(store, {
    ...rival,
    resources: {
      ...rival.resources,
      stone: rival.resources.stone - cost.stone,
      wood: rival.resources.wood - cost.wood,
      hammers: rival.resources.hammers - cost.hammers,
    },
    island: {
      hexes: rival.island.hexes.map((hex) => {
        if (hex.id !== hexId) {
          return hex;
        }

        const { damaged: _damage, ...rest } = hex;

        return { ...rest, building: converter.id };
      }),
    },
  });
  showNotice(store, `${rival.nickname} строит «${converter.label}»`);
};

/** The scout phase: one flight toward the lair, once the hunt has started. */
const playRivalScout = (store: TStore, rivalId: string) => {
  const rival = findRival(store, rivalId);
  const world = store.world.world.peek();
  const lair = world ? findBossCell(world) : null;
  if (!rival || !world || !lair || store.game.turn.peek() < HUNT_START_TURN) {
    return;
  }

  // A rival with the trophy leaves the lair free for the others. Before
  // that, it flies toward the lair. The lair may be taken by another
  // island; the rival then waits.
  const here = getCell(world, rival.cellId);
  const nextId = rival.bossSlain
    ? (here?.id === lair.id ? here.neighbors.find((id) => getCell(world, id)?.ownerId === null) ?? null : null)
    : stepToward(world, rival.cellId, lair.id);
  if (!nextId) {
    return;
  }

  store.world.world.value = {
    ...world,
    cells: world.cells.map((cell) => {
      if (cell.id === rival.cellId) {
        return { ...cell, ownerId: null };
      }

      return cell.id === nextId ? { ...cell, ownerId: rival.id } : cell;
    }),
  };
  replacePlayer(store, { ...rival, cellId: nextId });
};

/** The clearing phase: a rival in the lair fights the boss. */
const playRivalClear = (store: TStore, rivalId: string) => {
  const rival = findRival(store, rivalId);
  const world = store.world.world.peek();
  const lair = world ? findBossCell(world) : null;
  if (!rival || !lair || rival.bossSlain || rival.cellId !== lair.id) {
    return;
  }

  const rng = createRng(hashSeed(`${store.game.nickname.peek()}:boss:${store.game.turn.peek()}:${rival.id}`));
  const next = fightBoss(rival, rng);
  replacePlayer(store, next);

  if (next.bossSlain) {
    showNotice(store, `${rival.nickname} победил Повелителя Мора`);
  }
};

export { playRivalBuild, playRivalClear, playRivalScout };
