import { useSignals } from "@preact/signals-react/runtime";
import { useEffect, useRef } from "react";
import { ATTACH_PULL_SECONDS, drainEvents, shatterPlan, summarizeCleanup, TICK_SECONDS } from "../../../core/cleanup-sim";
import { getSkill, SKILLS } from "../../../core/skills";
import { useStore } from "../../../store/store";
import { absorbEvents, drawScene, pickHex, pruneEffects } from "./cleanup-render";
import { bakeIsland, releaseSprite } from "./island-sprites";
import { createSkyRenderer } from "./sky-gl";
import type { FC } from "react";
import type { TCleanupSim, TSimIsland } from "../../../core/cleanup-sim";
import type {
  TArmSkillAction,
  TCancelSkillAction,
  TCastSkillAction,
  TSetCleanupInputAction,
  TSetCleanupSpeedAction,
  TStepCleanupAction,
  TToggleCleanupPauseAction,
} from "../../../domain/registry";
import type { TCamera, TFrame, TScene, TTargetHover } from "./cleanup-render";

/** WASD and the arrows both steer the island. */
const KEY_DIRECTIONS: Readonly<Record<string, readonly [number, number]>> = {
  KeyW: [0, -1],
  KeyS: [0, 1],
  KeyA: [-1, 0],
  KeyD: [1, 0],
  ArrowUp: [0, -1],
  ArrowDown: [0, 1],
  ArrowLeft: [-1, 0],
  ArrowRight: [1, 0],
};
const MIN_ZOOM = 0.4;
const MAX_ZOOM = 1.8;
const ZOOM_STEP = 1.12;
/** The view shows about this many world units across its short side. */
const DEFAULT_VIEW_SPAN = 1250;
/** How fast the camera catches up with the island and with the wheel, per second. */
const CAMERA_FOLLOW = 4.5;
const ZOOM_FOLLOW = 8;
/** The camera looks ahead of a moving island by this many seconds of travel. */
const LOOK_AHEAD = 0.45;
/** A frame longer than this was a stall; the sim does not try to catch up. */
const MAX_FRAME_SECONDS = 0.25;
const MAX_TICKS_PER_FRAME = 8;
/** The scene draws at most 1.5 device pixels per CSS pixel: the paper look hides the rest. */
const MAX_PIXEL_RATIO = 1.5;
/** A press that moves farther than this, in CSS pixels, is a drag, not a click. */
const DRAG_THRESHOLD = 5;
/** A panned camera may look this far past the map border. */
const PAN_MARGIN = 500;

type TCleanupCanvasRegistrySlice = {
  setCleanupInputAction: TSetCleanupInputAction;
  stepCleanupAction: TStepCleanupAction;
  setCleanupSpeedAction: TSetCleanupSpeedAction;
  toggleCleanupPauseAction: TToggleCleanupPauseAction;
  armSkillAction: TArmSkillAction;
  cancelSkillAction: TCancelSkillAction;
  castSkillAction: TCastSkillAction;
};

type TCleanupCanvasProps = {
  registry: TCleanupCanvasRegistrySlice;
};

/** A test handle: `window.__ostrovCleanup.step(300)` runs 300 ticks at once. */
type TCleanupDevHook = {
  readonly step: (ticks: number) => void;
  readonly steer: (x: number, y: number) => void;
  readonly snapshot: () => unknown;
  readonly sim: () => TCleanupSim | null;
};

declare global {
  interface Window {
    __ostrovCleanup?: TCleanupDevHook;
  }
}

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/** A sky layer, or `null` without WebGL2 or when a shader fails: the scene still plays. */
const createSky = (canvas: HTMLCanvasElement | null, mode: "under" | "over", seed: number) => {
  if (!canvas) {
    return null;
  }

  try {
    return createSkyRenderer(canvas, mode, seed);
  } catch (error: unknown) {
    console.error(error);

    return null;
  }
};

const playerCenter = (sim: TCleanupSim) => {
  const island = sim.islands[0];
  if (!island) {
    return { x: 0, y: 0, vx: 0, vy: 0 };
  }

  return {
    x: island.body.x + island.body.centerX,
    y: island.body.y + island.body.centerY,
    vx: island.body.vx,
    vy: island.body.vy,
  };
};

/**
 * The cleanup scene. It owns the frame loop: the sim ticks at a fixed 30 Hz
 * from an accumulator, and every frame draws between the last two ticks. The
 * loop stops while the tab is hidden, and a long stall is not caught up.
 */
const CleanupCanvas: FC<TCleanupCanvasProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const sim = store.battle.sim.value;
  const stageRef = useRef<HTMLDivElement>(null);
  const skyRef = useRef<HTMLCanvasElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const veilRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const stage = stageRef.current;
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (!stage || !canvas || !context || !sim) {
      return;
    }

    let cancelled = false;
    const scene: TScene = {
      sprites: sim.islands.map(() => null),
      effects: [],
      bakedShape: sim.islands.map(() => -1),
      playerAt: null,
      wisps: null,
      wispArt: null,
      targeting: null,
    };
    // Without WebGL2 the sky is the plain slate of the stage's CSS background.
    const sky = createSky(skyRef.current, "under", sim.seed);
    const veil = createSky(veilRef.current, "over", sim.seed + 1);
    const motionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
    const clockStart = performance.now();
    const playerId = store.derived.humanPlayer.peek()?.id ?? "human";
    const baking = new Set<number>();

    /** Bakes an island with its current hexes. A destroyed hex or a joined island changes its shape. */
    const bake = (island: TSimIsland) => {
      const shape = island.shape;
      const seedKey = island.side === "player" ? playerId : `${sim.seed}:${island.id}`;
      baking.add(island.index);
      bakeIsland([...island.hexes], island.side, seedKey)
        .then((sprite) => {
          if (cancelled) {
            releaseSprite(sprite);

            return;
          }

          const old = scene.sprites[island.index];
          scene.sprites[island.index] = sprite;
          scene.bakedShape[island.index] = shape;
          if (old) {
            releaseSprite(old);
          }
        })
        .catch((error: unknown) => {
          console.error(error);
        })
        .finally(() => {
          baking.delete(island.index);
        });
    };

    sim.islands.forEach(bake);

    const start = playerCenter(sim);
    const baseZoom = () => clamp(Math.min(canvas.clientWidth, canvas.clientHeight) / DEFAULT_VIEW_SPAN, MIN_ZOOM, MAX_ZOOM);
    const camera: TCamera = { x: start.x, y: start.y, zoom: baseZoom() };
    let zoomTarget = camera.zoom;
    /**
     * A panned camera stays where the player left it. It glides back to the
     * island when the player steers, presses C or double-clicks.
     */
    let free = false;
    /** The world point under the cursor at the last wheel step, held there while a free camera zooms. */
    let zoomAnchor: { sx: number; sy: number; wx: number; wy: number } | null = null;
    let drag: { id: number; button: number; x: number; y: number; startX: number; startY: number; moved: boolean } | null = null;
    /** The cursor in CSS pixels of the stage, or `null` while it is outside. */
    let mouse: { x: number; y: number } | null = null;
    let lastFrame: TFrame | null = null;

    /** CSS pixels of the stage to world units. The canvas backing store's device pixel ratio does not enter here. */
    const toWorld = (sx: number, sy: number) => ({
      x: camera.x + (sx - canvas.clientWidth / 2) / camera.zoom,
      y: camera.y + (sy - canvas.clientHeight / 2) / camera.zoom,
    });

    const stagePoint = (event: MouseEvent) => {
      const rect = stage.getBoundingClientRect();

      return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    };

    const recenter = () => {
      free = false;
      zoomAnchor = null;
    };

    const pressed = new Set<string>();
    const pushInput = () => {
      let x = 0;
      let y = 0;
      for (const code of pressed) {
        const direction = KEY_DIRECTIONS[code];
        if (direction) {
          x += direction[0];
          y += direction[1];
        }
      }

      if (x !== 0 || y !== 0) {
        recenter();
      }

      registry.setCleanupInputAction(clamp(x, -1, 1), clamp(y, -1, 1));
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (KEY_DIRECTIONS[event.code]) {
        event.preventDefault();
        if (!pressed.has(event.code)) {
          pressed.add(event.code);
          pushInput();
        }

        return;
      }

      const skill = SKILLS.find((candidate) => candidate.hotkey === event.code);
      if (skill && !event.repeat) {
        registry.armSkillAction(skill.id);

        return;
      }

      if (event.code === "Space" && !event.repeat) {
        event.preventDefault();
        registry.toggleCleanupPauseAction();
      } else if (event.code === "Digit1") {
        registry.setCleanupSpeedAction(1);
      } else if (event.code === "Digit2") {
        registry.setCleanupSpeedAction(2);
      } else if (event.code === "KeyC") {
        recenter();
      } else if (event.code === "Escape" && store.battle.targeting.peek()) {
        registry.cancelSkillAction();
      }
    };

    const onKeyUp = (event: KeyboardEvent) => {
      if (pressed.delete(event.code)) {
        pushInput();
      }
    };

    const onBlur = () => {
      pressed.clear();
      pushInput();
      drag = null;
    };

    const onWheel = (event: WheelEvent) => {
      event.preventDefault();
      zoomTarget = clamp(zoomTarget * Math.pow(ZOOM_STEP, -event.deltaY / 100), MIN_ZOOM, MAX_ZOOM);
      if (free) {
        const point = stagePoint(event);
        const world = toWorld(point.x, point.y);
        zoomAnchor = { sx: point.x, sy: point.y, wx: world.x, wy: world.y };
      }
    };

    /** Any mouse button drags the camera. A left click without a drag casts the picked skill; a right click drops it. */
    const onPointerDown = (event: PointerEvent) => {
      if (event.pointerType === "mouse" && event.button > 2) {
        return;
      }

      if (event.button === 1) {
        // No autoscroll: the middle button pans like the others.
        event.preventDefault();
      }

      const point = stagePoint(event);
      drag = { id: event.pointerId, button: event.button, x: point.x, y: point.y, startX: point.x, startY: point.y, moved: false };
      stage.setPointerCapture(event.pointerId);
    };

    const onPointerMove = (event: PointerEvent) => {
      const point = stagePoint(event);
      mouse = point;
      if (!drag || drag.id !== event.pointerId) {
        return;
      }

      if (!drag.moved && Math.hypot(point.x - drag.startX, point.y - drag.startY) > DRAG_THRESHOLD) {
        drag.moved = true;
      }

      if (drag.moved) {
        camera.x -= (point.x - drag.x) / camera.zoom;
        camera.y -= (point.y - drag.y) / camera.zoom;
        free = true;
        zoomAnchor = null;
      }

      drag.x = point.x;
      drag.y = point.y;
    };

    const onPointerUp = (event: PointerEvent) => {
      if (!drag || drag.id !== event.pointerId) {
        return;
      }

      const click = !drag.moved;
      const button = drag.button;
      drag = null;
      if (stage.hasPointerCapture(event.pointerId)) {
        stage.releasePointerCapture(event.pointerId);
      }

      if (!click || !store.battle.targeting.peek()) {
        return;
      }

      if (button === 2) {
        registry.cancelSkillAction();

        return;
      }

      if (button === 0 && lastFrame) {
        const point = stagePoint(event);
        const world = toWorld(point.x, point.y);
        const target = pickHex(sim, lastFrame, world.x, world.y);
        if (target) {
          registry.castSkillAction(target);
        }
      }
    };

    const onPointerLeave = () => {
      mouse = null;
    };

    const onContextMenu = (event: MouseEvent) => {
      event.preventDefault();
    };

    const onDoubleClick = () => {
      if (!store.battle.targeting.peek()) {
        recenter();
      }
    };

    /** What the targeting cursor points at, and what a cast there would destroy. */
    const hoverOf = (frame: TFrame): TTargetHover | null => {
      if (!mouse) {
        return null;
      }

      const world = toWorld(mouse.x, mouse.y);
      const target = pickHex(sim, frame, world.x, world.y);
      if (!target) {
        return null;
      }

      const plan = shatterPlan(sim, target);

      return "refusal" in plan
        ? { island: target.island, hex: target.hex, removed: [target.hex], refusal: plan.refusal }
        : { island: target.island, hex: target.hex, removed: plan.removed, refusal: null };
    };

    let last = performance.now();
    let accumulator = 0;
    let frameHandle = 0;

    const onVisibility = () => {
      // A hidden tab gets no frames. On return the clock restarts, so the
      // time away is not played back.
      last = performance.now();
      if (document.hidden) {
        pressed.clear();
        pushInput();
      }
    };

    const frame = (now: number) => {
      frameHandle = requestAnimationFrame(frame);
      const seconds = Math.min(MAX_FRAME_SECONDS, Math.max(0, (now - last) / 1000));
      last = now;

      const running = sim.status === "running" && !store.battle.paused.peek() && !document.hidden;
      if (running) {
        accumulator += seconds * store.battle.speed.peek();
        const ticks = Math.min(MAX_TICKS_PER_FRAME, Math.floor(accumulator / TICK_SECONDS));
        accumulator -= ticks * TICK_SECONDS;
        accumulator = Math.min(accumulator, TICK_SECONDS);
        if (ticks > 0) {
          registry.stepCleanupAction(ticks);
        }
      }

      const alpha = sim.status === "running" ? clamp(accumulator / TICK_SECONDS, 0, 1) : 1;

      // An island changed its hexes: it is baked again. A joining island
      // first plays its pull-in, so the seam blends like any other.
      const pulling = sim.islands.some((island) => {
        return island.attach !== null && (sim.tick - island.attach.tick) * TICK_SECONDS < ATTACH_PULL_SECONDS;
      });
      for (const island of sim.islands) {
        const stale = (scene.bakedShape[island.index] ?? -1) !== island.shape;
        const drawn = island.state !== "attached" && island.state !== "lost" && island.hexes.length > 0;
        if (stale && drawn && !baking.has(island.index) && !(island.index === 0 && pulling)) {
          bake(island);
        }
      }

      const simTime = sim.tick * TICK_SECONDS;
      absorbEvents(scene, sim, drainEvents(sim), simTime);
      pruneEffects(scene, simTime);

      if (free) {
        camera.zoom += (zoomTarget - camera.zoom) * (1 - Math.exp(-ZOOM_FOLLOW * seconds));
        if (zoomAnchor) {
          camera.x = zoomAnchor.wx - (zoomAnchor.sx - canvas.clientWidth / 2) / camera.zoom;
          camera.y = zoomAnchor.wy - (zoomAnchor.sy - canvas.clientHeight / 2) / camera.zoom;
        }

        camera.x = clamp(camera.x, -sim.bounds.halfWidth - PAN_MARGIN, sim.bounds.halfWidth + PAN_MARGIN);
        camera.y = clamp(camera.y, -sim.bounds.halfHeight - PAN_MARGIN, sim.bounds.halfHeight + PAN_MARGIN);
      } else {
        const target = playerCenter(sim);
        const follow = 1 - Math.exp(-CAMERA_FOLLOW * seconds);
        camera.x += (target.x + target.vx * LOOK_AHEAD - camera.x) * follow;
        camera.y += (target.y + target.vy * LOOK_AHEAD - camera.y) * follow;
        camera.zoom += (zoomTarget - camera.zoom) * (1 - Math.exp(-ZOOM_FOLLOW * seconds));
      }

      const ratio = Math.min(MAX_PIXEL_RATIO, window.devicePixelRatio || 1);
      const width = canvas.clientWidth;
      const height = canvas.clientHeight;
      if (canvas.width !== Math.round(width * ratio) || canvas.height !== Math.round(height * ratio)) {
        canvas.width = Math.round(width * ratio);
        canvas.height = Math.round(height * ratio);
      }

      const motion = !motionQuery.matches;
      const clock = (now - clockStart) / 1000;
      const player = sim.islands[0];
      const skyFrame = {
        width,
        height,
        ratio,
        cameraX: camera.x,
        cameraY: camera.y,
        zoom: camera.zoom,
        time: motion ? clock : 0,
        focusX: width / 2 + ((player ? player.body.x + player.body.centerX : 0) - camera.x) * camera.zoom,
        focusY: height / 2 + ((player ? player.body.y + player.body.centerY : 0) - camera.y) * camera.zoom,
        focusRadius: ((player?.body.radius ?? 300) + 80) * camera.zoom,
      };

      const sceneFrame: TFrame = { width, height, ratio, alpha, camera, clock, motion };
      lastFrame = sceneFrame;
      const skillId = store.battle.targeting.peek();
      scene.targeting = skillId ? { reach: getSkill(skillId).reach, hover: hoverOf(sceneFrame) } : null;
      stage.classList.toggle("cleanup-stage--targeting", skillId !== null);
      stage.classList.toggle("cleanup-stage--panning", drag !== null && drag.moved);

      sky?.render(skyFrame);
      drawScene(context, sim, scene, sceneFrame);
      veil?.render(skyFrame);
    };

    frameHandle = requestAnimationFrame(frame);

    window.__ostrovCleanup = {
      step: (ticks) => registry.stepCleanupAction(ticks),
      steer: (x, y) => registry.setCleanupInputAction(x, y),
      snapshot: () => ({
        tick: sim.tick,
        status: sim.status,
        player: playerCenter(sim),
        bridges: sim.bridgeLinks.reduce((sum, links) => sum + links.length, 0) / 2,
        islands: sim.islands.map((island) => ({
          id: island.id,
          state: island.state,
          x: Math.round(island.body.x + island.body.centerX),
          y: Math.round(island.body.y + island.body.centerY),
        })),
        hud: summarizeCleanup(sim),
      }),
      sim: () => store.battle.sim.peek(),
    };

    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("keyup", onKeyUp);
    window.addEventListener("blur", onBlur);
    document.addEventListener("visibilitychange", onVisibility);
    stage.addEventListener("wheel", onWheel, { passive: false });
    stage.addEventListener("pointerdown", onPointerDown);
    stage.addEventListener("pointermove", onPointerMove);
    stage.addEventListener("pointerup", onPointerUp);
    stage.addEventListener("pointercancel", onPointerUp);
    stage.addEventListener("pointerleave", onPointerLeave);
    stage.addEventListener("contextmenu", onContextMenu);
    stage.addEventListener("dblclick", onDoubleClick);

    return () => {
      cancelled = true;
      cancelAnimationFrame(frameHandle);
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("keyup", onKeyUp);
      window.removeEventListener("blur", onBlur);
      document.removeEventListener("visibilitychange", onVisibility);
      stage.removeEventListener("wheel", onWheel);
      stage.removeEventListener("pointerdown", onPointerDown);
      stage.removeEventListener("pointermove", onPointerMove);
      stage.removeEventListener("pointerup", onPointerUp);
      stage.removeEventListener("pointercancel", onPointerUp);
      stage.removeEventListener("pointerleave", onPointerLeave);
      stage.removeEventListener("contextmenu", onContextMenu);
      stage.removeEventListener("dblclick", onDoubleClick);
      stage.classList.remove("cleanup-stage--targeting", "cleanup-stage--panning");
      sky?.dispose();
      veil?.dispose();
      registry.setCleanupInputAction(0, 0);
      delete window.__ostrovCleanup;

      for (const sprite of scene.sprites) {
        if (sprite) {
          releaseSprite(sprite);
        }
      }
    };
  }, [sim, registry, store]);

  return (
    <div className="cleanup-stage" ref={stageRef}>
      <canvas className="cleanup-stage__layer" ref={skyRef} />
      <canvas className="cleanup-stage__layer" ref={canvasRef} />
      <canvas className="cleanup-stage__layer cleanup-stage__layer--veil" ref={veilRef} />
    </div>
  );
};

export { CleanupCanvas };
