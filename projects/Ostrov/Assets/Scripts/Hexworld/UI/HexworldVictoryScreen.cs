using TMPro;
using UnityEngine;

/// <summary>
/// The screen that closes the game. It says who won and how, and offers one
/// button that opens a fresh game.
/// </summary>
public sealed class HexworldVictoryScreen : MonoBehaviour
{
    /// <summary>The hub that performs the actions.</summary>
    [SerializeField]
    private HexworldUiRoot _root;

    /// <summary>The dimmed overlay with the card on it.</summary>
    [SerializeField]
    private GameObject _panel;

    /// <summary>Who won.</summary>
    [SerializeField]
    private TMP_Text _titleText;

    /// <summary>How the game was won.</summary>
    [SerializeField]
    private TMP_Text _reasonText;

    /// <summary>True while the screen is up.</summary>
    public bool IsVisible
    {
        get { return _panel != null && _panel.activeSelf; }
    }

    /// <summary>
    /// Raises the screen.
    /// </summary>
    /// <param name="data">Who won, how and on which turn.</param>
    public void Show(HexworldGameOverEvent data)
    {
        if (_titleText != null)
        {
            bool human = data.Winner == HexworldGameRunner.HumanPlayerIndex;
            _titleText.text = human ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
            _titleText.color = HexworldUiTheme.SideColor(data.Winner);
        }

        if (_reasonText != null)
        {
            _reasonText.text = string.Format(
                "{0} побеждает на ходу {1}.\n{2}",
                HexworldUiTheme.SideName(data.Winner),
                data.TurnNumber,
                ReasonText(data.Reason));
        }

        if (_panel != null)
        {
            _panel.SetActive(true);
        }
    }

    /// <summary>
    /// Takes the screen down.
    /// </summary>
    public void Hide()
    {
        if (_panel != null)
        {
            _panel.SetActive(false);
        }
    }

    /// <summary>
    /// Opens a fresh game. This is what the new game button calls.
    /// </summary>
    public void Restart()
    {
        Hide();

        if (_root != null)
        {
            _root.Restart();
        }
    }

    /// <summary>
    /// Writes the win condition in plain words.
    /// </summary>
    /// <param name="reason">How the game was won.</param>
    /// <returns>The sentence shown under the title.</returns>
    private static string ReasonText(HexworldWinReason reason)
    {
        switch (reason)
        {
            case HexworldWinReason.Monument:
                return "Монумент простоял полный круг.";
            case HexworldWinReason.Annihilation:
                return "Противник потерял замок и все тайлы.";
            default:
                return string.Empty;
        }
    }
}
