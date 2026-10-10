import { DEFAULT_LINKS } from "../core/buildings";
import { parseLinks, serializeLinks } from "../core/links";
import { commitLinks } from "./link-actions";
import { showNotice } from "./ui-actions";
import type { TStore } from "../store/store";

const EXPORT_FILE_NAME = "ostrov-links.json";

const exportLinksAction = (store: TStore) => {
  const blob = new Blob([serializeLinks(store.links.links.value)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = EXPORT_FILE_NAME;
  link.click();
  URL.revokeObjectURL(url);
};

/** A broken file changes nothing: the notice names the first problem. */
const importLinksAction = async (store: TStore, file: File) => {
  const result = parseLinks(await file.text(), DEFAULT_LINKS);
  if (!result.ok) {
    showNotice(store, { kind: "error", text: `Импорт не удался. ${result.error}` });
    return;
  }

  commitLinks(store, result.links);
  showNotice(store, { kind: "ok", text: `Связи загружены из ${file.name}.` });
};

const resetLinksAction = (store: TStore) => {
  commitLinks(store, DEFAULT_LINKS);
  showNotice(store, { kind: "ok", text: "Связи сброшены к значениям прототипа." });
};

export { exportLinksAction, importLinksAction, resetLinksAction };
