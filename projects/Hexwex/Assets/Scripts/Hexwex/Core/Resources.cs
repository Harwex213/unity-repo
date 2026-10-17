using System.Collections.Generic;

namespace Hexwex.Core
{
    public enum ResourceKind
    {
        Basic,
        Special,
        Negative,
    }

    public sealed class ResourceInfo
    {
        public readonly ResourceId Id;
        /// <summary>The icon's file name in the prototype's <c>assets/icons</c>, without the extension.</summary>
        public readonly string Icon;
        public readonly string Label;
        public readonly ResourceKind Kind;
        /// <summary>What the resource is spent on, shown in the resources panel tooltip.</summary>
        public readonly string Feeds;

        public ResourceInfo(ResourceId id, string icon, string label, ResourceKind kind, string feeds)
        {
            Id = id;
            Icon = icon;
            Label = label;
            Kind = kind;
            Feeds = feeds;
        }
    }

    /// <summary>
    /// The resource table of the spec (<c>core/resources.ts</c>): three basic
    /// resources, six special ones and one negative one.
    /// </summary>
    public static class ResourceTable
    {
        public static readonly IReadOnlyList<ResourceInfo> All = new[]
        {
            new ResourceInfo(ResourceId.Food, "food", "Еда", ResourceKind.Basic, "даёт население"),
            new ResourceInfo(ResourceId.Stone, "stone", "Камень", ResourceKind.Basic, "даёт здания"),
            new ResourceInfo(ResourceId.Wood, "wood", "Дерево", ResourceKind.Basic, "даёт здания"),
            new ResourceInfo(ResourceId.Population, "population", "Население", ResourceKind.Special, "даёт армию"),
            new ResourceInfo(ResourceId.Hammers, "hammers", "Молотки", ResourceKind.Special, "даёт здания"),
            new ResourceInfo(ResourceId.Science, "science", "Наука", ResourceKind.Special, "даёт технологии"),
            new ResourceInfo(ResourceId.Scouting, "scouting", "Разведка", ResourceKind.Special, "даёт разведку"),
            new ResourceInfo(ResourceId.Mana, "mana", "Мана", ResourceKind.Special, "даёт активные скиллы"),
            new ResourceInfo(ResourceId.Power, "power", "Власть", ResourceKind.Special, "меняет выпавшую грань здания"),
            new ResourceInfo(ResourceId.Mad, "mad", "Сумасшедшие", ResourceKind.Negative, "съедает население"),
        };

        public static ResourceInfo Get(ResourceId id)
        {
            return All[(int)id];
        }

        /// <summary>
        /// What a player starts the first build phase with. Enough stone, wood and
        /// hammers for a handful of buildings, so the first turn is a real decision.
        /// </summary>
        public static ResourcePool CreateStartingPool()
        {
            ResourcePool pool = new ResourcePool();
            pool[ResourceId.Food] = 10;
            pool[ResourceId.Stone] = 12;
            pool[ResourceId.Wood] = 12;
            pool[ResourceId.Population] = 6;
            pool[ResourceId.Hammers] = 6;
            // Enough to scout once before an observatory is standing.
            pool[ResourceId.Scouting] = 2;
            // Enough for one face change in the first tax phase, or two cheap ones.
            pool[ResourceId.Power] = 2;
            // Enough for two casts of «Разрушить землю» in the first battles.
            pool[ResourceId.Mana] = 4;

            return pool;
        }
    }
}
