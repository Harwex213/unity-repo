import { JsonNode } from "./json-node";
import { ValidityBadge } from "./validity-badge";
import type { FC } from "react";
import type { TConfigEntry, TSchemaEntry } from "../../core/types";

type TJsonViewerProps = {
  entry: TSchemaEntry | TConfigEntry;
};

const JsonViewer: FC<TJsonViewerProps> = ({ entry }) => {
  const title = entry.kind === "config"
    ? `configs/${entry.family}/${entry.version}.json`
    : `schemas/${entry.family}/${entry.version}.json`;
  const errors = entry.kind === "config" && entry.validation.status !== "valid" ? entry.validation.errors : [];

  return (
    <div className="json-viewer">
      <header className="viewer-header">
        <span className="viewer-title">{title}</span>
        {entry.kind === "config" && <ValidityBadge validation={entry.validation} />}
      </header>

      {errors.length > 0 && (
        <ul className="errors">
          {errors.map((error, index) => (
            <li key={index} className="error">
              <code className="error-path">{error.path}</code> {error.message}
            </li>
          ))}
        </ul>
      )}

      {/* The key resets the collapsed state when the selection changes. */}
      <div className="json-code" key={`${entry.kind}:${entry.family}@${entry.version}`}>
        <JsonNode value={entry.json} depth={0} isLast />
      </div>
    </div>
  );
};

export { JsonViewer };
