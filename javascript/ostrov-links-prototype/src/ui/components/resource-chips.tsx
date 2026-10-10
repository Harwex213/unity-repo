import { getResource } from "../../core/resources";
import { Icon } from "./icon";
import type { FC } from "react";
import type { TResourceUse } from "../../core/links";

type TResourceChipsProps = {
  uses: readonly TResourceUse[];
};

/** The resources a building brings, as a row of icons. */
const ResourceChips: FC<TResourceChipsProps> = ({ uses }) => {
  if (uses.length === 0) {
    return <span className="muted">ничего</span>;
  }

  return (
    <span className="resource-chips">
      {uses.map((use) => {
        const resource = getResource(use.resource);

        return <Icon key={use.resource} src={resource.icon} label={resource.label} />;
      })}
    </span>
  );
};

export { ResourceChips };
