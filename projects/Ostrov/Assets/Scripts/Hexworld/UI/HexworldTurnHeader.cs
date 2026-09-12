using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The strip at the top of the screen: which turn is running, whose turn it is,
/// which phase is open and one line that says what to do next. The same line
/// also carries the answer when the game refuses an action.
/// </summary>
public sealed class HexworldTurnHeader : MonoBehaviour
{
    /// <summary>Turn number and the side that plays it.</summary>
    [SerializeField]
    private TMP_Text _turnText;

    /// <summary>The running phase.</summary>
    [SerializeField]
    private TMP_Text _phaseText;

    /// <summary>What to do next, or why the last action was refused.</summary>
    [SerializeField]
    private TMP_Text _hintText;

    /// <summary>A bar painted in the colour of the side that plays.</summary>
    [SerializeField]
    private Image _sideStripe;

    /// <summary>The "the opponent is playing" badge.</summary>
    [SerializeField]
    private GameObject _aiBadge;

    /// <summary>The game being shown, or null.</summary>
    private HexworldGame _game;

    /// <summary>The message that replaces the hint, or an empty string.</summary>
    private string _status = string.Empty;

    /// <summary>Colour of the message that replaces the hint.</summary>
    private Color _statusColor = Color.white;

    /// <summary>
    /// Points the header at a game.
    /// </summary>
    /// <param name="game">The running game, or null to show nothing.</param>
    public void Bind(HexworldGame game)
    {
        _game = game;
        _status = string.Empty;
        Refresh();
    }

    /// <summary>
    /// Rereads the turn, the side and the phase and repaints the header.
    /// </summary>
    public void Refresh()
    {
        if (_game == null)
        {
            return;
        }

        int side = _game.CurrentPlayerIndex;
        Color sideColor = HexworldUiTheme.SideColor(side);

        if (_turnText != null)
        {
            _turnText.text = _game.IsGameOver
                ? "Партия окончена"
                : "Ход " + _game.TurnNumber + "  ·  " + HexworldUiTheme.Colored(
                    HexworldUiTheme.SideName(side).ToUpperInvariant(), sideColor);
        }

        if (_phaseText != null)
        {
            _phaseText.text = "Фаза: " + HexworldUiTheme.PhaseName(_game.CurrentPhase);
        }

        if (_sideStripe != null)
        {
            _sideStripe.color = _game.IsGameOver ? HexworldUiTheme.AccentColor : sideColor;
        }

        if (_hintText != null)
        {
            if (string.IsNullOrEmpty(_status))
            {
                _hintText.text = DefaultHint();
                _hintText.color = HexworldUiTheme.MutedColor;
            }
            else
            {
                _hintText.text = _status;
                _hintText.color = _statusColor;
            }
        }
    }

    /// <summary>
    /// Replaces the hint with a message, for example the reason an action was
    /// refused.
    /// </summary>
    /// <param name="text">The message, or an empty string to bring the hint back.</param>
    /// <param name="color">Colour of the message.</param>
    public void SetStatus(string text, Color color)
    {
        _status = text ?? string.Empty;
        _statusColor = color;
        Refresh();
    }

    /// <summary>
    /// Brings the default hint back.
    /// </summary>
    public void ClearStatus()
    {
        SetStatus(string.Empty, HexworldUiTheme.MutedColor);
    }

    /// <summary>
    /// Shows or hides the "the opponent is playing" badge.
    /// </summary>
    /// <param name="visible">True to show it.</param>
    public void SetAiBadgeVisible(bool visible)
    {
        if (_aiBadge != null)
        {
            _aiBadge.SetActive(visible);
        }
    }

    /// <summary>
    /// Builds the line that says what the player should do right now.
    /// </summary>
    /// <returns>The hint text.</returns>
    private string DefaultHint()
    {
        if (_game.IsGameOver)
        {
            return "Нажмите «Новая партия», чтобы сыграть ещё раз.";
        }

        if (_game.CurrentPlayerIndex != HexworldGameRunner.HumanPlayerIndex)
        {
            return "Противник делает ход. Подождите.";
        }

        switch (_game.CurrentPhase)
        {
            case HexworldPhase.Harvest:
                return "Щёлкните кубики, которые хотите перебросить, затем «Реролл» или «Подтвердить».";
            case HexworldPhase.Build:
                return "Выберите здание справа и щёлкните подсвеченный тайл на острове.";
            case HexworldPhase.Combat:
                return "Щёлкните подсвеченный тайл противника, задайте число солдат и атакуйте.";
            default:
                return string.Empty;
        }
    }
}
