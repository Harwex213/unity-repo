import type { FC } from "react";
import type { TDocument } from "../../core/types";

type TDocumentListProps = {
  documents: TDocument[];
  selectedId: string | null;
  onSelect: (id: string) => void;
};

const DocumentList: FC<TDocumentListProps> = ({ documents, selectedId, onSelect }) => (
  <nav className="document-list">
    {documents.map((doc) => (
      <button
        key={doc.id}
        type="button"
        className={doc.id === selectedId ? "document-item is-selected" : "document-item"}
        aria-current={doc.id === selectedId ? "page" : undefined}
        onClick={() => onSelect(doc.id)}
      >
        <span className="document-title">{doc.title}</span>
        <span className="document-description">{doc.description}</span>
      </button>
    ))}
  </nav>
);

export { DocumentList };
