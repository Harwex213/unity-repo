import { useSignals } from "@preact/signals-react/runtime";
import { getModule } from "../../core/modules";
import { useStore } from "../../store/store";
import { Icon } from "./icon";
import type { FC } from "react";

/** The card of one module. It is read-only: the numbers of modules are not designed yet. */
const ModuleView: FC = () => {
  useSignals();
  const store = useStore();
  const module = getModule(store.ui.selectedModuleId.value);

  return (
    <div className="editor">
      <header className="editor-header">
        <Icon src={module.art} size="l" className="editor-art" />
        <div>
          <h1 className="editor-title">{module.label}</h1>
          <div className="muted">id: {module.id}</div>
        </div>
      </header>

      {module.sections.map((section) => (
        <section key={section.title} className="section">
          <div className="section-head section-head--icon">
            {section.icon ? <Icon src={section.icon} size="m" /> : null}
            <h2>{section.title}</h2>
          </div>
          <ul className="rules-list">
            {section.rules.map((rule) => (
              <li key={rule}>{rule}</li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
};

export { ModuleView };
