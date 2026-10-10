import { signal } from "@preact/signals-react";
import type { TBuildingId } from "../core/types";

/** `building` edits one building, `matrix` shows every link at once. */
type TView = "building" | "matrix";

/** A short message under the toolbar: an import error or a confirmation. */
type TNotice = {
  readonly kind: "ok" | "error";
  readonly text: string;
};

const createUiState = () => {
  return {
    view: signal<TView>("building"),
    selectedBuildingId: signal<TBuildingId>("farm"),
    notice: signal<TNotice | null>(null),
  };
};

type TUiState = ReturnType<typeof createUiState>;

export type { TNotice, TUiState, TView };
export { createUiState };
