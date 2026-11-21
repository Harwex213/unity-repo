import { PALETTE } from "./palette";
import { SCREEN } from "./hud";
import { circle, paint, rect, text } from "./wire";
import type { TPoint } from "../../core/types";

// A phase scheme: the game screen with numbered callouts around it,
// redrawn from the inline SVG of docs/spec/ui-phase-*.html.
// Scheme units are the units of the SVG viewBox. The game screen sits inside the scheme,
// moved and scaled as in the SVG; screen units are the units of a 1600 × 900 screen.

const SCHEME = { width: 2000, height: 1025 };
const SCREEN_ORIGIN: TPoint = { x: 320, y: 110 };
const SCREEN_SCALE = 0.85;

const toScheme = (point: TPoint): TPoint => ({
  x: SCREEN_ORIGIN.x + point.x * SCREEN_SCALE,
  y: SCREEN_ORIGIN.y + point.y * SCREEN_SCALE,
});

// ---------- Callouts ----------

// Badges stand in four lines around the screen.
const BADGE_LEFT_X = 276;
const BADGE_RIGHT_X = 1724;
const BADGE_TOP_Y = 56;
const BADGE_BOTTOM_Y = [929, 975];
// A leader from a top or bottom badge goes straight to this line, then to its tip.
const ELBOW_TOP_Y = 96;
const ELBOW_BOTTOM_Y = 889;

const BADGE_RADIUS = 17;
const TIP_RADIUS = 7;
const LABEL_GAP = 27;

type TCallout = {
  number: number;
  label: string;
  badge: TPoint;
  // The point of the screen the leader ends at, in scheme units.
  tip: TPoint;
  // A part that appears after an action: blue and dashed. Otherwise the part stays all the phase.
  onAction: boolean;
};

type TCalloutOptions = {
  onAction?: boolean;
  // Moves the badge along its line when a straight leader would cross another one.
  badgeAt?: number;
};

// `target` is in screen units.
const leftCallout = (number: number, label: string, target: TPoint, options: TCalloutOptions = {}): TCallout => {
  const tip = toScheme(target);
  return { number, label, tip, badge: { x: BADGE_LEFT_X, y: options.badgeAt ?? tip.y }, onAction: !!options.onAction };
};

const rightCallout = (number: number, label: string, target: TPoint, options: TCalloutOptions = {}): TCallout => {
  const tip = toScheme(target);
  return { number, label, tip, badge: { x: BADGE_RIGHT_X, y: options.badgeAt ?? tip.y }, onAction: !!options.onAction };
};

const topCallout = (number: number, label: string, target: TPoint, options: TCalloutOptions = {}): TCallout => {
  const tip = toScheme(target);
  return { number, label, tip, badge: { x: options.badgeAt ?? tip.x, y: BADGE_TOP_Y }, onAction: !!options.onAction };
};

// The second line of bottom badges keeps a label clear of the label next to it.
const bottomCallout = (
  number: number,
  label: string,
  target: TPoint,
  options: TCalloutOptions & { secondLine?: boolean } = {},
): TCallout => {
  const tip = toScheme(target);
  const y = BADGE_BOTTOM_Y[options.secondLine ? 1 : 0] ?? 0;
  return { number, label, tip, badge: { x: options.badgeAt ?? tip.x, y }, onAction: !!options.onAction };
};

const leaderOf = (callout: TCallout): TPoint[] => {
  const { badge, tip } = callout;
  if (badge.y === BADGE_TOP_Y) {
    return [badge, { x: badge.x, y: ELBOW_TOP_Y }, tip];
  }
  if (badge.y > ELBOW_BOTTOM_Y) {
    return [badge, { x: badge.x, y: ELBOW_BOTTOM_Y }, tip];
  }
  return [badge, tip];
};

const drawCallouts = (ctx: CanvasRenderingContext2D, callouts: TCallout[]) => {
  callouts.forEach((callout) => {
    const color = callout.onAction ? PALETTE.onAction : PALETTE.always;
    paint(ctx, { stroke: color, width: 1.75, dash: callout.onAction ? [7, 5] : [] }, () => {
      leaderOf(callout).forEach((point, index) =>
        index === 0 ? ctx.moveTo(point.x, point.y) : ctx.lineTo(point.x, point.y),
      );
    });
    circle(ctx, callout.tip, TIP_RADIUS, { fill: color });
  });
  callouts.forEach((callout) => {
    const { badge } = callout;
    const color = callout.onAction ? PALETTE.onAction : PALETTE.always;
    circle(ctx, badge, BADGE_RADIUS, { fill: color });
    text(ctx, String(callout.number), badge.x, badge.y + 7, { size: 21, weight: 700, color: PALETTE.paper }, "center");
    const onLeft = badge.x === BADGE_LEFT_X;
    text(
      ctx,
      callout.label,
      onLeft ? badge.x - LABEL_GAP : badge.x + LABEL_GAP,
      badge.y + 8,
      { size: 23, weight: 600 },
      onLeft ? "right" : "left",
    );
  });
};

// ---------- Scheme ----------

type TPhaseScheme = {
  // The text under the frame title, from the header of the spec.
  summary: string;
  // Draws the game screen in screen units.
  drawScreen: (ctx: CanvasRenderingContext2D) => void;
  callouts: TCallout[];
};

// Draws the scheme with its top left corner at `origin`, `scale` world units per scheme unit.
const drawScheme = (ctx: CanvasRenderingContext2D, scheme: TPhaseScheme, origin: TPoint, scale: number) => {
  ctx.save();
  ctx.translate(origin.x, origin.y);
  ctx.scale(scale, scale);

  ctx.save();
  ctx.translate(SCREEN_ORIGIN.x, SCREEN_ORIGIN.y);
  ctx.scale(SCREEN_SCALE, SCREEN_SCALE);
  const frame = { x: 0, y: 0, ...SCREEN };
  rect(ctx, frame, 0, { fill: PALETTE.screen });
  ctx.save();
  ctx.beginPath();
  ctx.rect(frame.x, frame.y, frame.width, frame.height);
  ctx.clip();
  scheme.drawScreen(ctx);
  ctx.restore();
  rect(ctx, frame, 0, { stroke: PALETTE.ink, width: 4 });
  ctx.restore();

  drawCallouts(ctx, scheme.callouts);
  ctx.restore();
};

export { SCHEME, bottomCallout, drawScheme, leftCallout, rightCallout, topCallout };
export type { TCallout, TPhaseScheme };
