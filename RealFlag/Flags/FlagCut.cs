using System;

namespace RealFlag.Flags
{
    /// <summary>
    /// A flag's cut: the outline of its fly, the rest of the flag's rectangle cut away. Stored by
    /// VALUE (in the save and the settings file): APPEND ONLY, never renumber. PURE.
    /// </summary>
    /// <remarks>
    /// Every cut is a fly edge drawn as a function of the height down the hoist: each row of the
    /// cloth runs from the hoist to <see cref="Edge"/>, so the cloth stays one piece from its hoist
    /// out (the shapes vexillology names, W. Smith 1975: the swallowtail of the Nordic state flags
    /// and guidons, the three-tailed swallowtail of the Danish and Swedish naval ensigns, the pennant,
    /// the swallowtailed burgee of yacht clubs, the pointed pennon, the rounded fly of cavalry
    /// guidons). The same function is written three times and kept in step BY HAND: here (the cloth's
    /// cells, the panels' pictures), FlagWear.cginc's RFCutEdge (the shader's outline) and
    /// FlagPatternView (through this class).
    /// Depth is a share of the flag's LENGTH (hoist to fly) for every cut, so a cut keeps its look
    /// as the flag is resized; at the default third a 2:3 flag's point is square (45 degrees) and its
    /// rounded fly a half circle.
    /// </remarks>
    public static class FlagCut
    {
        public const int Rectangle = 0;
        public const int Swallowtail = 1;
        public const int Tongued = 2;
        public const int Pennant = 3;
        public const int Burgee = 4;
        public const int Pointed = 5;
        public const int Rounded = 6;

        public const int Count = 7;

        public static readonly int[] Values = { Rectangle, Swallowtail, Tongued, Pennant, Burgee, Pointed, Rounded };

        public static readonly string[] NameKeys =
        {
            "Cut.Rectangle", "Cut.Swallowtail", "Cut.Tongued", "Cut.Pennant", "Cut.Burgee", "Cut.Pointed", "Cut.Rounded",
        };

        private static readonly string[] LogNames =
        {
            "rectangle", "swallowtail", "swallowtail and tongue", "pennant", "burgee", "pointed", "rounded",
        };

        /// <summary>The depth of a notch or point, a share of the length: a third (the Danish split flag's notch).</summary>
        public const float DefaultDepth = 1f / 3f;
        public const float MinDepth = 0.1f;
        public const float MaxDepth = 0.6f;

        /// <summary>
        /// A burgee's two tails meet the fly at this far from the middle, a share of half the hoist:
        /// each tail's tip a quarter of the hoist in from its edge.
        /// </summary>
        public const float BurgeeTips = 0.5f;

        public static bool IsKnown(int value)
        {
            return value >= 0 && value < Count;
        }

        public static int Sanitise(int value)
        {
            return IsKnown(value) ? value : Rectangle;
        }

        public static float SanitiseDepth(float depth)
        {
            if (float.IsNaN(depth) || float.IsInfinity(depth))
                return DefaultDepth;

            return Math.Max(MinDepth, Math.Min(depth, MaxDepth));
        }

        /// <summary>Whether the cut has a depth to set (the rectangle and the pennant have none).</summary>
        public static bool UsesDepth(int cut)
        {
            return cut != Rectangle && cut != Pennant && IsKnown(cut);
        }

        public static string LogName(int cut)
        {
            return LogNames[Sanitise(cut)];
        }

        /// <summary>
        /// The fly edge at <paramref name="v"/> down the hoist (0 top .. 1 bottom), as a share of the
        /// length (0 hoist .. 1 the rectangle's fly): the cloth of that row runs from 0 to this.
        /// </summary>
        public static float Edge(int cut, float depth, float v)
        {
            v = v < 0f ? 0f : v > 1f ? 1f : v;
            float d = depth;
            // 0 in the middle of the hoist, 1 at its top and bottom.
            float a = Math.Abs(2f * v - 1f);
            switch (cut)
            {
                case Swallowtail:
                    return 1f - d * (1f - a);
                case Tongued:
                {
                    // Two notches, a quarter and three quarters down: tails at the top, the middle and the bottom.
                    float b = v < 0.5f ? Math.Abs(4f * v - 1f) : Math.Abs(4f * v - 3f);
                    return 1f - d * (1f - b);
                }
                case Pennant:
                    return 1f - a;
                case Burgee:
                {
                    float w = BurgeeTips;
                    float taper = (1f - a) / (1f - w);
                    float notch = 1f - d + d * a / w;
                    return Math.Min(1f, Math.Min(taper, notch));
                }
                case Pointed:
                    return 1f - d * a;
                case Rounded:
                    return 1f - d + d * (float)Math.Sqrt(Math.Max(0f, 1f - a * a));
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Whether the point (<paramref name="u"/> along, <paramref name="v"/> down, shares of the
        /// length and the hoist) is cloth.
        /// </summary>
        public static bool Inside(int cut, float depth, float u, float v)
        {
            return u <= Edge(cut, depth, v);
        }

        /// <summary>The farthest the fly edge reaches between two heights (shares of the hoist).</summary>
        public static float MaxEdge(int cut, float depth, float v0, float v1)
        {
            if (v1 < v0)
            {
                float t = v0; v0 = v1; v1 = t;
            }

            const int Samples = 16;
            float best = Math.Max(Edge(cut, depth, v0), Edge(cut, depth, v1));
            for (int s = 1; s < Samples; s++)
                best = Math.Max(best, Edge(cut, depth, v0 + (v1 - v0) * s / Samples));

            // The tips, where the edge turns: sampling could step over them.
            float tip = (1f - BurgeeTips) * 0.5f;
            float[] kinks = { 0f, 0.25f, 0.5f, 0.75f, 1f, tip, 1f - tip };
            for (int c = 0; c < kinks.Length; c++)
            {
                if (kinks[c] > v0 && kinks[c] < v1)
                    best = Math.Max(best, Edge(cut, depth, kinks[c]));
            }

            return best;
        }

        /// <summary>
        /// The cloth's cells for a grid of <paramref name="columns"/> x <paramref name="rows"/>
        /// particles: cell (i, j) -- between columns i, i + 1 and rows j, j + 1, index
        /// j * (columns - 1) + i -- is cloth when any of it is inside the outline, so the cloth
        /// covers the whole outline and the shader cuts its exact edge. Null for the rectangle (every
        /// cell is cloth).
        /// </summary>
        public static bool[] Cells(int cut, float depth, int columns, int rows)
        {
            cut = Sanitise(cut);
            if (cut == Rectangle || columns < 2 || rows < 2)
                return null;

            depth = SanitiseDepth(depth);
            int cu = columns - 1, cv = rows - 1;
            var cells = new bool[cu * cv];
            for (int j = 0; j < cv; j++)
            {
                float reach = MaxEdge(cut, depth, j / (float)cv, (j + 1) / (float)cv);
                for (int i = 0; i < cu; i++)
                {
                    // Anything of the cell inside: its hoist side short of the edge, a hair of slack.
                    cells[j * cu + i] = i / (float)cu < reach - 1e-4f;
                }
            }

            return cells;
        }

        /// <summary>The share of the rectangle's area the cut keeps (for the log and the tests).</summary>
        public static float Area(int cut, float depth)
        {
            const int Samples = 256;
            float sum = 0f;
            for (int s = 0; s < Samples; s++)
                sum += Math.Max(0f, Math.Min(1f, Edge(cut, depth, (s + 0.5f) / Samples)));

            return sum / Samples;
        }
    }
}
