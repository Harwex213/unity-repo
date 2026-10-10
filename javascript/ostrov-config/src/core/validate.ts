import Ajv2020 from "ajv/dist/2020";
import addFormats from "ajv-formats";
import type { ErrorObject, ValidateFunction } from "ajv";
import type { TJson, TValidation, TValidationError } from "./types";

// Each schema gets its own Ajv instance.
// Two schema versions can share an $id, and one instance rejects a duplicate $id.
const createValidator = (schema: TJson): ValidateFunction => {
  const ajv = new Ajv2020({ allErrors: true });
  addFormats(ajv);
  return ajv.compile(schema as object);
};

const toValidationError = (error: ErrorObject): TValidationError => {
  const params = error.keyword === "additionalProperties"
    ? ` (${String(error.params["additionalProperty"])})`
    : "";

  return {
    path: error.instancePath || "/",
    message: `${error.message ?? error.keyword}${params}`,
  };
};

// A schema that does not compile makes the config invalid.
// The compile error message becomes the single validation error.
const validateConfig = (config: TJson, schema: TJson | undefined, schemaVersion: string | null): TValidation => {
  if (schema === undefined) {
    const message = schemaVersion === null
      ? "The config has no schemaVersion field"
      : `Schema version ${schemaVersion} is missing`;
    return { status: "missing-schema", errors: [{ path: "/schemaVersion", message }] };
  }

  let validate: ValidateFunction;
  try {
    validate = createValidator(schema);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    return { status: "invalid", errors: [{ path: "(schema)", message: `The schema does not compile: ${message}` }] };
  }

  if (validate(config)) {
    return { status: "valid" };
  }

  return { status: "invalid", errors: (validate.errors ?? []).map(toValidationError) };
};

export { validateConfig };
