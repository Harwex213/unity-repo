import { useEffect, useState } from "react";
import { CanvasView } from "./components/canvas-view";
import { DocumentList } from "./components/document-list";
import { NotesPanel } from "./components/notes-panel";
import { useHashLocation } from "./use-hash-location";
import { useNotes } from "./use-notes";
import type { FC } from "react";
import type { TDocument } from "../core/types";
import "./app.css";

type TAppProps = {
  documents: TDocument[];
};

const NOTES_OPEN_KEY = "design-docs.notes-open";

// The panel state is a convenience of one browser. Storage may be blocked; then the panel starts closed.
const readNotesOpen = () => {
  try {
    return localStorage.getItem(NOTES_OPEN_KEY) === "1";
  } catch {
    return false;
  }
};

const writeNotesOpen = (open: boolean) => {
  try {
    localStorage.setItem(NOTES_OPEN_KEY, open ? "1" : "0");
  } catch {
    // The state is not saved; nothing else breaks.
  }
};

const App: FC<TAppProps> = ({ documents }) => {
  const [location, selectDocument] = useHashLocation();
  const [notesOpen, setNotesOpen] = useState(readNotesOpen);

  // An unknown or absent id falls back to the first document.
  const selected = documents.find((doc) => doc.id === location.id) ?? documents[0] ?? null;
  const view = selected?.id === location.id ? location.view : null;
  const notes = useNotes(selected?.id ?? null);

  useEffect(() => writeNotesOpen(notesOpen), [notesOpen]);

  return (
    <div className={notesOpen ? "app has-notes" : "app"}>
      <aside className="sidebar">
        <header className="sidebar-header">Документы</header>
        <DocumentList documents={documents} selectedId={selected?.id ?? null} onSelect={selectDocument} />
      </aside>
      <main className="workspace">
        <CanvasView doc={selected} view={view} />
        {!notesOpen && selected && (
          <button type="button" className="notes-toggle" onClick={() => setNotesOpen(true)}>
            Notes <span className="notes-count">{notes.notes.length}</span>
          </button>
        )}
      </main>
      {notesOpen && selected && <NotesPanel key={selected.id} state={notes} onClose={() => setNotesOpen(false)} />}
    </div>
  );
};

export { App };
