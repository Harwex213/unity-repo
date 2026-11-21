import { useSignals } from "@preact/signals-react/runtime";
import { useRef } from "react";
import { useStore } from "../../store/store";
import type { FC } from "react";
import type {
  TExportLinksAction,
  TImportLinksAction,
  TResetLinksAction,
  TSetSectionAction,
} from "../../domain/registry";
import type { TSection } from "../../store/ui-state";

type TToolbarProps = {
  registry: {
    exportLinksAction: TExportLinksAction;
    importLinksAction: TImportLinksAction;
    resetLinksAction: TResetLinksAction;
    setSectionAction: TSetSectionAction;
  };
};

const SECTIONS: readonly { id: TSection; label: string }[] = [
  { id: "buildings", label: "Здания" },
  { id: "technologies", label: "Технологии" },
  { id: "negative-events", label: "Негативные ивенты" },
  { id: "units", label: "Юниты" },
  { id: "enemies", label: "Враги" },
  { id: "artifacts", label: "Артефакты" },
];

const Toolbar: FC<TToolbarProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const section = store.ui.section.value;
  const notice = store.ui.notice.value;
  const isChanged = store.derived.isChanged.value;
  const fileInputRef = useRef<HTMLInputElement>(null);

  return (
    <header className="toolbar">
      <div className="toolbar-title">Ostrov · связи</div>

      <div className="tabs" role="tablist">
        {SECTIONS.map((item) => (
          <button
            key={item.id}
            type="button"
            role="tab"
            aria-selected={item.id === section}
            className={item.id === section ? "tab tab--active" : "tab"}
            onClick={() => registry.setSectionAction(item.id)}
          >
            <span className="tab-label" data-label={item.label}>
              {item.label}
            </span>
          </button>
        ))}
      </div>

      <div className="toolbar-actions">
        <span className="muted">{isChanged ? "Изменено" : "Как в прототипе"}</span>
        <button type="button" className="button" onClick={() => fileInputRef.current?.click()}>
          Импорт
        </button>
        <button type="button" className="button button--primary" onClick={registry.exportLinksAction}>
          Экспорт JSON
        </button>
        <button
          type="button"
          className="button"
          disabled={!isChanged}
          onClick={() => {
            if (window.confirm("Сбросить все связи к значениям прототипа?")) {
              registry.resetLinksAction();
            }
          }}
        >
          Сбросить
        </button>
        <input
          ref={fileInputRef}
          type="file"
          accept="application/json,.json"
          hidden
          onChange={(event) => {
            const file = event.target.files?.[0];
            event.target.value = "";
            if (file) {
              void registry.importLinksAction(file);
            }
          }}
        />
      </div>

      {notice ? (
        <div className={`notice notice--${notice.kind}`} role="status">
          {notice.text}
        </div>
      ) : null}
    </header>
  );
};

export { Toolbar };
