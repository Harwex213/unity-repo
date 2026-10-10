using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    public enum UnitClass
    {
        Melee,
        Ranged,
        Cavalry,
        Air,
    }

    /// <summary>What a ranged attacker throws. It only changes how the shot is drawn and how fast it flies.</summary>
    public enum Projectile
    {
        None,
        Stone,
        Arrow,
        Bullet,
        Hex,
        Bolt,
    }

    /// <summary>
    /// Combat stats of the cleanup phase. Distances are in hex steps and speeds in
    /// hex steps per second, so the numbers read the same at any zoom. One hex step
    /// is the distance between two neighbouring hex centres.
    /// </summary>
    public class Combatant
    {
        /// <summary>The prototype's id: <c>militia</c>, <c>wolf</c>, and so on.</summary>
        public string Key;
        public string Label;
        /// <summary>The icon in <c>Resources/Icons</c>.</summary>
        public string Icon;
        public int Hp;
        /// <summary>Damage of one hit, before the ±15% roll.</summary>
        public double Damage;
        /// <summary>Seconds between two hits.</summary>
        public double Cooldown;
        /// <summary>Reach in hex steps. Melee is 1: it hits the same or a neighbouring hex.</summary>
        public double Range = 1;
        /// <summary>Hex steps per second.</summary>
        public double Speed;
        /// <summary>A flyer crosses open water and ignores hex paths.</summary>
        public bool Flying;
        /// <summary><see cref="Core.Projectile.None"/> for melee.</summary>
        public Projectile Projectile;
    }

    public sealed class Unit : Combatant
    {
        public UnitClass Class;
        /// <summary>How many people the unit costs to field.</summary>
        public int Upkeep;
    }

    public sealed class Enemy : Combatant
    {
    }

    /// <summary>
    /// The rosters of the spec (<c>core/units.ts</c>). Melee, ranged, cavalry and
    /// air are the player's; the ten creatures are what sits on an uncleared
    /// island.
    ///
    /// The spec writes the cavalry list with the same four names as the ranged
    /// one. That is kept as written: the ids differ, the labels say which is which.
    /// </summary>
    public static class Units
    {
        /* The table is wide on purpose: one row is one unit, so rows compare at a glance. */
        public static readonly IReadOnlyList<Unit> All = new[]
        {
            Melee("militia", "Ополченец", "militia", 30, 6, 1, 1.1, 1),
            Melee("spearman", "Копейщик", "spearman", 42, 8, 0.9, 1.1, 1),
            Melee("swordsman", "Мечник", "swordsman", 60, 12, 0.9, 1.1, 2),
            Melee("halberdier", "Алебардист", "halberdier", 74, 17, 1.1, 1, 2),
            Melee("knight", "Рыцарь", "knight", 110, 22, 1, 1.2, 3),
            Ranged("slinger", "Пращник", "slinger", UnitClass.Ranged, 24, 5, 1.1, 2.2, 1, 1, Projectile.Stone),
            Ranged("archer", "Лучник", "archer", UnitClass.Ranged, 28, 7, 1.2, 3, 1, 1, Projectile.Arrow),
            Ranged("longbowman", "Длинный лучник", "longbowman", UnitClass.Ranged, 32, 10, 1.4, 4, 0.95, 2, Projectile.Arrow),
            Ranged("musketeer", "Мушкетёр", "musketeer", UnitClass.Ranged, 36, 19, 2.2, 3.5, 0.9, 3, Projectile.Bullet),
            Ranged("cavalry_slinger", "Пращник (кавалерия)", "cavalry", UnitClass.Cavalry, 44, 6, 1.1, 2.2, 2, 2, Projectile.Stone),
            Ranged("cavalry_archer", "Лучник (кавалерия)", "cavalry", UnitClass.Cavalry, 50, 8, 1.2, 3, 2, 2, Projectile.Arrow),
            Ranged("cavalry_longbowman", "Длинный лучник (кавалерия)", "cavalry", UnitClass.Cavalry, 56, 11, 1.4, 4, 1.9, 3, Projectile.Arrow),
            Ranged("cavalry_musketeer", "Мушкетёр (кавалерия)", "cavalry", UnitClass.Cavalry, 62, 20, 2.2, 3.5, 1.9, 4, Projectile.Bullet),
            Air("crow", "Ворона", "crow", 26, 6, 0.8, 2.6, 1),
            Air("great_eagle", "Великий орёл", "great-eagle", 54, 12, 0.9, 2.8, 3),
            Air("griffin", "Грифон", "griffin", 96, 20, 1, 2.5, 4),
        };

        public static readonly IReadOnlyList<Enemy> Enemies = new[]
        {
            Foe("wolf", "Волк", "wolf", 32, 7, 0.8, 1.6),
            Foe("spider", "Паук", "spider", 26, 6, 0.8, 1.3),
            Foe("leech", "Пиявка", "leech", 40, 5, 0.7, 0.7),
            Foe("skeleton", "Скелет", "skeleton", 44, 9, 1, 1),
            Foe("zombie", "Зомби", "zombie", 62, 9, 1.2, 0.6),
            Foe("ogre", "Огр", "ogre", 120, 24, 1.6, 0.8),
            Foe("witch", "Ведьма", "witch", 46, 12, 1.6, 0.9, 3, false, Projectile.Hex),
            Foe("vampire", "Вампир", "vampire", 88, 16, 0.9, 1.4),
            Foe("moth", "Моль", "moth", 30, 7, 0.9, 2.2, 1, true),
            Foe("bat", "Летучая мышь", "bat", 24, 5, 0.7, 2.6, 1, true),
            // The boss. It only lives in its lair on the world map.
            Foe("plague_lord", "Повелитель Мора", "boss", 450, 20, 1.5, 0.55, 2.5, false, Projectile.Hex),
        };

        public static Unit Get(string key)
        {
            foreach (Unit unit in All)
            {
                if (unit.Key == key)
                {
                    return unit;
                }
            }

            throw new ArgumentException("Unknown unit: " + key);
        }

        public static Enemy GetEnemy(string key)
        {
            foreach (Enemy enemy in Enemies)
            {
                if (enemy.Key == key)
                {
                    return enemy;
                }
            }

            throw new ArgumentException("Unknown enemy: " + key);
        }

        /// <summary>
        /// Who goes to the clearing phase. The spec never names a military building,
        /// so the army is levied from the population: each unit costs people, and
        /// only researched units can be fielded. The militia needs no research.
        /// </summary>
        public static List<Unit> BuildRoster(int population, ICollection<string> unlocked, Rng rng)
        {
            List<Unit> pool = new List<Unit>();
            foreach (Unit unit in All)
            {
                if (unlocked.Contains(unit.Key))
                {
                    pool.Add(unit);
                }
            }

            List<Unit> roster = new List<Unit>();
            int budget = population;

            while (budget > 0)
            {
                List<Unit> affordable = pool.FindAll(unit => unit.Upkeep <= budget);
                if (affordable.Count == 0)
                {
                    break;
                }

                Unit picked = rng.Pick(affordable);
                roster.Add(picked);
                budget -= picked.Upkeep;
            }

            return roster;
        }

        private static Unit Melee(string key, string label, string icon, int hp, double damage, double cooldown, double speed, int upkeep)
        {
            return new Unit { Key = key, Label = label, Icon = icon, Class = UnitClass.Melee, Hp = hp, Damage = damage, Cooldown = cooldown, Speed = speed, Upkeep = upkeep };
        }

        private static Unit Ranged(string key, string label, string icon, UnitClass unitClass, int hp, double damage, double cooldown, double range, double speed, int upkeep, Projectile projectile)
        {
            return new Unit { Key = key, Label = label, Icon = icon, Class = unitClass, Hp = hp, Damage = damage, Cooldown = cooldown, Range = range, Speed = speed, Upkeep = upkeep, Projectile = projectile };
        }

        private static Unit Air(string key, string label, string icon, int hp, double damage, double cooldown, double speed, int upkeep)
        {
            return new Unit { Key = key, Label = label, Icon = icon, Class = UnitClass.Air, Hp = hp, Damage = damage, Cooldown = cooldown, Speed = speed, Upkeep = upkeep, Flying = true };
        }

        private static Enemy Foe(string key, string label, string icon, int hp, double damage, double cooldown, double speed, double range = 1, bool flying = false, Projectile projectile = Projectile.None)
        {
            return new Enemy { Key = key, Label = label, Icon = icon, Hp = hp, Damage = damage, Cooldown = cooldown, Speed = speed, Range = range, Flying = flying, Projectile = projectile };
        }
    }

    /// <summary>
    /// An active skill of the cleanup phase (<c>core/skills.ts</c>). A skill is
    /// cast on one hex of any island in reach and is paid with mana. The battle
    /// keeps the cooldown; the player keeps the pool, so mana spent in battle is
    /// gone after it. There is one skill so far.
    /// </summary>
    public static class ShatterSkill
    {
        public const string Label = "Разрушить землю";
        public const string Description = "Разрушает гекс острова: врага или свой, например токсичный. Части острова, которые потеряли связь с ядром, откалываются и падают. Гекс твердыни разрушить нельзя.";
        public const int ManaCost = 2;
        public const double CooldownSeconds = 6;
        public const string HotkeyLabel = "Q";
        /// <summary>How far past the rim of the player's island the skill reaches, in the prototype's units.</summary>
        public const double Reach = 700;
    }
}
