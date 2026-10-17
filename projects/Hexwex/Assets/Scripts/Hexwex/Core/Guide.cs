using System.Collections.Generic;

namespace Hexwex.Core
{
    public enum GuideStepId
    {
        Intro,
        Build,
        Tax,
        Scout,
        Battle,
    }

    public sealed class GuideStep
    {
        public readonly GuideStepId Id;
        public readonly string Title;
        public readonly string Text;
        /// <summary>The picture in <c>Resources/Tutorial</c>.</summary>
        public readonly string Art;

        public GuideStep(GuideStepId id, string title, string text, string art)
        {
            Id = id;
            Title = title;
            Text = text;
            Art = art;
        }
    }

    /// <summary>
    /// The learn guide (<c>core/guide.ts</c>, <c>domain/guide-actions.ts</c>): five
    /// cards, one per part of the core loop. Each card opens by itself once, when
    /// the game first reaches its moment. The guide lives for the session: a new
    /// game does not show the cards again unless the guide is turned on anew.
    ///
    /// The view tells the guide which card belongs to what is on screen now; the
    /// guide decides which card is open.
    /// </summary>
    public sealed class Guide
    {
        /// <summary>The order is the order of <see cref="GuideStepId"/>.</summary>
        public static readonly IReadOnlyList<GuideStep> Steps = new[]
        {
            new GuideStep(
                GuideStepId.Intro,
                "Введение и цель игры",
                "Госпожа судьба привела Вас к порогу дверей острова, чьи жители несут бремя великого несчастья. Сотни лет эволюции человечества привели к тому, что мир оказался расколот на части, а неутолимое желание человека довольствоваться удобством и материальным благом вычистило из его памяти знания о том, как достичь этого не разрушительным путём. Ныне всё, что доступно человеку, — это разрушать окружающую среду. Ваша задача — спасти свою общину, привести её к процветанию и даровать шанс на избавление от греха путём достижения технологии переработки токсичных отходов в чистый воздух. Подобная технология сродни магии.",
                "intro"),
            new GuideStep(
                GuideStepId.Build,
                "Фаза строительства",
                "В фазу строительства вам дарована возможность развивать свой остров, превращая его в хищный остров или колыбель. Вашей главной задачей как управляющего будет не допускать загрязнения малых земельных ресурсов токсичными отходами от зданий. Путь избавления от них варварский: вам придётся жертвовать землёй, безвозмездно её уничтожая.",
                "build"),
            new GuideStep(
                GuideStepId.Tax,
                "Фаза сбора ресурсов",
                "В фазу сбора ресурсов извозчики обходят здания и собирают с них налоги. Делают они это максимально неэффективно, а потому здесь присутствует элемент случайности. Вы как управляющий можете воспользоваться своим влиянием, чтобы добиться нужного результата, но так вы истратите ценный ресурс.",
                "tax"),
            new GuideStep(
                GuideStepId.Scout,
                "Фаза разведки",
                "Фаза разведки — важная часть жизни управляющего: вам необходимо выбирать стратегию дальнейшего движения острова. Полагаясь на удачу или на данные разведки, вам предстоит обнаружить древний храм канувшей цивилизации, дабы отыскать технологию и прекратить мучения вашего народа.",
                "scout"),
            new GuideStep(
                GuideStepId.Battle,
                "Фаза боя",
                "В фазу боя вы управляете своим островом с помощью WASD и принимаете решения о стратегии. Благодаря уникальной технологии, сохранившейся в библиотеке вашей твердыни, вам дарована возможность присоединять пустой остров к своему. Так вы получите необходимую землю, которую можно потратить на избавление от токсичных отходов.",
                "battle"),
        };

        private readonly HashSet<GuideStepId> _seen = new HashSet<GuideStepId>();

        /// <summary>Off means the guide never shows.</summary>
        public bool Enabled { get; private set; }
        /// <summary>The card on screen, or <c>null</c> when the guide is closed.</summary>
        public GuideStepId? OpenStep { get; private set; }

        public static GuideStep Get(GuideStepId id)
        {
            return Steps[(int)id];
        }

        public bool HasSeen(GuideStepId id)
        {
            return _seen.Contains(id);
        }

        /// <summary>True when an earlier card has been seen, so "Назад" has somewhere to go.</summary>
        public bool CanGoBack
        {
            get { return OpenStep.HasValue && PreviousSeen(OpenStep.Value).HasValue; }
        }

        /// <summary>Turns the guide on for a new game: every card will open again.</summary>
        public void Enable()
        {
            Enabled = true;
            OpenStep = null;
            _seen.Clear();
        }

        /// <summary>
        /// Opens the card of what is on screen, once. A phase reached out of order
        /// still shows its own card: the skipped cards are not forced on the player.
        /// </summary>
        public void Reach(GuideStepId? context)
        {
            if (!Enabled || !context.HasValue || OpenStep.HasValue || _seen.Contains(context.Value))
            {
                return;
            }

            Open(context.Value);
        }

        /// <summary>
        /// Goes to the next card if the player has seen it already or its phase is
        /// on screen now. Otherwise the guide closes, and the next card opens by
        /// itself when its phase arrives.
        /// </summary>
        public void Next(GuideStepId? context)
        {
            if (!OpenStep.HasValue)
            {
                return;
            }

            int next = (int)OpenStep.Value + 1;
            if (next < Steps.Count && (_seen.Contains((GuideStepId)next) || context == (GuideStepId)next))
            {
                Open((GuideStepId)next);

                return;
            }

            OpenStep = null;
        }

        /// <summary>Opens the closest earlier card the player has already seen.</summary>
        public void Previous()
        {
            if (!OpenStep.HasValue)
            {
                return;
            }

            GuideStepId? previous = PreviousSeen(OpenStep.Value);
            if (previous.HasValue)
            {
                Open(previous.Value);
            }
        }

        /// <summary>"Пропустить" turns the guide off for the rest of the session.</summary>
        public void Skip()
        {
            OpenStep = null;
            Enabled = false;
        }

        private void Open(GuideStepId id)
        {
            _seen.Add(id);
            OpenStep = id;
        }

        private GuideStepId? PreviousSeen(GuideStepId current)
        {
            for (int index = (int)current - 1; index >= 0; index -= 1)
            {
                if (_seen.Contains((GuideStepId)index))
                {
                    return (GuideStepId)index;
                }
            }

            return null;
        }
    }
}
