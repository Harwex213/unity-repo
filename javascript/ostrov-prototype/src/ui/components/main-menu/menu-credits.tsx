import { useEffect, useRef } from "react";
import { useFocusTrap } from "../use-focus-trap";
import type { FC } from "react";
import type { TCloseMenuCreditsAction } from "../../../domain/registry";

type TMenuCreditsProps = {
  registry: {
    closeMenuCreditsAction: TCloseMenuCreditsAction;
  };
};

const TITLE_ID = "menu-credits-title";

/** The credits of the "Эпилог" entry. The text is a placeholder. */
const MenuCredits: FC<TMenuCreditsProps> = ({ registry }) => {
  const dialogRef = useRef<HTMLDivElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const onTrapKeyDown = useFocusTrap(dialogRef);

  useEffect(() => {
    closeRef.current?.focus({ preventScroll: true });
  }, []);

  return (
    <div className="modal-backdrop main-menu-credits__backdrop" onClick={registry.closeMenuCreditsAction}>
      <div
        ref={dialogRef}
        className="main-menu-credits"
        role="dialog"
        aria-modal="true"
        aria-labelledby={TITLE_ID}
        onClick={(click) => click.stopPropagation()}
        onKeyDown={onTrapKeyDown}
      >
        <span className="main-menu-credits__label">
          {"Эпилог"}
        </span>

        <h2 className="main-menu-credits__title" id={TITLE_ID}>
          {"Авторы"}
        </h2>

        <dl className="main-menu-credits__list">
          <dt>{"Замысел и правила"}</dt>
          <dd>{"Команда «Острова»"}</dd>
          <dt>{"Код прототипа"}</dt>
          <dd>{"Команда «Острова»"}</dd>
          <dt>{"Иллюстрации"}</dt>
          <dd>{"Команда «Острова»"}</dd>
        </dl>

        <p className="main-menu-credits__note">
          {"Текст-заглушка: список авторов появится позже."}
        </p>

        <button ref={closeRef} type="button" className="main-menu-button" onClick={registry.closeMenuCreditsAction}>
          {"Закрыть"}
        </button>
      </div>
    </div>
  );
};

export { MenuCredits };
