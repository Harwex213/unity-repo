using Hexwex.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Hexwex.View
{
    /// <summary>
    /// The learn guide on screen (<c>ui/components/learn-guide.tsx</c>), on top of
    /// every page. It shows nothing while it is closed, so the game gets its input
    /// back. Each frame it asks the game which card belongs to what is on screen,
    /// and the rules in <see cref="Guide"/> decide which card is open.
    ///
    /// A card stands next to the part of the HUD it talks about, on the side with
    /// more room, with a ring around that part. With no such part, or too little
    /// room, it stands in the middle of the screen.
    /// </summary>
    public sealed class LearnGuide : MonoBehaviour
    {
        private const float CardWidth = 600f;
        private const float ScreenEdge = 22f;
        /// <summary>The room between the card and its anchor.</summary>
        private const float AnchorGap = 20f;
        /// <summary>The ring sits a little outside the anchor.</summary>
        private const float RingPad = 8f;
        /// <summary>A card squeezed below this height goes to the middle of the screen instead.</summary>
        private const float MinCardHeight = 480f;
        private const float MaxCardHeight = 900f;
        /// <summary>The HUD is rebuilt and resized, so the anchor is measured again on this period.</summary>
        private const float MeasurePeriod = 0.2f;

        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet hudStyleSheet;
        [SerializeField] private StyleSheet guideStyleSheet;

        private readonly VisualElement[] _dims = new VisualElement[4];

        private GameRoot _game;
        private Hud _hud;
        private VisualElement _root;
        private VisualElement _ring;
        private VisualElement _card;
        private GuideStepId? _shown;
        private float _nextMeasure;

        public Guide Guide { get; } = new Guide();

        /// <summary>True while a card is on screen: the card owns the keyboard, and a battle under it waits.</summary>
        public bool IsOpen
        {
            get { return Guide.OpenStep.HasValue; }
        }

        public void Bind(GameRoot game, Hud hud)
        {
            _game = game;
            _hud = hud;
            _root = document.rootVisualElement;
            _root.Clear();
            _root.styleSheets.Add(hudStyleSheet);
            _root.styleSheets.Add(guideStyleSheet);
            _root.AddToClassList("learn-guide");
            _root.style.display = DisplayStyle.None;

            // Four shades around the ring darken everything but the anchor.
            for (int index = 0; index < _dims.Length; index += 1)
            {
                _dims[index] = new VisualElement();
                _dims[index].AddToClassList("learn-guide__dim");
                _root.Add(_dims[index]);
            }

            _ring = new VisualElement();
            _ring.AddToClassList("learn-guide__ring");
            _ring.pickingMode = PickingMode.Ignore;
            _root.Add(_ring);

            _card = new VisualElement();
            _card.AddToClassList("learn-guide__card");
            _root.Add(_card);
        }

        private void Update()
        {
            if (_root == null || _game == null || _game.Session == null)
            {
                return;
            }

            GuideStepId? context = _game.GuideContext();
            Guide.Reach(context);

            if (Guide.OpenStep.HasValue)
            {
                ReadKeys(context);
            }

            if (Guide.OpenStep != _shown)
            {
                _shown = Guide.OpenStep;
                _root.style.display = _shown.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
                if (_shown.HasValue)
                {
                    BuildCard(_shown.Value);
                    _nextMeasure = 0f;
                }
            }

            if (_shown.HasValue && Time.unscaledTime >= _nextMeasure)
            {
                _nextMeasure = Time.unscaledTime + MeasurePeriod;
                Place(_shown.Value);
            }
        }

        /// <summary>The card owns the keyboard while it is open: Esc skips, the arrows and Enter turn the cards.</summary>
        private void ReadKeys(GuideStepId? context)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Guide.Skip();
            }
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                Guide.Next(context);
            }
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
            {
                Guide.Previous();
            }
        }

        private void BuildCard(GuideStepId stepId)
        {
            GuideStep step = Guide.Get(stepId);
            int index = (int)stepId;
            bool isLast = index == Guide.Steps.Count - 1;
            _card.Clear();

            VisualElement head = Add(_card, "learn-guide__head");
            VisualElement progress = Add(head, "learn-guide__progress");
            for (int position = 0; position < Guide.Steps.Count; position += 1)
            {
                VisualElement segment = Add(progress, "learn-guide__segment");
                segment.EnableInClassList("learn-guide__segment--done", position < index);
                segment.EnableInClassList("learn-guide__segment--current", position == index);
            }

            Text(head, (index + 1) + "/" + Guide.Steps.Count, "learn-guide__counter");

            VisualElement art = Add(_card, "learn-guide__art");
            Texture2D texture = Resources.Load<Texture2D>("Tutorial/" + step.Art);
            if (texture != null)
            {
                art.style.backgroundImage = new StyleBackground(texture);
            }

            Text(_card, step.Title, "learn-guide__title");

            // A long text scrolls inside the card; the buttons stay in view.
            ScrollView body = new ScrollView();
            body.AddToClassList("learn-guide__body");
            _card.Add(body);
            Text(body, step.Text, "learn-guide__text");

            VisualElement buttons = Add(_card, "learn-guide__buttons");
            Button skip = new Button(Guide.Skip) { text = "Пропустить" };
            skip.AddToClassList("small-button");
            skip.AddToClassList("learn-guide__skip");
            buttons.Add(skip);

            Button back = new Button(Guide.Previous) { text = "Назад" };
            back.AddToClassList("small-button");
            back.SetEnabled(Guide.CanGoBack);
            buttons.Add(back);

            Button next = new Button(() => Guide.Next(_game.GuideContext())) { text = isLast ? "Понятно" : "Далее" };
            next.AddToClassList("end-button");
            buttons.Add(next);
        }

        /// <summary>
        /// The card goes on the side of the anchor that has more room. With no
        /// anchor, or too little room on both sides, it goes to the middle of the screen.
        /// </summary>
        private void Place(GuideStepId stepId)
        {
            float viewWidth = _root.resolvedStyle.width;
            float viewHeight = _root.resolvedStyle.height;
            if (float.IsNaN(viewWidth) || viewWidth <= 0f)
            {
                return;
            }

            VisualElement anchor = _hud.GuideAnchor(stepId);
            Rect box = anchor != null ? anchor.worldBound : default;
            bool hasAnchor = anchor != null && anchor.resolvedStyle.display != DisplayStyle.None
                && box.width > 0f && box.height > 0f && box.yMax > 0f && box.xMax > 0f && box.yMin < viewHeight && box.xMin < viewWidth;

            float width = Mathf.Min(CardWidth, viewWidth - ScreenEdge * 2f);
            float roomAbove = box.yMin - RingPad - AnchorGap - ScreenEdge;
            float roomBelow = viewHeight - box.yMax - RingPad - AnchorGap - ScreenEdge;
            float room = Mathf.Max(roomAbove, roomBelow);

            _card.style.width = width;

            if (!hasAnchor || room < MinCardHeight)
            {
                // The middle of the screen, over one shade.
                _ring.style.display = DisplayStyle.None;
                SetBox(_dims[0], 0f, 0f, viewWidth, viewHeight);
                for (int index = 1; index < _dims.Length; index += 1)
                {
                    _dims[index].style.display = DisplayStyle.None;
                }

                float height = Mathf.Min(MaxCardHeight, viewHeight - ScreenEdge * 2f);
                _card.style.maxHeight = height;
                _card.style.left = (viewWidth - width) * 0.5f;
                _card.style.top = StyleKeyword.Auto;
                _card.style.bottom = StyleKeyword.Auto;
                _card.style.top = Mathf.Max(ScreenEdge, (viewHeight - Mathf.Min(height, _card.resolvedStyle.height > 0f ? _card.resolvedStyle.height : height)) * 0.5f);

                return;
            }

            Rect ring = new Rect(box.xMin - RingPad, box.yMin - RingPad, box.width + RingPad * 2f, box.height + RingPad * 2f);
            _ring.style.display = DisplayStyle.Flex;
            SetBox(_ring, ring.xMin, ring.yMin, ring.width, ring.height);
            SetBox(_dims[0], 0f, 0f, viewWidth, Mathf.Max(0f, ring.yMin));
            SetBox(_dims[1], 0f, ring.yMax, viewWidth, Mathf.Max(0f, viewHeight - ring.yMax));
            SetBox(_dims[2], 0f, ring.yMin, Mathf.Max(0f, ring.xMin), ring.height);
            SetBox(_dims[3], ring.xMax, ring.yMin, Mathf.Max(0f, viewWidth - ring.xMax), ring.height);

            _card.style.maxHeight = Mathf.Min(room, MaxCardHeight);
            _card.style.left = Mathf.Clamp(box.center.x - width * 0.5f, ScreenEdge, viewWidth - ScreenEdge - width);
            if (roomAbove >= roomBelow)
            {
                _card.style.top = StyleKeyword.Auto;
                _card.style.bottom = viewHeight - box.yMin + RingPad + AnchorGap;
            }
            else
            {
                _card.style.bottom = StyleKeyword.Auto;
                _card.style.top = box.yMax + RingPad + AnchorGap;
            }
        }

        private static void SetBox(VisualElement element, float left, float top, float width, float height)
        {
            element.style.display = DisplayStyle.Flex;
            element.style.left = left;
            element.style.top = top;
            element.style.width = width;
            element.style.height = height;
        }

        private static VisualElement Add(VisualElement parent, string className)
        {
            VisualElement element = new VisualElement();
            element.AddToClassList(className);
            parent.Add(element);

            return element;
        }

        private static void Text(VisualElement parent, string text, string className)
        {
            Label label = new Label(text);
            label.AddToClassList(className);
            parent.Add(label);
        }
    }
}
