import { buildCatalog } from "../core/catalog";
import type { TCatalog, TJson } from "../core/types";
import type { TDataFiles } from "../core/catalog";

// Rspack finds every JSON file in the folder at build time.
// A new file in the folder becomes a new version without code changes.
const readFolder = (context: TWebpackContext): TDataFiles => {
  const files: TDataFiles = {};
  for (const key of context.keys()) {
    files[key] = context(key) as TJson;
  }
  return files;
};

const loadCatalog = (): TCatalog => {
  const schemaFiles = readFolder(import.meta.webpackContext("../../data/schemas", { recursive: true, regExp: /\.json$/ }));
  const configFiles = readFolder(import.meta.webpackContext("../../data/configs", { recursive: true, regExp: /\.json$/ }));
  return buildCatalog(schemaFiles, configFiles);
};

export { loadCatalog };
