import { useState } from "react";
import type { CSSProperties, FC } from "react";
import type { TJson } from "../../core/types";

type TJsonNodeProps = {
  // The key of this value in its parent object. Array items have no key.
  name?: string;
  value: TJson;
  depth: number;
  isLast: boolean;
};

const indent = (depth: number): CSSProperties => ({ paddingLeft: `${depth * 2}ch` });

const Primitive: FC<{ value: Exclude<TJson, object> | null }> = ({ value }) => {
  if (value === null) {
    return <span className="tok-null">null</span>;
  }
  if (typeof value === "string") {
    return <span className="tok-string">{JSON.stringify(value)}</span>;
  }
  if (typeof value === "number") {
    return <span className="tok-number">{value}</span>;
  }
  return <span className="tok-boolean">{String(value)}</span>;
};

const KeyLabel: FC<{ name: string | undefined }> = ({ name }) => {
  if (name === undefined) {
    return null;
  }
  return (
    <>
      <span className="tok-key">{JSON.stringify(name)}</span>
      <span className="tok-punct">: </span>
    </>
  );
};

const JsonNode: FC<TJsonNodeProps> = ({ name, value, depth, isLast }) => {
  const [open, setOpen] = useState(true);
  const comma = isLast ? null : <span className="tok-punct">,</span>;

  if (value === null || typeof value !== "object") {
    return (
      <div className="json-line" style={indent(depth)}>
        <span className="toggle-space" />
        <KeyLabel name={name} />
        <Primitive value={value} />
        {comma}
      </div>
    );
  }

  const isArray = Array.isArray(value);
  const children: [string | undefined, TJson][] = isArray
    ? value.map((item) => [undefined, item])
    : Object.entries(value);
  const [openBrace, closeBrace] = isArray ? ["[", "]"] : ["{", "}"];

  if (children.length === 0) {
    return (
      <div className="json-line" style={indent(depth)}>
        <span className="toggle-space" />
        <KeyLabel name={name} />
        <span className="tok-punct">{openBrace}{closeBrace}</span>
        {comma}
      </div>
    );
  }

  return (
    <>
      <div className="json-line" style={indent(depth)}>
        <button
          type="button"
          className="toggle"
          aria-label={open ? "Collapse" : "Expand"}
          aria-expanded={open}
          onClick={() => setOpen(!open)}
        >
          {open ? "▾" : "▸"}
        </button>
        <KeyLabel name={name} />
        <span className="tok-punct">{openBrace}</span>
        {!open && (
          <>
            <button type="button" className="collapsed" onClick={() => setOpen(true)}>
              {children.length} {isArray ? "items" : "keys"}
            </button>
            <span className="tok-punct">{closeBrace}</span>
            {comma}
          </>
        )}
      </div>

      {open && children.map(([childName, childValue], index) => (
        <JsonNode
          key={childName ?? index}
          name={childName}
          value={childValue}
          depth={depth + 1}
          isLast={index === children.length - 1}
        />
      ))}

      {open && (
        <div className="json-line" style={indent(depth)}>
          <span className="toggle-space" />
          <span className="tok-punct">{closeBrace}</span>
          {comma}
        </div>
      )}
    </>
  );
};

export { JsonNode };
