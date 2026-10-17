using System;
using System.Collections.Generic;
using System.Linq;

namespace Hexwex.Core
{
    /// <summary>An axial hex coordinate: <c>Q</c> to the right, <c>R</c> down-right.</summary>
    public readonly struct Axial : IEquatable<Axial>
    {
        public readonly int Q;
        public readonly int R;

        public Axial(int q, int r)
        {
            Q = q;
            R = r;
        }

        public bool Equals(Axial other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object obj)
        {
            return obj is Axial other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked(Q * 397 ^ R);
        }

        public override string ToString()
        {
            return HexMath.HexId(Q, R);
        }
    }

    /// <summary>
    /// Pointy-top hexes in axial coordinates (<c>core/hex.ts</c>). The prototype
    /// draws them on a 2D canvas with y down. The 3D view lays the same plane on
    /// the ground: prototype x is world x, prototype y is world -z.
    /// </summary>
    public static class HexMath
    {
        /// <summary>Radius of one hex in the prototype's canvas units.</summary>
        public const double HexSize = 52;

        private static readonly double Sqrt3 = Math.Sqrt(3);

        public static readonly IReadOnlyList<Axial> Directions = new[]
        {
            new Axial(1, 0),
            new Axial(1, -1),
            new Axial(0, -1),
            new Axial(-1, 0),
            new Axial(-1, 1),
            new Axial(0, 1),
        };

        /// <summary><c>q,r</c> as a string. It keys hexes everywhere, and seeds depend on its order.</summary>
        public static string HexId(int q, int r)
        {
            return q + "," + r;
        }

        /// <summary>The hex centre on the prototype's plane, y down.</summary>
        public static void HexToPixel(int q, int r, double size, out double x, out double y)
        {
            x = size * Sqrt3 * (q + r / 2.0);
            y = size * 1.5 * r;
        }

        public static IEnumerable<Axial> Neighbors(int q, int r)
        {
            for (int index = 0; index < Directions.Count; index += 1)
            {
                yield return new Axial(q + Directions[index].Q, r + Directions[index].R);
            }
        }

        public static int Distance(int aq, int ar, int bq, int br)
        {
            int dq = aq - bq;
            int dr = ar - br;

            return (Math.Abs(dq) + Math.Abs(dq + dr) + Math.Abs(dr)) / 2;
        }

        /// <summary>Every axial coordinate within <paramref name="radius"/> of the origin, centre first.</summary>
        public static List<Axial> Area(int radius)
        {
            List<Axial> area = new List<Axial>();

            for (int q = -radius; q <= radius; q += 1)
            {
                int rMin = Math.Max(-radius, -q - radius);
                int rMax = Math.Min(radius, -q + radius);

                for (int r = rMin; r <= rMax; r += 1)
                {
                    area.Add(new Axial(q, r));
                }
            }

            // OrderBy is stable, as Array.prototype.sort is. List.Sort is not, and
            // island generation consumes the generator in this order.
            return area.OrderBy(cell => Distance(cell.Q, cell.R, 0, 0)).ToList();
        }

        /// <summary>True when every hex reaches every other through neighbours.</summary>
        public static bool IsConnected(IReadOnlyList<Axial> hexes)
        {
            if (hexes.Count == 0)
            {
                return true;
            }

            HashSet<Axial> keys = new HashSet<Axial>(hexes);
            HashSet<Axial> seen = new HashSet<Axial> { hexes[0] };
            Queue<Axial> queue = new Queue<Axial>();
            queue.Enqueue(hexes[0]);

            while (queue.Count > 0)
            {
                Axial hex = queue.Dequeue();
                foreach (Axial next in Neighbors(hex.Q, hex.R))
                {
                    if (keys.Contains(next) && seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return seen.Count == keys.Count;
        }
    }
}
