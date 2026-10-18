import { useSignals } from "@preact/signals-react/runtime";
import { getBiome } from "../../core/biomes";
import { buildRefusal } from "../../core/build-check";
import {
  canBuildOn,
  effectiveCost,
  facesOn,
  getBuilding,
} from "../../core/buildings";
import { ICONS } from "../../core/icons";
import { isStaffed, workersNeeded, workersOn } from "../../core/population";
import { hexDie } from "../../core/dice";
import { canPlaceStronghold, isStrongholdHex, STRONGHOLD_LABEL } from "../../core/stronghold";
import { isDead, isFoodBlocked } from "../../core/tax";
import { findRoll } from "../../core/tax-plan";
import { useStore } from "../../store/store";
import { DieFaces } from "./die-faces";
import { Icon } from "./icon";
import { useLayoutEffect, useRef } from "react";
import type { FC } from "react";
import type { TBuilding, THex, TPlayer } from "../../core/types";

/** The smallest gap between the popup and the window edge, in px. */
const WINDOW_EDGE_PX = 8;

/**
 * Shifts the popup back inside the window when the popup crosses an edge.
 * The CSS centers the popup above the hex. The shift runs after every render,
 * because the size of the popup depends on its rows.
 */
const useKeepInWindow = () => {
  const ref = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    const element = ref.current;
    if (!element) {
      return;
    }

    element.style.translate = "";
    const rect = element.getBoundingClientRect();
    const dx =
      Math.max(WINDOW_EDGE_PX - rect.left, 0) + Math.min(window.innerWidth - WINDOW_EDGE_PX - rect.right, 0);
    const dy =
      Math.max(WINDOW_EDGE_PX - rect.top, 0) + Math.min(window.innerHeight - WINDOW_EDGE_PX - rect.bottom, 0);
    element.style.translate = `${dx}px ${dy}px`;
  });

  return ref;
};

type TBuildPreviewProps = {
  hex: THex;
  building: TBuilding;
  player: TPlayer;
  stoneDiscount: number;
  anchor: { x: number; y: number };
};

/**
 * What the armed building would be on the hovered hex: its die on this biome,
 * what the hex's toxicity leaves of each face, the price, and why the click
 * would be refused. The canvas shows the building itself as a ghost on the hex.
 * The verdict comes from the same rules the build action runs.
 */
const BuildPreview: FC<TBuildPreviewProps> = ({ hex, building, player, stoneDiscount, anchor }) => {
  const biome = getBiome(hex.biome);
  const refusal = buildRefusal(player, hex, building, stoneDiscount);
  const fits = canBuildOn(building, hex.biome);
  const cost = effectiveCost(building, stoneDiscount);
  const pool = player.resources;
  const standing = isStrongholdHex(player, hex.id)
    ? STRONGHOLD_LABEL
    : hex.building
      ? getBuilding(hex.building).label
      : null;
  const biomeFace = building.biomeFaces[hex.biome];
  const faces = facesOn(building, hex.biome);
  const ref = useKeepInWindow();

  return (
    <div
      ref={ref}
      className={`hex-tooltip hex-tooltip--build ${refusal ? "hex-tooltip--refused" : "hex-tooltip--allowed"}`}
      style={{ left: `${anchor.x}px`, top: `${anchor.y}px` }}
    >
      <div className="hex-tooltip__biome">
        {biome.label}
      </div>

      {standing ? (
        <div className="hex-tooltip__standing">
          {`здесь стоит: ${standing}`}
        </div>
      ) : null}

      {fits ? (
        <DieFaces
          faces={faces}
          biomeFaceIndex={biomeFace === undefined ? -1 : faces.length - 1}
          biomeLabel={biome.label}
          hex={hex}
          className="hex-tooltip__faces"
        />
      ) : null}

      <div className="hex-tooltip__row">
        {"Цена: "}

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
      </div>

      {hex.toxicity > 0 ? (
        <div className="hex-tooltip__toxicity">
          {`Токсичность гекса: ${Math.round(hex.toxicity)}% — урожай меньше`}
        </div>
      ) : null}

      {refusal === null && isDead(hex) ? (
        <div className="hex-tooltip__warning">
          {"Гекс мёртв: здание ничего не произведёт"}
        </div>
      ) : null}

      {refusal === null && !isDead(hex) && isFoodBlocked(hex) && building.yields === "food" ? (
        <div className="hex-tooltip__warning">
          {"Токсичность выше 50%: еду здесь не вырастить"}
        </div>
      ) : null}

      {refusal ? (
        <div className="hex-tooltip__warning">
          {refusal.message}
        </div>
      ) : null}
    </div>
  );
};

/**
 * The hover popup of the spec: the name of what stands on the hex and the
 * resource combinations its die can roll. The stronghold has a die too. An
 * empty hex only names its biome. In the tax phase the popup shows only the
 * title and the faces, with the chosen face lit: the hex plates already show
 * the workers and the payout. The hp of a structure lives on its hex plate.
 */
const HexTooltip = () => {
  useSignals();
  const store = useStore();
  const hex = store.derived.hoveredHex.value;
  const anchor = store.ui.hoverAnchor.value;
  const player = store.derived.viewedPlayer.value;
  const isSetup = store.game.stage.value === "setup";
  const isPlay = store.game.stage.value === "play";
  const armedId = store.ui.armedBuilding.value;
  const isReadonly = store.derived.isReadonly.value;
  const effects = store.derived.techEffects.value;
  const taxPlan = store.derived.humanTaxPlan.value;
  const isTaxPhase = isPlay && store.game.phase.value === "tax";
  const ref = useKeepInWindow();
  const isBuildPhase =
    isPlay && store.game.phase.value === "build" && !store.ui.busy.value && !store.derived.isHumanReady.value;

  if (!hex || !anchor) {
    return null;
  }

  if (isPlay && armedId && player && !isReadonly) {
    return (
      <BuildPreview
        hex={hex}
        building={getBuilding(armedId)}
        player={player}
        stoneDiscount={effects.stoneDiscount}
        anchor={anchor}
      />
    );
  }

  const biome = getBiome(hex.biome);
  const die = hexDie(player, hex);
  const isStronghold = isStrongholdHex(player, hex.id);
  const roll = isReadonly ? null : findRoll(taxPlan, hex.id);
  // The hex that already holds the stronghold needs no "place" hint.
  const canPlace = isSetup && player !== null && !isStronghold && canPlaceStronghold(player.island, hex.id);

  return (
    <div ref={ref} className="hex-tooltip" style={{ left: `${anchor.x}px`, top: `${anchor.y}px` }}>
      <div className="hex-tooltip__title">
        {die ? <Icon src={die.art} size="m" /> : null}
        <span>{die ? die.label : biome.label}</span>
        {die ? <span className="hex-tooltip__subtitle">{biome.label}</span> : null}
      </div>

      {die ? (
        <DieFaces
          faces={die.faces}
          biomeFaceIndex={die.biomeFaceIndex}
          biomeLabel={biome.label}
          alwaysYields={die.alwaysYields}
          selectedIndex={roll ? roll.chosenIndex : -1}
          className="hex-tooltip__faces"
        />
      ) : null}

      {hex.building && !isTaxPhase ? (
        <div className="hex-tooltip__row">
          <Icon src={ICONS.population} label="Рабочие" />
          {`Рабочие: ${workersOn(hex)}/${workersNeeded(hex)}`}
        </div>
      ) : null}

      {hex.building && !isStaffed(hex) && !isTaxPhase ? (
        <div className="hex-tooltip__warning">
          {"Нет рабочих: здание стоит и ничего не производит"}
        </div>
      ) : null}

      {hex.building && !isStaffed(hex) && !isReadonly && isBuildPhase ? (
        <div className="hex-tooltip__action">
          {"Клик — открыть карточку и назначить рабочего"}
        </div>
      ) : null}

      {canPlace ? (
        <div className="hex-tooltip__action">
          {"Клик — поставить твердыню"}
        </div>
      ) : null}

      {hex.toxicity > 0 ? (
        <div className="hex-tooltip__toxicity">
          {`Токсичность гекса: ${Math.round(hex.toxicity)}%`}
        </div>
      ) : null}

      {isDead(hex) ? (
        <div className="hex-tooltip__warning">
          {"Гекс мёртв: ничего не производит"}
        </div>
      ) : null}

      {!isDead(hex) && isFoodBlocked(hex) ? (
        <div className="hex-tooltip__warning">
          {"Токсичность выше 50%: еду здесь не вырастить"}
        </div>
      ) : null}
    </div>
  );
};

export { HexTooltip };
