import azureConcordatIcon from "../assets/factions/azure_concordat.png";
import childrenOfRootsIcon from "../assets/factions/children_of_roots.png";
import crimsonTideIcon from "../assets/factions/crimson_tide.png";
import heliosIcon from "../assets/factions/helios.png";
import ironFrontierIcon from "../assets/factions/iron_frontier.png";
import whiteSparkIcon from "../assets/factions/white_spark.png";
import azureConcordatLargeIcon from "../assets/factions/large/azure_concordat.png";
import childrenOfRootsLargeIcon from "../assets/factions/large/children_of_roots.png";
import crimsonTideLargeIcon from "../assets/factions/large/crimson_tide.png";
import heliosLargeIcon from "../assets/factions/large/helios.png";
import ironFrontierLargeIcon from "../assets/factions/large/iron_frontier.png";
import whiteSparkLargeIcon from "../assets/factions/large/white_spark.png";
import type { TBiomeId } from "./types";

/**
 * The six factions of the island world. Their mobs hold the wild islands of
 * the globe. World generation gives every held island one owning faction,
 * guided by the biomes each faction prefers. Helios holds exactly one island:
 * the boss's lair, its city Nexus Arcology.
 */

type TFactionId =
  | "children_of_roots"
  | "azure_concordat"
  | "iron_frontier"
  | "white_spark"
  | "crimson_tide"
  | "helios";

type TFaction = {
  readonly id: TFactionId;
  readonly name: string;
  /** A few words under the name. */
  readonly tagline: string;
  /** The opening paragraph of the lore. */
  readonly summary: string;
  /** The lore bullets, in the order of the design text. */
  readonly lore: readonly string[];
  readonly strength: string;
  readonly weakness: string;
  /**
   * The game biomes the faction settles in. The lore also names places the
   * game has no biome for (coasts, ruins, coves), so those are left out here
   * and covered by the island-size rules in `pickFaction`.
   */
  readonly biomes: readonly TBiomeId[];
  /** 64x64, for the globe badge, the cell panel and the list. */
  readonly icon: string;
  /** 256x256, for the details pane of the factions modal. */
  readonly iconLarge: string;
};

/** The one city of Helios. It stands in the boss's lair. */
const NEXUS_ARCOLOGY = { id: "nexus_arcology", name: "Аркология Нексус" } as const;

const FACTIONS: readonly TFaction[] = [
  {
    id: "children_of_roots",
    name: "Дети Корней",
    summary: "Союз независимых племён, живущих на разных островах. По уровню развития ближе всего к стартовой позиции игрока.",
    tagline: "Союз независимых племён",
    lore: [
      "Селятся в лугах, лесах, джунглях и болотах.",
      "Хорошо добывают еду и быстро увеличивают население.",
      "Используют охотников, копейщиков, шаманов и приручённых животных.",
      "Могут передвигаться по лесам без штрафов.",
      "Строят деревни, святилища, фермы и лесные мастерские.",
      "Не любят шахты, вырубку лесов и промышленное загрязнение.",
      "Между их племенами нет полного единства: с одними можно дружить, пока другие будут нападать.",
    ],
    strength: "численность.",
    weakness: "примитивное оружие и отсутствие тяжёлых укреплений.",
    biomes: ["grassland", "forrest", "rainforest", "swamp"],
    icon: childrenOfRootsIcon,
    iconLarge: childrenOfRootsLargeIcon,
  },
  {
    id: "azure_concordat",
    name: "Лазурный Конкордат",
    summary: "Богатая федерация портовых городов, контролирующая торговлю между островами.",
    tagline: "Федерация портовых городов",
    lore: [
      "Селится на побережьях, равнинах и возле утёсов.",
      "Строит порты, рынки, склады, верфи и торговые фактории.",
      "Получает бонусы от небесных маршрутов и торговли ресурсами.",
      "Использует быстрые корабли, морскую пехоту и наёмников.",
      "Предпочитает покупать территории и союзников, а не захватывать их.",
      "Может вводить торговую блокаду и перекупать нейтральные поселения.",
      "Продаёт игроку редкие ресурсы и информацию о мире.",
    ],
    strength: "деньги, флот и дипломатия.",
    weakness: "слабая сухопутная армия и зависимость от портов.",
    biomes: ["plains", "cliffs"],
    icon: azureConcordatIcon,
    iconLarge: azureConcordatLargeIcon,
  },
  {
    id: "iron_frontier",
    name: "Железный Предел",
    summary: "Суровое государство шахтёров, инженеров и заводских городов. Для него остров — это прежде всего залежи ресурсов.",
    tagline: "Государство шахтёров и заводов",
    lore: [
      "Селится в холмах, горах, бесплодных землях и возле вулканов.",
      "Быстро добывает уголь, железо, медь и другие металлы.",
      "Строит шахты, карьеры, литейные заводы и железные крепости.",
      "Использует тяжёлую пехоту, артиллерию и бронированные машины.",
      "Может истощать месторождения ради временного ускорения производства.",
      "Загрязняет соседние гексы и портит отношения с туземцами.",
      "Охотно покупает еду, но почти никогда не продаёт оружие.",
    ],
    strength: "производство, укрепления и тяжёлая армия.",
    weakness: "нехватка еды, медленное перемещение и загрязнение земель.",
    biomes: ["hills", "mountains", "badlands", "volcano"],
    icon: ironFrontierIcon,
    iconLarge: ironFrontierLargeIcon,
  },
  {
    id: "white_spark",
    name: "Белая Искра",
    summary: "Союз учёных, исследователей и изгнанных инженеров. Они пытаются понять древние технологии и происхождение островного мира.",
    tagline: "Союз учёных и исследователей",
    lore: [
      "Ищут кратеры, полярные пустыни, руины и редкие минералы.",
      "Строят лаборатории, обсерватории и исследовательские станции.",
      "Получают много очков науки от уникальных месторождений.",
      "Используют небольшие армии с экспериментальным оружием.",
      "Могут исследовать гексы на большом расстоянии.",
      "Быстро развиваются, если получают уран, литий и редкоземельные металлы.",
      "Знают больше остальных о сверхразвитом острове-городе.",
    ],
    strength: "технологии, разведка и особые устройства.",
    weakness: "малое население и дорогое производство.",
    biomes: ["crater", "polar_desert"],
    icon: whiteSparkIcon,
    iconLarge: whiteSparkLargeIcon,
  },
  {
    id: "crimson_tide",
    name: "Алый Прилив",
    summary: "Объединение пиратских капитанов, беглых рабов, контрабандистов и военных вождей.",
    tagline: "Совет пиратских капитанов",
    lore: [
      "Занимает небольшие острова, бухты и скрытые базы возле утёсов.",
      "Грабит торговые маршруты и прибрежные поселения.",
      "Строит пиратские гавани, тайники, таверны и рынки контрабанды.",
      "Использует дешёвые корабли, налётчиков и диверсантов.",
      "Может захватывать вражеские корабли и воровать ресурсы.",
      "Иногда заключает временные союзы, но легко нарушает договоры.",
      "Единой власти нет: фракцией управляет совет капитанов.",
    ],
    strength: "мобильность, грабежи и внезапные атаки.",
    weakness: "плохая дисциплина, слабые города и внутренние конфликты.",
    biomes: ["cliffs"],
    icon: crimsonTideIcon,
    iconLarge: crimsonTideLargeIcon,
  },
  {
    id: "helios",
    name: "Гелиос",
    summary: "Гелиос представлен одним огромным технологическим островом-городом — Аркологией Нексус. Весь остров покрыт зданиями, энергетическими сетями, заводами и оборонительными системами. Город управляется древним искусственным интеллектом. Его жители считают остальные фракции примитивными и не вмешиваются в их войны, пока те не становятся угрозой.",
    tagline: "Остров-город под властью ИИ",
    lore: [
      "Не основывает новые поселения и не захватывает обычные острова.",
      "Начинает с открытыми технологиями высшего уровня.",
      "Имеет энергетические щиты, дронов, боевых роботов и авиацию.",
      "Получает ресурсы из автоматических производственных комплексов.",
      "Может уничтожить обычную армию прямым столкновением.",
      "Контролирует спутники и видит почти всю карту.",
      "Периодически требует дань или забирает редкие ископаемые.",
      "Атакует только нарушителей своей территории и слишком развитые державы.",
    ],
    strength: "абсолютно всё — технологии, оборона и армия.",
    weakness: "зависимость от единого энергетического ядра и центрального ИИ.",
    biomes: [],
    icon: heliosIcon,
    iconLarge: heliosLargeIcon,
  },
];

/**
 * Biomes no faction names in its lore. Each one goes to the faction whose
 * land it is closest to, so every held island gets an owner.
 */
const FALLBACK_BY_BIOME: Readonly<Partial<Record<TBiomeId, TFactionId>>> = {
  savanna: "children_of_roots",
  taiga: "children_of_roots",
  tundra: "white_spark",
  // The game has no ruins biome. The desert stands in for "руины".
  desert: "white_spark",
};

/** A faction from the biome list weighs this much in the roll. */
const BIOME_WEIGHT = 2;
/** A faction from the island-size rules weighs this much in the roll. */
const SIZE_WEIGHT = 1;
/** A cell with this many wild islands or more counts as large land. */
const LARGE_ISLAND_COUNT = 3;

const getFaction = (id: TFactionId) => {
  const faction = FACTIONS.find((candidate) => candidate.id === id);
  if (!faction) {
    throw new Error(`Unknown faction: ${id}`);
  }

  return faction;
};

/**
 * The owner of a held island, rolled with `roll` in [0, 1).
 * - A faction whose biome list holds the biome gets weight 2.
 * - Large land (3 wild islands) adds the Azure Concordat with weight 1.
 * - A lone small island adds the Crimson Tide with weight 1.
 * - A biome no faction names falls back to `FALLBACK_BY_BIOME`.
 * Helios never takes part: it holds only the boss's lair.
 */
const pickFaction = (biome: TBiomeId, islandCount: number, roll: number): TFactionId => {
  const weights = new Map<TFactionId, number>();
  const add = (id: TFactionId, weight: number) => weights.set(id, (weights.get(id) ?? 0) + weight);

  for (const faction of FACTIONS) {
    if (faction.biomes.includes(biome)) {
      add(faction.id, BIOME_WEIGHT);
    }
  }

  if (weights.size === 0) {
    add(FALLBACK_BY_BIOME[biome] ?? "children_of_roots", BIOME_WEIGHT);
  }

  if (islandCount >= LARGE_ISLAND_COUNT) {
    add("azure_concordat", SIZE_WEIGHT);
  }

  if (islandCount === 1) {
    add("crimson_tide", SIZE_WEIGHT);
  }

  const total = [...weights.values()].reduce((sum, weight) => sum + weight, 0);
  let left = roll * total;

  for (const [id, weight] of weights) {
    left -= weight;
    if (left < 0) {
      return id;
    }
  }

  return [...weights.keys()][weights.size - 1] ?? "children_of_roots";
};

export type { TFaction, TFactionId };
export { FACTIONS, getFaction, NEXUS_ARCOLOGY, pickFaction };
