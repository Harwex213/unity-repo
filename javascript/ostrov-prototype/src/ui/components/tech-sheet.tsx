import { useCallback, useEffect, useRef, useState } from "react";
import { ICONS } from "../../core/icons";
import { getTech, isAvailable, TECHS } from "../../core/techs";
import { Icon } from "./icon";
import { TechDetails } from "./tech-details";
import {
  ancestorsOf,
  BRANCH_LABEL_POINTS,
  BRANCH_LABELS,
  edgePath,
  HUB,
  NODE_RADIUS,
  SHEET_BOX,
  SHEET_EDGES,
  TECH_ICONS,
  techPoint,
} from "./tech-tree-layout";
import type { CSSProperties, FC, PointerEvent as ReactPointerEvent } from "react";
import type { TTech, TTechBranch, TTechId } from "../../core/techs";
import type { TSheetEdge } from "./tech-tree-layout";

/**
 * What a node shows. `ready` can be researched now, `short` is open but the
 * science is not enough, `locked` waits for a prerequisite, `trophy` waits
 * for the boss.
 */
type TTechNodeState = "owned" | "ready" | "short" | "locked" | "trophy";

type TTechSheetProps = {
  researched: readonly TTechId[];
  science: number;
  isWaiting: boolean;
  selectedId: TTechId | null;
  onSelect: (techId: TTechId | null) => void;
  onResearch: (techId: TTechId) => void;
};

type TView = {
  x: number;
  y: number;
  scale: number;
};

/** The hub circle is a bit larger than a technology node. */
const HUB_RADIUS = 40;
const MAX_SCALE = 2.4;
/** The sheet can zoom out a little past the fitted size, not further. */
const MIN_SCALE_OF_FIT = 0.8;
/** One wheel notch. Zoom is geometric, so a notch is a constant ratio. */
const ZOOM_STEP = 1.12;
/** A pointer that moved less than this between down and up was a click. */
const DRAG_SLOP_PX = 4;
/** The bow of a connector, in sheet units. Neighbouring connectors bow to opposite sides. */
const EDGE_BOW = 9;

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

const nodeState = (tech: TTech, owned: ReadonlySet<TTechId>, researched: readonly TTechId[], science: number): TTechNodeState => {
  if (owned.has(tech.id)) {
    return "owned";
  }

  if (tech.trophy) {
    return "trophy";
  }

  if (!isAvailable(tech, researched)) {
    return "locked";
  }

  return science >= tech.cost ? "ready" : "short";
};

const edgeClass = (edge: TSheetEdge, owned: ReadonlySet<TTechId>, lit: ReadonlySet<TTechId>) => {
  const target = getTech(edge.to);
  const isFromOwned = edge.from === null || owned.has(edge.from);
  const modifiers = [
    owned.has(edge.to) ? "tech-edge--owned" : isFromOwned ? "tech-edge--open" : "tech-edge--locked",
    target.trophy && !owned.has(edge.to) ? "tech-edge--trophy" : "",
    lit.has(edge.to) ? "tech-edge--lit" : "",
  ];

  return `tech-edge ${modifiers.join(" ")}`;
};

const toSheet = (point: { x: number; y: number }) => ({ x: point.x - SHEET_BOX.minX, y: point.y - SHEET_BOX.minY });

/**
 * The technology tree drawn as a sheet, following the `ostrov-tech` reference:
 * a hub in the centre, one branch per category, connectors from every
 * prerequisite. The wheel zooms around the pointer and a drag pans. A hover
 * lights the path back to the hub and shows the name. A click opens the card.
 * A double click fits the whole tree back into the sheet.
 */
const TechSheet: FC<TTechSheetProps> = ({ researched, science, isWaiting, selectedId, onSelect, onResearch }) => {
  const sheetRef = useRef<HTMLDivElement>(null);
  const dragRef = useRef<{ x: number; y: number; moved: number; captured: boolean } | null>(null);
  /** How far the pointer travelled in the gesture that just ended. */
  const lastMovedRef = useRef(0);
  const fitScaleRef = useRef(1);
  const [view, setView] = useState<TView>({ x: 0, y: 0, scale: 1 });
  const [size, setSize] = useState({ width: 0, height: 0 });
  const [hoveredId, setHoveredId] = useState<TTechId | null>(null);

  const owned = new Set(researched);
  const pathId = hoveredId ?? selectedId;
  const lit = pathId ? new Set<TTechId>([pathId, ...ancestorsOf(pathId)]) : new Set<TTechId>();

  /** Centres the sheet and picks the scale that shows all of it. */
  const fitToView = useCallback(() => {
    const sheet = sheetRef.current;
    if (!sheet) {
      return;
    }

    const width = sheet.clientWidth;
    const height = sheet.clientHeight;
    const scale = Math.min(width / SHEET_BOX.width, height / SHEET_BOX.height, MAX_SCALE);
    fitScaleRef.current = scale;
    setSize({ width, height });
    setView({
      scale,
      x: (width - SHEET_BOX.width * scale) / 2,
      y: (height - SHEET_BOX.height * scale) / 2,
    });
  }, []);

  // The sheet refits whenever the window, and so the modal, changes size.
  useEffect(() => {
    const sheet = sheetRef.current;
    if (!sheet) {
      return;
    }

    fitToView();
    const observer = new ResizeObserver(fitToView);
    observer.observe(sheet);

    return () => {
      observer.disconnect();
    };
  }, [fitToView]);

  // Wheel has to be bound by hand: React's own listener is passive, so it
  // cannot stop the page from scrolling behind the sheet.
  useEffect(() => {
    const sheet = sheetRef.current;
    if (!sheet) {
      return;
    }

    const onWheel = (event: WheelEvent) => {
      event.preventDefault();

      const rect = sheet.getBoundingClientRect();
      const pointerX = event.clientX - rect.left;
      const pointerY = event.clientY - rect.top;
      const notches = event.deltaY / 100;

      setView((current) => {
        const minScale = fitScaleRef.current * MIN_SCALE_OF_FIT;
        const scale = clamp(current.scale * Math.pow(ZOOM_STEP, -notches), minScale, MAX_SCALE);
        const ratio = scale / current.scale;

        return {
          scale,
          x: pointerX - (pointerX - current.x) * ratio,
          y: pointerY - (pointerY - current.y) * ratio,
        };
      });
    };

    sheet.addEventListener("wheel", onWheel, { passive: false });

    return () => {
      sheet.removeEventListener("wheel", onWheel);
    };
  }, []);

  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    // The pointer is captured only once it starts to drag. Capturing it here
    // would retarget the click to the sheet, and no node would ever be hit.
    dragRef.current = { x: event.clientX, y: event.clientY, moved: 0, captured: false };
  };

  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (!drag) {
      return;
    }

    const dx = event.clientX - drag.x;
    const dy = event.clientY - drag.y;
    drag.moved += Math.abs(dx) + Math.abs(dy);
    drag.x = event.clientX;
    drag.y = event.clientY;

    if (!drag.captured && drag.moved > DRAG_SLOP_PX) {
      event.currentTarget.setPointerCapture(event.pointerId);
      drag.captured = true;
    }

    setView((current) => ({ ...current, x: current.x + dx, y: current.y + dy }));
  };

  const onPointerUp = (event: ReactPointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (drag?.captured) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }

    lastMovedRef.current = drag?.moved ?? 0;
    dragRef.current = null;
  };

  /** The click right after a pan is part of the pan, not a click on a node. */
  const wasDragged = () => lastMovedRef.current > DRAG_SLOP_PX;

  const worldStyle = {
    width: `${SHEET_BOX.width}px`,
    height: `${SHEET_BOX.height}px`,
    transform: `translate(${view.x}px, ${view.y}px) scale(${view.scale})`,
    "--tech-scale": view.scale,
  } as CSSProperties;

  const hub = toSheet(HUB);
  const selected = selectedId ? getTech(selectedId) : null;
  const selectedPoint = selectedId ? toSheet(techPoint(selectedId)) : null;
  const anchor = selectedPoint
    ? {
        x: view.x + selectedPoint.x * view.scale,
        y: view.y + selectedPoint.y * view.scale,
        radius: NODE_RADIUS * view.scale,
      }
    : null;

  return (
    <div
      ref={sheetRef}
      className="tech-sheet"
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerUp}
      onDoubleClick={(event) => {
        // A double click on a node or on the card is two clicks, not a fit.
        if (event.target instanceof Element && event.target.closest("button, .tech-details")) {
          return;
        }

        fitToView();
      }}
      onClick={() => {
        // A click on empty paper closes the card.
        if (!wasDragged()) {
          onSelect(null);
        }
      }}
    >
      <div className="tech-sheet__world" style={worldStyle}>
        <svg
          className="tech-sheet__edges"
          viewBox={`0 0 ${SHEET_BOX.width} ${SHEET_BOX.height}`}
          width={SHEET_BOX.width}
          height={SHEET_BOX.height}
          aria-hidden="true"
        >
          {SHEET_EDGES.map((edge, index) => {
            const from = toSheet(edge.from ? techPoint(edge.from) : HUB);
            const to = toSheet(techPoint(edge.to));
            const fromRadius = edge.from ? NODE_RADIUS : HUB_RADIUS;
            const bow = index % 2 === 0 ? EDGE_BOW : -EDGE_BOW;

            return (
              <path
                key={`${edge.from ?? "hub"}-${edge.to}`}
                className={edgeClass(edge, owned, lit)}
                d={edgePath(from, to, fromRadius, NODE_RADIUS, bow)}
              />
            );
          })}
        </svg>

        {(Object.keys(BRANCH_LABEL_POINTS) as TTechBranch[]).map((branch) => {
          const point = toSheet(BRANCH_LABEL_POINTS[branch]);

          return (
            <span
              key={branch}
              className={`tech-sheet__branch tech-sheet__branch--${branch}`}
              style={{ left: `${point.x}px`, top: `${point.y}px` }}
            >
              {BRANCH_LABELS[branch]}
            </span>
          );
        })}

        <span className="tech-hub" style={{ left: `${hub.x}px`, top: `${hub.y}px` }}>
          <Icon src={ICONS.science} label="Наука" size="l" />
        </span>

        {TECHS.map((tech) => {
          const point = toSheet(techPoint(tech.id));
          const state = nodeState(tech, owned, researched, science);
          const modifiers = [
            `tech-node--${tech.branch}`,
            `tech-node--${state}`,
            tech.id === selectedId ? "tech-node--selected" : "",
            tech.id === hoveredId ? "tech-node--hovered" : "",
            lit.has(tech.id) && tech.id !== pathId ? "tech-node--lit" : "",
          ];

          return (
            <button
              type="button"
              key={tech.id}
              className={`tech-node ${modifiers.join(" ")}`}
              style={{ left: `${point.x}px`, top: `${point.y}px` }}
              aria-label={tech.label}
              aria-expanded={tech.id === selectedId}
              onPointerEnter={() => setHoveredId(tech.id)}
              onPointerLeave={() => setHoveredId((current) => (current === tech.id ? null : current))}
              onClick={(click) => {
                click.stopPropagation();
                if (!wasDragged()) {
                  onSelect(tech.id === selectedId ? null : tech.id);
                }
              }}
            >
              <Icon src={TECH_ICONS[tech.id]} size="l" className="tech-node__icon" />

              {state === "owned" ? (
                <span className="tech-node__badge tech-node__badge--owned">
                  <Icon src={ICONS.check} />
                </span>
              ) : null}

              {state === "trophy" ? (
                <span className="tech-node__badge tech-node__badge--trophy">
                  <Icon src={ICONS.boss} />
                </span>
              ) : null}

              {state === "ready" || state === "short" || state === "locked" ? (
                <span className={`tech-node__badge ${state === "short" ? "tech-node__badge--short" : ""}`}>
                  {tech.cost}
                </span>
              ) : null}

              <span className="tech-node__name">
                {tech.label}
              </span>
            </button>
          );
        })}
      </div>

      {selected && anchor ? (
        <TechDetails
          techId={selected.id}
          state={nodeState(selected, owned, researched, science)}
          researched={researched}
          science={science}
          isWaiting={isWaiting}
          anchor={anchor}
          opensLeft={anchor.x > size.width / 2}
          onResearch={onResearch}
          onClose={() => onSelect(null)}
        />
      ) : null}
    </div>
  );
};

export type { TTechNodeState };
export { TechSheet };
