import { entryId } from "../../core/catalog";
import { ValidityBadge } from "./validity-badge";
import type { FC } from "react";
import type { TConfigEntry, TSchemaEntry } from "../../core/types";

type TVersionListProps = {
  entries: (TSchemaEntry | TConfigEntry)[];
  selectedId: string | null;
  onSelect: (id: string) => void;
};

const VersionList: FC<TVersionListProps> = ({ entries, selectedId, onSelect }) => {
  if (entries.length === 0) {
    return <div className="version-list-empty">No versions</div>;
  }

  return (
    <ul className="version-list">
      {entries.map((entry) => {
        const id = entryId(entry);
        return (
          <li key={id}>
            <button
              type="button"
              className={id === selectedId ? "version version-active" : "version"}
              onClick={() => onSelect(id)}
            >
              <span className="version-main">
                <span className="version-name">{entry.kind === "config" ? `v${entry.version}` : entry.version}</span>
                <span className="version-family">{entry.family}</span>
              </span>

              {entry.kind === "config" && (
                <span className="version-meta">
                  <span className="version-schema">schema {entry.schemaVersion ?? "?"}</span>
                  <ValidityBadge validation={entry.validation} />
                </span>
              )}
            </button>
          </li>
        );
      })}
    </ul>
  );
};

export { VersionList };
