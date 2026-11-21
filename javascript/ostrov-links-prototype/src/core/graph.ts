import { BUILDINGS, ROLE_LABELS } from "./buildings";
import { MODULES } from "./modules";
import type { TSize } from "./board";
import type { TBuildingId, TModuleId } from "./types";

/** The "Граф" tab: a card for every building and every module. The board has no links yet. */

type TGraphNode =
  | {
      readonly id: string;
      readonly kind: "building";
      readonly refId: TBuildingId;
      readonly label: string;
      readonly subtitle: string;
      readonly art: string;
    }
  | {
      readonly id: string;
      readonly kind: "module";
      readonly refId: TModuleId;
      readonly label: string;
      readonly subtitle: string;
      readonly art: string;
    };

/** Every card has the same size. The first layout copies a hand-drawn map with this card size. */
const GRAPH_NODE_SIZE: TSize = { width: 240, height: 76 };

const GRAPH_NODES: readonly TGraphNode[] = [
  ...BUILDINGS.map((building): TGraphNode => ({
    id: `building:${building.id}`,
    kind: "building",
    refId: building.id,
    label: building.label,
    subtitle: ROLE_LABELS[building.role],
    art: building.art,
  })),
  ...MODULES.map((module): TGraphNode => ({
    id: `module:${module.id}`,
    kind: "module",
    refId: module.id,
    label: module.label,
    subtitle: "Модуль",
    art: module.art,
  })),
];

const GRAPH_NODE_IDS = GRAPH_NODES.map((node) => node.id);

export type { TGraphNode };
export { GRAPH_NODE_IDS, GRAPH_NODE_SIZE, GRAPH_NODES };
