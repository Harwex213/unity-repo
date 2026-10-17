using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    /// <summary>The collision view of an island. The sim owns these objects and mutates them.</summary>
    public sealed class Body
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        /// <summary>Inverse mass. A heavier island shoves a lighter one further.</summary>
        public double InvMass;
        /// <summary>Hex centres relative to the body position. They grow when an island joins.</summary>
        public double[] LocalX;
        public double[] LocalY;
        /// <summary>Centroid of the hexes, relative to the body position.</summary>
        public double CenterX;
        public double CenterY;
        /// <summary>Centroid to the farthest hex corner.</summary>
        public double Radius;
        /// <summary>A body that has joined another island, or drifted off, takes no part in collisions.</summary>
        public bool Solid = true;
        /// <summary>
        /// An anchored body is not pushed by an unanchored one: a guarded enemy
        /// island holds its ground against the player's island. Two anchored bodies
        /// still push each other by mass.
        /// </summary>
        public bool Anchored;
    }

    public readonly struct Bridge
    {
        public readonly int A;
        public readonly int B;
        public readonly int HexA;
        public readonly int HexB;

        public Bridge(int a, int b, int hexA, int hexB)
        {
            A = a;
            B = b;
            HexA = hexA;
            HexB = hexB;
        }
    }

    public readonly struct PairDistance
    {
        public readonly int A;
        public readonly int B;
        /// <summary>Shortest gap between any two hexes of the two islands, by the SAT measure.</summary>
        public readonly double Gap;

        public PairDistance(int a, int b, double gap)
        {
            A = a;
            B = b;
            Gap = gap;
        }
    }

    /// <summary>
    /// Hex-level collision between the islands of the cleanup phase
    /// (<c>core/cleanup-collision.ts</c>). Islands never rotate, so every hex of
    /// every island has the same orientation. Two such hexes overlap exactly when
    /// their centres overlap on all three edge normals. That makes the separating
    /// axis test exact and cheap: three dot products.
    /// </summary>
    public static class CleanupCollision
    {
        /// <summary>Distance from a hex centre to the middle of an edge.</summary>
        public static readonly double HexInradius = HexMath.HexSize * Math.Sqrt(3) / 2;
        /// <summary>Distance between two neighbouring hex centres.</summary>
        public static readonly double HexStep = HexMath.HexSize * Math.Sqrt(3);
        /// <summary>Two facing hexes of two islands closer than this are a bridge for units.</summary>
        public const double BridgeGap = 12;

        /// <summary>A pointy-top hex has its edges facing 0°, 60° and 120°.</summary>
        private static readonly double[] AxisX = { Math.Cos(0), Math.Cos(60 * Math.PI / 180), Math.Cos(120 * Math.PI / 180) };
        private static readonly double[] AxisY = { Math.Sin(0), Math.Sin(60 * Math.PI / 180), Math.Sin(120 * Math.PI / 180) };
        /// <summary>A bridge must join hexes that face each other, not two hexes a step apart.</summary>
        private static readonly double BridgeMaxCenterDistance = HexStep * 1.2;
        /// <summary>How much of the sideways slip two docked islands lose per tick.</summary>
        private const double DockFriction = 0.3;
        private const int SolverIterations = 4;
        /// <summary>Islands farther apart than their radii plus this skip the hex pass.</summary>
        private static readonly double BroadphaseMargin = HexStep;

        /// <summary>
        /// The signed gap between two hexes with centre offset (dx, dy). Negative is
        /// an overlap. <paramref name="axis"/> is the edge normal of the best separating axis.
        /// </summary>
        public static double HexGap(double dx, double dy, out int axis)
        {
            double best = double.NegativeInfinity;
            axis = 0;

            for (int index = 0; index < 3; index += 1)
            {
                double separation = Math.Abs(dx * AxisX[index] + dy * AxisY[index]) - HexInradius * 2;
                if (separation > best)
                {
                    best = separation;
                    axis = index;
                }
            }

            return best;
        }

        /// <summary>
        /// Pushes overlapping islands apart and kills the part of their velocity that
        /// drives them into each other. The push is split by inverse mass, so a big
        /// island shoves a small one. Docked islands also lose some sideways slip, so
        /// they stay docked instead of sliding off each other.
        /// </summary>
        public static void Solve(IReadOnlyList<Body> bodies)
        {
            for (int iteration = 0; iteration < SolverIterations; iteration += 1)
            {
                bool touched = false;

                for (int i = 0; i < bodies.Count; i += 1)
                {
                    Body a = bodies[i];
                    if (!a.Solid)
                    {
                        continue;
                    }

                    for (int j = i + 1; j < bodies.Count; j += 1)
                    {
                        Body b = bodies[j];
                        if (!b.Solid || !NearEnough(a, b))
                        {
                            continue;
                        }

                        double invA = a.Anchored && !b.Anchored ? 0 : a.InvMass;
                        double invB = b.Anchored && !a.Anchored ? 0 : b.InvMass;
                        double invSum = invA + invB;
                        if (invSum == 0)
                        {
                            continue;
                        }

                        if (!DeepestOverlap(a, b, out double depth, out double normalX, out double normalY))
                        {
                            continue;
                        }

                        touched = true;
                        a.X -= normalX * depth * (invA / invSum);
                        a.Y -= normalY * depth * (invA / invSum);
                        b.X += normalX * depth * (invB / invSum);
                        b.Y += normalY * depth * (invB / invSum);

                        double relX = b.Vx - a.Vx;
                        double relY = b.Vy - a.Vy;
                        double closing = relX * normalX + relY * normalY;
                        if (closing < 0)
                        {
                            double impulse = -closing / invSum;
                            a.Vx -= impulse * normalX * invA;
                            a.Vy -= impulse * normalY * invA;
                            b.Vx += impulse * normalX * invB;
                            b.Vy += impulse * normalY * invB;
                        }

                        if (iteration == 0)
                        {
                            double slipX = relX - closing * normalX;
                            double slipY = relY - closing * normalY;
                            double impulse = DockFriction / invSum;
                            a.Vx += slipX * impulse * invA;
                            a.Vy += slipY * impulse * invA;
                            b.Vx -= slipX * impulse * invB;
                            b.Vy -= slipY * impulse * invB;
                        }
                    }
                }

                if (!touched)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Every pair of facing hexes of two islands that nearly touch. Units walk
        /// across these. Also gives the smallest gap of each nearby island pair.
        /// </summary>
        public static void FindBridges(IReadOnlyList<Body> bodies, List<Bridge> bridges, List<PairDistance> distances)
        {
            bridges.Clear();
            distances.Clear();

            for (int a = 0; a < bodies.Count; a += 1)
            {
                Body bodyA = bodies[a];
                if (!bodyA.Solid)
                {
                    continue;
                }

                for (int b = a + 1; b < bodies.Count; b += 1)
                {
                    Body bodyB = bodies[b];
                    if (!bodyB.Solid)
                    {
                        continue;
                    }

                    double dx = bodyB.X + bodyB.CenterX - (bodyA.X + bodyA.CenterX);
                    double dy = bodyB.Y + bodyB.CenterY - (bodyA.Y + bodyA.CenterY);
                    double centerGap = CleanupMath.Hypot(dx, dy) - bodyA.Radius - bodyB.Radius;
                    // Far islands still get a rough gap, so units can rally toward them.
                    if (centerGap > BroadphaseMargin * 6)
                    {
                        distances.Add(new PairDistance(a, b, centerGap));

                        continue;
                    }

                    double smallest = double.PositiveInfinity;

                    for (int i = 0; i < bodyA.LocalX.Length; i += 1)
                    {
                        double ax = bodyA.X + bodyA.LocalX[i];
                        double ay = bodyA.Y + bodyA.LocalY[i];

                        for (int j = 0; j < bodyB.LocalX.Length; j += 1)
                        {
                            double hx = bodyB.X + bodyB.LocalX[j] - ax;
                            double hy = bodyB.Y + bodyB.LocalY[j] - ay;
                            double gap = HexGap(hx, hy, out _);
                            smallest = Math.Min(smallest, gap);

                            if (gap < BridgeGap && CleanupMath.Hypot(hx, hy) < BridgeMaxCenterDistance)
                            {
                                bridges.Add(new Bridge(a, b, i, j));
                            }
                        }
                    }

                    distances.Add(new PairDistance(a, b, smallest));
                }
            }
        }

        private static bool NearEnough(Body a, Body b)
        {
            double dx = b.X + b.CenterX - (a.X + a.CenterX);
            double dy = b.Y + b.CenterY - (a.Y + a.CenterY);

            return CleanupMath.Hypot(dx, dy) < a.Radius + b.Radius + BroadphaseMargin;
        }

        /// <summary>The deepest overlap between two bodies. Returns <c>false</c> when they do not touch.</summary>
        private static bool DeepestOverlap(Body a, Body b, out double depth, out double normalX, out double normalY)
        {
            depth = 0;
            normalX = 0;
            normalY = 0;

            for (int i = 0; i < a.LocalX.Length; i += 1)
            {
                double ax = a.X + a.LocalX[i];
                double ay = a.Y + a.LocalY[i];

                for (int j = 0; j < b.LocalX.Length; j += 1)
                {
                    double dx = b.X + b.LocalX[j] - ax;
                    double dy = b.Y + b.LocalY[j] - ay;
                    double gap = HexGap(dx, dy, out int axis);
                    if (-gap > depth)
                    {
                        double sign = dx * AxisX[axis] + dy * AxisY[axis] >= 0 ? 1 : -1;
                        depth = -gap;
                        normalX = AxisX[axis] * sign;
                        normalY = AxisY[axis] * sign;
                    }
                }
            }

            return depth > 0;
        }
    }

    /// <summary>Where a joining island fits on the player's island.</summary>
    public sealed class AttachPlan
    {
        /// <summary>Axial translation from the joining island's frame to the player's frame.</summary>
        public int OffsetQ;
        public int OffsetR;
        /// <summary>Indices of the joining island's hexes that join, in its own order.</summary>
        public List<int> Kept;
        /// <summary>How many of its hexes were dropped (clash or no connection).</summary>
        public int Dropped;
    }

    /// <summary>
    /// Snapping a cleared island onto the player's island
    /// (<c>core/cleanup-attach.ts</c>). Islands never rotate, so both hex lattices
    /// share one orientation: the only freedom is an axial translation. The snap
    /// rounds the islands' relative position to the nearest lattice translation,
    /// then tries its six neighbours too and keeps the one with the fewest
    /// clashing hexes. Clashing hexes are dropped, and so is any new hex that
    /// would not connect to the island: the island stays one piece.
    /// </summary>
    public static class CleanupAttach
    {
        /// <summary>Cube rounding: the lattice cell that contains a fractional axial point.</summary>
        public static Axial RoundAxial(double q, double r)
        {
            double s = -q - r;
            int rq = JsMath.Round(q);
            int rr = JsMath.Round(r);
            int rs = JsMath.Round(s);
            double dq = Math.Abs(rq - q);
            double dr = Math.Abs(rr - r);
            double ds = Math.Abs(rs - s);

            if (dq > dr && dq > ds)
            {
                rq = -rr - rs;
            }
            else if (dr > ds)
            {
                rr = -rq - rs;
            }

            return new Axial(rq, rr);
        }

        /// <summary>
        /// Where a joining island fits. <c>dx, dy</c> is its origin relative to the
        /// player island's origin. Returns <c>null</c> when no hex of it would connect.
        /// </summary>
        public static AttachPlan Plan(IReadOnlyList<CleanupHex> baseHexes, IReadOnlyList<CleanupHex> joining, double dx, double dy)
        {
            HashSet<Axial> taken = new HashSet<Axial>();
            foreach (CleanupHex hex in baseHexes)
            {
                taken.Add(new Axial(hex.Q, hex.R));
            }

            // Fractional axial coordinates of the pixel offset (pointy-top).
            double fractionQ = (Math.Sqrt(3) / 3 * dx - dy / 3) / HexMath.HexSize;
            double fractionR = 2.0 / 3 * dy / HexMath.HexSize;
            Axial center = RoundAxial(fractionQ, fractionR);
            List<Axial> candidates = new List<Axial> { center };
            foreach (Axial step in HexMath.Directions)
            {
                candidates.Add(new Axial(center.Q + step.Q, center.R + step.R));
            }

            Axial offset = center;
            double bestScore = double.PositiveInfinity;

            foreach (Axial candidate in candidates)
            {
                int clashes = 0;
                foreach (CleanupHex hex in joining)
                {
                    if (taken.Contains(new Axial(hex.Q + candidate.Q, hex.R + candidate.R)))
                    {
                        clashes += 1;
                    }
                }

                HexMath.HexToPixel(candidate.Q, candidate.R, HexMath.HexSize, out double atX, out double atY);
                // A clash costs more than any distance, so the cleanest fit wins first.
                double score = clashes * 1e6 + CleanupMath.Hypot(atX - dx, atY - dy);
                if (score < bestScore)
                {
                    offset = candidate;
                    bestScore = score;
                }
            }

            Dictionary<Axial, int> free = new Dictionary<Axial, int>();
            List<Axial> freeOrder = new List<Axial>();
            for (int index = 0; index < joining.Count; index += 1)
            {
                Axial cell = new Axial(joining[index].Q + offset.Q, joining[index].R + offset.R);
                if (!taken.Contains(cell))
                {
                    free[cell] = index;
                    freeOrder.Add(cell);
                }
            }

            // Keep only the new hexes that the old island reaches through new hexes.
            HashSet<Axial> reached = new HashSet<Axial>();
            List<Axial> queue = new List<Axial>();
            foreach (Axial cell in freeOrder)
            {
                foreach (Axial step in HexMath.Directions)
                {
                    if (taken.Contains(new Axial(cell.Q + step.Q, cell.R + step.R)))
                    {
                        reached.Add(cell);
                        queue.Add(cell);

                        break;
                    }
                }
            }

            for (int head = 0; head < queue.Count; head += 1)
            {
                Axial cell = queue[head];
                foreach (Axial step in HexMath.Directions)
                {
                    Axial next = new Axial(cell.Q + step.Q, cell.R + step.R);
                    if (free.ContainsKey(next) && reached.Add(next))
                    {
                        queue.Add(next);
                    }
                }
            }

            List<int> kept = new List<int>();
            foreach (Axial cell in freeOrder)
            {
                if (reached.Contains(cell))
                {
                    kept.Add(free[cell]);
                }
            }

            if (kept.Count == 0)
            {
                return null;
            }

            return new AttachPlan { OffsetQ = offset.Q, OffsetR = offset.R, Kept = kept, Dropped = joining.Count - kept.Count };
        }
    }
}
