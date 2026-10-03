import { useSignals } from "@preact/signals-react/runtime";
import { getBiome } from "../../core/biomes";
import { getBuilding } from "../../core/buildings";
import { HEX_ART } from "../../core/hex-art";
import { HEX_SIZE, hexBounds, hexCornerPoints, hexToPixel } from "../../core/hex";
import { isPageSwitchPhase } from "../../core/phases";
import { STRONGHOLD_HEX_ART } from "../../core/stronghold";
import { isRuinedStronghold } from "../../core/structure-hp";
import { isDead } from "../../core/tax";
import { useStore } from "../../store/store";
import type { FC } from "react";
import type { TNavigateToIslandAction } from "../../domain/registry";

type TIslandMinimapRegistrySlice = {
  navigateToIslandAction: TNavigateToIslandAction;
};

type TIslandMinimapProps = {
  registry: TIslandMinimapRegistrySlice;
};

/** The same hex and art sizes as the island canvas, so the SVG is a scaled copy. */
const CORNER_POINTS = hexCornerPoints(HEX_SIZE);
const ART_SPAN = HEX_SIZE * 1.36;
const ART_LIFT = HEX_SIZE * 0.08;
const DEAD_SPAN = HEX_SIZE * 0.8;

/**
 * The player's own island in small, left of the end-turn wheel on the world
 * page. It draws the island canvas's flat look: biome hexes, toxicity, the
 * buildings and the stronghold. A click takes the player back to the island.
 */
const IslandMinimap: FC<TIslandMinimapProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const player = store.derived.humanPlayer.value;
  // The same guard as `navigateToIslandAction`.
  const canOpen = store.game.stage.value === "play" && isPageSwitchPhase(store.game.phase.value) && !store.ui.busy.value;

  if (!player || player.island.hexes.length === 0) {
    return null;
  }

  const hexes = player.island.hexes;
  const bounds = hexBounds(hexes, HEX_SIZE);
  // The building art rises above the top row of hexes.
  const top = bounds.minY - ART_LIFT - (ART_SPAN - 2 * HEX_SIZE) / 2;
  const viewBox = `${bounds.minX} ${top} ${bounds.maxX - bounds.minX} ${bounds.maxY - top}`;
  const buildings = hexes.filter((hex) => hex.building !== null).length;

  return (
    <button
      type="button"
      className={`panel minimap-panel has-hint ${canOpen ? "" : "panel--locked"}`}
      aria-label="Вернуться на остров"
      disabled={!canOpen}
      onClick={() => registry.navigateToIslandAction(null)}
    >
      <svg className="minimap-panel__svg" viewBox={viewBox} aria-hidden="true">
        {hexes.map((hex) => {
          const center = hexToPixel(hex.q, hex.r, HEX_SIZE);
          const building = hex.building ? getBuilding(hex.building) : null;
          const isStronghold = hex.id === player.strongholdHexId;

          return (
            <g
              key={hex.id}
              className={isDead(hex) ? "hex--dead" : ""}
              transform={`translate(${center.x} ${center.y})`}
            >
              <polygon className="island-minimap__hex" points={CORNER_POINTS} fill={getBiome(hex.biome).color} />

              {hex.toxicity > 0 ? (
                <polygon points={CORNER_POINTS} fill="#9bff4f" opacity={hex.toxicity / 160} />
              ) : null}

              {isDead(hex) ? (
                <image
                  href={HEX_ART.dead}
                  x={-DEAD_SPAN / 2}
                  y={-DEAD_SPAN / 2}
                  width={DEAD_SPAN}
                  height={DEAD_SPAN}
                />
              ) : null}

              {building ? (
                <image
                  className="hex__art"
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
                  className={`hex__art ${isRuinedStronghold(player, hex) ? "hex__art--ruined" : ""}`}
                  href={STRONGHOLD_HEX_ART}
                  x={-ART_SPAN / 2}
                  y={-ART_SPAN / 2 - ART_LIFT}
                  width={ART_SPAN}
                  height={ART_SPAN}
                  preserveAspectRatio="xMidYMid meet"
                />
              ) : null}
            </g>
          );
        })}
      </svg>

      <span className="hint">
        <span className="hint__title">
          {"Вернуться на остров"}
        </span>

        <span className="hint__row">
          {`Гексов: ${hexes.length}, зданий: ${buildings}`}
        </span>
      </span>
    </button>
  );
};

export { IslandMinimap };
