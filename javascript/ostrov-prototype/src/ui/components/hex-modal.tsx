import { useSignals } from "@preact/signals-react/runtime";
import { getBiome } from "../../core/biomes";
import {
  bestBuildingForBiome,
  buildingsForBiome,
  getBuilding,
} from "../../core/buildings";
import { hexCornerPoints } from "../../core/hex";
import { ICONS } from "../../core/icons";
import { workersNeeded, workersOn, populationOf } from "../../core/population";
import { hexDie } from "../../core/dice";
import { soilCleanseRefusal } from "../../core/soil-cleanse";
import { isStrongholdHex, STRONGHOLD_ART, STRONGHOLD_LABEL } from "../../core/stronghold";
import { structureHp } from "../../core/structure-hp";
import { useStore } from "../../store/store";
import { DieFaces } from "./die-faces";
import { Icon } from "./icon";
import { useState } from "react";
import type { AnimationEvent, FC, ReactNode } from "react";
import type { TBiome, THex, TPlayer } from "../../core/types";
import type { THexDie } from "../../core/dice";
import type { TSoilCleanseMode } from "../../store/ui-state";
import type {
  TArmBuildingAction,
  TChangeArmyAction,
  TCloseHexModalAction,
  TRequestDemolishAction,
  TSetHexWorkersAction,
  TToggleSoilCleanseAction,
} from "../../domain/registry";

/** Radius of the biome hex drawn behind the art in the header. */
const EMBLEM_SIZE = 48;
const EMBLEM_POINTS = hexCornerPoints(EMBLEM_SIZE);

type THexModalRegistrySlice = {
  armBuildingAction: TArmBuildingAction;
  changeArmyAction: TChangeArmyAction;
  closeHexModalAction: TCloseHexModalAction;
  requestDemolishAction: TRequestDemolishAction;
  setHexWorkersAction: TSetHexWorkersAction;
  toggleSoilCleanseAction: TToggleSoilCleanseAction;
};

type THexModalProps = {
  registry: THexModalRegistrySlice;
};

/**
 * Everything the sections need, computed once per render of the panel. Each
 * section reads it and decides by itself whether it applies.
 */
type TPanelContext = {
  hex: THex;
  biome: TBiome;
  player: TPlayer | null;
  isReadonly: boolean;
  isBuildPhase: boolean;
  isStronghold: boolean;
  die: THexDie | null;
  registry: THexModalRegistrySlice;
  soilMode: TSoilCleanseMode | null;
  isSoilUsed: boolean;
};

type TPanelSectionProps = {
  /** The small gold label over the section. No label means no header row. */
  title?: string;
  className?: string;
  children: ReactNode;
};

/** One category of the panel: a card with an optional small gold label. */
const PanelSection: FC<TPanelSectionProps> = ({ title, className = "", children }) => {
  return (
    <section className={`hex-section ${className}`}>
      {title ? (
        <h3 className="hex-section__title">
          {title}
        </h3>
      ) : null}

      {children}
    </section>
  );
};

type TButtonHintProps = {
  /** The hover text: the refusal reason on a disabled button, a short description otherwise. */
  hint: string;
  /** "block" stretches a wide button; "end" opens the hint below the button, aligned right. */
  variant?: "block" | "end";
  children: ReactNode;
};

/**
 * A button with a hover hint in the shared `.hint` style. A disabled button
 * gets no mouse events, so the wrapper carries the hover.
 */
const ButtonHint: FC<TButtonHintProps> = ({ hint, variant, children }) => {
  return (
    <span className={`hex-hint ${variant ? `hex-hint--${variant}` : ""}`}>
      {children}

      <span className="hint hex-hint__box" role="tooltip">
        <span className="hint__row">
          {hint}
        </span>
      </span>
    </span>
  );
};

/** One short grey line under the controls of a section. */
const SectionNote: FC<{ children: ReactNode }> = ({ children }) => {
  return (
    <p className="hex-section__note">
      {children}
    </p>
  );
};

/**
 * The header: a hex picture, the name with the biome on the same line, and
 * the hp of what stands on the hex. The long biome text is the tooltip of
 * the picture.
 */
const HexHeaderSection: FC<{ ctx: TPanelContext }> = ({ ctx }) => {
  const { hex, biome, player, isStronghold, isReadonly, isBuildPhase, registry } = ctx;
  const building = hex.building ? getBuilding(hex.building) : null;
  const name = isStronghold ? STRONGHOLD_LABEL : building?.label ?? biome.label;
  const art = isStronghold ? STRONGHOLD_ART : building?.art ?? null;
  const hp = structureHp(player, hex);
  const canDemolish = building !== null && !isStronghold && !isReadonly;

  let hpNote = "";
  if (hp && hp.hp <= 0) {
    hpNote = "Руины: не приносит дохода, пока не починится";
  } else if (hp && hp.hp < hp.max) {
    hpNote = "Чинится на 25% в конце хода";
  }

  return (
    <PanelSection className="hex-section--header">
      <div className="hex-head">
        <div className="hex-head__pic" title={biome.description}>
          <svg className="hex-head__emblem" viewBox="-50 -50 100 100" role="presentation">
            <polygon points={EMBLEM_POINTS} fill={biome.color} stroke={biome.edgeColor} strokeWidth={5} />
          </svg>

          {art ? <img className="hex-head__art" src={art} alt="" draggable={false} /> : null}
        </div>

        <div className="hex-head__text">
          <h2 className="hex-head__title">
            <span className="hex-head__name">{name}</span>

            <span className="hex-head__biome" title={biome.description}>
              {art ? biome.label : "Пустой гекс"}
            </span>
          </h2>

          {hp ? (
            <span
              className={`hex-head__hp ${hp.hp < hp.max ? "hex-head__hp--damaged" : ""}`}
              title={hpNote === "" ? "Прочность" : `Прочность. ${hpNote}`}
            >
              <span className="hex-head__hp-bar">
                <span className="hex-head__hp-fill" style={{ width: `${Math.max(0, (hp.hp / hp.max) * 100)}%` }} />
              </span>

              {hp.hp <= 0 ? "руины" : `${Math.round(hp.hp)}/${hp.max}`}
            </span>
          ) : null}
        </div>

        {canDemolish ? (
          <ButtonHint
            variant="end"
            hint={isBuildPhase ? "Снести здание" : "Сносить можно только в фазе строительства"}
          >
            <button
              type="button"
              className="hex-head__demolish"
              disabled={!isBuildPhase}
              onClick={() => registry.requestDemolishAction(hex.id)}
            >
              <Icon src={ICONS.demolish} label="Снести" />
            </button>
          </ButtonHint>
        ) : null}
      </div>
    </PanelSection>
  );
};

/** The die of the building or the stronghold: the faces two per row, then what comes every turn. */
const DiceSection: FC<{ ctx: TPanelContext }> = ({ ctx }) => {
  const { die, hex, biome } = ctx;
  if (!die) {
    return null;
  }

  return (
    <PanelSection className="hex-section--dice">
      <DieFaces
        faces={die.faces}
        biomeFaceIndex={die.biomeFaceIndex}
        biomeLabel={biome.label}
        hex={hex}
        alwaysYields={die.alwaysYields}
      />
    </PanelSection>
  );
};

/** On an empty hex: the buildings the biome allows, best first, each one arms the build. */
const SuitableSection: FC<{ ctx: TPanelContext }> = ({ ctx }) => {
  const { hex, isReadonly, isStronghold, registry } = ctx;
  if (hex.building || isStronghold) {
    return null;
  }

  const allowed = buildingsForBiome(hex.biome);
  const best = bestBuildingForBiome(hex.biome);

  return (
    <PanelSection title="Что построить" className="hex-section--suitable">
      {best ? (
        <SectionNote>
          {"Лучше всего здесь встанет: "}
          <strong>{best.label}</strong>
        </SectionNote>
      ) : (
        <SectionNote>
          {"Строить здесь нечего"}
        </SectionNote>
      )}

      {allowed.length > 0 ? (
        <ul className="hex-modal__list">
          {allowed.map((building) => (
            <li className="hex-modal__option" key={building.id}>
              <button
                type="button"
                className={`hex-modal__option-button ${building.id === best?.id ? "hex-modal__option-button--best" : ""}`}
                disabled={isReadonly}
                onClick={() => registry.armBuildingAction(building.id)}
              >
                <img className="hex-modal__option-art" src={building.art} alt={building.label} />

                <span className="hex-modal__option-name">
                  {building.label}
                </span>
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </PanelSection>
  );
};

/**
 * The workers of a production building. A building works only with all its
 * workers in place; the button moves one idle person in, or sends one back.
 */
const WorkersSection: FC<{ ctx: TPanelContext }> = ({ ctx }) => {
  const { hex, player, isReadonly, isBuildPhase, registry } = ctx;
  const needed = workersNeeded(hex);
  if (!player || !hex.building || needed <= 0) {
    return null;
  }

  const workers = workersOn(hex);
  const idle = populationOf(player).idle;
  const isStaffed = workers >= needed;
  const canChange = isBuildPhase && !isReadonly;

  let hint = isStaffed ? "Снятый рабочий станет свободным. Здание перестанет работать." : `Свободных жителей: ${idle}`;
  if (!isBuildPhase) {
    hint = "Менять можно только в фазе строительства";
  } else if (!isStaffed && idle <= 0) {
    hint = "Нет свободных жителей";
  }

  return (
    <PanelSection title="Рабочие" className={`hex-section--workers ${isStaffed ? "" : "hex-section--short"}`}>
      <div className="hex-section__control">
        <span className="hex-section__status">
          <Icon src={ICONS.population} label="Рабочие" />
          {`${workers}/${needed} — `}
          <strong>{isStaffed ? "работает" : "стоит"}</strong>
        </span>

        {isReadonly ? null : (
          <ButtonHint hint={hint}>
            <button
              type="button"
              className="button hex-section__button"
              disabled={!canChange || (!isStaffed && idle <= 0)}
              onClick={() => registry.setHexWorkersAction(hex.id, isStaffed ? workers - 1 : workers + 1)}
            >
              {isStaffed ? "Снять" : "Назначить"}
            </button>
          </ButtonHint>
        )}
      </div>
    </PanelSection>
  );
};

/**
 * The army lives in the stronghold. The buttons move one idle person into the
 * army, or one soldier back to idle. The whole army goes to the clearing phase.
 */
const ArmySection: FC<{ ctx: TPanelContext }> = ({ ctx }) => {
  const { player, isStronghold, isReadonly, isBuildPhase, registry } = ctx;
  if (!player || !isStronghold || isReadonly) {
    return null;
  }

  const army = player.army;
  const idle = populationOf(player).idle;
  const phaseRefusal = "Менять можно только в фазе строительства";
  let dismissHint = "Распустить солдата: он станет свободным жителем";
  if (!isBuildPhase) {
    dismissHint = phaseRefusal;
  } else if (army <= 0) {
    dismissHint = "В армии нет солдат";
  }

  let recruitHint = "Призвать свободного жителя в армию";
  if (!isBuildPhase) {
    recruitHint = phaseRefusal;
  } else if (idle <= 0) {
    recruitHint = "Нет свободных жителей";
  }

  return (
    <PanelSection title="Армия" className="hex-section--army">
      <div className="hex-section__control">
        <span className="hex-section__status">
          <Icon src={ICONS.militia} label="Ополченцы" />
          <strong>{army}</strong>
          <span className="hex-section__dim">{`· свободны: ${idle}`}</span>
        </span>

        <span className="hex-section__buttons">
          <ButtonHint hint={dismissHint}>
            <button
              type="button"
              className="button hex-section__button"
              disabled={!isBuildPhase || army <= 0}
              onClick={() => registry.changeArmyAction(-1)}
            >
              {"− Распустить"}
            </button>
          </ButtonHint>

          <ButtonHint hint={recruitHint}>
            <button
              type="button"
              className="button hex-section__button"
              disabled={!isBuildPhase || idle <= 0}
              onClick={() => registry.changeArmyAction(1)}
            >
              {"+ Призвать"}
            </button>
          </ButtonHint>
        </span>
      </div>

      <SectionNote>
        Вся армия идёт в бой в фазе зачистки
      </SectionNote>
    </PanelSection>
  );
};

/** The stronghold's soil cleansing: sacrifice one hex to purify another. */
const SoilSection: FC<{ ctx: TPanelContext }> = ({ ctx }) => {
  const { player, isStronghold, isReadonly, isBuildPhase, soilMode, isSoilUsed, registry } = ctx;
  if (!player || !isStronghold || isReadonly) {
    return null;
  }

  const block = isBuildPhase ? soilCleanseRefusal(player, isSoilUsed) : { message: "Доступно только в фазе строительства" };
  const isActive = soilMode !== null;

  return (
    <PanelSection title="Очистка почвы" className="hex-section--soil">
      <ButtonHint
        variant="block"
        hint={block && !isActive ? block.message : "Уничтожьте один гекс острова вместе со зданием на нём, и токсичность другого гекса упадёт до 0. Раз в ход."}
      >
        <button
          type="button"
          className={`button soil-button hex-section__wide ${isActive ? "soil-button--active" : ""}`}
          disabled={block !== null && !isActive}
          aria-pressed={isActive}
          onClick={registry.toggleSoilCleanseAction}
        >
          <Icon src={ICONS.toxicity} label="" />
          {isActive ? "Отменить очистку" : "Очистить почву"}
        </button>
      </ButtonHint>
    </PanelSection>
  );
};

/** The sections in their fixed order. Each one returns null when it does not apply. */
const SECTIONS: readonly FC<{ ctx: TPanelContext }>[] = [
  DiceSection,
  SuitableSection,
  WorkersSection,
  ArmySection,
  SoilSection,
];

/**
 * The panel that slides out of the right edge when a hex is clicked. It is a
 * fixed list of sections: the header always, then the die, the buildings to
 * build, the workers, the army and the soil cleansing when they apply to this
 * hex.
 *
 * On close the panel keeps the last hex and slides back out. It unmounts when
 * that animation ends. A click on another hex while the panel is open keeps
 * the panel in place; only the content fades in.
 */
const HexModal: FC<THexModalProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const hex = store.derived.selectedHex.value;
  const isReadonly = store.derived.isReadonly.value;
  const player = store.derived.viewedPlayer.value;

  // Read every signal before the early return, so the hooks keep their order.
  const soilMode = store.ui.soilCleanse.value;
  const isBuildPhase =
    store.game.stage.value === "play" &&
    store.game.phase.value === "build" &&
    !store.ui.busy.value &&
    !store.derived.isHumanReady.value;
  const isSoilUsed = store.game.soilCleansedTurn.value === store.game.turn.value;

  // The last selected hex stays on screen while the panel slides out.
  const [lastHex, setLastHex] = useState<THex | null>(hex);
  if (hex !== null && hex !== lastHex) {
    setLastHex(hex);
  }

  const shownHex = hex ?? lastHex;
  if (!shownHex) {
    return null;
  }

  const isClosing = hex === null;
  const handleAnimationEnd = (event: AnimationEvent<HTMLElement>) => {
    if (isClosing && event.target === event.currentTarget) {
      setLastHex(null);
    }
  };

  const ctx: TPanelContext = {
    hex: shownHex,
    biome: getBiome(shownHex.biome),
    player,
    isReadonly,
    isBuildPhase,
    isStronghold: isStrongholdHex(player, shownHex.id),
    die: hexDie(player, shownHex),
    registry,
    soilMode,
    isSoilUsed,
  };

  return (
    <aside
      className={`panel hex-modal ${isClosing ? "hex-modal--closing" : ""}`}
      onAnimationEnd={handleAnimationEnd}
    >
      <button type="button" className="hex-modal__close" onClick={registry.closeHexModalAction}>
        <Icon src={ICONS.close} label="Закрыть" size="m" />
      </button>

      <div className="hex-modal__content" key={shownHex.id}>
        <HexHeaderSection ctx={ctx} />

        <div className="hex-modal__body">
          {SECTIONS.map((Section, index) => (
            <Section key={index} ctx={ctx} />
          ))}
        </div>
      </div>
    </aside>
  );
};

export { HexModal };
