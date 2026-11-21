import { useSignals } from "@preact/signals-react/runtime";
import scienceIcon from "../../assets/icons/science.png";
import { neighbours } from "../../core/board";
import { getBuilding } from "../../core/buildings";
import { techIdOf, techNodeId } from "../../core/tech-tree";
import { getTechnology } from "../../core/technologies";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import type { FC } from "react";
import type { TFocusBoardNodeAction, TSelectBuildingAction, TSelectTechnologyAction } from "../../domain/registry";

type TTechnologyViewProps = {
  registry: {
    focusBoardNodeAction: TFocusBoardNodeAction;
    selectBuildingAction: TSelectBuildingAction;
    selectTechnologyAction: TSelectTechnologyAction;
  };
};

/** The panel of the selected technology, right of the tree. It is read-only, like the module card. */
const TechnologyView: FC<TTechnologyViewProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const technology = getTechnology(store.ui.selectedTechnologyId.value);
  const building = getBuilding(technology.unlocks);
  const edges = store.boards.technologies.layout.value?.edges ?? [];
  const linked = neighbours(edges, techNodeId(technology.id)).map((nodeId) => getTechnology(techIdOf(nodeId)));

  return (
    <div className="technology-panel">
      <img className="technology-art" src={technology.art} alt="" draggable={false} />

      <header className="technology-panel-header">
        <Icon src={technology.icon} size="l" />
        <div>
          <h1 className="editor-title">{technology.label}</h1>
          <div className="muted">id: {technology.id}</div>
        </div>
        <div className="technology-cost" title="Сколько науки нужно">
          <Icon src={scienceIcon} label="Наука" size="m" />
          <strong>{technology.science}</strong>
        </div>
      </header>

      <p className="technology-summary">{technology.summary}</p>
      <p className="technology-text">{technology.description}</p>

      <section className="technology-block">
        <h2>Открывает здание</h2>
        <button type="button" className="link-button" onClick={() => registry.selectBuildingAction(building.id)}>
          <Icon src={building.art} size="m" />
          <strong>{building.label}</strong>
        </button>
      </section>

      <section className="technology-block">
        <h2>Связи на дереве</h2>
        {linked.length === 0 ? (
          <span className="muted">Нет связей. Shift + клик по другой технологии добавит связь.</span>
        ) : (
          <ul className="technology-links">
            {linked.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  className="link-button"
                  onClick={() => {
                    registry.selectTechnologyAction(item.id);
                    registry.focusBoardNodeAction("technologies", techNodeId(item.id));
                  }}
                >
                  <Icon src={item.icon} size="m" />
                  <span>{item.label}</span>
                  <span className="muted">· {item.science} науки</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
};

export { TechnologyView };
