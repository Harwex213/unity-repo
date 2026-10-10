import { signal } from "@preact/signals-react";
import { DEFAULT_LINKS } from "../core/buildings";
import { parseLinks } from "../core/links";
import type { TLinks } from "../core/types";

/** The browser keeps the last edit, so a reload does not lose work. */
const LINKS_STORAGE_KEY = "ostrov-links/v1";

const readStoredLinks = (): TLinks => {
  try {
    const text = window.localStorage.getItem(LINKS_STORAGE_KEY);
    if (!text) {
      return DEFAULT_LINKS;
    }

    const result = parseLinks(text, DEFAULT_LINKS);

    return result.ok ? result.links : DEFAULT_LINKS;
  } catch {
    return DEFAULT_LINKS;
  }
};

const createLinksState = () => {
  return {
    links: signal<TLinks>(readStoredLinks()),
  };
};

type TLinksState = ReturnType<typeof createLinksState>;

export type { TLinksState };
export { createLinksState, LINKS_STORAGE_KEY };
