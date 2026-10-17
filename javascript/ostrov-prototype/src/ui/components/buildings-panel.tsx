import { useSignals } from "@preact/signals-react/runtime";
import { BUILDINGS, canAfford, effectiveCost, isBuildingUnlocked } from "../../core/buildings";
import { ICONS } from "../../core/icons";
import { getResource } from "../../core/resources";
import { useStore } from "../../store/store";
import { FaceChip, FaceGrid } from "./die-faces";
import { Icon } from "./icon";
import type { FC } from "react";
import type { TBuilding, TFace } from "../../core/types";
import type { TArmBuildingAction } from "../../domain/registry";

type TBuildingsPanelRegistrySlice = {
  armBuildingAction: TArmBuildingAction;
};

type TBuildingsPanelProps = {
  registry: TBuildingsPanelRegistrySlice;
};

/**
 * The extra face that the biome adds, as far as the card can know it. The card
 * has no hex, so the face is known only if every biome gives the same one.
 * Returns undefined if no biome adds a face, and null if the face depends on
 * the biome.
 */
const knownBiomeFace = (building: TBuilding): TFace | null | undefined => {
  const faces = Object.values(building.biomeFaces).filter((item): item is TFace => item !== undefined);
  const [first] = faces;
  if (!first) {
    return undefined;
  }

  const same = faces.every((item) => item.amount === first.amount && item.toxicity === first.toxicity);

  return same ? first : null;
};

/**
 * The building cards show only the art. Clicking one arms it; the next click
 * on a hex of the island places it. The armed card stays armed so several
 * hexes can be filled in a row. The name, the faces and the price show on
 * hover.
 */
const BuildingsPanel: FC<TBuildingsPanelProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const player = store.derived.humanPlayer.value;
  const pool = player?.resources;
  const armedId = store.ui.armedBuilding.value;
  const locked = store.game.phase.value !== "build" || store.ui.busy.value || store.derived.isHumanReady.value;
  const discount = store.derived.techEffects.value.stoneDiscount;

  if (!pool || !player) {
    return null;
  }

  return (
    <div
      className={`panel buildings-panel ${locked ? "panel--stowed" : ""}`}
      data-tutorial="build"
      aria-hidden={locked}
      inert={locked}
    >
      {BUILDINGS.map((building) => {
        const affordable = canAfford(pool, building, discount);
        const cost = effectiveCost(building, discount);
        const yields = getResource(building.yields);
        const isLocked = !isBuildingUnlocked(player, building);
        const biomeFace = knownBiomeFace(building);
        const stateClass =`${building.trophy ? "building-card--trophy" : ""} ${isLocked ? "building-card--locked" : ""}`;

        return (
          <button
            key={building.id}
            type="button"
            className={`building-card has-hint ${armedId === building.id ? "building-card--armed" : ""} ${affordable ? "" : "building-card--poor"} ${stateClass}`}
            onClick={() => registry.armBuildingAction(building.id)}
            aria-label={building.label}
          >
            <img className="building-card__art" src={building.art} alt="" />

            <span className="hint hint--building">
              <span className="hint__head">
                <span className="hint__title">
                  {building.label}
                </span>

                <Icon src={yields.icon} label={`Даёт: ${yields.label}`} className="hint__yield" />
              </span>

              <FaceGrid>
                {building.baseFaces.map((item, index) => (
                  <FaceChip
                    key={`${item.amount}-${item.toxicity}-${index}`}
                    resource={item.resource}
                    amount={item.amount}
                    toxicity={item.toxicity}
                  />
                ))}

                {biomeFace === undefined ? null : (
                  <FaceChip
                    kind="biome"
                    resource={building.yields}
                    amount={biomeFace === null ? "?" : biomeFace.amount}
                    toxicity={biomeFace === null ? "?" : biomeFace.toxicity}
                  />
                )}
              </FaceGrid>

              {building.trophy ? (
                <span className="hint__note">
                  {isLocked
                    ? "Нужна технология «Центральная конверсия»: найдите и победите босса на карте мира"
                    : "Отключает токсичность всех ваших зданий. Построить его — победить"}
                </span>
              ) : null}

              <span className="hint__price">
                <span className="hint__price-label">
                  {"Цена постройки"}
                </span>

                <span className="hint__price-items">
                  <span className={pool.stone >= cost.stone ? "" : "cost--short"}>
                    <Icon src={ICONS.stone} label="Камень" />
                    {cost.stone}
                  </span>

                  <span className={pool.wood >= cost.wood ? "" : "cost--short"}>
                    <Icon src={ICONS.wood} label="Дерево" />
                    {cost.wood}
                  </span>

                  <span className={pool.hammers >= cost.hammers ? "" : "cost--short"}>
                    <Icon src={ICONS.hammers} label="Молотки" />
                    {cost.hammers}
                  </span>
                </span>
              </span>
            </span>
          </button>
        );
      })}
    </div>
  );
};

export { BuildingsPanel };
