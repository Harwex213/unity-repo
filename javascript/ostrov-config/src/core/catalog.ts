import { compareSemver } from "./semver";
import { validateConfig } from "./validate";
import type { TCatalog, TConfigEntry, TJson, TSchemaEntry } from "./types";

// Keys look like "./game-config/1.0.0.json".
// The folder is the family. The file name without ".json" is the version.
type TDataFiles = Record<string, TJson>;

const FILE_KEY_PATTERN = /^\.\/([^/]+)\/([^/]+)\.json$/;

const parseFileKey = (key: string): { family: string; name: string } | null => {
  const match = FILE_KEY_PATTERN.exec(key);
  if (!match || match[1] === undefined || match[2] === undefined) {
    return null;
  }

  return { family: match[1], name: match[2] };
};

const readSchemaVersion = (json: TJson): string | null => {
  if (json === null || typeof json !== "object" || Array.isArray(json)) {
    return null;
  }

  const value = json["schemaVersion"];
  return typeof value === "string" ? value : null;
};

const entryId = (entry: TSchemaEntry | TConfigEntry): string => `${entry.family}@${entry.version}`;

const buildSchemas = (files: TDataFiles): TSchemaEntry[] => {
  const schemas: TSchemaEntry[] = [];
  for (const [key, json] of Object.entries(files)) {
    const parsed = parseFileKey(key);
    if (parsed) {
      schemas.push({ kind: "schema", family: parsed.family, version: parsed.name, json });
    }
  }

  // Newest version goes first.
  return schemas.sort((a, b) => compareSemver(b.version, a.version) || a.family.localeCompare(b.family));
};

const buildConfigs = (files: TDataFiles, schemas: TSchemaEntry[]): TConfigEntry[] => {
  const configs: TConfigEntry[] = [];
  for (const [key, json] of Object.entries(files)) {
    const parsed = parseFileKey(key);
    // A config file name must be an integer, because it equals configVersion.
    // A leading zero is rejected, because "01.json" and "1.json" would share one id.
    if (!parsed || !/^(0|[1-9]\d*)$/.test(parsed.name)) {
      continue;
    }

    const schemaVersion = readSchemaVersion(json);
    const schema = schemas.find((entry) => entry.family === parsed.family && entry.version === schemaVersion);
    configs.push({
      kind: "config",
      family: parsed.family,
      version: Number(parsed.name),
      schemaVersion,
      json,
      validation: validateConfig(json, schema?.json, schemaVersion),
    });
  }

  // Newest version goes first.
  return configs.sort((a, b) => b.version - a.version || a.family.localeCompare(b.family));
};

const buildCatalog = (schemaFiles: TDataFiles, configFiles: TDataFiles): TCatalog => {
  const schemas = buildSchemas(schemaFiles);
  return { schemas, configs: buildConfigs(configFiles, schemas) };
};

export { buildCatalog, entryId };
export type { TDataFiles };
