import battleArt from "../assets/tutorial/battle.webp";
import buildArt from "../assets/tutorial/build.webp";
import introArt from "../assets/tutorial/intro.webp";
import scoutArt from "../assets/tutorial/scout.webp";
import taxArt from "../assets/tutorial/tax.webp";

/**
 * The learn guide: five cards, one per part of the core loop. Each card shows
 * once, when the game first reaches its moment. The guide is off unless the
 * address has the `?guide` flag.
 */

type TGuideStepId = "intro" | "build" | "tax" | "scout" | "battle";

type TGuideStep = {
  readonly id: TGuideStepId;
  readonly title: string;
  readonly text: string;
  readonly art: string;
};

const GUIDE_STEPS: readonly TGuideStep[] = [
  {
    id: "intro",
    title: "Введение и цель игры",
    text: "Госпожа судьба привела Вас к порогу дверей острова, чьи жители несут бремя великого несчастья. Сотни лет эволюции человечества привели к тому, что мир оказался расколот на части, а неутолимое желание человека довольствоваться удобством и материальным благом вычистило из его памяти знания о том, как достичь этого не разрушительным путём. Ныне всё, что доступно человеку, — это разрушать окружающую среду. Ваша задача — спасти свою общину, привести её к процветанию и даровать шанс на избавление от греха путём достижения технологии переработки токсичных отходов в чистый воздух. Подобная технология сродни магии.",
    art: introArt,
  },
  {
    id: "build",
    title: "Фаза строительства",
    text: "В фазу строительства вам дарована возможность развивать свой остров, превращая его в хищный остров или колыбель. Вашей главной задачей как управляющего будет не допускать загрязнения малых земельных ресурсов токсичными отходами от зданий. Путь избавления от них варварский: вам придётся жертвовать землёй, безвозмездно её уничтожая.",
    art: buildArt,
  },
  {
    id: "tax",
    title: "Фаза сбора ресурсов",
    text: "В фазу сбора ресурсов извозчики обходят здания и собирают с них налоги. Делают они это максимально неэффективно, а потому здесь присутствует элемент случайности. Вы как управляющий можете воспользоваться своим влиянием, чтобы добиться нужного результата, но так вы истратите ценный ресурс.",
    art: taxArt,
  },
  {
    id: "scout",
    title: "Фаза разведки",
    text: "Фаза разведки — важная часть жизни управляющего: вам необходимо выбирать стратегию дальнейшего движения острова. Полагаясь на удачу или на данные разведки, вам предстоит обнаружить древний храм канувшей цивилизации, дабы отыскать технологию и прекратить мучения вашего народа.",
    art: scoutArt,
  },
  {
    id: "battle",
    title: "Фаза боя",
    text: "В фазу боя вы управляете своим островом с помощью WASD и принимаете решения о стратегии. Благодаря уникальной технологии, сохранившейся в библиотеке вашей твердыни, вам дарована возможность присоединять пустой остров к своему. Так вы получите необходимую землю, которую можно потратить на избавление от токсичных отходов.",
    art: battleArt,
  },
];

const guideStepIndex = (id: TGuideStepId) => GUIDE_STEPS.findIndex((step) => step.id === id);

const getGuideStep = (id: TGuideStepId) => {
  const step = GUIDE_STEPS.find((candidate) => candidate.id === id);
  if (!step) {
    throw new Error(`Unknown guide step: ${id}`);
  }

  return step;
};

const GUIDE_FLAGS = ["guide", "tutorial"];

/**
 * Reads the flag from the query part of the address, before the hash:
 * `?guide`, `?guide=1`, `?tutorial` and `?tutorial=1` turn the guide on, and
 * `guide=0` (or `false`, `off`) keeps it off.
 */
const isGuideRequested = (search: string) => {
  const params = new URLSearchParams(search);

  return GUIDE_FLAGS.some((flag) => {
    const value = params.get(flag);
    if (value === null) {
      return false;
    }

    return !["0", "false", "off", "no"].includes(value.trim().toLowerCase());
  });
};

export type { TGuideStep, TGuideStepId };
export { getGuideStep, GUIDE_STEPS, guideStepIndex, isGuideRequested };
