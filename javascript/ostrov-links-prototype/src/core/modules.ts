import assemblyShopIcon from "../assets/icons/assembly-shop.png";
import dynamoIcon from "../assets/icons/dynamo.png";
import greenhouseIcon from "../assets/icons/greenhouse.png";
import hedonisticParadiseIcon from "../assets/icons/hedonistic-paradise.png";
import madIcon from "../assets/icons/mad.png";
import powerIcon from "../assets/icons/power.png";
import resonatorIcon from "../assets/icons/resonator.png";
import sawFrameIcon from "../assets/icons/saw-frame.png";
import stoneCrusherIcon from "../assets/icons/stone-crusher.png";
import tripwireIcon from "../assets/icons/tripwire.png";
import turretIcon from "../assets/icons/turret.png";
import { getResource } from "./resources";
import type { TModule, TModuleId, TYieldResourceId } from "./types";

/** A module that adds one resource to the production of its building. */
const productionModule = (id: TModuleId, label: string, art: string, resource: TYieldResourceId): TModule => {
  const { label: resourceLabel, icon } = getResource(resource);

  return {
    id,
    label,
    art,
    summary: `Дополнительное производство: ${resourceLabel.toLowerCase()}.`,
    sections: [
      {
        title: "Эффект",
        icon,
        rules: [`Здание с модулем дополнительно производит ресурс «${resourceLabel}».`],
      },
    ],
  };
};

/** The order follows the design map: production, synergy, electricity, defense, then the mad. */
const MODULES: readonly TModule[] = [
  productionModule("greenhouse", "Теплица", greenhouseIcon, "food"),
  productionModule("assembly_shop", "Сборочный цех", assemblyShopIcon, "mechanisms"),
  productionModule("saw_frame", "Пилорама", sawFrameIcon, "wood"),
  productionModule("stone_crusher", "Камнедробилка", stoneCrusherIcon, "stone"),
  {
    id: "resonator",
    label: "Резонатор",
    art: resonatorIcon,
    summary: "Синергия с соседями.",
    sections: [
      {
        title: "Эффект",
        rules: ["Модуль создаёт синергию у зданий рядом, если у них тоже стоит резонатор."],
      },
    ],
  },
  productionModule("dynamo", "Динамо-машина", dynamoIcon, "electricity"),
  {
    id: "turret",
    label: "Турель",
    art: turretIcon,
    summary: "Дальнобойная защита.",
    sections: [
      {
        title: "Эффект",
        rules: ["Модуль даёт зданию дальнобойную защиту."],
      },
    ],
  },
  {
    id: "tripwire",
    label: "Растяжка",
    art: tripwireIcon,
    summary: "Разовая атака по врагу.",
    sections: [
      {
        title: "Эффект",
        rules: ["Активная защита.", "Когда враг входит в гекс, растяжка один раз атакует его, как мина."],
      },
    ],
  },
  {
    id: "hedonistic_paradise",
    label: "Гедонистический рай",
    art: hedonisticParadiseIcon,
    summary: "Модуль, который сумасшедшие строят сами.",
    sections: [
      {
        title: "Как появляется",
        rules: [
          "Сумасшедшие сами строят модуль в здании, где живут. Игрок не выбирает, где и когда модуль появится.",
          "Модуль появляется в фазе строительства.",
          "Модулю нужен свободный слот в здании. Без свободного слота модуль не появляется.",
          "Шанс появления = сумасшедшие в здании / всё население × коэффициент появления.",
          "Когда модуль появляется, игрок получает уведомление.",
          "В «Гетто киберпсихов» модуль не появляется.",
        ],
      },
      {
        title: "Эффект",
        rules: ["Модуль всегда даёт гексу токсичность."],
      },
      {
        title: "Как убрать",
        icon: powerIcon,
        rules: ["Игрок может разогнать модуль за N власти."],
      },
      {
        title: "Сумасшедшие",
        icon: madIcon,
        rules: [
          "Сумасшедший — человек, который не справился с невзгодами жизни. Он потерял контроль над своей волей и ушёл в бесконечный гедонизм.",
          "Сумасшедшие появляются из-за негативного ивента: прокрута слота фортуны.",
          "Сумасшедшего нельзя вылечить до конца игры.",
          "Сумасшедшего можно назначить в производственный посёлок или призвать в армию, как обычного человека.",
          "Сумасшедший добавляет зданию, где живёт, фиксированные N токсичности. Из-за этого растёт токсичность гекса.",
          "Призванный сумасшедший не выполняет приказ атаковать. Врагов он атакует, только когда защищает себя.",
          "Сумасшедших можно поселить в «Гетто киберпсихов». Там они не строят «Гедонистический рай» и дают меньше фиксированной токсичности.",
        ],
      },
    ],
  },
];

const MODULE_BY_ID = new Map(MODULES.map((module) => [module.id, module]));

const getModule = (id: TModuleId) => {
  const module = MODULE_BY_ID.get(id);
  if (!module) {
    throw new Error(`Unknown module: ${id}`);
  }

  return module;
};

const isModuleId = (value: unknown): value is TModuleId => {
  return typeof value === "string" && MODULE_BY_ID.has(value as TModuleId);
};

export { getModule, isModuleId, MODULES };
