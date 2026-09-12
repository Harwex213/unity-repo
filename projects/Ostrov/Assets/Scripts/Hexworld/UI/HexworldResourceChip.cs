using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One cell of the resource bar: a caption, the amount and the change the last
/// harvest brought. The bar clones it once per resource.
/// </summary>
public sealed class HexworldResourceChip : MonoBehaviour
{
    /// <summary>Name of the resource.</summary>
    [SerializeField]
    private TMP_Text _caption;

    /// <summary>How much the player owns.</summary>
    [SerializeField]
    private TMP_Text _value;

    /// <summary>How much the last harvest added.</summary>
    [SerializeField]
    private TMP_Text _delta;

    /// <summary>The cell background.</summary>
    [SerializeField]
    private Image _background;

    /// <summary>
    /// Writes the caption of the cell.
    /// </summary>
    /// <param name="caption">Name of the resource.</param>
    public void SetCaption(string caption)
    {
        if (_caption != null)
        {
            _caption.text = caption;
        }
    }

    /// <summary>
    /// Writes the amount the player owns.
    /// </summary>
    /// <param name="amount">The amount.</param>
    public void SetValue(int amount)
    {
        if (_value != null)
        {
            _value.text = amount.ToString();
        }
    }

    /// <summary>
    /// Writes the change of the last harvest, or hides the label for zero.
    /// </summary>
    /// <param name="amount">How much was added; a spend is negative.</param>
    public void SetDelta(int amount)
    {
        if (_delta == null)
        {
            return;
        }

        _delta.text = HexworldUiTheme.DescribeDelta(amount);
        _delta.color = amount >= 0 ? HexworldUiTheme.GoodColor : HexworldUiTheme.BadColor;
    }

    /// <summary>
    /// Paints the cell background.
    /// </summary>
    /// <param name="color">The new background colour.</param>
    public void SetBackground(Color color)
    {
        if (_background != null)
        {
            _background.color = color;
        }
    }

    /// <summary>
    /// Paints the amount label.
    /// </summary>
    /// <param name="color">The new text colour.</param>
    public void SetValueColor(Color color)
    {
        if (_value != null)
        {
            _value.color = color;
        }
    }
}
