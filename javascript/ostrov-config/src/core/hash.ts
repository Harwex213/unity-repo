import type { TTab } from "./types";

type THashState = {
  tab: TTab;
  // Id of the selected entry, for example "game-config@1". It is null when the hash has no id.
  id: string | null;
};

// A malformed escape such as "%E0%A4%A" makes decodeURIComponent throw.
// Such an id counts as absent, so the app falls back to the first version.
const decodeId = (value: string): string | null => {
  try {
    return decodeURIComponent(value) || null;
  } catch {
    return null;
  }
};

// The hash looks like "#configs/game-config@1".
const parseHash = (hash: string): THashState => {
  const [tabPart, ...rest] = hash.replace(/^#/, "").split("/");
  const tab: TTab = tabPart === "configs" ? "configs" : "schemas";
  const id = rest.length > 0 ? decodeId(rest.join("/")) : null;
  return { tab, id };
};

const formatHash = (state: THashState): string => {
  return state.id === null ? `#${state.tab}` : `#${state.tab}/${encodeURIComponent(state.id)}`;
};

export { formatHash, parseHash };
export type { THashState };
