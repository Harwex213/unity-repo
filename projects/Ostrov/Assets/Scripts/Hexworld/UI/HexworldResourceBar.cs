using TMPro;
using UnityEngine;

/// <summary>
/// The stockpile of the human player: the five resources plus the skulls of the
/// running turn. A second, one line label repeats the stockpile of the AI, so
/// the player sees at a glance who is pulling ahead.
/// </summary>
public sealed class HexworldResourceBar : MonoBehaviour
{
    /// <summary>How many cells the bar holds: five resources plus the skulls.</summary>
    private const int ChipCount = 6;

    /// <summary>Index of the skull cell.</summary>
    private const int SkullChipIndex = 5;

    /// <summary>Where the cells are placed.</summary>
    [SerializeField]
    private RectTransform _chipRoot;

    /// <summary>The cell the bar clones. It stays switched off.</summary>
    [SerializeField]
    private HexworldResourceChip _chipTemplate;

    /// <summary>One line stockpile of the opponent.</summary>
    [SerializeField]
    private TMP_Text _opponentText;

    /// <summary>The cells, built once from the template.</summary>
    private readonly HexworldResourceChip[] _chips = new HexworldResourceChip[ChipCount];

    /// <summary>The game being shown, or null.</summary>
    private HexworldGame _game;

    /// <summary>True while the bar sums up the changes of a running harvest.</summary>
    private bool _accumulating;

    /// <summary>The changes summed up since the harvest was confirmed.</summary>
    private HexworldResources _accumulated;

    /// <summary>
    /// Builds the cells. Calling it twice does nothing the second time.
    /// </summary>
    public void Initialize()
    {
        if (_chips[0] != null || _chipTemplate == null || _chipRoot == null)
        {
            return;
        }

        _chipTemplate.gameObject.SetActive(false);

        for (int i = 0; i < ChipCount; i++)
        {
            HexworldResourceChip chip = Instantiate(_chipTemplate, _chipRoot);
            chip.gameObject.name = "Chip" + i;
            chip.gameObject.SetActive(true);
            chip.SetCaption(i == SkullChipIndex
                ? "Черепа"
                : HexworldUiTheme.ResourceLong((HexworldResourceType)i));
            chip.SetValue(0);
            chip.SetDelta(0);

            if (i == SkullChipIndex)
            {
                chip.SetBackground(HexworldUiTheme.DieSkullColor);
            }

            _chips[i] = chip;
        }
    }

    /// <summary>
    /// Points the bar at a game.
    /// </summary>
    /// <param name="game">The running game, or null to show nothing.</param>
    public void Bind(HexworldGame game)
    {
        Initialize();
        _game = game;
        _accumulating = false;
        _accumulated = HexworldResources.Zero;
        ClearDeltas();
        Refresh();
    }

    /// <summary>
    /// Rereads both stockpiles and repaints the bar.
    /// </summary>
    public void Refresh()
    {
        Initialize();

        if (_game == null)
        {
            return;
        }

        HexworldPlayerState human = _game.GetPlayer(HexworldGameRunner.HumanPlayerIndex);
        HexworldPlayerState ai = _game.GetPlayer(HexworldGameRunner.AiPlayerIndex);

        for (int i = 0; i < ChipCount; i++)
        {
            if (_chips[i] == null)
            {
                continue;
            }

            _chips[i].SetValue(i == SkullChipIndex
                ? human.Skulls
                : human.Resources.Get((HexworldResourceType)i));
        }

        if (_opponentText != null)
        {
            _opponentText.text = string.Format(
                "{0}   {1}   ·   черепа {2}",
                HexworldUiTheme.Colored("ПРОТИВНИК", HexworldUiTheme.AiColor),
                DescribeStockpile(ai.Resources),
                ai.Skulls);
        }
    }

    /// <summary>
    /// Starts summing the changes of a harvest, so the bar can show one net
    /// delta instead of flickering through every single payout.
    /// </summary>
    public void BeginHarvestDelta()
    {
        _accumulating = true;
        _accumulated = HexworldResources.Zero;
        ClearDeltas();
    }

    /// <summary>
    /// Stops summing and writes the net change of the harvest onto the cells.
    /// </summary>
    public void EndHarvestDelta()
    {
        _accumulating = false;

        for (int i = 0; i < ChipCount - 1; i++)
        {
            if (_chips[i] != null)
            {
                _chips[i].SetDelta(_accumulated.Get((HexworldResourceType)i));
            }
        }
    }

    /// <summary>
    /// Hides every delta label.
    /// </summary>
    public void ClearDeltas()
    {
        for (int i = 0; i < ChipCount; i++)
        {
            if (_chips[i] != null)
            {
                _chips[i].SetDelta(0);
            }
        }
    }

    /// <summary>
    /// Takes one stockpile change of the game.
    /// </summary>
    /// <param name="data">Whose stockpile changed and by how much.</param>
    public void HandleResourcesChanged(HexworldResourcesChangedEvent data)
    {
        if (_accumulating && data.PlayerIndex == HexworldGameRunner.HumanPlayerIndex)
        {
            _accumulated += data.Delta;
        }

        Refresh();
    }

    /// <summary>
    /// Writes a stockpile as one compact line.
    /// </summary>
    /// <param name="resources">The stockpile to write.</param>
    /// <returns>Text such as "Еда 5 · Дер 5 · Кам 3 · Кул 0 · Сол 2".</returns>
    private static string DescribeStockpile(HexworldResources resources)
    {
        return string.Format(
            "Еда {0} · Дер {1} · Кам {2} · Кул {3} · Сол {4}",
            resources.Food,
            resources.Wood,
            resources.Stone,
            resources.Culture,
            resources.Soldiers);
    }
}
