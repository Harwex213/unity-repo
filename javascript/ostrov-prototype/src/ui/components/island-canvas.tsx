import { useSignals } from "@preact/signals-react/runtime";
import { useCallback, useEffect, useRef, useState } from "react";
import { getBiome } from "../../core/biomes";
import { buildRefusal } from "../../core/build-check";
import { getBuilding } from "../../core/buildings";
import { HEX_ART } from "../../core/hex-art";
import { canPlaceStronghold, STRONGHOLD_HEX_ART } from "../../core/stronghold";
import { isRuinedStronghold } from "../../core/structure-hp";
import { purifyRefusal, sacrificeRefusal } from "../../core/soil-cleanse";
import { isDead } from "../../core/tax";
import { findRoll } from "../../core/tax-plan";
import {
  AXIAL_DIRECTIONS,
  HEX_SIZE,
  hexBounds,
  hexCornerPoints,
  hexEdge,
  hexId,
  hexToPixel,
} from "../../core/hex";
import { useStore } from "../../store/store";
import { useIslandGround } from "../ground/use-island-ground";
import { HexHpBar } from "./hex-hp-bar";
import { HexPlates } from "./hex-plates";
import type { CSSProperties, FC, PointerEvent as ReactPointerEvent } from "react";
import type { TBuildingId, TGameStage, THex, TIsland, TPlayer } from "../../core/types";
import type { TSoilCleanseMode } from "../../store/ui-state";
import type {
  TBuildOnHexAction,
  TCancelSoilCleanseAction,
  THoverHexAction,
  TOpenTaxPickAction,
  TPickSoilHexAction,
  TPlaceStrongholdAction,
  TRequestDemolishAction,
  TSelectHexAction,
  TSetCameraAction,
} from "../../domain/registry";

/**
 * Hex art is the 256x256 set from `hex-art.ts`, not the 64px icons. The hex
 * scales it down at every zoom, so the art stays sharp. It sits a little above
 * the hex centre, because its scorched base reads as the ground of the hex.
 * A placed building and the ghost of the armed building both read
 * `building.hexArt`, so both draw the same image.
 */
const ART_SPAN = HEX_SIZE * 1.36;
const ART_LIFT = HEX_SIZE * 0.08;
/** The skull on a dead hex is smaller than a building, so it reads as a mark. */
const DEAD_SPAN = HEX_SIZE * 0.8;
const MIN_SCALE = 0.35;
const MAX_SCALE = 2.6;
/** One wheel notch. Zoom is geometric, so a notch is a constant ratio. */
const ZOOM_STEP = 1.12;
/**
 * The HUD floats over the canvas, so fitting centres the island in what is left
 * free: the turn panel above, the bottom bar below.
 */
const FIT_INSET_TOP_PX = 96;
const FIT_INSET_BOTTOM_PX = 196;
const FIT_INSET_SIDE_PX = 48;
/** A pointer that moved less than this between down and up was a click. */
const DRAG_SLOP_PX = 4;

const CORNER_POINTS = hexCornerPoints(HEX_SIZE);
/** Must match the stroke-width of `.hex-outline` in app.css. */
const OUTLINE_WIDTH = 4;
/**
 * A highlight outline is drawn inside its own hex. Its outer side lands on the
 * hex edge, so the outline of a neighbour never covers it. The corner radius
 * shrinks by w / sqrt(3), so each edge moves inwards by w / 2.
 */
const OUTLINE_POINTS = hexCornerPoints(HEX_SIZE - OUTLINE_WIDTH / Math.sqrt(3));
/** Must match the stroke-width of `.hex-outline--candidate` in app.css. */
const CANDIDATE_WIDTH = 4;
/**
 * A stronghold candidate draws its ring with a gap to the hex edge. Two
 * touching candidates then show two equal rings, not one doubled band.
 */
const CANDIDATE_INSET = CANDIDATE_WIDTH / 2 + 2;
const CANDIDATE_RADIUS = HEX_SIZE - CANDIDATE_INSET * (2 / Math.sqrt(3));
/** The share of the island width that the shimmer band covers. */
const SHIMMER_BAND = 0.3;
/** One sweep, then a pause before the next. */
const SHIMMER_SECONDS = 3.2;
/** The share of the cycle the band spends moving. The rest is the pause. */
const SHIMMER_SWEEP_SHARE = 0.7;
const SHIMMER_ID = "hex-candidate-shimmer";

/**
 * The candidate ring in island coordinates, not in the hex's own translated
 * space. The shimmer gradient uses island coordinates, so one band of light
 * crosses every candidate at once.
 */
const candidatePoints = (q: number, r: number) => {
  const center = hexToPixel(q, r, HEX_SIZE);

  return hexCornerPoints(CANDIDATE_RADIUS)
    .split(" ")
    .map((corner) => {
      const [x = 0, y = 0] = corner.split(",").map(Number);

      return `${(center.x + x).toFixed(2)},${(center.y + y).toFixed(2)}`;
    })
    .join(" ");
};

/** The hex grid toggle is remembered per browser. */
const GRID_STORAGE_KEY = "ostrov-v6:hex-grid";

const readGridPreference = () => {
  try {
    return window.localStorage.getItem(GRID_STORAGE_KEY) !== "off";
  } catch {
    return true;
  }
};

const writeGridPreference = (isVisible: boolean) => {
  try {
    window.localStorage.setItem(GRID_STORAGE_KEY, isVisible ? "on" : "off");
  } catch {
    // Storage can be blocked; the toggle then lasts for this page only.
  }
};

type TView = {
  readonly x: number;
  readonly y: number;
  readonly scale: number;
};

type TIslandCanvasRegistrySlice = {
  buildOnHexAction: TBuildOnHexAction;
  hoverHexAction: THoverHexAction;
  openTaxPickAction: TOpenTaxPickAction;
  pickSoilHexAction: TPickSoilHexAction;
  cancelSoilCleanseAction: TCancelSoilCleanseAction;
  placeStrongholdAction: TPlaceStrongholdAction;
  requestDemolishAction: TRequestDemolishAction;
  selectHexAction: TSelectHexAction;
  setCameraAction: TSetCameraAction;
};

type TIslandCanvasProps = {
  registry: TIslandCanvasRegistrySlice;
};

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

type THexStateContext = {
  readonly stage: TGameStage;
  readonly island: TIsland;
  readonly strongholdHexId: string | null;
  readonly armedBuildingId: TBuildingId | null;
  readonly demolishMode: boolean;
  readonly soilCleanse: TSoilCleanseMode | null;
  readonly player: TPlayer | null;
  readonly stoneDiscount: number;
};

/** Which visual state a hex is in, which is also its modifier class. */
const hexStateClass = (hex: THex, context: THexStateContext) => {
  // While the stronghold waits for a hex, every hex that can take it pulses.
  if (context.stage === "setup") {
    if (hex.id === context.strongholdHexId) {
      return "";
    }

    return canPlaceStronghold(context.island, hex.id) ? "hex--candidate" : "hex--blocked";
  }

  if (context.stage !== "play") {
    return "";
  }

  // The soil cleansing: first the hexes that can be destroyed, then the
  // hexes that can be purified. The stronghold hex may be purified too.
  if (context.soilCleanse && context.player) {
    const sacrificeHexId = context.soilCleanse.sacrificeHexId;
    if (sacrificeHexId === null) {
      return sacrificeRefusal(context.player, hex) === null ? "hex--sacrificable" : "hex--blocked";
    }

    if (hex.id === sacrificeHexId) {
      return "hex--sacrificed";
    }

    return purifyRefusal(hex, sacrificeHexId) === null ? "hex--purifiable" : "hex--blocked";
  }

  // The stronghold keeps its full biome colour: its gold outline already
  // says that nothing can be built on it or demolished from it.
  if (hex.id === context.strongholdHexId) {
    return "";
  }

  if (context.demolishMode) {
    return hex.building === null ? "hex--blocked" : "hex--demolishable";
  }

  if (!context.armedBuildingId || !context.player) {
    return "";
  }

  // The same rules as the click and the tooltip, the price included.
  const building = getBuilding(context.armedBuildingId);
  const refusal = buildRefusal(context.player, hex, building, context.stoneDiscount);

  return refusal === null ? "hex--buildable" : "hex--blocked";
};

/**
 * The armed building as a ghost on the hovered hex. The hex draws the ghost
 * through the same art element as a placed building, so the ghost always
 * matches the placed look. An occupied hex and the stronghold hex show their
 * own art, so they get no ghost.
 */
const ghostOn = (hex: THex, hoveredHexId: string | null, context: THexStateContext) => {
  if (context.stage !== "play" || context.demolishMode || !context.armedBuildingId || !context.player) {
    return null;
  }

  if (hex.id !== hoveredHexId || hex.building !== null || hex.id === context.strongholdHexId) {
    return null;
  }

  const building = getBuilding(context.armedBuildingId);
  const refusal = buildRefusal(context.player, hex, building, context.stoneDiscount);

  return { building, refused: refusal !== null };
};

const IslandCanvas: FC<TIslandCanvasProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const player = store.derived.viewedPlayer.value;
  const armedBuildingId = store.ui.armedBuilding.value;
  const demolishMode = store.ui.demolishMode.value;
  const soilCleanse = store.ui.soilCleanse.value;
  const soilFx = store.ui.soilCleanseFx.value;
  const hoveredHexId = store.ui.hoveredHexId.value;
  const selectedHexId = store.ui.selectedHexId.value;
  const isReadonly = store.derived.isReadonly.value;
  const stage = store.game.stage.value;
  const stoneDiscount = store.derived.techEffects.value.stoneDiscount;
  const taxPlan = store.derived.humanTaxPlan.value;
  const isTaxOpen = store.game.tax.value?.status === "rolled";
  const reveal = store.ui.productionReveal.value;

  const containerRef = useRef<HTMLDivElement>(null);
  const shimmerRef = useRef<SVGLinearGradientElement>(null);
  const dragRef = useRef<{ x: number; y: number; moved: number; captured: boolean } | null>(null);
  /** How far the pointer travelled in the gesture that just ended. */
  const lastMovedRef = useRef(0);
  const [view, setView] = useState<TView>({ x: 0, y: 0, scale: 1 });
  const [isGridVisible, setGridVisible] = useState(readGridPreference);

  const hexes = player?.island.hexes ?? [];
  const playerId = player?.id ?? null;
  const strongholdHexId = player?.strongholdHexId ?? null;
  const stateContext: THexStateContext = {
    stage,
    island: player?.island ?? { hexes: [] },
    strongholdHexId,
    armedBuildingId,
    demolishMode,
    soilCleanse,
    player,
    stoneDiscount,
  };

  /** Centres the island in the viewport and picks the scale that fits it. */
  const fitToView = useCallback(() => {
    const container = containerRef.current;
    if (!container || hexes.length === 0) {
      return;
    }

    const bounds = hexBounds(hexes, HEX_SIZE);
    const width = container.clientWidth - FIT_INSET_SIDE_PX * 2;
    const height = container.clientHeight - FIT_INSET_TOP_PX - FIT_INSET_BOTTOM_PX;
    const scale = clamp(
      Math.min(width / (bounds.maxX - bounds.minX), height / (bounds.maxY - bounds.minY)),
      MIN_SCALE,
      MAX_SCALE,
    );

    setView({
      scale,
      x: FIT_INSET_SIDE_PX + width / 2 - ((bounds.minX + bounds.maxX) / 2) * scale,
      y: FIT_INSET_TOP_PX + height / 2 - ((bounds.minY + bounds.maxY) / 2) * scale,
    });
  }, [hexes.length, playerId]);

  useEffect(() => {
    fitToView();
  }, [fitToView]);

  useEffect(() => {
    registry.setCameraAction(view);
  }, [registry, view]);

  // Wheel has to be bound by hand: React's own listener is passive, so it
  // cannot stop the page from scrolling behind the canvas.
  useEffect(() => {
    const container = containerRef.current;
    if (!container) {
      return;
    }

    const onWheel = (event: WheelEvent) => {
      event.preventDefault();

      const rect = container.getBoundingClientRect();
      const pointerX = event.clientX - rect.left;
      const pointerY = event.clientY - rect.top;
      // A trackpad pinch arrives as ctrl+wheel; both it and a mouse wheel zoom.
      const notches = event.deltaY / 100;

      setView((current) => {
        const scale = clamp(current.scale * Math.pow(ZOOM_STEP, -notches), MIN_SCALE, MAX_SCALE);
        const ratio = scale / current.scale;

        return {
          scale,
          x: pointerX - (pointerX - current.x) * ratio,
          y: pointerY - (pointerY - current.y) * ratio,
        };
      });
    };

    container.addEventListener("wheel", onWheel, { passive: false });

    return () => {
      container.removeEventListener("wheel", onWheel);
    };
  }, []);

  const toggleGrid = useCallback(() => {
    setGridVisible((current) => {
      writeGridPreference(!current);

      return !current;
    });
  }, []);

  // G toggles the hex grid, unless the player is typing in a field.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (event.repeat || event.ctrlKey || event.metaKey || event.altKey) {
        return;
      }

      if (target && (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName))) {
        return;
      }

      if (event.code === "KeyG") {
        toggleGrid();
      }
    };

    window.addEventListener("keydown", onKeyDown);

    return () => {
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [toggleGrid]);

  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    // The pointer is captured only once it starts to drag. Capturing it here
    // would retarget the click to the container, and no hex would ever be hit.
    dragRef.current = { x: event.clientX, y: event.clientY, moved: 0, captured: false };
  };

  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (!drag) {
      return;
    }

    const dx = event.clientX - drag.x;
    const dy = event.clientY - drag.y;
    drag.moved += Math.abs(dx) + Math.abs(dy);
    drag.x = event.clientX;
    drag.y = event.clientY;

    if (!drag.captured && drag.moved > DRAG_SLOP_PX) {
      event.currentTarget.setPointerCapture(event.pointerId);
      drag.captured = true;
    }

    setView((current) => ({ ...current, x: current.x + dx, y: current.y + dy }));
  };

  const onPointerUp = (event: ReactPointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (drag?.captured) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }

    lastMovedRef.current = drag?.moved ?? 0;
    dragRef.current = null;
  };

  /** The click right after a pan is part of the pan, not a click on a hex. */
  const wasDragged = () => lastMovedRef.current > DRAG_SLOP_PX;

  const onHexEnter = (hex: THex) => {
    const center = hexToPixel(hex.q, hex.r, HEX_SIZE);

    registry.hoverHexAction(hex.id, {
      x: view.x + center.x * view.scale,
      y: view.y + center.y * view.scale - HEX_SIZE * view.scale,
    });
  };

  const onHexClick = (hex: THex) => {
    if (wasDragged()) {
      return;
    }

    if (stage === "setup") {
      registry.placeStrongholdAction(hex.id);

      return;
    }

    // The start animation owns the screen for its short run.
    if (stage !== "play") {
      return;
    }

    if (!isReadonly && soilCleanse) {
      registry.pickSoilHexAction(hex.id);

      return;
    }

    if (!isReadonly && demolishMode) {
      registry.requestDemolishAction(hex.id);

      return;
    }

    if (!isReadonly && armedBuildingId) {
      registry.buildOnHexAction(hex.id);

      return;
    }

    // In the tax phase a click on a die opens its face popup.
    if (!isReadonly && isTaxOpen && findRoll(taxPlan, hex.id)) {
      registry.openTaxPickAction(hex.id);

      return;
    }

    registry.selectHexAction(hex.id);
  };

  const outlines = hexes
    .flatMap((hex) => {
      const center = hexToPixel(hex.q, hex.r, HEX_SIZE);
      const stateClass = isReadonly ? "" : hexStateClass(hex, stateContext);
      const isHovered = hex.id === hoveredHexId;
      const isSelected = hex.id === selectedHexId;
      const entries: { key: string; className: string; rank: number; x: number; y: number }[] = [];

      if (hex.id === strongholdHexId) {
        entries.push({
          key: `stronghold-${hex.id}`,
          className: "hex-outline hex-outline--stronghold",
          rank: 1,
          x: center.x,
          y: center.y,
        });
      }

      // A candidate draws its own ring in the shimmer layer below.
      const outlineClass = stateClass === "hex--candidate" ? "" : stateClass;

      if (outlineClass || isHovered || isSelected) {
        entries.push({
          key: `outline-${hex.id}`,
          className: `hex-outline ${outlineClass} ${isHovered ? "hex--hovered" : ""} ${isSelected ? "hex--selected" : ""}`,
          rank: isHovered ? 3 : isSelected ? 2 : 0,
          x: center.x,
          y: center.y,
        });
      }

      return entries;
    })
    .sort((left, right) => left.rank - right.rank);

  const candidates = isReadonly
    ? []
    : hexes.filter((hex) => hexStateClass(hex, stateContext) === "hex--candidate");
  // The shimmer band starts left of the island and ends right of it.
  const shimmerBounds = candidates.length > 0 ? hexBounds(candidates, HEX_SIZE) : null;
  const shimmerWidth = shimmerBounds ? shimmerBounds.maxX - shimmerBounds.minX : 0;
  const shimmerFrom = shimmerBounds ? shimmerBounds.minX - shimmerWidth * (0.5 + SHIMMER_BAND) : 0;
  const shimmerTo = shimmerBounds ? shimmerBounds.maxX + shimmerWidth * (SHIMMER_BAND - 0.5) : 0;

  // One loop moves the one gradient. The phase comes from the page clock, so
  // a candidate that appears later joins the same sweep. Reduced motion keeps
  // the static gold ring from the stylesheet.
  useEffect(() => {
    const gradient = shimmerRef.current;
    if (!gradient || window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      return;
    }

    let frame = 0;
    const tick = (now: number) => {
      const phase = (now / 1000 / SHIMMER_SECONDS) % 1;
      const progress = Math.min(phase / SHIMMER_SWEEP_SHARE, 1);
      gradient.setAttribute("gradientTransform", `translate(${shimmerFrom + (shimmerTo - shimmerFrom) * progress} 0)`);
      frame = requestAnimationFrame(tick);
    };

    frame = requestAnimationFrame(tick);

    return () => {
      cancelAnimationFrame(frame);
    };
  }, [shimmerFrom, shimmerTo]);

  const present = new Set(hexes.map((hex) => hex.id));
  const rimEdges = hexes.flatMap((hex) =>
    AXIAL_DIRECTIONS.flatMap((step, direction) => {
      if (present.has(hexId(hex.q + step.q, hex.r + step.r))) {
        return [];
      }

      return [{ key: `${hex.id}:${direction}`, ...hexEdge(hex.q, hex.r, direction, HEX_SIZE) }];
    }),
  );

  const ground = useIslandGround(hexes, playerId ?? "island");

  return (
    <div
      className="island-canvas"
      ref={containerRef}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerUp}
      onPointerLeave={() => registry.hoverHexAction(null, null)}
      onContextMenu={(event) => {
        // A right click is the way out of the soil cleansing.
        if (soilCleanse) {
          event.preventDefault();
          registry.cancelSoilCleanseAction();
        }
      }}
    >
      <svg
        className={`island-canvas__svg ${isGridVisible ? "" : "island-canvas__svg--grid-off"}`}
        role="presentation"
      >
        <g transform={`translate(${view.x} ${view.y}) scale(${view.scale})`}>
          {/* The painted ground draws its own organic coastline. The hex rim
              stays as the fallback while it loads or without WebGL2. */}
          {ground ? null : rimEdges.map((edge) => (
            <line
              className="island-rim"
              key={edge.key}
              x1={edge.x1}
              y1={edge.y1}
              x2={edge.x2}
              y2={edge.y2}
            />
          ))}

          {/* The ground is one painted image of the whole island, under every
              hex group. Until it is ready, flat biome colours stand in. */}
          {ground ? (
            <image
              className="island-ground"
              href={ground.href}
              x={ground.x}
              y={ground.y}
              width={ground.width}
              height={ground.height}
              preserveAspectRatio="none"
            />
          ) : (
            <g className="island-ground">
              {hexes.map((hex) => {
                const center = hexToPixel(hex.q, hex.r, HEX_SIZE);

                return (
                  <polygon
                    key={hex.id}
                    points={CORNER_POINTS}
                    transform={`translate(${center.x} ${center.y})`}
                    fill={getBiome(hex.biome).color}
                  />
                );
              })}
            </g>
          )}

          {hexes.map((hex) => {
            const center = hexToPixel(hex.q, hex.r, HEX_SIZE);
            const ghost = isReadonly ? null : ghostOn(hex, hoveredHexId, stateContext);
            const ghostClass = ghost ? `hex--ghost ${ghost.refused ? "hex--ghost-refused" : ""}` : "";
            const building = hex.building ? getBuilding(hex.building) : (ghost?.building ?? null);
            const stateClass = isReadonly ? "" : hexStateClass(hex, stateContext);
            const isHovered = hex.id === hoveredHexId;
            const isSelected = hex.id === selectedHexId;
            const isStronghold = hex.id === strongholdHexId;
            // The production pulse of the tax phase's reveal, in its turn.
            const pulseDelay = !isReadonly && reveal && !reveal.done ? reveal.delays[hex.id] : undefined;
            const pulseClass = pulseDelay === undefined ? "" : "hex__art--producing";
            const pulseStyle = pulseDelay === undefined ? undefined : { animationDelay: `${pulseDelay}ms` };

            return (
              <g
                key={hex.id}
                className={`hex ${stateClass} ${isDead(hex) ? "hex--dead" : ""} ${isHovered ? "hex--hovered" : ""} ${isSelected ? "hex--selected" : ""} ${isStronghold ? "hex--stronghold" : ""} ${ghostClass}`}
                transform={`translate(${center.x} ${center.y})`}
                onPointerEnter={() => onHexEnter(hex)}
                onPointerLeave={() => registry.hoverHexAction(null, null)}
                onClick={() => onHexClick(hex)}
              >
                {/* The ground lives in the layer below. The face is the hit
                    area, the faint grid line and the tint of a blocked hex. */}
                <polygon className="hex__face" points={CORNER_POINTS} />

                {hex.toxicity > 0 ? (
                  <polygon
                    className="hex__toxicity"
                    points={CORNER_POINTS}
                    fill="#9bff4f"
                    opacity={hex.toxicity / 160}
                  />
                ) : null}

                {/* The old tint of a hex the stronghold just purified fades out. */}
                {soilFx?.hexId === hex.id ? (
                  <polygon
                    key={soilFx.id}
                    className="hex__toxicity hex__toxicity--cleansed"
                    points={CORNER_POINTS}
                    fill="#9bff4f"
                    style={{ "--soil-from": soilFx.toxicity / 160 } as CSSProperties}
                  />
                ) : null}

                {isDead(hex) ? (
                  <image
                    className="hex__dead"
                    href={HEX_ART.dead}
                    x={-DEAD_SPAN / 2}
                    y={-DEAD_SPAN / 2}
                    width={DEAD_SPAN}
                    height={DEAD_SPAN}
                  />
                ) : null}

                {building ? (
                  <image
                    className={`hex__art ${pulseClass}`}
                    style={pulseStyle}
                    href={building.hexArt}
                    x={-ART_SPAN / 2}
                    y={-ART_SPAN / 2 - ART_LIFT}
                    width={ART_SPAN}
                    height={ART_SPAN}
                    preserveAspectRatio="xMidYMid meet"
                  />
                ) : null}

                {isStronghold ? (
                  <image
                    className={`hex__art hex__art--stronghold ${isRuinedStronghold(player, hex) ? "hex__art--ruined" : ""} ${pulseClass}`}
                    style={pulseStyle}
                    href={STRONGHOLD_HEX_ART}
                    x={-ART_SPAN / 2}
                    y={-ART_SPAN / 2 - ART_LIFT}
                    width={ART_SPAN}
                    height={ART_SPAN}
                    preserveAspectRatio="xMidYMid meet"
                  />
                ) : null}

                <HexHpBar player={player} hex={hex} />
              </g>
            );
          })}

          {/* Stronghold candidates share one gradient in island coordinates.
              One loop moves it, so the glint sweeps across all of them as one
              band of light. */}
          {shimmerBounds ? (
            <g className="hex-candidates">
              <defs>
                <linearGradient
                  ref={shimmerRef}
                  id={SHIMMER_ID}
                  gradientUnits="userSpaceOnUse"
                  x1={0}
                  y1={0}
                  x2={shimmerWidth}
                  y2={0}
                >
                  <stop className="hex-shimmer__base" offset={0} />
                  <stop className="hex-shimmer__base" offset={0.5 - SHIMMER_BAND / 2} />
                  <stop className="hex-shimmer__glint" offset={0.5} />
                  <stop className="hex-shimmer__base" offset={0.5 + SHIMMER_BAND / 2} />
                  <stop className="hex-shimmer__base" offset={1} />
                </linearGradient>
              </defs>

              {candidates.map((hex) => (
                <polygon
                  key={`back-${hex.id}`}
                  className="hex-outline hex-outline--candidate-back"
                  points={candidatePoints(hex.q, hex.r)}
                />
              ))}

              {candidates.map((hex) => (
                <polygon
                  key={hex.id}
                  className="hex-outline hex-outline--candidate"
                  points={candidatePoints(hex.q, hex.r)}
                />
              ))}
            </g>
          ) : null}

          {/* Highlight outlines go above every hex face, so no neighbour face
              covers them. Hover and selection go last, so they stay on top. */}
          {outlines.map((outline) => (
            <polygon
              key={outline.key}
              className={outline.className}
              points={OUTLINE_POINTS}
              transform={`translate(${outline.x} ${outline.y})`}
            />
          ))}

          {/* The plates go last: no hex, sprite or outline may cover them. */}
          <HexPlates scale={view.scale} />
        </g>
      </svg>

      <button
        type="button"
        className={`island-canvas__grid-toggle ${isGridVisible ? "island-canvas__grid-toggle--on" : ""}`}
        title="Сетка гексов (G)"
        aria-pressed={isGridVisible}
        onPointerDown={(event) => event.stopPropagation()}
        onClick={toggleGrid}
      >
        Сетка
      </button>
    </div>
  );
};

export { IslandCanvas };
