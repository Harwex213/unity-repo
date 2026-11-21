import { drawText } from "../../kit/shapes";
import { PALETTE } from "./palette";
import type { TPoint, TRect } from "../../core/types";

// The tech tree as a radial "snowflake", like the passive tree of Path of Exile:
// a start node in the center, six branches, and a small ring of nodes at the end of each branch.
// The player learns nodes from the center outwards. Learned nodes are filled.
// ostrov-prototype has a list of technologies, not a tree, so the snowflake is schematic.

const BRANCHES = 6;
// The nodes along a branch, as shares of the radius.
const BRANCH_NODES = [0.3, 0.55];
const CLUSTER_SHARE = 0.82;
const CLUSTER_NODES = 4;
// The branch that the player has learned up to its cluster, and one more step on the next branch.
const LEARNED_BRANCH = 0;
const LEARNED_STEPS = { [LEARNED_BRANCH]: 3, 1: 1 } as Record<number, number>;

const TITLE_HEIGHT = 18;

const polar = (center: TPoint, radius: number, angle: number): TPoint => ({
  x: center.x + Math.cos(angle) * radius,
  y: center.y + Math.sin(angle) * radius,
});

const drawNode = (ctx: CanvasRenderingContext2D, at: TPoint, radius: number, learned: boolean) => {
  ctx.beginPath();
  ctx.arc(at.x, at.y, radius, 0, Math.PI * 2);
  ctx.fillStyle = learned ? PALETTE.ink : PALETTE.paper;
  ctx.fill();
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.25;
  ctx.stroke();
};

// A mini window with the title on top and the snowflake under it.
const drawTechTree = (ctx: CanvasRenderingContext2D, rect: TRect) => {
  ctx.beginPath();
  ctx.roundRect(rect.x, rect.y, rect.width, rect.height, 6);
  ctx.fillStyle = PALETTE.paper;
  ctx.fill();
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.5;
  ctx.stroke();
  drawText(ctx, "Дерево технологий", rect.x + 8, rect.y + 5, { size: 11, weight: 700 });

  const center = { x: rect.x + rect.width / 2, y: rect.y + TITLE_HEIGHT + (rect.height - TITLE_HEIGHT) / 2 };
  const radius = Math.min(rect.width, rect.height - TITLE_HEIGHT) / 2 - 8;
  const clusterRadius = radius * 0.16;

  // The links first, so the nodes cover their ends.
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1;
  ctx.beginPath();
  for (let branch = 0; branch < BRANCHES; branch++) {
    const angle = -Math.PI / 2 + (branch * Math.PI * 2) / BRANCHES;
    const cluster = polar(center, radius * CLUSTER_SHARE, angle);
    ctx.moveTo(center.x, center.y);
    ctx.lineTo(cluster.x, cluster.y);
    ctx.moveTo(cluster.x + clusterRadius, cluster.y);
    ctx.arc(cluster.x, cluster.y, clusterRadius, 0, Math.PI * 2);
    // Each cluster links to the next branch's middle node, so the branches make a web.
    const next = polar(center, radius * (BRANCH_NODES[1] ?? 0), angle + (Math.PI * 2) / BRANCHES);
    const middle = polar(center, radius * (BRANCH_NODES[1] ?? 0), angle);
    ctx.moveTo(middle.x, middle.y);
    ctx.lineTo(next.x, next.y);
  }
  ctx.stroke();

  for (let branch = 0; branch < BRANCHES; branch++) {
    const angle = -Math.PI / 2 + (branch * Math.PI * 2) / BRANCHES;
    const learned = LEARNED_STEPS[branch] ?? 0;
    BRANCH_NODES.forEach((share, index) => {
      drawNode(ctx, polar(center, radius * share, angle), 2.6, index < learned);
    });
    const cluster = polar(center, radius * CLUSTER_SHARE, angle);
    for (let node = 0; node < CLUSTER_NODES; node++) {
      const nodeAngle = angle + (node * Math.PI * 2) / CLUSTER_NODES;
      drawNode(ctx, polar(cluster, clusterRadius, nodeAngle), 2.2, learned > BRANCH_NODES.length && node === 2);
    }
  }
  // The start node.
  drawNode(ctx, center, 4.5, true);
};

export { drawTechTree };
