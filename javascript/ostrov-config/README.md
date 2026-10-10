# ostrov-config

A viewer for versioned game config schemas and configs. Unity reads these configs.
Later the app will also create new configs.

Stack: React + Rspack + TypeScript. Ajv (draft 2020-12) validates every config.

## What it shows

- The left panel has two tabs: **schemas** and **configs**. Below the tabs is the list of versions.
- Schema versions are semver. The newest version is at the top.
- Config versions are integers. The newest version is at the top.
- Each config row shows its `schemaVersion` and a badge: `valid`, `invalid` or `no schema`.
- The right panel shows the JSON of the selected version. A config that fails validation shows the Ajv errors above the JSON.
- The URL hash stores the tab and the selection, for example `#configs/game-config%401`.

## Commands

Run every command in `javascript/ostrov-config/`.

```bash
npm install         # install dependencies
npm run dev         # rspack dev server on a free port
npm run build       # bundle into dist/
npm run typecheck   # tsc --noEmit
```

## Data

```
data/
  schemas/<family>/<semver>.json       # for example schemas/game-config/1.0.0.json
  configs/<family>/<configVersion>.json  # for example configs/game-config/1.json
```

Rspack collects every JSON file in these folders at build time (`import.meta.webpackContext` in `src/data/load-data.ts`).
A new file needs no code change. The dev server picks it up on reload.

### Add a schema version

1. Create `data/schemas/game-config/<x.y.z>.json`. The file name is the version.
2. Give the schema a unique `$id`, for example `https://ostrov.game/schemas/game-config/<x.y.z>.json`.

### Add a config version

1. Create `data/configs/game-config/<n>.json`. The file name must equal `configVersion`.
2. Set `schemaVersion` to an existing schema version. The app checks the config against that schema.
3. Set `$schema` to `../../schemas/game-config/<x.y.z>.json`. IDEs use this path for autocomplete.

## Layout

| path | role |
| --- | --- |
| `src/core/` | pure logic without React: catalog, validation, semver, URL hash |
| `src/data/` | loads the JSON files from `data/` |
| `src/ui/` | React components |
