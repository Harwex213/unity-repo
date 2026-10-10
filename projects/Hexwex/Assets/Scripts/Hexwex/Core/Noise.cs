using System;

namespace Hexwex.Core
{
    /// <summary>
    /// Seeded 3D Perlin noise and its fractal sum (<c>core/noise.ts</c>). The
    /// world generator reads it at cell centres to cluster islands into
    /// archipelagos.
    /// </summary>
    public sealed class PerlinNoise
    {
        private readonly int[] _perm = new int[512];

        /// <summary>Improved Perlin noise over a permutation shuffled by the seed.</summary>
        public PerlinNoise(Rng rng)
        {
            int[] source = new int[256];
            for (int index = 0; index < source.Length; index += 1)
            {
                source[index] = index;
            }

            for (int index = source.Length - 1; index > 0; index -= 1)
            {
                int other = rng.NextIndex(index + 1);
                int swap = source[index];
                source[index] = source[other];
                source[other] = swap;
            }

            for (int index = 0; index < 512; index += 1)
            {
                _perm[index] = source[index & 255];
            }
        }

        public double Sample(double x, double y, double z)
        {
            double floorX = Math.Floor(x);
            double floorY = Math.Floor(y);
            double floorZ = Math.Floor(z);
            int cellX = (int)floorX & 255;
            int cellY = (int)floorY & 255;
            int cellZ = (int)floorZ & 255;
            double fx = x - floorX;
            double fy = y - floorY;
            double fz = z - floorZ;
            double u = Fade(fx);
            double v = Fade(fy);
            double w = Fade(fz);

            int a = _perm[cellX] + cellY;
            int aa = _perm[a] + cellZ;
            int ab = _perm[a + 1] + cellZ;
            int b = _perm[cellX + 1] + cellY;
            int ba = _perm[b] + cellZ;
            int bb = _perm[b + 1] + cellZ;

            return Lerp(
                Lerp(
                    Lerp(Gradient(_perm[aa], fx, fy, fz), Gradient(_perm[ba], fx - 1, fy, fz), u),
                    Lerp(Gradient(_perm[ab], fx, fy - 1, fz), Gradient(_perm[bb], fx - 1, fy - 1, fz), u),
                    v),
                Lerp(
                    Lerp(Gradient(_perm[aa + 1], fx, fy, fz - 1), Gradient(_perm[ba + 1], fx - 1, fy, fz - 1), u),
                    Lerp(Gradient(_perm[ab + 1], fx, fy - 1, fz - 1), Gradient(_perm[bb + 1], fx - 1, fy - 1, fz - 1), u),
                    v),
                w);
        }

        /// <summary>Fractal sum of octaves, normalised back to roughly -1..1.</summary>
        public double Fbm(double x, double y, double z, int octaves)
        {
            double sum = 0;
            double amplitude = 1;
            double frequency = 1;
            double total = 0;

            for (int octave = 0; octave < octaves; octave += 1)
            {
                sum += Sample(x * frequency, y * frequency, z * frequency) * amplitude;
                total += amplitude;
                amplitude *= 0.5;
                frequency *= 2.03;
            }

            return sum / total * 1.6;
        }

        private static double Fade(double t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private static double Gradient(int hash, double x, double y, double z)
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : h == 12 || h == 14 ? x : z;

            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}
