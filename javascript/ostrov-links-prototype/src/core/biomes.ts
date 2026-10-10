import badlandsArt from "../assets/biomes/badlands.jpg";
import cliffsArt from "../assets/biomes/cliffs.jpg";
import craterArt from "../assets/biomes/crater.jpg";
import desertArt from "../assets/biomes/desert.jpg";
import forrestArt from "../assets/biomes/forrest.jpg";
import grasslandArt from "../assets/biomes/grassland.jpg";
import hillsArt from "../assets/biomes/hills.jpg";
import mountainsArt from "../assets/biomes/mountains.jpg";
import plainsArt from "../assets/biomes/plains.jpg";
import polarDesertArt from "../assets/biomes/polar_desert.jpg";
import rainforestArt from "../assets/biomes/rainforest.jpg";
import savannaArt from "../assets/biomes/savanna.jpg";
import swampArt from "../assets/biomes/swamp.jpg";
import taigaArt from "../assets/biomes/taiga.jpg";
import tundraArt from "../assets/biomes/tundra.jpg";
import volcanoArt from "../assets/biomes/volcano.jpg";
import type { TBiome, TBiomeId } from "./types";

/** The 16 biomes, copied from `javascript/ostrov-prototype/src/core/biomes.ts`. */
const BIOMES: readonly TBiome[] = [
  {
    id: "grassland",
    label: "Умеренные луга",
    description: "Жирная трава по пояс и ровная почва. Здесь растёт всё, и людям тут хорошо живётся.",
    color: "#6fae4f",
    art: grasslandArt,
  },
  {
    id: "plains",
    label: "Равнина",
    description: "Сухая ровная степь. Родит скупо, зато на ней легко строить что угодно.",
    color: "#a8bd5e",
    art: plainsArt,
  },
  {
    id: "forrest",
    label: "Лес",
    description: "Прямые стволы в два обхвата. Главный источник дерева на острове.",
    color: "#3f7d43",
    art: forrestArt,
  },
  {
    id: "savanna",
    label: "Саванна",
    description: "Редкие зонтичные деревья на выжженной траве. Дерева мало, но оно рядом.",
    color: "#c2a24c",
    art: savannaArt,
  },
  {
    id: "rainforest",
    label: "Джунгли",
    description: "Душная зелёная стена. Дерева больше, чем где бы то ни было, и гниёт оно так же быстро.",
    color: "#2f7d5c",
    art: rainforestArt,
  },
  {
    id: "taiga",
    label: "Хвойный лес",
    description: "Смолистые ели на вечной мерзлоте. Ровный, надёжный лесоповал.",
    color: "#356b55",
    art: taigaArt,
  },
  {
    id: "tundra",
    label: "Тундра",
    description: "Мох, лишайник и ветер. Людям тут тяжело, но жить можно.",
    color: "#8fa79b",
    art: tundraArt,
  },
  {
    id: "desert",
    label: "Пустыня",
    description: "Песок и камень до горизонта. Еды нет, зато никто не мешает.",
    color: "#dcc179",
    art: desertArt,
  },
  {
    id: "polar_desert",
    label: "Ледяная пустыня",
    description: "Голый лёд. Тут выживают только упрямые.",
    color: "#c9dbe4",
    art: polarDesertArt,
  },
  {
    id: "swamp",
    label: "Болото",
    description: "Тёплая жижа, мошка и пузыри газа. Отдаёт много и травит сильнее всего.",
    color: "#5c6b3a",
    art: swampArt,
  },
  {
    id: "badlands",
    label: "Бесплодные земли",
    description: "Растрескавшаяся отравленная глина. Селиться тут — плохая идея.",
    color: "#a2603f",
    art: badlandsArt,
  },
  {
    id: "crater",
    label: "Кратер",
    description: "Воронка от чего-то очень старого. По краям обнажилась порода.",
    color: "#7a6f66",
    art: craterArt,
  },
  {
    id: "volcano",
    label: "Вулкан",
    description: "Дышит серой и даёт больше камня, чем любая гора. И травит соразмерно.",
    color: "#6b3733",
    art: volcanoArt,
  },
  {
    id: "hills",
    label: "Холмы",
    description: "Пологие склоны с хорошей землёй в распадках.",
    color: "#8a9a4e",
    art: hillsArt,
  },
  {
    id: "mountains",
    label: "Горы",
    description: "Серый камень и снег на макушках. Лучшая порода на острове.",
    color: "#8d8f96",
    art: mountainsArt,
  },
  {
    id: "cliffs",
    label: "Утёсы",
    description: "Обрыв по краю острова. Камень рядом, места мало.",
    color: "#7e8894",
    art: cliffsArt,
  },
];

const BIOME_BY_ID = new Map(BIOMES.map((biome) => [biome.id, biome]));

const getBiome = (id: TBiomeId) => {
  const biome = BIOME_BY_ID.get(id);
  if (!biome) {
    throw new Error(`Unknown biome: ${id}`);
  }

  return biome;
};

const isBiomeId = (value: unknown): value is TBiomeId => {
  return typeof value === "string" && BIOME_BY_ID.has(value as TBiomeId);
};

export { BIOMES, getBiome, isBiomeId };
