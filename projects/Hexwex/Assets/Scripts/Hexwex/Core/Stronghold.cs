using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    /// <summary>
    /// The stronghold (твердыня) is the seat of a player's power
    /// (<c>core/stronghold.ts</c>). Every player puts exactly one on their island
    /// before the first turn. It is not one of the seven buildings: it cannot be
    /// built, demolished or built over. It still rolls a die in the tax phase, and
    /// it is the only source of power (власть).
    /// </summary>
    public static class Stronghold
    {
        public const string Label = "Твердыня";
        /// <summary>Power the stronghold pays at the end of every tax phase, on top of its roll.</summary>
        public const int PowerPerTurn = 1;
        /// <summary>The file name of the stronghold's icon and hex sprite in the prototype's assets.</summary>
        public const string ArtName = "stronghold";

        /// <summary>
        /// The stronghold's die: 3 wood, 3 stone, 3 food or 2 hammers. No face
        /// leaves toxicity, and the die is the same on every biome.
        /// </summary>
        public static readonly Face[] Faces =
        {
            new Face(ResourceId.Wood, 3, 0),
            new Face(ResourceId.Stone, 3, 0),
            new Face(ResourceId.Food, 3, 0),
            new Face(ResourceId.Hammers, 2, 0),
        };

        /// <summary>A stronghold goes on a hex of the player's own island that has no building.</summary>
        public static bool CanPlace(Player player, string hexId)
        {
            HexTile hex = player.FindHex(hexId);

            return hex != null && hex.Building == null;
        }

        public static bool IsStrongholdHex(Player player, string hexId)
        {
            return player != null && player.StrongholdHexId == hexId;
        }

        /// <summary>
        /// Puts the stronghold on a hex. A second call moves it. A building on the
        /// target hex is cleared: a bot may pick a built hex when its island has no
        /// free one.
        /// </summary>
        public static void Place(Player player, string hexId)
        {
            HexTile hex = player.FindHex(hexId);
            if (hex != null)
            {
                hex.Building = null;
            }

            player.StrongholdHexId = hexId;
        }

        /// <summary>
        /// Where a bot puts its stronghold: a free hex nearest to the island centre.
        /// A tie is broken by the seeded generator. Returns <c>null</c> only for an
        /// island with no hexes.
        /// </summary>
        public static string PickHex(Player player, Rng rng)
        {
            List<HexTile> candidates = player.Hexes.FindAll(hex => hex.Building == null);
            if (candidates.Count == 0)
            {
                candidates = player.Hexes;
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            int nearest = int.MaxValue;
            for (int index = 0; index < candidates.Count; index += 1)
            {
                nearest = Math.Min(nearest, HexMath.Distance(candidates[index].Q, candidates[index].R, 0, 0));
            }

            List<HexTile> central = candidates.FindAll(hex => HexMath.Distance(hex.Q, hex.R, 0, 0) == nearest);

            return rng.Pick(central).Id;
        }
    }

    /// <summary>
    /// Hit points of what stands on a hex (<c>core/structure-hp.ts</c>). A
    /// building that falls in battle is razed. The stronghold that falls becomes
    /// ruins at 0 hp: ruins roll no die and pay no power until they are repaired.
    /// At the end of every turn each damaged structure regains 25% of its max hp.
    /// </summary>
    public static class StructureHp
    {
        public const int StrongholdMaxHp = 600;
        public const double RepairSharePerTurn = 0.25;
        /// <summary>The stronghold also shoots: its bolt in the cleanup phase. The range is in hex steps.</summary>
        public const double DefenseDamage = 14;
        public const double DefenseCooldown = 1.3;
        public const double DefenseRange = 4;

        private static readonly Dictionary<BuildingId, int> BuildingMaxHp = new Dictionary<BuildingId, int>
        {
            { BuildingId.Farm, 120 },
            { BuildingId.Sawmill, 140 },
            { BuildingId.Village, 150 },
            { BuildingId.Observatory, 160 },
            { BuildingId.University, 170 },
            { BuildingId.Mine, 180 },
            { BuildingId.MasonsGuild, 200 },
            { BuildingId.Converter, 300 },
        };

        public static StructureKind ToKind(BuildingId buildingId)
        {
            return (StructureKind)(int)buildingId;
        }

        public static StructureKind? KindOn(Player player, HexTile hex)
        {
            if (player != null && player.StrongholdHexId == hex.Id)
            {
                return StructureKind.Stronghold;
            }

            return hex.Building.HasValue ? ToKind(hex.Building.Value) : (StructureKind?)null;
        }

        public static int MaxHp(StructureKind kind)
        {
            return kind == StructureKind.Stronghold ? StrongholdMaxHp : BuildingMaxHp[(BuildingId)(int)kind];
        }

        /// <summary>Current hp of what stands on the hex. Returns <c>false</c> for an empty hex.</summary>
        public static bool TryGet(Player player, HexTile hex, out StructureKind kind, out int hp, out int max)
        {
            StructureKind? found = KindOn(player, hex);
            if (!found.HasValue)
            {
                kind = default;
                hp = 0;
                max = 0;

                return false;
            }

            kind = found.Value;
            max = MaxHp(kind);
            hp = hex.DamagedKind == kind ? hex.DamagedHp : max;

            return true;
        }

        public static bool IsRuinedStronghold(Player player, HexTile hex)
        {
            return TryGet(player, hex, out StructureKind kind, out int hp, out _) && kind == StructureKind.Stronghold && hp <= 0;
        }

        /// <summary>Sets the structure on the hex to <paramref name="hp"/>. Full health drops the damage record.</summary>
        public static void Set(HexTile hex, StructureKind kind, double hp)
        {
            if (hp >= MaxHp(kind))
            {
                hex.DamagedKind = null;
                hex.DamagedHp = 0;

                return;
            }

            hex.DamagedKind = kind;
            hex.DamagedHp = Math.Max(0, JsMath.Round(hp));
        }

        /// <summary>The free repair at the end of a turn. <paramref name="skipHexIds"/> are ruins made this turn.</summary>
        public static void RepairIsland(Player player, ICollection<string> skipHexIds)
        {
            for (int index = 0; index < player.Hexes.Count; index += 1)
            {
                HexTile hex = player.Hexes[index];
                if (!TryGet(player, hex, out StructureKind kind, out int hp, out int max) || hp >= max)
                {
                    continue;
                }

                if (skipHexIds != null && skipHexIds.Contains(hex.Id))
                {
                    continue;
                }

                Set(hex, kind, hp + max * RepairSharePerTurn);
            }
        }
    }
}
