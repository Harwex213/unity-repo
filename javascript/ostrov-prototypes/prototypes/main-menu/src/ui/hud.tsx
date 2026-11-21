import { useCallback, useEffect, useRef, useState } from "react";
import logoSrc from "../assets/logo.webp";
import sigilSrc from "../assets/sigil.webp";
import { DIFFICULTIES, MENU_ENTRIES } from "./menu-data";
import type { FC } from "react";
import type { TMenuEntry } from "./menu-data";

/** How long the stub of a chosen entry stays on the screen. */
const CARD_MS = 2300;

type THudProps = {
  readonly isVisible: boolean;
  readonly isMuted: boolean;
  readonly onHover: () => void;
  readonly onConfirm: () => void;
  readonly onReplay: () => void;
  readonly onToggleMute: () => void;
};

/**
 * The menu has a slow, heavy order of entrance. The sigil comes first, the name
 * burns in, then the rule and the entries follow one by one. The delays are in
 * the CSS and hang on the `hud--visible` class.
 */
const Hud: FC<THudProps> = ({ isVisible, isMuted, onHover, onConfirm, onReplay, onToggleMute }) => {
  const [activeIndex, setActiveIndex] = useState(1);
  const [difficulty, setDifficulty] = useState(1);
  const [card, setCard] = useState<TMenuEntry | null>(null);
  const entryRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const cardTimer = useRef(0);
  const isInteractive = isVisible && card === null;

  const moveTo = useCallback((index: number) => {
    const next = (index + MENU_ENTRIES.length) % MENU_ENTRIES.length;
    setActiveIndex((current) => {
      if (current !== next) {
        onHover();
      }

      return next;
    });
    entryRefs.current[next]?.focus({ preventScroll: true });
  }, [onHover]);

  const choose = useCallback((entry: TMenuEntry) => {
    onConfirm();
    setCard(entry);
    cardTimer.current = window.setTimeout(() => setCard(null), CARD_MS);
  }, [onConfirm]);

  const shiftDifficulty = useCallback((step: number) => {
    setDifficulty((current) => (current + step + DIFFICULTIES.length) % DIFFICULTIES.length);
    onHover();
  }, [onHover]);

  useEffect(() => () => window.clearTimeout(cardTimer.current), []);

  useEffect(() => {
    if (isInteractive) {
      const timer = window.setTimeout(() => {
        entryRefs.current[activeIndex]?.focus({ preventScroll: true });
      }, 60);

      return () => window.clearTimeout(timer);
    }

    return undefined;
  }, [isInteractive, activeIndex]);

  useEffect(() => {
    if (!isInteractive) {
      return undefined;
    }

    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === "ArrowDown") {
        event.preventDefault();
        moveTo(activeIndex + 1);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        moveTo(activeIndex - 1);
      } else if (event.key === "Home") {
        event.preventDefault();
        moveTo(0);
      } else if (event.key === "End") {
        event.preventDefault();
        moveTo(MENU_ENTRIES.length - 1);
      } else if (event.key === "ArrowLeft") {
        event.preventDefault();
        shiftDifficulty(-1);
      } else if (event.key === "ArrowRight") {
        event.preventDefault();
        shiftDifficulty(1);
      } else if (event.key === "r" || event.key === "R" || event.key === "к" || event.key === "К") {
        onReplay();
      } else if (event.key === "m" || event.key === "M" || event.key === "ь" || event.key === "Ь") {
        onToggleMute();
      }
    };

    window.addEventListener("keydown", onKeyDown);

    return () => window.removeEventListener("keydown", onKeyDown);
  }, [activeIndex, isInteractive, moveTo, onReplay, onToggleMute, shiftDifficulty]);

  return (
    <div className={`hud ${isVisible ? "hud--visible" : ""} ${card ? "hud--card" : ""}`} inert={!isInteractive}>
      <div className="hud__shade" aria-hidden="true" />

      <div className="hud__column">
        <header className="hud__title">
          <img className="hud__sigil" src={sigilSrc} alt="" draggable={false} />

          <p className="hud__campaign">{"Кампания управляющего"}</p>

          <h1 className="hud__logo">
            <img className="hud__logo-image" src={logoSrc} alt="Остров" draggable={false} />
          </h1>

          <p className="hud__subtitle">{"Бремя отходов"}</p>
        </header>

        <div className="hud__rule" aria-hidden="true">
          <span className="hud__rule-diamond" />
        </div>

        <nav aria-label="Главное меню">
          <ul className="hud__list">
            {MENU_ENTRIES.map((entry, index) => (
              <li key={entry.id} className="hud__item" style={{ "--index": index } as React.CSSProperties}>
                <button
                  ref={(node) => {
                    entryRefs.current[index] = node;
                  }}
                  type="button"
                  className={`hud-entry ${index === activeIndex ? "hud-entry--active" : ""}`}
                  onClick={() => choose(entry)}
                  onFocus={() => setActiveIndex(index)}
                  onMouseEnter={() => moveTo(index)}
                >
                  <span className="hud-entry__mark" aria-hidden="true" />
                  <span className="hud-entry__title">{entry.title}</span>
                  <span className="hud-entry__label">{entry.label}</span>
                </button>
              </li>
            ))}
          </ul>
        </nav>
      </div>

      <footer className="hud__footer">
        <div className="hud-difficulty">
          <span className="hud-difficulty__label">{"Сложность"}</span>
          <button type="button" className="hud-difficulty__arrow" aria-label="Легче" onClick={() => shiftDifficulty(-1)}>
            {"‹"}
          </button>
          <span className="hud-difficulty__value">{DIFFICULTIES[difficulty]}</span>
          <button type="button" className="hud-difficulty__arrow" aria-label="Сложнее" onClick={() => shiftDifficulty(1)}>
            {"›"}
          </button>
        </div>

        <div className="hud__hints">
          <span><kbd>{"↑↓"}</kbd>{"выбор"}</span>
          <span><kbd>{"Enter"}</kbd>{"принять"}</span>
          <button type="button" className="hud__hint-button" onClick={onReplay}><kbd>{"R"}</kbd>{"заставка"}</button>
          <button type="button" className="hud__hint-button" onClick={onToggleMute}>
            <kbd>{"M"}</kbd>{isMuted ? "звук выкл." : "звук вкл."}
          </button>
        </div>
      </footer>

      {card ? (
        <div className="hud-card" role="status">
          <span className="hud-card__label">{card.label}</span>
          <span className="hud-card__title">{card.title}</span>
          <span className="hud-card__note">{"Раздел подключится в основной игре"}</span>
        </div>
      ) : null}
    </div>
  );
};

export { Hud };
