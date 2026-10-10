import type { TCleanupSpeed } from "../store/battle-state";
import type { TSkillTarget } from "../core/cleanup-sim";
import type { TSkillId } from "../core/skills";
import type { TFactionId } from "../core/factions";
import type { TTechId } from "../core/techs";
import type { TBuildingId } from "../core/types";
import type { TDifficulty } from "../core/main-menu";
import type { TStartFromMenuOptions } from "./menu-actions";
import type { TCamera, THudAnchorId, TPointerAnchor } from "../store/ui-state";

/**
 * The public contract of the domain layer. It is hand-written, never inferred:
 * the phases after the build phase add their own action types here.
 */

type TPlaceStrongholdAction = (hexId: string) => void;
type TStartGameAction = () => void;
type TEndPhaseAction = () => void;
type TNewGameAction = () => void;

type TNavigateToIslandAction = (playerId: string | null) => void;
type TNavigateToWorldAction = () => void;

type TArmBuildingAction = (buildingId: TBuildingId) => void;
type TDisarmAction = () => void;
type TToggleDemolishModeAction = () => void;
type TBuildOnHexAction = (hexId: string) => void;
type TRequestDemolishAction = (hexId: string) => void;
type TConfirmDemolishAction = (skipNextTime: boolean) => void;
type TCancelDemolishAction = () => void;
type TToggleSoilCleanseAction = () => void;
type TPickSoilHexAction = (hexId: string) => void;
type TCancelSoilCleanseAction = () => void;

type THoverHexAction = (hexId: string | null, anchor: TPointerAnchor | null) => void;
type TSelectHexAction = (hexId: string) => void;
type TCloseHexModalAction = () => void;
type TOpenTechModalAction = () => void;
type TSetCameraAction = (camera: TCamera) => void;
type TSetHudAnchorsAction = (anchors: Readonly<Partial<Record<THudAnchorId, TPointerAnchor>>>) => void;
type TSkipTaxAnimationAction = () => void;
type TOpenTaxPickAction = (hexId: string) => void;
type TCloseTaxPickAction = () => void;
type TPickTaxFaceAction = (hexId: string, faceIndex: number) => void;
type TCloseSlotModalAction = () => void;

type TResearchTechAction = (techId: TTechId) => void;
type TSelectWorldCellAction = (cellId: string) => void;
type TScoutAction = (cellId: string) => void;
type TMoveIslandAction = (cellId: string) => void;
type TCloseTrailEventAction = () => void;
type TSetCleanupInputAction = (x: number, y: number) => void;
type TStepCleanupAction = (ticks: number) => void;
type TSetCleanupSpeedAction = (speed: TCleanupSpeed) => void;
type TToggleCleanupPauseAction = () => void;
type TArmSkillAction = (skillId: TSkillId) => void;
type TCancelSkillAction = () => void;
type TCastSkillAction = (target: TSkillTarget) => void;
type TCloseTechModalAction = () => void;

type TOpenFactionsModalAction = (factionId?: TFactionId | null) => void;
type TSelectFactionAction = (factionId: TFactionId) => void;
type TCloseFactionsModalAction = () => void;

type TReachGuideStepAction = () => void;
type TNextGuideStepAction = () => void;
type TPrevGuideStepAction = () => void;
type TSkipGuideAction = () => void;

type TOpenMainMenuAction = () => void;
type TStartFromMenuAction = (options: TStartFromMenuOptions) => void;
type TExitMenuAction = () => void;
type TSetDifficultyAction = (difficulty: TDifficulty) => void;
type TOpenMenuCreditsAction = () => void;
type TCloseMenuCreditsAction = () => void;

type TAppRegistry = {
  placeStrongholdAction: TPlaceStrongholdAction;
  startGameAction: TStartGameAction;
  endPhaseAction: TEndPhaseAction;
  newGameAction: TNewGameAction;
  navigateToIslandAction: TNavigateToIslandAction;
  navigateToWorldAction: TNavigateToWorldAction;
  armBuildingAction: TArmBuildingAction;
  disarmAction: TDisarmAction;
  toggleDemolishModeAction: TToggleDemolishModeAction;
  buildOnHexAction: TBuildOnHexAction;
  requestDemolishAction: TRequestDemolishAction;
  confirmDemolishAction: TConfirmDemolishAction;
  cancelDemolishAction: TCancelDemolishAction;
  toggleSoilCleanseAction: TToggleSoilCleanseAction;
  pickSoilHexAction: TPickSoilHexAction;
  cancelSoilCleanseAction: TCancelSoilCleanseAction;
  hoverHexAction: THoverHexAction;
  selectHexAction: TSelectHexAction;
  closeHexModalAction: TCloseHexModalAction;
  openTechModalAction: TOpenTechModalAction;
  closeTechModalAction: TCloseTechModalAction;
  setCameraAction: TSetCameraAction;
  setHudAnchorsAction: TSetHudAnchorsAction;
  skipTaxAnimationAction: TSkipTaxAnimationAction;
  openTaxPickAction: TOpenTaxPickAction;
  closeTaxPickAction: TCloseTaxPickAction;
  pickTaxFaceAction: TPickTaxFaceAction;
  closeSlotModalAction: TCloseSlotModalAction;
  researchTechAction: TResearchTechAction;
  selectWorldCellAction: TSelectWorldCellAction;
  scoutAction: TScoutAction;
  moveIslandAction: TMoveIslandAction;
  closeTrailEventAction: TCloseTrailEventAction;
  setCleanupInputAction: TSetCleanupInputAction;
  stepCleanupAction: TStepCleanupAction;
  setCleanupSpeedAction: TSetCleanupSpeedAction;
  toggleCleanupPauseAction: TToggleCleanupPauseAction;
  armSkillAction: TArmSkillAction;
  cancelSkillAction: TCancelSkillAction;
  castSkillAction: TCastSkillAction;
  reachGuideStepAction: TReachGuideStepAction;
  nextGuideStepAction: TNextGuideStepAction;
  prevGuideStepAction: TPrevGuideStepAction;
  skipGuideAction: TSkipGuideAction;
  openFactionsModalAction: TOpenFactionsModalAction;
  selectFactionAction: TSelectFactionAction;
  closeFactionsModalAction: TCloseFactionsModalAction;
  openMainMenuAction: TOpenMainMenuAction;
  startFromMenuAction: TStartFromMenuAction;
  exitMenuAction: TExitMenuAction;
  setDifficultyAction: TSetDifficultyAction;
  openMenuCreditsAction: TOpenMenuCreditsAction;
  closeMenuCreditsAction: TCloseMenuCreditsAction;
};

export type {
  TAppRegistry,
  TCloseTrailEventAction,
  TMoveIslandAction,
  TResearchTechAction,
  TScoutAction,
  TSelectWorldCellAction,
  TSetCleanupInputAction,
  TSetCleanupSpeedAction,
  TStepCleanupAction,
  TToggleCleanupPauseAction,
  TArmSkillAction,
  TCancelSkillAction,
  TCastSkillAction,
  TArmBuildingAction,
  TBuildOnHexAction,
  TCancelDemolishAction,
  TCancelSoilCleanseAction,
  TCloseFactionsModalAction,
  TCloseHexModalAction,
  TCloseMenuCreditsAction,
  TExitMenuAction,
  TOpenMainMenuAction,
  TOpenMenuCreditsAction,
  TSetDifficultyAction,
  TStartFromMenuAction,
  TCloseSlotModalAction,
  TCloseTaxPickAction,
  TCloseTechModalAction,
  TConfirmDemolishAction,
  TDisarmAction,
  TEndPhaseAction,
  THoverHexAction,
  TNavigateToIslandAction,
  TNavigateToWorldAction,
  TNewGameAction,
  TNextGuideStepAction,
  TOpenFactionsModalAction,
  TOpenTaxPickAction,
  TOpenTechModalAction,
  TPickSoilHexAction,
  TPickTaxFaceAction,
  TPlaceStrongholdAction,
  TPrevGuideStepAction,
  TReachGuideStepAction,
  TRequestDemolishAction,
  TSelectHexAction,
  TSetCameraAction,
  TSetHudAnchorsAction,
  TSelectFactionAction,
  TSkipGuideAction,
  TSkipTaxAnimationAction,
  TStartGameAction,
  TToggleDemolishModeAction,
  TToggleSoilCleanseAction,
};
