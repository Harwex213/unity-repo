import type { TCamera, TPoint } from "../core/board";
import type { TBoardId } from "../core/boards";
import type { TFacePatch } from "../core/links";
import type { TBiomeId, TBuildingId, TModuleId, TTechnologyId, TYieldResourceId } from "../core/types";
import type { TBuildingsView, TSection } from "../store/ui-state";

/** The public contract of the domain layer. It is hand-written, never inferred. */

type TAddBaseFaceAction = (buildingId: TBuildingId) => void;
type TUpdateBaseFaceAction = (buildingId: TBuildingId, index: number, patch: TFacePatch) => void;
type TRemoveBaseFaceAction = (buildingId: TBuildingId, index: number) => void;
type TSetBiomeAllowedAction = (buildingId: TBuildingId, biomeId: TBiomeId, allowed: boolean) => void;
type TUpdateBiomeFaceAction = (buildingId: TBuildingId, biomeId: TBiomeId, patch: TFacePatch) => void;
type TSetYieldsAction = (buildingId: TBuildingId, resource: TYieldResourceId) => void;

type TSelectBuildingAction = (buildingId: TBuildingId) => void;
type TSelectModuleAction = (moduleId: TModuleId) => void;
type TSelectTechnologyAction = (technologyId: TTechnologyId) => void;
type TSetSectionAction = (section: TSection) => void;
type TSetBuildingsViewAction = (view: TBuildingsView) => void;
type TStartRouteSyncAction = () => void;

type TLoadBoardAction = (boardId: TBoardId) => Promise<void>;
type TMoveBoardNodeAction = (boardId: TBoardId, nodeId: string, position: TPoint) => void;
type TSaveBoardAction = (boardId: TBoardId) => Promise<void>;
type TToggleBoardEdgeAction = (boardId: TBoardId, a: string, b: string) => void;
type TSetBoardCameraAction = (boardId: TBoardId, camera: TCamera | null) => void;
type TFocusBoardNodeAction = (boardId: TBoardId, nodeId: string | null) => void;

type TExportLinksAction = () => void;
type TImportLinksAction = (file: File) => Promise<void>;
type TResetLinksAction = () => void;

type TAppRegistry = {
  addBaseFaceAction: TAddBaseFaceAction;
  exportLinksAction: TExportLinksAction;
  focusBoardNodeAction: TFocusBoardNodeAction;
  importLinksAction: TImportLinksAction;
  loadBoardAction: TLoadBoardAction;
  moveBoardNodeAction: TMoveBoardNodeAction;
  removeBaseFaceAction: TRemoveBaseFaceAction;
  resetLinksAction: TResetLinksAction;
  saveBoardAction: TSaveBoardAction;
  selectBuildingAction: TSelectBuildingAction;
  selectModuleAction: TSelectModuleAction;
  selectTechnologyAction: TSelectTechnologyAction;
  setBiomeAllowedAction: TSetBiomeAllowedAction;
  setBoardCameraAction: TSetBoardCameraAction;
  setBuildingsViewAction: TSetBuildingsViewAction;
  setSectionAction: TSetSectionAction;
  setYieldsAction: TSetYieldsAction;
  startRouteSyncAction: TStartRouteSyncAction;
  toggleBoardEdgeAction: TToggleBoardEdgeAction;
  updateBaseFaceAction: TUpdateBaseFaceAction;
  updateBiomeFaceAction: TUpdateBiomeFaceAction;
};

export type {
  TAddBaseFaceAction,
  TAppRegistry,
  TExportLinksAction,
  TFocusBoardNodeAction,
  TImportLinksAction,
  TLoadBoardAction,
  TMoveBoardNodeAction,
  TRemoveBaseFaceAction,
  TResetLinksAction,
  TSaveBoardAction,
  TSelectBuildingAction,
  TSelectModuleAction,
  TSelectTechnologyAction,
  TSetBiomeAllowedAction,
  TSetBoardCameraAction,
  TSetBuildingsViewAction,
  TSetSectionAction,
  TSetYieldsAction,
  TStartRouteSyncAction,
  TToggleBoardEdgeAction,
  TUpdateBaseFaceAction,
  TUpdateBiomeFaceAction,
};
