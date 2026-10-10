using System.Collections.Generic;

namespace Hexwex.Core
{
    public enum TechBranch
    {
        Military,
        Economy,
        Ecology,
    }

    public enum TechId
    {
        Spears,
        Swords,
        Halberds,
        Knighthood,
        Slings,
        Archery,
        Longbow,
        Gunpowder,
        Stables,
        HeavyStables,
        Falconry,
        GriffinRoost,
        Masonry,
        Irrigation,
        Filters,
        Asylums,
        CentralConversion,
    }

    public sealed class Tech
    {
        public readonly TechId Id;
        public readonly string Label;
        public readonly TechBranch Branch;
        public readonly int Cost;
        public readonly TechId[] Requires;
        public readonly string Description;
        /// <summary>Unit ids of <c>core/units.ts</c> this technology fields in the clearing phase.</summary>
        public readonly string[] Unlocks;
        /// <summary>A trophy is not researched: the boss drops it. Science cannot buy it.</summary>
        public readonly bool Trophy;

        public Tech(TechId id, string label, TechBranch branch, int cost, TechId[] requires, string description, string[] unlocks, bool trophy = false)
        {
            Id = id;
            Label = label;
            Branch = branch;
            Cost = cost;
            Requires = requires;
            Description = description;
            Unlocks = unlocks;
            Trophy = trophy;
        }
    }

    /// <summary>Everything the researched set changes, gathered in one place.</summary>
    public sealed class TechEffects
    {
        public List<string> UnlockedUnits = new List<string>();
        public int StoneDiscount;
        public int FoodBonus;
        public double ToxicityMultiplier = 1;
        public int MadCuredPerTurn;

        /// <summary>The bots have no technologies of their own.</summary>
        public static readonly TechEffects None = Techs.Effects(new TechId[0]);
    }

    /// <summary>
    /// The technology tree (<c>core/techs.ts</c>). Science unlocks the units the
    /// clearing phase fields, cheapens the build phase and slows the toxicity the
    /// tax phase produces.
    /// </summary>
    public static class Techs
    {
        /// <summary>The boss's trophy. It unlocks the central converter.</summary>
        public const TechId BossTechId = TechId.CentralConversion;

        private const double FiltersToxicityMultiplier = 0.75;

        private static readonly TechId[] NoTechs = new TechId[0];
        private static readonly string[] NoUnits = new string[0];

        /// <summary>The order is the order of <see cref="TechId"/>.</summary>
        public static readonly IReadOnlyList<Tech> All = new[]
        {
            new Tech(TechId.Spears, "Копья", TechBranch.Military, 8, NoTechs, "Копейщик достаёт врага раньше, чем тот дотянется до него.", new[] { "spearman" }),
            new Tech(TechId.Swords, "Мечи", TechBranch.Military, 18, new[] { TechId.Spears }, "Мечник — прочная середина строя.", new[] { "swordsman" }),
            new Tech(TechId.Halberds, "Алебарды", TechBranch.Military, 30, new[] { TechId.Swords }, "Алебардист бьёт дальше мечника и держит удар.", new[] { "halberdier" }),
            new Tech(TechId.Knighthood, "Рыцарство", TechBranch.Military, 48, new[] { TechId.Halberds }, "Рыцарь переживает то, что убивает всех остальных.", new[] { "knight" }),
            new Tech(TechId.Slings, "Пращи", TechBranch.Military, 8, NoTechs, "Пращник — первый, кто бьёт на расстоянии.", new[] { "slinger" }),
            new Tech(TechId.Archery, "Стрельба из лука", TechBranch.Military, 20, new[] { TechId.Slings }, "Лучник бьёт вдвое дальше пращника.", new[] { "archer" }),
            new Tech(TechId.Longbow, "Длинный лук", TechBranch.Military, 34, new[] { TechId.Archery }, "Длинный лучник достаёт врага через весь разрыв между островами.", new[] { "longbowman" }),
            new Tech(TechId.Gunpowder, "Порох", TechBranch.Military, 52, new[] { TechId.Longbow }, "Мушкетёр бьёт сильнее всех стрелков.", new[] { "musketeer" }),
            new Tech(TechId.Stables, "Конюшни", TechBranch.Military, 24, new[] { TechId.Slings }, "Кавалерия быстрее пехоты вдвое и живёт дольше стрелков.", new[] { "cavalry_slinger", "cavalry_archer" }),
            new Tech(TechId.HeavyStables, "Тяжёлая кавалерия", TechBranch.Military, 44, new[] { TechId.Stables, TechId.Longbow }, "Длинный лучник и мушкетёр в седле.", new[] { "cavalry_longbowman", "cavalry_musketeer" }),
            new Tech(TechId.Falconry, "Соколиная охота", TechBranch.Military, 26, NoTechs, "Ворона и великий орёл достают воздушных врагов.", new[] { "crow", "great_eagle" }),
            new Tech(TechId.GriffinRoost, "Гнездо грифонов", TechBranch.Military, 56, new[] { TechId.Falconry, TechId.Knighthood }, "Грифон — самое живучее, что есть у острова.", new[] { "griffin" }),
            new Tech(TechId.Masonry, "Каменная кладка", TechBranch.Economy, 14, NoTechs, "Каждое здание обходится на 1 камень дешевле.", NoUnits),
            new Tech(TechId.Irrigation, "Ирригация", TechBranch.Economy, 16, NoTechs, "Каждый бросок, который даёт еду, даёт на 1 еду больше.", NoUnits),
            new Tech(TechId.Filters, "Фильтры", TechBranch.Ecology, 22, NoTechs, "Здания пачкают свой гекс на четверть меньше.", NoUnits),
            new Tech(TechId.Asylums, "Лечебницы", TechBranch.Ecology, 30, new[] { TechId.Filters }, "Каждый ход один сумасшедший возвращается к работе.", NoUnits),
            new Tech(TechId.CentralConversion, "Центральная конверсия", TechBranch.Ecology, 0, NoTechs, "Трофей босса. Открывает Центральный конвертер: он отключает токсичность всех ваших зданий.", NoUnits, true),
        };

        public static Tech Get(TechId id)
        {
            return All[(int)id];
        }

        public static TechEffects Effects(ICollection<TechId> researched)
        {
            TechEffects effects = new TechEffects();
            // The militia needs no research: an island can always arm its farmers.
            effects.UnlockedUnits.Add("militia");

            for (int index = 0; index < All.Count; index += 1)
            {
                if (researched.Contains(All[index].Id))
                {
                    effects.UnlockedUnits.AddRange(All[index].Unlocks);
                }
            }

            effects.StoneDiscount = researched.Contains(TechId.Masonry) ? 1 : 0;
            effects.FoodBonus = researched.Contains(TechId.Irrigation) ? 1 : 0;
            effects.ToxicityMultiplier = researched.Contains(TechId.Filters) ? FiltersToxicityMultiplier : 1;
            effects.MadCuredPerTurn = researched.Contains(TechId.Asylums) ? 1 : 0;

            return effects;
        }

        public static bool IsAvailable(Tech tech, ICollection<TechId> researched)
        {
            if (tech.Trophy || researched.Contains(tech.Id))
            {
                return false;
            }

            for (int index = 0; index < tech.Requires.Length; index += 1)
            {
                if (!researched.Contains(tech.Requires[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
