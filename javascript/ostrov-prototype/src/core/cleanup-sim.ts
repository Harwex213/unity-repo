import { BRIDGE_GAP, findBridges, HEX_STEP, solveCollisions } from "./cleanup-collision";
import { planAttachment, seamEdges } from "./cleanup-attach";
import {
  alongOf,
  createBorder,
  depthFrom,
  inwardNormal,
  isWindowOpen,
  plumeSideAt,
  stepBorder,
  windowAt,
} from "./cleanup-border";
import { islandExtent } from "./cleanup-level";
import { AXIAL_DIRECTIONS, HEX_SIZE, hexId, hexToPixel } from "./hex";
import { createRng } from "./rng";
import { getBuilding } from "./buildings";
import { STRONGHOLD_HEX_ART, STRONGHOLD_LABEL } from "./stronghold";
import { STRONGHOLD_DEFENSE } from "./structure-hp";
import { getEnemy, getUnit } from "./units";
import { getSkill, SKILLS } from "./skills";
import type { TSeamEdge } from "./cleanup-attach";
import type { TBorder, TBorderSide } from "./cleanup-border";
import type { TBody } from "./cleanup-collision";
import type { TAnnexedHex, TCleanupHex, TCleanupSide, TIslandBehavior, TLevelBounds, TLevelSpec } from "./cleanup-level";
import type { TRng } from "./rng";
import type { TSkillId } from "./skills";
import type { TStructureKind } from "./structure-hp";
import type { TBuildingId } from "./types";
import type { TCombatant, TEnemyId, TProjectile, TUnitId } from "./units";

/**
 * The auto-battle of the cleanup phase. The player steers their island; the
 * units on every island find their own fights.
 *
 * The sim is deterministic: one seed, a fixed tick and a fixed update order
 * give the same battle every time. The state is mutated in place, because a
 * hundred units at 30 ticks a second would otherwise allocate a lot. Every
 * moving thing keeps its position from the previous tick, so the renderer can
 * interpolate between two ticks.
 */

const TICK_HZ = 30;
const TICK_SECONDS = 1 / TICK_HZ;

const PLAYER_ACCELERATION = 420;
const PLAYER_MAX_SPEED = 170;
const PLAYER_DRAG_IDLE = 1.8;
const PLAYER_DRAG_STEERING = 0.5;
const DRIFT_MAX_SPEED = 16;
const DRIFT_WOBBLE = 60;
const APPROACH_SPEED = 34;
const APPROACH_RADIUS = 1100;
/** Islands answer a change of desired velocity at this rate per second. */
const ENEMY_RESPONSE = 0.8;
/** The plumes push an island back inward this hard, per second squared. Stronger than the player's own push. */
const PLUME_PUSH = 560;
/** The share of an island's outward speed that the plumes eat per second. */
const PLUME_DAMP = 6;
/** The plumes poison the player's island in pulses this many seconds apart. */
const PLUME_PULSE_SECONDS = 0.5;
/** Toxicity, in percent, that one pulse adds to a hex in the plumes. */
const PLUME_TOXICITY = 2;
/** Share of max hp that one pulse takes from a unit or a building on such a hex. */
const PLUME_UNIT_DAMAGE = 0.04;
const PLUME_STRUCTURE_DAMAGE = 0.02;

/** Melee reach against a flyer, and a flyer's own reach. */
const FLYER_REACH = 70;
/** A flyer counts as over a hex this close to its centre. */
const HOVER_RADIUS = 60;
const PLAYER_FLYER_AGGRO = 480;
const PLAYER_FLYER_LEASH = 700;
const ENEMY_FLYER_AGGRO = 460;
const ENEMY_FLYER_LEASH = 900;
const FLYER_SEPARATION = 30;
const FLYER_STEERING = 6;
/** Ground monsters chase no farther than this many hex steps from where they stand. */
const ENEMY_CHASE_STEPS = 8;
/** A foreign island this close pulls idle units to the rim facing it. */
const RALLY_GAP = 420;
const RALLY_NODES = 5;
const IDLE_WAIT_MIN = 1.2;
const IDLE_WAIT_SPREAD = 2.4;
const IDLE_PACE = 0.55;
const REGEN_DELAY = 3;
const REGEN_SHARE_PER_SECOND = 0.03;
const DAMAGE_ROLL = 0.3;
const PROJECTILE_SPEED: Readonly<Record<TProjectile, number>> = {
  stone: 420,
  arrow: 560,
  bullet: 1100,
  hex: 320,
  bolt: 760,
};

/** A cleared island waits this long for the player to dock it, then starts to drift off. */
const DRIFT_DELAY = 10;
const DRIFT_AWAY_SPEED = 16;
/** Seconds after clearing when an undocked island is gone for good. */
const LOST_AFTER = 24;
/** How long the attach animation pulls the joining island into its lattice spot. */
const ATTACH_PULL_SECONDS = 0.6;
const LOSS_DELAY = 1.5;
const WIN_DELAY = 1;
/** The event queue is drained by the renderer; this cap guards a stalled one. */
const MAX_EVENTS = 400;

type TSimStatus = "running" | "won" | "lost" | "retreated";
/**
 * active: guarded by monsters. cleared: an empty husk that floats free until
 * the player docks it. attached: it joined the player's island. lost: it
 * drifted off before anyone docked it.
 */
type TIslandState = "active" | "cleared" | "attached" | "lost";

/** How an island joined the player's island, for the attach animation and the seam stitches. */
type TAttachInfo = {
  readonly tick: number;
  /** Its origin relative to the player island's origin after the snap. */
  readonly offsetX: number;
  readonly offsetY: number;
  /** Where it was before the snap, relative to where it landed. */
  readonly pullX: number;
  readonly pullY: number;
  /** Shared edges with the old island, in the player island's frame. */
  readonly seam: readonly TSeamEdge[];
  /** The player island's shape number right after this join, so the renderer knows when its re-bake covers it. */
  readonly shapeAfter: number;
  readonly joined: number;
  readonly dropped: number;
};

type TSimIsland = {
  readonly index: number;
  readonly id: string;
  readonly label: string;
  readonly side: TCleanupSide;
  readonly behavior: TIslandBehavior;
  /** The player's island grows when another island joins it. */
  hexes: TCleanupHex[];
  readonly body: TBody;
  /** The graph node of each hex, by hex index. */
  nodes: number[];
  readonly homeX: number;
  readonly homeY: number;
  readonly wobblePhase: number;
  readonly garrisonTotal: number;
  px: number;
  py: number;
  state: TIslandState;
  stateTick: number;
  attach: TAttachInfo | null;
  /** The hexes' outer edges relative to the body position. */
  extent: { readonly minX: number; readonly maxX: number; readonly minY: number; readonly maxY: number };
  /** Grows by one every time the hex set changes, so the renderer bakes the island again. */
  shape: number;
};

type TSimUnit = {
  readonly id: number;
  readonly side: TCleanupSide;
  readonly kind: TUnitId | TEnemyId | TStructureKind;
  /**
   * The hex index on the player's island for a building or the stronghold,
   * -1 for a unit. A structure never moves; only the stronghold shoots.
   */
  structureHex: number;
  readonly stats: TCombatant;
  readonly maxHp: number;
  readonly damage: number;
  readonly homeIsland: number;
  /** Offset from the hex centre, so units on one hex do not stack. */
  readonly slotX: number;
  readonly slotY: number;
  hp: number;
  alive: boolean;
  /** Ground units: the hex node they stand on. -1 for a flyer. */
  node: number;
  /** The node the unit walks to, or -1. */
  nextNode: number;
  /** Ground units: position relative to the island of `node`. */
  lx: number;
  ly: number;
  x: number;
  y: number;
  px: number;
  py: number;
  vx: number;
  vy: number;
  cooldown: number;
  lastHurtTick: number;
  lastAttackTick: number;
  /** Unit vector toward the last target, for the attack lunge. */
  aimX: number;
  aimY: number;
  idlePath: number[];
  idleWait: number;
  /** Flyers: the node under them, or -1 over water. */
  hoverNode: number;
};

type TSimProjectile = {
  readonly kind: TProjectile;
  readonly side: TCleanupSide;
  readonly targetId: number;
  readonly damage: number;
  readonly fromX: number;
  readonly fromY: number;
  readonly duration: number;
  age: number;
  targetX: number;
  targetY: number;
  x: number;
  y: number;
  px: number;
  py: number;
  done: boolean;
};

type TSimEvent =
  | { readonly type: "hit"; readonly x: number; readonly y: number; readonly amount: number; readonly side: TCleanupSide }
  | { readonly type: "death"; readonly x: number; readonly y: number; readonly icon: string; readonly side: TCleanupSide }
  | { readonly type: "ferry"; readonly x: number; readonly y: number }
  | { readonly type: "razed"; readonly x: number; readonly y: number; readonly hex: number }
  | { readonly type: "cleared"; readonly island: number }
  | { readonly type: "attached"; readonly island: number; readonly joined: number }
  | { readonly type: "lost"; readonly island: number }
  /** A hex of the player's island choked in the border plumes. */
  | { readonly type: "poison"; readonly x: number; readonly y: number }
  /**
   * A hex was destroyed. `lx, ly` is its centre relative to the island origin;
   * `shape` is the island's shape number right after the loss.
   */
  | {
      readonly type: "crumble";
      readonly island: number;
      readonly lx: number;
      readonly ly: number;
      readonly x: number;
      readonly y: number;
      readonly shape: number;
      readonly target: boolean;
    };

type TSimInput = {
  x: number;
  y: number;
};

type TCleanupSim = {
  readonly seed: number;
  readonly tier: number;
  readonly rng: TRng;
  readonly islands: TSimIsland[];
  readonly bodies: TBody[];
  units: TSimUnit[];
  projectiles: TSimProjectile[];
  events: TSimEvent[];
  readonly input: TSimInput;
  tick: number;
  status: TSimStatus;
  /** Tick at which the end condition was first met, or -1. */
  endingTick: number;
  kills: number;
  /** Everything the player fielded, dead or alive. */
  readonly roster: readonly TUnitId[];
  readonly lost: TUnitId[];
  readonly annexed: TAnnexedHex[];
  /* The hex graph. Node n is hex `nodeHex[n]` of island `nodeIsland[n]`. */
  nodeIsland: Int32Array;
  nodeHex: Int32Array;
  nodeX: Float64Array;
  nodeY: Float64Array;
  /** Neighbours inside one island. They change only when an island joins. */
  innerLinks: number[][];
  /** Neighbours across a bridge, rebuilt every tick. */
  bridgeLinks: number[][];
  /** Smallest gap between each pair of islands, rebuilt every tick. */
  gaps: Float64Array;
  /** Hex steps to the nearest foe, per side, rebuilt every tick. -1 is unreachable. */
  flowToEnemy: Int32Array;
  flowToPlayer: Int32Array;
  nextUnitId: number;
  readonly bounds: TLevelBounds;
  /** The plumes, the rocks and the windows along the map border. */
  readonly border: TBorder;
  /** 0..1: how far the player's island has sailed through an open window. 1 is out. */
  exitProgress: number;
  /** True while the player's island sits in the band of an open window. */
  inWindow: boolean;
  /** Nodes of hexes destroyed in battle. They stay in the arrays but are never walked again. */
  nodeGone: Uint8Array;
  /** Ids of the player's hexes destroyed in battle, in the player's own frame. */
  readonly destroyedHexIds: string[];
  /** Toxicity of the player's hexes when the battle began, by hex id. */
  readonly startToxicity: ReadonlyMap<string, number>;
  /** The player's mana: the pool at the start minus what the skills spent. */
  mana: number;
  manaSpent: number;
  /** The tick from which each skill can be cast again. */
  readonly skillReadyTick: Record<TSkillId, number>;
  /** Hp of each structure on the player's island, by hex index. -1 where nothing stands. */
  structureHp: Float64Array;
  /** Structures standing when the battle began. Zero means defeat by ruin cannot happen. */
  structuresAtStart: number;
  /** Structures razed in this battle. */
  razed: number;
};

const hexCenter = (hex: TCleanupHex) => hexToPixel(hex.q, hex.r, HEX_SIZE);

const combatantOf = (kind: TUnitId | TEnemyId, side: TCleanupSide): TCombatant => {
  return side === "player" ? getUnit(kind as TUnitId) : getEnemy(kind as TEnemyId);
};

const roll = (rng: TRng, value: number) => value * (1 - DAMAGE_ROLL / 2 + rng() * DAMAGE_ROLL);

const createBody = (hexes: readonly TCleanupHex[], x: number, y: number, mass: number): TBody => {
  const extent = islandExtent(hexes);
  const localX = new Float64Array(hexes.length);
  const localY = new Float64Array(hexes.length);

  hexes.forEach((hex, index) => {
    const center = hexCenter(hex);
    localX[index] = center.x;
    localY[index] = center.y;
  });

  return {
    x,
    y,
    vx: 0,
    vy: 0,
    invMass: 1 / mass,
    localX,
    localY,
    centerX: extent.cx,
    centerY: extent.cy,
    radius: extent.radius,
    solid: true,
    anchored: false,
  };
};

const extentOf = (body: TBody) => ({
  minX: Math.min(...body.localX) - HEX_SIZE,
  maxX: Math.max(...body.localX) + HEX_SIZE,
  minY: Math.min(...body.localY) - HEX_SIZE,
  maxY: Math.max(...body.localY) + HEX_SIZE,
});

/** The node indices of an island, sorted so that units spread from its middle. */
const spawnNodes = (island: TSimIsland, rng: TRng, preferHex: number) => {
  const origin = preferHex >= 0 ? hexCenter(island.hexes[preferHex] as TCleanupHex) : { x: island.body.centerX, y: island.body.centerY };

  return island.hexes
    .map((hex, index) => {
      const center = hexCenter(hex);

      return { node: island.nodes[index] as number, order: Math.hypot(center.x - origin.x, center.y - origin.y) + rng() * 20 };
    })
    .sort((a, b) => a.order - b.order)
    .map((entry) => entry.node);
};

const addUnit = (
  sim: TCleanupSim,
  kind: TUnitId | TEnemyId,
  side: TCleanupSide,
  homeIsland: number,
  node: number,
  growth: number,
) => {
  const stats = combatantOf(kind, side);
  const id = sim.nextUnitId;
  sim.nextUnitId += 1;
  // A golden-angle spiral: every unit on a hex gets its own spot.
  const angle = id * 2.39996;
  const radius = 10 + (id % 3) * 9;
  const island = sim.islands[homeIsland] as TSimIsland;
  const hexIndex = sim.nodeHex[node] as number;
  const hex = island.hexes[hexIndex] as TCleanupHex;
  const center = hexCenter(hex);
  const slotX = Math.cos(angle) * radius;
  const slotY = Math.sin(angle) * radius * 0.7;
  const lx = center.x + slotX;
  const ly = center.y + slotY;
  const x = island.body.x + lx;
  const y = island.body.y + ly;
  const maxHp = Math.round(stats.hp * growth);

  sim.units.push({
    id,
    side,
    kind,
    structureHex: -1,
    stats,
    maxHp,
    damage: stats.damage * growth,
    homeIsland,
    slotX,
    slotY,
    hp: maxHp,
    alive: true,
    node: stats.flying ? -1 : node,
    nextNode: -1,
    lx,
    ly,
    x,
    y: stats.flying ? y - 10 : y,
    px: x,
    py: y,
    vx: 0,
    vy: 0,
    cooldown: sim.rng() * stats.cooldown,
    lastHurtTick: -1000,
    lastAttackTick: -1000,
    aimX: 1,
    aimY: 0,
    idlePath: [],
    idleWait: sim.rng() * IDLE_WAIT_SPREAD,
    hoverNode: -1,
  });
};

/**
 * A building or the stronghold, as a combatant that never moves. Monsters
 * attack it like a unit. The stronghold shoots back with bolts.
 */
const addStructure = (sim: TCleanupSim, hex: TCleanupHex, index: number) => {
  if (hex.maxHp <= 0) {
    return;
  }

  sim.structureHp[index] = hex.hp;
  if (hex.hp <= 0) {
    return;
  }

  const kind: TStructureKind = hex.stronghold ? "stronghold" : (hex.building as TBuildingId);
  const defends = kind === "stronghold";
  const stats: TCombatant = {
    label: defends ? STRONGHOLD_LABEL : getBuilding(kind as TBuildingId).label,
    icon: defends ? STRONGHOLD_HEX_ART : getBuilding(kind as TBuildingId).hexArt,
    hp: hex.maxHp,
    damage: defends ? STRONGHOLD_DEFENSE.damage : 0,
    cooldown: STRONGHOLD_DEFENSE.cooldown,
    range: defends ? STRONGHOLD_DEFENSE.range : 0,
    speed: 0,
    flying: false,
    projectile: defends ? "bolt" : null,
  };
  const island = sim.islands[0] as TSimIsland;
  const center = hexCenter(hex);
  const id = sim.nextUnitId;
  sim.nextUnitId += 1;
  sim.structuresAtStart += 1;

  sim.units.push({
    id,
    side: "player",
    kind,
    structureHex: index,
    stats,
    maxHp: hex.maxHp,
    damage: stats.damage,
    homeIsland: 0,
    slotX: 0,
    slotY: 0,
    hp: hex.hp,
    alive: true,
    node: island.nodes[index] as number,
    nextNode: -1,
    lx: center.x,
    ly: center.y,
    x: island.body.x + center.x,
    y: island.body.y + center.y,
    px: island.body.x + center.x,
    py: island.body.y + center.y,
    vx: 0,
    vy: 0,
    cooldown: 0,
    lastHurtTick: -1000,
    lastAttackTick: -1000,
    aimX: 1,
    aimY: 0,
    idlePath: [],
    idleWait: 0,
    hoverNode: -1,
  });
};

const createCleanup = (level: TLevelSpec, seed: number): TCleanupSim => {
  const rng = createRng(seed);
  const islands: TSimIsland[] = [];
  let nodeCount = 0;

  level.islands.forEach((spec, index) => {
    const mass = spec.side === "player" ? Math.max(12, spec.hexes.length) * 1.5 : spec.hexes.length;
    const body = createBody(spec.hexes, spec.x, spec.y, mass);
    body.anchored = spec.side === "enemy";

    islands.push({
      index,
      id: spec.id,
      label: spec.label,
      side: spec.side,
      behavior: spec.behavior,
      hexes: [...spec.hexes],
      body,
      nodes: spec.hexes.map((_, hexIndex) => nodeCount + hexIndex),
      homeX: spec.x,
      homeY: spec.y,
      wobblePhase: rng() * Math.PI * 2,
      garrisonTotal: spec.garrison.length,
      px: spec.x,
      py: spec.y,
      state: "active",
      stateTick: 0,
      attach: null,
      extent: extentOf(body),
      shape: 0,
    });
    nodeCount += spec.hexes.length;
  });

  const nodeIsland = new Int32Array(nodeCount);
  const nodeHex = new Int32Array(nodeCount);
  const innerLinks: number[][] = [];

  for (const island of islands) {
    const byKey = new Map(island.hexes.map((hex, index) => [hexId(hex.q, hex.r), index]));

    island.hexes.forEach((hex, index) => {
      const node = island.nodes[index] as number;
      nodeIsland[node] = island.index;
      nodeHex[node] = index;
      innerLinks[node] = AXIAL_DIRECTIONS.map((step) => byKey.get(hexId(hex.q + step.q, hex.r + step.r)))
        .filter((neighbor): neighbor is number => neighbor !== undefined)
        .map((neighbor) => island.nodes[neighbor] as number);
    });
  }

  const sim: TCleanupSim = {
    seed,
    tier: level.tier,
    rng,
    islands,
    bodies: islands.map((island) => island.body),
    units: [],
    projectiles: [],
    events: [],
    input: { x: 0, y: 0 },
    tick: 0,
    status: "running",
    endingTick: -1,
    kills: 0,
    roster: level.roster,
    lost: [],
    annexed: [],
    nodeIsland,
    nodeHex,
    nodeX: new Float64Array(nodeCount),
    nodeY: new Float64Array(nodeCount),
    innerLinks,
    bridgeLinks: Array.from({ length: nodeCount }, () => []),
    gaps: new Float64Array(islands.length * islands.length),
    flowToEnemy: new Int32Array(nodeCount),
    flowToPlayer: new Int32Array(nodeCount),
    nextUnitId: 1,
    bounds: level.bounds,
    border: createBorder(level.bounds, seed, islands[0]?.body.radius ?? 300),
    exitProgress: 0,
    inWindow: false,
    nodeGone: new Uint8Array(nodeCount),
    destroyedHexIds: [],
    startToxicity: new Map((level.islands[0]?.hexes ?? []).map((hex) => [hex.id, hex.toxicity])),
    mana: level.mana,
    manaSpent: 0,
    skillReadyTick: Object.fromEntries(SKILLS.map((skill) => [skill.id, 0])) as Record<TSkillId, number>,
    structureHp: new Float64Array(level.islands[0]?.hexes.length ?? 0).fill(-1),
    structuresAtStart: 0,
    razed: 0,
  };

  const player = islands[0];
  if (player) {
    player.hexes.forEach((hex, index) => {
      addStructure(sim, hex, index);
    });

    const strongholdHex = player.hexes.findIndex((hex) => hex.stronghold);
    const nodes = spawnNodes(player, rng, strongholdHex);
    level.roster.forEach((kind, index) => {
      addUnit(sim, kind, "player", 0, nodes[index % nodes.length] as number, 1);
    });
  }

  for (const island of islands) {
    if (island.side !== "enemy") {
      continue;
    }

    const nodes = spawnNodes(island, rng, -1);
    level.islands[island.index]?.garrison.forEach((kind, index) => {
      addUnit(sim, kind, "enemy", island.index, nodes[index % nodes.length] as number, level.growth);
    });
  }

  refreshWorld(sim);
  stepBorder(sim.border, 0, TICK_HZ, islands[0]?.body.radius ?? 300);

  return sim;
};

const pushEvent = (sim: TCleanupSim, event: TSimEvent) => {
  sim.events.push(event);
  if (sim.events.length > MAX_EVENTS) {
    sim.events.splice(0, sim.events.length - MAX_EVENTS);
  }
};

const islandCenter = (island: TSimIsland) => ({
  x: island.body.x + island.body.centerX,
  y: island.body.y + island.body.centerY,
});

const gapBetween = (sim: TCleanupSim, a: number, b: number) => {
  return sim.gaps[a * sim.islands.length + b] as number;
};

const nodeActive = (sim: TCleanupSim, node: number) => {
  if (sim.nodeGone[node] === 1) {
    return false;
  }

  const island = sim.islands[sim.nodeIsland[node] as number] as TSimIsland;

  return island.state === "active" || island.state === "cleared";
};

/* ---------- islands ---------- */

/** The nearest enemy island that sails at the player. Only one does at a time, so the player is never boxed in. */
const leadApproacher = (sim: TCleanupSim) => {
  const player = sim.islands[0];
  if (!player) {
    return -1;
  }

  const to = islandCenter(player);
  let best = -1;
  let bestDistance = APPROACH_RADIUS;

  for (const island of sim.islands) {
    if (island.side !== "enemy" || island.state !== "active" || island.behavior !== "approach") {
      continue;
    }

    const from = islandCenter(island);
    const distance = Math.hypot(to.x - from.x, to.y - from.y);
    if (distance < bestDistance) {
      best = island.index;
      bestDistance = distance;
    }
  }

  return best;
};

/** An island that joined the player's island or drifted off: it no longer moves on its own. */
const isGone = (island: TSimIsland) => island.state === "attached" || island.state === "lost";

/**
 * The span of the player's island along a side fits inside an open window
 * there: the island may sail past the border on that side.
 */
const fitsWindow = (sim: TCleanupSim, island: TSimIsland, side: TBorderSide) => {
  const body = island.body;
  const low = side % 2 === 0 ? body.x + island.extent.minX : body.y + island.extent.minY;
  const high = side % 2 === 0 ? body.x + island.extent.maxX : body.y + island.extent.maxY;
  const window = windowAt(sim.border, side, (low + high) / 2, sim.tick);

  return window !== null && low >= window.at - window.half && high <= window.at + window.half;
};

/**
 * Keeps every island inside the map. An island that hits the edge stops
 * there. Enemy islands and husks keep out of the plume band altogether, so
 * they never stick in it. The player's island stops at the border line,
 * except where it fits through an open window.
 */
const clampToBounds = (sim: TCleanupSim) => {
  for (const island of sim.islands) {
    if (isGone(island) || island.hexes.length === 0) {
      continue;
    }

    const inset = island.side === "player" ? 0 : sim.border.depth;
    const halfWidth = sim.bounds.halfWidth - inset;
    const halfHeight = sim.bounds.halfHeight - inset;
    const free = (side: TBorderSide) => island.side === "player" && fitsWindow(sim, island, side);
    const body = island.body;
    const left = body.x + island.extent.minX;
    const right = body.x + island.extent.maxX;
    const top = body.y + island.extent.minY;
    const bottom = body.y + island.extent.maxY;

    if (left < -halfWidth && !free(3)) {
      body.x += -halfWidth - left;
      body.vx = Math.max(0, body.vx);
    } else if (right > halfWidth && !free(1)) {
      body.x -= right - halfWidth;
      body.vx = Math.min(0, body.vx);
    }

    if (top < -halfHeight && !free(0)) {
      body.y += -halfHeight - top;
      body.vy = Math.max(0, body.vy);
    } else if (bottom > halfHeight && !free(2)) {
      body.y -= bottom - halfHeight;
      body.vy = Math.min(0, body.vy);
    }
  }
};

/** The rocks of the border are solid: an island that runs into one is pushed back out. */
const solveRocks = (sim: TCleanupSim) => {
  for (const island of sim.islands) {
    if (isGone(island) || !island.body.solid || island.hexes.length === 0) {
      continue;
    }

    const body = island.body;
    const cx = body.x + body.centerX;
    const cy = body.y + body.centerY;

    for (const rock of sim.border.rocks) {
      if (Math.hypot(rock.x - cx, rock.y - cy) > body.radius + rock.radius) {
        continue;
      }

      let depth = 0;
      let normalX = 0;
      let normalY = 0;

      for (let index = 0; index < body.localX.length; index += 1) {
        const dx = body.x + (body.localX[index] as number) - rock.x;
        const dy = body.y + (body.localY[index] as number) - rock.y;
        const distance = Math.hypot(dx, dy) || 1;
        const overlap = rock.radius + HEX_SIZE * 0.85 - distance;
        if (overlap > depth) {
          depth = overlap;
          normalX = dx / distance;
          normalY = dy / distance;
        }
      }

      if (depth <= 0) {
        continue;
      }

      body.x += normalX * depth;
      body.y += normalY * depth;
      const closing = body.vx * normalX + body.vy * normalY;
      if (closing < 0) {
        body.vx -= closing * normalX;
        body.vy -= closing * normalY;
      }
    }
  }
};

/** The hexes of an island that sit in the plumes, with the side whose plumes cover each. */
const hexesInPlumes = (sim: TCleanupSim, island: TSimIsland) => {
  const found: { index: number; side: TBorderSide }[] = [];
  const body = island.body;
  const { halfWidth, halfHeight } = sim.bounds;
  const depth = sim.border.depth + HEX_SIZE;

  // Most of the time the island is far from every edge.
  if (
    body.x + island.extent.minX > -halfWidth + depth &&
    body.x + island.extent.maxX < halfWidth - depth &&
    body.y + island.extent.minY > -halfHeight + depth &&
    body.y + island.extent.maxY < halfHeight - depth
  ) {
    return found;
  }

  for (let index = 0; index < body.localX.length; index += 1) {
    const x = body.x + (body.localX[index] as number);
    const y = body.y + (body.localY[index] as number);
    const side = plumeSideAt(sim.border, sim.bounds, x, y, sim.tick, HEX_SIZE * 0.8);
    if (side >= 0) {
      found.push({ index, side: side as TBorderSide });
    }
  }

  return found;
};

/** The plumes push an island back inward and eat its outward speed. */
const pushOutOfPlumes = (sim: TCleanupSim, dt: number) => {
  for (const island of sim.islands) {
    if (isGone(island) || island.hexes.length === 0) {
      continue;
    }

    const inside = hexesInPlumes(sim, island);
    if (inside.length === 0) {
      continue;
    }

    let pushX = 0;
    let pushY = 0;
    for (const entry of inside) {
      const normal = inwardNormal(entry.side);
      pushX += normal.x;
      pushY += normal.y;
    }

    const length = Math.hypot(pushX, pushY) || 1;
    const nx = pushX / length;
    const ny = pushY / length;
    const body = island.body;
    const along = body.vx * nx + body.vy * ny;
    if (along < 0) {
      const damp = Math.min(1, PLUME_DAMP * dt);
      body.vx -= along * nx * damp;
      body.vy -= along * ny * damp;
    }

    body.vx += nx * PLUME_PUSH * dt;
    body.vy += ny * PLUME_PUSH * dt;
  }
};

const steerIslands = (sim: TCleanupSim, dt: number) => {
  const player = sim.islands[0];
  const approacher = leadApproacher(sim);

  for (const island of sim.islands) {
    const body = island.body;
    island.px = body.x;
    island.py = body.y;

    if (isGone(island)) {
      continue;
    }

    if (island.side === "player") {
      const length = Math.hypot(sim.input.x, sim.input.y);
      const steering = length > 0.01;
      if (steering) {
        body.vx += (sim.input.x / length) * PLAYER_ACCELERATION * dt;
        body.vy += (sim.input.y / length) * PLAYER_ACCELERATION * dt;
      }

      const drag = Math.exp(-(steering ? PLAYER_DRAG_STEERING : PLAYER_DRAG_IDLE) * dt);
      body.vx *= drag;
      body.vy *= drag;
      const speed = Math.hypot(body.vx, body.vy);
      if (speed > PLAYER_MAX_SPEED) {
        body.vx *= PLAYER_MAX_SPEED / speed;
        body.vy *= PLAYER_MAX_SPEED / speed;
      }
    } else {
      let desiredX = 0;
      let desiredY = 0;

      // A docked island holds still, so the fight on the bridge stays put.
      const docked = gapBetween(sim, 0, island.index) < HEX_STEP * 0.2;

      if (island.state === "active" && !docked) {
        const time = sim.tick * TICK_SECONDS * 0.12 + island.wobblePhase;
        const wobbleX = island.homeX + Math.cos(time) * DRIFT_WOBBLE;
        const wobbleY = island.homeY + Math.sin(time * 1.3) * DRIFT_WOBBLE;
        desiredX = (wobbleX - body.x) * 0.2;
        desiredY = (wobbleY - body.y) * 0.2;
        const drift = Math.hypot(desiredX, desiredY);
        if (drift > DRIFT_MAX_SPEED) {
          desiredX *= DRIFT_MAX_SPEED / drift;
          desiredY *= DRIFT_MAX_SPEED / drift;
        }

        if (island.index === approacher && player) {
          const from = islandCenter(island);
          const to = islandCenter(player);
          const dx = to.x - from.x;
          const dy = to.y - from.y;
          const distance = Math.hypot(dx, dy);
          if (distance < APPROACH_RADIUS) {
            desiredX = (dx / distance) * APPROACH_SPEED;
            desiredY = (dy / distance) * APPROACH_SPEED;
          }
        }
      }

      // An undocked husk drifts away from the player after a while.
      const sinceCleared = (sim.tick - island.stateTick) * TICK_SECONDS;
      if (island.state === "cleared" && sinceCleared > DRIFT_DELAY && player) {
        const from = islandCenter(island);
        const to = islandCenter(player);
        const away = Math.hypot(from.x - to.x, from.y - to.y) || 1;
        desiredX = ((from.x - to.x) / away) * DRIFT_AWAY_SPEED;
        desiredY = ((from.y - to.y) / away) * DRIFT_AWAY_SPEED;
      }

      const response = Math.min(1, ENEMY_RESPONSE * dt);
      body.vx += (desiredX - body.vx) * response;
      body.vy += (desiredY - body.vy) * response;
    }

    body.x += body.vx * dt;
    body.y += body.vy * dt;
  }

  pushOutOfPlumes(sim, dt);
  clampToBounds(sim);
  solveCollisions(sim.bodies);
  solveRocks(sim);
  // The solver may push an island back out; the border wins.
  clampToBounds(sim);
};

/** Node positions, bridges and island gaps. */
function refreshWorld(sim: TCleanupSim) {
  const count = sim.nodeIsland.length;

  for (let node = 0; node < count; node += 1) {
    (sim.bridgeLinks[node] as number[]).length = 0;
    if (sim.nodeGone[node] === 1) {
      continue;
    }

    const island = sim.islands[sim.nodeIsland[node] as number] as TSimIsland;
    sim.nodeX[node] = island.body.x + (island.body.localX[sim.nodeHex[node] as number] as number);
    sim.nodeY[node] = island.body.y + (island.body.localY[sim.nodeHex[node] as number] as number);
  }

  const { bridges, distances } = findBridges(sim.bodies);
  sim.gaps.fill(Infinity);

  for (const pair of distances) {
    sim.gaps[pair.a * sim.islands.length + pair.b] = pair.gap;
    sim.gaps[pair.b * sim.islands.length + pair.a] = pair.gap;
  }

  for (const bridge of bridges) {
    const nodeA = (sim.islands[bridge.a] as TSimIsland).nodes[bridge.hexA] as number;
    const nodeB = (sim.islands[bridge.b] as TSimIsland).nodes[bridge.hexB] as number;
    (sim.bridgeLinks[nodeA] as number[]).push(nodeB);
    (sim.bridgeLinks[nodeB] as number[]).push(nodeA);
  }
}

const linksOf = (sim: TCleanupSim, node: number) => {
  const inner = sim.innerLinks[node] as readonly number[];
  const bridged = sim.bridgeLinks[node] as number[];

  return bridged.length === 0 ? inner : [...inner, ...bridged];
};

const areLinked = (sim: TCleanupSim, a: number, b: number) => {
  return a === b || (sim.innerLinks[a] as readonly number[]).includes(b) || (sim.bridgeLinks[a] as number[]).includes(b);
};

/** Multi-source BFS from every node that holds a unit of `goalSide`. */
const buildFlow = (sim: TCleanupSim, goalSide: TCleanupSide, flow: Int32Array) => {
  flow.fill(-1);
  const queue: number[] = [];

  for (const unit of sim.units) {
    if (!unit.alive || unit.side !== goalSide) {
      continue;
    }

    const node = unit.node >= 0 ? unit.node : unit.hoverNode;
    if (node >= 0 && flow[node] === -1 && nodeActive(sim, node)) {
      flow[node] = 0;
      queue.push(node);
    }
  }

  for (let head = 0; head < queue.length; head += 1) {
    const node = queue[head] as number;
    const next = (flow[node] as number) + 1;

    for (const neighbor of linksOf(sim, node)) {
      if (flow[neighbor] === -1 && nodeActive(sim, neighbor)) {
        flow[neighbor] = next;
        queue.push(neighbor);
      }
    }
  }
};

/* ---------- combat ---------- */

const hurt = (sim: TCleanupSim, target: TSimUnit, amount: number) => {
  if (!target.alive) {
    return;
  }

  target.hp -= amount;
  target.lastHurtTick = sim.tick;
  pushEvent(sim, { type: "hit", x: target.x, y: target.y, amount: Math.max(1, Math.round(amount)), side: target.side });

  if (target.structureHex >= 0) {
    sim.structureHp[target.structureHex] = Math.max(0, target.hp);
  }

  if (target.hp > 0) {
    return;
  }

  target.alive = false;
  target.hp = 0;

  if (target.structureHex >= 0) {
    sim.razed += 1;
    pushEvent(sim, { type: "razed", x: target.x, y: target.y, hex: target.structureHex });

    return;
  }

  pushEvent(sim, { type: "death", x: target.x, y: target.y, icon: target.stats.icon, side: target.side });

  if (target.side === "player") {
    sim.lost.push(target.kind as TUnitId);
  } else {
    sim.kills += 1;
  }
};

const attack = (sim: TCleanupSim, unit: TSimUnit, target: TSimUnit) => {
  const dx = target.x - unit.x;
  const dy = target.y - unit.y;
  const distance = Math.hypot(dx, dy) || 1;
  unit.aimX = dx / distance;
  unit.aimY = dy / distance;
  unit.lastAttackTick = sim.tick;
  unit.cooldown = unit.stats.cooldown;
  const damage = roll(sim.rng, unit.damage);

  if (unit.stats.projectile) {
    const speed = PROJECTILE_SPEED[unit.stats.projectile];
    sim.projectiles.push({
      kind: unit.stats.projectile,
      side: unit.side,
      targetId: target.id,
      damage,
      fromX: unit.x,
      fromY: unit.y,
      duration: Math.max(TICK_SECONDS * 2, distance / speed),
      age: 0,
      targetX: target.x,
      targetY: target.y,
      x: unit.x,
      y: unit.y,
      px: unit.x,
      py: unit.y,
      done: false,
    });

    return;
  }

  hurt(sim, target, damage);
};

/** The nearest living foe that `accept` allows, by straight distance. */
const nearestFoe = (sim: TCleanupSim, unit: TSimUnit, maxDistance: number, accept: (foe: TSimUnit) => boolean) => {
  let best: TSimUnit | null = null;
  let bestDistance = maxDistance;

  for (const other of sim.units) {
    if (!other.alive || other.side === unit.side) {
      continue;
    }

    const distance = Math.hypot(other.x - unit.x, other.y - unit.y);
    if (distance <= bestDistance && accept(other)) {
      best = other;
      bestDistance = distance;
    }
  }

  return best;
};

/** A foe a ground unit can hit from where it stands. */
const isStructure = (unit: TSimUnit) => unit.structureHex >= 0;

/**
 * A foe a ground unit can hit from where it stands. Monsters hit units in
 * reach first and turn on buildings only when no unit is in reach.
 */
const targetForGround = (sim: TCleanupSim, unit: TSimUnit) => {
  const inReach = (foe: TSimUnit) => {
    if (unit.stats.range > 1) {
      return true;
    }

    if (foe.node < 0) {
      return Math.hypot(foe.x - unit.x, foe.y - unit.y) <= FLYER_REACH;
    }

    return areLinked(sim, unit.node, foe.node);
  };
  const reach = unit.stats.range > 1 ? unit.stats.range * HEX_STEP : HEX_STEP * 1.9;
  const fighter = nearestFoe(sim, unit, reach, (foe) => !isStructure(foe) && inReach(foe));

  return fighter ?? nearestFoe(sim, unit, reach, (foe) => isStructure(foe) && inReach(foe));
};

/** The stronghold shoots the nearest monster in range. Other buildings only stand. */
const stepStructure = (sim: TCleanupSim, unit: TSimUnit) => {
  if (unit.damage <= 0 || unit.cooldown > 0) {
    return;
  }

  const target = nearestFoe(sim, unit, unit.stats.range * HEX_STEP, () => true);
  if (target) {
    attack(sim, unit, target);
  }
};

/* ---------- ground movement ---------- */

/** Where a unit stands on `node`, in the frame of the island of `frameNode`. */
const slotIn = (sim: TCleanupSim, unit: TSimUnit, node: number, frameNode: number) => {
  const frame = sim.islands[sim.nodeIsland[frameNode] as number] as TSimIsland;

  return {
    x: (sim.nodeX[node] as number) + unit.slotX - frame.body.x,
    y: (sim.nodeY[node] as number) + unit.slotY - frame.body.y,
  };
};

const walk = (sim: TCleanupSim, unit: TSimUnit, pace: number, dt: number) => {
  // A bridge that breaks under a walking unit sends it back to its own hex.
  if (unit.nextNode >= 0 && (!areLinked(sim, unit.node, unit.nextNode) || !nodeActive(sim, unit.nextNode))) {
    unit.nextNode = -1;
    unit.idlePath = [];
  }

  const goalNode = unit.nextNode >= 0 ? unit.nextNode : unit.node;
  const goal = slotIn(sim, unit, goalNode, unit.node);
  const dx = goal.x - unit.lx;
  const dy = goal.y - unit.ly;
  const distance = Math.hypot(dx, dy);
  const step = unit.stats.speed * HEX_STEP * pace * dt;

  if (distance <= step) {
    unit.lx = goal.x;
    unit.ly = goal.y;

    if (unit.nextNode >= 0) {
      const arrived = unit.nextNode;
      const island = sim.islands[sim.nodeIsland[arrived] as number] as TSimIsland;
      unit.node = arrived;
      unit.nextNode = -1;
      unit.lx = (sim.nodeX[arrived] as number) + unit.slotX - island.body.x;
      unit.ly = (sim.nodeY[arrived] as number) + unit.slotY - island.body.y;
    }

    return;
  }

  unit.lx += (dx / distance) * step;
  unit.ly += (dy / distance) * step;
};

/** BFS path from `from` to the first node that `isGoal` accepts. */
const pathTo = (sim: TCleanupSim, from: number, isGoal: (node: number) => boolean, crossBridges: boolean) => {
  const previous = new Map<number, number>([[from, -1]]);
  const queue = [from];

  for (let head = 0; head < queue.length; head += 1) {
    const node = queue[head] as number;
    if (node !== from && isGoal(node)) {
      const path: number[] = [];
      let cursor = node;
      while (cursor !== from) {
        path.unshift(cursor);
        cursor = previous.get(cursor) as number;
      }

      return path;
    }

    const links = crossBridges ? linksOf(sim, node) : (sim.innerLinks[node] as readonly number[]);
    for (const neighbor of links) {
      if (!previous.has(neighbor) && nodeActive(sim, neighbor)) {
        previous.set(neighbor, node);
        queue.push(neighbor);
      }
    }
  }

  return [];
};

/** The foreign island this island's idle units should gather to face. */
const rallyIsland = (sim: TCleanupSim, island: TSimIsland) => {
  let best = -1;
  let bestGap = RALLY_GAP;

  for (const other of sim.islands) {
    if (other.side === island.side || other.state !== "active") {
      continue;
    }

    const gap = gapBetween(sim, island.index, other.index);
    if (gap < bestGap) {
      best = other.index;
      bestGap = gap;
    }
  }

  return best;
};

/** Wandering, gathering on the rim facing a foe, or walking home. */
const idle = (sim: TCleanupSim, unit: TSimUnit, dt: number) => {
  if (unit.nextNode < 0 && unit.idlePath.length > 0) {
    unit.nextNode = unit.idlePath.shift() as number;
  }

  if (unit.nextNode >= 0) {
    walk(sim, unit, IDLE_PACE, dt);

    return;
  }

  walk(sim, unit, IDLE_PACE, dt);
  unit.idleWait -= dt;
  if (unit.idleWait > 0) {
    return;
  }

  unit.idleWait = IDLE_WAIT_MIN + sim.rng() * IDLE_WAIT_SPREAD;
  const hereIsland = sim.nodeIsland[unit.node] as number;
  const home = unit.side === "player" ? 0 : unit.homeIsland;

  if (hereIsland !== home) {
    const homeIsland = sim.islands[home] as TSimIsland;
    if (homeIsland.state === "active" || homeIsland.state === "cleared") {
      unit.idlePath = pathTo(sim, unit.node, (node) => sim.nodeIsland[node] === home, true);
      unit.idleWait = 0.3;
    }

    if (unit.idlePath.length > 0) {
      return;
    }
  }

  const island = sim.islands[hereIsland] as TSimIsland;
  const rally = rallyIsland(sim, island);

  if (rally >= 0) {
    const target = islandCenter(sim.islands[rally] as TSimIsland);
    const facing = island.hexes
      .map((_, index) => island.nodes[index] as number)
      .sort((a, b) => {
        const da = Math.hypot((sim.nodeX[a] as number) - target.x, (sim.nodeY[a] as number) - target.y);
        const db = Math.hypot((sim.nodeX[b] as number) - target.x, (sim.nodeY[b] as number) - target.y);

        return da - db;
      })
      .slice(0, RALLY_NODES);
    const goal = facing[Math.floor(sim.rng() * facing.length)] as number;
    if (goal !== unit.node) {
      unit.idlePath = pathTo(sim, unit.node, (node) => node === goal, false);
    }

    unit.idleWait *= 0.5;

    return;
  }

  // A short stroll: one or two hexes in a random direction.
  const links = sim.innerLinks[unit.node] as readonly number[];
  if (links.length > 0 && sim.rng() < 0.7) {
    const first = links[Math.floor(sim.rng() * links.length)] as number;
    unit.idlePath = [first];
  }
};

const stepGroundUnit = (sim: TCleanupSim, unit: TSimUnit, dt: number) => {
  const onBridge = unit.nextNode >= 0 && sim.nodeIsland[unit.nextNode] !== sim.nodeIsland[unit.node];
  const target = onBridge ? null : targetForGround(sim, unit);

  if (target) {
    // A unit finishes the step it is in before it swings.
    if (unit.nextNode >= 0) {
      walk(sim, unit, 1, dt);
    } else {
      walk(sim, unit, 1, dt);
      if (unit.cooldown <= 0) {
        attack(sim, unit, target);
      }
    }

    unit.idlePath = [];

    return;
  }

  if (unit.nextNode < 0) {
    const flow = unit.side === "player" ? sim.flowToEnemy : sim.flowToPlayer;
    const here = flow[unit.node] as number;
    const chases = here > 0 && (unit.side === "player" || here <= ENEMY_CHASE_STEPS);

    if (chases) {
      const options = linksOf(sim, unit.node).filter((node) => flow[node] === here - 1 && nodeActive(sim, node));
      if (options.length > 0) {
        unit.nextNode = options[(unit.id + Math.floor(sim.tick / TICK_HZ)) % options.length] as number;
        unit.idlePath = [];
      }
    }
  }

  if (unit.nextNode >= 0 && unit.idlePath.length === 0) {
    walk(sim, unit, 1, dt);

    return;
  }

  idle(sim, unit, dt);
};

/* ---------- flyers ---------- */

const hoverNodeAt = (sim: TCleanupSim, x: number, y: number) => {
  let best = -1;
  let bestDistance = HOVER_RADIUS;

  for (const island of sim.islands) {
    if (island.state !== "active" && island.state !== "cleared") {
      continue;
    }

    const center = islandCenter(island);
    if (Math.hypot(center.x - x, center.y - y) > island.body.radius + HOVER_RADIUS) {
      continue;
    }

    for (let index = 0; index < island.hexes.length; index += 1) {
      const node = island.nodes[index] as number;
      const distance = Math.hypot((sim.nodeX[node] as number) - x, (sim.nodeY[node] as number) - y);
      if (distance < bestDistance) {
        best = node;
        bestDistance = distance;
      }
    }
  }

  return best;
};

const flyerTarget = (sim: TCleanupSim, unit: TSimUnit) => {
  if (unit.side === "player") {
    const home = islandCenter(sim.islands[0] as TSimIsland);

    return nearestFoe(sim, unit, PLAYER_FLYER_AGGRO, (foe) => Math.hypot(foe.x - home.x, foe.y - home.y) < PLAYER_FLYER_LEASH);
  }

  const home = islandCenter(sim.islands[unit.homeIsland] as TSimIsland);

  const allowed = (foe: TSimUnit) => {
    const nearFlyer = Math.hypot(foe.x - unit.x, foe.y - unit.y) < ENEMY_FLYER_AGGRO;

    return nearFlyer && Math.hypot(foe.x - home.x, foe.y - home.y) < ENEMY_FLYER_LEASH;
  };

  // A flyer in reach of a unit fights it; otherwise it raids the nearest building.
  const fighter = nearestFoe(sim, unit, FLYER_REACH * 1.5, (foe) => !isStructure(foe) && allowed(foe));

  return fighter ?? nearestFoe(sim, unit, ENEMY_FLYER_AGGRO * 2, allowed);
};

const stepFlyer = (sim: TCleanupSim, unit: TSimUnit, dt: number) => {
  const target = flyerTarget(sim, unit);
  let goalX: number;
  let goalY: number;
  let pace = 1;

  if (target) {
    const dx = target.x - unit.x;
    const dy = target.y - unit.y;
    const distance = Math.hypot(dx, dy) || 1;
    if (distance <= FLYER_REACH && unit.cooldown <= 0) {
      attack(sim, unit, target);
    }

    // It hangs just short of the target instead of sitting on it.
    const standoff = FLYER_REACH * 0.7;
    goalX = target.x - (dx / distance) * standoff;
    goalY = target.y - (dy / distance) * standoff;
  } else {
    const homeIndex = unit.side === "player" ? 0 : unit.homeIsland;
    const homeIsland = sim.islands[homeIndex] as TSimIsland;
    const gone = homeIsland.state === "attached" || homeIsland.state === "lost";
    const home = gone ? islandCenter(sim.islands[0] as TSimIsland) : islandCenter(homeIsland);
    const orbit = 50 + (unit.id % 4) * 18;
    const angle = sim.tick * TICK_SECONDS * 0.6 + unit.id;
    goalX = home.x + Math.cos(angle) * orbit;
    goalY = home.y + Math.sin(angle) * orbit * 0.7;
    pace = 0.6;
  }

  const dx = goalX - unit.x;
  const dy = goalY - unit.y;
  const distance = Math.hypot(dx, dy);
  const maxSpeed = unit.stats.speed * HEX_STEP * pace;
  let desiredX = distance > 1 ? (dx / distance) * Math.min(maxSpeed, distance * 3) : 0;
  let desiredY = distance > 1 ? (dy / distance) * Math.min(maxSpeed, distance * 3) : 0;

  for (const other of sim.units) {
    if (other === unit || !other.alive || other.node >= 0) {
      continue;
    }

    const ox = unit.x - other.x;
    const oy = unit.y - other.y;
    const gap = Math.hypot(ox, oy);
    if (gap > 0 && gap < FLYER_SEPARATION) {
      desiredX += (ox / gap) * maxSpeed * 0.5;
      desiredY += (oy / gap) * maxSpeed * 0.5;
    }
  }

  const response = Math.min(1, FLYER_STEERING * dt);
  unit.vx += (desiredX - unit.vx) * response;
  unit.vy += (desiredY - unit.vy) * response;
  unit.x += unit.vx * dt;
  unit.y += unit.vy * dt;
};

/* ---------- the tick ---------- */

const stepProjectiles = (sim: TCleanupSim, byId: Map<number, TSimUnit>, dt: number) => {
  for (const shot of sim.projectiles) {
    shot.px = shot.x;
    shot.py = shot.y;
    const target = byId.get(shot.targetId);
    if (target && target.alive) {
      shot.targetX = target.x;
      shot.targetY = target.y;
    }

    shot.age += dt;
    const t = Math.min(1, shot.age / shot.duration);
    shot.x = shot.fromX + (shot.targetX - shot.fromX) * t;
    shot.y = shot.fromY + (shot.targetY - shot.fromY) * t;

    if (t >= 1) {
      shot.done = true;
      if (target && target.alive) {
        hurt(sim, target, shot.damage);
      }
    }
  }

  sim.projectiles = sim.projectiles.filter((shot) => !shot.done);
};

const garrisonAlive = (sim: TCleanupSim, island: number) => {
  return sim.units.some((unit) => unit.alive && unit.side === "enemy" && unit.homeIsland === island);
};

/** Carries a unit to a home node, as when a lost island leaves it behind. */
const ferryHome = (sim: TCleanupSim, unit: TSimUnit, node: number) => {
  const player = sim.islands[0] as TSimIsland;
  pushEvent(sim, { type: "ferry", x: unit.x, y: unit.y });
  unit.node = node;
  unit.nextNode = -1;
  unit.idlePath = [];
  const target = slotIn(sim, unit, node, node);
  unit.lx = target.x;
  unit.ly = target.y;
  unit.x = player.body.x + target.x;
  unit.y = player.body.y + target.y;
  unit.px = unit.x;
  unit.py = unit.y;
  pushEvent(sim, { type: "ferry", x: unit.x, y: unit.y });
};

const growArray = <T extends Int32Array | Float64Array>(array: T, length: number, fill: number): T => {
  const next = new (array.constructor as new (size: number) => T)(length);
  next.fill(fill);
  next.set(array);

  return next;
};

/**
 * Joins a cleared island to the player's island. Its hexes snap to the
 * player's lattice at the docking offset and become hexes of the player's
 * island, in the same pattern. Units on them come along. The merged island is
 * one rigid body from now on. Returns false when no hex would connect.
 */
const attachIsland = (sim: TCleanupSim, island: TSimIsland) => {
  const player = sim.islands[0];
  if (!player) {
    return false;
  }

  const dx = island.body.x - player.body.x;
  const dy = island.body.y - player.body.y;
  const plan = planAttachment(player.hexes, island.hexes, dx, dy);
  if (!plan) {
    return false;
  }

  const offset = hexToPixel(plan.offsetQ, plan.offsetR, HEX_SIZE);
  const oldHexes = player.hexes.map((hex) => ({ q: hex.q, r: hex.r }));
  const firstNew = sim.nodeIsland.length;
  const nodeCount = firstNew + plan.kept.length;
  const newNodeOf = new Map<number, number>();

  sim.nodeIsland = growArray(sim.nodeIsland, nodeCount, 0);
  sim.nodeHex = growArray(sim.nodeHex, nodeCount, 0);
  sim.nodeX = growArray(sim.nodeX, nodeCount, 0);
  sim.nodeY = growArray(sim.nodeY, nodeCount, 0);
  const nodeGone = new Uint8Array(nodeCount);
  nodeGone.set(sim.nodeGone);
  sim.nodeGone = nodeGone;
  sim.flowToEnemy = growArray(sim.flowToEnemy, nodeCount, -1);
  sim.flowToPlayer = growArray(sim.flowToPlayer, nodeCount, -1);
  sim.structureHp = growArray(sim.structureHp, player.hexes.length + plan.kept.length, -1);

  plan.kept.forEach((hexIndex, order) => {
    const hex = island.hexes[hexIndex] as TCleanupHex;
    const q = hex.q + plan.offsetQ;
    const r = hex.r + plan.offsetR;
    const node = firstNew + order;
    newNodeOf.set(island.nodes[hexIndex] as number, node);
    sim.nodeIsland[node] = 0;
    sim.nodeHex[node] = player.hexes.length;
    sim.bridgeLinks.push([]);
    sim.innerLinks.push([]);
    player.nodes.push(node);
    sim.annexed.push({ q, r, biome: hex.biome, toxicity: hex.toxicity });
    // A wild island brings no buildings: only its ground joins.
    player.hexes.push({ ...hex, id: hexId(q, r), q, r, building: null, stronghold: false, hp: 0, maxHp: 0 });
  });

  // The neighbours of every player hex, old and new.
  const byKey = new Map(player.hexes.map((hex, index) => [hexId(hex.q, hex.r), index]));
  player.hexes.forEach((hex, index) => {
    sim.innerLinks[player.nodes[index] as number] = AXIAL_DIRECTIONS.map((step) => byKey.get(hexId(hex.q + step.q, hex.r + step.r)))
      .filter((neighbor): neighbor is number => neighbor !== undefined)
      .map((neighbor) => player.nodes[neighbor] as number);
  });

  // One rigid body: the union of hexes, the summed mass and momentum.
  const body = player.body;
  const playerMass = 1 / body.invMass;
  const joinedMass = plan.kept.length * 1.5;
  body.vx = (body.vx * playerMass + island.body.vx * joinedMass) / (playerMass + joinedMass);
  body.vy = (body.vy * playerMass + island.body.vy * joinedMass) / (playerMass + joinedMass);
  body.invMass = 1 / (playerMass + joinedMass);
  body.localX = new Float64Array(player.hexes.map((hex) => hexCenter(hex).x));
  body.localY = new Float64Array(player.hexes.map((hex) => hexCenter(hex).y));
  const extent = islandExtent(player.hexes);
  body.centerX = extent.cx;
  body.centerY = extent.cy;
  body.radius = extent.radius;
  player.extent = extentOf(body);
  player.shape += 1;
  player.nodes.forEach((node, index) => {
    sim.nodeX[node] = body.x + (body.localX[index] as number);
    sim.nodeY[node] = body.y + (body.localY[index] as number);
  });

  // Units on the joining island now stand on the player's island.
  const homeNodes = spawnNodes(player, sim.rng, -1);
  let spare = 0;
  for (const unit of sim.units) {
    if (unit.nextNode >= 0 && sim.nodeIsland[unit.nextNode] === island.index) {
      unit.nextNode = newNodeOf.get(unit.nextNode) ?? -1;
    }

    if (!unit.alive || unit.node < 0 || sim.nodeIsland[unit.node] !== island.index) {
      continue;
    }

    unit.idlePath = [];
    const node = newNodeOf.get(unit.node);
    if (node === undefined) {
      // Its hex clashed and was dropped: the unit is carried onto the island.
      ferryHome(sim, unit, homeNodes[spare % homeNodes.length] as number);
      spare += 1;

      continue;
    }

    unit.node = node;
    unit.lx += offset.x;
    unit.ly += offset.y;
  }

  island.state = "attached";
  island.stateTick = sim.tick;
  island.body.solid = false;
  island.attach = {
    tick: sim.tick,
    offsetX: offset.x,
    offsetY: offset.y,
    pullX: dx - offset.x,
    pullY: dy - offset.y,
    seam: seamEdges(oldHexes, plan.kept.map((index) => {
      const hex = island.hexes[index] as TCleanupHex;

      return { q: hex.q + plan.offsetQ, r: hex.r + plan.offsetR };
    })),
    shapeAfter: player.shape,
    joined: plan.kept.length,
    dropped: plan.dropped,
  };
  pushEvent(sim, { type: "attached", island: island.index, joined: plan.kept.length });

  return true;
};

/**
 * The life of an enemy island after its last monster: it floats free as a
 * husk, joins the player's island the moment the two touch, or drifts off and
 * is lost if nobody docks it in time.
 */
const stepIslandStates = (sim: TCleanupSim) => {
  for (const island of sim.islands) {
    if (island.side !== "enemy") {
      continue;
    }

    if (island.state === "active" && !garrisonAlive(sim, island.index)) {
      island.state = "cleared";
      island.stateTick = sim.tick;
      // A cleared island is a husk: the player's island may shove it aside.
      island.body.anchored = false;
      pushEvent(sim, { type: "cleared", island: island.index });
    }

    if (island.state !== "cleared") {
      continue;
    }

    // Touching the player's island: it joins at once.
    if (gapBetween(sim, 0, island.index) < BRIDGE_GAP && attachIsland(sim, island)) {
      continue;
    }

    if ((sim.tick - island.stateTick) * TICK_SECONDS < LOST_AFTER) {
      continue;
    }

    // Gone for good. Player units still on it are carried home; monsters go with it.
    const player = sim.islands[0];
    const homeNodes = player ? spawnNodes(player, sim.rng, -1) : [];
    let slot = 0;
    for (const unit of sim.units) {
      if (!unit.alive || unit.node < 0 || sim.nodeIsland[unit.node] !== island.index) {
        continue;
      }

      if (unit.side === "player" && homeNodes.length > 0) {
        ferryHome(sim, unit, homeNodes[slot % homeNodes.length] as number);
        slot += 1;
      } else {
        unit.alive = false;
      }
    }

    island.state = "lost";
    island.stateTick = sim.tick;
    island.body.solid = false;
    pushEvent(sim, { type: "lost", island: island.index });
  }
};

const stepStatus = (sim: TCleanupSim) => {
  if (sim.status !== "running") {
    return;
  }

  const enemies = sim.islands.filter((island) => island.side === "enemy");
  // A level with no enemy islands ends at once: there is nothing to clear.
  const allSunk = enemies.every((island) => isGone(island));
  // Defeat is the last building or the stronghold falling. A wiped army alone
  // does not end the battle: the stronghold keeps shooting.
  const islandRuined = sim.structuresAtStart > 0 && !sim.units.some((unit) => unit.alive && isStructure(unit));

  if (!allSunk && !islandRuined) {
    sim.endingTick = -1;

    return;
  }

  if (sim.endingTick < 0) {
    sim.endingTick = sim.tick;
  }

  const waited = (sim.tick - sim.endingTick) * TICK_SECONDS;
  if (islandRuined && waited >= LOSS_DELAY) {
    sim.status = "lost";
  } else if (allSunk && !islandRuined && waited >= WIN_DELAY) {
    sim.status = "won";
  }
};

/**
 * The retreat costs the units that are not home: a ground unit standing on
 * another island stays behind and is lost. Flyers keep up with the island.
 */
const leaveBattle = (sim: TCleanupSim) => {
  for (const unit of sim.units) {
    if (!unit.alive || unit.side !== "player" || unit.node < 0) {
      continue;
    }

    if (sim.nodeIsland[unit.node] !== 0) {
      unit.alive = false;
      unit.hp = 0;
      sim.lost.push(unit.kind as TUnitId);
      pushEvent(sim, { type: "death", x: unit.x, y: unit.y, icon: unit.stats.icon, side: unit.side });
    }
  }

  sim.units = sim.units.filter((unit) => unit.alive);
  sim.status = "retreated";
};

/**
 * The way out. The player's island leaves once its centre has sailed past
 * the border line, which only an open window lets it do. `exitProgress`
 * shows how far through the window band it has come.
 */
const stepExit = (sim: TCleanupSim) => {
  const player = sim.islands[0];
  if (!player || player.hexes.length === 0) {
    return;
  }

  const cx = player.body.x + player.body.centerX;
  const cy = player.body.y + player.body.centerY;
  let progress = 0;
  let inWindow = false;
  let out = false;

  for (const side of [0, 1, 2, 3] as const) {
    const depth = depthFrom(sim.bounds, side, cx, cy);
    if (depth < 0) {
      out = true;
    }

    if (depth >= sim.border.depth || !windowAt(sim.border, side, alongOf(side, cx, cy), sim.tick)) {
      continue;
    }

    inWindow = true;
    progress = Math.max(progress, 1 - Math.max(0, depth) / sim.border.depth);
  }

  sim.inWindow = inWindow;
  sim.exitProgress = out ? 1 : progress;

  if (out && sim.status === "running") {
    leaveBattle(sim);
  }
};

/**
 * The plumes poison the player's island in pulses: each hex in them gains
 * toxicity, and units and buildings on it lose a share of their hp. The
 * island is pushed back out long before this kills anyone.
 */
const stepPlumePoison = (sim: TCleanupSim) => {
  const pulse = Math.round(PLUME_PULSE_SECONDS * TICK_HZ);
  const player = sim.islands[0];
  if (!player || sim.tick % pulse !== 0) {
    return;
  }

  const inside = hexesInPlumes(sim, player);
  if (inside.length === 0) {
    return;
  }

  const poisoned = new Set<number>();
  for (const entry of inside) {
    const hex = player.hexes[entry.index] as TCleanupHex;
    const toxicity = Math.min(100, hex.toxicity + PLUME_TOXICITY);
    player.hexes[entry.index] = { ...hex, toxicity, dead: hex.dead || toxicity >= 100 };
    poisoned.add(player.nodes[entry.index] as number);
    pushEvent(sim, { type: "poison", x: sim.nodeX[player.nodes[entry.index] as number] as number, y: sim.nodeY[player.nodes[entry.index] as number] as number });
  }

  for (const unit of sim.units) {
    if (!unit.alive || unit.side !== "player" || !poisoned.has(unit.node)) {
      continue;
    }

    hurt(sim, unit, unit.maxHp * (isStructure(unit) ? PLUME_STRUCTURE_DAMAGE : PLUME_UNIT_DAMAGE));
  }
};

/* ---------- skills ---------- */

type TSkillTarget = {
  readonly island: number;
  readonly hex: number;
};

/**
 * What a cast at the target would destroy: the hex itself and every piece of
 * its island that loses touch with the core. The core of the player's island
 * is the piece with the stronghold; the core of any other island is its
 * largest piece. Returns the reason instead when the cast is refused.
 */
const shatterPlan = (sim: TCleanupSim, target: TSkillTarget): { readonly removed: readonly number[] } | { readonly refusal: string } => {
  const island = sim.islands[target.island];
  const hex = island?.hexes[target.hex];
  if (!island || !hex || isGone(island) || island.hexes.length === 0) {
    return { refusal: "Здесь нечего разрушать" };
  }

  if (hex.stronghold) {
    return { refusal: "Гекс твердыни разрушить нельзя" };
  }

  const player = sim.islands[0];
  if (player && island.index !== 0) {
    const reach = getSkill("shatter").reach + player.body.radius;
    const dx = (sim.nodeX[island.nodes[target.hex] as number] as number) - (player.body.x + player.body.centerX);
    const dy = (sim.nodeY[island.nodes[target.hex] as number] as number) - (player.body.y + player.body.centerY);
    if (Math.hypot(dx, dy) > reach) {
      return { refusal: "Слишком далеко: подведите остров ближе" };
    }
  }

  // The pieces of the island without the target hex.
  const byKey = new Map(island.hexes.map((entry, index) => [hexId(entry.q, entry.r), index]));
  const pieceOf = new Int32Array(island.hexes.length).fill(-1);
  const pieces: number[][] = [];

  island.hexes.forEach((_, first) => {
    if (first === target.hex || pieceOf[first] !== -1) {
      return;
    }

    const piece = [first];
    pieceOf[first] = pieces.length;
    for (let head = 0; head < piece.length; head += 1) {
      const current = island.hexes[piece[head] as number] as TCleanupHex;
      for (const step of AXIAL_DIRECTIONS) {
        const next = byKey.get(hexId(current.q + step.q, current.r + step.r));
        if (next !== undefined && next !== target.hex && pieceOf[next] === -1) {
          pieceOf[next] = pieces.length;
          piece.push(next);
        }
      }
    }

    pieces.push(piece);
  });

  const strongholdHex = island.hexes.findIndex((entry) => entry.stronghold);
  let core = -1;
  if (island.index === 0 && strongholdHex >= 0) {
    core = pieceOf[strongholdHex] as number;
  } else {
    pieces.forEach((piece, index) => {
      if (core < 0 || piece.length > (pieces[core] as number[]).length) {
        core = index;
      }
    });
  }

  if (island.index === 0 && core < 0) {
    return { refusal: "Это последний гекс вашего острова" };
  }

  const removed = [target.hex];
  pieces.forEach((piece, index) => {
    if (index !== core) {
      removed.push(...piece);
    }
  });

  return { removed };
};

/**
 * Takes hexes out of an island. Their nodes are marked gone; the rest of the
 * island keeps its nodes, renumbered by hex. Monsters on a lost hex fall with
 * it; the player's units are carried home, as from a lost island. A building
 * on a lost hex of the player's island is razed with it.
 */
const removeHexes = (sim: TCleanupSim, island: TSimIsland, removedList: readonly number[], targetHex: number) => {
  const removed = new Set(removedList);
  const keep = island.hexes.map((_, index) => index).filter((index) => !removed.has(index));
  const newIndex = new Map(keep.map((old, index) => [old, index]));
  const goneNodes = new Set(removedList.map((index) => island.nodes[index] as number));
  const shape = island.shape + 1;

  for (const index of removedList) {
    const hex = island.hexes[index] as TCleanupHex;
    const node = island.nodes[index] as number;
    const center = hexCenter(hex);
    sim.nodeGone[node] = 1;
    pushEvent(sim, {
      type: "crumble",
      island: island.index,
      lx: center.x,
      ly: center.y,
      x: sim.nodeX[node] as number,
      y: sim.nodeY[node] as number,
      shape,
      target: index === targetHex,
    });

    if (island.index === 0) {
      sim.destroyedHexIds.push(hex.id);
    }
  }

  for (const node of goneNodes) {
    sim.innerLinks[node] = [];
  }

  for (const node of island.nodes) {
    if (!goneNodes.has(node)) {
      sim.innerLinks[node] = (sim.innerLinks[node] as number[]).filter((link) => !goneNodes.has(link));
    }
  }

  island.hexes = keep.map((index) => island.hexes[index] as TCleanupHex);
  island.nodes = keep.map((index) => island.nodes[index] as number);
  island.nodes.forEach((node, index) => {
    sim.nodeHex[node] = index;
  });
  island.shape = shape;

  const body = island.body;
  body.localX = new Float64Array(island.hexes.map((hex) => hexCenter(hex).x));
  body.localY = new Float64Array(island.hexes.map((hex) => hexCenter(hex).y));
  if (island.hexes.length > 0) {
    const extent = islandExtent(island.hexes);
    body.centerX = extent.cx;
    body.centerY = extent.cy;
    body.radius = extent.radius;
    island.extent = extentOf(body);
  }

  if (island.index === 0) {
    const structureHp = new Float64Array(keep.length);
    keep.forEach((old, index) => {
      structureHp[index] = sim.structureHp[old] as number;
    });
    sim.structureHp = structureHp;
  }

  const home = sim.islands[0];
  const homeNodes = home && home.hexes.length > 0 ? spawnNodes(home, sim.rng, -1) : [];
  let slot = 0;

  for (const unit of sim.units) {
    if (!unit.alive) {
      continue;
    }

    if (isStructure(unit) && island.index === 0) {
      const moved = newIndex.get(unit.structureHex);
      if (moved === undefined) {
        unit.alive = false;
        unit.hp = 0;
        sim.razed += 1;
        pushEvent(sim, { type: "razed", x: unit.x, y: unit.y, hex: unit.structureHex });
      } else {
        unit.structureHex = moved;
      }

      continue;
    }

    if (unit.nextNode >= 0 && goneNodes.has(unit.nextNode)) {
      unit.nextNode = -1;
    }

    if (unit.idlePath.some((node) => goneNodes.has(node))) {
      unit.idlePath = [];
    }

    if (unit.node < 0 || !goneNodes.has(unit.node)) {
      continue;
    }

    if (unit.side === "player" && homeNodes.length > 0) {
      ferryHome(sim, unit, homeNodes[slot % homeNodes.length] as number);
      slot += 1;

      continue;
    }

    unit.alive = false;
    unit.hp = 0;
    pushEvent(sim, { type: "death", x: unit.x, y: unit.y, icon: unit.stats.icon, side: unit.side });
    if (unit.side === "player") {
      sim.lost.push(unit.kind as TUnitId);
    } else {
      sim.kills += 1;
    }
  }

  sim.units = sim.units.filter((unit) => unit.alive);

  if (island.hexes.length === 0) {
    island.state = "lost";
    island.stateTick = sim.tick;
    body.solid = false;
    pushEvent(sim, { type: "lost", island: island.index });
  }
};

/** Why the skill cannot be cast right now, or `null`. The target is checked by the cast itself. */
const skillRefusal = (sim: TCleanupSim, skillId: TSkillId) => {
  const skill = getSkill(skillId);
  if (sim.status !== "running") {
    return "Бой окончен";
  }

  if (sim.tick < sim.skillReadyTick[skillId]) {
    return `«${skill.label}» ещё восстанавливается`;
  }

  if (sim.mana < skill.cost.amount) {
    return `Не хватает маны: нужно ${skill.cost.amount}`;
  }

  return null;
};

/** Casts a skill at a hex. Returns `null` on success, or the reason it was refused. */
const castSkill = (sim: TCleanupSim, skillId: TSkillId, target: TSkillTarget) => {
  const refusal = skillRefusal(sim, skillId);
  if (refusal) {
    return refusal;
  }

  const plan = shatterPlan(sim, target);
  if ("refusal" in plan) {
    return plan.refusal;
  }

  const skill = getSkill(skillId);
  removeHexes(sim, sim.islands[target.island] as TSimIsland, plan.removed, target.hex);
  refreshWorld(sim);
  sim.mana -= skill.cost.amount;
  sim.manaSpent += skill.cost.amount;
  sim.skillReadyTick[skillId] = sim.tick + Math.round(skill.cooldownSeconds * TICK_HZ);

  return null;
};

/** One fixed tick of the whole level. Does nothing once the level has ended. */
const stepCleanup = (sim: TCleanupSim) => {
  if (sim.status !== "running") {
    return;
  }

  const dt = TICK_SECONDS;
  sim.tick += 1;

  steerIslands(sim, dt);
  refreshWorld(sim);

  for (const unit of sim.units) {
    unit.px = unit.x;
    unit.py = unit.y;
    unit.cooldown -= dt;
    if (unit.node < 0) {
      unit.hoverNode = hoverNodeAt(sim, unit.x, unit.y);
    }
  }

  buildFlow(sim, "enemy", sim.flowToEnemy);
  buildFlow(sim, "player", sim.flowToPlayer);

  for (const unit of sim.units) {
    if (!unit.alive) {
      continue;
    }

    if (isStructure(unit)) {
      stepStructure(sim, unit);

      continue;
    }

    if (unit.node < 0) {
      stepFlyer(sim, unit, dt);
    } else {
      stepGroundUnit(sim, unit, dt);
    }

    if (unit.side === "player" && unit.hp < unit.maxHp && (sim.tick - unit.lastHurtTick) * dt > REGEN_DELAY) {
      unit.hp = Math.min(unit.maxHp, unit.hp + unit.maxHp * REGEN_SHARE_PER_SECOND * dt);
    }
  }

  const byId = new Map(sim.units.map((unit) => [unit.id, unit]));
  stepProjectiles(sim, byId, dt);

  // Ground units ride their island: the world position follows the local one.
  for (const unit of sim.units) {
    if (unit.node < 0) {
      continue;
    }

    const island = sim.islands[sim.nodeIsland[unit.node] as number] as TSimIsland;
    unit.x = island.body.x + unit.lx;
    unit.y = island.body.y + unit.ly;
  }

  sim.units = sim.units.filter((unit) => unit.alive);
  stepIslandStates(sim);
  stepStatus(sim);
  stepBorder(sim.border, sim.tick, TICK_HZ, sim.islands[0]?.body.radius ?? 300);
  stepPlumePoison(sim);
  stepExit(sim);
};

/**
 * Ends a level that is still running as a retreat, at the same cost as
 * sailing out through the border. Only the turn flow calls it, as a safety
 * net when the phase moves on without the level having ended.
 */
const retreatCleanup = (sim: TCleanupSim) => {
  if (sim.status === "running") {
    leaveBattle(sim);
  }
};

const setCleanupInput = (sim: TCleanupSim, x: number, y: number) => {
  sim.input.x = Math.max(-1, Math.min(1, x));
  sim.input.y = Math.max(-1, Math.min(1, y));
};

/** Takes the events queued since the last call. */
const drainEvents = (sim: TCleanupSim) => {
  const events = sim.events;
  sim.events = [];

  return events;
};

type TIslandSummary = {
  readonly id: string;
  readonly label: string;
  readonly state: TIslandState;
  readonly alive: number;
  readonly total: number;
  readonly kinds: readonly TEnemyId[];
  /** Hexes of the island; once attached, the hexes that joined. */
  readonly hexes: number;
  /** Whole seconds until an undocked husk is lost, or -1. */
  readonly driftLeft: number;
};

type TCleanupHud = {
  readonly status: TSimStatus;
  readonly alive: Readonly<Partial<Record<TUnitId, number>>>;
  readonly aliveTotal: number;
  readonly kills: number;
  readonly islands: readonly TIslandSummary[];
  readonly inWindow: boolean;
  /** Rounded to tenths, so the HUD updates a few times per second, not every tick. */
  readonly exitProgress: number;
  readonly windowsOpen: number;
  readonly mana: number;
  /** Seconds until each skill is ready, rounded to tenths. 0 is ready. */
  readonly skillCooldowns: Readonly<Record<TSkillId, number>>;
  readonly buildingsStanding: number;
  readonly buildingsTotal: number;
  /** The stronghold's hp in percent, rounded, or -1 when the island has none. */
  readonly strongholdPct: number;
};

/** The counts the HUD shows. Cheap enough to take every tick. */
const summarizeCleanup = (sim: TCleanupSim): TCleanupHud => {
  const alive: Partial<Record<TUnitId, number>> = {};
  let aliveTotal = 0;

  let buildingsStanding = 0;

  for (const unit of sim.units) {
    if (unit.alive && isStructure(unit)) {
      buildingsStanding += 1;

      continue;
    }

    if (unit.alive && unit.side === "player") {
      const kind = unit.kind as TUnitId;
      alive[kind] = (alive[kind] ?? 0) + 1;
      aliveTotal += 1;
    }
  }

  const islands = sim.islands
    .filter((island) => island.side === "enemy")
    .map((island) => {
      const garrison = sim.units.filter((unit) => unit.alive && unit.side === "enemy" && unit.homeIsland === island.index);

      return {
        id: island.id,
        label: island.label,
        state: island.state,
        alive: garrison.length,
        total: island.garrisonTotal,
        kinds: [...new Set(garrison.map((unit) => unit.kind as TEnemyId))],
        hexes: island.attach ? island.attach.joined : island.hexes.length,
        driftLeft: island.state === "cleared" ? Math.max(0, Math.ceil(LOST_AFTER - (sim.tick - island.stateTick) * TICK_SECONDS)) : -1,
      };
    });

  return {
    status: sim.status,
    alive,
    aliveTotal,
    kills: sim.kills,
    islands,
    inWindow: sim.inWindow,
    exitProgress: Math.round(sim.exitProgress * 10) / 10,
    windowsOpen: sim.border.windows.filter((window) => isWindowOpen(window, sim.tick)).length,
    mana: sim.mana,
    skillCooldowns: Object.fromEntries(
      SKILLS.map((skill) => [skill.id, Math.max(0, Math.ceil(((sim.skillReadyTick[skill.id] - sim.tick) * TICK_SECONDS) * 10) / 10)]),
    ) as Record<TSkillId, number>,
    buildingsStanding,
    buildingsTotal: sim.structuresAtStart,
    strongholdPct: strongholdPct(sim),
  };
};

const strongholdPct = (sim: TCleanupSim) => {
  const player = sim.islands[0];
  const index = player ? player.hexes.findIndex((hex) => hex.stronghold) : -1;
  const hex = player && index >= 0 ? player.hexes[index] : null;
  if (!hex || hex.maxHp <= 0) {
    return -1;
  }

  return Math.round((Math.max(0, sim.structureHp[index] as number) / hex.maxHp) * 100);
};

type TCleanupOutcome = "won" | "lost" | "retreated" | "calm";

type TCleanupResult = {
  readonly outcome: TCleanupOutcome;
  readonly totalIslands: number;
  readonly clearedIslands: number;
  /** Cleared islands that joined the player's island. */
  readonly attachedIslands: number;
  readonly annexed: readonly TAnnexedHex[];
  readonly survivors: readonly TUnitId[];
  readonly lost: readonly TUnitId[];
  readonly kills: number;
  /** Every structure of the player's island after the battle, by hex id. */
  readonly structures: readonly { readonly hexId: string; readonly hp: number; readonly startHp: number; readonly maxHp: number }[];
  readonly razed: number;
  /** Hexes of the player's island destroyed in battle, by id. They leave the island. */
  readonly destroyedHexIds: readonly string[];
  /** Hexes of the player's island that the border plumes poisoned, with their new toxicity. */
  readonly poisoned: readonly { readonly hexId: string; readonly toxicity: number }[];
  readonly manaSpent: number;
};

/** What the level ends with. A level with no enemy islands is a calm sea. */
const cleanupResult = (sim: TCleanupSim): TCleanupResult => {
  const enemies = sim.islands.filter((island) => island.side === "enemy");
  const survivors = sim.units
    .filter((unit) => unit.alive && unit.side === "player" && !isStructure(unit))
    .map((unit) => unit.kind as TUnitId);
  const player = sim.islands[0];
  const structures = (player?.hexes ?? [])
    .map((hex, index) => ({ hexId: hex.id, hp: Math.round(Math.max(0, sim.structureHp[index] as number)), startHp: hex.hp, maxHp: hex.maxHp }))
    .filter((entry) => entry.maxHp > 0);
  const hexById = new Map((player?.hexes ?? []).map((hex) => [hex.id, hex]));
  const poisoned = (player?.hexes ?? [])
    .filter((hex) => sim.startToxicity.has(hex.id) && hex.toxicity > (sim.startToxicity.get(hex.id) as number))
    .map((hex) => ({ hexId: hex.id, toxicity: hex.toxicity }));
  // A joined hex keeps the toxicity it has now; a joined hex destroyed later is gone.
  const annexed = sim.annexed
    .filter((gain) => hexById.has(hexId(gain.q, gain.r)))
    .map((gain) => ({ ...gain, toxicity: (hexById.get(hexId(gain.q, gain.r)) as TCleanupHex).toxicity }));
  let outcome: TCleanupOutcome = sim.status === "running" ? "retreated" : sim.status;
  if (enemies.length === 0) {
    outcome = "calm";
  }

  return {
    outcome,
    totalIslands: enemies.length,
    clearedIslands: enemies.filter((island) => island.state !== "active").length,
    attachedIslands: enemies.filter((island) => island.state === "attached").length,
    // A lost battle claims nothing: the island is in ruins.
    annexed: sim.status === "lost" ? [] : annexed,
    survivors,
    lost: [...sim.lost],
    kills: sim.kills,
    structures,
    razed: sim.razed,
    destroyedHexIds: [...sim.destroyedHexIds],
    poisoned,
    manaSpent: sim.manaSpent,
  };
};

export type {
  TSkillTarget,
  TAttachInfo,
  TCleanupHud,
  TCleanupOutcome,
  TCleanupResult,
  TCleanupSim,
  TIslandState,
  TIslandSummary,
  TSimEvent,
  TSimIsland,
  TSimProjectile,
  TSimStatus,
  TSimUnit,
};
export {
  cleanupResult,
  ATTACH_PULL_SECONDS,
  createCleanup,
  drainEvents,
  castSkill,
  retreatCleanup,
  shatterPlan,
  skillRefusal,
  setCleanupInput,
  stepCleanup,
  summarizeCleanup,
  TICK_HZ,
  TICK_SECONDS,
};
