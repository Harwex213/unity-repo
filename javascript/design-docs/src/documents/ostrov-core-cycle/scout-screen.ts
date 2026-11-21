import { drawImageContain } from "../../kit/image";
import { PALETTE } from "./palette";
import { cellCenter, drawSphere, traceCell } from "./globe";
import type { TPoint, TRect } from "../../core/types";
import type { TImageAsset } from "../../kit/image";
import type { TAxial } from "../../kit/hex";
import type { TSphere } from "./globe";

// A mock screenshot of the scout phase in ostrov-prototype.
// The globe fills the screen. The panels stand where the prototype puts them:
// players at the top left, the turn at the top center, the end-turn wheel at the bottom right.
// The route leads from the player's island to the boss's lair.

const LAIR: TAxial = { q: 3, r: -1 };
const LAIR_RING_RADIUS = 11;

const drawRoute = (ctx: CanvasRenderingContext2D, from: TPoint, to: TPoint) => {
  const length = Math.hypot(to.x - from.x, to.y - from.y);
  // The route stops at the ring of the lair and at the edge of the home cell.
  const ux = (to.x - from.x) / length;
  const uy = (to.y - from.y) / length;
  const start = { x: from.x + ux * 12, y: from.y + uy * 12 };
  const end = { x: to.x - ux * (LAIR_RING_RADIUS + 3), y: to.y - uy * (LAIR_RING_RADIUS + 3) };
  ctx.setLineDash([4, 3]);
  ctx.beginPath();
  ctx.moveTo(start.x, start.y);
  ctx.lineTo(end.x, end.y);
  ctx.strokeStyle = PALETTE.ink;
  ctx.lineWidth = 1.5;
  ctx.stroke();
  ctx.setLineDash([]);
};

const drawPanels = (ctx: CanvasRenderingContext2D, screen: TRect) => {
  ctx.fillStyle = PALETTE.paper;
  ctx.strokeStyle = PALETTE.muted;
  ctx.lineWidth = 1;
  const panels: TRect[] = [
    { x: screen.x + 5, y: screen.y + 5, width: 30, height: 22 },
    { x: screen.x + screen.width / 2 - 16, y: screen.y + 5, width: 32, height: 12 },
  ];
  for (const panel of panels) {
    ctx.fillRect(panel.x, panel.y, panel.width, panel.height);
    ctx.strokeRect(panel.x, panel.y, panel.width, panel.height);
  }
  ctx.beginPath();
  ctx.arc(screen.x + screen.width - 16, screen.y + screen.height - 16, 11, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
};

const drawScoutScreen = (ctx: CanvasRenderingContext2D, screen: TRect, lairIcon: TImageAsset) => {
  ctx.save();
  ctx.beginPath();
  ctx.rect(screen.x, screen.y, screen.width, screen.height);
  ctx.clip();

  // The globe is taller than the screen, as on the scout page of the prototype.
  // Its left and right edges stay in view.
  const sphere: TSphere = {
    center: { x: screen.x + screen.width / 2, y: screen.y + screen.height * 0.55 },
    radius: screen.width * 0.5 - 2,
    cellAngle: 0.2,
  };
  drawSphere(ctx, sphere);

  const home = sphere.center;
  const lair = cellCenter(sphere, LAIR);
  if (traceCell(ctx, sphere, LAIR)) {
    ctx.fillStyle = PALETTE.paper;
    ctx.fill();
    ctx.lineWidth = 2.5;
    ctx.stroke();
  }
  ctx.beginPath();
  ctx.arc(lair.x, lair.y, LAIR_RING_RADIUS, 0, Math.PI * 2);
  ctx.lineWidth = 1.5;
  ctx.stroke();
  const iconSize = LAIR_RING_RADIUS * 1.6;
  drawImageContain(ctx, lairIcon, { x: lair.x - iconSize / 2, y: lair.y - iconSize / 2, width: iconSize, height: iconSize });
  drawRoute(ctx, home, lair);

  drawPanels(ctx, screen);
  ctx.restore();
};

export { drawScoutScreen };
