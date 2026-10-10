import { useSignals } from "@preact/signals-react/runtime";
import { useRef } from "react";
import { useStore } from "../../store/store";
import type { FC } from "react";
import type {
  TExportLinksAction,
  TImportLinksAction,
  TResetLinksAction,
  TSetViewAction,
} from "../../domain/registry";
import type { TView } from "../../store/ui-state";

type TToolbarProps = {
  registry: {
    exportLinksAction: TExportLinksAction;
    importLinksAction: TImportLinksAction;
    resetLinksAction: TResetLinksAction;
    setViewAction: TSetViewAction;
  };
};

const VIEWS: readonly { id: TView; label: string }[] = [
  { id: "building", label: "Здание" },
  { id: "matrix", label: "Матрица" },
];

const Toolbar: FC<TToolbarProps> = ({ registry }) => {
  useSignals();
  const store = useStore();
  const view = store.ui.view.value;
  const notice = store.ui.notice.value;
  const issueCount = store.derived.issues.value.length;
  const isChanged = store.derived.isChanged.value;
  const fileInputRef = useRef<HTMLInputElement>(null);

  return (
    <header className="toolbar">
      <div className="toolbar-title">Ostrov · связи</div>

      <div className="tabs" role="tablist">
        {VIEWS.map((item) => (
          <button
            key={item.id}
            type="button"
            role="tab"
            aria-selected={item.id === view}
            className={item.id === view ? "tab tab--active" : "tab"}
            onClick={() => registry.setViewAction(item.id)}
          >
            {item.label}
            {item.id === "matrix" && issueCount > 0 ? <span className="badge">{issueCount}</span> : null}
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
