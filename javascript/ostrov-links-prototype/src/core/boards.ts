import { GRAPH_NODE_IDS, GRAPH_NODE_SIZE } from "./graph";
import { TECH_NODE_IDS, TECH_NODE_SIZE } from "./tech-tree";
import type { TSize } from "./board";

type TBoardId = "buildings" | "technologies";

type TBoardConfig = {
  /** Served at `data/<file>` and kept in git as `data/<file>`. */
  readonly file: string;
  readonly nodeIds: readonly string[];
  readonly nodeSize: TSize;
};

const BOARDS: Readonly<Record<TBoardId, TBoardConfig>> = {
  buildings: { file: "buildings-graph.json", nodeIds: GRAPH_NODE_IDS, nodeSize: GRAPH_NODE_SIZE },
  technologies: { file: "tech-tree.json", nodeIds: TECH_NODE_IDS, nodeSize: TECH_NODE_SIZE },
};

export type { TBoardConfig, TBoardId };
export { BOARDS };
