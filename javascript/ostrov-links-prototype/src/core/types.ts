/**
 * The data model of the links editor. Ids and labels come from
 * `javascript/ostrov-prototype/src/core/types.ts`. Everything here is plain
 * data: the store keeps it inside signals, and nothing here imports React.
 */

/** The 16 hex biomes, spelled exactly as the spec spells them. */
type TBiomeId =
  | "grassland"
  | "plains"
  | "forrest"
  | "savanna"
  | "rainforest"
  | "taiga"
  | "tundra"
  | "desert"
  | "polar_desert"
  | "swamp"
  | "badlands"
  | "crater"
  | "volcano"
  | "hills"
  | "mountains"
  | "cliffs";

/** Resources a building die can roll. Power is not on any die: it changes a rolled face. */
type TYieldResourceId =
  "food" | "stone" | "wood" | "population" | "mechanisms" | "microprocessors" | "science" | "electricity" | "mana";

type TBuildingId =
  | "survivors_outpost"
  | "farm"
  | "water_tower"
  | "forge"
  | "mine"
  | "sawmill"
  | "village"
  | "masons_guild"
  | "observatory"
  | "university"
  | "converter"
  | "electric_station"
  | "artillery_tower"
  | "barricades"
  | "cyberpsycho_ghetto";

/**
 * `producer` has a die and brings resources. `booster` and `defense` have no die:
 * a booster raises the production of the buildings nearby, a defense fights enemies.
 */
type TBuildingRole = "producer" | "booster" | "defense";

/** One face of a building die: a yield and the toxicity it leaves on the hex. */
type TFace = {
  readonly resource: TYieldResourceId;
  readonly amount: number;
  readonly toxicity: number;
};

type TBiome = {
  readonly id: TBiomeId;
  readonly label: string;
  readonly description: string;
  readonly color: string;
  /** A small picture of the biome for the biome cards. */
  readonly art: string;
};

type TResource = {
  readonly id: TYieldResourceId;
  readonly label: string;
  readonly icon: string;
};

/** The fixed part of a building: the editor does not change it. */
type TBuildingInfo = {
  readonly id: TBuildingId;
  readonly label: string;
  readonly art: string;
  readonly role: TBuildingRole;
  /** Rules of the building that the die does not show. */
  readonly rules?: readonly string[];
};

/**
 * The editable part of a building: the links to resources and to biomes.
 * The shape matches `buildings[]` of the game config in `javascript/ostrov-config`.
 */
type TBuildingLinks = {
  readonly id: TBuildingId;
  /** The main resource of the building. A new face starts with it. Null for a building without a die. */
  readonly yields: TYieldResourceId | null;
  /** Faces every copy of the building has, on any biome. Empty for a building without a die. */
  readonly baseFaces: readonly TFace[];
  /**
   * The extra face a biome adds. A biome missing here cannot host the building.
   * A building without a die keeps `null` here: the biome allows it and adds no face.
   */
  readonly biomeFaces: Readonly<Partial<Record<TBiomeId, TFace | null>>>;
};

type TLinks = Readonly<Record<TBuildingId, TBuildingLinks>>;

export type {
  TBiome,
  TBiomeId,
  TBuildingId,
  TBuildingInfo,
  TBuildingLinks,
  TBuildingRole,
  TFace,
  TLinks,
  TResource,
  TYieldResourceId,
};

/** The modules a building can hold, besides its own die. */
type TModuleId =
  | "greenhouse"
  | "assembly_shop"
  | "saw_frame"
  | "stone_crusher"
  | "resonator"
  | "dynamo"
  | "turret"
  | "tripwire"
  | "hedonistic_paradise";

/** One block of the module card: a title and a list of rules. */
type TModuleSection = {
  readonly title: string;
  readonly icon?: string;
  readonly rules: readonly string[];
};

/**
 * A module sits in one of the four slots of a building. The player usually
 * buys a module for stone, wood, mechanisms or microprocessors. Some modules
 * appear by themselves. The fields are plain text for now: the numbers of
 * the module are not designed yet.
 */
type TModule = {
  readonly id: TModuleId;
  readonly label: string;
  readonly art: string;
  readonly summary: string;
  readonly sections: readonly TModuleSection[];
};

export type { TModule, TModuleId, TModuleSection };

/** Every building needs one technology. */
type TTechnologyId =
  | "survival"
  | "agriculture"
  | "settlement"
  | "logging"
  | "mining"
  | "irrigation"
  | "mechanics"
  | "smithing"
  | "education"
  | "fortification"
  | "electrification"
  | "ballistics"
  | "quarantine"
  | "astronomy"
  | "mana_conversion";

type TTechnology = {
  readonly id: TTechnologyId;
  readonly label: string;
  /** A small icon for lists. For now it is the icon of the building the technology opens. */
  readonly icon: string;
  /** A big picture for the technology card. */
  readonly art: string;
  /** One line for lists. */
  readonly summary: string;
  readonly description: string;
  /** How much science the technology costs. */
  readonly science: number;
  /** The building the technology opens. */
  readonly unlocks: TBuildingId;
};

export type { TTechnology, TTechnologyId };
