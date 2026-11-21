import { TECHNOLOGIES } from "./technologies";
import type { TSize } from "./board";
import type { TTechnologyId } from "./types";

/**
 * The technology tree, built by hand like the passive tree of Path of Exile:
 * round nodes and links between them. A link has no direction yet.
 */

/** The box of a node: the circle and the label under it. */
const TECH_NODE_SIZE: TSize = { width: 150, height: 130 };
/** The diameter of the circle at the top of the box. */
const TECH_CIRCLE_SIZE = 84;

const techNodeId = (id: TTechnologyId) => `technology:${id}`;

const techIdOf = (nodeId: string) => nodeId.slice("technology:".length) as TTechnologyId;

const TECH_NODE_IDS = TECHNOLOGIES.map((technology) => techNodeId(technology.id));

export { TECH_CIRCLE_SIZE, TECH_NODE_IDS, TECH_NODE_SIZE, techIdOf, techNodeId };
