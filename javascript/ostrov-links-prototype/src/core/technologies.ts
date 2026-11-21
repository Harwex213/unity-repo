import agricultureArt from "../assets/technologies/agriculture.jpg";
import astronomyArt from "../assets/technologies/astronomy.jpg";
import ballisticsArt from "../assets/technologies/ballistics.jpg";
import educationArt from "../assets/technologies/education.jpg";
import electrificationArt from "../assets/technologies/electrification.jpg";
import fortificationArt from "../assets/technologies/fortification.jpg";
import irrigationArt from "../assets/technologies/irrigation.jpg";
import loggingArt from "../assets/technologies/logging.jpg";
import manaConversionArt from "../assets/technologies/mana-conversion.jpg";
import mechanicsArt from "../assets/technologies/mechanics.jpg";
import miningArt from "../assets/technologies/mining.jpg";
import quarantineArt from "../assets/technologies/quarantine.jpg";
import settlementArt from "../assets/technologies/settlement.jpg";
import smithingArt from "../assets/technologies/smithing.jpg";
import survivalArt from "../assets/technologies/survival.jpg";
import { getBuilding } from "./buildings";
import type { TBuildingId, TTechnology, TTechnologyId } from "./types";

type TTechnologyDraft = Omit<TTechnology, "icon">;

/** The icon of a technology is the icon of the building it opens, until it gets its own. */
const withIcon = (draft: TTechnologyDraft): TTechnology => ({ ...draft, icon: getBuilding(draft.unlocks).art });

/** One technology per building, sorted by the science they cost. The costs are a draft. */
const TECHNOLOGIES: readonly TTechnology[] = (
  [
    {
      id: "survival",
      label: "Выживание",
      art: survivalArt,
      summary: "Первое укрытие для выживших.",
      description:
        "Выжившие собирают укрытие из досок и обломков. Форпост даёт немного ресурса того биома, на котором стоит. С этой технологии начинается игра.",
      science: 0,
      unlocks: "survivors_outpost",
    },
    {
      id: "agriculture",
      label: "Земледелие",
      art: agricultureArt,
      summary: "Поля и урожай.",
      description: "Поселенцы распахивают землю и сеют зерно. Ферма производит еду. Еда даёт население.",
      science: 10,
      unlocks: "farm",
    },
    {
      id: "settlement",
      label: "Оседлость",
      art: settlementArt,
      summary: "Постоянные дома для людей.",
      description:
        "Люди перестают кочевать и строят дома. Деревня производит население. Население работает в зданиях и уходит в армию.",
      science: 10,
      unlocks: "village",
    },
    {
      id: "logging",
      label: "Лесозаготовка",
      art: loggingArt,
      summary: "Рубка леса.",
      description: "Лесорубы валят деревья и пилят брёвна. Лесопилка производит дерево. Дерево идёт на строительство.",
      science: 15,
      unlocks: "sawmill",
    },
    {
      id: "mining",
      label: "Горное дело",
      art: miningArt,
      summary: "Добыча камня.",
      description: "Шахтёры добывают камень в горах и холмах. Рудник производит камень. Камень идёт на строительство.",
      science: 15,
      unlocks: "mine",
    },
    {
      id: "irrigation",
      label: "Ирригация",
      art: irrigationArt,
      summary: "Вода для полей.",
      description: "Каналы несут воду от башни к полям. Водонапорная башня усиливает производство еды у зданий рядом.",
      science: 25,
      unlocks: "water_tower",
    },
    {
      id: "mechanics",
      label: "Механика",
      art: mechanicsArt,
      summary: "Шестерни и механизмы.",
      description:
        "Инженеры собирают механизмы из шестерён и пружин. Гильдия масонов производит механизмы. Механизмы идут на строительство.",
      science: 30,
      unlocks: "masons_guild",
    },
    {
      id: "smithing",
      label: "Кузнечное дело",
      art: smithingArt,
      summary: "Инструменты для мастеров.",
      description:
        "Кузнецы куют инструменты для лесорубов, шахтёров и механиков. Кузница усиливает производство дерева, камня и механизмов у зданий рядом.",
      science: 35,
      unlocks: "forge",
    },
    {
      id: "education",
      label: "Образование",
      art: educationArt,
      summary: "Школы и учёные.",
      description: "Учёные передают знания ученикам. Университет производит науку. Наука открывает технологии.",
      science: 40,
      unlocks: "university",
    },
    {
      id: "fortification",
      label: "Фортификация",
      art: fortificationArt,
      summary: "Препятствия для врага.",
      description:
        "Солдаты ставят ежи, натягивают колючую проволоку и закапывают мины. Заграждения мешают врагам двигаться.",
      science: 40,
      unlocks: "barricades",
    },
    {
      id: "electrification",
      label: "Электрификация",
      art: electrificationArt,
      summary: "Ток в проводах.",
      description:
        "Турбины вырабатывают ток, провода несут его к зданиям. Электростанция производит электричество. Электричество даёт разведку, работу зданиям и активные скиллы.",
      science: 50,
      unlocks: "electric_station",
    },
    {
      id: "ballistics",
      label: "Баллистика",
      art: ballisticsArt,
      summary: "Стрельба на дальность.",
      description: "Артиллеристы учатся стрелять далеко и точно. Артиллерийская башня бьёт врагов на расстоянии.",
      science: 55,
      unlocks: "artillery_tower",
    },
    {
      id: "quarantine",
      label: "Карантин",
      art: quarantineArt,
      summary: "Отдельное жильё для сумасшедших.",
      description:
        "Сумасшедших отделяют от остальных за забором. В гетто киберпсихов живут только сумасшедшие. Там они не строят «Гедонистический рай» и дают меньше токсичности.",
      science: 60,
      unlocks: "cyberpsycho_ghetto",
    },
    {
      id: "astronomy",
      label: "Астрономия",
      art: astronomyArt,
      summary: "Наблюдение за звёздами.",
      description:
        "Телескоп показывает звёзды и потоки маны. Обсерватория усиливает производство науки и маны у зданий рядом.",
      science: 70,
      unlocks: "observatory",
    },
    {
      id: "mana_conversion",
      label: "Преобразование маны",
      art: manaConversionArt,
      summary: "Мана из недр.",
      description:
        "Машина вытягивает ману из кристаллов в земле. Центральный конвертер производит ману. Мана даёт активные скиллы.",
      science: 80,
      unlocks: "converter",
    },
  ] satisfies readonly TTechnologyDraft[]
).map(withIcon);

const TECHNOLOGY_BY_ID = new Map(TECHNOLOGIES.map((technology) => [technology.id, technology]));

const getTechnology = (id: TTechnologyId) => {
  const technology = TECHNOLOGY_BY_ID.get(id);
  if (!technology) {
    throw new Error(`Unknown technology: ${id}`);
  }

  return technology;
};

/** The technology that opens the building. Every building has exactly one. */
const technologyFor = (buildingId: TBuildingId) => {
  return TECHNOLOGIES.find((technology) => technology.unlocks === buildingId);
};

const isTechnologyId = (value: unknown): value is TTechnologyId => {
  return typeof value === "string" && TECHNOLOGY_BY_ID.has(value as TTechnologyId);
};

export { getTechnology, isTechnologyId, technologyFor, TECHNOLOGIES };
