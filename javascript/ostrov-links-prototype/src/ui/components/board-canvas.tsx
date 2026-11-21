import { useSignals } from "@preact/signals-react/runtime";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { boundsOf, centerOn, fitRect, zoomAt } from "../../core/board";
import { BOARDS } from "../../core/boards";
import { useStore } from "../../store/store";
import type { FC, PointerEvent as ReactPointerEvent, ReactNode } from "react";
import type { TCamera, TPoint } from "../../core/board";
import type { TBoardId } from "../../core/boards";
import type {
  TFocusBoardNodeAction,
  TMoveBoardNodeAction,
  TSaveBoardAction,
  TSetBoardCameraAction,
} from "../../domain/registry";
import type { TBoardSaveStatus } from "../../store/boards-state";

type TBoardCanvasProps = {
  boardId: TBoardId;
  registry: {
    focusBoardNodeAction: TFocusBoardNodeAction;
    moveBoardNodeAction: TMoveBoardNodeAction;
    saveBoardAction: TSaveBoardAction;
    setBoardCameraAction: TSetBoardCameraAction;
  };
  /** The content of a node box. The box itself has the size from `BOARDS`. */
  renderNode: (nodeId: string) => ReactNode;
  nodeClassName?: (nodeId: string) => string;
  /** Where links attach inside a node box. The middle of the box by default. */
  edgeAnchor?: TPoint;
  /** A link that touches this node is drawn brighter. */
  highlightNodeId?: string | null;
  /** A click without a drag. */
  onNodeClick?: (nodeId: string, modifiers: { readonly shiftKey: boolean }) => void;
  onNodeDoubleClick?: (nodeId: string) => void;
  /** Extra text at the right end of the board toolbar. */
  hint?: ReactNode;
};

type TDrag =
  | { readonly kind: "pan"; readonly start: TPoint; readonly camera: TCamera; moved: boolean }
  | { readonly kind: "node"; readonly start: TPoint; readonly nodeId: string; readonly origin: TPoint; moved: boolean };

/** A press that moves less than this is a click, not a drag. */
const DRAG_THRESHOLD_PX = 3;
/** Two clicks on the same target within this time are a double click. */
const DOUBLE_CLICK_MS = 350;
/** The click target of the background in `lastClickRef`. */
const BACKGROUND = "";
/** The background grid step in board units. */
const GRID_STEP = 40;

const STATUS_TEXT: Readonly<Record<TBoardSaveStatus, string>> = {
  loading: "Загружаю…",
  saved: "Сохранено",
  saving: "Сохраняю…",
  readonly: "Только чтение",
  error: "Ошибка",
};

/**
 * A canvas with zoom and pan. The wheel and a trackpad pinch zoom around the
 * cursor. Dragging the background pans, dragging a node moves it. The board
 * saves itself when a drag ends.
 */
const BoardCanvas: FC<TBoardCanvasProps> = ({
  boardId,
  registry,
  renderNode,
  nodeClassName,
  edgeAnchor,
  highlightNodeId,
  onNodeClick,
  onNodeDoubleClick,
  hint,
}) => {
  useSignals();
  const store = useStore();
  const board = store.boards[boardId];
  const { file, nodeIds, nodeSize } = BOARDS[boardId];
  const layout = board.layout.value;
  const camera = board.camera.value;
  const focusNodeId = board.focusNodeId.value;
  const saveStatus = board.saveStatus.value;
  const viewportRef = useRef<HTMLDivElement>(null);
  const dragRef = useRef<TDrag | null>(null);
  // Pointer capture sends click and dblclick to the board, not to the node, so the board detects double clicks itself.
  const lastClickRef = useRef<{ readonly target: string; readonly time: number } | null>(null);
  const [draggingId, setDraggingId] = useState<string | null>(null);
  const anchor = edgeAnchor ?? { x: nodeSize.width / 2, y: nodeSize.height / 2 };

  const viewportSize = () => {
    const element = viewportRef.current;
    return element ? { width: element.clientWidth, height: element.clientHeight } : null;
  };

  const fit = () => {
    const size = viewportSize();
    const current = board.layout.peek();
    if (size && size.width > 0 && current) {
      registry.setBoardCameraAction(boardId, fitRect(boundsOf(current.positions, nodeSize), size));
    }
  };

  const zoomBy = (factor: number) => {
    const size = viewportSize();
    const current = board.camera.peek();
    if (size && current) {
      registry.setBoardCameraAction(boardId, zoomAt(current, { x: size.width / 2, y: size.height / 2 }, factor));
    }
  };

  // The first time the board is shown, fit every node into the viewport.
  useLayoutEffect(() => {
    if (layout && !camera) {
      fit();
    }
  });

  // A list asked to show a node: center it and keep the zoom.
  useLayoutEffect(() => {
    const size = viewportSize();
    if (!focusNodeId || !layout || !camera || !size) {
      return;
    }

    const position = layout.positions[focusNodeId];
    if (position) {
      registry.setBoardCameraAction(
        boardId,
        centerOn(camera, { x: position.x + anchor.x, y: position.y + anchor.y }, size),
      );
    }
    registry.focusBoardNodeAction(boardId, null);
  });

  // React attaches wheel listeners as passive, and a passive listener cannot stop the page zoom.
  useEffect(() => {
    const element = viewportRef.current;
    if (!element) {
      return;
    }

    const onWheel = (event: WheelEvent) => {
      event.preventDefault();
      const current = board.camera.peek();
      if (!current) {
        return;
      }

      const rect = element.getBoundingClientRect();
      // A pinch arrives as a wheel event with ctrlKey and small deltas.
      const factor = Math.exp(-event.deltaY * (event.ctrlKey ? 0.01 : 0.0015));
      registry.setBoardCameraAction(
        boardId,
        zoomAt(current, { x: event.clientX - rect.left, y: event.clientY - rect.top }, factor),
      );
    };

    element.addEventListener("wheel", onWheel, { passive: false });
    return () => element.removeEventListener("wheel", onWheel);
  }, [board, boardId, registry]);

  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (event.button !== 0 || !layout || !camera) {
      return;
    }

    const start = { x: event.clientX, y: event.clientY };
    const nodeElement = (event.target as Element).closest<HTMLElement>("[data-node-id]");
    const nodeId = nodeElement?.dataset.nodeId;

    dragRef.current =
      nodeId && layout.positions[nodeId]
        ? { kind: "node", start, nodeId, origin: layout.positions[nodeId], moved: false }
        : { kind: "pan", start, camera, moved: false };
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (!drag) {
      return;
    }

    const dx = event.clientX - drag.start.x;
    const dy = event.clientY - drag.start.y;
    if (!drag.moved && Math.hypot(dx, dy) < DRAG_THRESHOLD_PX) {
      return;
    }

    drag.moved = true;
    if (drag.kind === "pan") {
      registry.setBoardCameraAction(boardId, { ...drag.camera, x: drag.camera.x + dx, y: drag.camera.y + dy });
      return;
    }

    const scale = board.camera.peek()?.scale ?? 1;
    setDraggingId(drag.nodeId);
    registry.moveBoardNodeAction(boardId, drag.nodeId, {
      x: drag.origin.x + dx / scale,
      y: drag.origin.y + dy / scale,
    });
  };

  const onPointerUp = (event: ReactPointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    dragRef.current = null;
    setDraggingId(null);
    if (!drag) {
      return;
    }

    if (drag.moved) {
      lastClickRef.current = null;
      if (drag.kind === "node") {
        void registry.saveBoardAction(boardId);
      }
      return;
    }

    const target = drag.kind === "node" ? drag.nodeId : BACKGROUND;
    const last = lastClickRef.current;
    const isDouble = last !== null && last.target === target && event.timeStamp - last.time < DOUBLE_CLICK_MS;
    lastClickRef.current = isDouble ? null : { target, time: event.timeStamp };

    if (drag.kind === "node") {
      onNodeClick?.(drag.nodeId, { shiftKey: event.shiftKey });
      if (isDouble) {
        onNodeDoubleClick?.(drag.nodeId);
      }
    } else if (isDouble) {
      fit();
    }
  };

  const onPointerCancel = () => {
    const drag = dragRef.current;
    dragRef.current = null;
    setDraggingId(null);
    if (drag?.kind === "node" && drag.moved) {
      void registry.saveBoardAction(boardId);
    }
  };

  const gridStep = GRID_STEP * (camera?.scale ?? 1);

  return (
    <div
      ref={viewportRef}
      className="board"
      style={{
        backgroundSize: `${gridStep}px ${gridStep}px`,
        backgroundPosition: `${camera?.x ?? 0}px ${camera?.y ?? 0}px`,
      }}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerCancel}
    >
      {layout && camera ? (
        <div
          className="board-world"
          style={{ transform: `translate(${camera.x}px, ${camera.y}px) scale(${camera.scale})` }}
        >
          <svg className="board-edges" width="1" height="1" aria-hidden="true">
            {layout.edges.map(([a, b]) => {
              const from = layout.positions[a];
              const to = layout.positions[b];
              if (!from || !to) {
                return null;
              }

              const isHighlighted = highlightNodeId === a || highlightNodeId === b;

              return (
                <line
                  key={`${a}|${b}`}
                  className={isHighlighted ? "board-edge board-edge--highlighted" : "board-edge"}
                  x1={from.x + anchor.x}
                  y1={from.y + anchor.y}
                  x2={to.x + anchor.x}
                  y2={to.y + anchor.y}
                />
              );
            })}
          </svg>

          {nodeIds.map((nodeId) => {
            const position = layout.positions[nodeId];
            if (!position) {
              return null;
            }

            const classes = ["board-node", nodeClassName?.(nodeId) ?? ""];
            if (nodeId === draggingId) {
              classes.push("board-node--dragging");
            }

            return (
              <div
                key={nodeId}
                data-node-id={nodeId}
                className={classes.join(" ")}
                style={{ left: position.x, top: position.y, width: nodeSize.width, height: nodeSize.height }}
              >
                {renderNode(nodeId)}
              </div>
            );
          })}
        </div>
      ) : (
        <div className="board-loading muted">Загружаю доску…</div>
      )}

      <div className="board-toolbar" onPointerDown={(event) => event.stopPropagation()}>
        <button type="button" className="button" title="Отдалить" onClick={() => zoomBy(1 / 1.25)}>
          −
        </button>
        <button type="button" className="button" title="Приблизить" onClick={() => zoomBy(1.25)}>
          +
        </button>
        <button type="button" className="button" onClick={fit}>
          Вписать
        </button>
        <span className={`board-status board-status--${saveStatus}`} title={`data/${file}`}>
          {STATUS_TEXT[saveStatus]} · data/{file}
        </span>
        {hint ? <span className="board-hint muted">{hint}</span> : null}
      </div>
    </div>
  );
};

export { BoardCanvas };
