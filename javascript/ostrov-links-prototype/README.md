# ostrov-links-prototype

An editor of the links between biomes, buildings and resources of Ostrov.
It answers two questions:

- On which biome can a building stand?
- Which resources does a building bring?

Stack: React + Rspack + TypeScript + `@preact/signals-react`.
The data and the architecture come from `javascript/ostrov-prototype`.

## Model

A building is a die. The die has two kinds of faces:

- `baseFaces`. Every copy of the building has these faces, on any biome.
- `biomeFaces`. A biome adds one more face. A biome that is missing from `biomeFaces` cannot host the building.

A face is `{ resource, amount, toxicity }`. The resources a building brings are the resources of its faces.

## Screens

- **Здание**. The editor of one building: the main resource, the base faces, and 16 biome cards. A checkbox on a card allows the building on the biome. The card also edits the biome face.
- **Матрица**. Two tables. "Здания × биомы": a click on a cell allows or forbids the building on the biome. "Здания × ресурсы": base faces / biome faces for each resource. Below the tables is the list of problems, for example a biome with no buildings.

The browser keeps the last edit in `localStorage`. "Сбросить" returns the values of the prototype.

## Export and import

"Экспорт JSON" downloads `ostrov-links.json`:

```json
{
  "format": "ostrov-links",
  "version": 1,
  "buildings": [
    { "id": "farm", "yields": "food", "baseFaces": [ ... ], "biomeFaces": { "grassland": { ... } } }
  ]
}
```

Each item of `buildings` has the shape of `buildings[]` in the game config of `javascript/ostrov-config`.
"Импорт" reads the same file. A building that is missing from the file keeps its current links.

## Commands

Run every command in `javascript/ostrov-links-prototype/`.

```bash
npm install         # install dependencies
npm run dev         # rspack dev server on a free port
npm run build       # bundle into dist/
npm run typecheck   # tsc --noEmit
```

## Layers

| path | role |
| --- | --- |
| `src/core/` | pure data and logic: catalogs, link operations, export and import |
| `src/store/` | signal slices; the only mutable state |
| `src/domain/` | actions written as `(store, ...args)`, bound in `registry-creator.ts` |
| `src/ui/` | React components; they read signals and call registry actions |
