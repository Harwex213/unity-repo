import foodIcon from "../assets/icons/food.png";
import hammersIcon from "../assets/icons/hammers.png";
import manaIcon from "../assets/icons/mana.png";
import populationIcon from "../assets/icons/population.png";
import scienceIcon from "../assets/icons/science.png";
import scoutingIcon from "../assets/icons/scouting.png";
import stoneIcon from "../assets/icons/stone.png";
import woodIcon from "../assets/icons/wood.png";
import type { TResource, TYieldResourceId } from "./types";

/**
 * The resources a building die can roll, copied from
 * `javascript/ostrov-prototype/src/core/resources.ts`. Power and the mad are
 * not on any die, so the editor leaves them out.
 */
const RESOURCES: readonly TResource[] = [
  { id: "food", label: "Еда", icon: foodIcon },
  { id: "stone", label: "Камень", icon: stoneIcon },
  { id: "wood", label: "Дерево", icon: woodIcon },
  { id: "population", label: "Население", icon: populationIcon },
  { id: "hammers", label: "Молотки", icon: hammersIcon },
  { id: "science", label: "Наука", icon: scienceIcon },
  { id: "scouting", label: "Разведка", icon: scoutingIcon },
  { id: "mana", label: "Мана", icon: manaIcon },
];

const RESOURCE_BY_ID = new Map(RESOURCES.map((resource) => [resource.id, resource]));

const getResource = (id: TYieldResourceId) => {
  const resource = RESOURCE_BY_ID.get(id);
  if (!resource) {
    throw new Error(`Unknown resource: ${id}`);
  }

  return resource;
};

const isResourceId = (value: unknown): value is TYieldResourceId => {
  return typeof value === "string" && RESOURCE_BY_ID.has(value as TYieldResourceId);
};

export { getResource, isResourceId, RESOURCES };
