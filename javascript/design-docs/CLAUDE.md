# design-docs

A viewer of design documents: a list on the left and a canvas with zoom and pan on the right.
Every document is drawn in code, in world units, in the style of an Excalidraw wireframe.

## Ownership

The work is split so that many documents can change in parallel.

| path | owner | rule |
| --- | --- | --- |
| `src/documents/<id>/` | one document editor at a time | the document's code, assets and palette |
| `src/kit/`, `src/core/`, `src/canvas/`, `src/ui/`, `src/types/`, `scripts/`, configs | the main session | shared code; a document editor never changes it |

- A document imports only from its own folder, `src/kit/` and `src/core/`. It never imports another document.
- Shared code never imports a document. `src/documents/registry.ts` finds documents at build time.
- A document keeps its own colors in `src/documents/<id>/palette.ts`, spread over the kit palette.
  Its images live in `src/documents/<id>/assets/`.
- `src/documents/<id>/notes.json` holds the user's notes about the document, written by the viewer
  (`scripts/notes-store.mjs`, `src/ui/use-notes.ts`). A document editor reads the notes as context
  and never changes the file unless the user asks.
- A document editor who needs a new shared primitive writes it inside the document folder first.
  The main session moves it into `src/kit/` later, if a second document needs it.

## Commands

```sh
npm run check -- <id>                 # isolation + types of one document only
npm run snapshot -- <id> [x,y,w,h …]  # PNGs in .snapshots/<id>/: full.png and view-<n>.png per rect
npm run new-doc -- <id> "<title>" "<description>" [order]
npm run check:all                     # isolation + types of everything (main session)
npm run build
```

`check` and `snapshot` touch only files of their document (`.cache/tsconfig.<id>.json`, `.snapshots/<id>/`).
An unfinished edit of one document does not fail the check or the snapshot of another.

A view rect is in world units. The same rect works as a link: `#<id>@x,y,w,h`.

## Workflow of the main session

- The main session changes shared code and creates new documents (`npm run new-doc`).
- Each user request about the content of a document goes to a `doc-editor` subagent
  (`.claude/agents/doc-editor.md`), one subagent per document. The prompt names the document id,
  quotes the user's request and lists the paths of the user's images.
- Requests about different documents run as parallel subagents. Two subagents never edit one document.
- After a change of shared code, the main session runs `npm run check:all` and a snapshot of every document.

## Document conventions

- `index.ts` exports `doc: TDocument`. `doc.id` equals the folder name. `doc.order` sets the place in the list.
- Coordinates are named constants in world units. Arrow tips are computed from the drawing they point at,
  not typed by hand.
- Texts from the user's wireframe are kept word for word, except obvious typos.
- Comments and identifiers are in English. Document texts are in the language of the document.
- The viewer copies every `fillText` call into a hidden DOM layer (`src/canvas/text-layer.ts`), and a double click
  selects one text block. Lines form one block when they follow each other in draw order with the same font,
  alignment, transform and anchor x, and the next line starts at most 1.6 line heights lower.
  Draw a paragraph line by line from top to bottom, and it stays one block.
