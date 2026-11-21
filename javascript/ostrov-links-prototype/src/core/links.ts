import { BIOMES, isBiomeId } from "./biomes";
import { BUILDINGS, face, getBuilding, hasDie, isBuildingId } from "./buildings";
import { readResourceId, RESOURCES } from "./resources";
import type { TBiomeId, TBuildingId, TBuildingLinks, TFace, TLinks, TYieldResourceId } from "./types";

/**
 * Pure operations on the links. Each one returns a new `TLinks` and leaves the
 * old one untouched, so the store can keep a plain undo-free signal.
 */

type TFacePatch = Partial<TFace>;

/** Face numbers are whole and never negative. */
const normalizeNumber = (value: number) => {
  return Number.isFinite(value) ? Math.max(0, Math.round(value)) : 0;
};

const patchFace = (target: TFace, patch: TFacePatch): TFace => ({
  resource: patch.resource ?? target.resource,
  amount: patch.amount === undefined ? target.amount : normalizeNumber(patch.amount),
  toxicity: patch.toxicity === undefined ? target.toxicity : normalizeNumber(patch.toxicity),
});

const updateBuilding = (
  links: TLinks,
  buildingId: TBuildingId,
  change: (building: TBuildingLinks) => TBuildingLinks,
): TLinks => {
  return { ...links, [buildingId]: change(links[buildingId]) };
};

const setYields = (links: TLinks, buildingId: TBuildingId, resource: TYieldResourceId) => {
  return updateBuilding(links, buildingId, (building) => ({ ...building, yields: resource }));
};

const addBaseFace = (links: TLinks, buildingId: TBuildingId) => {
  return updateBuilding(links, buildingId, (building) =>
    building.yields === null
      ? building
      : { ...building, baseFaces: [...building.baseFaces, face(building.yields, 1, 0)] },
  );
};

const updateBaseFace = (links: TLinks, buildingId: TBuildingId, index: number, patch: TFacePatch) => {
  return updateBuilding(links, buildingId, (building) => ({
    ...building,
    baseFaces: building.baseFaces.map((item, itemIndex) => (itemIndex === index ? patchFace(item, patch) : item)),
  }));
};

const removeBaseFace = (links: TLinks, buildingId: TBuildingId, index: number) => {
  return updateBuilding(links, buildingId, (building) => ({
    ...building,
    baseFaces: building.baseFaces.filter((_, itemIndex) => itemIndex !== index),
  }));
};

/**
 * A newly allowed biome gets a face of the main resource: 1 yield, 0 toxicity.
 * A building without a die gets `null`: the biome allows it and adds no face.
 */
const setBiomeAllowed = (links: TLinks, buildingId: TBuildingId, biomeId: TBiomeId, allowed: boolean) => {
  return updateBuilding(links, buildingId, (building) => {
    const biomeFaces = { ...building.biomeFaces };

    if (allowed) {
      if (!(biomeId in biomeFaces)) {
        biomeFaces[biomeId] = building.yields === null ? null : face(building.yields, 1, 0);
      }
    } else {
      delete biomeFaces[biomeId];
    }

    return { ...building, biomeFaces };
  });
};

const updateBiomeFace = (links: TLinks, buildingId: TBuildingId, biomeId: TBiomeId, patch: TFacePatch) => {
  return updateBuilding(links, buildingId, (building) => {
    const current = building.biomeFaces[biomeId];
    if (!current) {
      return building;
    }

    return { ...building, biomeFaces: { ...building.biomeFaces, [biomeId]: patchFace(current, patch) } };
  });
};

/* Derived values */

const canBuildOn = (building: TBuildingLinks, biomeId: TBiomeId) => {
  return building.biomeFaces[biomeId] !== undefined;
};

/** The die of a building on a biome: the base faces plus the biome face. */
const facesOn = (building: TBuildingLinks, biomeId: TBiomeId): readonly TFace[] => {
  const biomeFace = building.biomeFaces[biomeId];

  return biomeFace ? [...building.baseFaces, biomeFace] : building.baseFaces;
};

const averageOf = (faces: readonly TFace[], pick: (item: TFace) => number) => {
  if (faces.length === 0) {
    return 0;
  }

  return faces.reduce((sum, item) => sum + pick(item), 0) / faces.length;
};

const averageAmount = (faces: readonly TFace[]) => averageOf(faces, (item) => item.amount);

const averageToxicity = (faces: readonly TFace[]) => averageOf(faces, (item) => item.toxicity);

const allowedBiomes = (building: TBuildingLinks) => {
  return BIOMES.filter((biome) => canBuildOn(building, biome.id));
};

/** One resource a building brings: on how many base faces and on how many biomes. */
type TResourceUse = {
  readonly resource: TYieldResourceId;
  readonly baseFaces: number;
  readonly biomes: number;
};

const resourceUses = (building: TBuildingLinks): readonly TResourceUse[] => {
  const biomeFaces = Object.values(building.biomeFaces).filter((item): item is TFace => item !== null);

  return RESOURCES.map((resource) => ({
    resource: resource.id,
    baseFaces: building.baseFaces.filter((item) => item.resource === resource.id).length,
    biomes: biomeFaces.filter((item) => item.resource === resource.id).length,
  })).filter((use) => use.baseFaces > 0 || use.biomes > 0);
};

/* Export and import */

const LINKS_FORMAT = "ostrov-links";
const LINKS_FORMAT_VERSION = 1;

/** The file the editor writes. `buildings[]` has the shape of the game config. */
const serializeLinks = (links: TLinks) => {
  const file = {
    format: LINKS_FORMAT,
    version: LINKS_FORMAT_VERSION,
    buildings: BUILDINGS.map((info) => links[info.id]),
  };

  return `${JSON.stringify(file, null, 2)}\n`;
};

type TParseResult = { readonly ok: true; readonly links: TLinks } | { readonly ok: false; readonly error: string };

const isRecord = (value: unknown): value is Record<string, unknown> => {
  return typeof value === "object" && value !== null && !Array.isArray(value);
};

const isFaceNumber = (value: unknown): value is number => {
  return typeof value === "number" && Number.isInteger(value) && value >= 0;
};

const parseFace = (value: unknown, where: string): TFace => {
  if (!isRecord(value)) {
    throw new Error(`${where}: грань должна быть объектом.`);
  }

  const resource = readResourceId(value.resource);
  if (resource === null) {
    throw new Error(`${where}: неизвестный ресурс ${JSON.stringify(value.resource)}.`);
  }

  if (!isFaceNumber(value.amount) || !isFaceNumber(value.toxicity)) {
    throw new Error(`${where}: amount и toxicity должны быть целыми числами от 0.`);
  }

  return face(resource, value.amount, value.toxicity);
};

const parseBuilding = (value: unknown, index: number): TBuildingLinks => {
  const where = `buildings[${index}]`;
  if (!isRecord(value) || !isBuildingId(value.id)) {
    throw new Error(`${where}: неизвестное здание.`);
  }

  const id = value.id;
  const label = `${where} (${getBuilding(id).label})`;

  if (!isRecord(value.biomeFaces)) {
    throw new Error(`${label}: biomeFaces должен быть объектом.`);
  }

  for (const biomeId of Object.keys(value.biomeFaces)) {
    if (!isBiomeId(biomeId)) {
      throw new Error(`${label}: неизвестный биом ${JSON.stringify(biomeId)}.`);
    }
  }

  const biomeIds = Object.keys(value.biomeFaces) as TBiomeId[];

  // A building without a die keeps only the allowed biomes, even if an old file gives it faces.
  if (!hasDie(id)) {
    return {
      id,
      yields: null,
      baseFaces: [],
      biomeFaces: Object.fromEntries(biomeIds.map((biomeId) => [biomeId, null])),
    };
  }

  const yields = readResourceId(value.yields);
  if (yields === null) {
    throw new Error(`${label}: неизвестный основной ресурс ${JSON.stringify(value.yields)}.`);
  }

  if (!Array.isArray(value.baseFaces)) {
    throw new Error(`${label}: baseFaces должен быть массивом.`);
  }

  const biomeFaces: Partial<Record<TBiomeId, TFace>> = {};
  for (const biomeId of biomeIds) {
    biomeFaces[biomeId] = parseFace(value.biomeFaces[biomeId], `${label}.biomeFaces.${biomeId}`);
  }

  return {
    id,
    yields,
    baseFaces: value.baseFaces.map((item, faceIndex) => parseFace(item, `${label}.baseFaces[${faceIndex}]`)),
    biomeFaces,
  };
};

/**
 * Reads a file written by `serializeLinks`. A building missing from the file
 * keeps its links from `fallback`.
 */
const parseLinks = (text: string, fallback: TLinks): TParseResult => {
  try {
    const value: unknown = JSON.parse(text);
    if (!isRecord(value) || value.format !== LINKS_FORMAT) {
      return { ok: false, error: `Это не файл связей: нет поля "format": "${LINKS_FORMAT}".` };
    }

    if (value.version !== LINKS_FORMAT_VERSION) {
      return { ok: false, error: `Неизвестная версия файла: ${JSON.stringify(value.version)}.` };
    }

    if (!Array.isArray(value.buildings)) {
      return { ok: false, error: "Поле buildings должно быть массивом." };
    }

    const links: Record<TBuildingId, TBuildingLinks> = { ...fallback };
    value.buildings.forEach((item, index) => {
      const building = parseBuilding(item, index);
      links[building.id] = building;
    });

    return { ok: true, links };
  } catch (error) {
    return { ok: false, error: error instanceof Error ? error.message : String(error) };
  }
};

export type { TFacePatch, TResourceUse };
export {
  addBaseFace,
  allowedBiomes,
  averageAmount,
  averageToxicity,
  canBuildOn,
  facesOn,
  parseLinks,
  removeBaseFace,
  resourceUses,
  serializeLinks,
  setBiomeAllowed,
  setYields,
  updateBaseFace,
  updateBiomeFace,
};
