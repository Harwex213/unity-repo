using System.Collections.Generic;
using System.Linq;
using Hexwex.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hexwex.View
{
    /// <summary>
    /// Runs one session on the island scene. It owns what the prototype kept in
    /// <c>store/ui-state.ts</c>: the armed building, the tool modes and the
    /// selection. A click on a hex means what the current stage, phase and mode
    /// say it means, and every rule is asked of the session.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private string nickname = GameSession.DefaultNickname;
        [SerializeField] private IslandView islandView;
        [SerializeField] private GlobeView globeView;
        [SerializeField] private BattleView battleView;
        [SerializeField] private Hud hud;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private Light sun;
        [SerializeField] private MainMenu mainMenu;
        [SerializeField] private LearnGuide learnGuide;
        /// <summary>The prototype opens its menu only with the <c>?menu</c> flag. Here the game opens on it unless this is off.</summary>
        [SerializeField] private bool openMenuAtStart = true;

        public GameSession Session { get; private set; }
        public BuildingId? ArmedBuilding { get; private set; }
        public bool DemolishMode { get; private set; }
        public bool SoilMode { get; private set; }
        /// <summary>The hex the soil cleansing will destroy, once the first click has picked it.</summary>
        public string SoilSacrificeHexId { get; private set; }
        public string SelectedHexId { get; private set; }
        public string HoveredHexId { get; private set; }
        /// <summary>The player's toxicity slot spin, waiting to be shown and dismissed.</summary>
        public SlotSpin PendingSpin { get; private set; }
        /// <summary>What the toxic trail threw at the island, waiting to be shown and dismissed.</summary>
        public TrailEvent PendingTrail { get; private set; }
        /// <summary>The global map is on screen instead of the island.</summary>
        public bool ShowWorld { get; private set; }
        public string SelectedCellId { get; private set; }
        public string HoveredCellId { get; private set; }
        /// <summary>The battle level of the clear phase is on screen.</summary>
        public bool ShowBattle { get; private set; }
        /// <summary>The tax collection is playing: the motes fly, and the game waits for them.</summary>
        public bool Collecting { get; private set; }
        /// <summary>Whose island is open. <c>null</c> means the player's own island.</summary>
        public string ViewedPlayerId { get; private set; }

        /// <summary>The player whose island the island page shows: a rival while visiting, the human otherwise.</summary>
        public Player ViewedPlayer
        {
            get
            {
                Player viewed = ViewedPlayerId != null ? Session.Players.Find(player => player.Id == ViewedPlayerId) : null;

                return viewed ?? Session.Human;
            }
        }

        /// <summary>A rival's island is for looking only: nothing on it can be built, razed or picked.</summary>
        public bool IsReadonly
        {
            get { return Session != null && ViewedPlayer != Session.Human; }
        }
        public bool BattlePaused { get; private set; }
        /// <summary>1 or 2: how many seconds of battle one second of play runs.</summary>
        public int BattleSpeed { get; private set; } = 1;
        /// <summary>The battle's skill waits for a target hex. A click on a hex casts it.</summary>
        public bool SkillArmed { get; private set; }
        /// <summary>The seed typed into the setup bar.</summary>
        public string PendingNickname { get; set; }
        public Camera WorldCamera
        {
            get { return worldCamera; }
        }

        public GlobeView GlobeView
        {
            get { return globeView; }
        }

        public LearnGuide LearnGuide
        {
            get { return learnGuide; }
        }

        /// <summary>
        /// The card of the learn guide that belongs to what is on screen now, or
        /// <c>null</c>. Each phase has its own page, so the card waits for its page
        /// and its phase. The guide waits for the main menu to close.
        /// </summary>
        public GuideStepId? GuideContext()
        {
            bool isMenuUp = mainMenu != null && mainMenu.HoldsInput;
            if (Session == null || Session.Outcome != null || isMenuUp)
            {
                return null;
            }

            if (ShowBattle)
            {
                return Session.Phase == Phase.Clear ? GuideStepId.Battle : (GuideStepId?)null;
            }

            if (ShowWorld)
            {
                return Session.Phase == Phase.Scout ? GuideStepId.Scout : (GuideStepId?)null;
            }

            // The cards of the island page belong to the player's own island.
            if (IsReadonly)
            {
                return null;
            }

            if (Session.Stage == GameStage.Setup)
            {
                return GuideStepId.Intro;
            }

            if (Session.Phase == Phase.Build)
            {
                return GuideStepId.Build;
            }

            return Session.Phase == Phase.Tax ? GuideStepId.Tax : (GuideStepId?)null;
        }

        public MainMenu Menu
        {
            get { return mainMenu; }
        }

        public BattleView BattleView
        {
            get { return battleView; }
        }

        public IslandView IslandView
        {
            get { return islandView; }
        }

        private readonly HashSet<string> _reachable = new HashSet<string>();
        private CameraRig.Orbit? _islandOrbit;
        private CameraRig.Orbit? _globeOrbit;
        private CameraRig.Orbit? _homeOrbit;
        private float _accumulator;
        private float _nextBattleHud;

        /// <summary>A frame never runs more ticks than this, so a slow frame cannot snowball.</summary>
        private const int MaxTicksPerFrame = 8;
        /// <summary>Degrees per second the camera turns around the island behind the main menu.</summary>
        private const float MenuSpin = 5f;
        /// <summary>The gap between the starts of two buildings in the production reveal.</summary>
        private const float RevealStagger = 0.11f;
        /// <summary>The building's squash-and-stretch pulse.</summary>
        private const float PulseSeconds = 0.42f;
        /// <summary>The plate pops when the pulse releases, this long after the building's start.</summary>
        private const float PlateAtSeconds = 0.22f;

        private readonly Dictionary<string, float> _revealDelays = new Dictionary<string, float>();
        private float _revealStart;
        private bool _collected;
        private Quaternion _islandSunRotation;

        private void Start()
        {
            hud.Bind(this);
            NewGame(nickname);

            if (learnGuide != null)
            {
                learnGuide.Bind(this, hud);
            }

            if (mainMenu != null)
            {
                mainMenu.Bind(this, hud);
                if (openMenuAtStart)
                {
                    mainMenu.Open();
                }
            }
        }

        /// <summary>A fresh session. The nickname is the seed: the same name grows the same island.</summary>
        public void NewGame(string seed)
        {
            if (Session != null)
            {
                Session.Changed -= Redraw;
                Session.Notice -= hud.ShowNotice;
            }

            islandView.Clear();
            globeView.Clear();
            SetBattleShown(false);
            Session = new GameSession(seed);
            Session.Changed += Redraw;
            Session.Notice += hud.ShowNotice;
            PendingNickname = Session.Nickname;
            Collecting = false;
            _collected = false;
            _revealDelays.Clear();
            ViewedPlayerId = null;
            PendingSpin = null;
            PendingTrail = null;
            HoveredHexId = null;
            HoveredCellId = null;
            SelectedCellId = null;
            _globeOrbit = null;
            SetWorldShown(false);
            ClearModes();
            Redraw();
        }

        public void StartGame()
        {
            Session.StartGame();
        }

        /// <summary>Clicking the armed card again disarms it, which is how the player cancels.</summary>
        public void ArmBuilding(BuildingId buildingId)
        {
            if (!Session.IsBuildingAllowed)
            {
                return;
            }

            Building building = Buildings.Get(buildingId);
            if (!Buildings.IsUnlocked(Session.Human, building))
            {
                hud.ShowNotice("«" + building.Label + "» откроется после победы над боссом");

                return;
            }

            BuildingId? next = ArmedBuilding == buildingId ? (BuildingId?)null : buildingId;
            ClearModes();
            ArmedBuilding = next;
            Redraw();
        }

        /// <summary>Arming a building and arming the wrecking ball are mutually exclusive.</summary>
        public void ToggleDemolish()
        {
            if (!Session.IsBuildingAllowed)
            {
                return;
            }

            bool next = !DemolishMode;
            ClearModes();
            DemolishMode = next;
            Redraw();
        }

        /// <summary>The button arms the soil cleansing, and a second press cancels it.</summary>
        public void ToggleSoilCleanse()
        {
            if (SoilMode)
            {
                ClearModes();
                Redraw();

                return;
            }

            string block = Session.SoilCleanseBlock();
            if (block != null)
            {
                hud.ShowNotice(block);

                return;
            }

            ClearModes();
            SoilMode = true;
            Redraw();
        }

        public void EndPhase()
        {
            if (Collecting)
            {
                return;
            }

            // "Собрать" plays the collection first; the phase ends when the last mote has landed.
            if (Session.IsTaxOpen && !_collected && !ShowWorld && !ShowBattle)
            {
                StartCollection();

                return;
            }

            _collected = false;
            Phase before = Session.Phase;

            // Every phase starts on the player's own island.
            SetViewedPlayer(null);

            ClearModes();
            Session.EndPhase();

            if (before == Phase.Tax && Session.LastPayouts != null)
            {
                PendingSpin = Session.LastSlotSpin;
                hud.ShowNotice(DescribePayouts(Session.LastPayouts));
            }

            if (before == Phase.Build && Session.IsTaxOpen)
            {
                StartReveal();
            }

            // The scout phase happens on the global map, and the turn starts on the island.
            if (before == Phase.Tax && Session.Phase == Phase.Scout)
            {
                SelectedCellId = Session.Human.CellId;
                SetWorldShown(true);
            }

            if (before == Phase.Scout)
            {
                PendingTrail = Session.LastTrailEvent;
                SetWorldShown(false);

                // Wild islands in the cell: the clear phase is a battle on its own level.
                if (Session.Phase == Phase.Clear && Session.Battle != null)
                {
                    SetBattleShown(true);
                }
            }

            if (before == Phase.Clear && Session.Battle == null)
            {
                SetBattleShown(false);
            }

            // The tax phase and the end screen belong to the island.
            if (ShowWorld && !CanSwitchPage)
            {
                SetWorldShown(false);
            }

            Redraw();
        }

        /// <summary>
        /// The collection as the player sees it: the payouts fly to the HUD, and only
        /// then does the session pay them and move on. The payouts are read from the
        /// plan ahead of time; nothing is paid twice.
        /// </summary>
        private void StartCollection()
        {
            TaxPlan plan = Session.HumanTaxPlan;
            SetViewedPlayer(null);
            ClearModes();
            StopReveal();
            Collecting = true;
            // The motes aim at the HUD as it is laid out now; the redraw after it would move nothing, but its new elements have no place yet.
            hud.StartCollection(plan.Payouts(Session.Human, Session.Effects), () =>
            {
                Collecting = false;
                _collected = true;
                EndPhase();
            });
            Redraw();
        }

        /// <summary>
        /// The production reveal at the start of the tax phase. Every building with
        /// a die plays a short pulse, and its rolled payout pops up above it. The
        /// buildings go top to bottom, ties left to right, each starting a little
        /// after the one before, so the pulses overlap like an avalanche.
        /// </summary>
        private void StartReveal()
        {
            _revealDelays.Clear();
            TaxPlan plan = Session.HumanTaxPlan;
            if (plan == null)
            {
                return;
            }

            List<HexTile> hexes = new List<HexTile>();
            foreach (TaxRoll roll in plan.Rolls)
            {
                HexTile hex = Session.Human.FindHex(roll.HexId);
                if (hex != null)
                {
                    hexes.Add(hex);
                }
            }

            // On the prototype's plane y grows with r, and x with q + r / 2.
            List<HexTile> ordered = hexes.OrderBy(hex => hex.R).ThenBy(hex => hex.Q + hex.R * 0.5).ToList();
            for (int index = 0; index < ordered.Count; index += 1)
            {
                _revealDelays[ordered[index].Id] = index * RevealStagger;
            }

            _revealStart = Time.unscaledTime;
        }

        private void StopReveal()
        {
            foreach (string hexId in _revealDelays.Keys)
            {
                islandView.Pulse(hexId, 1f);
            }

            _revealDelays.Clear();
        }

        /// <summary>The pulses of the reveal, frame by frame. The reveal ends with its last pulse.</summary>
        private void UpdateReveal()
        {
            if (_revealDelays.Count == 0)
            {
                return;
            }

            float elapsed = Time.unscaledTime - _revealStart;
            float longest = 0f;
            foreach (KeyValuePair<string, float> entry in _revealDelays)
            {
                islandView.Pulse(entry.Key, (elapsed - entry.Value) / PulseSeconds);
                longest = Mathf.Max(longest, entry.Value);
            }

            if (elapsed > longest + PulseSeconds + PlateAtSeconds + 0.4f)
            {
                StopReveal();
            }
        }

        /// <summary>
        /// Seconds since the plate of a hex popped in the reveal. Negative while the
        /// plate still waits its turn; large when no reveal is playing.
        /// </summary>
        public float PlateAge(string hexId)
        {
            return _revealDelays.TryGetValue(hexId, out float delay) ? Time.unscaledTime - _revealStart - delay - PlateAtSeconds : 1000f;
        }

        public void DismissSpin()
        {
            PendingSpin = null;
            Redraw();
        }

        public void DismissTrail()
        {
            PendingTrail = null;
            Redraw();
        }

        /// <summary>
        /// The build and scout phases let the player switch between the island and
        /// the global map. The phase does not change, and scouting and the flight
        /// stay locked outside the scout phase.
        /// </summary>
        public bool CanSwitchPage
        {
            get
            {
                return Session.Stage == GameStage.Play && Session.Outcome == null
                    && (Session.Phase == Phase.Build || Session.Phase == Phase.Scout);
            }
        }

        /// <summary>
        /// Opens a player's island from the players list: a rival's read-only, or the
        /// player's own with <c>null</c> or their own id. The build and scout phases
        /// allow it; the tax phase and the battle keep the player at home.
        /// </summary>
        public void ViewIsland(string playerId)
        {
            if (!CanSwitchPage)
            {
                return;
            }

            string next = playerId == Session.Human.Id ? null : playerId;
            ClearModes();
            HoveredHexId = null;
            if (ShowWorld)
            {
                SetWorldShown(false);
            }

            SetViewedPlayer(next);
            Redraw();
        }

        /// <summary>Another island has other hexes under the same ids, so the view is built again.</summary>
        private void SetViewedPlayer(string playerId)
        {
            if (playerId == ViewedPlayerId)
            {
                return;
            }

            ViewedPlayerId = playerId;
            islandView.Clear();
        }

        public void ToggleWorld()
        {
            if (!CanSwitchPage)
            {
                return;
            }

            ClearModes();
            SetViewedPlayer(null);
            if (!ShowWorld && SelectedCellId == null)
            {
                SelectedCellId = Session.Human.CellId;
            }

            SetWorldShown(!ShowWorld);
            Redraw();
        }

        public void ScoutSelected()
        {
            if (SelectedCellId != null)
            {
                Session.Scout(SelectedCellId);
            }
        }

        public void FlyToSelected()
        {
            if (SelectedCellId != null)
            {
                Session.MoveIsland(SelectedCellId);
            }
        }

        public void Redraw()
        {
            RenderScene();
            hud.Refresh();
        }

        /// <summary>Arms the battle's skill for a target hex, or drops it when it is armed already.</summary>
        public void ToggleSkill()
        {
            CleanupSim sim = Session.Battle;
            if (sim == null)
            {
                return;
            }

            if (SkillArmed)
            {
                SkillArmed = false;
                hud.Refresh();

                return;
            }

            string refusal = sim.SkillRefusal();
            if (refusal != null)
            {
                hud.ShowNotice(refusal);

                return;
            }

            SkillArmed = true;
            hud.Refresh();
        }

        public void ToggleBattlePause()
        {
            BattlePaused = !BattlePaused;
            hud.Refresh();
        }

        public void ToggleBattleSpeed()
        {
            BattleSpeed = BattleSpeed == 1 ? 2 : 1;
            hud.Refresh();
        }

        /// <summary>
        /// Swaps the island for the battle level and back. In battle the camera
        /// follows the player's island, and WASD steers the island instead of the camera.
        /// </summary>
        private void SetBattleShown(bool show)
        {
            if (show == ShowBattle)
            {
                return;
            }

            ShowBattle = show;
            SkillArmed = false;
            BattlePaused = false;
            BattleSpeed = 1;
            _accumulator = 0f;

            if (show)
            {
                _homeOrbit = cameraRig.Current;
                battleView.Bind(Session.Battle, Props.Hex(Session.Human.Color));
                Vector3 center = battleView.PlayerCenter(1f);
                cameraRig.Current = new CameraRig.Orbit
                {
                    Target = center,
                    Distance = 30f,
                    Yaw = 0f,
                    Pitch = 58f,
                    MinDistance = 8f,
                    MaxDistance = 90f,
                    PanLimit = 100000f,
                    MinPitch = 25f,
                    MaxPitch = 88f,
                };
                cameraRig.Follow = center;
            }
            else
            {
                cameraRig.Follow = null;
                battleView.Clear();
                if (_homeOrbit.HasValue)
                {
                    cameraRig.Current = _homeOrbit.Value;
                }
            }

            ApplyPages();
        }

        /// <summary>Only the page on screen is active: the island, the globe or the battle level.</summary>
        private void ApplyPages()
        {
            islandView.gameObject.SetActive(!ShowWorld && !ShowBattle);
            globeView.gameObject.SetActive(ShowWorld && !ShowBattle);
            battleView.gameObject.SetActive(ShowBattle);
        }

        /// <summary>
        /// One frame of the battle: the helm, whole ticks of the sim from an
        /// accumulator, the skill's target, and the picture between the last two ticks.
        /// </summary>
        private void UpdateBattle(Mouse mouse, Vector2 pointer, bool overUi)
        {
            CleanupSim sim = Session.Battle;
            if (sim == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool isRunning = sim.Status == SimStatus.Running;

            if (keyboard != null && isRunning)
            {
                // The helm follows the view: W sails away from the camera.
                float right = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                float ahead = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                Vector3 heading = Quaternion.Euler(0f, cameraRig.Yaw, 0f) * new Vector3(right, 0f, ahead);
                // The sim's plane has y down: world z is its -y.
                sim.SetInput(heading.x, -heading.z);

                if (keyboard.qKey.wasPressedThisFrame)
                {
                    ToggleSkill();
                }

                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    ToggleBattlePause();
                }

                if (keyboard.escapeKey.wasPressedThisFrame && SkillArmed)
                {
                    SkillArmed = false;
                    hud.Refresh();
                }
            }

            // A modal over the battle holds the clock.
            bool isHeld = BattlePaused || PendingSpin != null || PendingTrail != null;
            if (isRunning && !isHeld)
            {
                _accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f) * BattleSpeed;
                int ticks = Mathf.Min(MaxTicksPerFrame, Mathf.FloorToInt(_accumulator / (float)CleanupSim.TickSeconds));
                _accumulator -= ticks * (float)CleanupSim.TickSeconds;
                _accumulator = Mathf.Min(_accumulator, (float)CleanupSim.TickSeconds);
                Session.StepBattle(ticks);
                AnnounceBattleEvents(sim);
            }
            else if (!isRunning)
            {
                // The level has ended: the session takes its result.
                Session.StepBattle(0);
            }

            if (sim.Status != SimStatus.Running)
            {
                SkillArmed = false;
            }

            if (SkillArmed && !overUi && battleView.Pick(worldCamera.ScreenPointToRay(pointer), out int island, out int hex))
            {
                battleView.SetTarget(island, hex);
                if (mouse.leftButton.wasPressedThisFrame && Session.CastShatter(island, hex))
                {
                    SkillArmed = false;
                    battleView.SetTarget(-1, -1);
                    hud.Refresh();
                }
            }
            else
            {
                battleView.SetTarget(-1, -1);
            }

            float alpha = sim.Status == SimStatus.Running ? Mathf.Clamp01(_accumulator / (float)CleanupSim.TickSeconds) : 1f;
            cameraRig.Follow = battleView.PlayerCenter(alpha);
            battleView.Draw(alpha, worldCamera);

            if (Time.unscaledTime >= _nextBattleHud)
            {
                _nextBattleHud = Time.unscaledTime + 0.2f;
                hud.RefreshBattle();
            }
        }

        /// <summary>What happened to the enemy islands since the last frame, as notices.</summary>
        private void AnnounceBattleEvents(CleanupSim sim)
        {
            foreach (SimEvent simEvent in sim.DrainEvents())
            {
                switch (simEvent.Type)
                {
                    case SimEventType.Cleared:
                        hud.ShowNotice(sim.Islands[simEvent.Island].Label + " зачищен: пристыкуйтесь к нему, пока он не уплыл");
                        break;
                    case SimEventType.Attached:
                        hud.ShowNotice(sim.Islands[simEvent.Island].Label + " присоединён: +" + simEvent.Joined + " гексов");
                        break;
                    case SimEventType.Lost:
                        hud.ShowNotice(sim.Islands[simEvent.Island].Label + " потерян");
                        break;
                    case SimEventType.Hit:
                        // Red over the player's own, pale over the enemy's.
                        hud.SpawnFloater(battleView.EffectPoint(simEvent.X, simEvent.Y, 0.9f), "−" + simEvent.Amount, simEvent.Side == CleanupSide.Player);
                        break;
                    case SimEventType.Death:
                    case SimEventType.Razed:
                    case SimEventType.Poison:
                    case SimEventType.Crumble:
                    case SimEventType.Ferry:
                        battleView.PlayEffect(simEvent);
                        break;
                }
            }
        }

        private void RenderScene()
        {
            // The battle is drawn every frame from the sim, not from the session's changes.
            if (ShowBattle)
            {
                return;
            }

            if (ShowWorld)
            {
                // The cells the island can reach are lit only while the flight is open.
                _reachable.Clear();
                if (Session.IsExplorationOpen)
                {
                    _reachable.UnionWith(WorldRules.ReachableCellIds(Session.World, Session.Human.CellId, Session.MovedThisTurn));
                }

                globeView.Render(Session.World, Session.Players, CellMarkOf);

                return;
            }

            islandView.Render(ViewedPlayer, MarkOf);
        }

        /// <summary>
        /// Swaps the island for the globe and back. Each keeps its own camera, so
        /// coming back finds the view as it was left.
        /// </summary>
        private void SetWorldShown(bool show)
        {
            if (show != ShowWorld)
            {
                if (show)
                {
                    _islandOrbit = cameraRig.Current;
                    _islandSunRotation = sun.transform.rotation;
                    WorldCell home = Session.World.Get(Session.Human.CellId);
                    Vector3 direction = home != null ? globeView.DirectionOf(home) : Vector3.back;
                    // Seen a little from the side, so what stands on the cells reads as standing.
                    Vector3 tiltAxis = Vector3.Cross(direction, Vector3.up);
                    if (tiltAxis.sqrMagnitude > 0.01f)
                    {
                        direction = Quaternion.AngleAxis(-14f, tiltAxis.normalized) * direction;
                    }

                    cameraRig.Current = _globeOrbit ?? CameraRig.AroundSphere(globeView.transform.position, direction, GlobeView.Radius);
                }
                else
                {
                    _globeOrbit = cameraRig.Current;
                    sun.transform.rotation = _islandSunRotation;
                    if (_islandOrbit.HasValue)
                    {
                        cameraRig.Current = _islandOrbit.Value;
                    }
                }
            }

            ShowWorld = show;
            HoveredHexId = null;
            HoveredCellId = null;
            ApplyPages();
        }

        private CellMark CellMarkOf(WorldCell cell)
        {
            if (cell.Id == SelectedCellId)
            {
                return CellMark.Selected;
            }

            if (cell.Id == HoveredCellId)
            {
                return CellMark.Hovered;
            }

            return _reachable.Contains(cell.Id) ? CellMark.Reachable : CellMark.None;
        }

        /// <summary>The pointer over the globe: hovering names a cell, a click selects it.</summary>
        private void UpdateWorld(Mouse mouse, Vector2 pointer, bool overUi)
        {
            string hovered = overUi ? null : globeView.Pick(worldCamera.ScreenPointToRay(pointer));
            if (hovered != HoveredCellId)
            {
                HoveredCellId = hovered;
                RenderScene();
                hud.RefreshHexPanel();
            }

            if (mouse.leftButton.wasPressedThisFrame && !overUi && hovered != null)
            {
                SelectedCellId = hovered;
                Redraw();
            }
        }

        private void Update()
        {
            // Behind the open menu the island turns slowly; the game takes no input until the menu has gone.
            bool isMenuOpen = mainMenu != null && mainMenu.IsOpen;
            bool isGuideOpen = learnGuide != null && learnGuide.IsOpen;
            cameraRig.Locked = isMenuOpen || isGuideOpen;
            cameraRig.Spin = isMenuOpen ? MenuSpin : 0f;
            if ((mainMenu != null && mainMenu.HoldsInput) || isGuideOpen)
            {
                // A card over the battle holds its clock, but the level stays on screen.
                if (isGuideOpen && ShowBattle && Session != null && Session.Battle != null)
                {
                    cameraRig.Follow = battleView.PlayerCenter(1f);
                    battleView.Draw(1f, worldCamera);
                }

                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || Session == null)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            bool overUi = hud.IsPointerOverUi(pointer);
            cameraRig.PointerOverUi = overUi;

            if (ShowBattle)
            {
                UpdateBattle(mouse, pointer, overUi);

                return;
            }

            if (ShowWorld)
            {
                // One sun lights half a planet. Over the globe it follows the camera,
                // so the side the player looks at is always the lit one.
                sun.transform.rotation = worldCamera.transform.rotation * Quaternion.Euler(20f, -28f, 0f);
                UpdateWorld(mouse, pointer, overUi);

                return;
            }

            UpdateReveal();
            if (Collecting)
            {
                return;
            }

            string hovered = null;
            if (!overUi && Physics.Raycast(worldCamera.ScreenPointToRay(pointer), out RaycastHit hit, 500f))
            {
                HexHandle handle = hit.collider.GetComponent<HexHandle>();
                hovered = handle != null ? handle.HexId : null;
            }

            if (hovered != HoveredHexId)
            {
                HoveredHexId = hovered;
                RenderScene();
                hud.RefreshHexPanel();
            }

            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                OnHexClicked(hovered);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ClearModes();
                Redraw();
            }
        }

        private void OnHexClicked(string hexId)
        {
            if (hexId == null)
            {
                SelectedHexId = null;
                Redraw();

                return;
            }

            // On a rival's island a click only picks a hex to read about.
            if (IsReadonly)
            {
                SelectedHexId = SelectedHexId == hexId ? null : hexId;
                Redraw();

                return;
            }

            if (Session.Stage == GameStage.Setup)
            {
                Session.PlaceStronghold(hexId);

                return;
            }

            if (Session.IsBuildingAllowed && SoilMode)
            {
                PickSoilHex(hexId);

                return;
            }

            if (Session.IsBuildingAllowed && DemolishMode)
            {
                Session.Demolish(hexId);

                return;
            }

            if (Session.IsBuildingAllowed && ArmedBuilding.HasValue)
            {
                Building building = Buildings.Get(ArmedBuilding.Value);
                Session.Build(hexId, building.Id);

                // The card stays armed for the next hex, unless the player just
                // spent the last of what it costs.
                if (!Buildings.CanAfford(Session.Human.Resources, building, Session.Effects.StoneDiscount))
                {
                    ArmedBuilding = null;
                    Redraw();
                }

                return;
            }

            SelectedHexId = SelectedHexId == hexId ? null : hexId;
            Redraw();
        }

        /// <summary>A hex click while the soil cleansing is armed: the sacrifice first, then the target.</summary>
        private void PickSoilHex(string hexId)
        {
            Player player = Session.Human;
            HexTile hex = player.FindHex(hexId);
            if (hex == null)
            {
                return;
            }

            if (SoilSacrificeHexId == null)
            {
                string refusal = BuildRules.SacrificeRefusal(player, hex);
                if (refusal != null)
                {
                    hud.ShowNotice(refusal);

                    return;
                }

                SoilSacrificeHexId = hexId;
                Redraw();

                return;
            }

            // A second click on the marked hex takes the mark back.
            if (hexId == SoilSacrificeHexId)
            {
                SoilSacrificeHexId = null;
                Redraw();

                return;
            }

            string sacrificeHexId = SoilSacrificeHexId;
            if (Session.CleanseSoil(sacrificeHexId, hexId))
            {
                // The destroyed hex is gone: nothing may point at it any more.
                if (SelectedHexId == sacrificeHexId)
                {
                    SelectedHexId = null;
                }

                ClearModes();
                Redraw();
            }
        }

        private void ClearModes()
        {
            ArmedBuilding = null;
            DemolishMode = false;
            SoilMode = false;
            SoilSacrificeHexId = null;
            SelectedHexId = null;
        }

        private HexMark MarkOf(HexTile hex)
        {
            if (IsReadonly)
            {
                return hex.Id == SelectedHexId ? HexMark.Selected : hex.Id == HoveredHexId ? HexMark.Hovered : HexMark.None;
            }

            if (SoilMode && hex.Id == SoilSacrificeHexId)
            {
                return HexMark.Sacrifice;
            }

            if (hex.Id == SelectedHexId)
            {
                return HexMark.Selected;
            }

            if (hex.Id == HoveredHexId)
            {
                return HexMark.Hovered;
            }

            if (ArmedBuilding.HasValue && Session.IsBuildingAllowed)
            {
                Building building = Buildings.Get(ArmedBuilding.Value);
                if (BuildRules.BuildRefusal(Session.Human, hex, building, Session.Effects.StoneDiscount) == null)
                {
                    return HexMark.Allowed;
                }
            }

            return HexMark.None;
        }

        private static string DescribePayouts(List<TaxPayout> payouts)
        {
            IEnumerable<string> parts = payouts
                .Where(payout => payout.Amount > 0)
                .GroupBy(payout => payout.Resource)
                .Select(group => "+" + group.Sum(payout => payout.Amount) + " " + ResourceTable.Get(group.Key).Label);
            string text = string.Join(", ", parts);

            return text.Length > 0 ? "Собрано: " + text : "Здания ничего не дали";
        }
    }
}
