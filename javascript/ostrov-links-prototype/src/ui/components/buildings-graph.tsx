import { GRAPH_NODES } from "../../core/graph";
import { BoardCanvas } from "./board-canvas";
import { Icon } from "./icon";
import type { FC } from "react";
import type {
  TFocusBoardNodeAction,
  TMoveBoardNodeAction,
  TSaveBoardAction,
  TSelectBuildingAction,
  TSelectModuleAction,
  TSetBoardCameraAction,
} from "../../domain/registry";

type TBuildingsGraphProps = {
  registry: {
    focusBoardNodeAction: TFocusBoardNodeAction;
    moveBoardNodeAction: TMoveBoardNodeAction;
    saveBoardAction: TSaveBoardAction;
    selectBuildingAction: TSelectBuildingAction;
    selectModuleAction: TSelectModuleAction;
    setBoardCameraAction: TSetBoardCameraAction;
  };
};

const NODE_BY_ID = new Map(GRAPH_NODES.map((node) => [node.id, node]));

/** "Граф": a card for every building and module. A double click opens the card in its list. */
const BuildingsGraph: FC<TBuildingsGraphProps> = ({ registry }) => {
  return (
    <BoardCanvas
      boardId="buildings"
      registry={registry}
      nodeClassName={(nodeId) => `graph-card graph-card--${NODE_BY_ID.get(nodeId)?.kind ?? "building"}`}
      renderNode={(nodeId) => {
        const node = NODE_BY_ID.get(nodeId);
        if (!node) {
          return null;
        }

        return (
          <>
            <Icon src={node.art} size="l" />
            <span className="graph-card-text">
              <span className="graph-card-label">{node.label}</span>
              <span className="graph-card-subtitle">{node.subtitle}</span>
            </span>
          </>
        );
      }}
      onNodeDoubleClick={(nodeId) => {
        const node = NODE_BY_ID.get(nodeId);
        if (node?.kind === "building") {
          registry.selectBuildingAction(node.refId);
        }
        if (node?.kind === "module") {
          registry.selectModuleAction(node.refId);
        }
      }}
    />
  );
};

export { BuildingsGraph };
