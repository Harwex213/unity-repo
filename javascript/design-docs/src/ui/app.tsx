import { CanvasView } from "./components/canvas-view";
import { DocumentList } from "./components/document-list";
import { useHashId } from "./use-hash-id";
import type { FC } from "react";
import type { TDocument } from "../core/types";
import "./app.css";

type TAppProps = {
  documents: TDocument[];
};

const App: FC<TAppProps> = ({ documents }) => {
  const [hashId, setHashId] = useHashId();

  // An unknown or absent id falls back to the first document.
  const selected = documents.find((doc) => doc.id === hashId) ?? documents[0] ?? null;

  return (
    <div className="app">
      <aside className="sidebar">
        <header className="sidebar-header">Документы</header>
        <DocumentList documents={documents} selectedId={selected?.id ?? null} onSelect={setHashId} />
      </aside>
      <main className="workspace">
        <CanvasView doc={selected} />
      </main>
    </div>
  );
};

export { App };
