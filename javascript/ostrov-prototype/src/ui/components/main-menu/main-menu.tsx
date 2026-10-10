import { useSignals } from "@preact/signals-react/runtime";
import { useEffect, useRef, useState } from "react";
import emblemSrc from "../../../assets/menu/menu-emblem.png";
import { ICONS } from "../../../core/icons";
import { DIFFICULTIES } from "../../../core/main-menu";
import { useStore } from "../../../store/store";
import { FactionsModal } from "../factions-modal";
import { TechModal } from "../tech-modal";
import { MenuBackground } from "./menu-background";
import { MenuCredits } from "./menu-credits";
import type { FC } from "react";
import type { TDifficulty } from "../../../core/main-menu";
import type { TAppRegistry } from "../../../domain/registry";

type TMainMenuRegistrySlice = Pick<
  TAppRegistry,
  | "startFromMenuAction"
  | "exitMenuAction"
  | "setDifficultyAction"
  | "openMenuCreditsAction"
  | "closeMenuCreditsAction"
  | "openFactionsModalAction"
  | "selectFactionAction"
  | "closeFactionsModalAction"
  | "openTechModalAction"
  | "closeTechModalAction"
  | "researchTechAction"
>;

type TMainMenuProps = {
  registry: TMainMenuRegistrySlice;
};

type TMenuEntry = {
  readonly id: string;
  /** The small gold line, like "Chapter One". */
  readonly label: string;
  /** The large white line. */
  readonly title: string;
  readonly icon: string;
  readonly run: () => void;
};

const createEntries = (registry: TMainMenuRegistrySlice): readonly TMenuEntry[] => [
  {
    id: "prologue",
    label: "Пролог",
    title: "Обучение",
    icon: ICONS.scouting,
    run: () => registry.startFromMenuAction({ guide: true }),
  },
  {
    id: "chapter-one",
    label: "Глава первая",
    title: "Новая игра",
    icon: ICONS.stronghold,
    run: () => registry.startFromMenuAction({ guide: false }),
  },
  {
    id: "chronicle",
    label: "Летопись",
    title: "Фракции",
    icon: ICONS.factions,
    run: () => registry.openFactionsModalAction(),
  },
  {
    id: "archive",
    label: "Архив",
    title: "Технологии",
    icon: ICONS.technology,
    run: () => registry.openTechModalAction(),
  },
  {
    id: "epilogue",
    label: "Эпилог",
    title: "Авторы",
    icon: ICONS.population,
    run: () => registry.openMenuCreditsAction(),
  },
];

/**
 * The main menu, after the campaign screen of Warcraft III: a living hero
 * picture through a stone arch, the title block on the left, the list of
 * entries on the right and the exit in the lower left corner. It covers the
 * whole game. The game page is not mounted while the menu is open.
 */
const MainMenu: FC<TMainMenuProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const state = store.ui.mainMenu.value;
  const difficulty = store.ui.difficulty.value;
  const isCreditsOpen = store.ui.menuCreditsOpen.value;
  const isOverlayOpen = isCreditsOpen
    || store.ui.techModalOpen.value
    || store.ui.factionsModalFactionId.value !== null;
  const isGuideOn = store.guide.enabled.value;

  const [entries] = useState(() => createEntries(registry));
  // The guide flag points the keyboard at the prologue, otherwise at a new game.
  const [activeIndex, setActiveIndex] = useState(() => (isGuideOn ? 0 : 1));
  const entryRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const activeIndexRef = useRef(activeIndex);
  activeIndexRef.current = activeIndex;

  const isOpen = state === "open";

  // The highlighted entry takes focus when the menu opens. It takes focus back
  // when a dialog closes and leaves the focus nowhere: the factions modal
  // stays mounted, so its focus trap does not return the focus.
  useEffect(() => {
    const active = document.activeElement;
    const isFocusLost = active === null || active === document.body;
    if (isOpen && !isOverlayOpen && isFocusLost) {
      entryRefs.current[activeIndexRef.current]?.focus({ preventScroll: true });
    }
  }, [isOpen, isOverlayOpen]);

  // While the menu fades out, the game is mounted under it. The menu still
  // owns the keyboard, so no hotkey of the game fires before the fade ends.
  useEffect(() => {
    if (state !== "leaving") {
      return;
    }

    const swallow = (event: KeyboardEvent) => {
      event.stopImmediatePropagation();
      event.preventDefault();
    };

    window.addEventListener("keydown", swallow, { capture: true });
    window.addEventListener("keyup", swallow, { capture: true });

    return () => {
      window.removeEventListener("keydown", swallow, { capture: true });
      window.removeEventListener("keyup", swallow, { capture: true });
    };
  }, [state]);

  // ↑/↓ (and Home/End) walk the list, Enter runs the highlighted entry, and
  // Escape closes the tech modal and the credits. The listener is on the
  // bubble phase, so a dialog that handles a key first keeps it.
  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const focusEntry = (index: number) => {
      const next = (index + entries.length) % entries.length;
      setActiveIndex(next);
      entryRefs.current[next]?.focus({ preventScroll: true });
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        if (store.ui.menuCreditsOpen.peek()) {
          registry.closeMenuCreditsAction();
        } else if (store.ui.techModalOpen.peek()) {
          registry.closeTechModalAction();
        }

        return;
      }

      const isOverlay = store.ui.menuCreditsOpen.peek()
        || store.ui.techModalOpen.peek()
        || store.ui.factionsModalFactionId.peek() !== null;
      const target = event.target;
      const isFormControl = target instanceof HTMLSelectElement || target instanceof HTMLInputElement;
      if (isOverlay || isFormControl) {
        return;
      }

      if (event.key === "ArrowDown") {
        event.preventDefault();
        focusEntry(activeIndexRef.current + 1);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        focusEntry(activeIndexRef.current - 1);
      } else if (event.key === "Home") {
        event.preventDefault();
        focusEntry(0);
      } else if (event.key === "End") {
        event.preventDefault();
        focusEntry(entries.length - 1);
      } else if (event.key === "Enter" && !(target instanceof HTMLButtonElement) && !event.repeat) {
        event.preventDefault();
        entries[activeIndexRef.current]?.run();
      }
    };

    window.addEventListener("keydown", onKeyDown);

    return () => {
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [isOpen, entries, registry, store]);

  if (state === "closed") {
    return null;
  }

  return (
    <div className={`main-menu main-menu--${state}`}>
      <MenuBackground />

      <div className="main-menu__shade" aria-hidden="true" />

      <div className="main-menu__layout" inert={!isOpen || isOverlayOpen}>
        <header className="main-menu__title-block">
          <img className="main-menu__emblem" src={emblemSrc} alt="" draggable={false} />

          <p className="main-menu__heading">
            {"Кампания управляющего"}
          </p>

          <h1 className="main-menu__title">
            {"Остров"}
          </h1>

          <p className="main-menu__subtitle">
            {"Бремя отходов"}
          </p>

          <div className="main-menu-bar">
            <label className="main-menu-bar__field" title="Пока не влияет на правила: выбор запоминается до конца сессии">
              <span className="main-menu-bar__label">
                {"Сложность"}
              </span>

              <select
                className="main-menu-bar__select"
                value={difficulty}
                onChange={(change) => registry.setDifficultyAction(change.target.value as TDifficulty)}
              >
                {DIFFICULTIES.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>

            <button
              type="button"
              className="main-menu-bar__button"
              aria-label="Летопись фракций"
              title="Летопись фракций"
              onClick={() => registry.openFactionsModalAction()}
            >
              <img className="main-menu-bar__icon" src={ICONS.factions} alt="" draggable={false} />
            </button>
          </div>
        </header>

        <nav className="main-menu__nav" aria-label="Главное меню">
          <ul className="main-menu__list">
            {entries.map((entry, index) => (
              <li key={entry.id}>
                <button
                  ref={(node) => {
                    entryRefs.current[index] = node;
                  }}
                  type="button"
                  className={`main-menu-entry ${index === activeIndex ? "main-menu-entry--active" : ""}`}
                  onClick={entry.run}
                  onFocus={() => setActiveIndex(index)}
                  onMouseEnter={() => setActiveIndex(index)}
                >
                  <span className="main-menu-entry__frame" aria-hidden="true">
                    <img className="main-menu-entry__icon" src={entry.icon} alt="" draggable={false} />
                  </span>

                  <span className="main-menu-entry__text">
                    <span className="main-menu-entry__label">
                      {entry.label}
                    </span>

                    <span className="main-menu-entry__title">
                      {entry.title}
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </nav>

        <div className="main-menu__footer">
          <button
            type="button"
            className="main-menu-button"
            title="Перезагрузить игру без главного меню"
            onClick={registry.exitMenuAction}
          >
            {"Выход"}
          </button>
        </div>
      </div>

      {isOpen ? (
        <div className="main-menu__overlays">
          <FactionsModal registry={registry} />

          <TechModal registry={registry} />

          {isCreditsOpen ? <MenuCredits registry={registry} /> : null}
        </div>
      ) : null}
    </div>
  );
};

export { MainMenu };
