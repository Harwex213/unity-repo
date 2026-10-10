import type { TBuildingId } from "../core/types";
import type { TStore } from "../store/store";
import type { TNotice, TView } from "../store/ui-state";

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

const setViewAction = (store: TStore, view: TView) => {
  store.ui.view.value = view;
};

/** Opens the building in the editor, from the list or from the matrix. */
const selectBuildingAction = (store: TStore, buildingId: TBuildingId) => {
  store.ui.selectedBuildingId.value = buildingId;
  store.ui.view.value = "building";
};

export { selectBuildingAction, setViewAction, showNotice };
