import { useSignals } from "@preact/signals-react/runtime";
import { getBiome } from "../../core/biomes";
import { getFaction, NEXUS_ARCOLOGY } from "../../core/factions";
import { ICONS } from "../../core/icons";
import { eventChance } from "../../core/trail-events";
import { cellVisibility, moveCheck, scoutCheck } from "../../core/world-rules";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import type { FC } from "react";
import type { TMoveIslandAction, TOpenFactionsModalAction, TScoutAction } from "../../domain/registry";

type TWorldCellPanelRegistrySlice = {
  moveIslandAction: TMoveIslandAction;
  openFactionsModalAction: TOpenFactionsModalAction;
  scoutAction: TScoutAction;
};

type TWorldCellPanelProps = {
  registry: TWorldCellPanelRegistrySlice;
};

const VISIBILITY_TITLE = {
  frontier: "Неразведанный гекс",
  fogged: "Гекс в тумане",
} as const;

const KIND_TITLE = {
  void: "Облака",
  island: "Дикий остров",
  settlement: "Поселение",
} as const;

/**
 * What the selected cell is worth: the scouting report, the faction that holds
 * it, the toxic trail left in it, and the two things the spec lets a player do
 * in this phase. Scouting costs more the farther the cell is from the island.
 * The panel stands bottom centre in three columns: the report, the numbers,
 * the buttons.
 */
const WorldCellPanel: FC<TWorldCellPanelProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const world = store.world.world.value;
  const cell = store.derived.selectedCell.value;
  const here = store.derived.currentCell.value;
  const player = store.derived.humanPlayer.value;
  const moved = store.world.movedThisTurn.value;
  const players = store.game.players.value;
  // Scouting and the flight belong to the scout phase, before "Готов".
  const isLocked = store.game.phase.value !== "scout" || store.derived.isHumanReady.value;

  if (!world || !player || !here) {
    return null;
  }

  // The panel keeps its box with no cell selected, so the bar does not jump.
  if (!cell) {
    return (
      <aside className="panel world-panel" data-tutorial="scout">
        <div className="world-panel__main">
          <h2 className="world-panel__title">
            {"Гекс не выбран"}
          </h2>

          <p className="world-panel__row">
            {"Кликните по гексу на глобусе: здесь появятся разведка, фракция и перелёт."}
          </p>
        </div>
      </aside>
    );
  }

  const isHere = cell.id === here.id;
  const visibility = cellVisibility(world, cell);
  const scout = scoutCheck(world, player.cellId, cell.id, player.resources.scouting);
  const move = moveCheck(world, player.cellId, cell.id, moved);
  const owner = cell.revealed && cell.ownerId ? players.find((candidate) => candidate.id === cell.ownerId) : null;
  const isLair = cell.revealed && cell.boss;
  const title = isLair
    ? "Логово Повелителя Мора"
    : isHere
      ? "Ваш гекс"
      : owner
        ? `Остров: ${owner.nickname}`
        : visibility === "revealed"
          ? KIND_TITLE[cell.kind]
          : VISIBILITY_TITLE[visibility];

  const faction = cell.revealed && cell.faction ? getFaction(cell.faction) : null;
  const isHeld = cell.kind === "island" && !cell.cleared && cell.islandCount > 0;

  return (
    <aside className="panel world-panel" data-tutorial="scout">
      <div className="world-panel__main">
        <h2 className={`world-panel__title ${isLair ? "world-panel__title--boss" : ""}`}>
          {title}
        </h2>

        {cell.revealed && cell.kind === "void" ? (
          <p className="world-panel__row">
            {"Облака над морем. Здесь пусто, но пролететь можно."}
          </p>
        ) : null}

        {cell.revealed && cell.kind === "settlement" ? (
          <p className="world-panel__row world-cell__row">
            <Icon src={ICONS.village} label="Поселение" />
            {"Поселение — скоро"}
          </p>
        ) : null}

        {isLair ? (
          <>
            <p className="world-panel__row world-cell__row world-cell__boss">
              <Icon src={ICONS.boss} label="Босс" />
              {player.bossSlain ? "Повелитель Мора повержен вами" : "Здесь ждёт босс — Повелитель Мора"}
            </p>

            <p className="world-panel__hint">
              {player.bossSlain
                ? "Трофей получен: стройте Центральный конвертер на своём острове."
                : "Прилетите сюда — в конце хода начнётся бой с боссом. Победа даёт технологию «Центральная конверсия» и Центральный конвертер."}
            </p>
          </>
        ) : null}

        {cell.revealed && cell.kind === "island" && !cell.boss ? (
          <>
            <p className="world-panel__row world-cell__row">
              <Icon src={cell.cleared ? ICONS.check : ICONS.skeleton} label="Дикие острова" />
              {cell.cleared || cell.islandCount === 0
                ? `${getBiome(cell.biome).label} · зачищен`
                : `${getBiome(cell.biome).label} · диких островов: ${cell.islandCount}`}
            </p>

            {cell.cleared || cell.islandCount === 0 ? null : (
              <p className="world-panel__hint">
                {cell.activated
                  ? "Острова проснулись: зачистка в конце хода, пока стоите здесь."
                  : "Прилетите сюда — острова проснутся, и в конце хода начнётся зачистка."}
              </p>
            )}
          </>
        ) : null}

        {cell.revealed ? null : (
          <p className="world-panel__row">
            {"Под туманом облака, остров или поселение. Разведка покажет, что здесь."}
          </p>
        )}

        {cell.revealed || scout.ok ? null : (
          <p className="world-panel__hint world-cell__reason">
            {scout.reason}
          </p>
        )}
      </div>

      {cell.revealed ? (
        <div className="world-panel__stats">
          {faction ? (
            <button
              type="button"
              className="world-cell__faction"
              title={`Фракция: ${faction.name}`}
              onClick={() => registry.openFactionsModalAction(faction.id)}
            >
              <Icon src={faction.icon} size="m" />

              <span className="world-cell__faction-text">
                <span className="world-cell__faction-name">
                  {faction.id === "helios" ? `${faction.name} · ${NEXUS_ARCOLOGY.name}` : faction.name}
                </span>

                <span className="world-cell__faction-note">
                  {isHeld || cell.boss ? "Удерживает остров" : "Был выбит отсюда"}
                </span>
              </span>
            </button>
          ) : null}

          {owner ? (
            <p className="world-panel__row world-cell__row" style={{ color: owner.color }}>
              <Icon src={ICONS.stronghold} label="Остров игрока" />
              {`Здесь стоит ${owner.nickname}`}
            </p>
          ) : null}

          <p className="world-panel__row world-panel__trail world-cell__row">
            <Icon src={ICONS.toxicity} label="Токсичный шлейф" />
            {`Токсичный шлейф: ${Math.round(cell.toxicTrail)}`}
          </p>

          <p className="world-panel__hint">
            {`Шанс события из шлейфа: ${Math.round(eventChance(cell.toxicTrail) * 100)}%`}
          </p>
        </div>
      ) : null}

      <div className="world-panel__buttons">
        {cell.revealed ? null : (
          <button
            type="button"
            className="button world-cell__scout"
            disabled={isLocked || !scout.ok}
            onClick={() => registry.scoutAction(cell.id)}
          >
            {"Разведать гекс"}
            <span className="world-cell__cost">
              <Icon src={ICONS.scouting} label="Разведка" />
              {`${scout.cost}`}
            </span>
          </button>
        )}

        {isHere ? null : (
          <button
            type="button"
            className="button button--primary"
            disabled={isLocked || !move.ok}
            onClick={() => registry.moveIslandAction(cell.id)}
          >
            {moved ? "Уже перелетали" : "Перелететь сюда"}
          </button>
        )}
      </div>
    </aside>
  );
};

export { WorldCellPanel };
