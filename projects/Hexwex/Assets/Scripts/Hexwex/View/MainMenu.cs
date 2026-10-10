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
    /// The main menu (<c>ui/components/main-menu</c>), after the campaign screen
    /// of Warcraft III: the title block on the left, the list of entries on the
    /// right and the exit in the lower left corner. It covers the whole game, and
    /// the game takes no input while it is open.
    ///
    /// The prototype shows a painted picture behind the menu. Here the picture is
    /// the game itself: the island of the waiting session turns slowly under a
    /// dark shade. A start fades the menu out over the freshly created game.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        /// <summary>The open menu fades out over this time before the game takes the input.</summary>
        private const float FadeOutSeconds = 0.7f;

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
            _root.style.display = DisplayStyle.Flex;
            _root.RemoveFromClassList("leaving");
            CloseOverlay();
            SetActive(_activeIndex);
            _hud.SetVisible(false);
        }

        private void Update()
        {
            if (_state == MenuState.Leaving && Time.unscaledTime >= _closeAt)
            {
                _state = MenuState.Closed;
                _root.style.display = DisplayStyle.None;

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

            _layout = Add(_root, "main-menu__layout");

            VisualElement title = Add(_layout, "main-menu__title-block");
            VisualElement emblem = Add(title, "main-menu__emblem");
            Texture2D emblemTexture = Resources.Load<Texture2D>("Icons/menu-emblem");
            if (emblemTexture != null)
            {
                emblem.style.backgroundImage = new StyleBackground(emblemTexture);
            }

            Label(title, "Кампания управляющего", "main-menu__heading");
            Label(title, "Остров", "main-menu__title");
            Label(title, "Бремя отходов", "main-menu__subtitle");

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

            VisualElement nav = Add(_layout, "main-menu__nav");
            AddEntry(nav, "Пролог", "Обучение", "scouting", () => StartGame(true));
            AddEntry(nav, "Глава первая", "Новая игра", "stronghold", () => StartGame(false));
            AddEntry(nav, "Летопись", "Фракции", "factions", OpenFactions);
            AddEntry(nav, "Архив", "Технологии", "technology", OpenTechs);
            AddEntry(nav, "Эпилог", "Авторы", "population", OpenCredits);

            VisualElement footer = Add(_root, "main-menu__footer");
            Button exit = new Button(Exit) { text = "Выход" };
            exit.AddToClassList("main-menu-button");
            footer.Add(exit);

            _overlay = Add(_root, "overlay");
            _overlay.style.display = DisplayStyle.None;
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
