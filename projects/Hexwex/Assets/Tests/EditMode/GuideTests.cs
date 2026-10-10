using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>The learn guide: each card opens once, in its own moment.</summary>
    public sealed class GuideTests
    {
        [Test]
        public void ADisabledGuideNeverOpens()
        {
            Guide guide = new Guide();

            guide.Reach(GuideStepId.Intro);

            Assert.IsNull(guide.OpenStep);
            Assert.IsFalse(guide.HasSeen(GuideStepId.Intro));
        }

        [Test]
        public void ACardOpensOnceWhenItsMomentComes()
        {
            Guide guide = new Guide();
            guide.Enable();

            guide.Reach(null);
            Assert.IsNull(guide.OpenStep);

            guide.Reach(GuideStepId.Intro);
            Assert.AreEqual(GuideStepId.Intro, guide.OpenStep);
            Assert.IsFalse(guide.CanGoBack);

            // The build phase is not on screen yet: "Далее" closes the guide.
            guide.Next(GuideStepId.Intro);
            Assert.IsNull(guide.OpenStep);

            // A seen card does not open by itself again.
            guide.Reach(GuideStepId.Intro);
            Assert.IsNull(guide.OpenStep);

            guide.Reach(GuideStepId.Build);
            Assert.AreEqual(GuideStepId.Build, guide.OpenStep);
            Assert.IsTrue(guide.CanGoBack);
        }

        [Test]
        public void NextAndBackWalkTheSeenCards()
        {
            Guide guide = new Guide();
            guide.Enable();
            guide.Reach(GuideStepId.Intro);

            // The next card's phase is on screen already: it opens at once.
            guide.Next(GuideStepId.Build);
            Assert.AreEqual(GuideStepId.Build, guide.OpenStep);

            guide.Previous();
            Assert.AreEqual(GuideStepId.Intro, guide.OpenStep);
            guide.Previous();
            Assert.AreEqual(GuideStepId.Intro, guide.OpenStep);

            // Build has been seen, so it opens whatever is on screen.
            guide.Next(null);
            Assert.AreEqual(GuideStepId.Build, guide.OpenStep);
            guide.Next(null);
            Assert.IsNull(guide.OpenStep);
        }

        [Test]
        public void APhaseReachedOutOfOrderShowsItsOwnCard()
        {
            Guide guide = new Guide();
            guide.Enable();
            guide.Reach(GuideStepId.Intro);
            guide.Next(null);

            guide.Reach(GuideStepId.Scout);
            Assert.AreEqual(GuideStepId.Scout, guide.OpenStep);

            // Back skips the cards that were never shown.
            guide.Previous();
            Assert.AreEqual(GuideStepId.Intro, guide.OpenStep);
        }

        [Test]
        public void SkipTurnsTheGuideOffAndEnableStartsItOver()
        {
            Guide guide = new Guide();
            guide.Enable();
            guide.Reach(GuideStepId.Intro);

            guide.Skip();
            Assert.IsNull(guide.OpenStep);
            Assert.IsFalse(guide.Enabled);
            guide.Reach(GuideStepId.Build);
            Assert.IsNull(guide.OpenStep);

            guide.Enable();
            guide.Reach(GuideStepId.Intro);
            Assert.AreEqual(GuideStepId.Intro, guide.OpenStep);
        }

        [Test]
        public void TheLastCardClosesTheGuide()
        {
            Guide guide = new Guide();
            guide.Enable();
            guide.Reach(GuideStepId.Battle);

            guide.Next(GuideStepId.Battle);

            Assert.IsNull(guide.OpenStep);
            Assert.IsTrue(guide.Enabled);
            Assert.AreEqual(5, Guide.Steps.Count);
        }
    }
}
