# ostrov-prototypes

A storage of prototypes of the game Ostrov. Every prototype lives in its own folder,
builds as its own compilation and opens by its own link: `http://localhost:<port>/<id>/`.

## Ownership

The work is split so that many prototypes can change in parallel.

| path | owner | rule |
| --- | --- | --- |
| `prototypes/<id>/` | one prototype editor at a time | the prototype's code, assets, art, tools and README |
| `shared/`, `types/`, `scripts/`, `package.json`, `tsconfig.json`, `CLAUDE.md`, `.claude/` | the main session | shared code; a prototype editor never changes it |

- A prototype imports only from its own folder, from `@shared/…` and from packages of `package.json`.
  It never imports another prototype.
- Shared code never imports a prototype.
- A prototype editor who needs a shared helper writes it inside the prototype folder first.
  The main session moves it into `shared/` later, if a second prototype needs it.
- A new npm package is a change of `package.json`, so only the main session adds it.

## Prototype folder

| file | role |
| --- | --- |
| `prototype.json` | `title`, `description`, `order` in the list, `devServer: { hot, liveReload }` (default: no HMR, live reload on) |
| `src/main.tsx` or `src/main.ts` | the entry |
| `index.html` | optional page template; without it `shared/index.html` is used, with `<div id="root">` |
| `README.md` | what the prototype shows and how to drive it |
| `art/`, `tools/` | optional: art sources and the prototype's own scripts |

Files that the bundler turns into URLs (images, audio, fonts, models, `.bin`) are listed in `types/assets.d.ts`.

## Commands

```sh
npm run dev [-- <id> …]                       # all prototypes on one port: / is the list, /<id>/ is a prototype
npm run check -- <id>                         # isolation + types of one prototype only
npm run snapshot -- <id> [--size WxH] [--wait ms] [view …]
                                              # PNGs in .snapshots/<id>/shot-<n>.png; a view is "?t=3", "#x" or ""
npm run with-server -- <id> <command …>       # builds and serves the prototype, runs the command in its folder,
                                              # "{url}" becomes the prototype link
npm run new -- <id> "<title>" "<description>"
npm run check                                 # isolation + types of everything (main session)
npm run build [-- <id> …]                     # dist/<id>/ and dist/index.html
```

`check`, `snapshot` and `with-server` touch only files of their prototype
(`.cache/tsconfig.<id>.json`, `.cache/build/<id>/`, `.snapshots/<id>/`).
An unfinished edit of one prototype does not fail the check or the snapshot of another.

The dev server gives each prototype its own rspack compiler and its own live reload socket.
An error or a rebuild of one prototype does not reload or break the page of another one.
A prototype compiles on the first request to its link, so a new folder needs no restart.

## Workflow of the main session

- The main session changes shared code, creates prototypes (`npm run new`) and deletes them (removes the folder).
- Each user request about the content of a prototype goes to a `prototype-editor` subagent
  (`.claude/agents/prototype-editor.md`), one subagent per prototype. The prompt names the prototype id,
  quotes the user's request word for word and lists the paths of the user's images.
  If the session was started outside this folder, the `prototype-editor` type is not registered:
  spawn a `general-purpose` subagent and tell it to read and follow `.claude/agents/prototype-editor.md`.
- Requests about different prototypes run as parallel subagents in one message. Two subagents never edit one prototype.
  A new request about a prototype whose subagent still works goes to that subagent by SendMessage.
- After a change of shared code, the main session runs `npm run check` and a snapshot of every prototype.
- The main session does not commit unless the user asks.

## Conventions

- Comments and identifiers are in English. Texts on screen are in Russian unless the request says otherwise.
- Stack: TypeScript, React, three.js, rspack. Strict TypeScript, see `tsconfig.json`.
