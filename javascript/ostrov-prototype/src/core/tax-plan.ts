import { getBuilding, hasConverter } from "./buildings";
import { hexDie } from "./dice";
import { createRng, hashSeed, randomInt } from "./rng";
import { isStaffed } from "./population";
import { STRONGHOLD_ALWAYS_YIELDS } from "./stronghold";
import { DEAD_TOXICITY_PCT, facePayout, isDead, isFoodBlocked, withFeeding } from "./tax";
import { withMeterGain } from "./toxic-slot";
import type { TRng } from "./rng";
import type { TPayoutEffects } from "./tax";
import type { TTechEffects } from "./techs";
import type { TBuildingId, TFace, THex, TPlayer, TResourceId } from "./types";

/**
 * The interactive tax phase. At the start of the phase every die on an island
 * is rolled once, and the rolls are kept in a plan. While the phase lasts the
 * player may spend power (власть) to swap a rolled face for another face of the
 * same die. Nothing is paid until the phase ends: then the plan is collected in
 * one go, the power is spent, the toxicity fills the island's meter, and then
 * everyone eats: the mad and the population (see `withFeeding` in `core/tax.ts`).
 *
 * Manual launch. A building with `manualLaunch` (the village) does not roll at
 * the start of the phase. Its owner launches it by hand, once per phase: the
 * launch rolls its die and adds the roll to the plan. Until then the building
 * has no roll, so it pays nothing and leaves no toxicity. After the launch its
 * roll is like any other: power can change the face.
 *
 * Costs. Swapping to a base face costs 1 power, swapping to the biome face
 * costs 2: the biome face is the one the hex is built for. Going back to the
 * rolled face is free. A building carries one choice at a time, so a second
 * pick on the same building replaces the first one and its power is refunded.
 * The power spent is never stored: it is read from the plan, so it cannot
 * drift away from the choices.
 */

const POWER_FACE_COST = 1;
const POWER_BIOME_FACE_COST = 2;
/** For a bot: one percent of hex toxicity is worth a quarter of a resource. */
const BOT_TOXICITY_WEIGHT = 0.25;

type TTaxRoll = {
  readonly hexId: string;
  /** What stood on the hex at the roll. A plan never pays a die that changed. */
  readonly dieKey: TBuildingId | "stronghold";
  readonly faces: readonly TFace[];
  readonly biomeFaceIndex: number;
  readonly rolledIndex: number;
  /** The face the phase will pay. Equals `rolledIndex` until power changes it. */
  readonly chosenIndex: number;
};

type TTaxPlan = {
  readonly playerId: string;
  readonly turn: number;
  readonly rolls: readonly TTaxRoll[];
};

/** Resources a payout can carry. Toxicity travels separately, on the hex. */
type TPayoutResourceId = Exclude<TResourceId, "mad">;

/** One line of the collection: what one hex pays and what it leaves behind. */
type TTaxPayout = {
  readonly hexId: string;
  readonly resource: TPayoutResourceId;
  readonly amount: number;
  /** Percent added to the hex's own toxicity. */
  readonly toxicity: number;
};

type TPickRefusalCode = "no-roll" | "dead" | "food-blocked" | "power";

type TPickRefusal = {
  readonly code: TPickRefusalCode;
  readonly message: string;
};

type TLaunchRefusalCode = "no-launch" | "no-plan" | "unstaffed" | "launched";

type TLaunchRefusal = {
  readonly code: TLaunchRefusalCode;
  readonly message: string;
};

const findHex = (player: TPlayer, hexId: string): THex | null => {
  return player.island.hexes.find((hex) => hex.id === hexId) ?? null;
};

const dieKeyOf = (player: TPlayer, hex: THex): TBuildingId | "stronghold" | null => {
  if (player.strongholdHexId === hex.id) {
    return "stronghold";
  }

  return hex.building;
};

/** The building on the hex waits for its owner to launch it instead of rolling. */
const needsLaunch = (hex: THex) => {
  return hex.building !== null && getBuilding(hex.building).manualLaunch === true;
};

/** One roll of the hex's die, or `null` when the hex has no working die. */
const rollHex = (player: TPlayer, hex: THex, rng: TRng): TTaxRoll | null => {
  const die = hexDie(player, hex);
  const dieKey = dieKeyOf(player, hex);
  // A building without its workers stands still: no roll, no yield, no toxicity.
  if (!die || !dieKey || die.faces.length === 0 || !isStaffed(hex)) {
    return null;
  }

  const rolledIndex = randomInt(rng, 0, die.faces.length - 1);

  return {
    hexId: hex.id,
    dieKey,
    faces: die.faces,
    biomeFaceIndex: die.biomeFaceIndex,
    rolledIndex,
    chosenIndex: rolledIndex,
  };
};

/**
 * Rolls every working die on the island once. A building that needs a launch
 * is skipped: its owner rolls it later (see `withLaunch`). The rolls are seeded by the world seed,
 * the turn and the player, so a turn always rolls the same, and opening the
 * phase twice cannot re-roll it.
 */
const rollTaxPlan = (player: TPlayer, turn: number, seed: string): TTaxPlan => {
  const rng = createRng(hashSeed(`${seed}:tax:${turn}:${player.id}`));
  const rolls: TTaxRoll[] = [];

  for (const hex of player.island.hexes) {
    const roll = needsLaunch(hex) ? null : rollHex(player, hex, rng);
    if (roll) {
      rolls.push(roll);
    }
  }

  return { playerId: player.id, turn, rolls };
};

const findRoll = (plan: TTaxPlan | null, hexId: string): TTaxRoll | null => {
  if (!plan) {
    return null;
  }

  return plan.rolls.find((roll) => roll.hexId === hexId) ?? null;
};

/**
 * Why the building on `hexId` cannot be launched now, or `null` when it can.
 * A launch is free, needs the building's workers, and happens once per phase.
 */
const launchRefusal = (player: TPlayer, plan: TTaxPlan | null, hexId: string): TLaunchRefusal | null => {
  const hex = findHex(player, hexId);
  if (!hex || !needsLaunch(hex)) {
    return { code: "no-launch", message: "Это здание не запускают вручную" };
  }

  if (!plan) {
    return { code: "no-plan", message: "Запустить можно только в фазе сбора ресурсов" };
  }

  if (findRoll(plan, hexId)) {
    return { code: "launched", message: "Уже запущена в этой фазе" };
  }

  if (!isStaffed(hex)) {
    return { code: "unstaffed", message: "Нет рабочего: назначьте его в фазе строительства" };
  }

  return null;
};

/**
 * The building waits for its launch: it could be launched right now. The
 * plates and the tooltip mark it.
 */
const awaitsLaunch = (player: TPlayer, plan: TTaxPlan | null, hex: THex) => {
  return plan !== null && needsLaunch(hex) && launchRefusal(player, plan, hex.id) === null;
};

/**
 * The plan with the building on `hexId` launched: its die rolls once and the
 * roll joins the plan. The roll is seeded by the world seed, the turn, the
 * player and the hex, so a launch always rolls the same. The caller checks
 * `launchRefusal` first.
 */
const withLaunch = (player: TPlayer, plan: TTaxPlan, hexId: string, seed: string): TTaxPlan => {
  const hex = findHex(player, hexId);
  const rng = createRng(hashSeed(`${seed}:launch:${plan.turn}:${player.id}:${hexId}`));
  const roll = hex ? rollHex(player, hex, rng) : null;

  return roll ? { ...plan, rolls: [...plan.rolls, roll] } : plan;
};

/** The power a face costs against the roll. The rolled face is always free. */
const pickCost = (roll: TTaxRoll, index: number) => {
  if (index === roll.rolledIndex) {
    return 0;
  }

  return index === roll.biomeFaceIndex ? POWER_BIOME_FACE_COST : POWER_FACE_COST;
};

const planPowerSpent = (plan: TTaxPlan | null) => {
  if (!plan) {
    return 0;
  }

  return plan.rolls.reduce((sum, roll) => sum + pickCost(roll, roll.chosenIndex), 0);
};

/** The power still free to spend, after the choices already in the plan. */
const powerLeft = (player: TPlayer, plan: TTaxPlan | null) => {
  return player.resources.power - planPowerSpent(plan);
};

/**
 * Whether the face at `index` can become the paid face of the die on `hexId`.
 * Returns `null` when it can. Going back to the rolled face, or picking the
 * face already chosen, is always allowed. The power already spent on this
 * building counts as free, because a new pick refunds it.
 */
const pickRefusal = (player: TPlayer, plan: TTaxPlan | null, hexId: string, index: number): TPickRefusal | null => {
  const roll = findRoll(plan, hexId);
  const hex = findHex(player, hexId);
  const face = roll?.faces[index];
  if (!roll || !hex || !face) {
    return { code: "no-roll", message: "На этом гексе нечего бросать" };
  }

  if (index === roll.chosenIndex || index === roll.rolledIndex) {
    return null;
  }

  if (isDead(hex)) {
    return { code: "dead", message: `Гекс мёртв (${DEAD_TOXICITY_PCT}%): любая грань даст 0` };
  }

  if (face.resource === "food" && isFoodBlocked(hex)) {
    return { code: "food-blocked", message: "Токсичность выше 50%: еда здесь не вырастет" };
  }

  const cost = pickCost(roll, index);
  const available = powerLeft(player, plan) + pickCost(roll, roll.chosenIndex);
  if (available < cost) {
    return { code: "power", message: `Не хватает власти: нужно ${cost}, есть ${Math.max(0, available)}` };
  }

  return null;
};

/** The plan with one die paying another face. The caller checks the refusal. */
const withPick = (plan: TTaxPlan, hexId: string, index: number): TTaxPlan => ({
  ...plan,
  rolls: plan.rolls.map((roll) => (roll.hexId === hexId ? { ...roll, chosenIndex: index } : roll)),
});

/**
 * Everything the plan pays, hex by hex, with the stronghold's power income as
 * its own line. A roll whose die is gone or changed pays nothing.
 */
const planPayouts = (player: TPlayer, plan: TTaxPlan, effects: TPayoutEffects): readonly TTaxPayout[] => {
  const payouts: TTaxPayout[] = [];
  // The central converter turns the toxicity of every building of its owner off.
  const isConverted = hasConverter(player);

  for (const roll of plan.rolls) {
    const hex = findHex(player, roll.hexId);
    const face = roll.faces[roll.chosenIndex];
    if (!hex || !face || dieKeyOf(player, hex) !== roll.dieKey) {
      continue;
    }

    const paid = facePayout(face, hex, effects);
    payouts.push({ hexId: hex.id, resource: face.resource, amount: paid.amount, toxicity: isConverted ? 0 : paid.toxicity });

    if (roll.dieKey === "stronghold") {
      for (const item of STRONGHOLD_ALWAYS_YIELDS) {
        payouts.push({ hexId: hex.id, resource: item.resource, amount: item.amount, toxicity: 0 });
      }
    }
  }

  return payouts;
};

/** Takes the plan's power off the pool. Done once, when the phase ends. */
const withPowerSpent = (player: TPlayer, plan: TTaxPlan): TPlayer => ({
  ...player,
  resources: {
    ...player.resources,
    power: Math.max(0, player.resources.power - planPowerSpent(plan)),
  },
});

/** Adds a paid amount to the pool. */
const withYield = (player: TPlayer, resource: TPayoutResourceId, amount: number): TPlayer => ({
  ...player,
  resources: { ...player.resources, [resource]: player.resources[resource] + amount },
});

/** Dirties one hex, never past the dead mark. */
const withHexToxicity = (player: TPlayer, hexId: string, pct: number): TPlayer => {
  if (pct === 0) {
    return player;
  }

  const hexes = player.island.hexes.map((hex) => {
    return hex.id === hexId ? { ...hex, toxicity: Math.min(DEAD_TOXICITY_PCT, hex.toxicity + pct) } : hex;
  });

  return { ...player, island: { hexes } };
};

/**
 * The toxicity half of a payout: the hex gets dirtier, and the same percent
 * fills the island's meter.
 */
const withToxicityPaid = (player: TPlayer, hexId: string, pct: number): TPlayer => {
  return withMeterGain(withHexToxicity(player, hexId, pct), pct);
};

/**
 * Pays one payout line. The tax animation pays the same line in two halves,
 * the yield and the toxicity, as their motes land.
 */
const withPayout = (player: TPlayer, payout: TTaxPayout): TPlayer => {
  return withToxicityPaid(withYield(player, payout.resource, payout.amount), payout.hexId, payout.toxicity);
};

/**
 * The whole end of the tax phase in one pure step: power spent, every payout,
 * then everyone eats. The food paid this phase is in the pool before the
 * eating. The bots collect through this; the player's animation runs the same
 * three steps one mote at a time.
 */
const collectTaxPlan = (player: TPlayer, plan: TTaxPlan, effects: TTechEffects): TPlayer => {
  const paid = planPayouts(player, plan, effects).reduce(withPayout, withPowerSpent(player, plan));

  return withFeeding(paid, effects.madCuredPerTurn);
};

/** The food the plan pays this phase, before anyone eats. */
const planFoodIncome = (player: TPlayer, plan: TTaxPlan | null, effects: TPayoutEffects) => {
  if (!plan) {
    return 0;
  }

  return planPayouts(player, plan, effects)
    .filter((payout) => payout.resource === "food")
    .reduce((sum, payout) => sum + payout.amount, 0);
};

/**
 * A bot's picks: it swaps the rolls that gain the most per power first, while
 * its power lasts. A face is worth its payout minus a quarter per percent of
 * toxicity it leaves.
 */
const withBotPicks = (player: TPlayer, plan: TTaxPlan, effects: TPayoutEffects): TTaxPlan => {
  const worth = (face: TFace, hex: THex) => {
    const paid = facePayout(face, hex, effects);

    return paid.amount - paid.toxicity * BOT_TOXICITY_WEIGHT;
  };

  const candidates = plan.rolls.flatMap((roll) => {
    const hex = findHex(player, roll.hexId);
    const rolled = roll.faces[roll.rolledIndex];
    if (!hex || !rolled || isDead(hex)) {
      return [];
    }

    let bestIndex = roll.rolledIndex;
    let bestGain = 0;

    roll.faces.forEach((face, index) => {
      const gain = worth(face, hex) - worth(rolled, hex);
      if (gain > bestGain) {
        bestGain = gain;
        bestIndex = index;
      }
    });

    if (bestIndex === roll.rolledIndex) {
      return [];
    }

    return [{ hexId: roll.hexId, index: bestIndex, ratio: bestGain / pickCost(roll, bestIndex) }];
  });

  candidates.sort((left, right) => right.ratio - left.ratio);

  return candidates.reduce((current, candidate) => {
    if (pickRefusal(player, current, candidate.hexId, candidate.index) !== null) {
      return current;
    }

    return withPick(current, candidate.hexId, candidate.index);
  }, plan);
};

export type {
  TLaunchRefusal,
  TLaunchRefusalCode,
  TPayoutResourceId,
  TPickRefusal,
  TPickRefusalCode,
  TTaxPayout,
  TTaxPlan,
  TTaxRoll,
};
export {
  awaitsLaunch,
  collectTaxPlan,
  findRoll,
  launchRefusal,
  needsLaunch,
  pickCost,
  pickRefusal,
  planFoodIncome,
  planPayouts,
  planPowerSpent,
  POWER_BIOME_FACE_COST,
  POWER_FACE_COST,
  powerLeft,
  rollTaxPlan,
  withBotPicks,
  withHexToxicity,
  withLaunch,
  withPayout,
  withPick,
  withPowerSpent,
  withToxicityPaid,
  withYield,
};
