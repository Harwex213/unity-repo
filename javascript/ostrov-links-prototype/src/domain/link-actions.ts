import {
  addBaseFace,
  removeBaseFace,
  serializeLinks,
  setBiomeAllowed,
  setYields,
  updateBaseFace,
  updateBiomeFace,
} from "../core/links";
import { LINKS_STORAGE_KEY } from "../store/links-state";
import type { TFacePatch } from "../core/links";
import type { TBiomeId, TBuildingId, TLinks, TYieldResourceId } from "../core/types";
import type { TStore } from "../store/store";

/** Every write of the links goes through here, so the browser copy stays in sync. */
const commitLinks = (store: TStore, links: TLinks) => {
  store.links.links.value = links;

  try {
    window.localStorage.setItem(LINKS_STORAGE_KEY, serializeLinks(links));
  } catch {
    // Storage can be off in a private window. The editor still works without it.
  }
};

const setYieldsAction = (store: TStore, buildingId: TBuildingId, resource: TYieldResourceId) => {
  commitLinks(store, setYields(store.links.links.value, buildingId, resource));
};

const addBaseFaceAction = (store: TStore, buildingId: TBuildingId) => {
  commitLinks(store, addBaseFace(store.links.links.value, buildingId));
};

const updateBaseFaceAction = (store: TStore, buildingId: TBuildingId, index: number, patch: TFacePatch) => {
  commitLinks(store, updateBaseFace(store.links.links.value, buildingId, index, patch));
};

const removeBaseFaceAction = (store: TStore, buildingId: TBuildingId, index: number) => {
  commitLinks(store, removeBaseFace(store.links.links.value, buildingId, index));
};

const setBiomeAllowedAction = (store: TStore, buildingId: TBuildingId, biomeId: TBiomeId, allowed: boolean) => {
  commitLinks(store, setBiomeAllowed(store.links.links.value, buildingId, biomeId, allowed));
};

const updateBiomeFaceAction = (store: TStore, buildingId: TBuildingId, biomeId: TBiomeId, patch: TFacePatch) => {
  commitLinks(store, updateBiomeFace(store.links.links.value, buildingId, biomeId, patch));
};

export {
  addBaseFaceAction,
  commitLinks,
  removeBaseFaceAction,
  setBiomeAllowedAction,
  setYieldsAction,
  updateBaseFaceAction,
  updateBiomeFaceAction,
};
