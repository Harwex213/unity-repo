using System;
using System.Collections.Generic;
using System.Linq;

namespace Hexwex.Core
{
    public enum HexDieSource
    {
        Building,
        Stronghold,
    }

    /// <summary>
    /// The die that stands on a hex (<c>core/dice.ts</c>): a building's die on its
    /// biome, or the stronghold's die. Everything that asks "what does this hex
    /// roll" asks <see cref="On"/>.
    /// </summary>
    public sealed class HexDie
    {
        public HexDieSource Source;
        public string Label;
        public string ArtName;
        public Face[] Faces;
        /// <summary>Index of the face the biome adds, or -1 when the die has none.</summary>
        public int BiomeFaceIndex;

        public static HexDie On(Player player, HexTile hex)
        {
            if (Stronghold.IsStrongholdHex(player, hex.Id))
            {
                // Ruins left by a lost battle roll nothing until they are repaired.
                if (StructureHp.IsRuinedStronghold(player, hex))
                {
                    return null;
                }

                return new HexDie
                {
                    Source = HexDieSource.Stronghold,
                    Label = Stronghold.Label,
                    ArtName = Stronghold.ArtName,
                    Faces = Stronghold.Faces,
                    BiomeFaceIndex = -1,
                };
            }

            if (!hex.Building.HasValue)
            {
                return null;
            }

            Building building = Buildings.Get(hex.Building.Value);
            Face[] faces = Buildings.FacesOn(building, hex.Biome);

            return new HexDie
            {
                Source = HexDieSource.Building,
                Label = building.Label,
                ArtName = building.ArtName,
                Faces = faces,
                BiomeFaceIndex = building.BiomeFaces.ContainsKey(hex.Biome) ? faces.Length - 1 : -1,
            };
        }
    }

    /// <summary>
    /// The rules of the tax phase (<c>core/tax.ts</c>). Every building rolls one
    /// face of its die; the hex's own toxicity eats into what the roll pays out
    /// and then grows by what the roll leaves behind.
    /// </summary>
    public static class Tax
    {
        /// <summary>A hex this toxic produces nothing at all: the spec's "клетка бесполезна".</summary>
        public const int DeadToxicityPct = 100;
        /// <summary>Past this, the spec forbids food outright, not merely reduces it.</summary>
        public const int FoodBlockedToxicityPct = 50;
        /// <summary>One toxicity point on a face is worth this much of the hex.</summary>
        public const int ToxicityPerFacePointPct = 4;

        public static bool IsDead(HexTile hex)
        {
            return hex.Toxicity >= DeadToxicityPct;
        }

        public static bool IsFoodBlocked(HexTile hex)
        {
            return hex.Toxicity >= FoodBlockedToxicityPct;
        }

        /// <summary>What the face actually pays after the hex's toxicity has taken its cut.</summary>
        public static int EffectiveYield(Face face, HexTile hex)
        {
            if (IsDead(hex))
            {
                return 0;
            }

            if (face.Resource == ResourceId.Food && IsFoodBlocked(hex))
            {
                return 0;
            }

            return Math.Max(0, JsMath.Round(face.Amount * (1 - hex.Toxicity / 100.0)));
        }

        /// <summary>How much the hex is dirtied by the roll, never past the dead mark.</summary>
        public static int ToxicityGain(Face face, HexTile hex)
        {
            if (IsDead(hex))
            {
                return 0;
            }

            return Math.Min(DeadToxicityPct - hex.Toxicity, face.Toxicity * ToxicityPerFacePointPct);
        }

        /// <summary>The island's whole toxicity load: the sum over its hexes.</summary>
        public static int TotalToxicity(Player player)
        {
            return player.Hexes.Sum(hex => hex.Toxicity);
        }

        /// <summary>
        /// What one face really pays on one hex this turn: the yield after the hex's
        /// toxicity and the irrigation bonus, and the toxicity in percent it leaves.
        /// </summary>
        public static void FacePayout(Face face, HexTile hex, TechEffects effects, out int amount, out int toxicity)
        {
            int raw = EffectiveYield(face, hex);
            // Irrigation adds to a roll that pays food, but never revives a dead hex.
            amount = raw > 0 && face.Resource == ResourceId.Food ? raw + effects.FoodBonus : raw;
            toxicity = JsMath.Round(ToxicityGain(face, hex) * effects.ToxicityMultiplier);
        }

        /// <summary>
        /// The madness step of the tax phase. <paramref name="converted"/> people go
        /// mad, asylums send up to <paramref name="curedPerTurn"/> of the mad back to
        /// work, and every mad person left eats one food without working.
        /// </summary>
        public static void ApplyMadness(Player player, int converted, int curedPerTurn)
        {
            ResourcePool pool = player.Resources;
            int cured = Math.Min(pool[ResourceId.Mad], curedPerTurn);
            int mad = pool[ResourceId.Mad] + converted - cured;

            pool[ResourceId.Population] = pool[ResourceId.Population] - converted + cured;
            pool[ResourceId.Mad] = mad;
            pool[ResourceId.Food] = Math.Max(0, pool[ResourceId.Food] - mad);
        }
    }

    public sealed class TaxRoll
    {
        public string HexId;
        /// <summary>What stood on the hex at the roll. A plan never pays a die that changed.</summary>
        public StructureKind DieKey;
        public Face[] Faces;
        public int BiomeFaceIndex;
        public int RolledIndex;
        /// <summary>The face the phase will pay. Equals <c>RolledIndex</c> until power changes it.</summary>
        public int ChosenIndex;
    }

    /// <summary>One line of the collection: what one hex pays and what it leaves behind.</summary>
    public readonly struct TaxPayout
    {
        public readonly string HexId;
        public readonly ResourceId Resource;
        public readonly int Amount;
        /// <summary>Percent added to the hex's own toxicity.</summary>
        public readonly int Toxicity;

        public TaxPayout(string hexId, ResourceId resource, int amount, int toxicity)
        {
            HexId = hexId;
            Resource = resource;
            Amount = amount;
            Toxicity = toxicity;
        }
    }

    /// <summary>
    /// The interactive tax phase (<c>core/tax-plan.ts</c>). At the start of the
    /// phase every die on an island is rolled once, and the rolls are kept in a
    /// plan. While the phase lasts the player may spend power to swap a rolled
    /// face for another face of the same die: 1 power for a base face, 2 for the
    /// biome face, and going back to the rolled face is free. Nothing is paid
    /// until the phase ends.
    /// </summary>
    public sealed class TaxPlan
    {
        public const int PowerFaceCost = 1;
        public const int PowerBiomeFaceCost = 2;
        /// <summary>For a bot: one percent of hex toxicity is worth a quarter of a resource.</summary>
        private const double BotToxicityWeight = 0.25;

        public string PlayerId;
        public int Turn;
        public List<TaxRoll> Rolls = new List<TaxRoll>();

        /// <summary>
        /// Rolls every die on the island once. The rolls are seeded by the world
        /// seed, the turn and the player, so a turn always rolls the same.
        /// </summary>
        public static TaxPlan Roll(Player player, int turn, string seed)
        {
            Rng rng = Rng.FromText(seed + ":tax:" + turn + ":" + player.Id);
            TaxPlan plan = new TaxPlan { PlayerId = player.Id, Turn = turn };

            for (int index = 0; index < player.Hexes.Count; index += 1)
            {
                HexTile hex = player.Hexes[index];
                HexDie die = HexDie.On(player, hex);
                StructureKind? dieKey = StructureHp.KindOn(player, hex);
                if (die == null || !dieKey.HasValue || die.Faces.Length == 0)
                {
                    continue;
                }

                int rolledIndex = rng.RandomInt(0, die.Faces.Length - 1);

                plan.Rolls.Add(new TaxRoll
                {
                    HexId = hex.Id,
                    DieKey = dieKey.Value,
                    Faces = die.Faces,
                    BiomeFaceIndex = die.BiomeFaceIndex,
                    RolledIndex = rolledIndex,
                    ChosenIndex = rolledIndex,
                });
            }

            return plan;
        }

        public TaxRoll FindRoll(string hexId)
        {
            return Rolls.Find(roll => roll.HexId == hexId);
        }

        /// <summary>The power a face costs against the roll. The rolled face is always free.</summary>
        public static int PickCost(TaxRoll roll, int index)
        {
            if (index == roll.RolledIndex)
            {
                return 0;
            }

            return index == roll.BiomeFaceIndex ? PowerBiomeFaceCost : PowerFaceCost;
        }

        public int PowerSpent()
        {
            return Rolls.Sum(roll => PickCost(roll, roll.ChosenIndex));
        }

        /// <summary>The power still free to spend, after the choices already in the plan.</summary>
        public int PowerLeft(Player player)
        {
            return player.Resources[ResourceId.Power] - PowerSpent();
        }

        /// <summary>
        /// Whether the face at <paramref name="index"/> can become the paid face of
        /// the die on the hex. Returns <c>null</c> when it can, or the reason why
        /// not. The power already spent on this building counts as free, because a
        /// new pick refunds it.
        /// </summary>
        public string PickRefusal(Player player, string hexId, int index)
        {
            TaxRoll roll = FindRoll(hexId);
            HexTile hex = player.FindHex(hexId);
            if (roll == null || hex == null || index < 0 || index >= roll.Faces.Length)
            {
                return "На этом гексе нечего бросать";
            }

            if (index == roll.ChosenIndex || index == roll.RolledIndex)
            {
                return null;
            }

            if (Tax.IsDead(hex))
            {
                return "Гекс мёртв (" + Tax.DeadToxicityPct + "%): любая грань даст 0";
            }

            if (roll.Faces[index].Resource == ResourceId.Food && Tax.IsFoodBlocked(hex))
            {
                return "Токсичность выше 50%: еда здесь не вырастет";
            }

            int cost = PickCost(roll, index);
            int available = PowerLeft(player) + PickCost(roll, roll.ChosenIndex);
            if (available < cost)
            {
                return "Не хватает власти: нужно " + cost + ", есть " + Math.Max(0, available);
            }

            return null;
        }

        /// <summary>Makes one die pay another face. The caller checks the refusal.</summary>
        public void Pick(string hexId, int index)
        {
            TaxRoll roll = FindRoll(hexId);
            if (roll != null)
            {
                roll.ChosenIndex = index;
            }
        }

        /// <summary>
        /// Everything the plan pays, hex by hex, with the stronghold's power income
        /// as its own line. A roll whose die is gone or changed pays nothing.
        /// </summary>
        public List<TaxPayout> Payouts(Player player, TechEffects effects)
        {
            List<TaxPayout> payouts = new List<TaxPayout>();
            // The central converter turns the toxicity of every building of its owner off.
            bool isConverted = Buildings.HasConverter(player);

            for (int index = 0; index < Rolls.Count; index += 1)
            {
                TaxRoll roll = Rolls[index];
                HexTile hex = player.FindHex(roll.HexId);
                if (hex == null || StructureHp.KindOn(player, hex) != roll.DieKey)
                {
                    continue;
                }

                Face face = roll.Faces[roll.ChosenIndex];
                Tax.FacePayout(face, hex, effects, out int amount, out int toxicity);
                payouts.Add(new TaxPayout(hex.Id, face.Resource, amount, isConverted ? 0 : toxicity));

                if (roll.DieKey == StructureKind.Stronghold)
                {
                    payouts.Add(new TaxPayout(hex.Id, ResourceId.Power, Stronghold.PowerPerTurn, 0));
                }
            }

            return payouts;
        }

        /// <summary>
        /// Pays one payout line: the yield joins the pool, the hex gets dirtier, and
        /// the same percent fills the island's meter.
        /// </summary>
        public static void ApplyPayout(Player player, TaxPayout payout)
        {
            player.Resources[payout.Resource] += payout.Amount;

            if (payout.Toxicity == 0)
            {
                return;
            }

            HexTile hex = player.FindHex(payout.HexId);
            if (hex != null)
            {
                hex.Toxicity = Math.Min(Tax.DeadToxicityPct, hex.Toxicity + payout.Toxicity);
            }

            ToxicSlot.AddMeterGain(player, payout.Toxicity);
        }

        /// <summary>
        /// The whole end of the tax phase in one step: power spent, every payout,
        /// then the mad eat. Returns the payout lines, for the view to animate.
        /// </summary>
        public List<TaxPayout> Collect(Player player, TechEffects effects)
        {
            // The payouts are read before anything is paid: one hex never pays twice
            // against the toxicity its own first line has just left.
            List<TaxPayout> payouts = Payouts(player, effects);

            player.Resources[ResourceId.Power] = Math.Max(0, player.Resources[ResourceId.Power] - PowerSpent());

            for (int index = 0; index < payouts.Count; index += 1)
            {
                ApplyPayout(player, payouts[index]);
            }

            Tax.ApplyMadness(player, 0, effects.MadCuredPerTurn);

            return payouts;
        }

        /// <summary>
        /// A bot's picks: it swaps the rolls that gain the most per power first,
        /// while its power lasts. A face is worth its payout minus a quarter per
        /// percent of toxicity it leaves.
        /// </summary>
        public void ApplyBotPicks(Player player, TechEffects effects)
        {
            List<(string hexId, int index, double ratio)> candidates = new List<(string, int, double)>();

            for (int rollIndex = 0; rollIndex < Rolls.Count; rollIndex += 1)
            {
                TaxRoll roll = Rolls[rollIndex];
                HexTile hex = player.FindHex(roll.HexId);
                if (hex == null || Tax.IsDead(hex))
                {
                    continue;
                }

                double rolledWorth = Worth(roll.Faces[roll.RolledIndex], hex, effects);
                int bestIndex = roll.RolledIndex;
                double bestGain = 0;

                for (int faceIndex = 0; faceIndex < roll.Faces.Length; faceIndex += 1)
                {
                    double gain = Worth(roll.Faces[faceIndex], hex, effects) - rolledWorth;
                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        bestIndex = faceIndex;
                    }
                }

                if (bestIndex != roll.RolledIndex)
                {
                    candidates.Add((roll.HexId, bestIndex, bestGain / PickCost(roll, bestIndex)));
                }
            }

            // Stable, as the prototype's sort is: equal ratios keep the island's hex order.
            foreach ((string hexId, int index, double _) in candidates.OrderByDescending(candidate => candidate.ratio))
            {
                if (PickRefusal(player, hexId, index) == null)
                {
                    Pick(hexId, index);
                }
            }
        }

        private static double Worth(Face face, HexTile hex, TechEffects effects)
        {
            Tax.FacePayout(face, hex, effects, out int amount, out int toxicity);

            return amount - toxicity * BotToxicityWeight;
        }
    }
}
