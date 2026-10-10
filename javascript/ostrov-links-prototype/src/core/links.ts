import { BIOMES, isBiomeId } from "./biomes";
import { BUILDINGS, face, getBuilding, isBuildingId } from "./buildings";
import { getResource, isResourceId, RESOURCES } from "./resources";
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
  return updateBuilding(links, buildingId, (building) => ({
    ...building,
    baseFaces: [...building.baseFaces, face(building.yields, 1, 0)],
  }));
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

/** A newly allowed biome gets a face of the main resource: 1 yield, 0 toxicity. */
const setBiomeAllowed = (links: TLinks, buildingId: TBuildingId, biomeId: TBiomeId, allowed: boolean) => {
  return updateBuilding(links, buildingId, (building) => {
    const biomeFaces = { ...building.biomeFaces };

    if (allowed) {
      biomeFaces[biomeId] = biomeFaces[biomeId] ?? face(building.yields, 1, 0);
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

const buildingsOnBiome = (links: TLinks, biomeId: TBiomeId) => {
  return BUILDINGS.filter((building) => canBuildOn(links[building.id], biomeId));
};

/** One resource a building brings: on how many base faces and on how many biomes. */
type TResourceUse = {
  readonly resource: TYieldResourceId;
  readonly baseFaces: number;
  readonly biomes: number;
};

const resourceUses = (building: TBuildingLinks): readonly TResourceUse[] => {
  const biomeFaces = Object.values(building.biomeFaces);

  return RESOURCES.map((resource) => ({
    resource: resource.id,
    baseFaces: building.baseFaces.filter((item) => item.resource === resource.id).length,
    biomes: biomeFaces.filter((item) => item.resource === resource.id).length,
  })).filter((use) => use.baseFaces > 0 || use.biomes > 0);
};

/** Problems the designer should see. None of them blocks the export. */
const findIssues = (links: TLinks): readonly string[] => {
  const issues: string[] = [];

  for (const info of BUILDINGS) {
    const building = links[info.id];

    if (allowedBiomes(building).length === 0) {
      issues.push(`«${info.label}» нельзя построить ни на одном биоме.`);
    }

    if (building.baseFaces.length === 0) {
      issues.push(`У «${info.label}» нет базовых граней.`);
    }

    if (!resourceUses(building).some((use) => use.resource === building.yields)) {
      issues.push(`«${info.label}» не приносит свой основной ресурс «${getResource(building.yields).label}».`);
    }
  }

  for (const biome of BIOMES) {
    if (buildingsOnBiome(links, biome.id).length === 0) {
      issues.push(`На биоме «${biome.label}» нельзя построить ни одного здания.`);
    }
  }

  return issues;
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

  if (!isResourceId(value.resource)) {
    throw new Error(`${where}: неизвестный ресурс ${JSON.stringify(value.resource)}.`);
  }

  if (!isFaceNumber(value.amount) || !isFaceNumber(value.toxicity)) {
    throw new Error(`${where}: amount и toxicity должны быть целыми числами от 0.`);
  }

  return face(value.resource, value.amount, value.toxicity);
};

const parseBuilding = (value: unknown, index: number): TBuildingLinks => {
  const where = `buildings[${index}]`;
  if (!isRecord(value) || !isBuildingId(value.id)) {
    throw new Error(`${where}: неизвестное здание.`);
  }

  const label = `${where} (${getBuilding(value.id).label})`;

  if (!isResourceId(value.yields)) {
    throw new Error(`${label}: неизвестный основной ресурс ${JSON.stringify(value.yields)}.`);
  }

  if (!Array.isArray(value.baseFaces)) {
    throw new Error(`${label}: baseFaces должен быть массивом.`);
  }

  if (!isRecord(value.biomeFaces)) {
    throw new Error(`${label}: biomeFaces должен быть объектом.`);
  }

  const biomeFaces: Partial<Record<TBiomeId, TFace>> = {};
  for (const [biomeId, biomeFace] of Object.entries(value.biomeFaces)) {
    if (!isBiomeId(biomeId)) {
      throw new Error(`${label}: неизвестный биом ${JSON.stringify(biomeId)}.`);
    }

    biomeFaces[biomeId] = parseFace(biomeFace, `${label}.biomeFaces.${biomeId}`);
  }

  return {
    id: value.id,
    yields: value.yields,
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
  buildingsOnBiome,
  canBuildOn,
  facesOn,
  findIssues,
  parseLinks,
  removeBaseFace,
  resourceUses,
  serializeLinks,
  setBiomeAllowed,
  setYields,
  updateBaseFace,
  updateBiomeFace,
};
