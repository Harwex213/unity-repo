import {
  armBuildingAction,
  buildOnHexAction,
  cancelDemolishAction,
  confirmDemolishAction,
  disarmAction,
  requestDemolishAction,
  toggleDemolishModeAction,
} from "./build-actions";
import { endPhaseAction, newGameAction, startGameAction } from "./game-actions";
import { nextGuideStepAction, prevGuideStepAction, reachGuideStepAction, skipGuideAction } from "./guide-actions";
import { navigateToIslandAction, navigateToWorldAction } from "./route-actions";
import { placeStrongholdAction } from "./setup-actions";
import { closeSlotModalAction } from "./slot-actions";
import {
  armSkillAction,
  cancelSkillAction,
  castSkillAction,
  setCleanupInputAction,
  setCleanupSpeedAction,
  stepCleanupAction,
  toggleCleanupPauseAction,
} from "./cleanup-actions";
import { cancelSoilCleanseAction, pickSoilHexAction, toggleSoilCleanseAction } from "./soil-actions";
import { researchTechAction } from "./tech-actions";
import {
  closeTaxPickAction,
  openTaxPickAction,
  pickTaxFaceAction,
  setCameraAction,
  setHudAnchorsAction,
  skipTaxAnimationAction,
} from "./tax-actions";
import { closeTrailEventAction, moveIslandAction, scoutAction, selectWorldCellAction } from "./world-actions";
import {
  closeFactionsModalAction,
  closeHexModalAction,
  closeTechModalAction,
  hoverHexAction,
  openFactionsModalAction,
  openTechModalAction,
  selectFactionAction,
  selectHexAction,
} from "./ui-actions";
import type { TStore } from "../store/store";
import type { TAppRegistry } from "./registry";

/**
 * Every action is written as `(store, ...args)` and bound to the store once,
 * here. The UI receives a registry of plain callbacks and never sees the store.
 */
const createRegistry = (store: TStore) => {
  const rawRegistry = {
    placeStrongholdAction,
    startGameAction,
    endPhaseAction,
    newGameAction,
    navigateToIslandAction,
    navigateToWorldAction,
    armBuildingAction,
    disarmAction,
    toggleDemolishModeAction,
    buildOnHexAction,
    requestDemolishAction,
    confirmDemolishAction,
    cancelDemolishAction,
    toggleSoilCleanseAction,
    pickSoilHexAction,
    cancelSoilCleanseAction,
    hoverHexAction,
    selectHexAction,
    closeHexModalAction,
    openTechModalAction,
    closeTechModalAction,
    setCameraAction,
    setHudAnchorsAction,
    skipTaxAnimationAction,
    openTaxPickAction,
    closeTaxPickAction,
    pickTaxFaceAction,
    closeSlotModalAction,
    researchTechAction,
    selectWorldCellAction,
    scoutAction,
    moveIslandAction,
    closeTrailEventAction,
    setCleanupInputAction,
    stepCleanupAction,
    setCleanupSpeedAction,
    toggleCleanupPauseAction,
    armSkillAction,
    cancelSkillAction,
    castSkillAction,
    reachGuideStepAction,
    nextGuideStepAction,
    prevGuideStepAction,
    skipGuideAction,
    openFactionsModalAction,
    selectFactionAction,
    closeFactionsModalAction,
  };

  // The actions differ in arity, so the store is bound through one shared shape.
  const registry = Object.entries(rawRegistry).reduce((newRegistry, [name, func]) => {
    const action = func as (store: TStore, ...args: never[]) => void;
    newRegistry[name] = action.bind(null, store);

    return newRegistry;
  }, {} as Record<string, Function>);

  return registry as TAppRegistry;
};

export { createRegistry };
