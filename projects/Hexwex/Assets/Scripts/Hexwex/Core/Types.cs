using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    /// <summary>The 16 hex biomes of the spec, spelled as the spec spells them, typo included.</summary>
    public enum BiomeId
    {
        Grassland,
        Plains,
        Forrest,
        Savanna,
        Rainforest,
        Taiga,
        Tundra,
        Desert,
        PolarDesert,
        Swamp,
        Badlands,
        Crater,
        Volcano,
        Hills,
        Mountains,
        Cliffs,
    }

    /// <summary>
    /// Everything the resources panel shows. The first eight can stand on a die.
    /// Power is paid by the stronghold, and the mad are the negative resource.
    /// Toxicity is not a resource: it lives on the hexes and in the meter.
    /// </summary>
    public enum ResourceId
    {
        Food,
        Stone,
        Wood,
        Population,
        Hammers,
        Science,
        Scouting,
        Mana,
        Power,
        Mad,
    }

    /// <summary>The seven buildings of the spec, and the central converter that ends the game.</summary>
    public enum BuildingId
    {
        Converter,
        Farm,
        Mine,
        Sawmill,
        Village,
        MasonsGuild,
        Observatory,
        University,
    }

    /// <summary>What can stand on a hex and take damage: a building or the stronghold.</summary>
    public enum StructureKind
    {
        Converter,
        Farm,
        Mine,
        Sawmill,
        Village,
        MasonsGuild,
        Observatory,
        University,
        Stronghold,
    }

    public enum Phase
    {
        Build,
        Tax,
        Scout,
        Clear,
    }

    /// <summary>In <c>Setup</c> every player places a stronghold. <c>Play</c> is the core loop.</summary>
    public enum GameStage
    {
        Setup,
        Play,
    }

    /// <summary>One face of a die: a yield paired with the toxicity it leaves on the hex.</summary>
    public readonly struct Face
    {
        public readonly ResourceId Resource;
        public readonly int Amount;
        public readonly int Toxicity;

        public Face(ResourceId resource, int amount, int toxicity)
        {
            Resource = resource;
            Amount = amount;
            Toxicity = toxicity;
        }
    }

    public readonly struct BuildCost
    {
        public readonly int Stone;
        public readonly int Wood;
        public readonly int Hammers;

        public BuildCost(int stone, int wood, int hammers)
        {
            Stone = stone;
            Wood = wood;
            Hammers = hammers;
        }
    }

    public sealed class ResourcePool
    {
        private readonly int[] _amounts = new int[Enum.GetValues(typeof(ResourceId)).Length];

        public int this[ResourceId id]
        {
            get { return _amounts[(int)id]; }
            set { _amounts[(int)id] = value; }
        }

        public ResourcePool Clone()
        {
            ResourcePool copy = new ResourcePool();
            Array.Copy(_amounts, copy._amounts, _amounts.Length);

            return copy;
        }
    }

    public sealed class HexTile
    {
        /// <summary><c>q,r</c> as a string.</summary>
        public string Id;
        public int Q;
        public int R;
        public BiomeId Biome;
        public BuildingId? Building;
        /// <summary>Accumulated toxicity of the hex, in percent, 0..100.</summary>
        public int Toxicity;
        /// <summary>
        /// Battle damage of what stands on the hex. It names what it belongs to, so
        /// a new building on the hex starts at full health. <c>null</c> means full health.
        /// </summary>
        public StructureKind? DamagedKind;
        public int DamagedHp;

        public Axial Axial
        {
            get { return new Axial(Q, R); }
        }
    }

    public sealed class Player
    {
        public string Id;
        public string Nickname;
        /// <summary>Banner colour, as the prototype's <c>#rrggbb</c>.</summary>
        public string Color;
        public bool IsHuman;
        public List<HexTile> Hexes = new List<HexTile>();
        public ResourcePool Resources = new ResourcePool();
        public int Army;
        public int Techs;
        /// <summary>The world cell the island is flying over.</summary>
        public string CellId = "";
        /// <summary>The hex the stronghold stands on, or <c>null</c> before it is placed.</summary>
        public string StrongholdHexId;
        /// <summary>The island's toxicity meter, 0..1000.</summary>
        public int ToxicMeter;
        /// <summary>The player holds the boss's trophy technology. It unlocks the central converter.</summary>
        public bool BossSlain;
        /// <summary>Losing every building is a defeat only for a player who had something to lose.</summary>
        public bool HasHadBuildings;
        /// <summary>A defeated rival is out of the game: it no longer plays its phases.</summary>
        public bool Eliminated;

        public HexTile FindHex(string hexId)
        {
            for (int index = 0; index < Hexes.Count; index += 1)
            {
                if (Hexes[index].Id == hexId)
                {
                    return Hexes[index];
                }
            }

            return null;
        }
    }
}
