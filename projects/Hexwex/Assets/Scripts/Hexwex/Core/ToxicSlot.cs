using System;
using System.Collections.Generic;
using System.Linq;

namespace Hexwex.Core
{
    public enum SlotSymbol
    {
        Gear,
        Coin,
        Skull,
        Poison,
    }

    /// <summary>0 bad symbols: luck. 1: a small fortune. 2: a misfortune. 3: a disaster.</summary>
    public enum SlotOutcome
    {
        Luck,
        Fortune,
        Misfortune,
        Disaster,
    }

    public enum SlotEffectKind
    {
        /// <summary>The meter drops by a seeded amount in <c>Min..Max</c>.</summary>
        Meter,
        Gain,
        /// <summary>Takes <c>Percent</c> of a resource, at least <c>Min</c> while the pool has any.</summary>
        Loss,
        /// <summary>Healthy people go mad. Never more than the population.</summary>
        Madness,
        Deaths,
        Army,
        /// <summary>Random built hexes take more toxicity, never past the dead mark.</summary>
        Pollute,
        /// <summary>The most toxic hexes lose toxicity.</summary>
        Cleanse,
        /// <summary>The worm eats one building (never the stronghold) and poisons its hex.</summary>
        Worm,
    }

    /// <summary>
    /// One step of an event. The numbers are the rule; the catalog text only
    /// describes them. Every step is applied in order on the same seeded stream.
    /// </summary>
    public sealed class SlotEffect
    {
        public SlotEffectKind Kind;
        public ResourceId Resource;
        public int Min;
        public int Max;
        public int Amount;
        public int Percent;
        public int Count;
        public int Hexes;
        public int Pct;

        public static SlotEffect Meter(int min, int max)
        {
            return new SlotEffect { Kind = SlotEffectKind.Meter, Min = min, Max = max };
        }

        public static SlotEffect Gain(ResourceId resource, int amount)
        {
            return new SlotEffect { Kind = SlotEffectKind.Gain, Resource = resource, Amount = amount };
        }

        public static SlotEffect Loss(ResourceId resource, int percent, int min)
        {
            return new SlotEffect { Kind = SlotEffectKind.Loss, Resource = resource, Percent = percent, Min = min };
        }

        public static SlotEffect Madness(int count)
        {
            return new SlotEffect { Kind = SlotEffectKind.Madness, Count = count };
        }

        public static SlotEffect Deaths(int count)
        {
            return new SlotEffect { Kind = SlotEffectKind.Deaths, Count = count };
        }

        public static SlotEffect Army(int count)
        {
            return new SlotEffect { Kind = SlotEffectKind.Army, Count = count };
        }

        public static SlotEffect Pollute(int hexes, int pct)
        {
            return new SlotEffect { Kind = SlotEffectKind.Pollute, Hexes = hexes, Pct = pct };
        }

        public static SlotEffect Cleanse(int hexes, int pct)
        {
            return new SlotEffect { Kind = SlotEffectKind.Cleanse, Hexes = hexes, Pct = pct };
        }

        public static SlotEffect Worm(int pct)
        {
            return new SlotEffect { Kind = SlotEffectKind.Worm, Pct = pct };
        }
    }

    public sealed class SlotEvent
    {
        public readonly string Id;
        public readonly int Level;
        public readonly SlotOutcome Outcome;
        public readonly string Title;
        public readonly string Text;
        /// <summary>The icon's file name in the prototype's <c>assets/icons</c>, without the extension.</summary>
        public readonly string Icon;
        public readonly SlotEffect[] Effects;

        public SlotEvent(string id, int level, SlotOutcome outcome, string title, string text, string icon, params SlotEffect[] effects)
        {
            Id = id;
            Level = level;
            Outcome = outcome;
            Title = title;
            Text = text;
            Icon = icon;
            Effects = effects;
        }
    }

    /// <summary>One line of the event display: what really happened, with real numbers.</summary>
    public readonly struct SlotEffectLine
    {
        public readonly string Icon;
        public readonly string Text;
        public readonly bool IsGood;

        public SlotEffectLine(string icon, string text, bool isGood)
        {
            Icon = icon;
            Text = text;
            IsGood = isGood;
        }
    }

    public sealed class SlotSpin
    {
        public string PlayerId;
        public int Turn;
        public int Level;
        public SlotSymbol[] Reels;
        /// <summary>The reel the full meter forced to a skull, or -1.</summary>
        public int ForcedReel;
        public int BadCount;
        public SlotOutcome Outcome;
        public SlotEvent Event;
        public int MeterBefore;
        public int MeterAfter;
        public List<SlotEffectLine> Lines;
    }

    public readonly struct MeterZone
    {
        public readonly int Level;
        public readonly int From;
        public readonly int To;
        public readonly string Label;
        /// <summary>The status line under the meter's counter.</summary>
        public readonly string Status;

        public MeterZone(int level, int from, int to, string label, string status)
        {
            Level = level;
            From = from;
            To = to;
            Label = label;
            Status = status;
        }
    }

    /// <summary>
    /// The island's toxicity meter and the slot under it (<c>core/toxic-slot.ts</c>).
    ///
    /// The meter. Every player has one meter, 0..1000. In the tax phase each
    /// percent of toxicity a building leaves on its hex adds 2.5 points to the
    /// meter. The meter never drops by itself. Only a lucky spin lowers it.
    ///
    /// The slot. Once per turn, when the tax phase ends, a meter at "Малые" or
    /// above spins three reels. Each reel shows a bad symbol with the chance of
    /// the level. At a full meter one reel always shows a skull. The number of bad
    /// symbols picks the outcome, and the level and the outcome pick one event
    /// from the catalog. Everything is seeded by the world seed, the turn and the
    /// player, so a spin can be replayed exactly.
    /// </summary>
    public static class ToxicSlot
    {
        public const int MeterMax = 1000;
        public const double MeterPerHexToxicityPct = 2.5;
        public const int ReelCount = 3;

        public static readonly IReadOnlyList<MeterZone> Zones = new[]
        {
            new MeterZone(0, 0, 249, "Нет последствий", "Нет последствий"),
            new MeterZone(1, 250, 499, "Малые", "Малые последствия"),
            new MeterZone(2, 500, 799, "Значительные", "Значительные последствия"),
            new MeterZone(3, 800, MeterMax, "Катастрофические", "Катастрофические последствия"),
        };

        /// <summary>The chance that one reel shows a bad symbol, per level. Level 0 never spins.</summary>
        private static readonly double[] BadChanceByLevel = { 0, 0.35, 0.55, 0.75 };

        public static readonly IReadOnlyDictionary<SlotOutcome, string> OutcomeLabels = new Dictionary<SlotOutcome, string>
        {
            { SlotOutcome.Luck, "Удача" },
            { SlotOutcome.Fortune, "Малая удача" },
            { SlotOutcome.Misfortune, "Несчастье" },
            { SlotOutcome.Disaster, "Бедствие" },
        };

        /// <summary>
        /// The catalog: one event per level and outcome. The bad events grow from the
        /// bandits of "Малые" to the garbage worm of "Катастрофические", the lucky
        /// ones grow with the level too.
        /// </summary>
        public static readonly IReadOnlyList<SlotEvent> Events = new[]
        {
            new SlotEvent("clean-wind", 1, SlotOutcome.Luck, "Чистый ветер", "Ветер переменился и унёс ядовитую дымку за край острова.", "scouting",
                SlotEffect.Meter(100, 150)),
            new SlotEvent("spoil-heap", 1, SlotOutcome.Fortune, "Находка в отвалах", "Старатели перебрали отвалы и нашли годный камень и доски.", "stone",
                SlotEffect.Gain(ResourceId.Stone, 4), SlotEffect.Gain(ResourceId.Wood, 3)),
            new SlotEvent("bandits", 1, SlotOutcome.Misfortune, "Налёт бандитов", "По ядовитому следу острова пришли бандиты и унесли часть еды и камня.", "archer",
                SlotEffect.Loss(ResourceId.Food, 20, 1), SlotEffect.Loss(ResourceId.Stone, 20, 1)),
            new SlotEvent("fumes", 1, SlotOutcome.Disaster, "Испарения", "Над стоками поднялся сладковатый дым. Двое жителей перестали узнавать своих.", "mad",
                SlotEffect.Madness(2), SlotEffect.Loss(ResourceId.Food, 10, 1)),
            new SlotEvent("cleansing-rain", 2, SlotOutcome.Luck, "Очищающий ливень", "Тяжёлый ливень смыл отраву со склонов в море.", "mana",
                SlotEffect.Meter(150, 200)),
            new SlotEvent("caravan", 2, SlotOutcome.Fortune, "Брошенный караван", "В пустошах нашли ржавый караван с инструментом и печатями прежней власти.", "hammers",
                SlotEffect.Gain(ResourceId.Hammers, 4), SlotEffect.Gain(ResourceId.Power, 1)),
            new SlotEvent("undead", 2, SlotOutcome.Misfortune, "Налёт нечисти", "Отрава подняла то, что лежало в земле. Остров отбился, но потерял людей и бойцов.", "zombie",
                SlotEffect.Deaths(2), SlotEffect.Army(2)),
            new SlotEvent("toxic-fog", 2, SlotOutcome.Disaster, "Ядовитый туман", "Туман лёг на постройки. Трое сошли с ума, а земля под двумя зданиями прогнила.", "moth",
                SlotEffect.Madness(3), SlotEffect.Pollute(2, 15)),
            new SlotEvent("great-cleansing", 3, SlotOutcome.Luck, "Великое очищение", "Небо прорвало чистой водой. Остров дышит так, как не дышал много лет.", "mana",
                SlotEffect.Meter(200, 250)),
            new SlotEvent("witch-gift", 3, SlotOutcome.Fortune, "Дар ведьмы", "Болотная ведьма взяла плату отравой: два самых грязных гекса стали чище, в запасе прибавилось маны.", "witch",
                SlotEffect.Cleanse(2, 20), SlotEffect.Gain(ResourceId.Mana, 3)),
            new SlotEvent("ghouls", 3, SlotOutcome.Misfortune, "Ночь упырей", "На запах отравы пришли упыри. Утром не досчитались троих, ещё двое бредят.", "vampire",
                SlotEffect.Deaths(3), SlotEffect.Madness(2), SlotEffect.Army(1)),
            new SlotEvent("garbage-worm", 3, SlotOutcome.Disaster, "Мусорный червь", "Из недр поднялся мусорный червь. Он жрёт постройки и оставляет за собой отраву.", "leech",
                SlotEffect.Worm(40), SlotEffect.Pollute(2, 15), SlotEffect.Madness(2)),
        };

        public static bool IsBad(SlotSymbol symbol)
        {
            return symbol == SlotSymbol.Skull || symbol == SlotSymbol.Poison;
        }

        public static int ClampMeter(double value)
        {
            return Math.Max(0, Math.Min(MeterMax, JsMath.Round(value)));
        }

        public static int MeterLevel(int value)
        {
            int level = 0;

            for (int index = 0; index < Zones.Count; index += 1)
            {
                if (value >= Zones[index].From)
                {
                    level = Zones[index].Level;
                }
            }

            return level;
        }

        public static MeterZone GetZone(int level)
        {
            return Zones[level];
        }

        /// <summary>Meter points for toxicity, in percent, that the tax phase leaves on a hex.</summary>
        public static int MeterGain(int hexToxicityPct)
        {
            return JsMath.Round(hexToxicityPct * MeterPerHexToxicityPct);
        }

        public static void AddMeterGain(Player player, int hexToxicityPct)
        {
            if (hexToxicityPct <= 0)
            {
                return;
            }

            player.ToxicMeter = ClampMeter(player.ToxicMeter + MeterGain(hexToxicityPct));
        }

        public static SlotEvent FindEvent(int level, SlotOutcome outcome)
        {
            for (int index = 0; index < Events.Count; index += 1)
            {
                if (Events[index].Level == level && Events[index].Outcome == outcome)
                {
                    return Events[index];
                }
            }

            throw new InvalidOperationException("No slot event for level " + level + " and outcome " + outcome);
        }

        /// <summary>
        /// The whole spin of one player in one turn, applied to the player. Returns
        /// <c>null</c> when the meter is below "Малые" and the slot stays off. The
        /// same player, turn and seed always give the same spin.
        /// </summary>
        public static SlotSpin Roll(Player player, int turn, string seed)
        {
            int level = MeterLevel(player.ToxicMeter);
            if (level == 0)
            {
                return null;
            }

            Rng rng = Rng.FromText(seed + ":slot:" + turn + ":" + player.Id);
            SlotSymbol[] reels = RollReels(rng, level, player.ToxicMeter >= MeterMax, out int forcedReel);
            int badCount = reels.Count(IsBad);
            SlotOutcome outcome = (SlotOutcome)Math.Max(0, Math.Min(ReelCount, badCount));
            SlotEvent slotEvent = FindEvent(level, outcome);
            int meterBefore = player.ToxicMeter;
            List<SlotEffectLine> lines = new List<SlotEffectLine>();

            for (int index = 0; index < slotEvent.Effects.Length; index += 1)
            {
                ApplyEffect(player, slotEvent.Effects[index], rng, lines);
            }

            if (lines.Count == 0)
            {
                lines.Add(new SlotEffectLine("check", "Обошлось без потерь", true));
            }

            return new SlotSpin
            {
                PlayerId = player.Id,
                Turn = turn,
                Level = level,
                Reels = reels,
                ForcedReel = forcedReel,
                BadCount = badCount,
                Outcome = outcome,
                Event = slotEvent,
                MeterBefore = meterBefore,
                MeterAfter = player.ToxicMeter,
                Lines = lines,
            };
        }

        /// <summary>
        /// The three reels. Every reel draws the same two numbers whatever it shows,
        /// so the stream stays aligned. At a full meter one seeded reel is a skull.
        /// </summary>
        private static SlotSymbol[] RollReels(Rng rng, int level, bool isFull, out int forcedReel)
        {
            double chance = BadChanceByLevel[level];
            forcedReel = isFull ? rng.RandomInt(0, ReelCount - 1) : -1;
            SlotSymbol[] reels = new SlotSymbol[ReelCount];

            for (int index = 0; index < ReelCount; index += 1)
            {
                bool isBad = rng.Next() < chance;
                bool isFirstOfPair = rng.Next() < 0.5;

                if (index == forcedReel)
                {
                    reels[index] = SlotSymbol.Skull;
                }
                else if (isBad)
                {
                    reels[index] = isFirstOfPair ? SlotSymbol.Skull : SlotSymbol.Poison;
                }
                else
                {
                    reels[index] = isFirstOfPair ? SlotSymbol.Gear : SlotSymbol.Coin;
                }
            }

            return reels;
        }

        /// <summary>"1 житель", "2 жителя", "5 жителей".</summary>
        public static string Plural(int count, string one, string few, string many)
        {
            int mod10 = count % 10;
            int mod100 = count % 100;
            if (mod10 == 1 && mod100 != 11)
            {
                return one;
            }

            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
            {
                return few;
            }

            return many;
        }

        private static void AddResource(Player player, ResourceId resource, int delta)
        {
            player.Resources[resource] = Math.Max(0, player.Resources[resource] + delta);
        }

        /// <summary>Picks up to <paramref name="count"/> different items, seeded.</summary>
        private static List<T> PickSome<T>(Rng rng, IEnumerable<T> items, int count)
        {
            List<T> pool = new List<T>(items);
            List<T> picked = new List<T>();

            while (picked.Count < count && pool.Count > 0)
            {
                int index = rng.NextIndex(pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return picked;
        }

        private static void ApplyEffect(Player player, SlotEffect effect, Rng rng, List<SlotEffectLine> lines)
        {
            switch (effect.Kind)
            {
                case SlotEffectKind.Meter:
                {
                    int drop = rng.RandomInt(effect.Min, effect.Max);
                    int next = ClampMeter(player.ToxicMeter - drop);
                    lines.Add(new SlotEffectLine("toxicity", "Шкала токсичности −" + (player.ToxicMeter - next), true));
                    player.ToxicMeter = next;

                    return;
                }

                case SlotEffectKind.Gain:
                {
                    AddResource(player, effect.Resource, effect.Amount);
                    ResourceInfo info = ResourceTable.Get(effect.Resource);
                    lines.Add(new SlotEffectLine(info.Icon, "+" + effect.Amount + " " + info.Label, true));

                    return;
                }

                case SlotEffectKind.Loss:
                {
                    int held = player.Resources[effect.Resource];
                    int taken = Math.Min(held, Math.Max(effect.Min, held * effect.Percent / 100));
                    if (taken <= 0)
                    {
                        return;
                    }

                    AddResource(player, effect.Resource, -taken);
                    ResourceInfo info = ResourceTable.Get(effect.Resource);
                    lines.Add(new SlotEffectLine(info.Icon, "−" + taken + " " + info.Label, false));

                    return;
                }

                case SlotEffectKind.Madness:
                {
                    int moved = Math.Min(effect.Count, player.Resources[ResourceId.Population]);
                    if (moved <= 0)
                    {
                        return;
                    }

                    AddResource(player, ResourceId.Population, -moved);
                    AddResource(player, ResourceId.Mad, moved);
                    lines.Add(new SlotEffectLine("mad", moved + " " + Plural(moved, "житель сошёл", "жителя сошли", "жителей сошли") + " с ума", false));

                    return;
                }

                case SlotEffectKind.Deaths:
                {
                    int lost = Math.Min(effect.Count, player.Resources[ResourceId.Population]);
                    if (lost <= 0)
                    {
                        return;
                    }

                    AddResource(player, ResourceId.Population, -lost);
                    lines.Add(new SlotEffectLine("population", "−" + lost + " " + ResourceTable.Get(ResourceId.Population).Label, false));

                    return;
                }

                case SlotEffectKind.Army:
                {
                    int lost = Math.Min(effect.Count, player.Army);
                    if (lost <= 0)
                    {
                        return;
                    }

                    player.Army -= lost;
                    lines.Add(new SlotEffectLine("army", "−" + lost + " " + Plural(lost, "боец", "бойца", "бойцов"), false));

                    return;
                }

                case SlotEffectKind.Pollute:
                {
                    List<HexTile> living = player.Hexes.FindAll(hex => hex.Toxicity < Tax.DeadToxicityPct);
                    List<HexTile> built = living.FindAll(hex => hex.Building.HasValue || hex.Id == player.StrongholdHexId);
                    List<HexTile> targets = PickSome(rng, built.Count > 0 ? built : living, effect.Hexes);
                    if (targets.Count == 0)
                    {
                        return;
                    }

                    foreach (HexTile hex in targets)
                    {
                        hex.Toxicity = Math.Min(Tax.DeadToxicityPct, hex.Toxicity + effect.Pct);
                    }

                    lines.Add(new SlotEffectLine("toxicity", "+" + effect.Pct + "% токсичности на " + targets.Count + " " + Plural(targets.Count, "гексе", "гексах", "гексах"), false));

                    return;
                }

                case SlotEffectKind.Cleanse:
                {
                    // The ids are ASCII, so ordinal order equals the prototype's localeCompare.
                    List<HexTile> dirty = player.Hexes
                        .Where(hex => hex.Toxicity > 0)
                        .OrderByDescending(hex => hex.Toxicity)
                        .ThenBy(hex => hex.Id, StringComparer.Ordinal)
                        .Take(effect.Hexes)
                        .ToList();
                    if (dirty.Count == 0)
                    {
                        return;
                    }

                    foreach (HexTile hex in dirty)
                    {
                        hex.Toxicity = Math.Max(0, hex.Toxicity - effect.Pct);
                    }

                    lines.Add(new SlotEffectLine("toxicity", "−" + effect.Pct + "% токсичности на " + dirty.Count + " " + Plural(dirty.Count, "гексе", "гексах", "гексах"), true));

                    return;
                }

                default:
                {
                    ApplyWorm(player, effect, rng, lines);

                    return;
                }
            }
        }

        /// <summary>The worm. The stronghold is the island's heart and is never eaten.</summary>
        private static void ApplyWorm(Player player, SlotEffect effect, Rng rng, List<SlotEffectLine> lines)
        {
            List<HexTile> prey = player.Hexes.FindAll(hex => hex.Building.HasValue && hex.Id != player.StrongholdHexId);
            if (prey.Count == 0)
            {
                // Nothing to eat: the worm burrows through a living hex and poisons it.
                List<HexTile> living = player.Hexes.FindAll(hex => hex.Toxicity < Tax.DeadToxicityPct);
                if (living.Count == 0)
                {
                    return;
                }

                HexTile burrow = living[rng.NextIndex(living.Count)];
                burrow.Toxicity = Math.Min(Tax.DeadToxicityPct, burrow.Toxicity + effect.Pct);
                lines.Add(new SlotEffectLine("leech", "Червю нечего есть: он отравил гекс (+" + effect.Pct + "%)", false));

                return;
            }

            HexTile victim = prey[rng.NextIndex(prey.Count)];
            string label = Buildings.Get(victim.Building.Value).Label;
            victim.Building = null;
            victim.Toxicity = Math.Min(Tax.DeadToxicityPct, victim.Toxicity + effect.Pct);
            lines.Add(new SlotEffectLine("leech", "Червь сожрал: " + label + " (+" + effect.Pct + "% на гексе)", false));
        }
    }
}
