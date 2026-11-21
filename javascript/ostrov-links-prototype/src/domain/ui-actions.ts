import type { TBuildingId, TModuleId, TTechnologyId } from "../core/types";
import type { TStore } from "../store/store";
import type { TBuildingsView, TNotice, TSection } from "../store/ui-state";

/** How long a notice stays under the toolbar. */
const NOTICE_LIFETIME_MS = 4000;

let noticeTimeoutId: ReturnType<typeof setTimeout> | null = null;

const showNotice = (store: TStore, notice: TNotice) => {
  store.ui.notice.value = notice;

  if (noticeTimeoutId !== null) {
    clearTimeout(noticeTimeoutId);
  }

  noticeTimeoutId = setTimeout(() => {
    store.ui.notice.value = null;
    noticeTimeoutId = null;
  }, NOTICE_LIFETIME_MS);
};

const setSectionAction = (store: TStore, section: TSection) => {
  store.ui.section.value = section;
};

const setBuildingsViewAction = (store: TStore, view: TBuildingsView) => {
  store.ui.buildingsView.value = view;
};

/** Opens the building in the editor: from the list, the graph or a technology card. */
const selectBuildingAction = (store: TStore, buildingId: TBuildingId) => {
  store.ui.selectedBuildingId.value = buildingId;
  store.ui.section.value = "buildings";
  store.ui.buildingsView.value = "buildings";
};

/** Opens the module card: from the list or the graph. */
const selectModuleAction = (store: TStore, moduleId: TModuleId) => {
  store.ui.selectedModuleId.value = moduleId;
  store.ui.section.value = "buildings";
  store.ui.buildingsView.value = "modules";
};

/** Selects the technology: in the list, on the tree, or from the building editor. */
const selectTechnologyAction = (store: TStore, technologyId: TTechnologyId) => {
  store.ui.selectedTechnologyId.value = technologyId;
  store.ui.section.value = "technologies";
};

export {
  selectBuildingAction,
  selectModuleAction,
  selectTechnologyAction,
  setBuildingsViewAction,
  setSectionAction,
  showNotice,
};
