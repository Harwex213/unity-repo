import { useEffect, useLayoutEffect, useRef, useState } from "react";
import type { FC, KeyboardEvent } from "react";
import type { TNote, TNotesState } from "../use-notes";

type TNotesPanelProps = {
  state: TNotesState;
  onClose: () => void;
};

// A second click within this time confirms a delete.
const DELETE_CONFIRM_MS = 3000;

const formatDate = (iso: string) => {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? ""
    : date.toLocaleDateString("ru-RU", { day: "2-digit", month: "2-digit", year: "numeric" });
};

// A textarea grows with its text.
const useAutoHeight = (value: string) => {
  const ref = useRef<HTMLTextAreaElement>(null);
  useLayoutEffect(() => {
    const element = ref.current;
    if (element) {
      element.style.height = "auto";
      element.style.height = `${element.scrollHeight}px`;
    }
  }, [value]);
  return ref;
};

// ⌘Enter or Ctrl+Enter submits, Escape cancels.
const submitKeys = (onSubmit: () => void, onCancel?: () => void) => (event: KeyboardEvent<HTMLTextAreaElement>) => {
  if (event.key === "Enter" && (event.metaKey || event.ctrlKey)) {
    event.preventDefault();
    onSubmit();
  } else if (event.key === "Escape" && onCancel) {
    event.preventDefault();
    onCancel();
  }
};

type TNoteEditorProps = {
  initial: string;
  placeholder?: string;
  submitLabel: string;
  autoFocus?: boolean;
  onSubmit: (text: string) => void;
  onCancel?: () => void;
};

const NoteEditor: FC<TNoteEditorProps> = ({ initial, placeholder, submitLabel, autoFocus, onSubmit, onCancel }) => {
  const [text, setText] = useState(initial);
  const ref = useAutoHeight(text);
  const canSubmit = text.trim() !== "";

  useEffect(() => {
    if (autoFocus && ref.current) {
      ref.current.focus();
      ref.current.setSelectionRange(ref.current.value.length, ref.current.value.length);
    }
  }, [autoFocus, ref]);

  const submit = () => {
    if (canSubmit) {
      onSubmit(text.trim());
      setText("");
    }
  };

  return (
    <div className="note-editor">
      <textarea
        ref={ref}
        className="note-textarea"
        value={text}
        rows={2}
        placeholder={placeholder}
        onChange={(event) => setText(event.target.value)}
        onKeyDown={submitKeys(submit, onCancel)}
      />
      <div className="note-editor-actions">
        <span className="note-hint">⌘Enter</span>
        {onCancel && (
          <button type="button" className="note-button" onClick={onCancel}>
            Отмена
          </button>
        )}
        <button type="button" className="note-button is-primary" disabled={!canSubmit} onClick={submit}>
          {submitLabel}
        </button>
      </div>
    </div>
  );
};

type TNoteItemProps = {
  note: TNote;
  editable: boolean;
  onEdit: (text: string) => void;
  onRemove: () => void;
};

const NoteItem: FC<TNoteItemProps> = ({ note, editable, onEdit, onRemove }) => {
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  useEffect(() => {
    if (!confirmDelete) {
      return;
    }
    const timer = window.setTimeout(() => setConfirmDelete(false), DELETE_CONFIRM_MS);
    return () => window.clearTimeout(timer);
  }, [confirmDelete]);

  if (editing) {
    return (
      <li className="note is-editing">
        <NoteEditor
          initial={note.text}
          submitLabel="Сохранить"
          autoFocus
          onSubmit={(text) => {
            onEdit(text);
            setEditing(false);
          }}
          onCancel={() => setEditing(false)}
        />
      </li>
    );
  }

  return (
    <li className="note">
      <div className="note-text">{note.text}</div>
      <div className="note-footer">
        <span className="note-date">{formatDate(note.createdAt)}</span>
        {editable && (
          <span className="note-actions">
            <button type="button" className="note-button" onClick={() => setEditing(true)}>
              Изменить
            </button>
            <button
              type="button"
              className={confirmDelete ? "note-button is-danger" : "note-button"}
              onClick={() => (confirmDelete ? onRemove() : setConfirmDelete(true))}
            >
              {confirmDelete ? "Точно удалить?" : "Удалить"}
            </button>
          </span>
        )}
      </div>
    </li>
  );
};

const NotesPanel: FC<TNotesPanelProps> = ({ state, onClose }) => {
  const { notes, editable, error, add, edit, remove } = state;
  const listRef = useRef<HTMLOListElement>(null);
  const countRef = useRef(notes.length);

  // A new note scrolls into view.
  useEffect(() => {
    const list = listRef.current;
    if (list && notes.length > countRef.current) {
      list.scrollTop = list.scrollHeight;
    }
    countRef.current = notes.length;
  }, [notes.length]);

  return (
    <aside className="notes-panel" aria-label="Notes">
      <header className="notes-header">
        <span className="notes-title">Notes</span>
        <span className="notes-count">{notes.length}</span>
        <button type="button" className="notes-close" onClick={onClose} aria-label="Скрыть заметки">
          ×
        </button>
      </header>
      {notes.length === 0 ? (
        <p className="notes-empty">Заметок пока нет.</p>
      ) : (
        <ol ref={listRef} className="notes-list">
          {notes.map((note) => (
            <NoteItem
              key={note.id}
              note={note}
              editable={editable}
              onEdit={(text) => edit(note.id, text)}
              onRemove={() => remove(note.id)}
            />
          ))}
        </ol>
      )}
      {error && <p className="notes-error">{error}</p>}
      {editable ? (
        <div className="notes-composer">
          <NoteEditor initial="" placeholder="Новая заметка…" submitLabel="Добавить" onSubmit={add} />
        </div>
      ) : (
        <p className="notes-readonly">Только чтение. Заметки правятся в npm run dev.</p>
      )}
    </aside>
  );
};

export { NotesPanel };
