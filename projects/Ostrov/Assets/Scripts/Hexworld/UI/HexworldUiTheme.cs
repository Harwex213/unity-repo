using System.Text;
using UnityEngine;

/// <summary>
/// The shared palette and the shared wording of the Hexworld interface. Every
/// panel reads its colours and its labels here, so the board and the screen
/// name the same things the same way.
/// </summary>
public static class HexworldUiTheme
{
    /// <summary>Blue of the human side. Same value as M_OwnerPlayer.</summary>
    public static readonly Color PlayerColor = new Color(0.2392f, 0.4902f, 0.8471f, 1f);

    /// <summary>Red of the AI side. Same value as M_OwnerAi.</summary>
    public static readonly Color AiColor = new Color(0.7882f, 0.2706f, 0.2392f, 1f);

    /// <summary>Yellow of the board highlight. Same value as M_Highlight.</summary>
    public static readonly Color AccentColor = new Color(0.9490f, 0.7725f, 0.2392f, 1f);

    /// <summary>Background of a panel.</summary>
    public static readonly Color PanelColor = new Color(0.047f, 0.067f, 0.106f, 0.92f);

    /// <summary>Background of a block inside a panel.</summary>
    public static readonly Color PanelSoftColor = new Color(0.106f, 0.137f, 0.188f, 0.95f);

    /// <summary>Background of the strip that titles a panel.</summary>
    public static readonly Color PanelHeaderColor = new Color(0.145f, 0.192f, 0.263f, 0.98f);

    /// <summary>Main text colour.</summary>
    public static readonly Color TextColor = new Color(0.925f, 0.949f, 0.980f, 1f);

    /// <summary>Text colour of a caption or a hint.</summary>
    public static readonly Color MutedColor = new Color(0.655f, 0.714f, 0.784f, 1f);

    /// <summary>Text colour of a gain.</summary>
    public static readonly Color GoodColor = new Color(0.451f, 0.824f, 0.451f, 1f);

    /// <summary>Text colour of a loss or a refusal.</summary>
    public static readonly Color BadColor = new Color(0.902f, 0.420f, 0.380f, 1f);

    /// <summary>Face of a normal button.</summary>
    public static readonly Color ButtonColor = new Color(0.180f, 0.231f, 0.310f, 1f);

    /// <summary>Face of a button that is switched on.</summary>
    public static readonly Color ButtonActiveColor = new Color(0.2392f, 0.4902f, 0.8471f, 1f);

    /// <summary>Face of a button that refuses to act.</summary>
    public static readonly Color ButtonDisabledColor = new Color(0.129f, 0.149f, 0.184f, 1f);

    /// <summary>Face of a die that may still be rerolled.</summary>
    public static readonly Color DieColor = new Color(0.925f, 0.933f, 0.949f, 1f);

    /// <summary>Face of a die the player marked for the reroll.</summary>
    public static readonly Color DieMarkedColor = new Color(0.9490f, 0.7725f, 0.2392f, 1f);

    /// <summary>Face of a die that shows a skull and is locked.</summary>
    public static readonly Color DieSkullColor = new Color(0.404f, 0.176f, 0.176f, 1f);

    /// <summary>Text colour on a light die.</summary>
    public static readonly Color DieTextColor = new Color(0.09f, 0.11f, 0.15f, 1f);

    /// <summary>Short names of the five resources, in enum order.</summary>
    private static readonly string[] ResourceShortNames = { "Еда", "Дер", "Кам", "Кул", "Сол" };

    /// <summary>Full names of the five resources, in enum order.</summary>
    private static readonly string[] ResourceLongNames = { "Еда", "Дерево", "Камень", "Культура", "Солдаты" };

    /// <summary>
    /// Returns the colour of a side.
    /// </summary>
    /// <param name="playerIndex">Which side, or -1 for nobody.</param>
    /// <returns>Blue for the human, red for the AI, grey for nobody.</returns>
    public static Color SideColor(int playerIndex)
    {
        if (playerIndex == HexworldGameRunner.HumanPlayerIndex)
        {
            return PlayerColor;
        }

        if (playerIndex == HexworldGameRunner.AiPlayerIndex)
        {
            return AiColor;
        }

        return MutedColor;
    }

    /// <summary>
    /// Returns the name of a side.
    /// </summary>
    /// <param name="playerIndex">Which side, or -1 for nobody.</param>
    /// <returns>The name shown in the header and in the log.</returns>
    public static string SideName(int playerIndex)
    {
        if (playerIndex == HexworldGameRunner.HumanPlayerIndex)
        {
            return "Вы";
        }

        if (playerIndex == HexworldGameRunner.AiPlayerIndex)
        {
            return "Противник";
        }

        return "Ничей";
    }

    /// <summary>
    /// Returns the name of a phase.
    /// </summary>
    /// <param name="phase">Which phase.</param>
    /// <returns>The name shown in the header.</returns>
    public static string PhaseName(HexworldPhase phase)
    {
        switch (phase)
        {
            case HexworldPhase.Harvest: return "Урожай";
            case HexworldPhase.Build: return "Строительство";
            case HexworldPhase.Combat: return "Бой";
            default: return "Партия окончена";
        }
    }

    /// <summary>
    /// Returns the name of a terrain kind.
    /// </summary>
    /// <param name="terrain">Which terrain.</param>
    /// <returns>The name shown in the combat panel.</returns>
    public static string TerrainName(HexTerrainType terrain)
    {
        switch (terrain)
        {
            case HexTerrainType.Grass: return "трава";
            case HexTerrainType.Forest: return "лес";
            case HexTerrainType.Stone: return "камень";
            default: return "завал";
        }
    }

    /// <summary>
    /// Returns the name of a building. Buildings keep their English names, so
    /// the screen and the models of the board call them the same thing.
    /// </summary>
    /// <param name="type">Which building.</param>
    /// <returns>The name shown in the panels and in the log.</returns>
    public static string BuildingName(HexBuildingType type)
    {
        return type == HexBuildingType.None ? "пусто" : type.ToString();
    }

    /// <summary>
    /// Returns the short name of a resource.
    /// </summary>
    /// <param name="type">Which resource.</param>
    /// <returns>A three letter name.</returns>
    public static string ResourceShort(HexworldResourceType type)
    {
        return ResourceShortNames[(int)type];
    }

    /// <summary>
    /// Returns the full name of a resource.
    /// </summary>
    /// <param name="type">Which resource.</param>
    /// <returns>The name shown as the caption of a resource chip.</returns>
    public static string ResourceLong(HexworldResourceType type)
    {
        return ResourceLongNames[(int)type];
    }

    /// <summary>
    /// Writes a resource bundle as a short list and skips the zero amounts.
    /// </summary>
    /// <param name="resources">The bundle to write.</param>
    /// <returns>Text such as "3 Дер, 2 Кам", or a dash for an empty bundle.</returns>
    public static string DescribeResources(HexworldResources resources)
    {
        var text = new StringBuilder();
        for (int i = 0; i < ResourceShortNames.Length; i++)
        {
            var type = (HexworldResourceType)i;
            int amount = resources.Get(type);
            if (amount == 0)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append(", ");
            }

            text.Append(amount).Append(' ').Append(ResourceShortNames[i]);
        }

        return text.Length == 0 ? "—" : text.ToString();
    }

    /// <summary>
    /// Writes a die face as the text printed on the die.
    /// </summary>
    /// <param name="face">The face that came up.</param>
    /// <returns>Text such as "2 Еда", or "ЧЕРЕП" for a skull face.</returns>
    public static string DescribeFace(HexworldDiceFace face)
    {
        if (face.IsSkull)
        {
            return "ЧЕРЕП";
        }

        var text = new StringBuilder();
        for (int i = 0; i < ResourceShortNames.Length; i++)
        {
            var type = (HexworldResourceType)i;
            int amount = face.Yield.Get(type);
            if (amount == 0)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append('\n');
            }

            text.Append(amount).Append(' ').Append(ResourceShortNames[i]);
        }

        return text.Length == 0 ? "—" : text.ToString();
    }

    /// <summary>
    /// Writes a signed amount for a delta label.
    /// </summary>
    /// <param name="amount">How much was added; a spend is negative.</param>
    /// <returns>Text such as "+3", or an empty string for zero.</returns>
    public static string DescribeDelta(int amount)
    {
        if (amount == 0)
        {
            return string.Empty;
        }

        return amount > 0 ? "+" + amount : amount.ToString();
    }

    /// <summary>
    /// Refusals the rules produce, paired with the Russian text the screen
    /// shows instead. A refusal the table misses is shown as it came.
    /// </summary>
    private static readonly string[] ReasonTable =
    {
        "Dice can only be rerolled", "Кубики бросают только в фазе урожая.",
        "No rerolls left", "Рероллы кончились.",
        "No dice were selected", "Не выбрано ни одного кубика.",
        "does not exist", "Такого кубика нет.",
        "shows a skull and is locked", "Кубик с черепом перебросить нельзя.",
        "The harvest phase is not running", "Фаза урожая не идёт.",
        "Buildings can only be raised", "Строить можно только в фазе строительства.",
        "cannot be built", "Это здание построить нельзя.",
        "There is no tile at", "Такого тайла нет.",
        "dropped into the Ether", "Тайл ушёл в Эфир.",
        "does not belong to the current player", "Тайл вам не принадлежит.",
        "Rubble must be cleared", "Сначала расчистите завал.",
        "already carries a building", "На тайле уже стоит здание.",
        "cannot pay", "Не хватает ресурсов.",
        "already owns a Monument", "Монумент уже построен.",
        "A Monument needs 1 Church", "Для монумента нужна церковь (Church).",
        "A Monument needs", "Для монумента не хватает зданий.",
        "Rubble can only be cleared", "Завалы расчищают только в фазе строительства.",
        "carries no rubble", "На тайле нет завала.",
        "The build phase is not running", "Фаза строительства не идёт.",
        "Tiles can only be captured", "Захватывать можно только в фазе боя.",
        "Only one tile may be captured", "За ход можно захватить только один тайл.",
        "already belongs to the current player", "Этот тайл уже ваш.",
        "does not touch any tile", "Тайл не граничит с вашими владениями.",
        "At least one soldier", "Нужен хотя бы один солдат.",
        "has fewer than", "Не хватает солдат.",
    };

    /// <summary>
    /// Turns a refusal of the rules into the Russian line the screen shows.
    /// </summary>
    /// <param name="reason">The refusal as the rules wrote it.</param>
    /// <returns>The Russian line, or the original text when it is unknown.</returns>
    public static string Translate(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            return string.Empty;
        }

        for (int i = 0; i < ReasonTable.Length; i += 2)
        {
            if (reason.IndexOf(ReasonTable[i], System.StringComparison.Ordinal) >= 0)
            {
                return ReasonTable[i + 1];
            }
        }

        return reason;
    }

    /// <summary>
    /// Wraps a piece of text in a TextMeshPro colour tag.
    /// </summary>
    /// <param name="text">The text to colour.</param>
    /// <param name="color">The colour to paint it in.</param>
    /// <returns>The text with the tag around it.</returns>
    public static string Colored(string text, Color color)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
    }
}
