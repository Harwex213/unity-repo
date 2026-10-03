import { useSignals } from "@preact/signals-react/runtime";
import { useMemo } from "react";
import { isPageSwitchPhase } from "../../core/phases";
import { useStore } from "../../store/store";
import type { FC } from "react";
import type { TVec3, TWorld } from "../../core/world-gen";
import type { TNavigateToWorldAction } from "../../domain/registry";

type TWorldMinimapRegistrySlice = {
  navigateToWorldAction: TNavigateToWorldAction;
};

type TWorldMinimapProps = {
  registry: TWorldMinimapRegistrySlice;
};

/** The sphere's radius in SVG units. The view box adds a margin for the rim. */
const RADIUS = 48;
const VIEW_BOX = `${-RADIUS - 2} ${-RADIUS - 2} ${2 * RADIUS + 4} ${2 * RADIUS + 4}`;

const normalize = (v: TVec3): TVec3 => {
  const length = Math.hypot(v[0], v[1], v[2]) || 1;

  return [v[0] / length, v[1] / length, v[2] / length];
};

const cross = (a: TVec3, b: TVec3): TVec3 => [
  a[1] * b[2] - a[2] * b[1],
  a[2] * b[0] - a[0] * b[2],
  a[0] * b[1] - a[1] * b[0],
];

const dot = (a: TVec3, b: TVec3) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

/** A fixed view from slightly above, so the globe reads as a sphere. */
const VIEW = normalize([0.3, 0.45, 1]);
const RIGHT = normalize(cross([0, 1, 0], VIEW));
const UP = cross(VIEW, RIGHT);

type TFace = {
  readonly points: string;
  /** Some faces draw as land, the rest as clouds, so the map has texture. */
  readonly isLand: boolean;
};

/**
 * The faces of the world grid on the near side, nearest to the viewer first.
 * A corner behind the limb is pushed onto the limb, so the rim cells close the
 * disc without folding over.
 */
const projectFaces = (world: TWorld): readonly TFace[] => {
  const project = (point: TVec3) => {
    const x = dot(point, RIGHT);
    const y = -dot(point, UP);
    const scale = dot(point, VIEW) < 0 ? 1 / (Math.hypot(x, y) || 1) : 1;

    return `${(x * scale * RADIUS).toFixed(2)},${(y * scale * RADIUS).toFixed(2)}`;
  };

  return world.cells
    .map((cell) => ({ cell, depth: dot(cell.center, VIEW) }))
    .filter((entry) => entry.depth > 0)
    .sort((a, b) => b.depth - a.depth)
    .map(({ cell }) => ({
      points: cell.polygon.map(project).join(" "),
      isLand: (cell.index * 7) % 5 === 0,
    }));
};

/**
 * A small schematic globe left of the end-turn wheel. It opens the global map.
 * The revealed share of the world shows as a patch that grows from the centre
 * of the disc. The patch covers the whole disc once every cell is revealed.
 */
const WorldMinimap: FC<TWorldMinimapProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const world = store.world.world.value;
  // The same guard as `navigateToWorldAction`.
  const canOpen = store.game.stage.value === "play" && isPageSwitchPhase(store.game.phase.value) && !store.ui.busy.value;
  const faces = useMemo(() => (world ? projectFaces(world) : []), [world]);

  if (!world) {
    return null;
  }

  const total = world.cells.length;
  const revealed = world.cells.filter((cell) => cell.revealed).length;
  const fraction = total > 0 ? revealed / total : 0;
  const shown = fraction >= 1 ? faces.length : Math.ceil(fraction * faces.length);

  return (
    <button
      type="button"
      className={`panel minimap-panel has-hint ${canOpen ? "" : "panel--locked"}`}
      aria-label="Глобальная карта"
      disabled={!canOpen}
      onClick={registry.navigateToWorldAction}
    >
      <svg className="minimap-panel__svg" viewBox={VIEW_BOX} aria-hidden="true">
        <defs>
          <clipPath id="world-minimap-disc">
            <circle r={RADIUS} />
          </clipPath>

          <pattern id="world-minimap-hatch" width="3" height="3" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
            <rect className="world-minimap__fog" width="3" height="3" />
            <line className="world-minimap__hatch" x1="0" y1="0" x2="0" y2="3" />
          </pattern>

          <radialGradient id="world-minimap-shade" cx="-0.15" cy="-0.2" r="1.1">
            <stop offset="0.45" stopColor="#000" stopOpacity="0" />
            <stop offset="1" stopColor="#000" stopOpacity="0.6" />
          </radialGradient>
        </defs>

        <g clipPath="url(#world-minimap-disc)">
          <circle className="world-minimap__sea" r={RADIUS} />

          {faces.map((face, index) => {
            const kind = index < shown ? (face.isLand ? "land" : "cloud") : "fog";

            return (
              <polygon
                key={index}
                className={`world-minimap__face world-minimap__face--${kind}`}
                points={face.points}
              />
            );
          })}

          <circle r={RADIUS} fill="url(#world-minimap-shade)" />
        </g>

        <circle className="world-minimap__rim" r={RADIUS} />
      </svg>

      <span className="hint">
        <span className="hint__title">
          {"Глобальная карта"}
        </span>

        <span className="hint__row">
          {`Разведано гексов: ${revealed} из ${total}`}
        </span>
      </span>
    </button>
  );
};

export { WorldMinimap };
