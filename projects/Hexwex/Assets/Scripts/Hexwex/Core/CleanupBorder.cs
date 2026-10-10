using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    public sealed class BorderRock
    {
        public double X;
        public double Y;
        public double Radius;
        /// <summary>0 top, 1 right, 2 bottom, 3 left.</summary>
        public int Side;
        /// <summary>Shapes the outline of the rock in the view.</summary>
        public double Seed;
    }

    /// <summary>A place on the border where a window can open: the gap between two rocks.</summary>
    public sealed class BorderSlot
    {
        public int Side;
        /// <summary>Coordinate of the slot centre along its side: x on top and bottom, y on left and right.</summary>
        public double At;
        /// <summary>Half the free room between the two rocks.</summary>
        public double Room;
    }

    public sealed class BorderWindow
    {
        public int Id;
        public int Side;
        public double At;
        public double Half;
        public int OpenTick;
        public int CloseTick;

        public bool IsOpen(int tick)
        {
            return tick >= OpenTick && tick < CloseTick;
        }

        /// <summary>Seconds until the window closes.</summary>
        public double SecondsLeft(int tick, int tickHz)
        {
            return Math.Max(0, (double)(CloseTick - tick) / tickHz);
        }

        public bool IsClosing(int tick, int tickHz)
        {
            return SecondsLeft(tick, tickHz) <= CleanupBorder.WindowWarnSeconds;
        }
    }

    /// <summary>
    /// The edge of the cleanup map (<c>core/cleanup-border.ts</c>). A band of
    /// toxic plumes lines the map border, with rocks rising from under the clouds.
    /// The plumes push an island back and poison the player's island. The player
    /// leaves the battle only through a window: a gap in the plumes between two
    /// rocks. Windows open at random slots, stay open for a random time, warn
    /// before they close, and new ones open elsewhere.
    ///
    /// Sides are numbered 0 top, 1 right, 2 bottom, 3 left, in the prototype's
    /// plane with y down.
    /// </summary>
    public sealed class CleanupBorder
    {
        /// <summary>Depth of the plume band, inward from the map border.</summary>
        public const double BorderDepth = 300;
        /// <summary>The window warns this many seconds before it closes.</summary>
        public const double WindowWarnSeconds = 4;

        /// <summary>No window opens this close to a corner of the map.</summary>
        private const double CornerClear = BorderDepth + 220;
        private const double RockRadiusMin = 105;
        private const double RockRadiusSpread = 45;
        /// <summary>The narrowest window. A wide island gets a wider one.</summary>
        private const double WindowHalfMin = 330;
        /// <summary>Room on each side of the island inside a window.</summary>
        private const double WindowSideRoom = 120;
        private const double WindowOpenMin = 14;
        private const double WindowOpenSpread = 12;
        private const double WindowGapMin = 3;
        private const double WindowGapSpread = 7;
        private const int MinWindows = 1;
        private const int MaxWindows = 3;

        public readonly double Depth = BorderDepth;
        public readonly List<BorderRock> Rocks = new List<BorderRock>();
        public readonly List<BorderSlot> Slots = new List<BorderSlot>();
        public List<BorderWindow> Windows = new List<BorderWindow>();

        private readonly LevelBounds _bounds;
        private readonly Rng _rng;
        private int _nextSpawnTick;
        private int _nextWindowId = 1;

        /// <summary>
        /// Lays out the rocks and the window slots. The rocks stand at even steps
        /// along each side; the slots are the gaps between them. The step leaves
        /// room for a window that the player's island fits through.
        /// </summary>
        public CleanupBorder(LevelBounds bounds, uint seed, double islandRadius)
        {
            _bounds = bounds;
            _rng = new Rng(seed ^ 0x5bd1e995u);
            double wantedHalf = WindowHalfFor(islandRadius * 1.25);
            double minStep = (wantedHalf + RockRadiusMin + RockRadiusSpread + 40) * 2;

            for (int side = 0; side < 4; side += 1)
            {
                double half = side % 2 == 0 ? bounds.HalfWidth : bounds.HalfHeight;
                double usable = (half - CornerClear) * 2;
                int gaps = Math.Max(1, (int)Math.Floor(usable / minStep));
                double step = usable / gaps;
                double start = -half + CornerClear;

                for (int index = 0; index <= gaps; index += 1)
                {
                    // The draws keep the prototype's order: the jitter, the radius, the seed.
                    double jitter = index == 0 || index == gaps ? 0 : (_rng.Next() - 0.5) * 60;
                    double along = start + index * step + jitter;
                    double radius = RockRadiusMin + _rng.Next() * RockRadiusSpread;
                    SidePoint(bounds, side, along, BorderDepth * 0.45, out double x, out double y);
                    Rocks.Add(new BorderRock { X = x, Y = y, Radius = radius, Side = side, Seed = _rng.Next() });
                }

                for (int index = 0; index < gaps; index += 1)
                {
                    double a = start + index * step;
                    double b = a + step;
                    Slots.Add(new BorderSlot { Side = side, At = (a + b) / 2, Room = step / 2 - RockRadiusMin - RockRadiusSpread - 20 });
                }
            }

            // Lone rocks in the corners, where no window opens, so the band never looks bare.
            foreach (int sx in new[] { -1, 1 })
            {
                foreach (int sy in new[] { -1, 1 })
                {
                    double radius = RockRadiusMin + _rng.Next() * RockRadiusSpread;
                    Rocks.Add(new BorderRock
                    {
                        X = sx * (bounds.HalfWidth - BorderDepth * 0.55),
                        Y = sy * (bounds.HalfHeight - BorderDepth * 0.55),
                        Radius = radius,
                        Side = sy < 0 ? 0 : 2,
                        Seed = _rng.Next(),
                    });
                }
            }
        }

        /// <summary>A point on a side, <paramref name="along"/> the side and <paramref name="inward"/> from the border line.</summary>
        public static void SidePoint(LevelBounds bounds, int side, double along, double inward, out double x, out double y)
        {
            switch (side)
            {
                case 0:
                    x = along;
                    y = -bounds.HalfHeight + inward;
                    break;
                case 1:
                    x = bounds.HalfWidth - inward;
                    y = along;
                    break;
                case 2:
                    x = along;
                    y = bounds.HalfHeight - inward;
                    break;
                default:
                    x = -bounds.HalfWidth + inward;
                    y = along;
                    break;
            }
        }

        /// <summary>The unit vector into the map from a side.</summary>
        public static void InwardNormal(int side, out double x, out double y)
        {
            x = side == 1 ? -1 : side == 3 ? 1 : 0;
            y = side == 0 ? 1 : side == 2 ? -1 : 0;
        }

        /// <summary>How far a point is inside the map from a side. Negative is past the border.</summary>
        public static double DepthFrom(LevelBounds bounds, int side, double x, double y)
        {
            switch (side)
            {
                case 0: return y + bounds.HalfHeight;
                case 1: return bounds.HalfWidth - x;
                case 2: return bounds.HalfHeight - y;
                default: return x + bounds.HalfWidth;
            }
        }

        public static double AlongOf(int side, double x, double y)
        {
            return side % 2 == 0 ? x : y;
        }

        /// <summary>
        /// The window schedule. Closed windows go; at least one window is always
        /// open; more open at random gaps, up to three.
        /// </summary>
        public void Step(int tick, int tickHz, double islandRadius)
        {
            Windows.RemoveAll(window => tick >= window.CloseTick);

            while (Windows.Count < MinWindows && OpenWindow(tick, tickHz, islandRadius))
            {
                _nextSpawnTick = tick + JsMath.Round((WindowGapMin + _rng.Next() * WindowGapSpread) * tickHz);
            }

            if (tick >= _nextSpawnTick && Windows.Count < MaxWindows)
            {
                OpenWindow(tick, tickHz, islandRadius);
                _nextSpawnTick = tick + JsMath.Round((WindowGapMin + _rng.Next() * WindowGapSpread) * tickHz);
            }
        }

        /// <summary>The open window on <paramref name="side"/> whose span holds <paramref name="along"/>, or <c>null</c>.</summary>
        public BorderWindow WindowAt(int side, double along, int tick)
        {
            foreach (BorderWindow window in Windows)
            {
                if (window.Side == side && window.IsOpen(tick) && Math.Abs(along - window.At) <= window.Half)
                {
                    return window;
                }
            }

            return null;
        }

        /// <summary>
        /// The side whose plumes cover the point, or -1. A point in the band of an
        /// open window is clear of that side's plumes. <paramref name="margin"/>
        /// widens the band, so a hex counts as soon as its rim touches the plumes.
        /// </summary>
        public int PlumeSideAt(double x, double y, int tick, double margin)
        {
            for (int side = 0; side < 4; side += 1)
            {
                double depth = DepthFrom(_bounds, side, x, y);
                if (depth >= Depth + margin)
                {
                    continue;
                }

                if (WindowAt(side, AlongOf(side, x, y), tick) == null)
                {
                    return side;
                }
            }

            return -1;
        }

        /// <summary>The half width of a window that the player's island fits through.</summary>
        private static double WindowHalfFor(double islandRadius)
        {
            return Math.Max(WindowHalfMin, islandRadius + WindowSideRoom);
        }

        /// <summary>Opens a window at a free slot. Returns false when every slot is taken.</summary>
        private bool OpenWindow(int tick, int tickHz, double islandRadius)
        {
            List<BorderSlot> free = Slots.FindAll(slot => !Windows.Exists(window => window.Side == slot.Side && window.At == slot.At));
            if (free.Count == 0)
            {
                return false;
            }

            BorderSlot picked = free[_rng.NextIndex(free.Count)];
            double seconds = WindowOpenMin + _rng.Next() * WindowOpenSpread;
            Windows.Add(new BorderWindow
            {
                Id = _nextWindowId,
                Side = picked.Side,
                At = picked.At,
                Half = Math.Min(picked.Room, WindowHalfFor(islandRadius)),
                OpenTick = tick,
                CloseTick = tick + JsMath.Round(seconds * tickHz),
            });
            _nextWindowId += 1;

            return true;
        }
    }
}
