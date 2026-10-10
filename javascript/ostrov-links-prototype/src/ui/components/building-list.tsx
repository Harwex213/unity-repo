import { useSignals } from "@preact/signals-react/runtime";
import { BUILDINGS } from "../../core/buildings";
import { allowedBiomes, resourceUses } from "../../core/links";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import { ResourceChips } from "./resource-chips";
import type { FC } from "react";
import type { TSelectBuildingAction } from "../../domain/registry";

type TBuildingListProps = {
  registry: {
    selectBuildingAction: TSelectBuildingAction;
  };
};

const BuildingList: FC<TBuildingListProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const links = store.links.links.value;
  const view = store.ui.view.value;
  const selectedId = store.ui.selectedBuildingId.value;

  return (
    <nav className="building-list" aria-label="Здания">
      <div className="sidebar-title">Здания</div>
      {BUILDINGS.map((info) => {
        const building = links[info.id];
        const isActive = view === "building" && info.id === selectedId;

        return (
          <button
            key={info.id}
            type="button"
            className={isActive ? "building-item building-item--active" : "building-item"}
            aria-current={isActive ? "true" : undefined}
            onClick={() => registry.selectBuildingAction(info.id)}
          >
            <Icon src={info.art} size="l" />
            <span className="building-item-text">
              <span className="building-item-label">{info.label}</span>
              <span className="building-item-meta">
                <ResourceChips uses={resourceUses(building)} />
                <span className="muted">· биомов: {allowedBiomes(building).length}</span>
              </span>
            </span>
          </button>
        );
      })}
    </nav>
  );
};

export { BuildingList };
