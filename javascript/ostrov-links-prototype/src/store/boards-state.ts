import { signal } from "@preact/signals-react";
import type { TBoardLayout, TCamera } from "../core/board";
import type { TBoardId } from "../core/boards";

/**
 * `loading` until the file arrives. `saving` while a POST is in flight.
 * `readonly` when the page runs without the dev server, for example from `dist/`.
 */
type TBoardSaveStatus = "loading" | "saved" | "saving" | "readonly" | "error";

const createBoardState = () => {
  return {
    /** Null until the board file is read. */
    layout: signal<TBoardLayout | null>(null),
    saveStatus: signal<TBoardSaveStatus>("loading"),
    /** Null until the board view fits the nodes into its first viewport. */
    camera: signal<TCamera | null>(null),
    /** A node the view should scroll to, for example after a click in a list. The view clears it. */
    focusNodeId: signal<string | null>(null),
  };
};

type TBoardState = ReturnType<typeof createBoardState>;

const createBoardsState = (): Readonly<Record<TBoardId, TBoardState>> => ({
  buildings: createBoardState(),
  technologies: createBoardState(),
});

export type { TBoardSaveStatus, TBoardState };
export { createBoardsState };
