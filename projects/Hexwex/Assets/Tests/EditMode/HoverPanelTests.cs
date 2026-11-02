using System;
using Hexwex.View;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>
    /// The hex panel floats over the hex it is about. Near the top of the screen it is
    /// pushed down and can cover that very hex. These tests play that case frame by
    /// frame: the panel must not blink.
    /// </summary>
    public sealed class HoverPanelTests
    {
        private const string Hex = "0,-2";

        /// <summary>
        /// Plays frames with the pointer resting on a hex that the panel covers once it
        /// shows. Returns how many times the panel appeared or disappeared after the first frame.
        /// </summary>
        private static int Flickers(Func<string, bool, bool, string, string> hoverAfter, int frames = 12)
        {
            string hovered = null;
            bool isShown = false;
            int flickers = 0;

            for (int frame = 0; frame < frames; frame += 1)
            {
                // The pointer is over the panel only while the panel is shown.
                bool overUi = isShown;
                bool overHexPanel = isShown;
                // The raycast reaches the hex only when no panel is in the way.
                string underPointer = overUi ? null : Hex;

                hovered = hoverAfter(hovered, overUi, overHexPanel, underPointer);
                bool nextShown = hovered != null;
                if (frame > 0 && nextShown != isShown)
                {
                    flickers += 1;
                }

                isShown = nextShown;
            }

            return flickers;
        }

        [Test]
        public void ThePanelOverItsOwnHexStaysSteady()
        {
            Assert.AreEqual(0, Flickers(GameRoot.HoverAfter));
        }

        [Test]
        public void TheOldRuleBlinkedEveryFrame()
        {
            // Before the fix any HUD under the pointer dropped the hover.
            string OldRule(string current, bool overUi, bool overHexPanel, string underPointer) => overUi ? null : underPointer;

            Assert.AreEqual(11, Flickers(OldRule));
        }

        [Test]
        public void TheHoverStaysOnThePanelAndMovesOnOffIt()
        {
            Assert.AreEqual(Hex, GameRoot.HoverAfter(Hex, true, true, null));
            Assert.AreEqual("1,0", GameRoot.HoverAfter(Hex, false, false, "1,0"));
        }

        [Test]
        public void OtherPartsOfTheHudStillDropTheHover()
        {
            Assert.IsNull(GameRoot.HoverAfter(Hex, true, false, null));
        }

        [Test]
        public void EmptySkyDropsTheHover()
        {
            Assert.IsNull(GameRoot.HoverAfter(Hex, false, false, null));
        }
    }
}
