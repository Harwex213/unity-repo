import { useCallback, useSyncExternalStore } from "react";
import type { TRect } from "../core/types";

// The URL hash stores the selected document and, optionally, a view of it:
// `#<id>` or `#<id>@<x>,<y>,<width>,<height>` in world units.
// A reload keeps the selection. A link with a view opens the document fitted to that rect.

type THashLocation = {
  id: string | null;
  view: TRect | null;
};

const subscribe = (onChange: () => void) => {
  window.addEventListener("hashchange", onChange);
  return () => window.removeEventListener("hashchange", onChange);
};

const getHash = () => window.location.hash;

const parseView = (text: string | undefined): TRect | null => {
  const numbers = text?.split(",").map(Number) ?? [];
  const [x, y, width, height] = numbers;
  if (numbers.length !== 4 || numbers.some((value) => !Number.isFinite(value))) {
    return null;
  }
  if (x === undefined || y === undefined || width === undefined || height === undefined || width <= 0 || height <= 0) {
    return null;
  }
  return { x, y, width, height };
};

const parseHash = (hash: string): THashLocation => {
  if (hash.length <= 1) {
    return { id: null, view: null };
  }
  const [id, view] = decodeURIComponent(hash.slice(1)).split("@");
  return { id: id || null, view: parseView(view) };
};

const useHashLocation = (): [THashLocation, (id: string) => void] => {
  const hash = useSyncExternalStore(subscribe, getHash);
  const selectDocument = useCallback((id: string) => {
    window.location.hash = encodeURIComponent(id);
  }, []);
  return [parseHash(hash), selectDocument];
};

export { useHashLocation };
