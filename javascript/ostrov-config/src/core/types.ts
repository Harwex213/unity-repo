type TJson = null | boolean | number | string | TJson[] | { [key: string]: TJson };

type TValidationError = {
  path: string;
  message: string;
};

type TValidation =
  | { status: "valid" }
  | { status: "invalid"; errors: TValidationError[] }
  | { status: "missing-schema"; errors: TValidationError[] };

type TSchemaEntry = {
  kind: "schema";
  // Name of the schema family, for example "game-config".
  family: string;
  version: string;
  json: TJson;
};

type TConfigEntry = {
  kind: "config";
  family: string;
  version: number;
  // The schemaVersion field of the config. It is null when the field is absent.
  schemaVersion: string | null;
  json: TJson;
  validation: TValidation;
};

type TCatalog = {
  schemas: TSchemaEntry[];
  configs: TConfigEntry[];
};

type TTab = "schemas" | "configs";

export type { TCatalog, TConfigEntry, TJson, TSchemaEntry, TTab, TValidation, TValidationError };
