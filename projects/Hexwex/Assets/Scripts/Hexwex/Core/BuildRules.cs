using System.Collections.Generic;
using System.Linq;

namespace Hexwex.Core
{
    /// <summary>
    /// The rules of the build phase: what can go on a hex, what can be demolished,
    /// and the stronghold's soil cleansing (<c>core/build-check.ts</c>,
    /// <c>core/demolish.ts</c>, <c>core/soil-cleanse.ts</c>). Every check returns
    /// <c>null</c> when the action is allowed, or the reason why not, so a hint
    /// cannot promise what the click then refuses.
    /// </summary>
    public static class BuildRules
    {
        public static string BuildRefusal(Player player, HexTile hex, Building building, int stoneDiscount)
        {
            if (!Buildings.IsUnlocked(player, building))
            {
                return "«" + building.Label + "» откроется после победы над боссом";
            }

            if (building.Id == BuildingId.Converter && Buildings.HasConverter(player))
            {
                return "«" + building.Label + "» уже построен";
            }

            if (Stronghold.IsStrongholdHex(player, hex.Id))
            {
                return "Здесь стоит " + Stronghold.Label.ToLowerInvariant();
            }

            if (hex.Building.HasValue)
            {
                return "Гекс уже занят";
            }

            if (!Buildings.CanBuildOn(building, hex.Biome))
            {
                return "«" + building.Label + "» нельзя строить на биоме «" + Biomes.Get(hex.Biome).Label + "»";
            }

            if (!Buildings.CanAfford(player.Resources, building, stoneDiscount))
            {
                return "Не хватает ресурсов на «" + building.Label + "»";
            }

            return null;
        }

        public static string DemolishRefusal(Player player, HexTile hex)
        {
            if (player.StrongholdHexId == hex.Id)
            {
                return "Твердыню снести нельзя";
            }

            if (!hex.Building.HasValue)
            {
                return "Здесь нечего сносить";
            }

            return null;
        }

        /// <summary>Removes the building. Its toxicity is wiped: the spec's "уменьшение токсичности через уничтожение".</summary>
        public static void Demolish(HexTile hex)
        {
            hex.Building = null;
            hex.Toxicity = 0;
        }

        /// <summary>A hex that can take the cleansing while <paramref name="sacrificeHexId"/> is sacrificed.</summary>
        public static bool IsPurifiable(HexTile hex, string sacrificeHexId)
        {
            return hex.Id != sacrificeHexId && hex.Toxicity > 0;
        }

        /// <summary>
        /// Any hex can be sacrificed, empty, built or dead, except the stronghold
        /// hex. The island must stay one piece.
        /// </summary>
        public static string SacrificeRefusal(Player player, HexTile hex)
        {
            if (player.StrongholdHexId == hex.Id)
            {
                return "Гекс твердыни уничтожить нельзя";
            }

            List<Axial> rest = player.Hexes.Where(other => other.Id != hex.Id).Select(other => other.Axial).ToList();
            if (!HexMath.IsConnected(rest))
            {
                return "Без этого гекса остров распадётся на части";
            }

            // A sacrifice with nothing left to purify would only shrink the island.
            if (!player.Hexes.Any(other => IsPurifiable(other, hex.Id)))
            {
                return "Кроме этого гекса, на острове нет токсичной почвы";
            }

            return null;
        }

        public static string PurifyRefusal(HexTile hex, string sacrificeHexId)
        {
            if (hex.Id == sacrificeHexId)
            {
                return "Этот гекс будет уничтожен";
            }

            if (hex.Toxicity <= 0)
            {
                return "Почва здесь уже чистая";
            }

            return null;
        }

        /// <summary>Whether the stronghold can cleanse soil now. The action works once per turn.</summary>
        public static string SoilCleanseRefusal(Player player, bool usedThisTurn)
        {
            if (usedThisTurn)
            {
                return "Почву уже очищали в этот ход";
            }

            if (!player.Hexes.Any(hex => hex.Toxicity > 0))
            {
                return "На острове нет токсичной почвы";
            }

            if (!player.Hexes.Any(hex => SacrificeRefusal(player, hex) == null))
            {
                return "Нет гекса, который можно уничтожить ради очистки";
            }

            return null;
        }

        /// <summary>The sacrificed hex leaves the island with its building, and the target is clean.</summary>
        public static void CleanseSoil(Player player, string sacrificeHexId, string purifyHexId)
        {
            player.Hexes.RemoveAll(hex => hex.Id == sacrificeHexId);

            HexTile target = player.FindHex(purifyHexId);
            if (target != null)
            {
                target.Toxicity = 0;
            }
        }
    }
}
