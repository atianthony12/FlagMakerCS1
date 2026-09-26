using System;

namespace RealFlag.Flags
{
    /// <summary>
    /// The drawn surface through a cloth's particles. The GPU draws it (FlagCloth.shader's vertex
    /// program: the Catmull-Rom patch of each cell, from the particles FlagMesh hands it in a
    /// small texture); this is the same arithmetic in C#, for the tests, and the smoothing of the
    /// cloth's edges before it is drawn. PURE.
    /// </summary>
    /// <remarks>
    /// The cloth has a particle every 7 to 17 cm on a 3 m flag and every metre and more on a 30 m
    /// one; drawn as flat triangles between them, a flying flag's outline and its folds are
    /// polygons -- straight runs of 20 pixels with a kink at every particle on a big flag seen
    /// near. The drawn sheet is instead the tensor-product Catmull-Rom spline through them
    /// (Catmull and Rom 1974, "A class of local interpolating splines"): it passes through every
    /// particle, so the cloth is exactly where the physics put it, and is smooth between them,
    /// with its normals from its own derivatives. At an edge the missing neighbour is the
    /// reflection of the one inside through the edge's, P(-1) = 2 P(0) - P(1) (the usual phantom
    /// point of an open Catmull-Rom curve: its end runs straight on), folded into the weights.
    ///
    /// The edges: a period-two zigzag along the fly, the top or the bottom -- a particle up, the
    /// next down -- is the one wrinkle the cloth's two-apart bending links cannot feel (their
    /// length does not change), and it shows as a frayed edge. Before drawing, each edge is
    /// smoothed as a curve by one pass pair of Taubin's filter (1995, "A signal processing
    /// approach to fair surface design"): x + lambda L(x), then + mu L(x) with mu below -lambda,
    /// which removes the zigzag entirely and passes the flag's own waves (a wave six particles
    /// long keeps 95% of its height) without shrinking the outline. The hoist and the corners
    /// are held.
    /// </remarks>
    public static class ClothSurface
    {
        /// <summary>Taubin's pass pair: lambda 0.5 and mu for a pass band of 0.1 (1 / lambda + 1 / mu = 0.1).</summary>
        public const float Lambda = 0.5f;
        public const float Mu = -0.5263158f;

        /// <summary>
        /// The fly edge, the top and the bottom smoothed as curves (see the remarks), in place;
        /// the hoist column and the four corners held. <paramref name="scratch"/> holds at least
        /// 3 x max(cols, rows) floats.
        /// </summary>
        public static void SmoothEdges(float[] x, float[] y, float[] z, int cols, int rows, float[] scratch)
        {
            SmoothLine(x, y, z, cols - 1, cols, rows, scratch);
            SmoothLine(x, y, z, 0, 1, cols, scratch);
            SmoothLine(x, y, z, (rows - 1) * cols, 1, cols, scratch);
        }

        private static void SmoothLine(float[] x, float[] y, float[] z, int start, int stride, int count, float[] t)
        {
            if (count < 3)
                return;

            for (int pass = 0; pass < 2; pass++)
            {
                float f = pass == 0 ? Lambda : Mu;
                for (int m = 1; m < count - 1; m++)
                {
                    int k = start + m * stride, a = k - stride, b = k + stride;
                    t[3 * m] = x[k] + f * (0.5f * (x[a] + x[b]) - x[k]);
                    t[3 * m + 1] = y[k] + f * (0.5f * (y[a] + y[b]) - y[k]);
                    t[3 * m + 2] = z[k] + f * (0.5f * (z[a] + z[b]) - z[k]);
                }

                for (int m = 1; m < count - 1; m++)
                {
                    int k = start + m * stride;
                    x[k] = t[3 * m];
                    y[k] = t[3 * m + 1];
                    z[k] = t[3 * m + 2];
                }
            }
        }

        /// <summary>
        /// The Catmull-Rom weights, at <paramref name="t"/> (0..1) between points i0 and i0 + 1 of
        /// <paramref name="n"/>, of the four points i0 - 1 .. i0 + 2, and their derivatives, with
        /// the phantom point past either end folded in (the shader's RFWeights, line for line).
        /// </summary>
        public static void Weights(float t, int i0, int n, float[] w, float[] d)
        {
            float t2 = t * t, t3 = t2 * t;
            float w0 = 0.5f * (-t3 + 2f * t2 - t), w1 = 0.5f * (3f * t3 - 5f * t2 + 2f);
            float w2 = 0.5f * (-3f * t3 + 4f * t2 + t), w3 = 0.5f * (t3 - t2);
            float d0 = 0.5f * (-3f * t2 + 4f * t - 1f), d1 = 0.5f * (9f * t2 - 10f * t);
            float d2 = 0.5f * (-9f * t2 + 8f * t + 1f), d3 = 0.5f * (3f * t2 - 2f * t);

            float lo = i0 <= 0 ? 1f : 0f, hi = i0 >= n - 2 ? 1f : 0f;
            w[0] = w0 * (1f - lo);
            w[1] = w1 + 2f * w0 * lo - w3 * hi;
            w[2] = w2 - w0 * lo + 2f * w3 * hi;
            w[3] = w3 * (1f - hi);
            d[0] = d0 * (1f - lo);
            d[1] = d1 + 2f * d0 * lo - d3 * hi;
            d[2] = d2 - d0 * lo + 2f * d3 * hi;
            d[3] = d3 * (1f - hi);
        }

        /// <summary>
        /// The surface at grid coordinates (<paramref name="gu"/> along the fly, <paramref name="gv"/>
        /// down the hoist, in particles), and its derivatives along each: what the shader draws.
        /// Each output holds three floats.
        /// </summary>
        public static void Evaluate(float[] x, float[] y, float[] z, int cols, int rows, float gu, float gv,
                                    float[] p, float[] pu, float[] pv)
        {
            int i0 = Math.Min((int)Math.Floor(gu), cols - 2), j0 = Math.Min((int)Math.Floor(gv), rows - 2);
            i0 = Math.Max(i0, 0);
            j0 = Math.Max(j0, 0);
            float[] wu = new float[4], du = new float[4], wv = new float[4], dv = new float[4];
            Weights(gu - i0, i0, cols, wu, du);
            Weights(gv - j0, j0, rows, wv, dv);

            for (int c = 0; c < 3; c++)
            {
                p[c] = 0f;
                pu[c] = 0f;
                pv[c] = 0f;
            }

            for (int b = 0; b < 4; b++)
            {
                int j = Math.Max(0, Math.Min(j0 - 1 + b, rows - 1));
                for (int a = 0; a < 4; a++)
                {
                    int i = Math.Max(0, Math.Min(i0 - 1 + a, cols - 1));
                    int k = j * cols + i;
                    float[] q = { x[k], y[k], z[k] };
                    for (int c = 0; c < 3; c++)
                    {
                        p[c] += wu[a] * wv[b] * q[c];
                        pu[c] += du[a] * wv[b] * q[c];
                        pv[c] += wu[a] * dv[b] * q[c];
                    }
                }
            }
        }

        /// <summary>The drawn grid's size for a cloth of cols x rows particles at <paramref name="refine"/> drawn cells a cloth cell.</summary>
        public static int VerticesFor(int cols, int rows, int refine)
        {
            return ((cols - 1) * refine + 1) * ((rows - 1) * refine + 1);
        }
    }
}
