using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The harvest panel. It shows one die per active building, lets the player
/// mark dice for the reroll and carries the two buttons that close the phase.
/// </summary>
public sealed class HexworldDicePanel : MonoBehaviour
{
    /// <summary>The hub that performs the actions.</summary>
    [SerializeField]
    private HexworldUiRoot _root;

    /// <summary>Where the dice are placed.</summary>
    [SerializeField]
    private RectTransform _dieRoot;

    /// <summary>The die widget the panel clones. It stays switched off.</summary>
    [SerializeField]
    private HexworldDieView _dieTemplate;

    /// <summary>The reroll button.</summary>
    [SerializeField]
    private Button _rerollButton;

    /// <summary>Caption of the reroll button.</summary>
    [SerializeField]
    private TMP_Text _rerollLabel;

    /// <summary>The confirm button.</summary>
    [SerializeField]
    private Button _confirmButton;

    /// <summary>The line under the dice.</summary>
    [SerializeField]
    private TMP_Text _infoText;

    /// <summary>The die widgets, one per die of the roll.</summary>
    private readonly List<HexworldDieView> _views = new List<HexworldDieView>();

    /// <summary>Indices of the dice the player marked for the reroll.</summary>
    private readonly List<int> _marked = new List<int>();

    /// <summary>The game being shown, or null.</summary>
    private HexworldGame _game;

    /// <summary>True while the player may act.</summary>
    private bool _interactable = true;

    /// <summary>Indices of the dice the player marked for the reroll.</summary>
    public IReadOnlyList<int> MarkedDice
    {
        get { return _marked; }
    }

    /// <summary>
    /// Points the panel at a game.
    /// </summary>
    /// <param name="game">The running game, or null to show nothing.</param>
    public void Bind(HexworldGame game)
    {
        _game = game;
        _marked.Clear();
        Refresh();
    }

    /// <summary>
    /// Rereads the dice of the running harvest and repaints the panel.
    /// </summary>
    public void Refresh()
    {
        if (_dieTemplate != null)
        {
            _dieTemplate.gameObject.SetActive(false);
        }

        IReadOnlyList<HexworldDie> dice = _game == null ? null : _game.Dice;
        int count = dice == null ? 0 : dice.Count;

        EnsureViews(count);
        DropMarksThatNoLongerFit(count);

        for (int i = 0; i < _views.Count; i++)
        {
            bool used = i < count;
            _views[i].gameObject.SetActive(used);
            if (used)
            {
                _views[i].Show(dice[i], _marked.Contains(i), _interactable);
            }
        }

        RefreshButtons();
    }

    /// <summary>
    /// Marks a die for the reroll, or takes the mark off again. This is what
    /// the die button calls.
    /// </summary>
    /// <param name="index">Position of the die inside the roll.</param>
    public void ToggleDie(int index)
    {
        if (!_interactable || _game == null || index < 0 || index >= _game.Dice.Count)
        {
            return;
        }

        if (!_game.Dice[index].CanReroll)
        {
            if (_root != null)
            {
                _root.ReportRefusal("Кубик с черепом перебросить нельзя.");
            }

            return;
        }

        if (!_marked.Remove(index))
        {
            _marked.Add(index);
        }

        Refresh();
    }

    /// <summary>
    /// Tells whether a die carries the reroll mark.
    /// </summary>
    /// <param name="index">Position of the die inside the roll.</param>
    /// <returns>True when the die is marked.</returns>
    public bool IsDieMarked(int index)
    {
        return _marked.Contains(index);
    }

    /// <summary>
    /// Rerolls the marked dice. This is what the reroll button calls.
    /// </summary>
    public void Reroll()
    {
        if (_root == null)
        {
            return;
        }

        _root.RerollDice(_marked);
        _marked.Clear();
        Refresh();
    }

    /// <summary>
    /// Applies the harvest. This is what the confirm button calls.
    /// </summary>
    public void Confirm()
    {
        if (_root == null)
        {
            return;
        }

        _marked.Clear();
        _root.ConfirmHarvest();
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
    /// Clones the die widget until there is one per die of the roll.
    /// </summary>
    /// <param name="count">How many dice the roll holds.</param>
    private void EnsureViews(int count)
    {
        if (_dieTemplate == null || _dieRoot == null)
        {
            return;
        }

        while (_views.Count < count)
        {
            HexworldDieView view = Instantiate(_dieTemplate, _dieRoot);
            view.gameObject.name = "Die" + _views.Count;
            view.gameObject.SetActive(true);
            view.Setup(_views.Count, ToggleDie);
            _views.Add(view);
        }
    }

    /// <summary>
    /// Forgets marks that point past the end of the current roll.
    /// </summary>
    /// <param name="count">How many dice the roll holds.</param>
    private void DropMarksThatNoLongerFit(int count)
    {
        for (int i = _marked.Count - 1; i >= 0; i--)
        {
            int index = _marked[i];
            if (index >= count || (_game != null && !_game.Dice[index].CanReroll))
            {
                _marked.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Dims the buttons the rules refuse and writes the line under the dice.
    /// </summary>
    private void RefreshButtons()
    {
        bool harvest = _game != null && _game.CurrentPhase == HexworldPhase.Harvest;
        string reason = string.Empty;
        bool canReroll = harvest && _interactable && _game.CanReroll(_marked, out reason);

        if (_rerollButton != null)
        {
            _rerollButton.interactable = canReroll;
        }

        if (_rerollLabel != null)
        {
            _rerollLabel.text = _game == null
                ? "Реролл"
                : "Реролл (" + _game.RerollsRemaining + ")";
        }

        if (_confirmButton != null)
        {
            _confirmButton.interactable = harvest && _interactable;
        }

        if (_infoText != null)
        {
            if (_game == null)
            {
                _infoText.text = string.Empty;
            }
            else if (_marked.Count == 0)
            {
                _infoText.text = "Осталось рероллов: " + _game.RerollsRemaining
                    + ". Щёлкните кубик, чтобы пометить его.";
                _infoText.color = HexworldUiTheme.MutedColor;
            }
            else if (canReroll)
            {
                _infoText.text = "Помечено кубиков: " + _marked.Count + ".";
                _infoText.color = HexworldUiTheme.AccentColor;
            }
            else
            {
                _infoText.text = HexworldUiTheme.Translate(reason);
                _infoText.color = HexworldUiTheme.BadColor;
            }
        }
    }
}
