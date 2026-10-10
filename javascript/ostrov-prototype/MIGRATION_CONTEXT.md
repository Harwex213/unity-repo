# ostrov-prototype: контекст для миграции

Документ для будущих сессий. Собран чтением кода в
`C:\Users\Shatterbind\Documents\Unity\unity-repo\javascript\ostrov-prototype`.
Дата: 2026-10-10.

## Важно: это TypeScript, а не JavaScript

Проект написан на **TypeScript + TSX** (React). В `tsconfig.json` включён
`strict`, `noUncheckedIndexedAccess`, `verbatimModuleSyntax`. `allowJs: true`
есть, но `.js` файлов в `src/` нет. Если миграция нужна именно на чистый JS,
это отдельная работа: типы придётся снять.

## Стек (из package.json)

| Пакет | Версия |
| --- | --- |
| react / react-dom | 19.2.0 |
| @preact/signals-react | 3.9.0 (состояние) |
| three | 0.185.1 (глобус, WebGL) |
| @rspack/cli, @rspack/core | 2.1.4 (сборка) |
| typescript | 5.9.3 |

Команды (из `README.md`, запускать из `javascript/`):

```bash
yarn
yarn workspace @hw/ostrov-prototype-v6 dev        # rspack serve
yarn workspace @hw/ostrov-prototype-v6 build      # dist/
yarn workspace @hw/ostrov-prototype-v6 typecheck  # tsc --noEmit
```

Имя пакета: `@hw/ostrov-prototype-v6`. Архитектура из монорепо по умолчанию:
`@hw/frontend-plain-architecture-v2`.

## Архитектура (три слоя)

| Путь | Роль |
| --- | --- |
| `src/store/` | слайсы из сигналов; единственное изменяемое состояние |
| `src/domain/` | действия вида `(store, ...args)`, связываются в `registry-creator.ts` |
| `src/ui/` | React-компоненты; только читают сигналы и вызывают действия реестра |

Правило: UI никогда не видит store напрямую. Он получает `registry`
(объект колбэков). Запись в состояние делает только domain.

Добавление действия: 1) функция в `src/domain/`, 2) тип в `src/domain/registry.ts`,
3) добавить в `rawRegistry` в `src/domain/registry-creator.ts`.

Точка входа `src/main.tsx`: создаёт store, registry, вызывает `createSession`,
включает guide по `?guide`, меню по `?menu`, маршрутизация по `location.hash`
(`#/island`, `#/world`, `#/battle`).

Store (`src/store/store.ts`) состоит из слайсов: `route`, `game`, `ui`, `world`,
`battle`, `guide`, плюс `derived` (computed-значения, например `humanPlayer`,
`isReadonly`, `powerLeft`, `guideContextStep`).

Ключевые слайсы:
- `game-state.ts`: `stage` (setup/starting/entering/play), `turn`, `phase`
  (build/tax/scout/clear), `players`, `humanPlayerId`, `researched`, `tax`,
  `ready`, `slot`, `soilCleansedTurn`, `outcome`.

## Игровые концепции (из `core/types.ts` и комментариев)

Игра называется **Toxic Island** (в комментариях). Остров из гексов, игрок
строит здания, токсичность растёт, есть противники (rivals) и босс.

- Биомы: 16 штук (`grassland`, `plains`, `forrest`, `savanna`, `rainforest`,
  `taiga`, `tundra`, `desert`, `polar_desert`, `swamp`, `badlands`, `crater`,
  `volcano`, `hills`, `mountains`, `cliffs`). Названия сохранены как в спеке,
  включая опечатку `forrest`.
- Ресурсы: food, stone, wood, population, hammers, science, scouting, mana,
  а также power (власть) и mad (отрицательный ресурс). Токсичность не ресурс:
  она хранится в гексах и в `toxicMeter` острова (0..1000).
- Здания (8): converter (центральный, ставит конец игры, открывается только
  после победы над боссом), farm, mine, sawmill, village, masons_guild,
  observatory, university. У каждого есть `baseFaces` и `biomeFaces` (грани кубика).
- Фазы хода: build, tax (броски кубиков, траты власти, слот токсичности),
  scout (карта мира), clear (зачистка: отдельный симулятор боя с canvas).
- Конец игры: `core/game-over.ts`, проверяется дважды за ход.
- Слот токсичности: `core/toxic-slot.ts` (самый большой файл core, 22 КБ).
- Противники: `core/rival-ai.ts`, `domain/rival-actions.ts`.
- Мир: `core/world-gen.ts` (16 КБ), клетки с `cellId`, острова-«диких» цели.
- Фракции: `core/factions.ts`, технологии: `core/techs.ts`.

Мир детерминирован: `nickname` является сидом (`core/rng.ts`, `core/noise.ts`).
Одно и то же имя даёт тот же остров и глобус. **При миграции обязательно
перенести тот же PRNG и шум, иначе сиды перестанут совпадать.**

## Инвентарь файлов

### Корень
- `index.html` (671 Б), `package.json` (814 Б), `package-lock.json` (20 КБ),
  `README.md` (1.1 КБ), `rspack.config.mjs` (1.3 КБ), `tsconfig.json` (1 КБ).

### `src/core/` (чистые данные и правила, без React и store)
Самые большие: `cleanup-sim.ts` (77.6 КБ), `toxic-slot.ts` (22.5 КБ),
`world-gen.ts` (16.4 КБ), `factions.ts` (14.3 КБ), `tax-plan.ts` (11.4 КБ),
`cleanup-level.ts` (12.8 КБ), `cleanup-border.ts` (9.5 КБ), `buildings.ts` (8.6 КБ),
`cleanup-collision.ts` (8.5 КБ), `units.ts` (8.2 КБ), `techs.ts` (7.4 КБ),
`island-gen.ts` (7.2 КБ), `cleanup-attach.ts` (6 КБ), `types.ts` (5.9 КБ),
`guide.ts` (5.9 КБ), `biomes.ts` (4.6 КБ), `icons.ts` (4.6 КБ), `world-rules.ts` (4.8 КБ),
`game-over.ts` (4.2 КБ), `hex.ts` (4 КБ), `tax.ts` (3.7 КБ), `soil-cleanse.ts` (3.7 КБ),
`rival-ai.ts` (3.5 КБ), `stronghold.ts` (3.5 КБ), `resources.ts` (3.4 КБ),
`production-reveal.ts` (3.1 КБ), `structure-hp.ts` (3.1 КБ), `noise.ts` (3 КБ),
`production-reveal.ts`, `rng.ts` (2 КБ), `dice.ts` (2.3 КБ), `build-check.ts` (2 КБ),
`hex-art.ts`, `skills.ts`, `trail-events.ts`, `phases.ts`, `main-menu.ts`, `demolish.ts`.

### `src/domain/` (действия, 25 файлов)
`game-actions` (10 КБ), `tax-actions` (16 КБ), `world-actions`, `build-actions`,
`cleanup-actions`, `rival-actions`, `setup-actions`, `ui-actions`, `slot-actions`,
`soil-actions`, `route-actions`, `ready-actions`, `menu-actions`, `guide-actions`,
`tech-actions`, `player-updates`, `registry`, `registry-creator`.

### `src/store/` (7 файлов)
`store.ts`, `game-state.ts`, `ui-state.ts` (5.4 КБ), `route-state.ts`,
`world-state.ts`, `battle-state.ts`, `guide-state.ts`.

### `src/ui/`
- `app.tsx`, `app.css` (**110 КБ**, самый большой стиль, основной риск при миграции стилей).
- `pages/`: `island-page`, `world-page`, `battle-page`.
- `components/` (около 50 файлов): HUD-панели, модалки, `island-canvas.tsx`
  (27.5 КБ, рисование острова на canvas), `globe-*.ts(x)` (three.js, шейдеры),
  `cleanup/` (канвас зачистки, рендер 57 КБ, небо на WebGL), `main-menu/`, `tech-*`, `tax-pick-modal`.
- `ground/`: `render-island-ground.ts` (21 КБ), `use-island-ground.ts`.

### `src/assets/`
Около 100 изображений: биомы (`.webp`), фракции, здания, иконки (`.png`), глобус,
меню, обучение (`tutorial/*.webp`). Используются через `asset/resource`.

## Что я читал и что нет

Прочитано: `README.md`, `package.json`, `rspack.config.mjs`, `tsconfig.json`,
`src/main.tsx`, `src/store/store.ts`, `src/store/game-state.ts`,
`src/domain/registry-creator.ts`, `src/core/types.ts`, первые 120 строк
`src/domain/game-actions.ts`.

**Не читал:** большую часть `core/` (особенно `cleanup-sim.ts`, `toxic-slot.ts`,
`world-gen.ts`, `tax-plan.ts`), `ui/`, `app.css`, `domain/` кроме game-actions.
Для полной миграции эти файлы нужно прочитать по мере работы.

## Риски и заметки для миграции

1. **Сиды и PRNG.** `core/rng.ts` и `core/noise.ts` должны быть перенесены
   точно, иначе мир и острова изменятся.
2. **Сигналы.** `@preact/signals-react` нужно заменить на аналог целевого стека
   (например, Solid signals, Zustand, или собственный). Слои store/domain
   устроены так, что `core/` не зависит от сигналов, и это облегчает перенос.
3. **Canvas и WebGL.** `island-canvas.tsx`, `render-island-ground.ts`,
   `cleanup-render.ts`, `sky-gl.ts`, `menu-background-gl.ts`, `globe-shaders.ts`.
   Это самая трудоёмкая часть: шейдеры и рендер нужно переписать или перенести без изменений.
4. **three.js 0.185.** Если целевой стек без three, глобус потребует отдельной работы.
5. **app.css 110 КБ.** Проверить, есть ли CSS-переменные и темы, и сохранить их.
6. **Опечатка `forrest`.** Сохранять в данных, пока не решено иначе: её используют
   ассеты (`biomes/forrest.webp`) и типы.
7. **Порядок миграции (предложение).** Сначала `core/` (чистые функции, легко
   покрыть тестами сравнением со старыми результатами на тех же сидах), затем
   `store` и `domain`, затем UI по страницам (`island`, `world`, `battle`).

## Как пользоваться в следующей сессии

Открыть этот файл и сказать, какой слой переносить. Доступ к папке проекта на
компьютере нужно запрашивать заново в каждой сессии (`device_request_folder_access`
на путь проекта), затем файлы стейджатся через `device_stage_files`.

## Статус порта в Unity (2026-10-10)

Цель: `projects/Hexwex` (Unity 6000.5.10f1, URP, 3D). Источник остаётся 2D-прототипом.

| Слой прототипа | Где в Unity | Состояние |
| --- | --- | --- |
| `core/` rng, noise, hex, biomes, resources, buildings, island-gen, stronghold, structure-hp, dice, tax, tax-plan, toxic-slot, techs, build-check, demolish, soil-cleanse, game-over, часть rival-ai | `Assets/Scripts/Hexwex/Core` (asmdef `Hexwex.Core`, без UnityEngine) | перенесено |
| `store/` + `domain/` (setup, build, tax, slot, soil, tech, game) | `Core/GameSession.cs` | перенесено; соперники ходят сразу, без таймеров |
| `ui/` остров, HUD | `Assets/Scripts/Hexwex/View`, `Assets/UI/Hud.uss` | 3D-остров и HUD на UI Toolkit; модели зданий — заглушки из примитивов |
| `core/world-gen`, `world-rules`, `trail-events`, `factions`, перелёт соперников (`rival-ai`) | `Core/WorldGen.cs`, `WorldRules.cs`, `TrailEvents.cs`, `Factions.cs`, `RivalAi.cs` | перенесено; планета сверена с прототипом по клеткам |
| `ui/` глобус, панель гекса мира, шлейф, фракции | `View/GlobeView.cs`, `Hud.cs` | 3D-шар из плит вместо шейдерной карты; без миникарты и анимации перелёта |
| `core/cleanup-*`, `units`, `skills`, `domain/cleanup-actions` | `Core/CleanupSim.cs`, `CleanupLevel.cs`, `CleanupCollision.cs` (+ стыковка), `CleanupBorder.cs`, `Units.cs`, `GameSession` (`EnterClearing`, `StepBattle`, `FinishClearing`) | перенесено: бой в реальном времени, стыковка островов, окна в облаках, навык «Разрушить землю», босс. Логика та же, но побитовой сверки с прототипом нет |
| `ui/` бой (`cleanup-canvas`, `cleanup-render`, HUD, итоги) | `View/BattleView.cs`, `Hud.cs` | 3D-уровень из примитивов; без эффектов попаданий, шва стыковки, анимации разрушения и миникарты |
| главное меню (`ui/components/main-menu`, `domain/menu-actions`) | `View/MainMenu.cs`, `Assets/UI/Menu.uss` | перенесено; фон — живой остров вместо картины |
| guide (обучение: `core/guide`, `domain/guide-actions`, `learn-guide.tsx`) | `Core/Guide.cs`, `View/LearnGuide.cs`, `Assets/UI/Guide.uss`, картинки в `Resources/Tutorial` (PNG из исходных WebP) | перенесено; включается пунктом меню «Обучение» |
| просмотр островов соперников (`route-actions`, `players-panel`, `isReadonly`) | `View/GameRoot.cs` (`ViewIsland`, `IsReadonly`), `Hud.cs` | перенесено: клик по строке игрока открывает его остров только для просмотра |
| анимации (`production-reveal`, полёты выплат, барабаны слота, эффекты боя) | `View/HudEffects.cs`, `GameRoot.cs` (`StartReveal`, `StartCollection`), `IslandView.Pulse`, `BattleView.PlayEffect` | перенесено: показ бросков, полёт выплат к HUD, барабаны слота, числа урона и вспышки в бою. Без шва стыковки и анимации перелёта по глобусу |
| 3D-модели построек (в прототипе — 2D-спрайты `assets/hex-buildings`) | `art-sources/Hexwex/build_buildings.py` → `Assets/Art/Models/Buildings.fbx` → `Resources/Buildings/*.prefab` (меню `Hexwex/Rebuild Model Prefabs`) | девять моделей в стиле `art-sources/Ostrov`: четыре взяты оттуда, пять собраны скриптом Blender. Декор биомов — 33 модели из `build_scenery.py` → `Scenery.fbx` → `Resources/Scenery`. Юниты и монстры — 27 фигур из `build_units.py` → `Units.fbx` → `Resources/Units` (без костей: ноги, руки и крылья — отдельные объекты на шарнирах, их двигает `View/FigurePose.cs`: шаг, удар, выстрел, взмах крыльев, падение). Маркеры глобуса — 6 моделей из `build_markers.py` → `Markers.fbx` → `Resources/Markers` (знамя красится в цвет игрока, дикий остров — в цвет биома) |

- Сцена `Assets/Scenes/Island.unity` генерируется: меню `Hexwex/Rebuild Island Scene`. Руками её не править.
- Тесты: `Assets/Tests/EditMode` (61). `RngParityTests` сверяет PRNG с эталоном, снятым с JS-движка. `WorldParityTests` сверяет планету с `WorldReference.txt` — выводом самого `core/world-gen.ts` в Firefox для двух сидов.
- `Vec3.Normalized` повторяет `Math.hypot` движка V8; не заменять на `Math.Sqrt` суммы квадратов.
- Правила паритета: `JsMath.Round` вместо `Math.Round`; только стабильные сортировки (`OrderBy`); порядок вызовов генератора не менять.
- Модель здания заменяется префабом `Resources/Buildings/<имя арта>` без правки кода.
