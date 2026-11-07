/**
 * The storyboard, in seconds from the first frame:
 *
 *   0-1  the black screen fades into the planet, without the HUD
 *   1-6  the planet explodes
 *   6+   the HUD appears with an animation
 *
 * Every moment of the intro is a pure function of the time. The engine can jump
 * to any moment, and a skip or a replay needs no extra state.
 */
const TIMELINE = {
  fadeInEnd: 1,
  crackStart: 1.05,
  /** The planet holds its breath: the rumble stops and the light drops. */
  inhale: 2.45,
  detonate: 2.8,
  /** The dust thins out here and the surviving island becomes visible. */
  islandReveal: 4.3,
  explosionEnd: 6,
  hud: 6,
} as const;

type TVec3 = readonly [number, number, number];

type TSceneState = {
  readonly cameraPosition: TVec3;
  readonly cameraTarget: TVec3;
  readonly fov: number;
  readonly roll: number;
  /** 0 is black, 1 is the full picture. */
  readonly fade: number;
  readonly exposure: number;
  readonly flash: number;
  readonly aberration: number;
  /** Strength of the light shafts from the core. */
  readonly shafts: number;
};

const clamp01 = (value: number): number => Math.min(1, Math.max(0, value));
const smooth = (from: number, to: number, value: number): number => {
  const x = clamp01((value - from) / (to - from));

  return x * x * (3 - 2 * x);
};
const mix = (from: number, to: number, amount: number): number => from + (to - from) * amount;

/** Smooth noise of one variable, in the range -1..1. */
const wobble = (x: number, seed: number): number =>
  (Math.sin(x * 1.31 + seed) + Math.sin(x * 2.17 + seed * 1.7) * 0.6 + Math.sin(x * 4.73 + seed * 2.3) * 0.4) / 2;

/** The core sits to the right of the HUD on a wide screen, in the centre on a narrow one. */
const menuShift = (aspect: number): number => 2.3 * smooth(1, 1.45, aspect);

/** On a tall screen the HUD fills the bottom, so the star and the island move up. */
const menuLift = (aspect: number): number => (1 - smooth(0.8, 1.1, aspect)) * 3.7;

/** How strongly the camera shakes at the time `t`. */
const traumaAt = (t: number): number => {
  const { crackStart, inhale, detonate } = TIMELINE;
  if (t < detonate) {
    return smooth(crackStart, inhale - 0.1, t) * 0.34 * (1 - smooth(inhale - 0.1, inhale + 0.05, t));
  }

  return Math.exp(-(t - detonate) * 1.5);
};

const sceneAt = (t: number, aspect: number, pointerX: number, pointerY: number): TSceneState => {
  const { fadeInEnd, inhale, detonate, explosionEnd } = TIMELINE;
  const sinceBlast = Math.max(0, t - detonate);

  // The camera creeps toward the planet, recoils from the blast, then settles
  // on the wide shot of the menu. The pull is slow at the end, so the shot never freezes.
  const push = smooth(0, detonate, t);
  const pull = 1 - Math.exp(-sinceBlast * 0.78);
  const pan = smooth(detonate + 0.5, explosionEnd + 1.5, t);
  const shift = menuShift(aspect);
  const lift = menuLift(aspect);
  const narrowZoom = 1 + (1 - smooth(0.7, 1.6, aspect)) * 0.9;
  const startDistance = mix(5.9, 5.15, push) * (1 + (narrowZoom - 1) * 0.5);
  const distance = mix(startDistance, 13.6 * narrowZoom, pull);
  const drift = Math.sin(t * 0.07) * 0.14;

  const trauma = traumaAt(t);
  const shake = trauma * trauma * 0.16;
  const parallax = pan;

  const cameraPosition: TVec3 = [
    mix(0, -shift + 0.55, pan) + wobble(t * 19, 1.3) * shake + pointerX * 0.5 * parallax + drift,
    mix(0.05, -0.2 - lift, pan) + wobble(t * 23, 4.1) * shake - pointerY * 0.32 * parallax,
    distance,
  ];
  const cameraTarget: TVec3 = [
    mix(0, -shift, pan) + wobble(t * 17, 7.7) * shake * 0.5 + pointerX * 0.12 * parallax,
    mix(0, -0.8 - lift, pan) + wobble(t * 21, 2.9) * shake * 0.5 - pointerY * 0.08 * parallax,
    0,
  ];

  const blastPunch = sinceBlast > 0 ? Math.exp(-sinceBlast * 2.4) : 0;
  const inhaleDim = smooth(inhale, detonate, t) * (t < detonate ? 1 : 0);
  // A slow glare follows the flash, then the eye adapts to the darker scene.
  const glare = sinceBlast > 0 ? Math.exp(-sinceBlast * 0.85) : 0;

  return {
    cameraPosition,
    cameraTarget,
    fov: 28 + blastPunch * 5 * (1 - Math.exp(-sinceBlast * 18)),
    roll: wobble(t * 13, 5.2) * shake * 0.1 + Math.sin(t * 0.05) * 0.008 * pan,
    fade: smooth(0, fadeInEnd, t),
    exposure: mix(1.08, 1, push) * (1 - inhaleDim * 0.68) + glare * 0.14,
    flash: t >= detonate ? Math.exp(-sinceBlast * 6) : 0,
    aberration: 0.5 + trauma * 3.2,
    shafts: glare * 0.9 + 0.2,
  };
};

export type { TSceneState, TVec3 };
export { TIMELINE, clamp01, menuShift, mix, sceneAt, smooth };
