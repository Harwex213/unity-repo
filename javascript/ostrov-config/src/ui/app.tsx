import { useEffect } from "react";
import { entryId } from "../core/catalog";
import { formatHash } from "../core/hash";
import { JsonViewer } from "./components/json-viewer";
import { VersionList } from "./components/version-list";
import { useHashState } from "./use-hash-state";
import type { FC } from "react";
import type { TCatalog, TTab } from "../core/types";
import "./app.css";

type TAppProps = {
  catalog: TCatalog;
};

const TABS: TTab[] = ["schemas", "configs"];

const App: FC<TAppProps> = ({ catalog }) => {
  const [hashState, setHashState] = useHashState();
  const { tab } = hashState;
  const entries = tab === "schemas" ? catalog.schemas : catalog.configs;

  // An unknown or absent id falls back to the first version in the list.
  const selected = entries.find((entry) => entryId(entry) === hashState.id) ?? entries[0];
  const selectedId = selected ? entryId(selected) : null;

  // The hash gets the real selection, so a link always names the shown version.
  useEffect(() => {
    if (selectedId !== hashState.id) {
      window.history.replaceState(null, "", formatHash({ tab, id: selectedId }));
    }
  }, [tab, selectedId, hashState.id]);

  const selectTab = (next: TTab) => {
    const first = (next === "schemas" ? catalog.schemas : catalog.configs)[0];
    setHashState({ tab: next, id: first ? entryId(first) : null });
  };

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="tabs" role="tablist">
          {TABS.map((item) => (
            <button
              key={item}
              type="button"
              role="tab"
              aria-selected={item === tab}
              className={item === tab ? "tab tab-active" : "tab"}
              onClick={() => selectTab(item)}
            >
              {item}
            </button>
          ))}
        </div>

        <div className="sidebar-title">versions</div>

        <VersionList
          entries={entries}
          selectedId={selectedId}
          onSelect={(id) => setHashState({ tab, id })}
        />
      </aside>

      <main className="viewer">
        {selected ? <JsonViewer entry={selected} /> : <div className="empty">No {tab} found in data/{tab}/</div>}
      </main>
    </div>
  );
};

export { App };
