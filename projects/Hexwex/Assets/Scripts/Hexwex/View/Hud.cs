using System;
using System.Collections.Generic;
using System.Linq;
using Hexwex.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hexwex.View
{
    /// <summary>
    /// The HUD of the island scene, built in UI Toolkit. It stands where the
    /// prototype's React components stood, with the same contract: it reads the
    /// session, and it changes nothing except through the actions of
    /// <see cref="GameRoot"/> and the session. <see cref="Refresh"/> rebuilds the
    /// panels from the current state. The styles are in <c>Assets/UI/Hud.uss</c>.
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        private const float NoticeSeconds = 3.2f;

        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet styleSheet;

        private readonly Dictionary<string, Texture2D> _icons = new Dictionary<string, Texture2D>();
        private readonly List<KeyValuePair<string, VisualElement>> _plateByHex = new List<KeyValuePair<string, VisualElement>>();

        private GameRoot _game;
        private VisualElement _root;
        private VisualElement _resources;
        private VisualElement _status;
        private VisualElement _players;
        private VisualElement _hexPanel;
        private VisualElement _bottom;
        private VisualElement _plates;
        private VisualElement _overlay;
        private Label _notice;
        private float _noticeUntil;
        private bool _techsOpen;
        private bool _factionsOpen;
        private string _battleBottomKey;
        private Action _externalClose;
        private Action _externalRefresh;

        private GameSession Session
        {
            get { return _game.Session; }
        }

        public void Bind(GameRoot game)
        {
            _game = game;
            _root = document.rootVisualElement;
            _root.Clear();
            _root.styleSheets.Add(styleSheet);
            _root.AddToClassList("hud");
            _root.pickingMode = PickingMode.Ignore;

            _plates = Layer("plates");

            VisualElement top = El("top-bar", _root);
            _resources = El("resources", top);
            _status = El("status", top);

            VisualElement middle = Layer("middle");
            _players = El("players", middle);
            _hexPanel = El("hex-panel", middle);

            _bottom = El("bottom-bar", _root);

            _notice = new Label();
            _notice.AddToClassList("notice");
            _notice.pickingMode = PickingMode.Ignore;
            _notice.style.display = DisplayStyle.None;
            _root.Add(_notice);

            _overlay = El("overlay", _root);
            _overlay.style.display = DisplayStyle.None;
        }

        /// <summary>Hides the whole HUD, as while the main menu covers the game.</summary>
        public void SetVisible(bool isVisible)
        {
            if (_root != null)
            {
                _root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// The part of the HUD a card of the learn guide talks about, or <c>null</c>
        /// for a card that stands in the middle of the screen.
        /// </summary>
        public VisualElement GuideAnchor(GuideStepId stepId)
        {
            switch (stepId)
            {
                case GuideStepId.Build: return _bottom != null ? _bottom.Q(className: "cards") : null;
                case GuideStepId.Tax: return _resources;
                case GuideStepId.Scout: return _hexPanel;
                case GuideStepId.Battle: return _bottom != null ? _bottom.Q(className: "hint") : null;
                default: return null;
            }
        }

        /// <summary>The factions list, built into a modal that someone else owns: the main menu.</summary>
        public void BuildFactions(VisualElement modal, Action close)
        {
            _externalClose = close;
            AddFactions(modal);
            _externalClose = null;
        }

        /// <summary>The technology tree, built into a modal that someone else owns: the main menu.</summary>
        public void BuildTechs(VisualElement modal, Action close, Action refresh)
        {
            _externalClose = close;
            _externalRefresh = refresh;
            AddTechs(modal);
            _externalClose = null;
            _externalRefresh = null;
        }

        public void ShowNotice(string message)
        {
            if (_notice == null)
            {
                return;
            }

            _notice.text = message;
            _notice.style.display = DisplayStyle.Flex;
            _noticeUntil = Time.unscaledTime + NoticeSeconds;
        }

        /// <summary>Whether a HUD panel is under the pointer, so the click must not reach the island.</summary>
        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            IPanel panel = _root != null ? _root.panel : null;
            if (panel == null)
            {
                return false;
            }

            Vector2 flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            return panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, flipped)) != null;
        }

        public void Refresh()
        {
            if (_root == null || Session == null)
            {
                return;
            }

            RefreshResources();
            RefreshStatus();
            RefreshPlayers();
            RefreshHexPanel();
            RefreshBottom();
            RefreshPlates();
            RefreshOverlay();
        }

        private void Update()
        {
            if (_notice != null && _notice.style.display == DisplayStyle.Flex && Time.unscaledTime > _noticeUntil)
            {
                _notice.style.display = DisplayStyle.None;
            }
        }

        /// <summary>The plates follow their hexes as the camera moves.</summary>
        private void PlacePlates()
        {
            if (_plateByHex.Count == 0)
            {
                return;
            }

            Camera worldCamera = _game.WorldCamera;
            foreach (KeyValuePair<string, VisualElement> entry in _plateByHex)
            {
                if (!_game.IslandView.TryGetPlateAnchor(entry.Key, out Vector3 world) || worldCamera.WorldToViewportPoint(world).z <= 0f)
                {
                    entry.Value.style.display = DisplayStyle.None;

                    continue;
                }

                Vector2 position = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, world, worldCamera);
                entry.Value.style.display = DisplayStyle.Flex;
                entry.Value.style.left = position.x;
                entry.Value.style.top = position.y;
                ApplyPlatePop(entry.Key, entry.Value);
            }
        }

        private void RefreshResources()
        {
            _resources.Clear();
            _chipByResource.Clear();
            Player player = Session.Human;
            TaxPlan plan = Session.HumanTaxPlan;

            foreach (ResourceInfo info in ResourceTable.All)
            {
                VisualElement chip = El("chip", _resources);
                _chipByResource[info.Id] = chip;
                chip.tooltip = info.Label + ": " + info.Feeds;
                Icon(info.Icon, "chip-icon", chip);

                // While the collection plays, the counters grow as the motes land.
                int amount = player.Resources[info.Id] + Landed(info.Id);
                // In the tax phase the power shown is what is still free to spend.
                string text = info.Id == ResourceId.Power && plan != null ? plan.PowerLeft(player) + "/" + amount : amount.ToString();
                Text(text, info.Kind == ResourceKind.Negative ? "chip-value negative" : "chip-value", chip);
            }
        }

        private void RefreshStatus()
        {
            _status.Clear();
            // The meter is the island's own reckoning: a rival's island shows the rival's.
            Player player = _game.ViewedPlayer;
            MeterZone zone = ToxicSlot.GetZone(ToxicSlot.MeterLevel(player.ToxicMeter));

            VisualElement meter = El("meter", _status);
            VisualElement meterHead = El("row", meter);
            Icon("toxicity", "chip-icon", meterHead);
            Text(player.ToxicMeter + " / " + ToxicSlot.MeterMax + " · " + zone.Status, "meter-label", meterHead);
            VisualElement bar = El("meter-bar", meter);
            _meterBar = bar;
            VisualElement fill = El("meter-fill level-" + zone.Level, bar);
            fill.style.width = Length.Percent(100f * player.ToxicMeter / ToxicSlot.MeterMax);

            Button techs = Btn("Технологии", "small-button", () =>
            {
                _techsOpen = true;
                RefreshOverlay();
            }, _status);
            techs.SetEnabled(Session.Stage == GameStage.Play);

            Button page = Btn(_game.ShowWorld ? "К острову" : "Карта мира", "small-button", _game.ToggleWorld, _status);
            page.SetEnabled(_game.CanSwitchPage);

            string phase = Session.Stage == GameStage.Setup ? "Расстановка"
                : Session.Phase == Phase.Build ? "Фаза строительства"
                : Session.Phase == Phase.Tax ? "Фаза сбора налогов"
                : Session.Phase == Phase.Scout ? "Фаза разведки"
                : "Фаза зачистки";
            Text("Ход " + Session.Turn + " · " + phase, "turn", _status);
        }

        private void RefreshPlayers()
        {
            _players.Clear();

            // A click on a row opens that player's island, read-only. The player's own row comes back home.
            bool canVisit = _game.CanSwitchPage;
            foreach (Player player in Session.Players)
            {
                Player current = player;
                Button row = new Button(() => _game.ViewIsland(current.Id));
                row.AddToClassList("player");
                row.EnableInClassList("eliminated", player.Eliminated);
                row.EnableInClassList("viewed", canVisit && !_game.ShowWorld && player == _game.ViewedPlayer);
                row.tooltip = player.Eliminated ? "Выбыл из игры" : player.IsHuman ? "Ваш остров" : "Посмотреть остров";
                row.SetEnabled(canVisit);
                _players.Add(row);
                VisualElement swatch = El("swatch", row);
                swatch.style.backgroundColor = Props.Hex(player.Color);
                VisualElement body = El("player-body", row);
                Text(player.Nickname, "player-name", body);
                Text("зданий " + GameOver.BuildingCount(player) + " · армия " + player.Army + " · техн. " + player.Techs, "player-meta", body);
            }
        }

        /// <summary>What the hex under the pointer, or the selected hex, is and what it rolls.</summary>
        public void RefreshHexPanel()
        {
            if (_hexPanel == null || Session == null)
            {
                return;
            }

            _hexPanel.Clear();
            if (_game.ShowBattle)
            {
                RefreshBattlePanel();

                return;
            }

            if (_game.ShowWorld)
            {
                RefreshCellPanel();

                return;
            }

            Player player = _game.ViewedPlayer;
            HexTile hex = player.FindHex(_game.SelectedHexId ?? _game.HoveredHexId);
            if (hex == null)
            {
                _hexPanel.style.display = DisplayStyle.None;

                return;
            }

            _hexPanel.style.display = DisplayStyle.Flex;
            Biome biome = Biomes.Get(hex.Biome);
            Text(biome.Label, "panel-title", _hexPanel);
            Text(biome.Description, "panel-text", _hexPanel);

            string toxicity = "Токсичность: " + hex.Toxicity + "%";
            if (Tax.IsDead(hex))
            {
                toxicity += " — гекс мёртв";
            }
            else if (Tax.IsFoodBlocked(hex))
            {
                toxicity += " — еда не растёт";
            }

            Text(toxicity, hex.Toxicity > 0 ? "panel-text toxic" : "panel-text", _hexPanel);

            HexDie die = HexDie.On(player, hex);
            // A rival's island is for looking only: its dice are not the player's to turn.
            TaxRoll roll = _game.IsReadonly ? null : Session.HumanTaxPlan?.FindRoll(hex.Id);

            if (StructureHp.TryGet(player, hex, out StructureKind kind, out int hp, out int max))
            {
                string label = kind == StructureKind.Stronghold ? Stronghold.Label : Buildings.Get(hex.Building.Value).Label;
                VisualElement head = El("row", _hexPanel);
                Icon(kind == StructureKind.Stronghold ? Stronghold.ArtName : Buildings.Get(hex.Building.Value).ArtName, "card-icon", head);
                Text(label + (hp < max ? " · " + hp + "/" + max : ""), "panel-subtitle", head);

                if (die == null)
                {
                    Text("Руины: кубик не бросается, пока твердыня не отстроится.", "panel-text toxic", _hexPanel);
                }
            }

            if (roll != null && Session.IsTaxOpen)
            {
                Text("Грань можно сменить за власть:", "panel-text", _hexPanel);
                AddFaces(player, hex, roll.Faces, roll);
            }
            else if (die != null)
            {
                Text("Кубик (в среднем " + Buildings.AverageAmount(die.Faces).ToString("0.#") + "):", "panel-text", _hexPanel);
                AddFaces(player, hex, die.Faces, null);
            }
            else if (!hex.Building.HasValue && !_game.IsReadonly)
            {
                AddSuggestions(hex);
            }
        }

        private void AddFaces(Player player, HexTile hex, Face[] faces, TaxRoll roll)
        {
            for (int index = 0; index < faces.Length; index += 1)
            {
                int faceIndex = index;
                Face face = faces[index];
                Tax.FacePayout(face, hex, Session.Effects, out int amount, out int toxicity);

                string classes = roll != null ? "face" : "face static";
                string note = "";
                if (roll != null)
                {
                    int cost = TaxPlan.PickCost(roll, index);
                    note = index == roll.RolledIndex ? "выпало" : cost + " власти";
                    classes += index == roll.ChosenIndex ? " chosen" : "";
                    classes += Session.HumanTaxPlan.PickRefusal(player, hex.Id, index) != null ? " refused" : "";
                }

                Button row = new Button(() =>
                {
                    if (roll != null)
                    {
                        Session.PickTaxFace(hex.Id, faceIndex);
                    }
                });
                foreach (string name in classes.Split(' '))
                {
                    row.AddToClassList(name);
                }

                _hexPanel.Add(row);
                Icon(ResourceTable.Get(face.Resource).Icon, "face-icon", row);
                Text("+" + amount, "face-amount", row);
                if (toxicity > 0)
                {
                    Icon("toxicity", "face-icon", row);
                    Text("+" + toxicity + "%", "face-toxicity", row);
                }

                Text(note, "face-note", row);
            }
        }

        /// <summary>The buildings an empty hex can host, the richest die first.</summary>
        private void AddSuggestions(HexTile hex)
        {
            List<Building> allowed = Buildings.ForBiome(hex.Biome).OrderByDescending(building => Buildings.AverageYieldOn(building, hex.Biome)).ToList();
            if (allowed.Count == 0)
            {
                Text("Здесь ничего нельзя построить.", "panel-text", _hexPanel);

                return;
            }

            Text("Можно построить:", "panel-text", _hexPanel);
            foreach (Building building in allowed)
            {
                VisualElement row = El("row suggestion", _hexPanel);
                Icon(building.ArtName, "face-icon", row);
                Text(building.Label, "face-amount", row);
                Icon(ResourceTable.Get(building.Yields).Icon, "face-icon", row);
                Text("≈" + Buildings.AverageYieldOn(building, hex.Biome).ToString("0.#"), "face-note", row);
            }
        }

        private void RefreshBottom()
        {
            _bottom.Clear();
            _bottom.style.display = Session.Outcome == null ? DisplayStyle.Flex : DisplayStyle.None;

            if (_game.IsReadonly && !_game.ShowWorld && !_game.ShowBattle)
            {
                Text("Остров игрока " + _game.ViewedPlayer.Nickname + " — только просмотр", "hint", _bottom);
                Btn("Вернуться на свой остров", "end-button", () => _game.ViewIsland(null), _bottom);

                return;
            }

            if (Session.Stage == GameStage.Setup)
            {
                Text("Поставьте твердыню на свободный гекс своего острова.", "hint", _bottom);
                TextField seed = new TextField { value = _game.PendingNickname ?? "" };
                seed.AddToClassList("seed-field");
                seed.RegisterValueChangedCallback(change => _game.PendingNickname = change.newValue);
                _bottom.Add(seed);
                Btn("Новый остров", "small-button", () => _game.NewGame(_game.PendingNickname), _bottom);
                Btn("Начать", "end-button", _game.StartGame, _bottom).SetEnabled(Session.Human.StrongholdHexId != null);

                return;
            }

            if (_game.ShowBattle)
            {
                RefreshBattleBottom();

                return;
            }

            if (_game.ShowWorld)
            {
                RefreshWorldBottom();

                return;
            }

            if (Session.Phase == Phase.Scout)
            {
                Text("Фаза разведки идёт на карте мира.", "hint", _bottom);
                Btn("Закончить разведку", "end-button", _game.EndPhase, _bottom);

                return;
            }

            if (Session.Phase == Phase.Tax)
            {
                Text("Кубики брошены. Выберите здание, чтобы сменить грань за власть.", "hint", _bottom);
                Btn(_game.Collecting ? "Сбор…" : "Собрать налоги", "end-button", _game.EndPhase, _bottom).SetEnabled(!_game.Collecting);

                return;
            }

            Player player = Session.Human;
            int discount = Session.Effects.StoneDiscount;
            VisualElement cards = El("cards", _bottom);

            foreach (Building building in Buildings.All)
            {
                if (!Buildings.IsUnlocked(player, building))
                {
                    continue;
                }

                Building current = building;
                string classes = "card";
                classes += _game.ArmedBuilding == building.Id ? " armed" : "";
                classes += Buildings.CanAfford(player.Resources, building, discount) ? "" : " poor";

                Button card = new Button(() => _game.ArmBuilding(current.Id));
                foreach (string name in classes.Split(' '))
                {
                    card.AddToClassList(name);
                }

                cards.Add(card);
                Icon(building.ArtName, "card-icon", card);
                Text(building.Label, "card-label", card);

                BuildCost cost = Buildings.EffectiveCost(building, discount);
                VisualElement costRow = El("row", card);
                AddCost(costRow, "stone", cost.Stone);
                AddCost(costRow, "wood", cost.Wood);
                AddCost(costRow, "hammers", cost.Hammers);
            }

            VisualElement tools = El("tools", _bottom);
            Btn("Снос", _game.DemolishMode ? "small-button armed" : "small-button", _game.ToggleDemolish, tools);
            Btn(_game.SoilMode ? "Отменить очистку" : "Очистка почвы", _game.SoilMode ? "small-button armed" : "small-button", _game.ToggleSoilCleanse, tools);

            if (_game.SoilMode)
            {
                Text(_game.SoilSacrificeHexId == null ? "Выберите гекс, который будет уничтожен." : "Выберите гекс, который станет чистым.", "hint", _bottom);
            }

            Btn("Закончить строительство", "end-button", _game.EndPhase, _bottom);
        }

        /// <summary>
        /// What the selected cell of the globe is worth: the scouting report, the
        /// faction that holds it, the toxic trail left in it, and the two things the
        /// spec lets a player do in the scout phase. Scouting costs more the farther
        /// the cell is from the island.
        /// </summary>
        private void RefreshCellPanel()
        {
            World world = Session.World;
            Player player = Session.Human;
            WorldCell cell = world.Get(_game.SelectedCellId ?? _game.HoveredCellId);
            _hexPanel.style.display = DisplayStyle.Flex;

            if (cell == null)
            {
                Text("Гекс не выбран", "panel-title", _hexPanel);
                Text("Кликните по гексу на глобусе: здесь появятся разведка, фракция и перелёт.", "panel-text", _hexPanel);

                return;
            }

            bool isHere = cell.Id == player.CellId;
            CellVisibility visibility = WorldRules.Visibility(world, cell);
            ScoutCheck scout = WorldRules.CheckScout(world, player.CellId, cell.Id, player.Resources[ResourceId.Scouting]);
            string moveRefusal = WorldRules.MoveRefusal(world, player.CellId, cell.Id, Session.MovedThisTurn);
            Player owner = cell.Revealed && cell.OwnerId != null ? Session.Players.Find(candidate => candidate.Id == cell.OwnerId) : null;
            bool isLair = cell.Revealed && cell.Boss;
            bool isHeld = cell.Kind == CellKind.Island && !cell.Cleared && cell.IslandCount > 0;

            string title = isLair ? "Логово Повелителя Мора"
                : isHere ? "Ваш гекс"
                : owner != null ? "Остров: " + owner.Nickname
                : visibility == CellVisibility.Frontier ? "Неразведанный гекс"
                : visibility == CellVisibility.Fogged ? "Гекс в тумане"
                : cell.Kind == CellKind.Void ? "Облака"
                : cell.Kind == CellKind.Island ? "Дикий остров"
                : "Поселение";
            Text(title, isLair ? "panel-title bad" : "panel-title", _hexPanel);

            if (!cell.Revealed)
            {
                Text("Под туманом облака, остров или поселение. Разведка покажет, что здесь.", "panel-text", _hexPanel);
                if (!scout.Ok)
                {
                    Text(scout.Refusal, "panel-text bad", _hexPanel);
                }
            }
            else if (cell.Kind == CellKind.Void)
            {
                Text("Облака над морем. Здесь пусто, но пролететь можно.", "panel-text", _hexPanel);
            }
            else if (cell.Kind == CellKind.Settlement)
            {
                VisualElement row = El("row", _hexPanel);
                Icon("village", "face-icon", row);
                Text("Поселение — скоро", "panel-text", row);
            }
            else if (isLair)
            {
                VisualElement row = El("row", _hexPanel);
                Icon("boss", "face-icon", row);
                Text(player.BossSlain ? "Повелитель Мора повержен вами" : "Здесь ждёт босс — Повелитель Мора", "panel-text bad", row);
                Text(
                    player.BossSlain
                        ? "Трофей получен: стройте Центральный конвертер на своём острове."
                        : "Прилетите сюда — в конце хода начнётся бой с боссом. Победа даёт технологию «Центральная конверсия» и Центральный конвертер.",
                    "panel-text",
                    _hexPanel);
            }
            else
            {
                bool isEmpty = cell.Cleared || cell.IslandCount == 0;
                VisualElement row = El("row", _hexPanel);
                Icon(cell.Cleared ? "check" : "skeleton", "face-icon", row);
                Text(Biomes.Get(cell.Biome).Label + (isEmpty ? " · зачищен" : " · диких островов: " + cell.IslandCount), "panel-text", row);

                if (!isEmpty)
                {
                    Text(
                        cell.Activated
                            ? "Острова проснулись: зачистка в конце хода, пока стоите здесь."
                            : "Прилетите сюда — острова проснутся, и в конце хода начнётся зачистка.",
                        "panel-text",
                        _hexPanel);
                }
            }

            if (cell.Revealed)
            {
                if (cell.Faction.HasValue)
                {
                    Faction faction = Factions.Get(cell.Faction.Value);
                    Button badge = new Button(() =>
                    {
                        _factionsOpen = true;
                        RefreshOverlay();
                    });
                    badge.AddToClassList("face");
                    badge.tooltip = "Фракция: " + faction.Name;
                    _hexPanel.Add(badge);
                    Icon(faction.Icon, "card-icon", badge);
                    VisualElement body = El("player-body", badge);
                    Text(faction.Id == FactionId.Helios ? faction.Name + " · " + Factions.NexusArcologyName : faction.Name, "face-amount", body);
                    Text(isHeld || cell.Boss ? "Удерживает остров" : "Был выбит отсюда", "player-meta", body);
                }

                if (owner != null)
                {
                    VisualElement row = El("row", _hexPanel);
                    Icon("stronghold", "face-icon", row);
                    Text("Здесь стоит " + owner.Nickname, "panel-text", row).style.color = Props.Hex(owner.Color);
                }

                VisualElement trail = El("row", _hexPanel);
                Icon("toxicity", "face-icon", trail);
                Text("Токсичный шлейф: " + cell.ToxicTrail, "panel-text toxic", trail);
                Text("Шанс события из шлейфа: " + JsMath.Round(TrailEvents.EventChance(cell.ToxicTrail) * 100) + "%", "panel-text", _hexPanel);
            }

            // Scouting and the flight belong to the scout phase.
            bool isLocked = !Session.IsExplorationOpen;
            VisualElement buttons = El("row cell-buttons", _hexPanel);

            if (!cell.Revealed)
            {
                Button scoutButton = new Button(_game.ScoutSelected);
                scoutButton.AddToClassList("small-button");
                scoutButton.AddToClassList("row");
                scoutButton.SetEnabled(!isLocked && scout.Ok);
                buttons.Add(scoutButton);
                Text("Разведать гекс", "cost-value", scoutButton);
                Icon("scouting", "cost-icon", scoutButton);
                Text(scout.Cost.ToString(), "cost-value", scoutButton);
            }

            if (!isHere)
            {
                Btn(Session.MovedThisTurn ? "Уже перелетали" : "Перелететь сюда", "small-button", _game.FlyToSelected, buttons)
                    .SetEnabled(!isLocked && moveRefusal == null);
            }
        }

        /// <summary>The bottom bar of the global map: what the phase allows, and the way on.</summary>
        private void RefreshWorldBottom()
        {
            Btn("Фракции", "small-button", () =>
            {
                _factionsOpen = true;
                RefreshOverlay();
            }, _bottom);

            if (!Session.IsExplorationOpen)
            {
                Text("Карта мира. Разведка и перелёт откроются в фазе разведки.", "hint", _bottom);
                Btn("К острову", "end-button", _game.ToggleWorld, _bottom);

                return;
            }

            Text(
                Session.MovedThisTurn
                    ? "Остров перелетел. Разведайте гексы на границе тумана или закончите фазу."
                    : "Разведайте гекс на границе тумана и перелетите в соседний. Остров, который остался на месте, оставит в гексе свой яд.",
                "hint",
                _bottom);
            Btn("Закончить разведку", "end-button", _game.EndPhase, _bottom);
        }

        /// <summary>Who holds the islands of the world: the six factions, their strengths and weaknesses.</summary>
        private void AddFactions(VisualElement modal)
        {
            Action close = _externalClose;
            Text("Фракции", "modal-title", modal);
            ScrollView list = new ScrollView();
            list.AddToClassList("tech-list");
            modal.Add(list);

            foreach (Faction faction in Factions.All)
            {
                VisualElement row = El("tech", list);
                VisualElement head = El("row", row);
                Icon(faction.Icon, "card-icon", head);
                VisualElement names = El("player-body", head);
                Text(faction.Name, "panel-subtitle", names);
                Text(faction.Tagline, "player-meta", names);
                Text(faction.Summary, "panel-text", row);
                Text("Сила: " + faction.Strength, "panel-text good", row);
                Text("Слабость: " + faction.Weakness, "panel-text bad", row);
            }

            Btn("Закрыть", "end-button", () =>
            {
                if (close != null)
                {
                    close();

                    return;
                }

                _factionsOpen = false;
                RefreshOverlay();
            }, modal);
        }

        /// <summary>
        /// The battle's numbers change every tick. The game root calls this a few
        /// times a second; the bottom bar is rebuilt only when what it shows changes,
        /// so a button is not torn down under a click.
        /// </summary>
        public void RefreshBattle()
        {
            if (_root == null || Session == null || Session.Battle == null || !_game.ShowBattle)
            {
                return;
            }

            RefreshResources();
            RefreshHexPanel();

            string key = BattleBottomKey(Session.Battle);
            if (key != _battleBottomKey)
            {
                RefreshBottom();
            }
        }

        private string BattleBottomKey(CleanupSim sim)
        {
            return sim.Status + ":" + _game.SkillArmed + ":" + _game.BattlePaused + ":" + _game.BattleSpeed + ":" + sim.Mana + ":" + Mathf.CeilToInt((float)sim.SkillCooldown);
        }

        /// <summary>The state of the level: the army, the stronghold, every enemy island, the way out.</summary>
        private void RefreshBattlePanel()
        {
            CleanupSim sim = Session.Battle;
            if (sim == null)
            {
                _hexPanel.style.display = DisplayStyle.None;

                return;
            }

            _hexPanel.style.display = DisplayStyle.Flex;
            Text("Зачистка · уровень " + sim.Tier, "panel-title", _hexPanel);

            VisualElement army = El("row", _hexPanel);
            Icon("army", "face-icon", army);
            Text("Армия: " + sim.ArmyAlive + " / " + sim.Roster.Count + " · убито врагов: " + sim.Kills, "panel-text", army);

            int stronghold = sim.StrongholdPct;
            VisualElement home = El("row", _hexPanel);
            Icon("stronghold", "face-icon", home);
            Text(
                (stronghold >= 0 ? "Твердыня: " + stronghold + "% · " : "") + "построек: " + sim.StructuresStanding + " / " + sim.StructuresAtStart,
                stronghold >= 0 && stronghold < 35 ? "panel-text bad" : "panel-text",
                home);

            foreach (SimIsland island in sim.Islands)
            {
                if (island.Side != CleanupSide.Enemy)
                {
                    continue;
                }

                string state;
                string classes = "panel-text";
                switch (island.State)
                {
                    case IslandState.Active:
                        state = "врагов: " + sim.GarrisonAlive(island.Index) + " / " + island.GarrisonTotal;
                        break;
                    case IslandState.Cleared:
                        state = "зачищен — пристыкуйтесь, уплывёт через " + sim.DriftLeft(island) + " с";
                        classes = "panel-text good";
                        break;
                    case IslandState.Attached:
                        state = "присоединён: +" + island.Joined + " гексов";
                        classes = "panel-text good";
                        break;
                    default:
                        state = "потерян";
                        classes = "panel-text bad";
                        break;
                }

                VisualElement row = El("row", _hexPanel);
                Icon(island.State == IslandState.Active ? "skeleton" : "check", "face-icon", row);
                Text(island.Label + " · " + state, classes, row);
            }

            string exit = "Окон в облаках открыто: " + sim.WindowsOpen;
            if (sim.InWindow)
            {
                exit += " · выход: " + JsMath.Round(sim.ExitProgress * 100) + "%";
            }

            Text(exit, sim.InWindow ? "panel-text toxic" : "panel-text", _hexPanel);
        }

        /// <summary>The battle's bar: the skill, the clock, and how to sail.</summary>
        private void RefreshBattleBottom()
        {
            CleanupSim sim = Session.Battle;
            if (sim == null)
            {
                return;
            }

            _battleBottomKey = BattleBottomKey(sim);
            bool isRunning = sim.Status == SimStatus.Running;

            int cooldown = Mathf.CeilToInt((float)sim.SkillCooldown);
            string skill = ShatterSkill.HotkeyLabel + " · " + ShatterSkill.Label + " (" + ShatterSkill.ManaCost + " маны)" + (cooldown > 0 ? " · " + cooldown + " с" : "");
            Button skillButton = Btn(skill, _game.SkillArmed ? "small-button armed" : "small-button", _game.ToggleSkill, _bottom);
            skillButton.tooltip = ShatterSkill.Description;
            skillButton.SetEnabled(isRunning);

            VisualElement mana = El("chip", _bottom);
            Icon("mana", "chip-icon", mana);
            Text(sim.Mana.ToString(), "chip-value", mana);

            Text(
                _game.SkillArmed
                    ? "Кликните по гексу, который нужно разрушить. Esc — отмена."
                    : "WASD — вести остров. Сойдитесь с островом врага бортом: войска перейдут сами. Уйти из боя можно через светлое окно в облаках.",
                "hint",
                _bottom);

            Btn(_game.BattlePaused ? "Продолжить" : "Пауза", _game.BattlePaused ? "small-button armed" : "small-button", _game.ToggleBattlePause, _bottom).SetEnabled(isRunning);
            Btn("Скорость ×" + _game.BattleSpeed, "small-button", _game.ToggleBattleSpeed, _bottom).SetEnabled(isRunning);
        }

        /// <summary>What the level ended with, and the way on to the next turn.</summary>
        private void AddBattleResult(VisualElement modal, CleanupResult result)
        {
            string title;
            string text;
            switch (result.Outcome)
            {
                case CleanupOutcome.Won:
                    title = "Уровень зачищен";
                    text = "Все вражеские острова зачищены. Пристыкованные острова стали частью вашего острова — ровно так, как вы их пристыковали.";
                    break;
                case CleanupOutcome.Lost:
                    title = "Остров пал";
                    text = "Разрушена последняя постройка. Твердыня лежит в руинах и не приносит дохода, пока её не отстроят. Острова, пристыкованные в этом бою, откалываются и уходят, острова врага остаются в клетке.";
                    break;
                case CleanupOutcome.Retreated:
                    title = "Отступление";
                    text = "Остров ушёл через окно в ядовитых облаках. Пристыкованные острова остаются вашими, остальные ждут следующего хода. Кто стоял на чужих островах, остался там.";
                    break;
                default:
                    title = "Море спокойно";
                    text = "В этой клетке нет вражеских островов.";
                    break;
            }

            Text(title, result.Outcome == CleanupOutcome.Lost ? "modal-title bad" : "modal-title good", modal);
            Text(text, "panel-text", modal);

            if (result.TotalIslands > 0)
            {
                Text("Присоединено островов: " + result.AttachedIslands + " / " + result.TotalIslands + " · врагов убито: " + result.Kills + " · гексов к острову: +" + result.Annexed.Count, "panel-subtitle", modal);
            }

            if (result.Razed > 0 || result.Structures.Exists(entry => entry.Hp < entry.StartHp))
            {
                Text("Разрушено построек: " + result.Razed + " · повреждено: " + result.Structures.Count(entry => entry.Hp > 0 && entry.Hp < entry.MaxHp), "panel-text bad", modal);
                Text("Разрушенные постройки исчезают с гексов. Повреждённые чинятся сами: +25% прочности в конце каждого хода.", "panel-text", modal);
            }

            List<string> land = new List<string>();
            if (result.DestroyedHexIds.Count > 0)
            {
                land.Add("своих гексов разрушено: " + result.DestroyedHexIds.Count);
            }

            if (result.Poisoned.Count > 0)
            {
                land.Add("отравлено облаками: " + result.Poisoned.Count + " гекс.");
            }

            if (result.ManaSpent > 0)
            {
                land.Add("потрачено маны: " + result.ManaSpent);
            }

            if (land.Count > 0)
            {
                Text("Земля и мана — " + string.Join(", ", land), "panel-text", modal);
            }

            Text("Вернулись: " + result.Survivors.Count + DescribeUnits(result.Survivors), "panel-text good", modal);
            if (result.Lost.Count > 0)
            {
                Text("Погибли: " + result.Lost.Count + DescribeUnits(result.Lost) + " · людей: −" + result.Lost.Sum(unit => unit.Upkeep), "panel-text bad", modal);
            }

            Btn("Следующий ход", "end-button", _game.EndPhase, modal);
        }

        private static string DescribeUnits(List<Unit> units)
        {
            if (units.Count == 0)
            {
                return "";
            }

            return " — " + string.Join(", ", units.GroupBy(unit => unit.Label).Select(group => group.Key + " ×" + group.Count()));
        }

        private void AddCost(VisualElement row, string icon, int amount)
        {
            Icon(icon, "cost-icon", row);
            Text(amount.ToString(), "cost-value", row);
        }

        /// <summary>In the tax phase every rolled die shows what it will pay, above its hex.</summary>
        private void RefreshPlates()
        {
            _plates.Clear();
            _plateByHex.Clear();

            TaxPlan plan = Session.HumanTaxPlan;
            if (plan == null || !Session.IsTaxOpen || _game.ShowWorld || _game.IsReadonly || _game.Collecting)
            {
                return;
            }

            Player player = Session.Human;
            foreach (TaxRoll roll in plan.Rolls)
            {
                HexTile hex = player.FindHex(roll.HexId);
                if (hex == null)
                {
                    continue;
                }

                Face face = roll.Faces[roll.ChosenIndex];
                Tax.FacePayout(face, hex, Session.Effects, out int amount, out int toxicity);

                VisualElement plate = El(roll.ChosenIndex == roll.RolledIndex ? "plate" : "plate changed", _plates);
                plate.pickingMode = PickingMode.Ignore;
                Icon(ResourceTable.Get(face.Resource).Icon, "plate-icon", plate).pickingMode = PickingMode.Ignore;
                Text("+" + amount, "plate-amount", plate).pickingMode = PickingMode.Ignore;
                if (toxicity > 0 && !Buildings.HasConverter(player))
                {
                    Icon("toxicity", "plate-icon", plate).pickingMode = PickingMode.Ignore;
                    Text("+" + toxicity + "%", "plate-toxicity", plate).pickingMode = PickingMode.Ignore;
                }

                _plateByHex.Add(new KeyValuePair<string, VisualElement>(roll.HexId, plate));
            }
        }

        /// <summary>The end screen, the slot's spin, the trail's event, the factions or the technology tree, in that order of precedence.</summary>
        private void RefreshOverlay()
        {
            _overlay.Clear();

            if (Session.Outcome != null)
            {
                _overlay.style.display = DisplayStyle.Flex;
                AddOutcome(El("modal", _overlay), Session.Outcome);

                return;
            }

            if (_game.PendingSpin != null)
            {
                _overlay.style.display = DisplayStyle.Flex;
                AddSpin(El("modal", _overlay), _game.PendingSpin);

                return;
            }

            if (_game.PendingTrail != null)
            {
                _overlay.style.display = DisplayStyle.Flex;
                VisualElement modal = El("modal", _overlay);
                Text(_game.PendingTrail.Title, "modal-title bad", modal);
                Text(_game.PendingTrail.Text, "panel-text", modal);
                Btn("Ясно", "end-button", _game.DismissTrail, modal);

                return;
            }

            if (_game.ShowBattle && Session.BattleResult != null)
            {
                _overlay.style.display = DisplayStyle.Flex;
                AddBattleResult(El("modal wide", _overlay), Session.BattleResult);

                return;
            }

            if (_factionsOpen)
            {
                _overlay.style.display = DisplayStyle.Flex;
                AddFactions(El("modal wide", _overlay));

                return;
            }

            if (_techsOpen)
            {
                _overlay.style.display = DisplayStyle.Flex;
                AddTechs(El("modal wide", _overlay));

                return;
            }

            _overlay.style.display = DisplayStyle.None;
        }

        private void AddOutcome(VisualElement modal, GameOutcome outcome)
        {
            string title = outcome.Kind == GameOutcomeKind.Defeat ? "Поражение" : outcome.Kind == GameOutcomeKind.Shared ? "Общая победа" : "Победа";
            Text(title, outcome.Kind == GameOutcomeKind.Defeat ? "modal-title bad" : "modal-title good", modal);
            Text(DescribeReason(outcome.Reason) + " Ход " + outcome.Turn + ".", "panel-text", modal);
            Btn("Новая игра", "end-button", () => _game.NewGame(Session.Nickname), modal);
        }

        private static string DescribeReason(GameOutcomeReason reason)
        {
            switch (reason)
            {
                case GameOutcomeReason.Converter: return "Центральный конвертер построен: яд побеждён.";
                case GameOutcomeReason.SharedConverter: return "Конвертер построен в один ход с соперником.";
                case GameOutcomeReason.Elimination: return "Все соперники выбыли.";
                case GameOutcomeReason.Ruined: return "Твердыня лежит в руинах.";
                case GameOutcomeReason.NoBuildings: return "На острове не осталось ни одного здания.";
                default: return "Соперник построил Центральный конвертер раньше.";
            }
        }

        private static string SymbolIcon(SlotSymbol symbol)
        {
            switch (symbol)
            {
                case SlotSymbol.Gear: return "technology";
                case SlotSymbol.Coin: return "coin";
                case SlotSymbol.Skull: return "dead";
                default: return "poison";
            }
        }

        private void AddTechs(VisualElement modal)
        {
            Action close = _externalClose;
            Action refresh = _externalRefresh;
            Text("Технологии · наука " + Session.Human.Resources[ResourceId.Science], "modal-title", modal);
            ScrollView list = new ScrollView();
            list.AddToClassList("tech-list");
            modal.Add(list);

            foreach (Tech tech in Techs.All)
            {
                Tech current = tech;
                bool owned = Session.Researched.Contains(tech.Id);
                bool available = Techs.IsAvailable(tech, Session.Researched);
                string state = owned ? "изучено" : tech.Trophy ? "трофей босса" : available ? tech.Cost + " науки" : "недоступно";

                Button row = new Button(() =>
                {
                    Session.Research(current.Id);
                    (refresh ?? RefreshOverlay)();
                });
                row.AddToClassList("tech");
                row.AddToClassList(owned ? "owned" : available ? "available" : "locked");
                row.SetEnabled(available);
                list.Add(row);

                VisualElement head = El("row", row);
                Text(tech.Label, "face-amount", head);
                Text(state, "face-note", head);
                Text(tech.Description, "panel-text", row);
            }

            Btn("Закрыть", "end-button", () =>
            {
                if (close != null)
                {
                    close();

                    return;
                }

                _techsOpen = false;
                RefreshOverlay();
            }, modal);
        }

        /// <summary>A full-screen container that lets clicks through to what is under it.</summary>
        private VisualElement Layer(string className)
        {
            VisualElement layer = El(className, _root);
            layer.pickingMode = PickingMode.Ignore;

            return layer;
        }

        private static VisualElement El(string classNames, VisualElement parent)
        {
            VisualElement element = new VisualElement();
            foreach (string name in classNames.Split(' '))
            {
                element.AddToClassList(name);
            }

            parent.Add(element);

            return element;
        }

        private static Label Text(string text, string classNames, VisualElement parent)
        {
            Label label = new Label(text);
            foreach (string name in classNames.Split(' '))
            {
                label.AddToClassList(name);
            }

            parent.Add(label);

            return label;
        }

        private static Button Btn(string text, string classNames, Action onClick, VisualElement parent)
        {
            Button button = new Button(onClick) { text = text };
            foreach (string name in classNames.Split(' '))
            {
                button.AddToClassList(name);
            }

            parent.Add(button);

            return button;
        }

        /// <summary>An icon from <c>Resources/Icons</c>, named as in the prototype's <c>assets/icons</c>.</summary>
        private VisualElement Icon(string iconName, string className, VisualElement parent)
        {
            if (!_icons.TryGetValue(iconName, out Texture2D texture))
            {
                texture = Resources.Load<Texture2D>("Icons/" + iconName);
                _icons[iconName] = texture;
            }

            VisualElement icon = El(className, parent);
            icon.AddToClassList("icon");
            if (texture != null)
            {
                icon.style.backgroundImage = new StyleBackground(texture);
            }

            return icon;
        }
    }
}
