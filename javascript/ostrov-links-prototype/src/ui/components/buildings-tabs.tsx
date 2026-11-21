import { useSignals } from "@preact/signals-react/runtime";
import { useStore } from "../../store/store";
import type { FC } from "react";
import type { TSetBuildingsViewAction } from "../../domain/registry";
import type { TBuildingsView } from "../../store/ui-state";

type TBuildingsTabsProps = {
  registry: {
    setBuildingsViewAction: TSetBuildingsViewAction;
  };
};

const VIEWS: readonly { id: TBuildingsView; label: string }[] = [
  { id: "graph", label: "Граф" },
  { id: "buildings", label: "Список зданий" },
  { id: "modules", label: "Список модулей" },
];

/** The sub-tabs of "Здания". */
const BuildingsTabs: FC<TBuildingsTabsProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const view = store.ui.buildingsView.value;

  return (
    <div className="tabs" role="tablist" aria-label="Здания">
      {VIEWS.map((item) => (
        <button
          key={item.id}
          type="button"
          role="tab"
          aria-selected={item.id === view}
          className={item.id === view ? "tab tab--active" : "tab"}
          onClick={() => registry.setBuildingsViewAction(item.id)}
        >
          <span className="tab-label" data-label={item.label}>
            {item.label}
          </span>
        </button>
      ))}
    </div>
  );
};

export { BuildingsTabs };
