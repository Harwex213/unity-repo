import { useSignals } from "@preact/signals-react/runtime";
import { useStore } from "../store/store";
import { BoardHelp } from "./components/board-help";
import { BuildingEditor } from "./components/building-editor";
import { BuildingList } from "./components/building-list";
import { BuildingsGraph } from "./components/buildings-graph";
import { BuildingsTabs } from "./components/buildings-tabs";
import { ModuleList } from "./components/module-list";
import { ModuleView } from "./components/module-view";
import { SectionPlaceholder } from "./components/section-placeholder";
import { TechTree } from "./components/tech-tree";
import { TechnologyList } from "./components/technology-list";
import { TechnologyView } from "./components/technology-view";
import { Toolbar } from "./components/toolbar";
import type { FC, ReactNode } from "react";
import type { TAppRegistry } from "../domain/registry";
import type { TSection } from "../store/ui-state";
import "./app.css";

type TAppProps = {
  registry: TAppRegistry;
};

/** The sections that have no editor yet. */
const PLACEHOLDERS: Record<Exclude<TSection, "buildings" | "technologies">, { title: string; description?: string }> = {
  "negative-events": { title: "Негативные ивенты" },
  units: { title: "Юниты" },
  enemies: { title: "Враги" },
  artifacts: { title: "Артефакты" },
};

type TScreen = {
  readonly sidebar: ReactNode;
  readonly content: ReactNode;
  /** A board fills the content area edge to edge. */
  readonly isBoard?: boolean;
  /** A panel right of the content: the selected technology. */
  readonly panel?: ReactNode;
};

/**
 * Every section has the same frame: a row of sub-tabs, a sidebar and the content.
 * A section without sub-tabs or a list keeps the empty row and the empty sidebar,
 * so the content does not move when the user switches tabs.
 */
const App: FC<TAppProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const section = store.ui.section.value;
  const buildingsView = store.ui.buildingsView.value;

  const screen = ((): TScreen => {
    if (section === "buildings" && buildingsView === "graph") {
      return {
        sidebar: (
          <BoardHelp title="Граф" extraControls={["Двойной клик по карточке — открыть её в списке."]}>
            <div className="graph-legend">
              <span className="graph-legend-item graph-legend-item--building">Здание</span>
              <span className="graph-legend-item graph-legend-item--module">Модуль</span>
            </div>
          </BoardHelp>
        ),
        content: <BuildingsGraph registry={registry} />,
        isBoard: true,
      };
    }

    if (section === "buildings" && buildingsView === "modules") {
      return { sidebar: <ModuleList registry={registry} />, content: <ModuleView /> };
    }

    if (section === "buildings") {
      return { sidebar: <BuildingList registry={registry} />, content: <BuildingEditor registry={registry} /> };
    }

    if (section === "technologies") {
      return {
        sidebar: <TechnologyList registry={registry} />,
        content: <TechTree registry={registry} />,
        isBoard: true,
        panel: <TechnologyView registry={registry} />,
      };
    }

    return { sidebar: null, content: <SectionPlaceholder {...PLACEHOLDERS[section]} /> };
  })();

  return (
    <div className="app">
      <Toolbar registry={registry} />
      <div className="subtabs">{section === "buildings" ? <BuildingsTabs registry={registry} /> : null}</div>
      <div className={screen.panel ? "layout layout--panel" : "layout"}>
        <aside className="sidebar">{screen.sidebar}</aside>
        <main className={screen.isBoard ? "content content--board" : "content"}>{screen.content}</main>
        {screen.panel ? <aside className="panel">{screen.panel}</aside> : null}
      </div>
    </div>
  );
};

export { App };
