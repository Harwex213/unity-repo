# @hw/ostrov-prototype-v6

Frontend prototype scaffold: React + rspack + TypeScript + `@preact/signals-react`.
The architecture is the monorepo default (`@hw/frontend-plain-architecture-v2`): a store
of signal slices, a hand-written domain registry, and a UI layer that only reads signals.

## Commands

Run every command with cwd `javascript/`.

```bash
yarn                                              # install, from javascript/
yarn workspace @hw/ostrov-prototype-v6 dev        # rspack dev server on a free port
yarn workspace @hw/ostrov-prototype-v6 build      # bundle into dist/
yarn workspace @hw/ostrov-prototype-v6 typecheck  # tsc --noEmit
```

## Store snapshots

The dev server keeps one snapshot of the whole store in `store-snapshot.json`, next to
`package.json`. Run these commands in the browser console:

```js
await prototype.saveStoreSnapshot(); // writes the store to the file
await prototype.loadStoreSnapshot(); // reads the file back into the running game
```

A save is refused while an animation owns the turn or the riot modal is open. These
moments depend on timers and callbacks that a JSON file cannot hold. The commands
work only on the dev server.

## Layers

| path | role |
| --- | --- |
| `src/store/` | signal slices; the only mutable state |
| `src/domain/` | actions written as `(store, ...args)`, bound in `registry-creator.ts` |
| `src/ui/` | React components; they read signals and call registry actions |

A new action is added in three steps: write it in `src/domain/`, add its type to
`src/domain/registry.ts`, and list it in `rawRegistry` in `src/domain/registry-creator.ts`.
