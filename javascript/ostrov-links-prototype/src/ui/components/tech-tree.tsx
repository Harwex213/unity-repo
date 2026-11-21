import { useSignals } from "@preact/signals-react/runtime";
import scienceIcon from "../../assets/icons/science.png";
import { TECH_CIRCLE_SIZE, TECH_NODE_SIZE, techIdOf, techNodeId } from "../../core/tech-tree";
import { getTechnology } from "../../core/technologies";
import { useStore } from "../../store/store";
import { BoardCanvas } from "./board-canvas";
import { Icon } from "./icon";
import type { FC } from "react";
import type {
  TFocusBoardNodeAction,
  TMoveBoardNodeAction,
  TSaveBoardAction,
  TSelectTechnologyAction,
  TSetBoardCameraAction,
  TToggleBoardEdgeAction,
} from "../../domain/registry";

type TTechTreeProps = {
  registry: {
    focusBoardNodeAction: TFocusBoardNodeAction;
    moveBoardNodeAction: TMoveBoardNodeAction;
    saveBoardAction: TSaveBoardAction;
    selectTechnologyAction: TSelectTechnologyAction;
    setBoardCameraAction: TSetBoardCameraAction;
    toggleBoardEdgeAction: TToggleBoardEdgeAction;
  };
};

/** The gap between the top of a node box and its circle. */
const CIRCLE_TOP = 4;

/**
 * The technology tree, built by hand like the passive tree of Path of Exile.
 * A click selects a technology. Shift + click links the selected technology
 * with the clicked one, or removes the link.
 */
const TechTree: FC<TTechTreeProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const selectedNodeId = techNodeId(store.ui.selectedTechnologyId.value);

  return (
    <BoardCanvas
      boardId="technologies"
      registry={registry}
      edgeAnchor={{ x: TECH_NODE_SIZE.width / 2, y: CIRCLE_TOP + TECH_CIRCLE_SIZE / 2 }}
      highlightNodeId={selectedNodeId}
      hint="Shift + клик — связать с выбранной"
      nodeClassName={(nodeId) => (nodeId === selectedNodeId ? "tech-node tech-node--selected" : "tech-node")}
      renderNode={(nodeId) => {
        const technology = getTechnology(techIdOf(nodeId));

        return (
          <>
            <span className="tech-node-circle" style={{ width: TECH_CIRCLE_SIZE, height: TECH_CIRCLE_SIZE }}>
              <img src={technology.icon} alt="" width={56} height={56} draggable={false} />
            </span>
            <span className="tech-node-label">{technology.label}</span>
            <span className="tech-node-cost">
              <Icon src={scienceIcon} label="Наука" />
              {technology.science}
            </span>
          </>
        );
      }}
      onNodeClick={(nodeId, { shiftKey }) => {
        if (shiftKey) {
          registry.toggleBoardEdgeAction("technologies", selectedNodeId, nodeId);
          return;
        }

        registry.selectTechnologyAction(techIdOf(nodeId));
      }}
    />
  );
};

export { TechTree };
