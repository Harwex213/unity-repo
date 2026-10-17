using System;
using System.Collections.Generic;
using Hexwex.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hexwex.View
{
    /// <summary>
    /// The frame of the HUD, laid out as the prototype's island page lays it out
    /// (<c>ui/components</c>): the players in the upper left, the turn in the
    /// middle of the top edge, the island's toxicity as a flask on the right, the
    /// resources and the building bar in the lower left, and the end-turn wheel in
    /// the lower right. What a hex holds floats over that hex, as the prototype's
    /// tooltip does; the world map and the battle keep it at the right edge.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The prototype's two resource panels, two rows each. Toxicity has its flask instead.</summary>
        private static readonly ResourceId[][][] ResourceLayout =
        {
            new[] { new[] { ResourceId.Food, ResourceId.Wood, ResourceId.Stone }, new[] { ResourceId.Population, ResourceId.Mad, ResourceId.Hammers } },
            new[] { new[] { ResourceId.Power, ResourceId.Scouting }, new[] { ResourceId.Science, ResourceId.Mana } },
        };

        /// <summary>The four phases of the wheel, in the order of the turn: an icon and a short name each.</summary>
        private static readonly string[] PhaseIcons = { "hammers", "food", "scouting", "army" };
        private static readonly string[] PhaseNames = { "Строительство", "Налоги", "Разведка", "Зачистка" };

        private VisualElement _turnPanel;
        private VisualElement _topRight;
        private VisualElement _dock;
        private VisualElement _buildRow;
        private VisualElement _endTurn;
        private VisualElement _cardHint;
        private VisualElement _hintOwner;

        private void BuildFrame()
        {
            VisualElement frame = Layer("frame");
            _players = El("players panel", frame);
            _turnPanel = El("turn-panel panel", frame);
            _turnPanel.pickingMode = PickingMode.Ignore;
            _topRight = El("top-right", frame);
            _topRight.pickingMode = PickingMode.Ignore;
            _status = El("toxic-panel panel", frame);
            _hexPanel = El("hex-panel panel", frame);
            _dock = El("dock", frame);
            _dock.pickingMode = PickingMode.Ignore;
            _resources = El("resources", _dock);
            _resources.pickingMode = PickingMode.Ignore;
            _buildRow = El("build-row", _dock);
            _buildRow.pickingMode = PickingMode.Ignore;
            _endTurn = El("end-turn", frame);
            _bottom = El("bottom-bar panel", frame);

            _cardHint = El("card-hint panel", _root);
            _cardHint.pickingMode = PickingMode.Ignore;
            _cardHint.style.display = DisplayStyle.None;
        }

        /* ---------- resources ---------- */

        private void RefreshResources()
        {
            _resources.Clear();
            _chipByResource.Clear();
            _resources.style.display = _game.ShowBattle ? DisplayStyle.None : DisplayStyle.Flex;
            Player player = Session.Human;
            TaxPlan plan = Session.HumanTaxPlan;

            foreach (ResourceId[][] rows in ResourceLayout)
            {
                VisualElement panel = El("resource-panel panel", _resources);
                foreach (ResourceId[] row in rows)
                {
                    VisualElement line = El("resource-row", panel);
                    foreach (ResourceId id in row)
                    {
                        ResourceInfo info = ResourceTable.Get(id);
                        VisualElement cell = El("resource", line);
                        _chipByResource[id] = Icon(info.Icon, "resource-icon", cell);

                        // While the collection plays, the counters grow as the motes land.
                        int amount = player.Resources[id] + Landed(id);
                        // In the tax phase the power shown is what is still free to spend.
                        string text = id == ResourceId.Power && plan != null ? plan.PowerLeft(player) + "/" + amount : amount.ToString();
                        Text(text, info.Kind == ResourceKind.Negative ? "resource-amount negative" : "resource-amount", cell);
                        Hint(cell, info.Label, new[] { info.Feeds });
                    }
                }
            }
        }

        /* ---------- the turn, the page switch and the flask ---------- */

        private void RefreshTurn()
        {
            _turnPanel.Clear();
            _turnPanel.style.display = _game.ShowBattle ? DisplayStyle.None : DisplayStyle.Flex;
            string phase = Session.Stage == GameStage.Setup ? "Расстановка"
                : Session.Phase == Phase.Build ? "Фаза строительства"
                : Session.Phase == Phase.Tax ? "Фаза сбора налогов"
                : Session.Phase == Phase.Scout ? "Фаза разведки"
                : "Фаза зачистки";
            Text("Ход " + Session.Turn, "turn-number", _turnPanel);
            Text(phase, "turn-phase", _turnPanel);
        }

        private void RefreshTopRight()
        {
            _topRight.Clear();
            if (_game.ShowBattle || Session.Stage != GameStage.Play)
            {
                return;
            }

            Button page = Btn(_game.ShowWorld ? "К острову" : "Карта мира", "pill-button", _game.ToggleWorld, _topRight);
            page.SetEnabled(_game.CanSwitchPage);
        }

        /// <summary>
        /// The island's toxicity as the prototype's flask: a tall glass that fills
        /// from the bottom, with a tick at the foot of every zone, the count under
        /// it and the zone's name. The motes of the tax phase fly into it.
        /// </summary>
        private void RefreshToxic()
        {
            _status.Clear();
            bool isShown = !_game.ShowBattle && !_game.ShowWorld;
            _status.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
            _meterBar = null;
            if (!isShown)
            {
                return;
            }

            // The meter is the island's own reckoning: a rival's island shows the rival's.
            Player player = _game.ViewedPlayer;
            int level = ToxicSlot.MeterLevel(player.ToxicMeter);
            MeterZone zone = ToxicSlot.GetZone(level);

            VisualElement head = El("toxic-head", _status);
            Icon("toxicity", "toxic-icon", head);
            Text("Токсичность", "toxic-title", head);

            VisualElement flask = El("flask", _status);
            El("flask-neck", flask);
            VisualElement body = El("flask-body", flask);
            _meterBar = body;
            VisualElement fill = El("flask-fill level-" + level, body);
            fill.style.height = Length.Percent(100f * Mathf.Clamp01((float)player.ToxicMeter / ToxicSlot.MeterMax));
            foreach (MeterZone each in ToxicSlot.Zones)
            {
                if (each.From <= 0)
                {
                    continue;
                }

                VisualElement tick = El(player.ToxicMeter >= each.From ? "flask-tick passed" : "flask-tick", body);
                tick.style.bottom = Length.Percent(100f * each.From / ToxicSlot.MeterMax);
            }

            Text(player.ToxicMeter + " / " + ToxicSlot.MeterMax, "toxic-count", _status);
            Text(zone.Label, "toxic-status level-" + level, _status);
            Hint(_status, "Токсичность острова", new[] { zone.Status, "Каждый гекс с ядом добавляет её в конце фазы налогов." });
        }

        /* ---------- the building bar ---------- */

        /// <summary>The tools on the left and a card for every building, as the prototype's bar has them.</summary>
        private void RefreshBuildRow(bool isShown)
        {
            _buildRow.Clear();
            _buildRow.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isShown)
            {
                return;
            }

            VisualElement tools = El("tools panel", _buildRow);
            Tool(tools, "demolish", _game.DemolishMode, _game.ToggleDemolish, "Снести здание",
                _game.DemolishMode ? "Кликните по гексу со зданием. Токсичность гекса обнулится." : "Включает режим сноса: клик по гексу убирает с него здание.");
            Tool(tools, "poison", _game.SoilMode, _game.ToggleSoilCleanse, "Очистка почвы",
                _game.SoilMode ? "Выберите гекс, который будет уничтожен, затем гекс, который станет чистым." : "Отдать один гекс, чтобы очистить другой.");
            Tool(tools, "technology", false, () =>
            {
                _techsOpen = true;
                RefreshOverlay();
            }, "Технологии", "Наука копится в ресурсах и открывает технологии.");

            Player player = Session.Human;
            int discount = Session.Effects.StoneDiscount;
            VisualElement cards = El("cards panel", _buildRow);
            foreach (Building building in Buildings.All)
            {
                Building current = building;
                bool isLocked = !Buildings.IsUnlocked(player, building);
                Button card = new Button(() =>
                {
                    if (!isLocked)
                    {
                        _game.ArmBuilding(current.Id);
                    }
                });
                card.AddToClassList("card");
                card.EnableInClassList("armed", _game.ArmedBuilding == building.Id);
                card.EnableInClassList("poor", !Buildings.CanAfford(player.Resources, building, discount));
                card.EnableInClassList("locked", isLocked);
                card.EnableInClassList("trophy", building.Trophy);
                cards.Add(card);
                Icon(building.ArtName, "card-icon", card);
                Text(building.Label, "card-label", card);
                CardHint(card, building, player, discount, isLocked);
            }
        }

        private void Tool(VisualElement parent, string icon, bool isActive, Action onClick, string title, string text)
        {
            Button tool = new Button(onClick);
            tool.AddToClassList("tool");
            tool.EnableInClassList("active", isActive);
            parent.Add(tool);
            Icon(icon, "tool-icon", tool);
            Hint(tool, title, new[] { text });
        }

        /// <summary>The card's hover card: what the building gives, what it costs, the faces of its die.</summary>
        private void CardHint(VisualElement card, Building building, Player player, int discount, bool isLocked)
        {
            card.RegisterCallback<PointerEnterEvent>(_ =>
            {
                VisualElement hint = OpenHint(card, building.Label);
                ResourceInfo yields = ResourceTable.Get(building.Yields);

                VisualElement gives = El("hint-row", hint);
                Text("Даёт:", "hint-text", gives);
                Icon(yields.Icon, "hint-icon", gives);
                Text(yields.Label, "hint-text", gives);

                BuildCost cost = Buildings.EffectiveCost(building, discount);
                VisualElement price = El("hint-row", hint);
                Text("Цена:", "hint-text", price);
                HintCost(price, "stone", cost.Stone, player.Resources[ResourceId.Stone]);
                HintCost(price, "wood", cost.Wood, player.Resources[ResourceId.Wood]);
                HintCost(price, "hammers", cost.Hammers, player.Resources[ResourceId.Hammers]);

                VisualElement faces = El("hint-faces", hint);
                foreach (Face face in building.BaseFaces)
                {
                    VisualElement chip = El("hint-face", faces);
                    Icon(ResourceTable.Get(face.Resource).Icon, "hint-icon", chip);
                    Text(face.Amount.ToString(), "hint-text", chip);
                    Icon("toxicity", "hint-icon", chip);
                    Text(face.Toxicity.ToString(), "hint-text toxic", chip);
                }

                string note = building.Trophy
                    ? isLocked ? "Нужна технология «Центральная конверсия»: найдите и победите босса на карте мира" : "Отключает токсичность всех ваших зданий. Построить его — победить"
                    : "Биом острова добавляет зданию ещё одну грань";
                Text(note, "hint-note", hint);
            });
            card.RegisterCallback<PointerLeaveEvent>(_ => CloseHint(card));
        }

        private void HintCost(VisualElement row, string icon, int amount, int have)
        {
            VisualElement part = El(have >= amount ? "hint-cost" : "hint-cost short", row);
            Icon(icon, "hint-icon", part);
            Text(amount.ToString(), "hint-text", part);
        }

        /* ---------- hover cards ---------- */

        /// <summary>A hover card with a title and lines of text, shown over <paramref name="owner"/>.</summary>
        private void Hint(VisualElement owner, string title, IEnumerable<string> lines)
        {
            owner.RegisterCallback<PointerEnterEvent>(_ =>
            {
                VisualElement hint = OpenHint(owner, title);
                foreach (string line in lines)
                {
                    Text(line, "hint-text", El("hint-row", hint));
                }
            });
            owner.RegisterCallback<PointerLeaveEvent>(_ => CloseHint(owner));
        }

        private VisualElement OpenHint(VisualElement owner, string title)
        {
            _hintOwner = owner;
            _cardHint.Clear();
            _cardHint.style.display = DisplayStyle.Flex;
            // It is placed once it knows its own size: above the owner, kept on the screen.
            _cardHint.style.visibility = Visibility.Hidden;
            _cardHint.schedule.Execute(PlaceHint).StartingIn(0);
            Text(title, "hint-title", _cardHint);

            return _cardHint;
        }

        private void CloseHint(VisualElement owner)
        {
            if (_hintOwner == owner)
            {
                _hintOwner = null;
                _cardHint.style.display = DisplayStyle.None;
            }
        }

        private void PlaceHint()
        {
            if (_hintOwner == null || _hintOwner.panel == null)
            {
                _cardHint.style.display = DisplayStyle.None;

                return;
            }

            Rect owner = _root.WorldToLocal(_hintOwner.worldBound);
            Rect own = _cardHint.layout;
            Rect screen = _root.layout;
            float left = Mathf.Clamp(owner.center.x - own.width / 2f, 8f, Mathf.Max(8f, screen.width - own.width - 8f));
            // Above the owner, unless there is no room there; then below it.
            float top = owner.yMin - own.height - 10f;
            if (top < 8f)
            {
                top = owner.yMax + 10f;
            }

            _cardHint.style.left = left;
            _cardHint.style.top = top;
            _cardHint.style.visibility = Visibility.Visible;
        }

        /* ---------- the end-turn wheel ---------- */

        /// <summary>
        /// The prototype's end-turn wheel: the four phases round a disc, the one in
        /// play always at the top, under a banner that ends it. In the tax phase the
        /// banner collects the dice.
        /// </summary>
        private void RefreshEndTurn()
        {
            _endTurn.Clear();
            bool isShown = Session.Stage == GameStage.Play && Session.Outcome == null && !_game.ShowBattle && !_game.IsReadonly && Session.Phase != Phase.Clear;
            _endTurn.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isShown)
            {
                return;
            }

            int current = (int)Session.Phase;
            string banner = _game.Collecting ? "Сбор…" : Session.IsTaxOpen ? "Собрать" : "Готов";
            VisualElement panel = El("end-turn-panel panel", _endTurn);
            Button button = Btn(banner, "end-banner", _game.EndPhase, panel);
            button.SetEnabled(!_game.Collecting);

            PhaseWheel wheel = new PhaseWheel();
            wheel.AddToClassList("wheel");
            wheel.RegisterCallback<ClickEvent>(_ =>
            {
                if (!_game.Collecting)
                {
                    _game.EndPhase();
                }
            });
            panel.Add(wheel);
            for (int index = 0; index < PhaseIcons.Length; index += 1)
            {
                // The phase in play stands at the top; the rest follow it round clockwise.
                float angle = (index - current + 4) % 4 * Mathf.PI * 0.5f;
                VisualElement icon = Icon(PhaseIcons[index], index == current ? "wheel-icon active" : "wheel-icon", wheel);
                icon.style.left = Length.Percent(50f + 30f * Mathf.Sin(angle));
                icon.style.top = Length.Percent(50f - 30f * Mathf.Cos(angle));
                icon.pickingMode = PickingMode.Ignore;
            }

            Text(PhaseNames[current], "wheel-caption", panel);
        }

        /// <summary>The disc of the wheel: four quarters, the top one lit, and a gold rim.</summary>
        private sealed class PhaseWheel : VisualElement
        {
            private static readonly Color Disc = new Color(0.05f, 0.08f, 0.14f, 0.94f);
            private static readonly Color Lit = new Color(0.89f, 0.75f, 0.41f, 0.32f);
            private static readonly Color Spoke = new Color(0.89f, 0.75f, 0.41f, 0.45f);
            private static readonly Color Rim = new Color(0.89f, 0.75f, 0.41f, 1f);

            public PhaseWheel()
            {
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                Vector2 center = contentRect.center;
                float radius = Mathf.Min(contentRect.width, contentRect.height) * 0.5f - 3f;

                painter.fillColor = Disc;
                painter.BeginPath();
                painter.Arc(center, radius, 0f, 360f);
                painter.Fill();

                // The quarter at the top, from up-left to up-right.
                painter.fillColor = Lit;
                painter.BeginPath();
                painter.MoveTo(center);
                painter.Arc(center, radius, 225f, 315f);
                painter.ClosePath();
                painter.Fill();

                painter.strokeColor = Spoke;
                painter.lineWidth = 2f;
                for (int index = 0; index < 4; index += 1)
                {
                    float angle = (45f + index * 90f) * Mathf.Deg2Rad;
                    painter.BeginPath();
                    painter.MoveTo(center);
                    painter.LineTo(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                    painter.Stroke();
                }

                painter.strokeColor = Rim;
                painter.lineWidth = 4f;
                painter.BeginPath();
                painter.Arc(center, radius, 0f, 360f);
                painter.Stroke();
            }
        }

        /* ---------- the hex card ---------- */

        /// <summary>
        /// On the island the hex card stands over the hex it is about, as the
        /// prototype's tooltip does. It follows the hex as the camera moves.
        /// </summary>
        private void PlaceHexPanel()
        {
            bool isFloating = !_game.ShowWorld && !_game.ShowBattle;
            _hexPanel.EnableInClassList("floating", isFloating);
            _hexPanel.style.right = isFloating ? new StyleLength(StyleKeyword.Auto) : new StyleLength(StyleKeyword.Null);
            if (!isFloating)
            {
                _hexPanel.style.left = StyleKeyword.Null;
                _hexPanel.style.top = StyleKeyword.Null;

                return;
            }

            string hexId = _game.SelectedHexId ?? _game.HoveredHexId;
            if (hexId == null || _hexPanel.style.display == DisplayStyle.None || !_game.IslandView.TryGetPlateAnchor(hexId, out Vector3 world))
            {
                return;
            }

            Camera worldCamera = _game.WorldCamera;
            if (worldCamera.WorldToViewportPoint(world).z <= 0f)
            {
                return;
            }

            Vector2 at = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, world, worldCamera);
            Rect own = _hexPanel.layout;
            Rect screen = _root.layout;
            float left = Mathf.Clamp(at.x - own.width / 2f, 8f, Mathf.Max(8f, screen.width - own.width - 8f));
            float top = Mathf.Clamp(at.y - own.height - 18f, 8f, Mathf.Max(8f, screen.height - own.height - 8f));
            _hexPanel.style.left = left;
            _hexPanel.style.top = top;
        }
    }
}
