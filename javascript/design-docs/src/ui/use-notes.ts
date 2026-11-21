import { useCallback, useEffect, useRef, useState } from "react";

// A free-form note of a document. The notes live in src/documents/<id>/notes.json.
type TNote = {
  id: string;
  text: string;
  // ISO date.
  createdAt: string;
};

type TNotesState = {
  notes: TNote[];
  // False when no dev server answers: a production build shows the notes of build time read-only.
  editable: boolean;
  // The text of the last failed save, or null.
  error: string | null;
  add: (text: string) => void;
  edit: (id: string, text: string) => void;
  remove: (id: string) => void;
};

const notesUrl = (docId: string) => `/api/notes/${encodeURIComponent(docId)}`;

const bundledNotes = (docId: string): TNote[] => __DOC_NOTES__[docId] ?? [];

const newId = () =>
  typeof crypto.randomUUID === "function" ? crypto.randomUUID() : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;

const useNotes = (docId: string | null): TNotesState => {
  const [notes, setNotes] = useState<TNote[]>(() => (docId ? bundledNotes(docId) : []));
  const [editable, setEditable] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Saves go one after another, so the file always gets the latest list last.
  const saveQueueRef = useRef<Promise<void>>(Promise.resolve());

  useEffect(() => {
    setNotes(docId ? bundledNotes(docId) : []);
    setEditable(false);
    setError(null);
    if (!docId) {
      return;
    }
    const controller = new AbortController();
    fetch(notesUrl(docId), { signal: controller.signal })
      .then((response) => (response.ok ? (response.json() as Promise<TNote[]>) : Promise.reject(new Error(String(response.status)))))
      .then((fresh) => {
        setNotes(fresh);
        setEditable(true);
      })
      .catch(() => {
        // No dev server: keep the notes of build time, read-only.
      });
    return () => controller.abort();
  }, [docId]);

  const save = useCallback(
    (next: TNote[]) => {
      setNotes(next);
      if (!docId) {
        return;
      }
      saveQueueRef.current = saveQueueRef.current.then(async () => {
        try {
          const response = await fetch(notesUrl(docId), {
            method: "PUT",
            headers: { "content-type": "application/json" },
            body: JSON.stringify(next),
          });
          if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
          }
          setError(null);
        } catch (reason) {
          setError(`Заметки не сохранились: ${String(reason)}`);
        }
      });
    },
    [docId],
  );

  return {
    notes,
    editable,
    error,
    add: (text) => save([...notes, { id: newId(), text, createdAt: new Date().toISOString() }]),
    edit: (id, text) => save(notes.map((note) => (note.id === id ? { ...note, text } : note))),
    remove: (id) => save(notes.filter((note) => note.id !== id)),
  };
};

export { useNotes };
export type { TNote, TNotesState };
