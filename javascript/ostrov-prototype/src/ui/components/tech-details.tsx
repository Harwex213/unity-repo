import { useLayoutEffect, useRef } from "react";
import { ICONS } from "../../core/icons";
import { getTech } from "../../core/techs";
import { getUnit } from "../../core/units";
import { Icon } from "./icon";
import { BRANCH_LABELS, TECH_ICONS } from "./tech-tree-layout";
import type { FC, PointerEvent as ReactPointerEvent } from "react";
import type { TTechId } from "../../core/techs";
import type { TTechNodeState } from "./tech-sheet";

type TTechDetailsProps = {
  techId: TTechId;
  state: TTechNodeState;
  researched: readonly TTechId[];
  science: number;
  isWaiting: boolean;
  /** The node on screen, in px inside the sheet: its centre and its radius. */
  anchor: { x: number; y: number; radius: number };
  /** The node is on the right half of the sheet: the card opens to its left. */
  opensLeft: boolean;
  onResearch: (techId: TTechId) => void;
  onClose: () => void;
};

/** The gap between the node and the card, in px. */
const CARD_GAP = 14;
/** The card never touches the sheet edge closer than this, in px. */
const CARD_INSET = 8;

/**
 * The card of the selected technology. It opens next to its node, like the
 * popup of the reference sheet: the name and branch, what the technology does,
 * what it unlocks, what it needs and the research button.
 */
const TechDetails: FC<TTechDetailsProps> = ({
  techId,
  state,
  researched,
  science,
  isWaiting,
  anchor,
  opensLeft,
  onResearch,
  onClose,
}) => {
  const cardRef = useRef<HTMLDivElement>(null);
  const tech = getTech(techId);
  const owned = new Set(researched);
  const affordable = science >= tech.cost;
  const missing = tech.requires.filter((required) => !owned.has(required));

  // The card keeps its top next to the node, but it never leaves the sheet.
  // Its height is known only after it is drawn, so the top is set here.
  useLayoutEffect(() => {
    const card = cardRef.current;
    const sheet = card?.offsetParent;
    if (!card || !(sheet instanceof HTMLElement)) {
      return;
    }

    const desired = anchor.y - anchor.radius;
    const lowest = sheet.clientHeight - card.offsetHeight - CARD_INSET;
    card.style.top = `${Math.max(CARD_INSET, Math.min(desired, lowest))}px`;
  });

  const side = opensLeft
    ? { right: `calc(100% - ${anchor.x - anchor.radius - CARD_GAP}px)` }
    : { left: `${anchor.x + anchor.radius + CARD_GAP}px` };

  // A press on the card is not the start of a pan.
  const stopPan = (event: ReactPointerEvent) => {
    event.stopPropagation();
  };

  return (
    <div
      ref={cardRef}
      className={`tech-details tech-details--${tech.branch}`}
      style={side}
      onPointerDown={stopPan}
      onClick={(click) => click.stopPropagation()}
    >
      <header className="tech-details__header">
        <span className={`tech-details__art tech-details__art--${state}`}>
          <Icon src={TECH_ICONS[tech.id]} size="l" />
        </span>

        <span className="tech-details__heading">
          <span className="tech-details__title">
            {tech.label}
          </span>

          <span className="tech-details__branch">
            {BRANCH_LABELS[tech.branch]}
          </span>
        </span>

        <button type="button" className="tech-details__close" aria-label="Закрыть карточку" onClick={onClose}>
          {"×"}
        </button>
      </header>

      <p className="tech-details__description">
        {tech.description}
      </p>

      {tech.unlocks.length > 0 ? (
        <div className="tech-details__unlocks">
          <span className="tech-details__caption">
            {"Открывает"}
          </span>

          {tech.unlocks.map((unitId) => (
            <span className="tech-details__unit" key={unitId}>
              <Icon src={getUnit(unitId).icon} size="m" />
              {getUnit(unitId).label}
            </span>
          ))}
        </div>
      ) : null}

      {tech.requires.length > 0 && state !== "owned" ? (
        <div className="tech-details__requires">
          <span className="tech-details__caption">
            {"Нужно"}
          </span>

          {tech.requires.map((required) => (
            <span
              className={owned.has(required) ? "tech-details__require tech-details__require--met" : "tech-details__require"}
              key={required}
            >
              {owned.has(required) ? <Icon src={ICONS.check} /> : null}
              {getTech(required).label}
            </span>
          ))}
        </div>
      ) : null}

      {state === "owned" ? (
        <p className="tech-details__status tech-details__status--owned">
          {tech.trophy ? "Трофей получен" : "Изучено"}
        </p>
      ) : null}

      {state === "trophy" ? (
        <p className="tech-details__status tech-details__status--trophy">
          <Icon src={ICONS.boss} />
          {"Трофей босса: победите босса"}
        </p>
      ) : null}

      {state === "locked" ? (
        <p className="tech-details__status tech-details__status--locked">
          {missing.length > 1 ? "Сначала изучите нужные технологии" : "Сначала изучите технологию выше"}
        </p>
      ) : null}

      {state === "ready" || state === "short" ? (
        <button
          type="button"
          className="button button--primary tech-details__research"
          disabled={isWaiting || !affordable}
          onClick={() => onResearch(tech.id)}
        >
          {"Изучить — "}
          <Icon src={ICONS.science} label="Наука" />
          <span className={affordable ? "" : "cost--short"}>
            {tech.cost}
          </span>
        </button>
      ) : null}

      {state === "short" ? (
        <p className="tech-details__status tech-details__status--locked">
          {`Не хватает науки: есть ${science} из ${tech.cost}`}
        </p>
      ) : null}
    </div>
  );
};

export { TechDetails };
