import { useSignals } from "@preact/signals-react/runtime";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { getGuideStep, GUIDE_STEPS, guideStepIndex } from "../../core/guide";
import { useStore } from "../../store/store";
import { useFocusTrap } from "./use-focus-trap";
import type { CSSProperties, FC } from "react";
import type { TGuideStepId } from "../../core/guide";
import type {
  TNextGuideStepAction,
  TPrevGuideStepAction,
  TReachGuideStepAction,
  TSkipGuideAction,
} from "../../domain/registry";

type TLearnGuideRegistrySlice = {
  reachGuideStepAction: TReachGuideStepAction;
  nextGuideStepAction: TNextGuideStepAction;
  prevGuideStepAction: TPrevGuideStepAction;
  skipGuideAction: TSkipGuideAction;
};

type TLearnGuideProps = {
  registry: TLearnGuideRegistrySlice;
};

type TBox = {
  readonly left: number;
  readonly top: number;
  readonly width: number;
  readonly height: number;
};

/** Where the card goes: next to its anchor, or in the middle of the screen. */
type TPlacement =
  | { readonly kind: "center" }
  | {
    readonly kind: "above" | "below";
    readonly style: CSSProperties;
    readonly arrowX: number;
    readonly ring: TBox;
  };

const CARD_WIDTH = 420;
const SCREEN_EDGE = 16;
/** The room the arrow takes between the card and its anchor. */
const ARROW_GAP = 14;
/** The ring sits a little outside the anchor. */
const RING_PAD = 6;
/** A card squeezed below this height goes to the middle of the screen instead. */
const MIN_CARD_HEIGHT = 340;
const MAX_CARD_HEIGHT = 640;
/** The HUD slides and resizes, so the anchor is measured again on this period. */
const MEASURE_PERIOD_MS = 200;

/** The anchor's box on screen, or `null` when it is missing or off screen. */
const measureAnchor = (stepId: TGuideStepId): TBox | null => {
  const node = document.querySelector(`[data-tutorial="${stepId}"]`);
  if (!node) {
    return null;
  }

  const box = node.getBoundingClientRect();
  const isOnScreen = box.width > 0
    && box.height > 0
    && box.top + box.height > 0
    && box.left + box.width > 0
    && box.top < window.innerHeight
    && box.left < window.innerWidth;

  return isOnScreen ? box : null;
};

const isSameBox = (a: TBox | null, b: TBox | null) => {
  if (!a || !b) {
    return a === b;
  }

  return a.left === b.left && a.top === b.top && a.width === b.width && a.height === b.height;
};

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max);

/**
 * The card goes on the side of the anchor that has more room. With no anchor,
 * or too little room on both sides, it goes to the middle of the screen.
 */
const placeCard = (anchor: TBox | null, viewWidth: number, viewHeight: number): TPlacement => {
  if (!anchor) {
    return { kind: "center" };
  }

  const width = Math.min(CARD_WIDTH, viewWidth - SCREEN_EDGE * 2);
  const roomAbove = anchor.top - RING_PAD - ARROW_GAP - SCREEN_EDGE;
  const roomBelow = viewHeight - (anchor.top + anchor.height) - RING_PAD - ARROW_GAP - SCREEN_EDGE;
  const kind = roomAbove >= roomBelow ? "above" : "below";
  const room = Math.max(roomAbove, roomBelow);

  if (room < MIN_CARD_HEIGHT) {
    return { kind: "center" };
  }

  const anchorX = anchor.left + anchor.width / 2;
  const left = clamp(anchorX - width / 2, SCREEN_EDGE, viewWidth - SCREEN_EDGE - width);
  const vertical = kind === "above"
    ? { bottom: viewHeight - anchor.top + RING_PAD + ARROW_GAP }
    : { top: anchor.top + anchor.height + RING_PAD + ARROW_GAP };

  return {
    kind,
    style: { ...vertical, left, width, maxHeight: Math.min(room, MAX_CARD_HEIGHT) },
    arrowX: clamp(anchorX - left, 24, width - 24),
    ring: {
      left: anchor.left - RING_PAD,
      top: anchor.top - RING_PAD,
      width: anchor.width + RING_PAD * 2,
      height: anchor.height + RING_PAD * 2,
    },
  };
};

const viewSize = () => ({ width: window.innerWidth, height: window.innerHeight });

/** Follows the anchor of the open card and the size of the window. */
const useAnchorPlacement = (stepId: TGuideStepId) => {
  const [anchor, setAnchor] = useState<TBox | null>(() => measureAnchor(stepId));
  const [view, setView] = useState(viewSize);

  useLayoutEffect(() => {
    const measure = () => {
      const next = measureAnchor(stepId);
      const nextView = viewSize();
      setAnchor((current) => (isSameBox(current, next) ? current : next));
      setView((current) => {
        const isSame = current.width === nextView.width && current.height === nextView.height;

        return isSame ? current : nextView;
      });
    };

    measure();
    const intervalId = setInterval(measure, MEASURE_PERIOD_MS);
    window.addEventListener("resize", measure);

    return () => {
      clearInterval(intervalId);
      window.removeEventListener("resize", measure);
    };
  }, [stepId]);

  return placeCard(anchor, view.width, view.height);
};

type TGuideCardProps = {
  registry: TLearnGuideRegistrySlice;
  stepId: TGuideStepId;
  canGoBack: boolean;
};

const GuideCard: FC<TGuideCardProps> = ({ registry, stepId, canGoBack }) => {
  const step = getGuideStep(stepId);
  const index = guideStepIndex(stepId);
  const isLast = index === GUIDE_STEPS.length - 1;
  const placement = useAnchorPlacement(stepId);
  const dialogRef = useRef<HTMLDivElement>(null);
  const nextRef = useRef<HTMLButtonElement>(null);
  const onTrapKeyDown = useFocusTrap(dialogRef);
  const titleId = `learn-guide-title-${stepId}`;
  const textId = `learn-guide-text-${stepId}`;

  useEffect(() => {
    nextRef.current?.focus();
  }, []);

  // The card owns the keyboard while it is open: the game's own hotkeys (Esc,
  // WASD, the arrows, Space) wait. Tab still reaches the focus trap.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Tab") {
        return;
      }

      event.stopImmediatePropagation();

      const target = event.target;
      const isOnButton = target instanceof HTMLButtonElement && dialogRef.current?.contains(target) === true;

      if (event.key === "Escape") {
        event.preventDefault();
        registry.skipGuideAction();
      } else if (event.key === "ArrowRight" || (event.key === "Enter" && !isOnButton)) {
        event.preventDefault();
        if (!event.repeat) {
          registry.nextGuideStepAction();
        }
      } else if (event.key === "ArrowLeft") {
        event.preventDefault();
        if (!event.repeat) {
          registry.prevGuideStepAction();
        }
      }
    };

    window.addEventListener("keydown", onKeyDown, { capture: true });

    return () => {
      window.removeEventListener("keydown", onKeyDown, { capture: true });
    };
  }, [registry]);

  const isAnchored = placement.kind !== "center";

  return (
    <div className={`learn-guide ${isAnchored ? "" : "learn-guide--center"}`}>
      {isAnchored ? (
        <div
          className="learn-guide__ring"
          style={{
            left: placement.ring.left,
            top: placement.ring.top,
            width: placement.ring.width,
            height: placement.ring.height,
          }}
          aria-hidden="true"
        />
      ) : null}

      <div
        ref={dialogRef}
        className={`panel learn-guide__card ${isAnchored ? `learn-guide__card--${placement.kind}` : ""}`}
        style={isAnchored ? placement.style : undefined}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={textId}
        onKeyDown={onTrapKeyDown}
      >
        {isAnchored ? (
          <span className="learn-guide__arrow" style={{ left: placement.arrowX }} aria-hidden="true" />
        ) : null}

        <div className="learn-guide__head">
          <div className="learn-guide__progress" aria-hidden="true">
            {GUIDE_STEPS.map((candidate, position) => (
              <span
                key={candidate.id}
                className={`learn-guide__segment ${position < index ? "learn-guide__segment--done" : ""} ${position === index ? "learn-guide__segment--current" : ""}`}
              />
            ))}
          </div>

          <span className="learn-guide__counter">
            {`${index + 1}/${GUIDE_STEPS.length}`}
          </span>
        </div>

        <div className="learn-guide__body">
          <img className="learn-guide__art" src={step.art} alt="" />

          <h2 id={titleId} className="learn-guide__title">
            {step.title}
          </h2>

          <p id={textId} className="learn-guide__text">
            {step.text}
          </p>
        </div>

        <div className="learn-guide__buttons">
          <button type="button" className="button button--ghost learn-guide__skip" onClick={registry.skipGuideAction}>
            {"Пропустить"}
          </button>

          <button
            type="button"
            className="button learn-guide__back"
            disabled={!canGoBack}
            onClick={registry.prevGuideStepAction}
          >
            {"Назад"}
          </button>

          <button
            ref={nextRef}
            type="button"
            className="button button--primary learn-guide__next"
            onClick={registry.nextGuideStepAction}
          >
            {isLast ? "Понятно" : "Далее"}
          </button>
        </div>
      </div>
    </div>
  );
};

/**
 * The learn guide, on top of every page. It renders nothing while it is
 * closed, so the game gets its input back. Each time the page or the phase
 * changes, the guide asks the domain for the card that belongs there.
 */
const LearnGuide: FC<TLearnGuideProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const contextStep = store.derived.guideContextStep.value;
  const openStep = store.guide.openStep.value;
  const seen = store.guide.seen.value;

  useEffect(() => {
    registry.reachGuideStepAction();
  }, [registry, contextStep, openStep]);

  if (!openStep) {
    return null;
  }

  const index = guideStepIndex(openStep);
  const canGoBack = GUIDE_STEPS.slice(0, index).some((step) => seen.includes(step.id));

  // The key gives every card a fresh mount: the focus and the measuring restart.
  return <GuideCard key={openStep} registry={registry} stepId={openStep} canGoBack={canGoBack} />;
};

export { LearnGuide };
