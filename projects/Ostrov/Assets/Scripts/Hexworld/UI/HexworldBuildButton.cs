using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row of the build panel: a building with its price and a short hint, or
/// the "clear the rubble" order. A row the rules refuse goes grey and shows the
/// refusal instead of the hint.
/// </summary>
public sealed class HexworldBuildButton : MonoBehaviour
{
    /// <summary>The whole row is one button.</summary>
    [SerializeField]
    private Button _button;

    /// <summary>The row background.</summary>
    [SerializeField]
    private Image _background;

    /// <summary>Name of the building, or the name of the order.</summary>
    [SerializeField]
    private TMP_Text _nameText;

    /// <summary>The price.</summary>
    [SerializeField]
    private TMP_Text _costText;

    /// <summary>The hint, or the reason the row is refused.</summary>
    [SerializeField]
    private TMP_Text _hintText;

    /// <summary>Who is told about the click.</summary>
    private Action<HexworldBuildButton> _clicked;

    /// <summary>Which building this row builds. None for the rubble order.</summary>
    public HexBuildingType BuildingType { get; private set; }

    /// <summary>True when the row clears rubble instead of building something.</summary>
    public bool IsRubbleOrder { get; private set; }

    /// <summary>True when the rules allow the row right now.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Why the row is refused, or an empty string.</summary>
    public string Reason { get; private set; }

    /// <summary>
    /// Fills the row in once.
    /// </summary>
    /// <param name="type">Which building the row builds, or None for the rubble order.</param>
    /// <param name="isRubbleOrder">True when the row clears rubble.</param>
    /// <param name="title">Name shown on the row.</param>
    /// <param name="cost">Price shown on the row.</param>
    /// <param name="clicked">Called with this row when the player clicks it.</param>
    public void Setup(
        HexBuildingType type,
        bool isRubbleOrder,
        string title,
        string cost,
        Action<HexworldBuildButton> clicked)
    {
        BuildingType = type;
        IsRubbleOrder = isRubbleOrder;
        Reason = string.Empty;
        _clicked = clicked;

        if (_nameText != null)
        {
            _nameText.text = title;
        }

        if (_costText != null)
        {
            _costText.text = cost;
        }

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(RaiseClicked);
        }
    }

    /// <summary>
    /// Repaints the row.
    /// </summary>
    /// <param name="available">True when the rules allow the row.</param>
    /// <param name="selected">True when the player picked this row.</param>
    /// <param name="hint">What the row is good for.</param>
    /// <param name="reason">Why the row is refused, or an empty string.</param>
    public void SetState(bool available, bool selected, string hint, string reason)
    {
        IsAvailable = available;
        Reason = reason ?? string.Empty;

        if (_background != null)
        {
            _background.color = selected
                ? HexworldUiTheme.ButtonActiveColor
                : available ? HexworldUiTheme.ButtonColor : HexworldUiTheme.ButtonDisabledColor;
        }

        Color textColor = available || selected ? HexworldUiTheme.TextColor : HexworldUiTheme.MutedColor;

        if (_nameText != null)
        {
            _nameText.color = textColor;
        }

        if (_costText != null)
        {
            _costText.color = available || selected ? HexworldUiTheme.AccentColor : HexworldUiTheme.MutedColor;
        }

        if (_hintText != null)
        {
            bool refused = !available && !string.IsNullOrEmpty(Reason);
            _hintText.text = refused ? Reason : hint;
            _hintText.color = refused ? HexworldUiTheme.BadColor : HexworldUiTheme.MutedColor;
        }
    }

    /// <summary>
    /// Switches the row on or off.
    /// </summary>
    /// <param name="value">True to let the player click it.</param>
    public void SetInteractable(bool value)
    {
        if (_button != null)
        {
            _button.interactable = value;
        }
    }

    /// <summary>
    /// Tells the panel that this row was clicked.
    /// </summary>
    private void RaiseClicked()
    {
        Action<HexworldBuildButton> handler = _clicked;
        if (handler != null)
        {
            handler(this);
        }
    }
}
