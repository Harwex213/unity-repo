using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    public enum TrailEventId
    {
        Bandits,
        Undead,
        Madness,
        Worm,
    }

    public sealed class TrailEvent
    {
        public readonly TrailEventId Id;
        public readonly string Title;
        public readonly string Text;

        public TrailEvent(TrailEventId id, string title, string text)
        {
            Id = id;
            Title = title;
            Text = text;
        }
    }

    /// <summary>
    /// The toxic trail a player leaves in a world cell never fades, and the spec
    /// lets it throw negative events at them — or nothing at all
    /// (<c>core/trail-events.ts</c>).
    /// </summary>
    public static class TrailEvents
    {
        /// <summary>The trail worth one percentage point of event chance.</summary>
        private const int TrailPerChancePoint = 8;
        private const double MaxEventChance = 0.6;

        public static readonly IReadOnlyList<TrailEvent> All = new[]
        {
            new TrailEvent(TrailEventId.Bandits, "Налёт бандитов", "Из отравленных пустошей пришли люди с мешками. Часть камня и дерева унесли с собой."),
            new TrailEvent(TrailEventId.Undead, "Налёт нечисти", "Шлейф поднял то, что лежало под ним. Остров потерял людей, отбиваясь."),
            new TrailEvent(TrailEventId.Madness, "Испарения шлейфа", "Ветер принёс сладковатый дым. Ещё несколько человек перестали узнавать своих."),
            new TrailEvent(TrailEventId.Worm, "Мусорный червь", "Из недр поднялся червь и сожрал постройку вместе с половиной гекса."),
        };

        public static double EventChance(int trail)
        {
            return Math.Min(MaxEventChance, trail / (TrailPerChancePoint * 100.0));
        }

        /// <summary>Rolls the trail. Most turns it gives back nothing, as the spec allows.</summary>
        public static TrailEvent Roll(int trail, Rng rng)
        {
            if (rng.Next() > EventChance(trail))
            {
                return null;
            }

            return rng.Pick(All);
        }
    }
}
