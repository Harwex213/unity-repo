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

/** Resources a building die can roll. */
type TYieldResourceId = "food" | "stone" | "wood" | "population" | "hammers" | "science" | "scouting" | "mana";

type TBuildingId =
  | "farm"
  | "mine"
  | "sawmill"
  | "village"
  | "masons_guild"
  | "observatory"
  | "university"
  | "converter";

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
  /** A small picture of the biome for the cards and the matrix header. */
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
};

/**
 * The editable part of a building: the links to resources and to biomes.
 * The shape matches `buildings[]` of the game config in `javascript/ostrov-config`.
 */
type TBuildingLinks = {
  readonly id: TBuildingId;
  /** The main resource of the building. A new face starts with it. */
  readonly yields: TYieldResourceId;
  /** Faces every copy of the building has, on any biome. */
  readonly baseFaces: readonly TFace[];
  /** The extra face a biome adds. A biome missing here cannot host the building. */
  readonly biomeFaces: Readonly<Partial<Record<TBiomeId, TFace>>>;
};

type TLinks = Readonly<Record<TBuildingId, TBuildingLinks>>;

export type {
  TBiome,
  TBiomeId,
  TBuildingId,
  TBuildingInfo,
  TBuildingLinks,
  TFace,
  TLinks,
  TResource,
  TYieldResourceId,
};
