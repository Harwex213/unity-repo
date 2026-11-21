import artilleryTowerIcon from "../assets/icons/artillery-tower.png";
import barricadesIcon from "../assets/icons/barricades.png";
import converterIcon from "../assets/icons/converter.png";
import cyberpsychoGhettoIcon from "../assets/icons/cyberpsycho-ghetto.png";
import electricStationIcon from "../assets/icons/electric-station.png";
import farmIcon from "../assets/icons/farm.png";
import forgeIcon from "../assets/icons/forge.png";
import masonsGuildIcon from "../assets/icons/masons-guild.png";
import mineIcon from "../assets/icons/mine.png";
import observatoryIcon from "../assets/icons/observatory.png";
import sawmillIcon from "../assets/icons/sawmill.png";
import survivorsOutpostIcon from "../assets/icons/survivors-outpost.png";
import universityIcon from "../assets/icons/university.png";
import villageIcon from "../assets/icons/village.png";
import waterTowerIcon from "../assets/icons/water-tower.png";
import { BIOMES } from "./biomes";
import type {
  TBiomeId,
  TBuildingId,
  TBuildingInfo,
  TBuildingLinks,
  TBuildingRole,
  TFace,
  TLinks,
  TYieldResourceId,
} from "./types";

/** The order follows the design map: production, boosters, defense. */
const BUILDINGS: readonly TBuildingInfo[] = [
  {
    id: "survivors_outpost",
    label: "Форпост выживших",
    art: survivorsOutpostIcon,
    role: "producer",
    rules: [
      "Самое простое здание. Оно доступно с начала игры.",
      "Ресурс зависит от биома: грань биома даёт ресурс, типичный для этого биома.",
    ],
  },
  { id: "farm", label: "Ферма", art: farmIcon, role: "producer" },
  {
    id: "water_tower",
    label: "Водонапорная башня",
    art: waterTowerIcon,
    role: "booster",
    rules: ["Усиливает производство еды у зданий рядом."],
  },
  {
    id: "forge",
    label: "Кузница",
    art: forgeIcon,
    role: "booster",
    rules: ["Усиливает производство дерева, камня и механизмов у зданий рядом."],
  },
  { id: "village", label: "Деревня", art: villageIcon, role: "producer" },
  { id: "mine", label: "Рудник", art: mineIcon, role: "producer" },
  { id: "sawmill", label: "Лесопилка", art: sawmillIcon, role: "producer" },
  { id: "masons_guild", label: "Гильдия масонов", art: masonsGuildIcon, role: "producer" },
  { id: "electric_station", label: "Электростанция", art: electricStationIcon, role: "producer" },
  { id: "university", label: "Университет", art: universityIcon, role: "producer" },
  {
    id: "observatory",
    label: "Обсерватория",
    art: observatoryIcon,
    role: "booster",
    rules: ["Усиливает производство науки и маны у зданий рядом."],
  },
  { id: "converter", label: "Центральный конвертер", art: converterIcon, role: "producer" },
  {
    id: "artillery_tower",
    label: "Артиллерийская башня",
    art: artilleryTowerIcon,
    role: "defense",
    rules: ["Дальнобойная защита: башня стреляет по врагам на расстоянии."],
  },
  {
    id: "barricades",
    label: "Заграждения",
    art: barricadesIcon,
    role: "defense",
    rules: ["Пассивная защита: мины и ежи мешают врагам двигаться."],
  },
  {
    id: "cyberpsycho_ghetto",
    label: "Гетто киберпсихов",
    art: cyberpsychoGhettoIcon,
    role: "producer",
    rules: [
      "В гетто можно селить только сумасшедших.",
      "Сумасшедшие в гетто не строят модуль «Гедонистический рай».",
      "Сумасшедшие в гетто добавляют зданию меньше фиксированной токсичности, чем в других зданиях.",
    ],
  },
];

const ROLE_LABELS: Readonly<Record<TBuildingRole, string>> = {
  producer: "Производство",
  booster: "Усилитель",
  defense: "Защита",
};

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

/** Only a producer has a die. */
const hasDie = (id: TBuildingId) => getBuilding(id).role === "producer";

const face = (resource: TYieldResourceId, amount: number, toxicity: number): TFace => ({
  resource,
  amount,
  toxicity,
});

/**
 * Village, masons guild and university share the numbers; only the resource differs.
 * The cyberpsycho ghetto copies them too, until it gets its own numbers.
 */
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

/** A building without a die: the listed biomes allow it and add no face. */
const noDieLinks = (id: TBuildingId, biomes: readonly TBiomeId[]): TBuildingLinks => ({
  id,
  yields: null,
  baseFaces: [],
  biomeFaces: Object.fromEntries(biomes.map((biomeId) => [biomeId, null])),
});

/** The resource a biome is known for. The survivors outpost rolls it on the biome face. */
const OUTPOST_BIOME_RESOURCES: Readonly<Record<TBiomeId, TYieldResourceId>> = {
  grassland: "food",
  plains: "food",
  savanna: "food",
  swamp: "food",
  tundra: "food",
  forrest: "wood",
  rainforest: "wood",
  taiga: "wood",
  hills: "stone",
  mountains: "stone",
  cliffs: "stone",
  crater: "stone",
  badlands: "stone",
  volcano: "stone",
  desert: "stone",
  polar_desert: "stone",
};

const ALL_BIOME_IDS = BIOMES.map((biome) => biome.id);

/**
 * The first links come from `javascript/ostrov-prototype/src/core/buildings.ts`.
 * The buildings of the design map that the prototype lacks have draft numbers. The editor
 * starts from them, and "Сбросить" returns to them.
 */
const DEFAULT_LINKS: TLinks = {
  survivors_outpost: {
    id: "survivors_outpost",
    yields: "food",
    baseFaces: [face("food", 1, 0), face("food", 1, 0), face("wood", 1, 0), face("stone", 1, 0)],
    biomeFaces: Object.fromEntries(
      BIOMES.map((biome): [TBiomeId, TFace] => [biome.id, face(OUTPOST_BIOME_RESOURCES[biome.id], 2, 0)]),
    ),
  },
  water_tower: noDieLinks("water_tower", [
    "grassland",
    "plains",
    "savanna",
    "swamp",
    "tundra",
    "hills",
    "forrest",
    "rainforest",
  ]),
  forge: noDieLinks("forge", [
    "hills",
    "mountains",
    "forrest",
    "taiga",
    "plains",
    "grassland",
    "cliffs",
    "crater",
    "badlands",
    "volcano",
  ]),
  electric_station: {
    id: "electric_station",
    yields: "electricity",
    baseFaces: [
      face("electricity", 1, 1),
      face("electricity", 2, 2),
      face("electricity", 3, 3),
      face("electricity", 2, 1),
    ],
    biomeFaces: {
      plains: face("electricity", 2, 1),
      grassland: face("electricity", 2, 1),
      hills: face("electricity", 3, 1),
      mountains: face("electricity", 3, 2),
      cliffs: face("electricity", 3, 1),
      desert: face("electricity", 4, 2),
      tundra: face("electricity", 2, 1),
      volcano: face("electricity", 5, 3),
    },
  },
  artillery_tower: noDieLinks("artillery_tower", ALL_BIOME_IDS),
  barricades: noDieLinks("barricades", ALL_BIOME_IDS),
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
  masons_guild: settlementLinks("masons_guild", "mechanisms"),
  observatory: noDieLinks("observatory", [
    "hills",
    "mountains",
    "cliffs",
    "desert",
    "polar_desert",
    "tundra",
    "plains",
    "grassland",
  ]),
  university: settlementLinks("university", "science"),
  cyberpsycho_ghetto: settlementLinks("cyberpsycho_ghetto", "mechanisms"),
  converter: {
    id: "converter",
    yields: "mana",
    baseFaces: [face("mana", 1, 0), face("mana", 2, 0), face("mana", 3, 0), face("mana", 2, 0)],
    biomeFaces: Object.fromEntries(BIOMES.map((biome): [TBiomeId, TFace] => [biome.id, face("mana", 2, 0)])),
  },
};

export { BUILDINGS, DEFAULT_LINKS, face, getBuilding, hasDie, isBuildingId, ROLE_LABELS };
