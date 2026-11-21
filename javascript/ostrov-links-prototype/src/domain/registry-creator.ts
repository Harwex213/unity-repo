import {
  focusBoardNodeAction,
  loadBoardAction,
  moveBoardNodeAction,
  saveBoardAction,
  setBoardCameraAction,
  toggleBoardEdgeAction,
} from "./board-actions";
import { exportLinksAction, importLinksAction, resetLinksAction } from "./file-actions";
import {
  addBaseFaceAction,
  removeBaseFaceAction,
  setBiomeAllowedAction,
  setYieldsAction,
  updateBaseFaceAction,
  updateBiomeFaceAction,
} from "./link-actions";
import { startRouteSyncAction } from "./route-actions";
import {
  selectBuildingAction,
  selectModuleAction,
  selectTechnologyAction,
  setBuildingsViewAction,
  setSectionAction,
} from "./ui-actions";
import type { TStore } from "../store/store";
import type { TAppRegistry } from "./registry";

const rawRegistry = {
  addBaseFaceAction,
  exportLinksAction,
  focusBoardNodeAction,
  importLinksAction,
  loadBoardAction,
  moveBoardNodeAction,
  removeBaseFaceAction,
  resetLinksAction,
  saveBoardAction,
  selectBuildingAction,
  selectModuleAction,
  selectTechnologyAction,
  setBiomeAllowedAction,
  setBoardCameraAction,
  setBuildingsViewAction,
  setSectionAction,
  setYieldsAction,
  startRouteSyncAction,
  toggleBoardEdgeAction,
  updateBaseFaceAction,
  updateBiomeFaceAction,
};

/** Binds every action to the store, so the UI calls them without it. */
const createRegistry = (store: TStore): TAppRegistry => ({
  addBaseFaceAction: (...args) => rawRegistry.addBaseFaceAction(store, ...args),
  exportLinksAction: () => rawRegistry.exportLinksAction(store),
  focusBoardNodeAction: (...args) => rawRegistry.focusBoardNodeAction(store, ...args),
  importLinksAction: (...args) => rawRegistry.importLinksAction(store, ...args),
  loadBoardAction: (...args) => rawRegistry.loadBoardAction(store, ...args),
  moveBoardNodeAction: (...args) => rawRegistry.moveBoardNodeAction(store, ...args),
  removeBaseFaceAction: (...args) => rawRegistry.removeBaseFaceAction(store, ...args),
  resetLinksAction: () => rawRegistry.resetLinksAction(store),
  saveBoardAction: (...args) => rawRegistry.saveBoardAction(store, ...args),
  selectBuildingAction: (...args) => rawRegistry.selectBuildingAction(store, ...args),
  selectModuleAction: (...args) => rawRegistry.selectModuleAction(store, ...args),
  selectTechnologyAction: (...args) => rawRegistry.selectTechnologyAction(store, ...args),
  setBiomeAllowedAction: (...args) => rawRegistry.setBiomeAllowedAction(store, ...args),
  setBoardCameraAction: (...args) => rawRegistry.setBoardCameraAction(store, ...args),
  setBuildingsViewAction: (...args) => rawRegistry.setBuildingsViewAction(store, ...args),
  setSectionAction: (...args) => rawRegistry.setSectionAction(store, ...args),
  setYieldsAction: (...args) => rawRegistry.setYieldsAction(store, ...args),
  startRouteSyncAction: () => rawRegistry.startRouteSyncAction(store),
  toggleBoardEdgeAction: (...args) => rawRegistry.toggleBoardEdgeAction(store, ...args),
  updateBaseFaceAction: (...args) => rawRegistry.updateBaseFaceAction(store, ...args),
  updateBiomeFaceAction: (...args) => rawRegistry.updateBiomeFaceAction(store, ...args),
});

export { createRegistry };
