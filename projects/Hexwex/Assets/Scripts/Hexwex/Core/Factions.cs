using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    public enum FactionId
    {
        ChildrenOfRoots,
        AzureConcordat,
        IronFrontier,
        WhiteSpark,
        CrimsonTide,
        Helios,
    }

    public sealed class Faction
    {
        public FactionId Id;
        /// <summary>The prototype's id, which also names the icon (<c>Icons/faction_&lt;key&gt;</c>).</summary>
        public string Key;
        public string Name;
        /// <summary>A few words under the name.</summary>
        public string Tagline;
        /// <summary>The opening paragraph of the lore.</summary>
        public string Summary;
        /// <summary>The lore bullets, in the order of the design text.</summary>
        public string[] Lore;
        public string Strength;
        public string Weakness;
        /// <summary>
        /// The game biomes the faction settles in. The lore also names places the
        /// game has no biome for (coasts, ruins, coves), so those are left out here
        /// and covered by the island-size rules in <see cref="Factions.Pick"/>.
        /// </summary>
        public BiomeId[] Biomes;

        public string Icon
        {
            get { return "faction_" + Key; }
        }
    }

    /// <summary>
    /// The six factions of the island world (<c>core/factions.ts</c>). Their mobs
    /// hold the wild islands of the globe. World generation gives every held
    /// island one owning faction, guided by the biomes each faction prefers.
    /// Helios holds exactly one island: the boss's lair, its city Nexus Arcology.
    /// </summary>
    public static class Factions
    {
        /// <summary>The one city of Helios. It stands in the boss's lair.</summary>
        public const string NexusArcologyName = "Аркология Нексус";

        /// <summary>A faction from the biome list weighs this much in the roll.</summary>
        private const int BiomeWeight = 2;
        /// <summary>A faction from the island-size rules weighs this much in the roll.</summary>
        private const int SizeWeight = 1;
        /// <summary>A cell with this many wild islands or more counts as large land.</summary>
        private const int LargeIslandCount = 3;

        /// <summary>The order is the order of <see cref="FactionId"/>, and <see cref="Pick"/> rolls by it.</summary>
        public static readonly IReadOnlyList<Faction> All = new[]
        {
            new Faction
            {
                Id = FactionId.ChildrenOfRoots,
                Key = "children_of_roots",
                Name = "Дети Корней",
                Tagline = "Союз независимых племён",
                Summary = "Союз независимых племён, живущих на разных островах. По уровню развития ближе всего к стартовой позиции игрока.",
                Lore = new[]
                {
                    "Селятся в лугах, лесах, джунглях и болотах.",
                    "Хорошо добывают еду и быстро увеличивают население.",
                    "Используют охотников, копейщиков, шаманов и приручённых животных.",
                    "Могут передвигаться по лесам без штрафов.",
                    "Строят деревни, святилища, фермы и лесные мастерские.",
                    "Не любят шахты, вырубку лесов и промышленное загрязнение.",
                    "Между их племенами нет полного единства: с одними можно дружить, пока другие будут нападать.",
                },
                Strength = "численность.",
                Weakness = "примитивное оружие и отсутствие тяжёлых укреплений.",
                Biomes = new[] { BiomeId.Grassland, BiomeId.Forrest, BiomeId.Rainforest, BiomeId.Swamp },
            },
            new Faction
            {
                Id = FactionId.AzureConcordat,
                Key = "azure_concordat",
                Name = "Лазурный Конкордат",
                Tagline = "Федерация портовых городов",
                Summary = "Богатая федерация портовых городов, контролирующая торговлю между островами.",
                Lore = new[]
                {
                    "Селится на побережьях, равнинах и возле утёсов.",
                    "Строит порты, рынки, склады, верфи и торговые фактории.",
                    "Получает бонусы от небесных маршрутов и торговли ресурсами.",
                    "Использует быстрые корабли, морскую пехоту и наёмников.",
                    "Предпочитает покупать территории и союзников, а не захватывать их.",
                    "Может вводить торговую блокаду и перекупать нейтральные поселения.",
                    "Продаёт игроку редкие ресурсы и информацию о мире.",
                },
                Strength = "деньги, флот и дипломатия.",
                Weakness = "слабая сухопутная армия и зависимость от портов.",
                Biomes = new[] { BiomeId.Plains, BiomeId.Cliffs },
            },
            new Faction
            {
                Id = FactionId.IronFrontier,
                Key = "iron_frontier",
                Name = "Железный Предел",
                Tagline = "Государство шахтёров и заводов",
                Summary = "Суровое государство шахтёров, инженеров и заводских городов. Для него остров — это прежде всего залежи ресурсов.",
                Lore = new[]
                {
                    "Селится в холмах, горах, бесплодных землях и возле вулканов.",
                    "Быстро добывает уголь, железо, медь и другие металлы.",
                    "Строит шахты, карьеры, литейные заводы и железные крепости.",
                    "Использует тяжёлую пехоту, артиллерию и бронированные машины.",
                    "Может истощать месторождения ради временного ускорения производства.",
                    "Загрязняет соседние гексы и портит отношения с туземцами.",
                    "Охотно покупает еду, но почти никогда не продаёт оружие.",
                },
                Strength = "производство, укрепления и тяжёлая армия.",
                Weakness = "нехватка еды, медленное перемещение и загрязнение земель.",
                Biomes = new[] { BiomeId.Hills, BiomeId.Mountains, BiomeId.Badlands, BiomeId.Volcano },
            },
            new Faction
            {
                Id = FactionId.WhiteSpark,
                Key = "white_spark",
                Name = "Белая Искра",
                Tagline = "Союз учёных и исследователей",
                Summary = "Союз учёных, исследователей и изгнанных инженеров. Они пытаются понять древние технологии и происхождение островного мира.",
                Lore = new[]
                {
                    "Ищут кратеры, полярные пустыни, руины и редкие минералы.",
                    "Строят лаборатории, обсерватории и исследовательские станции.",
                    "Получают много очков науки от уникальных месторождений.",
                    "Используют небольшие армии с экспериментальным оружием.",
                    "Могут исследовать гексы на большом расстоянии.",
                    "Быстро развиваются, если получают уран, литий и редкоземельные металлы.",
                    "Знают больше остальных о сверхразвитом острове-городе.",
                },
                Strength = "технологии, разведка и особые устройства.",
                Weakness = "малое население и дорогое производство.",
                Biomes = new[] { BiomeId.Crater, BiomeId.PolarDesert },
            },
            new Faction
            {
                Id = FactionId.CrimsonTide,
                Key = "crimson_tide",
                Name = "Алый Прилив",
                Tagline = "Совет пиратских капитанов",
                Summary = "Объединение пиратских капитанов, беглых рабов, контрабандистов и военных вождей.",
                Lore = new[]
                {
                    "Занимает небольшие острова, бухты и скрытые базы возле утёсов.",
                    "Грабит торговые маршруты и прибрежные поселения.",
                    "Строит пиратские гавани, тайники, таверны и рынки контрабанды.",
                    "Использует дешёвые корабли, налётчиков и диверсантов.",
                    "Может захватывать вражеские корабли и воровать ресурсы.",
                    "Иногда заключает временные союзы, но легко нарушает договоры.",
                    "Единой власти нет: фракцией управляет совет капитанов.",
                },
                Strength = "мобильность, грабежи и внезапные атаки.",
                Weakness = "плохая дисциплина, слабые города и внутренние конфликты.",
                Biomes = new[] { BiomeId.Cliffs },
            },
            new Faction
            {
                Id = FactionId.Helios,
                Key = "helios",
                Name = "Гелиос",
                Tagline = "Остров-город под властью ИИ",
                Summary = "Гелиос представлен одним огромным технологическим островом-городом — Аркологией Нексус. Весь остров покрыт зданиями, энергетическими сетями, заводами и оборонительными системами. Город управляется древним искусственным интеллектом. Его жители считают остальные фракции примитивными и не вмешиваются в их войны, пока те не становятся угрозой.",
                Lore = new[]
                {
                    "Не основывает новые поселения и не захватывает обычные острова.",
                    "Начинает с открытыми технологиями высшего уровня.",
                    "Имеет энергетические щиты, дронов, боевых роботов и авиацию.",
                    "Получает ресурсы из автоматических производственных комплексов.",
                    "Может уничтожить обычную армию прямым столкновением.",
                    "Контролирует спутники и видит почти всю карту.",
                    "Периодически требует дань или забирает редкие ископаемые.",
                    "Атакует только нарушителей своей территории и слишком развитые державы.",
                },
                Strength = "абсолютно всё — технологии, оборона и армия.",
                Weakness = "зависимость от единого энергетического ядра и центрального ИИ.",
                Biomes = new BiomeId[0],
            },
        };

        public static Faction Get(FactionId id)
        {
            return All[(int)id];
        }

        /// <summary>
        /// The owner of a held island, rolled with <paramref name="roll"/> in [0, 1).
        /// - A faction whose biome list holds the biome gets weight 2.
        /// - Large land (3 wild islands) adds the Azure Concordat with weight 1.
        /// - A lone small island adds the Crimson Tide with weight 1.
        /// - A biome no faction names falls back to the closest faction.
        /// Helios never takes part: it holds only the boss's lair.
        /// </summary>
        public static FactionId Pick(BiomeId biome, int islandCount, double roll)
        {
            // The prototype rolls over a Map, which keeps the order of first insertion.
            List<FactionId> order = new List<FactionId>();
            Dictionary<FactionId, int> weights = new Dictionary<FactionId, int>();

            void Add(FactionId id, int weight)
            {
                if (!weights.ContainsKey(id))
                {
                    order.Add(id);
                    weights[id] = 0;
                }

                weights[id] += weight;
            }

            foreach (Faction faction in All)
            {
                if (Array.IndexOf(faction.Biomes, biome) >= 0)
                {
                    Add(faction.Id, BiomeWeight);
                }
            }

            if (order.Count == 0)
            {
                Add(Fallback(biome), BiomeWeight);
            }

            if (islandCount >= LargeIslandCount)
            {
                Add(FactionId.AzureConcordat, SizeWeight);
            }

            if (islandCount == 1)
            {
                Add(FactionId.CrimsonTide, SizeWeight);
            }

            int total = 0;
            foreach (FactionId id in order)
            {
                total += weights[id];
            }

            double left = roll * total;
            foreach (FactionId id in order)
            {
                left -= weights[id];
                if (left < 0)
                {
                    return id;
                }
            }

            return order[order.Count - 1];
        }

        /// <summary>
        /// Biomes no faction names in its lore. Each one goes to the faction whose
        /// land it is closest to, so every held island gets an owner.
        /// </summary>
        private static FactionId Fallback(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.Tundra:
                // The game has no ruins biome. The desert stands in for "руины".
                case BiomeId.Desert:
                    return FactionId.WhiteSpark;
                default:
                    return FactionId.ChildrenOfRoots;
            }
        }
    }
}
