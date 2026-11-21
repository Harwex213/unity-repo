using System;
using System.Collections.Generic;
using System.Linq;
using Hexwex.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hexwex.View
{
    /// <summary>
    /// The island generator: a scene of its own, not part of the game
    /// (<c>Assets/Scenes/IslandGenerator.unity</c>). On the left a panel holds the
    /// settings of a set of islands: a seed and any number of island types, each
    /// with how many islands of it to make, their size and shape, the biomes they
    /// are made of and in what amounts, how the biomes lie together, toxicity and
    /// buildings already standing. On the right every island of the set is drawn
    /// with the game's own island renderer, in a grid.
    ///
    /// "Сохранить" writes the set as JSON (<see cref="IslandSetFile"/>): its settings,
    /// so it can be opened here again, and its islands hex by hex, for the game.
    /// </summary>
    public sealed class IslandGeneratorWindow : MonoBehaviour
    {
        /// <summary>The grid draws at most this many islands; the file still holds them all.</summary>
        private const int MaxPreviewIslands = 36;
        /// <summary>A change rebuilds the islands this long after the last keystroke.</summary>
        private const float RebuildDelay = 0.35f;

        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private Material surfaceMaterial;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private Camera worldCamera;

        private readonly IslandSetSpec _spec = new IslandSetSpec();
        private List<ForgedIsland> _islands = new List<ForgedIsland>();
        private readonly List<IslandView> _views = new List<IslandView>();
        private readonly List<Label> _tags = new List<Label>();
        private readonly List<Vector3> _anchors = new List<Vector3>();

        private VisualElement _root;
        private VisualElement _panel;
        private VisualElement _recipes;
        private VisualElement _tagLayer;
        private Label _status;
        private Label _details;
        private DropdownField _files;
        private string _fileName = "islands";
        private bool _autoRebuild = true;
        private float _rebuildAt = -1f;
        private int _selected;

        private void Start()
        {
            _spec.Recipes.Add(new IslandRecipe { Name = "Обычный", Count = 3, Size = 30 });
            BuildPanel();
            Rebuild();
        }

        private void Update()
        {
            if (_rebuildAt >= 0f && Time.unscaledTime >= _rebuildAt)
            {
                _rebuildAt = -1f;
                Rebuild();
            }

            if (cameraRig != null && _root != null && _root.panel != null)
            {
                Vector2 pointer = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
                Vector2 flipped = new Vector2(pointer.x, Screen.height - pointer.y);
                cameraRig.PointerOverUi = _panel.worldBound.Contains(RuntimePanelUtils.ScreenToPanel(_root.panel, flipped));
            }

            PlaceTags();
        }

        /* ---------- generation and the preview ---------- */

        /// <summary>Grows the set from the settings and draws it.</summary>
        private void Rebuild()
        {
            try
            {
                _islands = IslandForge.Generate(_spec);
            }
            catch (Exception exception)
            {
                SetStatus("Ошибка генерации: " + exception.Message, true);

                return;
            }

            Draw();
            int hexes = _islands.Sum(island => island.Hexes.Count);
            string preview = _islands.Count > MaxPreviewIslands ? " · показаны первые " + MaxPreviewIslands : "";
            SetStatus("Островов: " + _islands.Count + " · гексов: " + hexes + preview, false);
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _islands.Count - 1));
            ShowDetails();
        }

        /// <summary>Lays the islands out in a grid, each on its own renderer, and names them.</summary>
        private void Draw()
        {
            foreach (IslandView view in _views)
            {
                Destroy(view.gameObject);
            }

            _views.Clear();
            _anchors.Clear();
            foreach (Label tag in _tags)
            {
                tag.RemoveFromHierarchy();
            }

            _tags.Clear();

            List<ForgedIsland> shown = _islands.Take(MaxPreviewIslands).ToList();
            if (shown.Count == 0)
            {
                return;
            }

            // One cell of the grid holds the widest island, with a gap.
            float reach = 0f;
            foreach (ForgedIsland island in shown)
            {
                foreach (HexTile hex in island.Hexes)
                {
                    Vector3 at = HexMeshes.WorldPosition(hex.Q, hex.R);
                    reach = Mathf.Max(reach, new Vector2(at.x, at.z).magnitude);
                }
            }

            float cell = reach * 2f + 4f;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(shown.Count));
            for (int index = 0; index < shown.Count; index += 1)
            {
                Vector3 offset = new Vector3(index % columns * cell, 0f, -(index / columns) * cell);
                GameObject holder = new GameObject(shown[index].Name);
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = offset;
                IslandView view = holder.AddComponent<IslandView>();
                view.SurfaceMaterial = surfaceMaterial;
                view.BuildSpeed = 0f;
                view.Render(new Player { Id = "preview" + index, Hexes = shown[index].Hexes }, null);
                _views.Add(view);
                _anchors.Add(holder.transform.position + Vector3.up * 2.2f + Vector3.forward * (reach * 0.6f));

                int number = index;
                Label tag = new Label(shown[index].Name);
                tag.AddToClassList("generator-tag");
                tag.RegisterCallback<ClickEvent>(_ => Select(number));
                _tagLayer.Add(tag);
                _tags.Add(tag);
            }

            if (cameraRig != null)
            {
                CameraRig.Orbit orbit = cameraRig.Current;
                int rows = Mathf.CeilToInt(shown.Count / (float)columns);
                orbit.Target = new Vector3((columns - 1) * cell * 0.5f, 0f, -(rows - 1) * cell * 0.5f);
                orbit.Distance = Mathf.Max(columns, rows) * cell * 0.8f + 4f;
                orbit.MinDistance = 3f;
                orbit.MaxDistance = 400f;
                orbit.PanLimit = 1000f;
                cameraRig.Current = orbit;
            }
        }

        private void PlaceTags()
        {
            if (worldCamera == null || _root == null || _root.panel == null)
            {
                return;
            }

            for (int index = 0; index < _tags.Count && index < _anchors.Count; index += 1)
            {
                bool isAhead = worldCamera.WorldToViewportPoint(_anchors[index]).z > 0f;
                _tags[index].style.display = isAhead ? DisplayStyle.Flex : DisplayStyle.None;
                if (isAhead)
                {
                    Vector2 at = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, _anchors[index], worldCamera);
                    _tags[index].style.left = at.x;
                    _tags[index].style.top = at.y;
                }

                _tags[index].EnableInClassList("selected", index == _selected);
            }
        }

        /// <summary>Picks an island: its numbers show in the panel, and the camera flies to it.</summary>
        private void Select(int index)
        {
            _selected = index;
            ShowDetails();
            if (cameraRig != null && index < _views.Count)
            {
                CameraRig.Orbit orbit = cameraRig.Current;
                orbit.Target = _views[index].transform.position;
                orbit.Distance = 16f;
                cameraRig.Current = orbit;
            }
        }

        private void ShowDetails()
        {
            if (_details == null)
            {
                return;
            }

            if (_islands.Count == 0)
            {
                _details.text = "Нет островов: добавьте тип и задайте количество.";

                return;
            }

            ForgedIsland island = _islands[_selected];
            int[] counts = island.BiomeCounts();
            IEnumerable<string> biomes = Biomes.All.Where(biome => counts[(int)biome.Id] > 0)
                .OrderByDescending(biome => counts[(int)biome.Id])
                .Select(biome => biome.Label + " " + counts[(int)biome.Id]);
            int built = island.Hexes.Count(hex => hex.Building.HasValue);
            double toxicity = island.Hexes.Count > 0 ? island.Hexes.Average(hex => hex.Toxicity) : 0;
            _details.text = island.Name + ": " + island.Hexes.Count + " гексов, зданий " + built + ", токсичность в среднем " + toxicity.ToString("0") + "%\n" + string.Join(" · ", biomes);
        }

        private void SetStatus(string text, bool isError)
        {
            if (_status != null)
            {
                _status.text = text;
                _status.EnableInClassList("error", isError);
            }
        }

        /// <summary>A setting changed: rebuild soon, once the typing has stopped.</summary>
        private void Changed()
        {
            if (_autoRebuild)
            {
                _rebuildAt = Time.unscaledTime + RebuildDelay;
            }
        }

        /* ---------- the panel ---------- */

        private void BuildPanel()
        {
            _root = document.rootVisualElement;
            _root.Clear();
            if (styleSheet != null)
            {
                _root.styleSheets.Add(styleSheet);
            }

            _root.AddToClassList("generator");
            _tagLayer = Add(_root, "generator-tags");
            _tagLayer.pickingMode = PickingMode.Ignore;

            _panel = Add(_root, "generator-panel");
            Text(_panel, "Генератор островов", "generator-title");
            Text(_panel, "Не часть игры: здесь собираются наборы островов, которые игра потом загружает из файла.", "generator-note");

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("generator-scroll");
            _panel.Add(scroll);

            VisualElement set = Section(scroll, "Набор");
            TextField seed = new TextField("Сид мира") { value = _spec.Seed };
            seed.RegisterValueChangedCallback(change => { _spec.Seed = change.newValue; Changed(); });
            set.Add(seed);
            VisualElement seedRow = Add(set, "generator-row");
            Button("Случайный сид", seedRow, () =>
            {
                _spec.Seed = "seed-" + UnityEngine.Random.Range(1000, 99999);
                seed.SetValueWithoutNotify(_spec.Seed);
                Rebuild();
            });
            Toggle auto = new Toggle("Перестраивать сразу") { value = _autoRebuild };
            auto.RegisterValueChangedCallback(change => _autoRebuild = change.newValue);
            seedRow.Add(auto);

            _recipes = Add(scroll, "generator-recipes");
            RebuildRecipes();
            Button("+ Добавить тип острова", scroll, () =>
            {
                _spec.Recipes.Add(new IslandRecipe { Name = "Тип " + (_spec.Recipes.Count + 1) });
                RebuildRecipes();
                Changed();
            }).AddToClassList("generator-add");

            VisualElement file = Section(_panel, "Файл");
            TextField name = new TextField("Имя файла") { value = _fileName };
            name.RegisterValueChangedCallback(change => _fileName = change.newValue);
            file.Add(name);
            VisualElement fileRow = Add(file, "generator-row");
            Button("Сгенерировать", fileRow, Rebuild).AddToClassList("primary");
            Button("Сохранить", fileRow, Save).AddToClassList("primary");
            VisualElement openRow = Add(file, "generator-row");
            _files = new DropdownField("Открыть", new List<string>(), 0);
            _files.AddToClassList("generator-files");
            openRow.Add(_files);
            Button("Открыть", openRow, () => Open(_files.value));
            RefreshFiles();

            _status = Text(_panel, "", "generator-status");
            _details = Text(_panel, "", "generator-details");
        }

        private void RebuildRecipes()
        {
            _recipes.Clear();
            for (int index = 0; index < _spec.Recipes.Count; index += 1)
            {
                RecipeCard(_spec.Recipes[index], index);
            }
        }

        /// <summary>The settings of one island type.</summary>
        private void RecipeCard(IslandRecipe recipe, int index)
        {
            Foldout card = new Foldout { text = (string.IsNullOrWhiteSpace(recipe.Name) ? "Тип" : recipe.Name) + " × " + recipe.Count, value = true };
            card.AddToClassList("generator-card");
            _recipes.Add(card);

            TextField name = new TextField("Название") { value = recipe.Name };
            name.RegisterValueChangedCallback(change => { recipe.Name = change.newValue; card.text = recipe.Name + " × " + recipe.Count; Changed(); });
            card.Add(name);

            IntegerField count = new IntegerField("Островов этого типа") { value = recipe.Count };
            count.RegisterValueChangedCallback(change =>
            {
                recipe.Count = Mathf.Clamp(change.newValue, 0, 200);
                card.text = recipe.Name + " × " + recipe.Count;
                Changed();
            });
            card.Add(count);

            DropdownField mode = new DropdownField("Биомы задаются", new List<string> { "Долями (вес)", "Точным числом гексов" }, recipe.Mode == BiomeAmountMode.Count ? 1 : 0);
            card.Add(mode);

            IntegerField size = new IntegerField("Размер, гексов (" + IslandRecipe.MinSize + "–" + IslandRecipe.MaxSize + ")") { value = recipe.Size };
            size.RegisterValueChangedCallback(change => { recipe.Size = Mathf.Clamp(change.newValue, IslandRecipe.MinSize, IslandRecipe.MaxSize); Changed(); });
            card.Add(size);
            Label sizeNote = Text(card, "", "generator-hint");

            Slider compact = new Slider("Форма: рваная ↔ круглая", 0f, 1f) { value = (float)recipe.Compactness, showInputField = true };
            compact.RegisterValueChangedCallback(change => { recipe.Compactness = change.newValue; Changed(); });
            card.Add(compact);

            Slider clump = new Slider("Биомы: россыпью ↔ областями", 0f, 1f) { value = (float)recipe.Clumping, showInputField = true };
            clump.RegisterValueChangedCallback(change => { recipe.Clumping = change.newValue; Changed(); });
            card.Add(clump);

            MinMaxSlider toxicity = new MinMaxSlider("Токсичность, %", recipe.ToxicityMin, recipe.ToxicityMax, 0, Tax.DeadToxicityPct);
            Label toxicityValue = Text(card, "", "generator-hint");
            toxicity.RegisterValueChangedCallback(change =>
            {
                recipe.ToxicityMin = Mathf.RoundToInt(change.newValue.x);
                recipe.ToxicityMax = Mathf.RoundToInt(change.newValue.y);
                toxicityValue.text = "Токсичность гексов: " + recipe.ToxicityMin + "–" + recipe.ToxicityMax + "%";
                Changed();
            });
            card.Add(toxicity);
            toxicityValue.text = "Токсичность гексов: " + recipe.ToxicityMin + "–" + recipe.ToxicityMax + "%";
            card.Add(toxicityValue);

            Slider buildings = new Slider("Уже построено, доля гексов", 0f, 1f) { value = (float)recipe.BuildingChance, showInputField = true };
            buildings.RegisterValueChangedCallback(change => { recipe.BuildingChance = change.newValue; Changed(); });
            card.Add(buildings);

            Toggle buildable = new Toggle("Гарантировать рудник, лес и ферму") { value = recipe.RequireBuildable };
            buildable.RegisterValueChangedCallback(change => { recipe.RequireBuildable = change.newValue; Changed(); });
            card.Add(buildable);

            Text(card, "Биомы", "generator-subtitle");
            VisualElement grid = Add(card, "generator-biomes");
            List<IntegerField> amounts = new List<IntegerField>();
            foreach (Biome biome in Biomes.All)
            {
                VisualElement row = Add(grid, "generator-biome");
                VisualElement swatch = Add(row, "generator-swatch");
                swatch.style.backgroundColor = Props.Hex(biome.Color);
                IntegerField amount = new IntegerField(biome.Label) { value = recipe.Biomes[(int)biome.Id] };
                amount.AddToClassList("generator-amount");
                BiomeId id = biome.Id;
                amount.RegisterValueChangedCallback(change =>
                {
                    recipe.Biomes[(int)id] = Mathf.Max(0, change.newValue);
                    UpdateSizeNote();
                    Changed();
                });
                row.Add(amount);
                amounts.Add(amount);
            }

            VisualElement tools = Add(card, "generator-row");
            Button("Как в игре", tools, () => SetAmounts(IslandRecipe.DefaultWeights()));
            Button("Обнулить", tools, () => SetAmounts(new int[Biomes.All.Count]));
            Button("Копия", tools, () =>
            {
                IslandRecipe copy = recipe.Clone();
                copy.Name = recipe.Name + " (копия)";
                _spec.Recipes.Insert(index + 1, copy);
                RebuildRecipes();
                Changed();
            });
            Button("Удалить тип", tools, () =>
            {
                _spec.Recipes.Remove(recipe);
                RebuildRecipes();
                Changed();
            }).AddToClassList("danger");

            void SetAmounts(int[] values)
            {
                recipe.Biomes = (int[])values.Clone();
                for (int biome = 0; biome < amounts.Count; biome += 1)
                {
                    amounts[biome].SetValueWithoutNotify(recipe.Biomes[biome]);
                }

                UpdateSizeNote();
                Changed();
            }

            void UpdateSizeNote()
            {
                bool isCount = recipe.Mode == BiomeAmountMode.Count;
                size.SetEnabled(!isCount);
                int sum = recipe.Biomes.Where(value => value > 0).Sum();
                sizeNote.text = isCount
                    ? "Размер = сумма чисел биомов: " + recipe.IslandSize + (sum > IslandRecipe.MaxSize ? " (обрезано до " + IslandRecipe.MaxSize + ")" : "")
                    : "Числа биомов — веса: остров делится между биомами пропорционально";
            }

            mode.RegisterValueChangedCallback(change =>
            {
                recipe.Mode = mode.index == 1 ? BiomeAmountMode.Count : BiomeAmountMode.Share;
                UpdateSizeNote();
                Changed();
            });
            UpdateSizeNote();
        }

        /* ---------- files ---------- */

        private void Save()
        {
            try
            {
                string path = IslandSetFile.Save(_fileName, _spec, _islands);
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
                SetStatus("Сохранено: " + path.Replace(System.IO.Path.DirectorySeparatorChar, '/'), false);
                RefreshFiles();
            }
            catch (Exception exception)
            {
                SetStatus("Не удалось сохранить: " + exception.Message, true);
            }
        }

        private void Open(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            try
            {
                IslandSetFile.Load(name, out IslandSetSpec spec, out List<ForgedIsland> _);
                _spec.Seed = spec.Seed;
                _spec.Recipes.Clear();
                _spec.Recipes.AddRange(spec.Recipes);
                _fileName = name;
                // The panel is rebuilt so every field shows the opened settings.
                BuildPanel();
                Rebuild();
                SetStatus("Открыт набор «" + name + "»", false);
            }
            catch (Exception exception)
            {
                SetStatus("Не удалось открыть: " + exception.Message, true);
            }
        }

        private void RefreshFiles()
        {
            List<string> names = IslandSetFile.List();
            _files.choices = names;
            _files.SetValueWithoutNotify(names.Contains(_fileName) ? _fileName : names.FirstOrDefault() ?? "");
        }

        /* ---------- helpers ---------- */

        private static VisualElement Section(VisualElement parent, string title)
        {
            VisualElement section = Add(parent, "generator-section");
            Text(section, title, "generator-subtitle");

            return section;
        }

        private static VisualElement Add(VisualElement parent, string className)
        {
            VisualElement element = new VisualElement();
            element.AddToClassList(className);
            parent.Add(element);

            return element;
        }

        private static Label Text(VisualElement parent, string text, string className)
        {
            Label label = new Label(text);
            label.AddToClassList(className);
            parent.Add(label);

            return label;
        }

        private static Button Button(string text, VisualElement parent, Action run)
        {
            Button button = new Button(run) { text = text };
            button.AddToClassList("generator-button");
            parent.Add(button);

            return button;
        }
    }
}
