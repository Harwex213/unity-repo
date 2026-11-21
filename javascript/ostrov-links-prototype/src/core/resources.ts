import electricityIcon from "../assets/icons/electricity.png";
import foodIcon from "../assets/icons/food.png";
import manaIcon from "../assets/icons/mana.png";
import mechanismsIcon from "../assets/icons/mechanisms.png";
import microprocessorsIcon from "../assets/icons/microprocessors.png";
import populationIcon from "../assets/icons/population.png";
import scienceIcon from "../assets/icons/science.png";
import stoneIcon from "../assets/icons/stone.png";
import woodIcon from "../assets/icons/wood.png";
import type { TResource, TYieldResourceId } from "./types";

/**
 * The resources a building die can roll. Power ("власть") changes a rolled
 * face and the mad are people, so neither is on a die.
 */
const RESOURCES: readonly TResource[] = [
  { id: "food", label: "Еда", icon: foodIcon },
  { id: "stone", label: "Камень", icon: stoneIcon },
  { id: "wood", label: "Дерево", icon: woodIcon },
  { id: "population", label: "Население", icon: populationIcon },
  { id: "mechanisms", label: "Механизмы", icon: mechanismsIcon },
  { id: "microprocessors", label: "Микропроцессоры", icon: microprocessorsIcon },
  { id: "science", label: "Наука", icon: scienceIcon },
  { id: "electricity", label: "Электричество", icon: electricityIcon },
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

/** Old resource ids in saved files and in `localStorage`. */
const LEGACY_RESOURCE_IDS: Readonly<Record<string, TYieldResourceId>> = {
  hammers: "mechanisms",
  scouting: "electricity",
};

/** Reads a resource id, an old one too. Returns null for an unknown id. */
const readResourceId = (value: unknown): TYieldResourceId | null => {
  if (isResourceId(value)) {
    return value;
  }

  return typeof value === "string" ? (LEGACY_RESOURCE_IDS[value] ?? null) : null;
};

export { getResource, isResourceId, readResourceId, RESOURCES };
