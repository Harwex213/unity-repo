import { effect } from "@preact/signals-react";
import { isBuildingId } from "../core/buildings";
import { isModuleId } from "../core/modules";
import { isTechnologyId } from "../core/technologies";
import type { TStore } from "../store/store";
import type { TBuildingsView, TSection } from "../store/ui-state";

/**
 * The URL hash keeps the open tab, the sub-tab and the selected item, so a reload
 * opens the same screen:
 *
 *   #/buildings/graph
 *   #/buildings/buildings/<building id>
 *   #/buildings/modules/<module id>
 *   #/technologies/<technology id>
 *   #/units
 */

const SECTIONS: readonly TSection[] = ["buildings", "technologies", "negative-events", "units", "enemies", "artifacts"];
const BUILDINGS_VIEWS: readonly TBuildingsView[] = ["graph", "buildings", "modules"];

const isSection = (value: string | undefined): value is TSection => SECTIONS.includes(value as TSection);
const isBuildingsView = (value: string | undefined): value is TBuildingsView =>
  BUILDINGS_VIEWS.includes(value as TBuildingsView);

const routeOf = (store: TStore) => {
  const { section, buildingsView, selectedBuildingId, selectedModuleId, selectedTechnologyId } = store.ui;

  switch (section.value) {
    case "buildings":
      if (buildingsView.value === "buildings") {
        return `/buildings/buildings/${selectedBuildingId.value}`;
      }
      if (buildingsView.value === "modules") {
        return `/buildings/modules/${selectedModuleId.value}`;
      }
      return "/buildings/graph";
    case "technologies":
      return `/technologies/${selectedTechnologyId.value}`;
    default:
      return `/${section.value}`;
  }
};

/** Applies a hash to the UI state. An unknown part is skipped: the screen keeps its current value. */
const applyRoute = (store: TStore, hash: string) => {
  const [section, second, third] = hash.replace(/^#?\/?/, "").split("/");
  if (!isSection(section)) {
    return;
  }

  store.ui.section.value = section;

  if (section === "buildings" && isBuildingsView(second)) {
    store.ui.buildingsView.value = second;
    if (second === "buildings" && isBuildingId(third)) {
      store.ui.selectedBuildingId.value = third;
    }
    if (second === "modules" && isModuleId(third)) {
      store.ui.selectedModuleId.value = third;
    }
  }

  if (section === "technologies" && isTechnologyId(second)) {
    store.ui.selectedTechnologyId.value = second;
  }
};

/**
 * Reads the hash once, then keeps it in sync both ways. A new tab or sub-tab adds
 * a history entry, so "Назад" returns to the previous screen. A new selection
 * replaces the entry, so clicking through a list does not flood the history.
 */
const startRouteSyncAction = (store: TStore) => {
  applyRoute(store, window.location.hash);

  let screen = `${store.ui.section.peek()}/${store.ui.buildingsView.peek()}`;

  effect(() => {
    const route = `#${routeOf(store)}`;
    const nextScreen = `${store.ui.section.value}/${store.ui.buildingsView.value}`;

    if (window.location.hash !== route) {
      if (nextScreen === screen) {
        window.history.replaceState(null, "", route);
      } else {
        window.history.pushState(null, "", route);
      }
    }

    screen = nextScreen;
  });

  window.addEventListener("popstate", () => applyRoute(store, window.location.hash));
};

export { startRouteSyncAction };
