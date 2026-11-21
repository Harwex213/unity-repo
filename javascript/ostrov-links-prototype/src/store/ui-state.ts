import { signal } from "@preact/signals-react";
import type { TBuildingId, TModuleId, TTechnologyId } from "../core/types";

/** A top-level tab of the toolbar. */
type TSection = "buildings" | "technologies" | "negative-events" | "units" | "enemies" | "artifacts";

/** A sub-tab of "Здания": the board of every card, the building editor, the module cards. */
type TBuildingsView = "graph" | "buildings" | "modules";

/** A short message under the toolbar: an import error or a confirmation. */
type TNotice = {
  readonly kind: "ok" | "error";
  readonly text: string;
};

const createUiState = () => {
  return {
    section: signal<TSection>("buildings"),
    buildingsView: signal<TBuildingsView>("graph"),
    selectedBuildingId: signal<TBuildingId>("farm"),
    selectedModuleId: signal<TModuleId>("greenhouse"),
    selectedTechnologyId: signal<TTechnologyId>("survival"),
    notice: signal<TNotice | null>(null),
  };
};

type TUiState = ReturnType<typeof createUiState>;

export type { TBuildingsView, TNotice, TSection, TUiState };
export { createUiState };
