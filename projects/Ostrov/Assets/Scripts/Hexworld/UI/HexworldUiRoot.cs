using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The hub of the interface. It owns the only reference to the game, listens to
/// every game event, feeds the panels and performs every action the panels ask
/// for. A panel never touches the game itself, so every rule check lives here.
/// </summary>
/// <remarks>
/// Subscribing happens in Awake, which runs before the runner creates the game
/// in Start. A game that already exists is picked up instead, so the hub also
/// works when it is switched on late or driven from an editor script.
/// </remarks>
public sealed class HexworldUiRoot : MonoBehaviour
{
    /// <summary>The entry point of the demo.</summary>
    [SerializeField]
    private HexworldGameRunner _runner;

    /// <summary>The stockpile bar.</summary>
    [SerializeField]
    private HexworldResourceBar _resourceBar;

    /// <summary>The strip at the top of the screen.</summary>
    [SerializeField]
    private HexworldTurnHeader _header;

    /// <summary>The harvest panel.</summary>
    [SerializeField]
    private HexworldDicePanel _dicePanel;

    /// <summary>The build panel.</summary>
    [SerializeField]
    private HexworldBuildPanel _buildPanel;

    /// <summary>The combat panel.</summary>
    [SerializeField]
    private HexworldCombatPanel _combatPanel;

    /// <summary>The event feed.</summary>
    [SerializeField]
    private HexworldEventLog _log;

    /// <summary>The screen that closes the game.</summary>
    [SerializeField]
    private HexworldVictoryScreen _victory;

    /// <summary>The game being shown, or null.</summary>
    private HexworldGame _game;

    /// <summary>True once the hub is listening to the runner.</summary>
    private bool _initialized;

    /// <summary>True while the AI plays its turn.</summary>
    private bool _aiPlaying;

    /// <summary>Scratch list for the legal target highlight.</summary>
    private readonly List<HexCoord> _targets = new List<HexCoord>();

    /// <summary>The game being shown, or null before the first one exists.</summary>
    public HexworldGame Game
    {
        get { return _game; }
    }

    /// <summary>The entry point of the demo.</summary>
    public HexworldGameRunner Runner
    {
        get { return _runner; }
    }

    /// <summary>The harvest panel.</summary>
    public HexworldDicePanel DicePanel
    {
        get { return _dicePanel; }
    }

    /// <summary>The build panel.</summary>
    public HexworldBuildPanel BuildPanel
    {
        get { return _buildPanel; }
    }

    /// <summary>The combat panel.</summary>
    public HexworldCombatPanel CombatPanel
    {
        get { return _combatPanel; }
    }

    /// <summary>The event feed.</summary>
    public HexworldEventLog Log
    {
        get { return _log; }
    }

    /// <summary>The screen that closes the game.</summary>
    public HexworldVictoryScreen Victory
    {
        get { return _victory; }
    }

    /// <summary>True while the human player may act.</summary>
    public bool IsHumanTurn
    {
        get
        {
            return _game != null
                && !_game.IsGameOver
                && !_aiPlaying
                && _game.CurrentPlayerIndex == HexworldGameRunner.HumanPlayerIndex;
        }
    }

    /// <summary>
    /// Starts listening to the runner and picks up a game that already runs.
    /// Calling it twice does nothing the second time.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        if (_runner == null)
        {
            _runner = FindAnyObjectByType<HexworldGameRunner>();
        }

        if (_runner == null)
        {
            Debug.LogWarning("[HexworldUiRoot] На сцене нет HexworldGameRunner.");
            return;
        }

        _initialized = true;

        _runner.GameCreated += Attach;

        if (_runner.AiDriver != null)
        {
            _runner.AiDriver.AiTurnStarted += HandleAiTurnStarted;
            _runner.AiDriver.AiTurnFinished += HandleAiTurnFinished;
        }

        if (_runner.InputController != null)
        {
            _runner.InputController.TileClicked += HandleTileClicked;
        }

        if (_runner.Game != null)
        {
            Attach(_runner.Game);
        }
    }

    /// <summary>
    /// Points the whole interface at a game.
    /// </summary>
    /// <param name="game">The new game.</param>
    public void Attach(HexworldGame game)
    {
        Detach();

        _game = game;
        _aiPlaying = false;

        if (_game == null)
        {
            return;
        }

        _game.ResourcesChanged += HandleResourcesChanged;
        _game.BuildingBuilt += HandleBuildingBuilt;
        _game.BuildingDestroyed += HandleBuildingDestroyed;
        _game.TileOwnerChanged += HandleTileOwnerChanged;
        _game.TileVoided += HandleTileVoided;
        _game.TerrainChanged += HandleTerrainChanged;
        _game.DiceRolled += HandleDiceRolled;
        _game.PhaseChanged += HandlePhaseChanged;
        _game.DisasterStruck += HandleDisaster;
        _game.CombatResolved += HandleCombatResolved;
        _game.GameOver += HandleGameOver;

        if (_victory != null)
        {
            _victory.Hide();
        }

        if (_log != null)
        {
            _log.Clear();
            _log.Append("Новая партия. Вы играете синими.", HexworldUiTheme.AccentColor);
        }

        if (_resourceBar != null)
        {
            _resourceBar.Bind(_game);
        }

        if (_header != null)
        {
            _header.Bind(_game);
        }

        if (_dicePanel != null)
        {
            _dicePanel.Bind(_game);
        }

        if (_buildPanel != null)
        {
            _buildPanel.Bind(_game);
        }

        if (_combatPanel != null)
        {
            _combatPanel.Bind(_game);
        }

        RefreshAll();
    }

    /// <summary>
    /// Stops listening to the running game.
    /// </summary>
    public void Detach()
    {
        if (_game == null)
        {
            return;
        }

        _game.ResourcesChanged -= HandleResourcesChanged;
        _game.BuildingBuilt -= HandleBuildingBuilt;
        _game.BuildingDestroyed -= HandleBuildingDestroyed;
        _game.TileOwnerChanged -= HandleTileOwnerChanged;
        _game.TileVoided -= HandleTileVoided;
        _game.TerrainChanged -= HandleTerrainChanged;
        _game.DiceRolled -= HandleDiceRolled;
        _game.PhaseChanged -= HandlePhaseChanged;
        _game.DisasterStruck -= HandleDisaster;
        _game.CombatResolved -= HandleCombatResolved;
        _game.GameOver -= HandleGameOver;
        _game = null;
    }

    /// <summary>
    /// Rereads the whole game and repaints every panel.
    /// </summary>
    public void RefreshAll()
    {
        if (_resourceBar != null)
        {
            _resourceBar.Refresh();
        }

        if (_header != null)
        {
            _header.Refresh();
            _header.SetAiBadgeVisible(_game != null && !_game.IsGameOver && !IsHumanTurn);
        }

        bool human = IsHumanTurn;

        if (_dicePanel != null)
        {
            _dicePanel.gameObject.SetActive(IsPhase(HexworldPhase.Harvest));
            _dicePanel.SetInteractable(human);
        }

        if (_buildPanel != null)
        {
            _buildPanel.gameObject.SetActive(IsPhase(HexworldPhase.Build));
            _buildPanel.SetInteractable(human);
        }

        if (_combatPanel != null)
        {
            _combatPanel.gameObject.SetActive(IsPhase(HexworldPhase.Combat));
            _combatPanel.SetInteractable(human);
        }

        RefreshValidTargets();
    }

    /// <summary>
    /// Recomputes which tiles the player may click right now and hands them to
    /// the board highlight.
    /// </summary>
    public void RefreshValidTargets()
    {
        HexworldInputController input = _runner == null ? null : _runner.InputController;
        if (input == null)
        {
            return;
        }

        _targets.Clear();

        if (_game != null && IsHumanTurn)
        {
            if (_game.CurrentPhase == HexworldPhase.Build && _buildPanel != null)
            {
                if (_buildPanel.RubbleOrderSelected)
                {
                    CollectRubbleTargets();
                }
                else if (_buildPanel.SelectedBuilding != HexBuildingType.None)
                {
                    CollectBuildTargets(_buildPanel.SelectedBuilding);
                }
            }
            else if (_game.CurrentPhase == HexworldPhase.Combat)
            {
                CollectCaptureTargets();
            }
        }

        input.SetValidTargets(_targets);
    }

    // ------------------------------------------------------------------
    // Actions the panels ask for
    // ------------------------------------------------------------------

    /// <summary>
    /// Rerolls the dice the player marked.
    /// </summary>
    /// <param name="diceIndices">Indices into the running roll.</param>
    public void RerollDice(IReadOnlyList<int> diceIndices)
    {
        if (_game == null || !IsHumanTurn)
        {
            return;
        }

        string reason;
        if (!_game.CanReroll(diceIndices, out reason))
        {
            ReportRefusal(reason);
            return;
        }

        _game.Reroll(diceIndices);
        ClearRefusal();
    }

    /// <summary>
    /// Applies the harvest and moves on to the build phase.
    /// </summary>
    public void ConfirmHarvest()
    {
        if (_game == null || !IsHumanTurn || _game.CurrentPhase != HexworldPhase.Harvest)
        {
            return;
        }

        if (_resourceBar != null)
        {
            _resourceBar.BeginHarvestDelta();
        }

        _game.ConfirmHarvest();

        if (_resourceBar != null)
        {
            _resourceBar.EndHarvestDelta();
        }

        ClearRefusal();
    }

    /// <summary>
    /// Raises a building on a tile.
    /// </summary>
    /// <param name="coord">Where to build.</param>
    /// <param name="type">What to build.</param>
    public void BuildAt(HexCoord coord, HexBuildingType type)
    {
        if (_game == null || !IsHumanTurn)
        {
            return;
        }

        string reason;
        if (!_game.CanBuild(coord, type, out reason))
        {
            ReportRefusal(reason);
            return;
        }

        _game.Build(coord, type);
        ClearRefusal();
    }

    /// <summary>
    /// Clears the rubble on a tile.
    /// </summary>
    /// <param name="coord">Which tile to clear.</param>
    public void ClearRubbleAt(HexCoord coord)
    {
        if (_game == null || !IsHumanTurn)
        {
            return;
        }

        string reason;
        if (!_game.CanClearRubble(coord, out reason))
        {
            ReportRefusal(reason);
            return;
        }

        _game.ClearRubble(coord);
        ClearRefusal();
    }

    /// <summary>
    /// Closes the build phase.
    /// </summary>
    public void EndBuildPhase()
    {
        if (_game == null || !IsHumanTurn || _game.CurrentPhase != HexworldPhase.Build)
        {
            return;
        }

        _game.EndBuildPhase();
        ClearRefusal();
    }

    /// <summary>
    /// Attacks a tile.
    /// </summary>
    /// <param name="coord">The tile to capture.</param>
    /// <param name="soldiers">How many soldiers to spend.</param>
    public void Capture(HexCoord coord, int soldiers)
    {
        if (_game == null || !IsHumanTurn)
        {
            return;
        }

        string reason;
        if (!_game.CanCapture(coord, soldiers, out reason))
        {
            ReportRefusal(reason);
            return;
        }

        _game.Capture(coord, soldiers);
        ClearRefusal();
    }

    /// <summary>
    /// Hands the turn over to the opponent.
    /// </summary>
    public void EndTurn()
    {
        if (_game == null || !IsHumanTurn || _game.CurrentPhase != HexworldPhase.Combat)
        {
            return;
        }

        _game.EndTurn();
        ClearRefusal();
    }

    /// <summary>
    /// Throws the running game away and opens a fresh one.
    /// </summary>
    public void Restart()
    {
        if (_runner != null)
        {
            _runner.Restart();
        }
    }

    /// <summary>
    /// Writes a refusal into the header.
    /// </summary>
    /// <param name="reason">Why the action was refused.</param>
    public void ReportRefusal(string reason)
    {
        if (_header != null && !string.IsNullOrEmpty(reason))
        {
            _header.SetStatus(HexworldUiTheme.Translate(reason), HexworldUiTheme.BadColor);
        }
    }

    /// <summary>
    /// Brings the normal hint back into the header.
    /// </summary>
    public void ClearRefusal()
    {
        if (_header != null)
        {
            _header.ClearStatus();
        }
    }

    /// <summary>
    /// Routes a click on the island to the panel of the running phase.
    /// </summary>
    /// <param name="coord">Which tile was clicked.</param>
    public void HandleTileClicked(HexCoord coord)
    {
        if (_game == null || !IsHumanTurn)
        {
            return;
        }

        switch (_game.CurrentPhase)
        {
            case HexworldPhase.Build:
                HandleBuildClick(coord);
                break;
            case HexworldPhase.Combat:
                if (_combatPanel != null)
                {
                    _combatPanel.SetTarget(coord);
                }

                break;
        }
    }

    // ------------------------------------------------------------------
    // Unity lifetime
    // ------------------------------------------------------------------

    /// <summary>
    /// Subscribes before the runner opens the first game.
    /// </summary>
    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// Drops every subscription.
    /// </summary>
    private void OnDestroy()
    {
        Detach();

        if (_runner != null)
        {
            _runner.GameCreated -= Attach;

            if (_runner.AiDriver != null)
            {
                _runner.AiDriver.AiTurnStarted -= HandleAiTurnStarted;
                _runner.AiDriver.AiTurnFinished -= HandleAiTurnFinished;
            }

            if (_runner.InputController != null)
            {
                _runner.InputController.TileClicked -= HandleTileClicked;
            }
        }

        _initialized = false;
    }

    // ------------------------------------------------------------------
    // Game events
    // ------------------------------------------------------------------

    /// <summary>
    /// Takes a stockpile change.
    /// </summary>
    /// <param name="data">Whose stockpile changed and by how much.</param>
    private void HandleResourcesChanged(HexworldResourcesChangedEvent data)
    {
        if (_resourceBar != null)
        {
            _resourceBar.HandleResourcesChanged(data);
        }
    }

    /// <summary>
    /// Logs a new building and repaints the panels.
    /// </summary>
    /// <param name="data">Who built what and where.</param>
    private void HandleBuildingBuilt(HexworldBuildingBuiltEvent data)
    {
        AppendLog(
            string.Format(
                "Построен {0} на {1} — {2}",
                HexworldUiTheme.BuildingName(data.BuildingType),
                data.Coord,
                HexworldUiTheme.SideName(data.PlayerIndex)),
            HexworldUiTheme.SideColor(data.PlayerIndex));

        if (_buildPanel != null)
        {
            _buildPanel.Refresh();
        }

        RefreshValidTargets();
    }

    /// <summary>
    /// Logs a building that left the board.
    /// </summary>
    /// <param name="data">Who lost what and why.</param>
    private void HandleBuildingDestroyed(HexworldBuildingDestroyedEvent data)
    {
        AppendLog(
            string.Format(
                "Разрушен {0} на {1} ({2}) — {3}",
                HexworldUiTheme.BuildingName(data.BuildingType),
                data.Coord,
                LossReason(data.Reason),
                HexworldUiTheme.SideName(data.PlayerIndex)),
            HexworldUiTheme.BadColor);
    }

    /// <summary>
    /// Logs a tile that changed hands.
    /// </summary>
    /// <param name="data">Which tile and between whom.</param>
    private void HandleTileOwnerChanged(HexworldTileOwnerChangedEvent data)
    {
        AppendLog(
            string.Format(
                "Тайл {0} переходит к {1}",
                data.Coord,
                HexworldUiTheme.SideName(data.NewOwner)),
            HexworldUiTheme.SideColor(data.NewOwner));
    }

    /// <summary>
    /// Logs a tile that dropped into the Ether.
    /// </summary>
    /// <param name="data">Which tile and whose it was.</param>
    private void HandleTileVoided(HexworldTileVoidedEvent data)
    {
        AppendLog("Тайл " + data.Coord + " ушёл в Эфир", HexworldUiTheme.BadColor);
    }

    /// <summary>
    /// Logs cleared rubble.
    /// </summary>
    /// <param name="data">Which tile changed and how.</param>
    private void HandleTerrainChanged(HexworldTerrainChangedEvent data)
    {
        AppendLog(
            string.Format(
                "Завал на {0} расчищен, теперь {1}",
                data.Coord,
                HexworldUiTheme.TerrainName(data.NewTerrain)),
            HexworldUiTheme.TextColor);

        if (_buildPanel != null)
        {
            _buildPanel.Refresh();
        }

        RefreshValidTargets();
    }

    /// <summary>
    /// Repaints the dice and logs the roll.
    /// </summary>
    /// <param name="data">Who rolled and what came up.</param>
    private void HandleDiceRolled(HexworldDiceRolledEvent data)
    {
        if (_dicePanel != null)
        {
            _dicePanel.Refresh();
        }

        AppendLog(
            string.Format(
                "{0}: {1} {2} куб.",
                HexworldUiTheme.SideName(data.PlayerIndex),
                data.IsReroll ? "реролл," : "бросок,",
                data.Dice.Count),
            HexworldUiTheme.MutedColor);
    }

    /// <summary>
    /// Switches the panels over to the new phase.
    /// </summary>
    /// <param name="data">Whose turn it is and which phase started.</param>
    private void HandlePhaseChanged(HexworldPhaseChangedEvent data)
    {
        if (_buildPanel != null)
        {
            _buildPanel.ClearSelection();
        }

        if (_combatPanel != null)
        {
            _combatPanel.SetTarget(null);
        }

        if (_runner != null && _runner.InputController != null)
        {
            _runner.InputController.ClearSelection();
        }

        if (data.Phase == HexworldPhase.Harvest && _resourceBar != null)
        {
            _resourceBar.ClearDeltas();
        }

        if (data.Phase == HexworldPhase.Harvest)
        {
            AppendLog(
                string.Format("— Ход {0}: {1} —", data.TurnNumber, HexworldUiTheme.SideName(data.PlayerIndex)),
                HexworldUiTheme.SideColor(data.PlayerIndex));
        }

        ClearRefusal();
        RefreshAll();
    }

    /// <summary>
    /// Logs a skull disaster.
    /// </summary>
    /// <param name="data">Who suffered it and what it cost.</param>
    private void HandleDisaster(HexworldDisasterEvent data)
    {
        string text = string.Format(
            "Бедствие у {0}: {1} чер.",
            HexworldUiTheme.SideName(data.PlayerIndex).ToLowerInvariant(),
            data.Skulls);

        if (data.DestroyedBuilding)
        {
            text += ", здание разрушено";
        }

        if (data.VoidedTile)
        {
            text += ", тайл ушёл в Эфир";
        }

        AppendLog(text, HexworldUiTheme.BadColor);
    }

    /// <summary>
    /// Logs an attack and shows its record on the combat panel.
    /// </summary>
    /// <param name="result">What the attack rolled.</param>
    private void HandleCombatResolved(HexworldCombatResult result)
    {
        if (_combatPanel != null && result.Attacker == HexworldGameRunner.HumanPlayerIndex)
        {
            _combatPanel.ShowResult(result);
        }

        string text = result.WasNeutral
            ? string.Format(
                "{0}: занят ничей тайл {1} за {2} солд.",
                HexworldUiTheme.SideName(result.Attacker),
                result.Coord,
                result.SoldiersSpent)
            : string.Format(
                "{0}: атака {1} против защиты {2} на {3} — {4}",
                HexworldUiTheme.SideName(result.Attacker),
                result.AttackPower,
                result.DefensePower,
                result.Coord,
                result.Captured ? "захвачено" : "отбито");

        AppendLog(text, result.Captured
            ? HexworldUiTheme.SideColor(result.Attacker)
            : HexworldUiTheme.MutedColor);

        if (_combatPanel != null)
        {
            _combatPanel.Refresh();
        }

        RefreshValidTargets();
    }

    /// <summary>
    /// Raises the victory screen.
    /// </summary>
    /// <param name="data">Who won, how and on which turn.</param>
    private void HandleGameOver(HexworldGameOverEvent data)
    {
        AppendLog(
            string.Format(
                "Партия окончена: побеждает {0}",
                HexworldUiTheme.SideName(data.Winner)),
            HexworldUiTheme.AccentColor);

        if (_victory != null)
        {
            _victory.Show(data);
        }

        RefreshAll();
    }

    // ------------------------------------------------------------------
    // AI turn
    // ------------------------------------------------------------------

    /// <summary>
    /// Takes the controls away while the AI plays.
    /// </summary>
    private void HandleAiTurnStarted()
    {
        _aiPlaying = true;
        AppendLog("Ход противника", HexworldUiTheme.AiColor);
        RefreshAll();
    }

    /// <summary>
    /// Gives the controls back once the AI is done.
    /// </summary>
    private void HandleAiTurnFinished()
    {
        _aiPlaying = false;
        RefreshAll();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Performs a click on the island during the build phase.
    /// </summary>
    /// <param name="coord">Which tile was clicked.</param>
    private void HandleBuildClick(HexCoord coord)
    {
        if (_buildPanel == null)
        {
            return;
        }

        if (_buildPanel.RubbleOrderSelected)
        {
            ClearRubbleAt(coord);
            _buildPanel.Refresh();
            RefreshValidTargets();
            return;
        }

        if (_buildPanel.SelectedBuilding == HexBuildingType.None)
        {
            ReportRefusal("Сначала выберите здание в панели справа.");
            return;
        }

        BuildAt(coord, _buildPanel.SelectedBuilding);
        _buildPanel.Refresh();
        RefreshValidTargets();
    }

    /// <summary>
    /// Fills the target list with every tile that accepts a building.
    /// </summary>
    /// <param name="type">Which building to place.</param>
    private void CollectBuildTargets(HexBuildingType type)
    {
        foreach (HexTile tile in _game.Board.Tiles)
        {
            if (_game.CanBuild(tile.Coord, type))
            {
                _targets.Add(tile.Coord);
            }
        }
    }

    /// <summary>
    /// Fills the target list with every tile whose rubble may be cleared.
    /// </summary>
    private void CollectRubbleTargets()
    {
        foreach (HexTile tile in _game.Board.Tiles)
        {
            if (_game.CanClearRubble(tile.Coord))
            {
                _targets.Add(tile.Coord);
            }
        }
    }

    /// <summary>
    /// Fills the target list with every tile the player may attack.
    /// </summary>
    private void CollectCaptureTargets()
    {
        foreach (HexTile tile in _game.Board.Tiles)
        {
            if (_game.CanCapture(tile.Coord, _game.GetMinimumSoldiers(tile.Coord)))
            {
                _targets.Add(tile.Coord);
            }
        }
    }

    /// <summary>
    /// Tells whether a phase of the running game is open.
    /// </summary>
    /// <param name="phase">The phase to test.</param>
    /// <returns>True when the game runs that phase right now.</returns>
    private bool IsPhase(HexworldPhase phase)
    {
        return _game != null && !_game.IsGameOver && _game.CurrentPhase == phase;
    }

    /// <summary>
    /// Adds one line to the event feed.
    /// </summary>
    /// <param name="text">What to write.</param>
    /// <param name="color">Colour of the line.</param>
    private void AppendLog(string text, Color color)
    {
        if (_log != null)
        {
            _log.Append(text, color);
        }
    }

    /// <summary>
    /// Writes why a building left the board.
    /// </summary>
    /// <param name="reason">The cause.</param>
    /// <returns>The cause in plain words.</returns>
    private static string LossReason(HexworldBuildingLossReason reason)
    {
        switch (reason)
        {
            case HexworldBuildingLossReason.Disaster: return "бедствие";
            case HexworldBuildingLossReason.Combat: return "бой";
            default: return "тайл ушёл в Эфир";
        }
    }
}
