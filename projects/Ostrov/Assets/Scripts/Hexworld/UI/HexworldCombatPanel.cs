using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The combat panel. It describes the tile the player clicked, lets the player
/// pick how many soldiers to spend and carries the attack button and the button
/// that hands the turn over.
/// </summary>
public sealed class HexworldCombatPanel : MonoBehaviour
{
    /// <summary>The hub that performs the actions.</summary>
    [SerializeField]
    private HexworldUiRoot _root;

    /// <summary>What stands on the tile under attack.</summary>
    [SerializeField]
    private TMP_Text _targetText;

    /// <summary>How many soldiers to spend.</summary>
    [SerializeField]
    private Slider _soldierSlider;

    /// <summary>The soldier count as a number.</summary>
    [SerializeField]
    private TMP_Text _soldierText;

    /// <summary>The button that spends one soldier less.</summary>
    [SerializeField]
    private Button _minusButton;

    /// <summary>The button that spends one soldier more.</summary>
    [SerializeField]
    private Button _plusButton;

    /// <summary>The attack button.</summary>
    [SerializeField]
    private Button _attackButton;

    /// <summary>The button that hands the turn over.</summary>
    [SerializeField]
    private Button _endTurnButton;

    /// <summary>What the last attack rolled.</summary>
    [SerializeField]
    private TMP_Text _resultText;

    /// <summary>The game being shown, or null.</summary>
    private HexworldGame _game;

    /// <summary>The tile under attack, or null.</summary>
    private HexCoord? _target;

    /// <summary>How many soldiers the player wants to spend.</summary>
    private int _soldiers = 1;

    /// <summary>True while the panel writes the slider itself.</summary>
    private bool _updatingSlider;

    /// <summary>True while the player may act.</summary>
    private bool _interactable = true;

    /// <summary>The tile under attack, or null.</summary>
    public HexCoord? Target
    {
        get { return _target; }
    }

    /// <summary>How many soldiers the player wants to spend.</summary>
    public int Soldiers
    {
        get { return _soldiers; }
    }

    /// <summary>
    /// Points the panel at a game.
    /// </summary>
    /// <param name="game">The running game, or null to show nothing.</param>
    public void Bind(HexworldGame game)
    {
        _game = game;
        _target = null;
        _soldiers = 1;

        if (_soldierSlider != null)
        {
            _soldierSlider.wholeNumbers = true;
            _soldierSlider.onValueChanged.RemoveAllListeners();
            _soldierSlider.onValueChanged.AddListener(HandleSliderChanged);
        }

        if (_resultText != null)
        {
            _resultText.text = string.Empty;
        }

        Refresh();
    }

    /// <summary>
    /// Points the panel at a tile. This is what a click on the island calls.
    /// </summary>
    /// <param name="coord">The tile under attack, or null to forget it.</param>
    public void SetTarget(HexCoord? coord)
    {
        _target = coord;

        if (coord.HasValue && _game != null)
        {
            _soldiers = Mathf.Max(_soldiers, _game.GetMinimumSoldiers(coord.Value));
        }

        Refresh();
    }

    /// <summary>
    /// Sets how many soldiers to spend. This is what the slider calls.
    /// </summary>
    /// <param name="value">The soldier count.</param>
    public void SetSoldiers(int value)
    {
        _soldiers = Mathf.Clamp(value, MinimumSoldiers(), MaximumSoldiers());
        Refresh();
    }

    /// <summary>
    /// Moves the soldier count. This is what the minus and plus buttons call.
    /// </summary>
    /// <param name="delta">How much to add, usually one or minus one.</param>
    public void AddSoldiers(int delta)
    {
        SetSoldiers(_soldiers + delta);
    }

    /// <summary>
    /// Attacks the chosen tile. This is what the attack button calls.
    /// </summary>
    public void Attack()
    {
        if (_root == null || !_target.HasValue)
        {
            return;
        }

        _root.Capture(_target.Value, _soldiers);
    }

    /// <summary>
    /// Hands the turn over. This is what the end turn button calls.
    /// </summary>
    public void EndTurn()
    {
        _target = null;

        if (_root != null)
        {
            _root.EndTurn();
        }
    }

    /// <summary>
    /// Writes the record of an attack onto the panel.
    /// </summary>
    /// <param name="result">What the attack rolled.</param>
    public void ShowResult(HexworldCombatResult result)
    {
        if (_resultText == null || result == null)
        {
            return;
        }

        string text;
        if (result.WasNeutral)
        {
            text = string.Format(
                "Ничей тайл {0} занят за {1} солд.",
                result.Coord,
                result.SoldiersSpent);
        }
        else
        {
            text = string.Format(
                "Атака {0} ({1} солд. + {2}) против защиты {3} ({4} + {5}) — {6}",
                result.AttackPower,
                result.SoldiersSpent,
                result.AttackRoll,
                result.DefensePower,
                result.DefensePower - result.DefenseRoll,
                result.DefenseRoll,
                result.Captured ? "тайл захвачен" : "атака отбита");
        }

        if (result.Captured && result.DestroyedBuilding != HexBuildingType.None)
        {
            text += ", разрушено " + HexworldUiTheme.BuildingName(result.DestroyedBuilding);
        }

        _resultText.text = text;
        _resultText.color = result.Captured ? HexworldUiTheme.GoodColor : HexworldUiTheme.BadColor;
    }

    /// <summary>
    /// Rereads the tile under attack and repaints the panel.
    /// </summary>
    public void Refresh()
    {
        int min = MinimumSoldiers();
        int max = MaximumSoldiers();
        _soldiers = Mathf.Clamp(_soldiers, min, max);

        if (_soldierSlider != null)
        {
            _updatingSlider = true;
            _soldierSlider.minValue = min;
            _soldierSlider.maxValue = max;
            _soldierSlider.value = _soldiers;
            _soldierSlider.interactable = _interactable && max > min;
            _updatingSlider = false;
        }

        if (_soldierText != null)
        {
            _soldierText.text = _soldiers.ToString();
        }

        if (_minusButton != null)
        {
            _minusButton.interactable = _interactable && _soldiers > min;
        }

        if (_plusButton != null)
        {
            _plusButton.interactable = _interactable && _soldiers < max;
        }

        string reason = string.Empty;
        bool canAttack = _interactable
            && _game != null
            && _target.HasValue
            && _game.CanCapture(_target.Value, _soldiers, out reason);

        if (_attackButton != null)
        {
            _attackButton.interactable = canAttack;
        }

        if (_endTurnButton != null)
        {
            _endTurnButton.interactable = _interactable
                && _game != null
                && _game.CurrentPhase == HexworldPhase.Combat;
        }

        if (_targetText != null)
        {
            _targetText.text = DescribeTarget(reason);
        }
    }

    /// <summary>
    /// Switches the whole panel on or off, for example while the AI plays.
    /// </summary>
    /// <param name="value">True to let the player act.</param>
    public void SetInteractable(bool value)
    {
        _interactable = value;
        Refresh();
    }

    /// <summary>
    /// Takes the slider value.
    /// </summary>
    /// <param name="value">The new slider value.</param>
    private void HandleSliderChanged(float value)
    {
        if (_updatingSlider)
        {
            return;
        }

        SetSoldiers(Mathf.RoundToInt(value));
    }

    /// <summary>
    /// Returns the lowest soldier count the chosen tile accepts.
    /// </summary>
    /// <returns>The lowest count, or one when no tile is chosen.</returns>
    private int MinimumSoldiers()
    {
        if (_game == null || !_target.HasValue)
        {
            return 1;
        }

        return Mathf.Max(1, _game.GetMinimumSoldiers(_target.Value));
    }

    /// <summary>
    /// Returns the highest soldier count the player can pay.
    /// </summary>
    /// <returns>The stockpile of soldiers, never below the lowest count.</returns>
    private int MaximumSoldiers()
    {
        int min = MinimumSoldiers();
        if (_game == null)
        {
            return min;
        }

        return Mathf.Max(min, _game.GetPlayer(HexworldGameRunner.HumanPlayerIndex).Resources.Soldiers);
    }

    /// <summary>
    /// Writes the block that describes the tile under attack.
    /// </summary>
    /// <param name="reason">Why the attack is refused, or an empty string.</param>
    /// <returns>The text of the block.</returns>
    private string DescribeTarget(string reason)
    {
        if (_game == null)
        {
            return string.Empty;
        }

        if (!_target.HasValue)
        {
            return "Цель не выбрана.\nЩёлкните подсвеченный тайл на острове.";
        }

        HexCoord coord = _target.Value;
        HexTile tile = _game.Board.GetTile(coord);
        if (tile == null)
        {
            return "Такого тайла нет.";
        }

        string owner = HexworldUiTheme.Colored(
            HexworldUiTheme.SideName(tile.Owner), HexworldUiTheme.SideColor(tile.Owner));

        string building = tile.HasBuilding
            ? HexworldUiTheme.BuildingName(tile.Building.Type)
            : "пусто";

        string text = string.Format(
            "Тайл {0} · {1}\nВладелец: {2} · Здание: {3}\nЗащита: {4} · Минимум солдат: {5}",
            coord,
            HexworldUiTheme.TerrainName(tile.Terrain),
            owner,
            building,
            _game.GetDefenseStrength(coord),
            _game.GetMinimumSoldiers(coord));

        if (!string.IsNullOrEmpty(reason))
        {
            text += "\n" + HexworldUiTheme.Colored(
                HexworldUiTheme.Translate(reason), HexworldUiTheme.BadColor);
        }

        return text;
    }
}
