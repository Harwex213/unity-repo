using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the turn of the AI one phase at a time. A pause between the phases
/// lets the player watch the dice, the new buildings and the attack instead of
/// seeing the whole turn happen in a single frame.
/// </summary>
public sealed class HexworldAiTurnDriver : MonoBehaviour
{
    /// <summary>How long the driver waits before the AI starts its turn.</summary>
    [SerializeField]
    private float _turnStartDelay = 0.6f;

    /// <summary>How long the driver waits after every phase.</summary>
    [SerializeField]
    private float _phaseDelay = 0.8f;

    /// <summary>Which side the AI plays.</summary>
    [SerializeField]
    private int _aiPlayerIndex = 1;

    /// <summary>The game the driver watches, or null.</summary>
    private HexworldGame _game;

    /// <summary>The heuristic opponent, or null.</summary>
    private HexworldAiPlayer _ai;

    /// <summary>The running turn coroutine, or null.</summary>
    private Coroutine _routine;

    /// <summary>Raised when the AI starts its turn.</summary>
    public event Action AiTurnStarted;

    /// <summary>Raised when the AI has handed the turn back.</summary>
    public event Action AiTurnFinished;

    /// <summary>True while the AI is playing.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Which side the AI plays.</summary>
    public int AiPlayerIndex
    {
        get { return _aiPlayerIndex; }
    }

    /// <summary>How long the driver waits after every phase.</summary>
    public float PhaseDelay
    {
        get { return _phaseDelay; }
        set { _phaseDelay = Mathf.Max(0f, value); }
    }

    /// <summary>The heuristic opponent, or null when no game is attached.</summary>
    public HexworldAiPlayer AiPlayer
    {
        get { return _ai; }
    }

    /// <summary>
    /// Watches a game. The driver takes over whenever the turn belongs to the
    /// AI side.
    /// </summary>
    /// <param name="game">The game to play in.</param>
    public void Attach(HexworldGame game)
    {
        Detach();

        _game = game;
        _ai = game == null ? null : new HexworldAiPlayer(game, _aiPlayerIndex);
    }

    /// <summary>
    /// Stops watching the current game and cancels a running AI turn.
    /// </summary>
    public void Detach()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        IsPlaying = false;
        _game = null;
        _ai = null;
    }

    /// <summary>
    /// Plays the whole AI turn in one go, without any pause. The editor checks
    /// use it because no frames pass there.
    /// </summary>
    public void PlayTurnImmediate()
    {
        if (_ai == null || _game == null)
        {
            return;
        }

        if (_game.IsGameOver || _game.CurrentPlayerIndex != _aiPlayerIndex)
        {
            return;
        }

        _ai.PlayTurn();
    }

    /// <summary>
    /// Starts the AI turn as soon as the turn belongs to the AI.
    /// </summary>
    private void Update()
    {
        if (IsPlaying || _game == null || _ai == null)
        {
            return;
        }

        if (_game.IsGameOver || _game.CurrentPlayerIndex != _aiPlayerIndex)
        {
            return;
        }

        _routine = StartCoroutine(RunTurn());
    }

    /// <summary>
    /// Walks the phases of one AI turn with a pause after each of them.
    /// </summary>
    /// <returns>The coroutine steps.</returns>
    private IEnumerator RunTurn()
    {
        IsPlaying = true;
        RaiseStarted();

        yield return new WaitForSeconds(_turnStartDelay);

        while (_game != null && !_game.IsGameOver && _game.CurrentPlayerIndex == _aiPlayerIndex)
        {
            HexworldPhase phase = _game.CurrentPhase;

            switch (phase)
            {
                case HexworldPhase.Harvest:
                    _ai.PlayHarvest();
                    break;
                case HexworldPhase.Build:
                    _ai.PlayBuild();
                    break;
                case HexworldPhase.Combat:
                    _ai.PlayCombat();
                    break;
                default:
                    phase = HexworldPhase.GameOver;
                    break;
            }

            if (phase == HexworldPhase.GameOver)
            {
                break;
            }

            // A phase that refused to move on would spin this loop forever.
            if (_game.CurrentPlayerIndex == _aiPlayerIndex && _game.CurrentPhase == phase && !_game.IsGameOver)
            {
                Debug.LogWarning("[HexworldAiTurnDriver] The AI did not leave the " + phase + " phase.");
                break;
            }

            yield return new WaitForSeconds(_phaseDelay);
        }

        _routine = null;
        IsPlaying = false;
        RaiseFinished();
    }

    /// <summary>
    /// Tells the listeners that the AI turn began.
    /// </summary>
    private void RaiseStarted()
    {
        Action handler = AiTurnStarted;
        if (handler != null)
        {
            handler();
        }
    }

    /// <summary>
    /// Tells the listeners that the AI turn ended.
    /// </summary>
    private void RaiseFinished()
    {
        Action handler = AiTurnFinished;
        if (handler != null)
        {
            handler();
        }
    }
}
