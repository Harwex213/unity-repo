import type { TFacePatch } from "../core/links";
import type { TBiomeId, TBuildingId, TYieldResourceId } from "../core/types";
import type { TView } from "../store/ui-state";

/** The public contract of the domain layer. It is hand-written, never inferred. */

type TAddBaseFaceAction = (buildingId: TBuildingId) => void;
type TUpdateBaseFaceAction = (buildingId: TBuildingId, index: number, patch: TFacePatch) => void;
type TRemoveBaseFaceAction = (buildingId: TBuildingId, index: number) => void;
type TSetBiomeAllowedAction = (buildingId: TBuildingId, biomeId: TBiomeId, allowed: boolean) => void;
type TUpdateBiomeFaceAction = (buildingId: TBuildingId, biomeId: TBiomeId, patch: TFacePatch) => void;
type TSetYieldsAction = (buildingId: TBuildingId, resource: TYieldResourceId) => void;

type TSelectBuildingAction = (buildingId: TBuildingId) => void;
type TSetViewAction = (view: TView) => void;

type TExportLinksAction = () => void;
type TImportLinksAction = (file: File) => Promise<void>;
type TResetLinksAction = () => void;

type TAppRegistry = {
  addBaseFaceAction: TAddBaseFaceAction;
  exportLinksAction: TExportLinksAction;
  importLinksAction: TImportLinksAction;
  removeBaseFaceAction: TRemoveBaseFaceAction;
  resetLinksAction: TResetLinksAction;
  selectBuildingAction: TSelectBuildingAction;
  setBiomeAllowedAction: TSetBiomeAllowedAction;
  setViewAction: TSetViewAction;
  setYieldsAction: TSetYieldsAction;
  updateBaseFaceAction: TUpdateBaseFaceAction;
  updateBiomeFaceAction: TUpdateBiomeFaceAction;
};

export type {
  TAddBaseFaceAction,
  TAppRegistry,
  TExportLinksAction,
  TImportLinksAction,
  TRemoveBaseFaceAction,
  TResetLinksAction,
  TSelectBuildingAction,
  TSetBiomeAllowedAction,
  TSetViewAction,
  TSetYieldsAction,
  TUpdateBaseFaceAction,
  TUpdateBiomeFaceAction,
};
