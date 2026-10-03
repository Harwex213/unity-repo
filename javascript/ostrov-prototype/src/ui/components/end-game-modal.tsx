import { useSignals } from "@preact/signals-react/runtime";
import { useEffect, useRef } from "react";
import { getBuilding } from "../../core/buildings";
import { ICONS } from "../../core/icons";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import { useFocusTrap } from "./use-focus-trap";
import type { FC } from "react";
import type { TGameOutcome, TGameOutcomeKind, TGameOutcomeReason } from "../../core/game-over";
import type { TPlayer } from "../../core/types";
import type { TNewGameAction } from "../../domain/registry";

type TEndGameModalRegistrySlice = {
  newGameAction: TNewGameAction;
};

type TEndGameModalProps = {
  registry: TEndGameModalRegistrySlice;
};

const TITLE_ID = "end-game-title";

const TITLES: Readonly<Record<TGameOutcomeKind, string>> = {
  victory: "Победа",
  defeat: "Поражение",
  shared: "Общая победа",
};

const ART: Readonly<Record<TGameOutcomeKind, string>> = {
  victory: getBuilding("converter").hexArt,
  shared: getBuilding("converter").hexArt,
  defeat: ICONS.dead,
};

const names = (players: readonly TPlayer[], ids: readonly string[]) => {
  return players.filter((player) => ids.includes(player.id)).map((player) => player.nickname).join(", ");
};

const describe = (reason: TGameOutcomeReason, outcome: TGameOutcome, players: readonly TPlayer[], humanId: string) => {
  const rivals = names(players, outcome.winnerIds.filter((id) => id !== humanId));

  switch (reason) {
    case "converter":
      return "Центральный конвертер запущен. Ваши здания больше не отравляют остров: яд побеждён.";
    case "shared-converter":
      return `Вы и ${rivals} запустили Центральный конвертер в один ход. Яд побеждён общими силами.`;
    case "elimination":
      return "Все соперники выбыли. Небо над островами принадлежит вам.";
    case "ruined":
      return "Твердыня лежит в руинах. Власть над островом потеряна.";
    case "no-buildings":
      return "На острове не осталось ни одного здания. Жить здесь больше некому.";
    case "rival-converter":
      return `${rivals} первым запустил Центральный конвертер и победил яд. Вы не успели.`;
  }
};

/**
 * The end screen. It opens over every page once the game is over, and the
 * turn loop stops under it. "Новая игра" starts a fresh session.
 */
const EndGameModal: FC<TEndGameModalProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const outcome = store.game.outcome.value;
  const players = store.game.players.value;
  const humanId = store.game.humanPlayerId.value;
  const dialogRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const onTrapKeyDown = useFocusTrap(dialogRef);

  useEffect(() => {
    if (outcome) {
      buttonRef.current?.focus();
    }
  }, [outcome]);

  if (!outcome) {
    return null;
  }

  return (
    <div className={`modal-backdrop end-game-backdrop end-game-backdrop--${outcome.kind}`}>
      <div
        ref={dialogRef}
        className={`panel modal end-game end-game--${outcome.kind}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby={TITLE_ID}
        onKeyDown={onTrapKeyDown}
      >
        <div className="end-game__art-wrap">
          <img className="end-game__art" src={ART[outcome.kind]} alt="" />
        </div>

        <h2 className="end-game__title" id={TITLE_ID}>
          {TITLES[outcome.kind]}
        </h2>

        <p className="end-game__text">
          {describe(outcome.reason, outcome, players, humanId)}
        </p>

        <p className="end-game__turn">
          <Icon src={ICONS.hammers} />
          {`Ход ${outcome.turn}`}
        </p>

        <div className="modal__buttons end-game__buttons">
          <button
            ref={buttonRef}
            type="button"
            className="button button--primary"
            onClick={registry.newGameAction}
          >
            {"Новая игра"}
          </button>
        </div>
      </div>
    </div>
  );
};

export { EndGameModal };
