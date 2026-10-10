import { useCallback, useSyncExternalStore } from "react";
import { formatHash, parseHash } from "../core/hash";
import type { THashState } from "../core/hash";

const subscribe = (onChange: () => void) => {
  window.addEventListener("hashchange", onChange);
  return () => window.removeEventListener("hashchange", onChange);
};

const getHash = () => window.location.hash;

// The URL hash is the only store of tab and selection, so a reload keeps both.
const useHashState = (): [THashState, (state: THashState) => void] => {
  const hash = useSyncExternalStore(subscribe, getHash);
  const setState = useCallback((state: THashState) => {
    window.location.hash = formatHash(state);
  }, []);
  return [parseHash(hash), setState];
};

export { useHashState };
