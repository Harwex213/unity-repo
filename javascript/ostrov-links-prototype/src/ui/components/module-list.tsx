import { useSignals } from "@preact/signals-react/runtime";
import { MODULES } from "../../core/modules";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import type { FC } from "react";
import type { TSelectModuleAction } from "../../domain/registry";

type TModuleListProps = {
  registry: {
    selectModuleAction: TSelectModuleAction;
  };
};

const ModuleList: FC<TModuleListProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const selectedId = store.ui.selectedModuleId.value;

  return (
    <nav className="building-list" aria-label="Модули">
      <div className="sidebar-title">Модули</div>
      {MODULES.map((module) => {
        const isActive = module.id === selectedId;

        return (
          <button
            key={module.id}
            type="button"
            className={isActive ? "building-item building-item--active" : "building-item"}
            aria-current={isActive ? "true" : undefined}
            onClick={() => registry.selectModuleAction(module.id)}
          >
            <Icon src={module.art} size="l" />
            <span className="building-item-text">
              <span className="building-item-label">{module.label}</span>
              <span className="building-item-meta muted">{module.summary}</span>
            </span>
          </button>
        );
      })}
    </nav>
  );
};

export { ModuleList };
