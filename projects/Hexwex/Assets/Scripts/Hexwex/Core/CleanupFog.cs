using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    /// <summary>
    /// The fog round the cleanup map. The prototype walls the map in
    /// (<c>core/cleanup-border.ts</c>): a square band of toxic plumes that pushes
    /// an island back and poisons it, with rocks in it and windows that open for
    /// a while to let the player out. The port drops the wall. The fight stands
    /// in a clearing that follows the islands, and fog lies all round it.
    ///
    /// The fog hides. Whoever stands in it is seen only from close by, and sees
    /// only that far itself, so an island that sails in drops out of every fight
    /// it is not locked in hand to hand. Enemy islands do not follow it in. An
    /// island that sails on through the whole depth of the fog has left the
    /// battle.
    /// </summary>
    public sealed class CleanupFog
    {
        /// <summary>From the edge of the clearing to where an island is gone from the battle.</summary>
        public const double Depth = 900;
        /// <summary>How far anyone sees into the fog, out of it, or inside it: about two hex steps.</summary>
        public const double Sight = 170;
        /// <summary>Open sky between an island and the fog at the start.</summary>
        public const double Margin = 380;

        /// <summary>The edge is kept as its reach in this many directions round the centre.</summary>
        private const int Directions = 256;
        /// <summary>Corners of the edge are rounded over this many directions each way.</summary>
        private const int Rounding = 12;
        /// <summary>The edge swells outward in a few slow waves, never inward: the clearing only gains room.</summary>
        private static readonly int[] WaveCounts = { 2, 3, 5 };
        private static readonly double[] WaveSizes = { 0.07, 0.06, 0.04 };

        public readonly double CenterX;
        public readonly double CenterY;

        private readonly double[] _reach = new double[Directions];

        /// <summary>
        /// Lays the clearing round the islands as they stand at the start. Its
        /// centre is the middle of them. In every direction it reaches as far as
        /// the farthest island does that way, and <see cref="Margin"/> more, so it
        /// is wide where the islands are and close where the sky is empty.
        /// </summary>
        /// <param name="discs">Each island as its centre and its radius: x, y, radius.</param>
        public CleanupFog(IReadOnlyList<double[]> discs, uint seed)
        {
            double minX = 0;
            double maxX = 0;
            double minY = 0;
            double maxY = 0;
            for (int index = 0; index < discs.Count; index += 1)
            {
                double[] disc = discs[index];
                minX = index == 0 ? disc[0] - disc[2] : Math.Min(minX, disc[0] - disc[2]);
                maxX = index == 0 ? disc[0] + disc[2] : Math.Max(maxX, disc[0] + disc[2]);
                minY = index == 0 ? disc[1] - disc[2] : Math.Min(minY, disc[1] - disc[2]);
                maxY = index == 0 ? disc[1] + disc[2] : Math.Max(maxY, disc[1] + disc[2]);
            }

            CenterX = (minX + maxX) / 2;
            CenterY = (minY + maxY) / 2;

            Rng rng = new Rng(seed ^ 0x5bd1e995u);
            double[] phases = new double[WaveCounts.Length];
            for (int index = 0; index < phases.Length; index += 1)
            {
                phases[index] = rng.Next() * Math.PI * 2;
            }

            // How far each island reaches in a direction: its centre along that
            // direction, and its radius. The clearing takes the farthest.
            double[] sharp = new double[Directions];
            for (int direction = 0; direction < Directions; direction += 1)
            {
                double angle = direction * Math.PI * 2 / Directions;
                double ux = Math.Cos(angle);
                double uy = Math.Sin(angle);
                double reach = Margin;
                foreach (double[] disc in discs)
                {
                    reach = Math.Max(reach, (disc[0] - CenterX) * ux + (disc[1] - CenterY) * uy + disc[2] + Margin);
                }

                sharp[direction] = reach;
            }

            for (int direction = 0; direction < Directions; direction += 1)
            {
                // Rounding takes the farthest of the neighbours first, so it never cuts into the open sky.
                double widest = 0;
                double sum = 0;
                for (int offset = -Rounding; offset <= Rounding; offset += 1)
                {
                    double near = sharp[((direction + offset) % Directions + Directions) % Directions];
                    widest = Math.Max(widest, near);
                    sum += near;
                }

                double rounded = Math.Max(sharp[direction], (widest + sum / (Rounding * 2 + 1)) / 2);
                double angle = direction * Math.PI * 2 / Directions;
                double swell = 0;
                for (int index = 0; index < WaveCounts.Length; index += 1)
                {
                    swell += WaveSizes[index] * (0.5 + 0.5 * Math.Sin(angle * WaveCounts[index] + phases[index]));
                }

                _reach[direction] = rounded * (1 + swell);
            }
        }

        /// <summary>How far the clearing reaches from its centre in a direction, in radians.</summary>
        public double EdgeAt(double angle)
        {
            double turn = angle / (Math.PI * 2);
            double at = (turn - Math.Floor(turn)) * Directions;
            int low = (int)at % Directions;
            int high = (low + 1) % Directions;
            double share = at - Math.Floor(at);

            return _reach[low] + (_reach[high] - _reach[low]) * share;
        }

        /// <summary>
        /// How deep a point is in the fog, measured along the line from the centre.
        /// Negative in the clearing, 0 on its edge.
        /// </summary>
        public double DepthAt(double x, double y)
        {
            double dx = x - CenterX;
            double dy = y - CenterY;

            return CleanupMath.Hypot(dx, dy) - EdgeAt(Math.Atan2(dy, dx));
        }

        public bool Covers(double x, double y)
        {
            return DepthAt(x, y) > 0;
        }

        /// <summary>
        /// Whether one can be seen from the other. In the clearing everyone sees
        /// everyone. With either of them in the fog they must be within <see cref="Sight"/>.
        /// </summary>
        public static bool Sees(bool fromFog, double fromX, double fromY, bool toFog, double toX, double toY)
        {
            return (!fromFog && !toFog) || CleanupMath.Hypot(toX - fromX, toY - fromY) <= Sight;
        }
    }
}
