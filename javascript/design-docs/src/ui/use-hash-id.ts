import { useCallback, useSyncExternalStore } from "react";

const subscribe = (onChange: () => void) => {
  window.addEventListener("hashchange", onChange);
  return () => window.removeEventListener("hashchange", onChange);
};

const getHash = () => window.location.hash;

// The URL hash stores the selected document id, so a reload keeps the selection.
const useHashId = (): [string | null, (id: string) => void] => {
  const hash = useSyncExternalStore(subscribe, getHash);
  const setId = useCallback((id: string) => {
    window.location.hash = encodeURIComponent(id);
  }, []);
  const id = hash.length > 1 ? decodeURIComponent(hash.slice(1)) : null;
  return [id, setId];
};

export { useHashId };
