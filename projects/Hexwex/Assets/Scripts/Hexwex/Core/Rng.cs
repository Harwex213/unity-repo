using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    /// <summary>
    /// The seeded generator of the prototype (<c>core/rng.ts</c>), ported bit for
    /// bit. A nickname is the world seed, so the same name must grow the same
    /// island here as it does in the browser. Do not replace it with
    /// <c>System.Random</c> or <c>UnityEngine.Random</c>.
    /// </summary>
    public sealed class Rng
    {
        private const uint HashPrime1 = 0x9e3779b9u;
        private const uint HashPrime2 = 0x85ebca6bu;
        private const uint HashPrime3 = 0xc2b2ae35u;

        private uint _state;

        public Rng(uint seed)
        {
            _state = seed;
        }

        /// <summary>Creates the generator a text seeds: <c>createRng(hashSeed(text))</c>.</summary>
        public static Rng FromText(string text)
        {
            return new Rng(HashSeed(text));
        }

        /// <summary>The next number in <c>[0, 1)</c>.</summary>
        public double Next()
        {
            unchecked
            {
                _state += HashPrime1;
                uint value = _state;
                value = (value ^ (value >> 15)) * (value | 1u);
                value ^= value + (value ^ (value >> 7)) * (value | 61u);

                return (value ^ (value >> 14)) / 4294967296.0;
            }
        }

        /// <summary>Turns any string into a seed. It hashes UTF-16 code units, as JavaScript does.</summary>
        public static uint HashSeed(string text)
        {
            unchecked
            {
                uint hash = HashPrime2;

                for (int index = 0; index < text.Length; index += 1)
                {
                    hash = (hash ^ text[index]) * HashPrime3;
                    hash = (hash << 13) | (hash >> 19);
                }

                return hash;
            }
        }

        public int RandomInt(int minInclusive, int maxInclusive)
        {
            return minInclusive + (int)Math.Floor(Next() * (maxInclusive - minInclusive + 1));
        }

        /// <summary>The index <c>pick</c> would take from a list of <paramref name="count"/> items.</summary>
        public int NextIndex(int count)
        {
            return (int)Math.Floor(Next() * count);
        }

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items.Count == 0)
            {
                throw new InvalidOperationException("Cannot pick from an empty list");
            }

            return items[NextIndex(items.Count)];
        }

        /// <summary>Picks by weight. <paramref name="weightOf"/> must never return a negative number.</summary>
        public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, double> weightOf)
        {
            double total = 0;
            for (int index = 0; index < items.Count; index += 1)
            {
                total += weightOf(items[index]);
            }

            double threshold = Next() * total;

            for (int index = 0; index < items.Count; index += 1)
            {
                threshold -= weightOf(items[index]);
                if (threshold <= 0)
                {
                    return items[index];
                }
            }

            return Pick(items);
        }
    }

    /// <summary>The JavaScript number rules the prototype's formulas rely on.</summary>
    public static class JsMath
    {
        /// <summary>
        /// <c>Math.round</c>: a half rounds up. <c>System.Math.Round</c> rounds a
        /// half to even, which would change yields and toxicity.
        /// </summary>
        public static int Round(double value)
        {
            return (int)Math.Floor(value + 0.5);
        }
    }
}
