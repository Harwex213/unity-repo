import { ICONS } from "../../core/icons";
import { Icon } from "./icon";
import type { FC } from "react";
import type { TOpenFactionsModalAction } from "../../domain/registry";

type TWorldToolsPanelRegistrySlice = {
  openFactionsModalAction: TOpenFactionsModalAction;
};

type TWorldToolsPanelProps = {
  registry: TWorldToolsPanelRegistrySlice;
};

/**
 * The world page's tool column, left of the cell panel. It mirrors the tools
 * panel next to the buildings on the island page. It holds one button: the
 * factions of the island world.
 */
const WorldToolsPanel: FC<TWorldToolsPanelProps> = ({ registry }) => {
  return (
    <div className="panel tools-panel">
      <button
        type="button"
        className="tool-icon has-hint"
        aria-label="Фракции"
        onClick={() => registry.openFactionsModalAction()}
      >
        <Icon src={ICONS.factions} size="l" />

        <span className="hint">
          <span className="hint__title">
            {"Фракции"}
          </span>

          <span className="hint__row">
            {"Кто удерживает острова мира: шесть фракций, их сильные стороны и слабости."}
          </span>
        </span>
      </button>
    </div>
  );
};

export { WorldToolsPanel };
