import { useSignals } from "@preact/signals-react/runtime";
import { useEffect, useRef } from "react";
import { FACTIONS, getFaction, NEXUS_ARCOLOGY } from "../../core/factions";
import { useStore } from "../../store/store";
import { useFocusTrap } from "./use-focus-trap";
import type { FC } from "react";
import type { TCloseFactionsModalAction, TSelectFactionAction } from "../../domain/registry";

type TFactionsModalRegistrySlice = {
  closeFactionsModalAction: TCloseFactionsModalAction;
  selectFactionAction: TSelectFactionAction;
};

type TFactionsModalProps = {
  registry: TFactionsModalRegistrySlice;
};

const TITLE_ID = "factions-modal-title";

/**
 * The lore of the six factions. The list on the left picks a faction, and the
 * right side shows its emblem, lore, strength and weakness. Escape, the
 * backdrop and the close button shut it.
 */
const FactionsModal: FC<TFactionsModalProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const factionId = store.ui.factionsModalFactionId.value;
  const dialogRef = useRef<HTMLDivElement>(null);
  const onTrapKeyDown = useFocusTrap(dialogRef);
  const isOpen = factionId !== null;

  // The selected faction's list item takes focus when the modal opens.
  // Escape closes the modal wherever the focus is.
  useEffect(() => {
    if (!isOpen) {
      return;
    }

    dialogRef.current?.querySelector<HTMLElement>(".factions-list__item--active")?.focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        registry.closeFactionsModalAction();
      }
    };

    window.addEventListener("keydown", onKeyDown);

    return () => {
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [isOpen, registry]);

  if (!factionId) {
    return null;
  }

  const faction = getFaction(factionId);

  return (
    <div className="modal-backdrop" onClick={registry.closeFactionsModalAction}>
      <div
        ref={dialogRef}
        className="panel modal factions-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={TITLE_ID}
        onClick={(click) => click.stopPropagation()}
        onKeyDown={onTrapKeyDown}
      >
        <h2 className="modal__title" id={TITLE_ID}>
          {"Фракции"}
        </h2>

        <div className="factions-modal__body">
          <ul className="factions-list">
            {FACTIONS.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  className={`factions-list__item ${item.id === factionId ? "factions-list__item--active" : ""}`}
                  aria-pressed={item.id === factionId}
                  onClick={() => registry.selectFactionAction(item.id)}
                >
                  <img className="factions-list__icon" src={item.icon} alt="" draggable={false} />

                  <span className="factions-list__text">
                    <span className="factions-list__name">
                      {item.name}
                    </span>

                    <span className="factions-list__tagline">
                      {item.tagline}
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>

          <section className="faction-details" aria-live="polite">
            <header className="faction-details__head">
              <img className="faction-details__icon" src={faction.iconLarge} alt="" draggable={false} />

              <div>
                <h3 className="faction-details__name">
                  {faction.name}
                </h3>

                <p className="faction-details__tagline">
                  {faction.id === "helios" ? `${faction.tagline} · ${NEXUS_ARCOLOGY.name}` : faction.tagline}
                </p>
              </div>
            </header>

            <p className="faction-details__summary">
              {faction.summary}
            </p>

            <ul className="faction-details__lore">
              {faction.lore.map((line) => (
                <li key={line}>
                  {line}
                </li>
              ))}
            </ul>

            <p className="faction-details__trait faction-details__trait--strength">
              <b>{"Сильная сторона: "}</b>
              {faction.strength}
            </p>

            <p className="faction-details__trait faction-details__trait--weakness">
              <b>{"Слабость: "}</b>
              {faction.weakness}
            </p>
          </section>
        </div>

        <div className="modal__buttons">
          <button type="button" className="button" onClick={registry.closeFactionsModalAction}>
            {"Закрыть"}
          </button>
        </div>
      </div>
    </div>
  );
};

export { FactionsModal };
