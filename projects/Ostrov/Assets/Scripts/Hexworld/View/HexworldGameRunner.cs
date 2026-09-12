using System;
using UnityEngine;

/// <summary>
/// The entry point of the demo. It creates the game, hands it to the board
/// view, the input and the AI driver, and only then opens the first turn.
/// </summary>
/// <remarks>
/// The game is created in <see cref="Start"/>, not in Awake, so every other
/// component has had its Awake and can subscribe to <see cref="GameCreated"/>
/// before the opening dice are rolled. A component that appears later can read
/// <see cref="Game"/> instead.
/// </remarks>
public sealed class HexworldGameRunner : MonoBehaviour
{
    /// <summary>Index of the human player.</summary>
    public const int HumanPlayerIndex = 0;

    /// <summary>Index of the AI player.</summary>
    public const int AiPlayerIndex = 1;

    /// <summary>Seed used when <see cref="_useRandomSeed"/> is off.</summary>
    [Header("Game")]
    [SerializeField]
    private int _seed = 12345;

    /// <summary>True to pick a fresh seed for every game.</summary>
    [SerializeField]
    private bool _useRandomSeed;

    /// <summary>True to open a game as soon as the scene runs.</summary>
    [SerializeField]
    private bool _startOnPlay = true;

    /// <summary>How long the AI waits between its phases, in seconds.</summary>
    [SerializeField]
    private float _aiPhaseDelay = 0.8f;

    /// <summary>The island on screen.</summary>
    [Header("Systems")]
    [SerializeField]
    private HexBoardView _boardView;

    /// <summary>The mouse picker.</summary>
    [SerializeField]
    private HexworldInputController _inputController;

    /// <summary>The AI turn driver.</summary>
    [SerializeField]
    private HexworldAiTurnDriver _aiDriver;

    /// <summary>The camera that frames the island.</summary>
    [SerializeField]
    private HexworldCameraRig _cameraRig;

    /// <summary>
    /// Raised right after a new game was created and the board was drawn, and
    /// before the first turn is opened. Subscribe here to catch the opening
    /// PhaseChanged and DiceRolled events.
    /// </summary>
    public event Action<HexworldGame> GameCreated;

    /// <summary>Raised right after the first turn of a new game was opened.</summary>
    public event Action<HexworldGame> GameStarted;

    /// <summary>The running game, or null before the first one was created.</summary>
    public HexworldGame Game { get; private set; }

    /// <summary>The seed the running game was created with.</summary>
    public int ActiveSeed { get; private set; }

    /// <summary>The island on screen.</summary>
    public HexBoardView BoardView
    {
        get { return _boardView; }
    }

    /// <summary>The mouse picker, which carries the hovered and the selected tile.</summary>
    public HexworldInputController InputController
    {
        get { return _inputController; }
    }

    /// <summary>The AI turn driver.</summary>
    public HexworldAiTurnDriver AiDriver
    {
        get { return _aiDriver; }
    }

    /// <summary>The camera that frames the island.</summary>
    public HexworldCameraRig CameraRig
    {
        get { return _cameraRig; }
    }

    /// <summary>Seed used when <see cref="UseRandomSeed"/> is off.</summary>
    public int Seed
    {
        get { return _seed; }
        set { _seed = value; }
    }

    /// <summary>True to pick a fresh seed for every game.</summary>
    public bool UseRandomSeed
    {
        get { return _useRandomSeed; }
        set { _useRandomSeed = value; }
    }

    /// <summary>True while the human player may act.</summary>
    public bool IsHumanTurn
    {
        get
        {
            return Game != null
                && !Game.IsGameOver
                && Game.CurrentPlayerIndex == HumanPlayerIndex
                && (_aiDriver == null || !_aiDriver.IsPlaying);
        }
    }

    /// <summary>
    /// Throws the running game away and opens a fresh one.
    /// </summary>
    public void StartNewGame()
    {
        Shutdown();

        ActiveSeed = _useRandomSeed ? UnityEngine.Random.Range(1, int.MaxValue) : _seed;

        var game = new HexworldGame(HexworldConfig.CreateDefault(), new HexworldRandom(ActiveSeed), false);
        Game = game;

        if (_boardView != null)
        {
            _boardView.Attach(game);
        }

        if (_inputController != null)
        {
            _inputController.ResetState();
            _inputController.InteractionEnabled = true;
        }

        if (_aiDriver != null)
        {
            _aiDriver.PhaseDelay = _aiPhaseDelay;
            _aiDriver.Attach(game);
        }

        if (_cameraRig != null && _boardView != null)
        {
            _cameraRig.Frame(_boardView.BoardCenter, _boardView.BoardWorldRadius);
        }

        game.GameOver += OnGameOver;

        RaiseGameCreated(game);

        game.Start();

        RaiseGameStarted(game);
    }

    /// <summary>
    /// Starts a fresh game. Kept as a separate name so UI buttons read well.
    /// </summary>
    public void Restart()
    {
        StartNewGame();
    }

    /// <summary>
    /// Starts a fresh game with a given seed.
    /// </summary>
    /// <param name="seed">The seed of the new game.</param>
    public void StartNewGame(int seed)
    {
        _seed = seed;
        _useRandomSeed = false;
        StartNewGame();
    }

    /// <summary>
    /// Opens the first game once every other component has woken up.
    /// </summary>
    private void Start()
    {
        if (_startOnPlay && Game == null)
        {
            StartNewGame();
        }
    }

    /// <summary>
    /// Drops the running game when the object goes away.
    /// </summary>
    private void OnDestroy()
    {
        Shutdown();
    }

    /// <summary>
    /// Detaches every system from the running game and forgets it.
    /// </summary>
    private void Shutdown()
    {
        if (Game != null)
        {
            Game.GameOver -= OnGameOver;
            Game = null;
        }

        if (_aiDriver != null)
        {
            _aiDriver.Detach();
        }

        if (_inputController != null)
        {
            _inputController.ResetState();
        }

        if (_boardView != null)
        {
            _boardView.Detach();
        }
    }

    /// <summary>
    /// Stops the mouse once the game is decided.
    /// </summary>
    /// <param name="data">Who won and how.</param>
    private void OnGameOver(HexworldGameOverEvent data)
    {
        if (_inputController != null)
        {
            _inputController.ClearValidTargets();
            _inputController.InteractionEnabled = false;
        }

        Debug.Log(string.Format(
            "[Hexworld] Player {0} won by {1} on turn {2}.", data.Winner, data.Reason, data.TurnNumber));
    }

    /// <summary>
    /// Tells the listeners that a new game exists and its board is drawn.
    /// </summary>
    /// <param name="game">The new game.</param>
    private void RaiseGameCreated(HexworldGame game)
    {
        Action<HexworldGame> handler = GameCreated;
        if (handler != null)
        {
            handler(game);
        }
    }

    /// <summary>
    /// Tells the listeners that the first turn of the new game is open.
    /// </summary>
    /// <param name="game">The new game.</param>
    private void RaiseGameStarted(HexworldGame game)
    {
        Action<HexworldGame> handler = GameStarted;
        if (handler != null)
        {
            handler(game);
        }
    }
}
