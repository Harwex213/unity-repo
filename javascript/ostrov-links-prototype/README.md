# ostrov-links-prototype

An editor of the links between biomes, buildings and resources of Ostrov.
It answers two questions:

- On which biome can a building stand?
- Which resources does a building bring?

Stack: React + Rspack + TypeScript + `@preact/signals-react`.
The data and the architecture come from `javascript/ostrov-prototype`.

## Model

A building has a role:

- `producer` has a die and brings resources.
- `booster` raises the production of the buildings nearby. It has no die.
- `defense` fights enemies. It has no die.

The die has two kinds of faces:

- `baseFaces`. Every copy of the building has these faces, on any biome.
- `biomeFaces`. A biome adds one more face. A biome that is missing from `biomeFaces` cannot host the building.

A face is `{ resource, amount, toxicity }`. The resources a building brings are the resources of its faces.
A building without a die has `yields: null`, no base faces, and `null` for every allowed biome in `biomeFaces`.

The die resources: food, stone, wood, population, mechanisms, microprocessors, science, electricity, mana.
Power ("власть") is not on a die: it changes a rolled face. Import reads the old ids `hammers` and `scouting` as `mechanisms` and `electricity`.

## Screens

The toolbar has six top-level tabs: Здания, Технологии, Негативные ивенты, Юниты, Враги, Артефакты.
Below the toolbar is a row of sub-tabs. It has the same height in every tab, so the content never jumps.
The URL hash keeps the tab, the sub-tab and the selected item, so a reload opens the same screen:
`#/buildings/graph`, `#/buildings/buildings/farm`, `#/buildings/modules/greenhouse`, `#/technologies/mining`.

**Здания** has three sub-tabs:

- **Граф** (default). A board with zoom and pan. Every building (green) and module (blue) is a card. Drag a card to move it. A double click opens the card in its list.
- **Список зданий**. The list of buildings on the left, the editor of one building on the right. The editor shows the role, the technology, the special rules (if any), the main resource, the base faces and 16 biome cards. A building without a die shows only the checkboxes of the biomes.
- **Список модулей**. The list of modules on the left, the card of one module on the right. A module sits in one of the four slots of a building. The data lives in `src/core/modules.ts`.

**Технологии**. Three columns: the list of technologies (sorted by science), the technology tree, and the panel of the selected technology. The tree is a board, like the passive tree of Path of Exile: drag a node to move it, click to select, Shift + click to link the selected technology with another one or to remove the link. The panel shows the picture, the short and the full description, the science cost, the building the technology opens and the linked technologies. Every building needs one technology. The data lives in `src/core/technologies.ts`. The icon of a technology is the icon of its building for now.

## Boards

A board is a canvas with zoom and pan: the wheel or a trackpad pinch zooms around the cursor, dragging the background pans, a double click on the background or "Вписать" shows everything.
The layout of a board is a file in git: `data/buildings-graph.json` and `data/tech-tree.json`.
So the layout survives a restart of the dev server and reaches other machines through git.

- `npm run dev` serves `/data/<board>.json` from disk. A drag or a new link sends the layout back with POST, and the dev server writes the file (`scripts/boards.mjs`). One node and one link per line keeps the git diff short.
- The build copies `data/` into `dist/`. There the boards are read-only.
- A building, module or technology missing from the file appears in a column right of the other nodes.

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
| `data/` | board layouts, written by the dev server, kept in git |
| `scripts/` | the dev-server middleware that reads and writes `data/` |
