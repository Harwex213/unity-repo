import { computed } from "@preact/signals-react";
import { createContext, useContext } from "react";
import { DEFAULT_LINKS } from "../core/buildings";
import { findIssues, serializeLinks } from "../core/links";
import { createLinksState } from "./links-state";
import { createUiState } from "./ui-state";
import type { TLinksState } from "./links-state";
import type { TUiState } from "./ui-state";

const DEFAULT_SERIALIZED = serializeLinks(DEFAULT_LINKS);

/** Anything a component would otherwise `useMemo` lives here. */
const createDerived = (links: TLinksState, ui: TUiState) => {
  return {
    selectedBuilding: computed(() => links.links.value[ui.selectedBuildingId.value]),
    issues: computed(() => findIssues(links.links.value)),
    /** The links differ from the ones copied from the prototype. */
    isChanged: computed(() => serializeLinks(links.links.value) !== DEFAULT_SERIALIZED),
  };
};

/**
 * The store is a plain object of slices, and every slice is a plain object of
 * signals. The UI only reads signals; the domain layer owns every write.
 */
const createStore = () => {
  const links = createLinksState();
  const ui = createUiState();

  return {
    links,
    ui,
    derived: createDerived(links, ui),
  };
};

type TStore = ReturnType<typeof createStore>;

const StoreProvider = createContext<TStore>(null!);

const useStore = () => useContext(StoreProvider);

export type { TStore };
export { createStore, StoreProvider, useStore };
