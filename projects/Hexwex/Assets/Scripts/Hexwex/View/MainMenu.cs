using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Hexwex.View
{
    /// <summary>
    /// The game has no difficulty setting yet. The menu keeps the choice as a
    /// preference, and no rule reads it.
    /// </summary>
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
    }

    /// <summary>
    /// The main menu (<c>ui/components/main-menu</c>), after the menus of
    /// Warcraft III: the logo on the left, and on the right a framed slab that
    /// hangs on two chains from the top of the screen, with the entries on it as
    /// gold-rimmed plates and the exit at its foot. It covers the whole game, and
    /// the game takes no input while it is open.
    ///
    /// Behind it is the prototype's painting of the castle in the clouds, alive
    /// as the prototype has it: clouds drift, the sun throws rays, banners sway,
    /// torches flicker, motes rise and the picture follows the pointer a little.
    /// It lies between two dark bands, as in a cutscene. A start fades the menu
    /// and the painting out over the freshly created game.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        /// <summary>The open menu fades out over this time before the game takes the input.</summary>
        private const float FadeOutSeconds = 0.7f;
        /// <summary>The painting fades in over this time, a little slower than the menu over it, as in the prototype.</summary>
        private const float BackdropFadeInSeconds = 1.2f;
        /// <summary>How fast the parallax catches up with the pointer, per second.</summary>
        private const float PointerEase = 3.6f;

        private static readonly int PointerId = Shader.PropertyToID("_Pointer");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private static readonly string[] DifficultyLabels = { "Лёгкая", "Средняя", "Сложная" };

        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet hudStyleSheet;
        [SerializeField] private StyleSheet menuStyleSheet;

        private readonly List<Button> _entryButtons = new List<Button>();
        private readonly List<Action> _entryRuns = new List<Action>();

        private GameRoot _game;
        private Hud _hud;
        private VisualElement _root;
        private VisualElement _layout;
        private VisualElement _overlay;
        private int _activeIndex = 1;
        private float _closeAt;
        private Action _rebuildOverlay;
        private Transform _backdrop;
        private Material _backdropMaterial;
        private Vector2 _pointer;
        private float _openedAt;

        private enum MenuState
        {
            Closed,
            Open,
            /// <summary>The fade-out after a start: the game is shown under the menu, and the menu still holds the input.</summary>
            Leaving,
        }

        private MenuState _state = MenuState.Closed;

        /// <summary>True while the menu covers the game or fades out over it: the game takes no input.</summary>
        public bool HoldsInput
        {
            get { return _state != MenuState.Closed; }
        }

        public bool IsOpen
        {
            get { return _state == MenuState.Open; }
        }

        public Difficulty Difficulty { get; private set; } = Difficulty.Normal;

        public void Bind(GameRoot game, Hud hud)
        {
            _game = game;
            _hud = hud;
            _root = document.rootVisualElement;
            _root.Clear();
            _root.styleSheets.Add(hudStyleSheet);
            _root.styleSheets.Add(menuStyleSheet);
            _root.AddToClassList("main-menu");
            _root.style.display = DisplayStyle.None;
            Build();
        }

        /// <summary>Opens the menu and closes everything the game had open on top of the page.</summary>
        public void Open()
        {
            _state = MenuState.Open;
            _openedAt = Time.unscaledTime;
            _root.style.display = DisplayStyle.Flex;
            _root.RemoveFromClassList("leaving");
            // The menu comes in: it fades up, the title slides in and the slab drops on its chains.
            _root.AddToClassList("entering");
            _root.schedule.Execute(() => _root.RemoveFromClassList("entering")).StartingIn(40);
            CloseOverlay();
            // With the learn guide on, the keyboard starts on the prologue, as the prototype's does.
            bool isGuideOn = _game != null && _game.LearnGuide != null && _game.LearnGuide.Guide.Enabled;
            SetActive(isGuideOn ? 0 : _activeIndex);
            _hud.SetVisible(false);
        }

        /// <summary>
        /// Draws the prototype's painted background, or keeps it out of the way.
        /// The painting and its life are a shader (<c>Hexwex/MenuBackdrop</c>) on a
        /// quad that fills the camera's view just past its near plane, so it covers
        /// the island turning behind. It fades in as the menu opens and out with it.
        /// </summary>
        private void UpdateBackdrop()
        {
            Camera view = Camera.main;
            bool isShown = _state != MenuState.Closed && view != null && (_backdrop != null || BuildBackdrop());
            if (!isShown)
            {
                if (_backdrop != null && _backdrop.gameObject.activeSelf)
                {
                    _backdrop.gameObject.SetActive(false);
                }

                return;
            }

            if (_backdrop.parent != view.transform)
            {
                _backdrop.SetParent(view.transform, false);
            }

            _backdrop.gameObject.SetActive(true);
            float distance = view.nearClipPlane + 0.02f;
            float tall = 2f * distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            _backdrop.localPosition = Vector3.forward * distance;
            _backdrop.localRotation = Quaternion.identity;
            _backdrop.localScale = new Vector3(tall * view.aspect, tall, 1f);

            // The parallax follows the pointer, eased. The prototype counts y down the screen.
            Mouse mouse = Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 at = mouse.position.ReadValue();
                // A pointer off the window says nothing about where the player looks: it counts as at the edge.
                Vector2 target = new Vector2(Mathf.Clamp(at.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(1f - at.y / Screen.height * 2f, -1f, 1f));
                _pointer += (target - _pointer) * (1f - Mathf.Exp(-PointerEase * Time.unscaledDeltaTime));
            }

            float now = Time.unscaledTime;
            float fade = _state == MenuState.Leaving
                ? Mathf.Clamp01((_closeAt - now) / FadeOutSeconds)
                : Mathf.Clamp01((now - _openedAt) / BackdropFadeInSeconds);
            _backdropMaterial.SetVector(PointerId, _pointer);
            _backdropMaterial.SetFloat(FadeId, fade);
        }

        private bool BuildBackdrop()
        {
            Shader shader = Resources.Load<Shader>("Shaders/HexwexMenuBackdrop");
            Texture2D art = Resources.Load<Texture2D>("Menu/menu-bg");
            Texture2D mask = Resources.Load<Texture2D>("Menu/menu-bg-mask");
            if (shader == null || art == null || mask == null)
            {
                return false;
            }

            _backdropMaterial = new Material(shader) { name = "M_MenuBackdrop" };
            _backdropMaterial.SetTexture("_Art", art);
            _backdropMaterial.SetTexture("_Mask", mask);
            _backdropMaterial.SetFloat("_ArtAspect", (float)art.width / art.height);

            Mesh quad = new Mesh { name = "Backdrop" };
            quad.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
            quad.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

            GameObject made = new GameObject("Menu backdrop");
            made.AddComponent<MeshFilter>().sharedMesh = quad;
            MeshRenderer renderer = made.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _backdropMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _backdrop = made.transform;
            _root.AddToClassList("painted");

            return true;
        }

        private void Update()
        {
            UpdateBackdrop();

            if (_state == MenuState.Leaving && Time.unscaledTime >= _closeAt)
            {
                _state = MenuState.Closed;
                _root.style.display = DisplayStyle.None;
                UpdateBackdrop();

                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (_state != MenuState.Open || keyboard == null)
            {
                return;
            }

            bool hasOverlay = _overlay.style.display == DisplayStyle.Flex;
            if (keyboard.escapeKey.wasPressedThisFrame && hasOverlay)
            {
                CloseOverlay();

                return;
            }

            if (hasOverlay)
            {
                return;
            }

            // The arrows (and Home/End) walk the list, Enter runs the highlighted entry.
            if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                SetActive(_activeIndex + 1);
            }
            else if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                SetActive(_activeIndex - 1);
            }
            else if (keyboard.homeKey.wasPressedThisFrame)
            {
                SetActive(0);
            }
            else if (keyboard.endKey.wasPressedThisFrame)
            {
                SetActive(_entryRuns.Count - 1);
            }
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                _entryRuns[_activeIndex]();
            }
        }

        private void Build()
        {
            VisualElement shade = Add(_root, "main-menu__shade");
            shade.pickingMode = PickingMode.Ignore;
            Add(_root, "main-menu__band main-menu__band--top").pickingMode = PickingMode.Ignore;
            VisualElement foot = Add(_root, "main-menu__band main-menu__band--bottom");
            foot.pickingMode = PickingMode.Ignore;
            Label(foot, "Hexwex · прототип", "main-menu__version");

            _layout = Add(_root, "main-menu__layout");

            VisualElement title = Add(_layout, "main-menu__title-block");
            Label(title, "Кампания управляющего", "main-menu__heading");
            SetIcon(Add(title, "main-menu__logo"), "logo");
            Label(title, "Остров · Бремя отходов", "main-menu__subtitle");

            VisualElement bar = Add(title, "main-menu-bar");
            Label(bar, "Сложность", "main-menu-bar__label");
            DropdownField difficulty = new DropdownField(new List<string>(DifficultyLabels), (int)Difficulty);
            difficulty.AddToClassList("main-menu-bar__select");
            difficulty.tooltip = "Пока не влияет на правила: выбор запоминается до конца сессии";
            difficulty.RegisterValueChangedCallback(change => Difficulty = (Difficulty)Mathf.Max(0, Array.IndexOf(DifficultyLabels, change.newValue)));
            bar.Add(difficulty);
            Button chronicle = new Button(OpenFactions) { tooltip = "Летопись фракций" };
            chronicle.AddToClassList("main-menu-bar__button");
            bar.Add(chronicle);
            SetIcon(Add(chronicle, "main-menu-bar__icon"), "factions");

            // The slab hangs from the top of the screen on two chains.
            VisualElement hang = Add(_layout, "main-menu__hang");
            VisualElement chains = Add(hang, "main-menu__chains");
            chains.pickingMode = PickingMode.Ignore;
            chains.Add(new Chain());
            chains.Add(new Chain());

            VisualElement panel = Add(hang, "main-menu__panel");
            VisualElement inner = Add(panel, "main-menu__panel-inner");
            foreach (string corner in new[] { "top-left", "top-right", "bottom-left", "bottom-right" })
            {
                VisualElement stud = new Stud();
                stud.AddToClassList("main-menu__stud");
                stud.AddToClassList("main-menu__stud--" + corner);
                inner.Add(stud);
            }

            Label(inner, "Главное меню", "main-menu__panel-title");
            Add(inner, "main-menu__rule");

            VisualElement nav = Add(inner, "main-menu__nav");
            AddEntry(nav, "Пролог", "Обучение", "scouting", () => StartGame(true));
            AddEntry(nav, "Глава первая", "Новая игра", "stronghold", () => StartGame(false));
            AddEntry(nav, "Летопись", "Фракции", "factions", OpenFactions);
            AddEntry(nav, "Архив", "Технологии", "technology", OpenTechs);
            AddEntry(nav, "Эпилог", "Авторы", "population", OpenCredits);

            Add(inner, "main-menu__rule");
            Button exit = new Button(Exit) { text = "Выход", tooltip = "Выйти из игры" };
            exit.AddToClassList("main-menu-button");
            exit.AddToClassList("main-menu-button--exit");
            inner.Add(exit);
            // The emblem sits astride the top of the frame, over everything on it.
            SetIcon(Add(panel, "main-menu__crest"), "logo-mark");
            Add(hang, "main-menu__hang-rest").pickingMode = PickingMode.Ignore;

            _overlay = Add(_root, "overlay");
            _overlay.style.display = DisplayStyle.None;
            // A click on the dark around a window closes it, as in the prototype.
            _overlay.RegisterCallback<PointerDownEvent>(down =>
            {
                if (down.target == _overlay)
                {
                    CloseOverlay();
                }
            });
        }

        /// <summary>A chain drawn link by link down the whole height of the element: a link seen flat, then one seen edge on.</summary>
        private sealed class Chain : VisualElement
        {
            private static readonly Color Iron = new Color(0.36f, 0.33f, 0.3f);
            private static readonly Color Shine = new Color(0.62f, 0.56f, 0.46f);
            private static readonly Color Dark = new Color(0.07f, 0.06f, 0.06f);

            public Chain()
            {
                AddToClassList("main-menu__chain");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                float middle = contentRect.width * 0.5f;
                float height = contentRect.height;
                const float linkLength = 30f;
                const float linkHalf = 8f;
                const float step = 21f;

                // Drawn from the bottom up, so the lowest link always meets the slab whole.
                int index = 0;
                for (float bottom = height + 6f; bottom > -linkLength; bottom -= step)
                {
                    float top = bottom - linkLength;
                    if (index % 2 == 0)
                    {
                        Link(painter, middle, top, bottom, linkHalf, Dark, 7f);
                        Link(painter, middle, top, bottom, linkHalf, Iron, 4.5f);
                        Link(painter, middle - 1f, top + 1f, bottom - 1f, linkHalf - 1f, Shine, 1.2f);
                    }
                    else
                    {
                        Bar(painter, middle, top + 3f, bottom - 3f, Dark, 8f);
                        Bar(painter, middle, top + 4f, bottom - 4f, Iron, 5f);
                        Bar(painter, middle - 1f, top + 5f, bottom - 5f, Shine, 1.2f);
                    }

                    index += 1;
                }
            }

            private static void Link(Painter2D painter, float x, float top, float bottom, float half, Color color, float width)
            {
                painter.strokeColor = color;
                painter.lineWidth = width;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x - half, top + half));
                painter.Arc(new Vector2(x, top + half), half, 180f, 360f);
                painter.LineTo(new Vector2(x + half, bottom - half));
                painter.Arc(new Vector2(x, bottom - half), half, 0f, 180f);
                painter.ClosePath();
                painter.Stroke();
            }

            private static void Bar(Painter2D painter, float x, float top, float bottom, Color color, float width)
            {
                painter.strokeColor = color;
                painter.lineWidth = width;
                painter.lineCap = LineCap.Round;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, top));
                painter.LineTo(new Vector2(x, bottom));
                painter.Stroke();
            }
        }

        /// <summary>A gold hexagonal stud, for the corners of the slab.</summary>
        private sealed class Stud : VisualElement
        {
            private static readonly Color Rim = new Color(0.35f, 0.25f, 0.08f);
            private static readonly Color Gold = new Color(0.89f, 0.72f, 0.36f);
            private static readonly Color Light = new Color(1f, 0.93f, 0.7f);

            public Stud()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                Vector2 center = contentRect.center;
                float radius = Mathf.Min(contentRect.width, contentRect.height) * 0.5f;
                Hexagon(painter, center, radius, Rim);
                Hexagon(painter, center, radius * 0.8f, Gold);
                Hexagon(painter, center + new Vector2(-radius * 0.12f, -radius * 0.14f), radius * 0.36f, Light);
            }

            private static void Hexagon(Painter2D painter, Vector2 center, float radius, Color color)
            {
                painter.fillColor = color;
                painter.BeginPath();
                for (int corner = 0; corner < 6; corner += 1)
                {
                    float angle = (corner * 60f - 90f) * Mathf.Deg2Rad;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (corner == 0)
                    {
                        painter.MoveTo(point);
                    }
                    else
                    {
                        painter.LineTo(point);
                    }
                }

                painter.ClosePath();
                painter.Fill();
            }
        }

        private void AddEntry(VisualElement parent, string label, string title, string icon, Action run)
        {
            int index = _entryRuns.Count;
            Button entry = new Button(run);
            entry.AddToClassList("main-menu-entry");
            entry.RegisterCallback<PointerEnterEvent>(_ => SetActive(index));
            parent.Add(entry);

            SetIcon(Add(Add(entry, "main-menu-entry__frame"), "main-menu-entry__icon"), icon);
            VisualElement text = Add(entry, "main-menu-entry__text");
            // The small gold line, like "Chapter One", over the large white one.
            Label(text, label, "main-menu-entry__label");
            Label(text, title, "main-menu-entry__title");

            _entryButtons.Add(entry);
            _entryRuns.Add(run);
        }

        private void SetActive(int index)
        {
            _activeIndex = (index + _entryRuns.Count) % _entryRuns.Count;
            for (int entry = 0; entry < _entryButtons.Count; entry += 1)
            {
                _entryButtons[entry].EnableInClassList("main-menu-entry--active", entry == _activeIndex);
            }
        }

        /// <summary>
        /// Starts a new game from the menu. The session is created again, and the
        /// menu fades out over it.
        /// </summary>
        private void StartGame(bool withGuide)
        {
            if (_state != MenuState.Open)
            {
                return;
            }

            CloseOverlay();
            _game.NewGame(_game.PendingNickname);
            _hud.SetVisible(true);
            if (withGuide && _game.LearnGuide != null)
            {
                // The intro card opens once the fade-out ends: the guide waits for the menu to close.
                _game.LearnGuide.Guide.Enable();
            }

            _state = MenuState.Leaving;
            _closeAt = Time.unscaledTime + FadeOutSeconds;
            _root.AddToClassList("leaving");
        }

        /// <summary>"Выход". A build quits; the Editor leaves Play mode.</summary>
        private void Exit()
        {
            if (_state != MenuState.Open)
            {
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OpenFactions()
        {
            ShowOverlay(() => _hud.BuildFactions(Add(_overlay, "modal wide"), CloseOverlay));
        }

        private void OpenTechs()
        {
            ShowOverlay(() => _hud.BuildTechs(Add(_overlay, "modal wide"), CloseOverlay, () => _rebuildOverlay?.Invoke()));
        }

        /// <summary>The credits of the "Эпилог" entry. The text is a placeholder, as in the prototype.</summary>
        private void OpenCredits()
        {
            ShowOverlay(() =>
            {
                VisualElement modal = Add(_overlay, "modal main-menu-credits");
                Label(modal, "Эпилог", "main-menu-entry__label");
                Label(modal, "Авторы", "modal-title");
                foreach (string role in new[] { "Замысел и правила", "Код прототипа", "Иллюстрации" })
                {
                    VisualElement row = Add(modal, "main-menu-credits__row");
                    Label(row, role, "main-menu-credits__role");
                    Label(row, "Команда «Острова»", "main-menu-credits__name");
                }

                Label(modal, "Текст-заглушка: список авторов появится позже.", "panel-text");
                Button close = new Button(CloseOverlay) { text = "Закрыть" };
                close.AddToClassList("main-menu-button");
                modal.Add(close);
            });
        }

        private void ShowOverlay(Action build)
        {
            if (_state != MenuState.Open)
            {
                return;
            }

            _rebuildOverlay = () =>
            {
                _overlay.Clear();
                build();
            };
            _overlay.style.display = DisplayStyle.Flex;
            _layout.SetEnabled(false);
            _rebuildOverlay();
        }

        private void CloseOverlay()
        {
            _rebuildOverlay = null;
            _overlay.Clear();
            _overlay.style.display = DisplayStyle.None;
            _layout.SetEnabled(true);
        }

        private static VisualElement Add(VisualElement parent, string classNames)
        {
            VisualElement element = new VisualElement();
            foreach (string name in classNames.Split(' '))
            {
                element.AddToClassList(name);
            }

            parent.Add(element);

            return element;
        }

        private static void Label(VisualElement parent, string text, string className)
        {
            Label label = new Label(text);
            label.AddToClassList(className);
            label.pickingMode = PickingMode.Ignore;
            parent.Add(label);
        }

        private static void SetIcon(VisualElement element, string iconName)
        {
            Texture2D texture = Resources.Load<Texture2D>("Icons/" + iconName);
            element.pickingMode = PickingMode.Ignore;
            if (texture != null)
            {
                element.style.backgroundImage = new StyleBackground(texture);
            }
        }
    }
}
