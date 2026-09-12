using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The build panel. The player picks a building here and then clicks a tile on
/// the island; the panel only holds the pick and the prices, the hub performs
/// the build itself.
/// </summary>
public sealed class HexworldBuildPanel : MonoBehaviour
{
    /// <summary>The order the buildings are listed in.</summary>
    private static readonly HexBuildingType[] Order =
    {
        HexBuildingType.Cottage,
        HexBuildingType.Farm,
        HexBuildingType.Quarry,
        HexBuildingType.Church,
        HexBuildingType.Barracks,
        HexBuildingType.Monument,
    };

    /// <summary>The hub that performs the actions.</summary>
    [SerializeField]
    private HexworldUiRoot _root;

    /// <summary>Where the rows are placed.</summary>
    [SerializeField]
    private RectTransform _buttonRoot;

    /// <summary>The row the panel clones. It stays switched off.</summary>
    [SerializeField]
    private HexworldBuildButton _buttonTemplate;

    /// <summary>The button that closes the phase.</summary>
    [SerializeField]
    private Button _endButton;

    /// <summary>The line under the rows.</summary>
    [SerializeField]
    private TMP_Text _statusText;

    /// <summary>The rows, one per building plus the rubble order.</summary>
    private readonly List<HexworldBuildButton> _buttons = new List<HexworldBuildButton>();

    /// <summary>The game being shown, or null.</summary>
    private HexworldGame _game;

    /// <summary>Which building the player picked, or None.</summary>
    private HexBuildingType _selected = HexBuildingType.None;

    /// <summary>True when the player picked the rubble order.</summary>
    private bool _rubbleSelected;

    /// <summary>True while the player may act.</summary>
    private bool _interactable = true;

    /// <summary>Which building the player picked, or None.</summary>
    public HexBuildingType SelectedBuilding
    {
        get { return _selected; }
    }

    /// <summary>True when the player picked the rubble order.</summary>
    public bool RubbleOrderSelected
    {
        get { return _rubbleSelected; }
    }

    /// <summary>
    /// Points the panel at a game and builds the rows.
    /// </summary>
    /// <param name="game">The running game, or null to show nothing.</param>
    public void Bind(HexworldGame game)
    {
        _game = game;
        _selected = HexBuildingType.None;
        _rubbleSelected = false;

        BuildRows();
        Refresh();
    }

    /// <summary>
    /// Picks a building. This is what a building row calls.
    /// </summary>
    /// <param name="type">Which building to pick.</param>
    public void SelectBuilding(HexBuildingType type)
    {
        if (_game == null || !_interactable)
        {
            return;
        }

        string reason;
        if (!IsBuildingAvailable(type, out reason))
        {
            _selected = HexBuildingType.None;
            _rubbleSelected = false;
            ReportRefusal(reason);
            Refresh();
            return;
        }

        _selected = _selected == type ? HexBuildingType.None : type;
        _rubbleSelected = false;
        Apply();
    }

    /// <summary>
    /// Picks the rubble order. This is what the rubble row calls.
    /// </summary>
    public void SelectClearRubble()
    {
        if (_game == null || !_interactable)
        {
            return;
        }

        string reason;
        if (!IsRubbleOrderAvailable(out reason))
        {
            _selected = HexBuildingType.None;
            _rubbleSelected = false;
            ReportRefusal(reason);
            Refresh();
            return;
        }

        _rubbleSelected = !_rubbleSelected;
        _selected = HexBuildingType.None;
        Apply();
    }

    /// <summary>
    /// Drops the pick.
    /// </summary>
    public void ClearSelection()
    {
        _selected = HexBuildingType.None;
        _rubbleSelected = false;
        Apply();
    }

    /// <summary>
    /// Closes the build phase. This is what the end button calls.
    /// </summary>
    public void EndBuild()
    {
        ClearSelection();

        if (_root != null)
        {
            _root.EndBuildPhase();
        }
    }

    /// <summary>
    /// Rereads the prices and the prerequisites and repaints every row.
    /// </summary>
    public void Refresh()
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            HexworldBuildButton button = _buttons[i];
            string reason;
            bool available;
            bool selected;
            string hint;

            if (button.IsRubbleOrder)
            {
                available = IsRubbleOrderAvailable(out reason);
                selected = _rubbleSelected;
                hint = "Освобождает тайл под стройку";
            }
            else
            {
                available = IsBuildingAvailable(button.BuildingType, out reason);
                selected = _selected == button.BuildingType;
                hint = Hint(button.BuildingType);
            }

            button.SetState(available && _interactable, selected, hint, reason);
            button.SetInteractable(_interactable);
        }

        if (_endButton != null)
        {
            _endButton.interactable = _interactable && _game != null
                && _game.CurrentPhase == HexworldPhase.Build;
        }

        if (_statusText != null)
        {
            if (_rubbleSelected)
            {
                _statusText.text = "Выбрано: расчистка завала. Щёлкните подсвеченный тайл.";
                _statusText.color = HexworldUiTheme.AccentColor;
            }
            else if (_selected != HexBuildingType.None)
            {
                _statusText.text = "Выбрано: " + HexworldUiTheme.BuildingName(_selected)
                    + ". Щёлкните подсвеченный тайл.";
                _statusText.color = HexworldUiTheme.AccentColor;
            }
            else
            {
                _statusText.text = "Выберите здание, затем тайл на острове.";
                _statusText.color = HexworldUiTheme.MutedColor;
            }
        }
    }

    /// <summary>
    /// Switches the whole panel on or off, for example while the AI plays.
    /// </summary>
    /// <param name="value">True to let the player act.</param>
    public void SetInteractable(bool value)
    {
        _interactable = value;
        if (!value)
        {
            _selected = HexBuildingType.None;
            _rubbleSelected = false;
        }

        Refresh();
    }

    /// <summary>
    /// Tells whether a building may be raised somewhere on the island right now.
    /// </summary>
    /// <param name="type">Which building to check.</param>
    /// <param name="reason">Receives why the building is refused, or an empty string.</param>
    /// <returns>True when at least one tile accepts the building.</returns>
    public bool IsBuildingAvailable(HexBuildingType type, out string reason)
    {
        reason = string.Empty;

        if (_game == null)
        {
            reason = "Партия не идёт.";
            return false;
        }

        if (_game.CurrentPhase != HexworldPhase.Build)
        {
            reason = "Сейчас не фаза строительства.";
            return false;
        }

        HexworldBuildingDefinition definition = _game.Config.GetBuilding(type);
        if (definition == null || !definition.IsBuildable)
        {
            reason = "Это здание построить нельзя.";
            return false;
        }

        if (type == HexBuildingType.Monument && !_game.CanBuildMonument(_game.CurrentPlayerIndex, out reason))
        {
            reason = HexworldUiTheme.Translate(reason);
            return false;
        }

        if (!_game.CurrentPlayer.Resources.Covers(definition.Cost))
        {
            reason = "Не хватает ресурсов: " + HexworldUiTheme.DescribeResources(
                (definition.Cost - _game.CurrentPlayer.Resources).ClampedToZero());
            return false;
        }

        if (CountBuildTargets(type) == 0)
        {
            reason = "Нет свободного тайла.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tells whether the player may clear rubble somewhere right now.
    /// </summary>
    /// <param name="reason">Receives why clearing is refused, or an empty string.</param>
    /// <returns>True when at least one tile may be cleared.</returns>
    public bool IsRubbleOrderAvailable(out string reason)
    {
        reason = string.Empty;

        if (_game == null)
        {
            reason = "Партия не идёт.";
            return false;
        }

        if (_game.CurrentPhase != HexworldPhase.Build)
        {
            reason = "Сейчас не фаза строительства.";
            return false;
        }

        if (!_game.CurrentPlayer.Resources.Covers(_game.Config.RubbleClearCost))
        {
            reason = "Не хватает ресурсов: " + HexworldUiTheme.DescribeResources(
                (_game.Config.RubbleClearCost - _game.CurrentPlayer.Resources).ClampedToZero());
            return false;
        }

        foreach (HexTile tile in _game.Board.Tiles)
        {
            if (_game.CanClearRubble(tile.Coord))
            {
                return true;
            }
        }

        reason = "Завалов на ваших тайлах нет.";
        return false;
    }

    /// <summary>
    /// Counts the tiles that accept a building right now.
    /// </summary>
    /// <param name="type">Which building to place.</param>
    /// <returns>How many tiles accept it.</returns>
    private int CountBuildTargets(HexBuildingType type)
    {
        int count = 0;
        foreach (HexTile tile in _game.Board.Tiles)
        {
            if (_game.CanBuild(tile.Coord, type))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Creates one row per building plus the rubble order.
    /// </summary>
    private void BuildRows()
    {
        if (_buttons.Count > 0 || _buttonTemplate == null || _buttonRoot == null || _game == null)
        {
            return;
        }

        _buttonTemplate.gameObject.SetActive(false);

        for (int i = 0; i < Order.Length; i++)
        {
            HexBuildingType type = Order[i];
            HexworldBuildingDefinition definition = _game.Config.GetBuilding(type);
            if (definition == null || !definition.IsBuildable)
            {
                continue;
            }

            HexworldBuildButton button = Instantiate(_buttonTemplate, _buttonRoot);
            button.gameObject.name = "Build_" + type;
            button.gameObject.SetActive(true);
            button.Setup(
                type,
                false,
                HexworldUiTheme.BuildingName(type),
                HexworldUiTheme.DescribeResources(definition.Cost),
                HandleRowClicked);
            _buttons.Add(button);
        }

        HexworldBuildButton rubble = Instantiate(_buttonTemplate, _buttonRoot);
        rubble.gameObject.name = "Build_ClearRubble";
        rubble.gameObject.SetActive(true);
        rubble.Setup(
            HexBuildingType.None,
            true,
            "Расчистить завал",
            HexworldUiTheme.DescribeResources(_game.Config.RubbleClearCost),
            HandleRowClicked);
        _buttons.Add(rubble);
    }

    /// <summary>
    /// Routes a row click to the right pick.
    /// </summary>
    /// <param name="button">The row that was clicked.</param>
    private void HandleRowClicked(HexworldBuildButton button)
    {
        if (button.IsRubbleOrder)
        {
            SelectClearRubble();
        }
        else
        {
            SelectBuilding(button.BuildingType);
        }
    }

    /// <summary>
    /// Repaints the panel and asks the hub to highlight the legal tiles.
    /// </summary>
    private void Apply()
    {
        Refresh();

        if (_root != null)
        {
            _root.ClearRefusal();
            _root.RefreshValidTargets();
        }
    }

    /// <summary>
    /// Shows why a pick was refused.
    /// </summary>
    /// <param name="reason">The refusal text.</param>
    private void ReportRefusal(string reason)
    {
        if (_root != null)
        {
            _root.ReportRefusal(reason);
            _root.RefreshValidTargets();
        }
    }

    /// <summary>
    /// Returns the one line hint of a building.
    /// </summary>
    /// <param name="type">Which building.</param>
    /// <returns>What the building is good for.</returns>
    private static string Hint(HexBuildingType type)
    {
        switch (type)
        {
            case HexBuildingType.Cottage: return "Дешёвая еда, нужен для монумента";
            case HexBuildingType.Farm: return "Еда каждый ход, без черепов";
            case HexBuildingType.Quarry: return "Камень";
            case HexBuildingType.Church: return "Культура, нужна для монумента";
            case HexBuildingType.Barracks: return "Солдаты и защита соседей";
            case HexBuildingType.Monument: return "Победа, если простоит круг";
            default: return string.Empty;
        }
    }
}
