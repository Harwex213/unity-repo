import converterIcon from "../assets/icons/converter.png";
import farmIcon from "../assets/icons/farm.png";
import masonsGuildIcon from "../assets/icons/masons-guild.png";
import mineIcon from "../assets/icons/mine.png";
import observatoryIcon from "../assets/icons/observatory.png";
import sawmillIcon from "../assets/icons/sawmill.png";
import universityIcon from "../assets/icons/university.png";
import villageIcon from "../assets/icons/village.png";
import { BIOMES } from "./biomes";
import type { TBiomeId, TBuildingId, TBuildingInfo, TBuildingLinks, TFace, TLinks, TYieldResourceId } from "./types";

const BUILDINGS: readonly TBuildingInfo[] = [
  { id: "farm", label: "Ферма", art: farmIcon },
  { id: "mine", label: "Рудник", art: mineIcon },
  { id: "sawmill", label: "Лесопилка", art: sawmillIcon },
  { id: "village", label: "Деревня", art: villageIcon },
  { id: "masons_guild", label: "Гильдия масонов", art: masonsGuildIcon },
  { id: "observatory", label: "Обсерватория", art: observatoryIcon },
  { id: "university", label: "Университет", art: universityIcon },
  { id: "converter", label: "Центральный конвертер", art: converterIcon },
];

const BUILDING_BY_ID = new Map(BUILDINGS.map((building) => [building.id, building]));

const getBuilding = (id: TBuildingId) => {
  const building = BUILDING_BY_ID.get(id);
  if (!building) {
    throw new Error(`Unknown building: ${id}`);
  }

  return building;
};

const isBuildingId = (value: unknown): value is TBuildingId => {
  return typeof value === "string" && BUILDING_BY_ID.has(value as TBuildingId);
};

const face = (resource: TYieldResourceId, amount: number, toxicity: number): TFace => ({
  resource,
  amount,
  toxicity,
});

/** Village, masons guild, observatory and university share the numbers; only the resource differs. */
const settlementLinks = (id: TBuildingId, resource: TYieldResourceId): TBuildingLinks => ({
  id,
  yields: resource,
  baseFaces: [face(resource, 1, 0), face(resource, 2, 2), face(resource, 3, 3), face(resource, 1, 1)],
  biomeFaces: {
    grassland: face(resource, 3, 1),
    plains: face(resource, 2, 1),
    desert: face(resource, 1, 2),
    tundra: face(resource, 2, 2),
    polar_desert: face(resource, 1, 3),
    swamp: face(resource, 2, 3),
    badlands: face(resource, 1, 4),
    cliffs: face(resource, 1, 0),
  },
});

/**
 * The links of `javascript/ostrov-prototype/src/core/buildings.ts`. The editor
 * starts from them, and "Сбросить" returns to them.
 */
const DEFAULT_LINKS: TLinks = {
  farm: {
    id: "farm",
    yields: "food",
    baseFaces: [face("food", 1, 0), face("food", 5, 0), face("food", 3, 0), face("food", 3, 0)],
    biomeFaces: {
      grassland: face("food", 4, 1),
      plains: face("food", 2, 0),
      tundra: face("food", 2, 0),
      swamp: face("food", 5, 3),
      hills: face("food", 3, 1),
    },
  },
  mine: {
    id: "mine",
    yields: "stone",
    baseFaces: [face("stone", 1, 0), face("stone", 5, 3), face("stone", 3, 2), face("stone", 3, 1)],
    biomeFaces: {
      mountains: face("stone", 7, 3),
      volcano: face("stone", 10, 5),
      crater: face("stone", 3, 1),
      cliffs: face("stone", 4, 1),
      swamp: face("stone", 2, 1),
    },
  },
  sawmill: {
    id: "sawmill",
    yields: "wood",
    baseFaces: [face("wood", 1, 0), face("wood", 5, 3), face("wood", 3, 2), face("wood", 3, 1)],
    biomeFaces: {
      forrest: face("wood", 5, 1),
      savanna: face("wood", 2, 0),
      rainforest: face("wood", 8, 4),
      taiga: face("wood", 5, 2),
    },
  },
  village: settlementLinks("village", "population"),
  masons_guild: settlementLinks("masons_guild", "hammers"),
  observatory: settlementLinks("observatory", "scouting"),
  university: settlementLinks("university", "science"),
  converter: {
    id: "converter",
    yields: "mana",
    baseFaces: [face("mana", 1, 0), face("mana", 2, 0), face("mana", 3, 0), face("mana", 2, 0)],
    biomeFaces: Object.fromEntries(BIOMES.map((biome): [TBiomeId, TFace] => [biome.id, face("mana", 2, 0)])),
  },
};

export { BUILDINGS, DEFAULT_LINKS, face, getBuilding, isBuildingId };
