import { parseBoardLayout, serializeBoardLayout, toggleEdge, withFallbackPositions } from "../core/board";
import { BOARDS } from "../core/boards";
import { showNotice } from "./ui-actions";
import type { TCamera, TPoint } from "../core/board";
import type { TBoardId } from "../core/boards";
import type { TStore } from "../store/store";

const boardUrl = (boardId: TBoardId) => `data/${BOARDS[boardId].file}`;

const errorText = (error: unknown) => (error instanceof Error ? error.message : String(error));

const loadBoardAction = async (store: TStore, boardId: TBoardId) => {
  const board = store.boards[boardId];
  const { nodeIds, nodeSize } = BOARDS[boardId];

  try {
    const response = await fetch(boardUrl(boardId), { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    board.layout.value = parseBoardLayout(await response.json(), nodeIds, nodeSize);
    board.saveStatus.value = "saved";
  } catch (error) {
    board.layout.value = { positions: withFallbackPositions({}, nodeIds, nodeSize), edges: [] };
    board.saveStatus.value = "error";
    showNotice(store, { kind: "error", text: `Не удалось прочитать ${boardUrl(boardId)}: ${errorText(error)}` });
  }
};

/** Moves a node while the pointer drags it. `saveBoardAction` writes the result when the drag ends. */
const moveBoardNodeAction = (store: TStore, boardId: TBoardId, nodeId: string, position: TPoint) => {
  const board = store.boards[boardId];
  const layout = board.layout.value;
  if (!layout) {
    return;
  }

  board.layout.value = {
    ...layout,
    positions: { ...layout.positions, [nodeId]: { x: Math.round(position.x), y: Math.round(position.y) } },
  };
};

/** Saves run one after another, so an older layout never overwrites a newer one. */
let saveQueue: Promise<void> = Promise.resolve();

const saveBoardAction = (store: TStore, boardId: TBoardId) => {
  const board = store.boards[boardId];

  saveQueue = saveQueue.then(async () => {
    const layout = board.layout.value;
    if (!layout) {
      return;
    }

    board.saveStatus.value = "saving";
    try {
      const response = await fetch(boardUrl(boardId), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: serializeBoardLayout(layout, BOARDS[boardId].nodeIds),
      });

      // A static server (dist/) answers a POST with 404, 405 or 501.
      if (response.status === 404 || response.status === 405 || response.status === 501) {
        board.saveStatus.value = "readonly";
        showNotice(store, { kind: "error", text: "Раскладка сохраняется только из dev-сервера (npm run dev)." });
        return;
      }

      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { error?: string } | null;
        throw new Error(body?.error ?? `HTTP ${response.status}`);
      }

      board.saveStatus.value = "saved";
    } catch (error) {
      board.saveStatus.value = "error";
      showNotice(store, { kind: "error", text: `Не удалось сохранить раскладку: ${errorText(error)}` });
    }
  });

  return saveQueue;
};

/** Adds the link between two nodes, or removes it, and saves the board. */
const toggleBoardEdgeAction = (store: TStore, boardId: TBoardId, a: string, b: string) => {
  const board = store.boards[boardId];
  const layout = board.layout.value;
  if (!layout || a === b) {
    return;
  }

  board.layout.value = { ...layout, edges: toggleEdge(layout.edges, a, b) };
  void saveBoardAction(store, boardId);
};

const setBoardCameraAction = (store: TStore, boardId: TBoardId, camera: TCamera | null) => {
  store.boards[boardId].camera.value = camera;
};

const focusBoardNodeAction = (store: TStore, boardId: TBoardId, nodeId: string | null) => {
  store.boards[boardId].focusNodeId.value = nodeId;
};

export {
  focusBoardNodeAction,
  loadBoardAction,
  moveBoardNodeAction,
  saveBoardAction,
  setBoardCameraAction,
  toggleBoardEdgeAction,
};
