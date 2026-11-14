using System;

namespace RealFlag.Flags
{
    /// <summary>
    /// Smooth value noise in one and three dimensions, for the wind's gusts and eddies. PURE:
    /// no engine call, so the tests and the preview run it as the game does.
    /// </summary>
    /// <remarks>
    /// Lattice values from a fixed permutation (the same table on every machine: a flag's motion
    /// is a function of its seed and its clock), blended with the quintic fade 6t^5 - 15t^4 + 10t^3
    /// (Perlin 2002, "Improving Noise"), whose first and second derivatives are continuous: the
    /// wind a particle feels has no kinks. Scaled so the result has about unit standard deviation
    /// (Scale, measured by tools/test-cloth.ps1), which lets a caller multiply by a physical
    /// sigma.
    /// </remarks>
    public static class FlagNoise
    {
        /// <summary>
        /// Brings the noise to about unit standard deviation: the lattice values have unit
        /// variance, and the smooth blend between them lowers it to 0.691 in 3-D and 0.884 in 1-D
        /// (measured by tools/test-cloth.ps1 over 200 000 samples, which prints the sigma it finds).
        /// </summary>
        public const float Scale3 = 1.447f;
        public const float Scale1 = 1.131f;

        private static readonly int[] Perm = BuildPermutation();
        private static readonly float[] Values = BuildValues();

        private static int[] BuildPermutation()
        {
            // A fixed shuffle (lowbias32 of the index, sorted): the same on every machine and
            // every run, unlike System.Random, whose algorithm is not promised.
            var keys = new uint[256];
            var order = new int[256];
            for (int i = 0; i < 256; i++)
            {
                keys[i] = Mix((uint)i * 2654435761u + 0x9E3779B9u);
                order[i] = i;
            }

            Array.Sort(keys, order);

            var perm = new int[512];
            for (int i = 0; i < 512; i++)
                perm[i] = order[i & 255];

            return perm;
        }

        /// <summary>The lattice values: hashed, then made exactly zero-mean and unit-variance (256 draws leave a bias the scale would multiply).</summary>
        private static float[] BuildValues()
        {
            var values = new double[256];
            double sum = 0.0;
            for (int i = 0; i < 256; i++)
            {
                values[i] = (Mix((uint)i + 0x68E31DA4u) & 0xFFFFFF) / (double)0x7FFFFF - 1.0;
                sum += values[i];
            }

            double mean = sum / 256.0, sum2 = 0.0;
            for (int i = 0; i < 256; i++)
            {
                values[i] -= mean;
                sum2 += values[i] * values[i];
            }

            double inv = 1.0 / Math.Sqrt(sum2 / 256.0);
            var result = new float[256];
            for (int i = 0; i < 256; i++)
                result[i] = (float)(values[i] * inv);

            return result;
        }

        /// <summary>lowbias32 (Chris Wellons' integer hash): nearby inputs give unrelated outputs.</summary>
        public static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352dU;
            x ^= x >> 15;
            x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }

        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static int Floor(float x)
        {
            int i = (int)x;
            return x < i ? i - 1 : i;
        }

        /// <summary>Value noise along a line, about unit sigma, period 256.</summary>
        public static float Value1(float x, int seed)
        {
            int xi = Floor(x);
            float t = Fade(x - xi);
            int s = seed & 255;
            float a = Values[Perm[((xi & 255) + s) & 255]];
            float b = Values[Perm[(((xi + 1) & 255) + s) & 255]];
            return (a + (b - a) * t) * Scale1;
        }

        /// <summary>Value noise in space, about unit sigma, period 256 on each axis.</summary>
        public static float Value3(float x, float y, float z, int seed)
        {
            int xi = Floor(x), yi = Floor(y), zi = Floor(z);
            float tx = Fade(x - xi), ty = Fade(y - yi), tz = Fade(z - zi);

            int x0 = (xi + seed) & 255, x1 = (xi + 1 + seed) & 255;
            int y0 = yi & 255, y1 = (yi + 1) & 255;
            int z0 = zi & 255, z1 = (zi + 1) & 255;

            int a0 = Perm[x0] + y0, a1 = Perm[x1] + y0;
            int b0 = Perm[x0] + y1, b1 = Perm[x1] + y1;

            float c000 = Values[Perm[Perm[a0] + z0] & 255];
            float c100 = Values[Perm[Perm[a1] + z0] & 255];
            float c010 = Values[Perm[Perm[b0] + z0] & 255];
            float c110 = Values[Perm[Perm[b1] + z0] & 255];
            float c001 = Values[Perm[Perm[a0] + z1] & 255];
            float c101 = Values[Perm[Perm[a1] + z1] & 255];
            float c011 = Values[Perm[Perm[b0] + z1] & 255];
            float c111 = Values[Perm[Perm[b1] + z1] & 255];

            float x00 = c000 + (c100 - c000) * tx;
            float x10 = c010 + (c110 - c010) * tx;
            float x01 = c001 + (c101 - c001) * tx;
            float x11 = c011 + (c111 - c011) * tx;
            float y0v = x00 + (x10 - x00) * ty;
            float y1v = x01 + (x11 - x01) * ty;
            return (y0v + (y1v - y0v) * tz) * Scale3;
        }

        /// <summary>
        /// Three values of noise in space at one point, one for each seed: each exactly what <see
        /// cref="Value3"/> gives with that seed (same sums in the same order), the floors and fades
        /// worked out once (2026-11-14: the wind's eddies of several sizes take three at every sample
        /// of every octave). A seed shifts the lattice along x by whole cells, so seeds a few dozen
        /// apart read unrelated lattice values.
        /// </summary>
        public static void Value3x3(float x, float y, float z, int seedA, int seedB, int seedC, out float a, out float b, out float c)
        {
            int xi = Floor(x), yi = Floor(y), zi = Floor(z);
            float tx = Fade(x - xi), ty = Fade(y - yi), tz = Fade(z - zi);
            int y0 = yi & 255, y1 = (yi + 1) & 255;
            int z0 = zi & 255, z1 = (zi + 1) & 255;
            a = Cell(xi + seedA, y0, y1, z0, z1, tx, ty, tz);
            b = Cell(xi + seedB, y0, y1, z0, z1, tx, ty, tz);
            c = Cell(xi + seedC, y0, y1, z0, z1, tx, ty, tz);
        }

        /// <summary>One lattice cell of <see cref="Value3"/>, its x already shifted by the seed.</summary>
        private static float Cell(int xs, int y0, int y1, int z0, int z1, float tx, float ty, float tz)
        {
            int x0 = xs & 255, x1 = (xs + 1) & 255;

            int a0 = Perm[x0] + y0, a1 = Perm[x1] + y0;
            int b0 = Perm[x0] + y1, b1 = Perm[x1] + y1;

            float c000 = Values[Perm[Perm[a0] + z0] & 255];
            float c100 = Values[Perm[Perm[a1] + z0] & 255];
            float c010 = Values[Perm[Perm[b0] + z0] & 255];
            float c110 = Values[Perm[Perm[b1] + z0] & 255];
            float c001 = Values[Perm[Perm[a0] + z1] & 255];
            float c101 = Values[Perm[Perm[a1] + z1] & 255];
            float c011 = Values[Perm[Perm[b0] + z1] & 255];
            float c111 = Values[Perm[Perm[b1] + z1] & 255];

            float x00 = c000 + (c100 - c000) * tx;
            float x10 = c010 + (c110 - c010) * tx;
            float x01 = c001 + (c101 - c001) * tx;
            float x11 = c011 + (c111 - c011) * tx;
            float y0v = x00 + (x10 - x00) * ty;
            float y1v = x01 + (x11 - x01) * ty;
            return (y0v + (y1v - y0v) * tz) * Scale3;
        }

        /// <summary>
        /// A noise coordinate brought into one period (0..256), worked in double: a field carried for
        /// hours stands thousands of units off, where a float keeps too few of its digits.
        /// </summary>
        public static float Wrap(double x)
        {
            return (float)(x - 256.0 * Math.Floor(x / 256.0));
        }
    }
}
