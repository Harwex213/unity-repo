using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One die on the harvest panel. It names the building that rolled it, prints
/// the face that came up and shows whether the player marked it for the reroll.
/// A die that shows a skull is locked and refuses the click.
/// </summary>
public sealed class HexworldDieView : MonoBehaviour
{
    /// <summary>The whole die is one button.</summary>
    [SerializeField]
    private Button _button;

    /// <summary>The die face.</summary>
    [SerializeField]
    private Image _background;

    /// <summary>Which building rolled the die.</summary>
    [SerializeField]
    private TMP_Text _sourceText;

    /// <summary>What the die shows.</summary>
    [SerializeField]
    private TMP_Text _faceText;

    /// <summary>Whether the die is marked or locked.</summary>
    [SerializeField]
    private TMP_Text _stateText;

    /// <summary>Who is told about the click.</summary>
    private Action<int> _clicked;

    /// <summary>Position of the die inside the harvest roll.</summary>
    public int Index { get; private set; }

    /// <summary>True when the die shows a skull and cannot be rerolled.</summary>
    public bool IsLocked { get; private set; }

    /// <summary>
    /// Hooks the die up to its position in the roll.
    /// </summary>
    /// <param name="index">Position of the die inside the roll.</param>
    /// <param name="clicked">Called with the index when the player clicks the die.</param>
    public void Setup(int index, Action<int> clicked)
    {
        Index = index;
        _clicked = clicked;

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(RaiseClicked);
        }
    }

    /// <summary>
    /// Writes a rolled die onto the widget.
    /// </summary>
    /// <param name="die">The die to show.</param>
    /// <param name="marked">True when the player marked it for the reroll.</param>
    /// <param name="interactable">True while the player may still change the mark.</param>
    public void Show(HexworldDie die, bool marked, bool interactable)
    {
        IsLocked = die.IsSkull;

        if (_sourceText != null)
        {
            _sourceText.text = HexworldUiTheme.BuildingName(die.BuildingType);
        }

        if (_faceText != null)
        {
            _faceText.text = HexworldUiTheme.DescribeFace(die.Face);
        }

        Color face;
        Color text;
        string state;

        if (die.IsSkull)
        {
            face = HexworldUiTheme.DieSkullColor;
            text = HexworldUiTheme.TextColor;
            state = "заперт";
        }
        else if (marked)
        {
            face = HexworldUiTheme.DieMarkedColor;
            text = HexworldUiTheme.DieTextColor;
            state = "перебросить";
        }
        else
        {
            face = HexworldUiTheme.DieColor;
            text = HexworldUiTheme.DieTextColor;
            state = string.Empty;
        }

        if (_background != null)
        {
            _background.color = face;
        }

        if (_sourceText != null)
        {
            _sourceText.color = text;
        }

        if (_faceText != null)
        {
            _faceText.color = text;
        }

        if (_stateText != null)
        {
            _stateText.text = state;
            _stateText.color = text;
        }

        if (_button != null)
        {
            _button.interactable = interactable && !die.IsSkull;
        }
    }

    /// <summary>
    /// Tells the panel that this die was clicked.
    /// </summary>
    private void RaiseClicked()
    {
        Action<int> handler = _clicked;
        if (handler != null)
        {
            handler(Index);
        }
    }
}
