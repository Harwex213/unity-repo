import { useSignals } from "@preact/signals-react/runtime";
import scienceIcon from "../../assets/icons/science.png";
import { techNodeId } from "../../core/tech-tree";
import { TECHNOLOGIES } from "../../core/technologies";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import type { FC } from "react";
import type { TFocusBoardNodeAction, TSelectTechnologyAction } from "../../domain/registry";

type TTechnologyListProps = {
  registry: {
    focusBoardNodeAction: TFocusBoardNodeAction;
    selectTechnologyAction: TSelectTechnologyAction;
  };
};

const TechnologyList: FC<TTechnologyListProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const selectedId = store.ui.selectedTechnologyId.value;

  return (
    <nav className="building-list" aria-label="Технологии">
      <div className="sidebar-title">Технологии</div>
      {TECHNOLOGIES.map((technology) => {
        const isActive = technology.id === selectedId;

        return (
          <button
            key={technology.id}
            type="button"
            className={isActive ? "building-item building-item--active" : "building-item"}
            aria-current={isActive ? "true" : undefined}
            onClick={() => {
              registry.selectTechnologyAction(technology.id);
              registry.focusBoardNodeAction("technologies", techNodeId(technology.id));
            }}
          >
            <Icon src={technology.icon} size="l" />
            <span className="building-item-text">
              <span className="building-item-label">{technology.label}</span>
              <span className="building-item-meta">
                <Icon src={scienceIcon} label="Наука" />
                <span>{technology.science}</span>
                <span className="muted">· {technology.summary}</span>
              </span>
            </span>
          </button>
        );
      })}
    </nav>
  );
};

export { TechnologyList };
