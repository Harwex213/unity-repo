import { useSignals } from "@preact/signals-react/runtime";
import { useEffect, useRef, useState } from "react";
import { ICONS } from "../../core/icons";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import { TechSheet } from "./tech-sheet";
import { useFocusTrap } from "./use-focus-trap";
import type { FC, KeyboardEvent as ReactKeyboardEvent } from "react";
import type { TTechId } from "../../core/techs";
import type { TCloseTechModalAction, TResearchTechAction } from "../../domain/registry";

type TTechModalRegistrySlice = {
  closeTechModalAction: TCloseTechModalAction;
  researchTechAction: TResearchTechAction;
};

type TTechModalProps = {
  registry: TTechModalRegistrySlice;
};

const TITLE_ID = "tech-modal-title";

/**
 * The open dialog. It mounts when the modal opens, so the focus trap
 * remembers the tools button and gives focus back to it on close.
 */
const TechDialog: FC<TTechModalProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const researched = store.game.researched.value;
  const science = store.derived.humanPlayer.value?.resources.science ?? 0;
  const isWaiting = store.derived.isHumanReady.value;
  const dialogRef = useRef<HTMLDivElement>(null);
  const onTrapKeyDown = useFocusTrap(dialogRef);
  const [selectedId, setSelectedId] = useState<TTechId | null>(null);

  // The first node that can be researched takes focus, or the close button.
  useEffect(() => {
    const dialog = dialogRef.current;
    const target =
      dialog?.querySelector<HTMLElement>(".tech-node--ready") ?? dialog?.querySelector<HTMLElement>(".tech-modal__close");
    target?.focus({ preventScroll: true });
  }, []);

  // Escape closes the open card first. The island page closes the modal on
  // the next Escape.
  const onKeyDown = (event: ReactKeyboardEvent) => {
    if (event.key === "Escape" && selectedId) {
      event.stopPropagation();
      setSelectedId(null);

      return;
    }

    onTrapKeyDown(event);
  };

  return (
    <div className="modal-backdrop" onClick={registry.closeTechModalAction}>
      <div
        ref={dialogRef}
        className="panel modal tech-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={TITLE_ID}
        onClick={(click) => click.stopPropagation()}
        onKeyDown={onKeyDown}
      >
        <h2 className="modal__title" id={TITLE_ID}>
          {"Технологии — "}
          <Icon src={ICONS.science} label="Наука" size="m" />
          {science}
        </h2>

        <TechSheet
          researched={researched}
          science={science}
          isWaiting={isWaiting}
          selectedId={selectedId}
          onSelect={setSelectedId}
          onResearch={registry.researchTechAction}
        />

        <div className="modal__buttons tech-modal__footer">
          <span className="tech-modal__hint">
            {"Колесо — масштаб, перетаскивание — сдвиг, двойной щелчок — весь план, щелчок по узлу — подробности"}
          </span>

          <button type="button" className="button tech-modal__close" onClick={registry.closeTechModalAction}>
            {"Закрыть"}
          </button>
        </div>
      </div>
    </div>
  );
};

/**
 * The technology tree, drawn as a sheet the way the `ostrov-tech` reference
 * draws it. The spec leaves the technology screen blank, so the tree itself is
 * invented. Every technology pays out into a phase that exists: units for the
 * clearing phase, a discount for the build phase, cleaner air for the tax phase.
 */
const TechModal: FC<TTechModalProps> = ({ registry }) => {
  useSignals();
  const store = useStore();

  if (!store.ui.techModalOpen.value) {
    return null;
  }

  return <TechDialog registry={registry} />;
};

export { TechModal };
