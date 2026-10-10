import { useSignals } from "@preact/signals-react/runtime";
import { useStore } from "../store/store";
import { BuildingEditor } from "./components/building-editor";
import { BuildingList } from "./components/building-list";
import { MatrixView } from "./components/matrix-view";
import { Toolbar } from "./components/toolbar";
import type { FC } from "react";
import type { TAppRegistry } from "../domain/registry";
import "./app.css";

type TAppProps = {
  registry: TAppRegistry;
};

const App: FC<TAppProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const view = store.ui.view.value;

  return (
    <div className="app">
      <Toolbar registry={registry} />
      <div className="layout">
        <aside className="sidebar">
          <BuildingList registry={registry} />
        </aside>
        <main className="content">
          {view === "building" ? <BuildingEditor registry={registry} /> : <MatrixView registry={registry} />}
        </main>
      </div>
    </div>
  );
};

export { App };
