import { createRng } from "./rng";
import type { TSeededRng } from "./rng";

/**
 * Turns the state of the store into plain JSON and back. Most of the state is
 * JSON already. The battle sim is not, so a value of these kinds is written as
 * an object with a `$type` key:
 * - `Map`, `Set` and the typed arrays;
 * - the numbers JSON has no form for: `Infinity`, `-Infinity` and `NaN`;
 * - a seeded generator, as the position it has reached;
 * - an object held in two places. The sim mutates its objects in place, so
 *   `island.body` and the entry of `bodies` have to stay one object. The first
 *   place holds the value, and each later place holds a `ref` to it.
 *
 * A value of any other kind throws: a save must not lose state silently.
 */

const TYPE_KEY = "$type";

const TYPED_ARRAYS = { Float64Array, Int32Array, Uint8Array };

type TTypedArrayName = keyof typeof TYPED_ARRAYS;

type TEncoded = Readonly<Record<string, unknown>>;

const isTypedArray = (value: object): value is Float64Array | Int32Array | Uint8Array => {
  return Object.values(TYPED_ARRAYS).some((constructor) => value instanceof constructor);
};

const isSeededRng = (value: Function): value is TSeededRng => {
  return typeof (value as Partial<TSeededRng>).state === "function";
};

const isPlainObject = (value: object) => {
  const prototype = Object.getPrototypeOf(value);

  return prototype === Object.prototype || prototype === null;
};

/** The values inside an object, for the walk that counts the shared objects. */
const childrenOf = (value: object): readonly unknown[] => {
  if (value instanceof Map) {
    return [...value.entries()].flat();
  }

  if (value instanceof Set) {
    return [...value.values()];
  }

  return isTypedArray(value) ? [] : Object.values(value);
};

const countObjectUses = (root: unknown) => {
  const uses = new Map<object, number>();

  const walk = (value: unknown) => {
    if (typeof value !== "object" || value === null) {
      return;
    }

    const count = uses.get(value) ?? 0;
    uses.set(value, count + 1);

    if (count === 0) {
      childrenOf(value).forEach(walk);
    }
  };

  walk(root);

  return uses;
};

const encodeSnapshot = (root: unknown): unknown => {
  const uses = countObjectUses(root);
  const sharedIds = new Map<object, number>();

  const encodeObject = (value: object): unknown => {
    if (Array.isArray(value)) {
      return value.map(encode);
    }

    if (value instanceof Map) {
      return { [TYPE_KEY]: "map", entries: [...value.entries()].map(([key, item]) => [encode(key), encode(item)]) };
    }

    if (value instanceof Set) {
      return { [TYPE_KEY]: "set", values: [...value.values()].map(encode) };
    }

    if (isTypedArray(value)) {
      return { [TYPE_KEY]: "typed", name: value.constructor.name, values: Array.from(value, encode) };
    }

    if (!isPlainObject(value)) {
      throw new Error(`A store snapshot cannot hold a ${value.constructor.name}`);
    }

    return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, encode(item)]));
  };

  const encode = (value: unknown): unknown => {
    if (value === undefined) {
      return { [TYPE_KEY]: "undefined" };
    }

    if (typeof value === "number" && !Number.isFinite(value)) {
      return { [TYPE_KEY]: "number", value: String(value) };
    }

    if (typeof value === "function") {
      if (!isSeededRng(value)) {
        throw new Error(`A store snapshot cannot hold the function ${value.name}`);
      }

      return { [TYPE_KEY]: "rng", state: value.state() };
    }

    if (typeof value !== "object" || value === null) {
      return value;
    }

    const knownId = sharedIds.get(value);
    if (knownId !== undefined) {
      return { [TYPE_KEY]: "ref", id: knownId };
    }

    if ((uses.get(value) ?? 0) < 2) {
      return encodeObject(value);
    }

    const id = sharedIds.size;
    sharedIds.set(value, id);

    return { [TYPE_KEY]: "shared", id, value: encodeObject(value) };
  };

  return encode(root);
};

const decodeSnapshot = (root: unknown): unknown => {
  const shared = new Map<number, unknown>();

  const decodeTagged = (value: TEncoded): unknown => {
    switch (value[TYPE_KEY]) {
      case "undefined":
        return undefined;
      case "number":
        return Number(value.value);
      case "rng":
        return createRng(value.state as number);
      case "map":
        return new Map((value.entries as [unknown, unknown][]).map(([key, item]) => [decode(key), decode(item)]));
      case "set":
        return new Set((value.values as unknown[]).map(decode));
      case "typed":
        return TYPED_ARRAYS[value.name as TTypedArrayName].from((value.values as unknown[]).map(decode) as number[]);
      case "shared": {
        const decoded = decode(value.value);
        shared.set(value.id as number, decoded);

        return decoded;
      }
      case "ref":
        if (!shared.has(value.id as number)) {
          throw new Error(`The store snapshot has no shared object ${String(value.id)}`);
        }

        return shared.get(value.id as number);
      default:
        throw new Error(`The store snapshot has an unknown type ${String(value[TYPE_KEY])}`);
    }
  };

  const decode = (value: unknown): unknown => {
    if (typeof value !== "object" || value === null) {
      return value;
    }

    if (Array.isArray(value)) {
      return value.map(decode);
    }

    if (TYPE_KEY in value) {
      return decodeTagged(value as TEncoded);
    }

    return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, decode(item)]));
  };

  return decode(root);
};

export { decodeSnapshot, encodeSnapshot };
