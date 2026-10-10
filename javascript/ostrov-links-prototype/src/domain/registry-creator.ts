import { exportLinksAction, importLinksAction, resetLinksAction } from "./file-actions";
import {
  addBaseFaceAction,
  removeBaseFaceAction,
  setBiomeAllowedAction,
  setYieldsAction,
  updateBaseFaceAction,
  updateBiomeFaceAction,
} from "./link-actions";
import { selectBuildingAction, setViewAction } from "./ui-actions";
import type { TStore } from "../store/store";
import type { TAppRegistry } from "./registry";

const rawRegistry = {
  addBaseFaceAction,
  exportLinksAction,
  importLinksAction,
  removeBaseFaceAction,
  resetLinksAction,
  selectBuildingAction,
  setBiomeAllowedAction,
  setViewAction,
  setYieldsAction,
  updateBaseFaceAction,
  updateBiomeFaceAction,
};

/** Binds every action to the store, so the UI calls them without it. */
const createRegistry = (store: TStore): TAppRegistry => ({
  addBaseFaceAction: (...args) => rawRegistry.addBaseFaceAction(store, ...args),
  exportLinksAction: () => rawRegistry.exportLinksAction(store),
  importLinksAction: (...args) => rawRegistry.importLinksAction(store, ...args),
  removeBaseFaceAction: (...args) => rawRegistry.removeBaseFaceAction(store, ...args),
  resetLinksAction: () => rawRegistry.resetLinksAction(store),
  selectBuildingAction: (...args) => rawRegistry.selectBuildingAction(store, ...args),
  setBiomeAllowedAction: (...args) => rawRegistry.setBiomeAllowedAction(store, ...args),
  setViewAction: (...args) => rawRegistry.setViewAction(store, ...args),
  setYieldsAction: (...args) => rawRegistry.setYieldsAction(store, ...args),
  updateBaseFaceAction: (...args) => rawRegistry.updateBaseFaceAction(store, ...args),
  updateBiomeFaceAction: (...args) => rawRegistry.updateBiomeFaceAction(store, ...args),
});

export { createRegistry };
