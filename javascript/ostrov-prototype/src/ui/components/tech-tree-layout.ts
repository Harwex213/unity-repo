import { ICONS } from "../../core/icons";
import { getTech, TECHS } from "../../core/techs";
import { getUnit } from "../../core/units";
import type { TTechBranch, TTechId } from "../../core/techs";

/**
 * Where each technology sits on the technology sheet. The sheet is a
 * snowflake: a hub in the centre and one branch per category. War grows up,
 * economy grows down to the right, ecology grows down to the left. A node's
 * place is its step along the branch and its lane across the branch.
 * The units are sheet units: the modal scales the whole sheet to fit.
 */

type TSheetPoint = {
  readonly x: number;
  readonly y: number;
};

type TSheetBox = {
  readonly minX: number;
  readonly minY: number;
  readonly width: number;
  readonly height: number;
};

/** One connector line: a prerequisite and the technology that needs it. */
type TSheetEdge = {
  /** `null` is the hub in the centre: a technology with no prerequisite grows from it. */
  readonly from: TTechId | null;
  readonly to: TTechId;
};

type TPlacement = {
  /** Distance from the hub along the branch, in steps. */
  readonly step: number;
  /** Offset across the branch, in lanes. Positive is to the right of the branch direction. */
  readonly lane: number;
};

const STEP = 100;
const LANE = 124;
/** The node circle radius. Must match the size of `.tech-node` in app.css. */
const NODE_RADIUS = 32;
/** Room around the outermost nodes for the name under a node and the branch labels. */
const MARGIN = 96;

/** The direction of each branch, in degrees. 0 points right, 90 points down. */
const BRANCH_ANGLE: Readonly<Record<TTechBranch, number>> = {
  military: -90,
  economy: 18,
  ecology: 162,
};

const BRANCH_LABELS: Readonly<Record<TTechBranch, string>> = {
  military: "Война",
  economy: "Хозяйство",
  ecology: "Экология",
};

/** Where the branch label sits: one step past the farthest node of the branch. */
const BRANCH_LABEL_STEP: Readonly<Record<TTechBranch, number>> = {
  military: 5.8,
  economy: 2.6,
  ecology: 3.4,
};

/**
 * War has four lines: melee (spears) and ranged (slings) up the middle,
 * birds on the left and cavalry on the right. The two late joins, heavy
 * cavalry and the griffin roost, sit between the lines they join.
 */
const PLACEMENTS: Readonly<Record<TTechId, TPlacement>> = {
  spears: { step: 1, lane: -0.8 },
  swords: { step: 2, lane: -0.8 },
  halberds: { step: 3, lane: -0.8 },
  knighthood: { step: 4, lane: -0.8 },
  slings: { step: 1, lane: 0.8 },
  archery: { step: 2, lane: 0.8 },
  longbow: { step: 3, lane: 0.8 },
  gunpowder: { step: 4, lane: 0.8 },
  stables: { step: 2.2, lane: 2.3 },
  heavy_stables: { step: 4.2, lane: 2.3 },
  falconry: { step: 2.2, lane: -2.3 },
  griffin_roost: { step: 5, lane: -1.6 },
  masonry: { step: 1.5, lane: -0.6 },
  irrigation: { step: 1.5, lane: 0.6 },
  filters: { step: 1.4, lane: 0.55 },
  asylums: { step: 2.4, lane: 0.55 },
  central_conversion: { step: 1.9, lane: -0.8 },
};

/** The art drawn inside each node: the first unit it unlocks, or what it changes. */
const TECH_ICONS: Readonly<Record<TTechId, string>> = {
  spears: getUnit("spearman").icon,
  swords: getUnit("swordsman").icon,
  halberds: getUnit("halberdier").icon,
  knighthood: getUnit("knight").icon,
  slings: getUnit("slinger").icon,
  archery: getUnit("archer").icon,
  longbow: getUnit("longbowman").icon,
  gunpowder: getUnit("musketeer").icon,
  stables: ICONS.cavalry,
  heavy_stables: ICONS.cavalry,
  falconry: getUnit("crow").icon,
  griffin_roost: getUnit("griffin").icon,
  masonry: ICONS.masonsGuild,
  irrigation: ICONS.farm,
  filters: ICONS.toxicity,
  asylums: ICONS.mad,
  central_conversion: ICONS.converter,
};

const HUB: TSheetPoint = { x: 0, y: 0 };

const branchPoint = (branch: TTechBranch, step: number, lane: number): TSheetPoint => {
  const radians = (BRANCH_ANGLE[branch] * Math.PI) / 180;
  const dirX = Math.cos(radians);
  const dirY = Math.sin(radians);

  // The lane axis is the branch direction turned a quarter to the right.
  return {
    x: dirX * step * STEP - dirY * lane * LANE,
    y: dirY * step * STEP + dirX * lane * LANE,
  };
};

const TECH_POINTS: ReadonlyMap<TTechId, TSheetPoint> = new Map(
  TECHS.map((tech) => [tech.id, branchPoint(tech.branch, PLACEMENTS[tech.id].step, PLACEMENTS[tech.id].lane)]),
);

const techPoint = (id: TTechId): TSheetPoint => TECH_POINTS.get(id) ?? HUB;

const BRANCH_LABEL_POINTS: Readonly<Record<TTechBranch, TSheetPoint>> = {
  military: branchPoint("military", BRANCH_LABEL_STEP.military, 0),
  economy: branchPoint("economy", BRANCH_LABEL_STEP.economy, 0),
  ecology: branchPoint("ecology", BRANCH_LABEL_STEP.ecology, 0),
};

const SHEET_EDGES: readonly TSheetEdge[] = TECHS.flatMap((tech): TSheetEdge[] =>
  tech.requires.length === 0
    ? [{ from: null, to: tech.id }]
    : tech.requires.map((required) => ({ from: required, to: tech.id })),
);

const SHEET_BOX: TSheetBox = (() => {
  const points = [HUB, ...TECH_POINTS.values(), ...Object.values(BRANCH_LABEL_POINTS)];
  const xs = points.map((point) => point.x);
  const ys = points.map((point) => point.y);
  const minX = Math.min(...xs) - MARGIN;
  const minY = Math.min(...ys) - MARGIN;

  return {
    minX,
    minY,
    width: Math.max(...xs) + MARGIN - minX,
    height: Math.max(...ys) + MARGIN - minY,
  };
})();

/** Every prerequisite of a technology, all the way back to the hub. */
const ancestorsOf = (id: TTechId): ReadonlySet<TTechId> => {
  const result = new Set<TTechId>();
  const queue: TTechId[] = [id];

  while (queue.length > 0) {
    const current = queue.pop();
    if (!current) {
      break;
    }

    getTech(current).requires.forEach((required) => {
      if (!result.has(required)) {
        result.add(required);
        queue.push(required);
      }
    });
  }

  return result;
};

/**
 * A connector bows a little to one side, so the lines do not read as ruler
 * strokes. The path runs from the edge of one circle to the edge of the other.
 */
const edgePath = (from: TSheetPoint, to: TSheetPoint, fromRadius: number, toRadius: number, bow: number): string => {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const length = Math.hypot(dx, dy) || 1;
  const unitX = dx / length;
  const unitY = dy / length;
  const start = { x: from.x + unitX * fromRadius, y: from.y + unitY * fromRadius };
  const end = { x: to.x - unitX * toRadius, y: to.y - unitY * toRadius };
  const control = {
    x: (start.x + end.x) / 2 - unitY * bow,
    y: (start.y + end.y) / 2 + unitX * bow,
  };

  return `M ${start.x} ${start.y} Q ${control.x} ${control.y} ${end.x} ${end.y}`;
};

export type { TSheetBox, TSheetEdge, TSheetPoint };
export {
  ancestorsOf,
  BRANCH_LABEL_POINTS,
  BRANCH_LABELS,
  edgePath,
  HUB,
  NODE_RADIUS,
  SHEET_BOX,
  SHEET_EDGES,
  TECH_ICONS,
  techPoint,
};
