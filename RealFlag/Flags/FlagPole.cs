using System;

namespace RealFlag.Flags
{
    /// <summary>
    /// Which way a flag's hoist runs and its fly points at rest, from its prop's angle (the
    /// heading, turned by Move It and the panel) and its spec's tilt and lean. PURE.
    /// </summary>
    /// <remarks>
    /// The flag's own axes: X along its heading (FlagProps.Direction: (cos a, 0, sin a)), Y up,
    /// Z = X x Y. The pole's up is Y turned by the tilt towards X (a wall-mounted pole leans out
    /// towards the fly), then by the lean towards Z: (sin t, cos t cos l, cos t sin l). The hoist
    /// runs down it from the anchor; the fly at rest is square to it in the flag's own plane,
    /// (cos t, -sin t cos l, -sin t sin l). No tilt and no lean: the hoist straight down, the fly
    /// along the heading -- the cloth as it always was.
    /// </remarks>
    public static class FlagOrientation
    {
        /// <param name="heading">The prop's angle, radians.</param>
        /// <param name="tilt">Degrees towards the fly (FlagSpec.Tilt).</param>
        /// <param name="lean">Degrees to the side (FlagSpec.Lean).</param>
        /// <param name="hx">The hoist's direction from the anchor down (unit).</param>
        /// <param name="fx">The fly's direction at rest (unit, square to the hoist).</param>
        public static void Frame(float heading, float tilt, float lean, out float hx, out float hy, out float hz,
                                 out float fx, out float fy, out float fz)
        {
            double ca = Math.Cos(heading), sa = Math.Sin(heading);
            double t = tilt * Math.PI / 180.0, l = lean * Math.PI / 180.0;
            double st = Math.Sin(t), ct = Math.Cos(t), sl = Math.Sin(l), cl = Math.Cos(l);

            // In the flag's axes: the pole's up, and the fly square to it.
            double ux = st, uy = ct * cl, uz = ct * sl;
            double lx = ct, ly = -st * cl, lz = -st * sl;

            // X = (ca, 0, sa), Z = (-sa, 0, ca).
            hx = (float)-(ux * ca - uz * sa);
            hy = (float)-uy;
            hz = (float)-(ux * sa + uz * ca);
            fx = (float)(lx * ca - lz * sa);
            fy = (float)ly;
            fz = (float)(lx * sa + lz * ca);
        }

        /// <summary>A heading as the Create new rotation holds it: whole degrees, 0..359 (0 for NaN).</summary>
        public static float WholeDegrees(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return 0f;

            double whole = Math.Round(degrees);
            whole -= 360.0 * Math.Floor(whole / 360.0);
            return whole >= 360.0 ? 0f : (float)whole;
        }

        /// <summary>
        /// A right click's turn of the flag being placed, the game's own for props (PropTool's
        /// ToggleAngle, from its IL): round(a / 45 - 1) x 45 degrees -- the next multiple of 45
        /// down, or from a heading off them, the nearest one's next. Whole degrees, 0..359.
        /// </summary>
        public static float StepTurn(float degrees)
        {
            return WholeDegrees((float)Math.Round(degrees / 45f - 1f) * 45f);
        }
    }

    /// <summary>
    /// The numbers of the pole the mod draws under a flag (FlagSpec.Pole): how thick, how
    /// tapered, where its truck and top sit, where the halyard runs. PURE: the pole's mesh
    /// (FlagPoleMesh) and the cloth's collider (FlagCloth.SetPole) are made from the same.
    /// </summary>
    /// <remarks>
    /// Coordinates along the pole's axis, metres UP from its foot. The anchor -- the flag's top
    /// hoist corner, where the halyard's top clip is -- is at <see cref="Length"/>; the shaft
    /// goes on a few centimetres above it to the truck (the cap whose sheave the halyard runs
    /// over), and the top on that. The flag's hoist hangs on the halyard <see cref="Offset"/>
    /// from the axis, on the side the flag flies to at rest: the pole stands just behind it.
    ///
    /// The size of a real pole: its foot is sized for the bending moment of the wind on its
    /// flag at its height, M ~ (flag area) x (height), and a tube's strength goes as its
    /// diameter cubed at a given wall ratio, so the foot's diameter grows as the cube root of
    /// area x height. 0.04 m x (A h)^(1/3) is fitted to the usual pairings of flagpole sizing
    /// charts -- about 13 cm at the foot for a 9 m pole flying a 1.5 x 2.4 m flag, 20 cm for a
    /// 15 m one flying 2.4 x 3.7 m. Cone-tapered to 0.7 of the foot on a short pole, 0.4 on a
    /// tall one. The player's Thickness scales it (50-200%).
    /// </remarks>
    public struct FlagPoleShape
    {
        public int Style;
        public int Ropes;

        /// <summary>From the foot to the anchor's level, metres.</summary>
        public float Length;

        /// <summary>The flag's hoist, metres (where the halyard's lower clip is: Length - FlagHeight).</summary>
        public float FlagHeight;

        /// <summary>The shaft's radius at its foot and at its top.</summary>
        public float RadiusFoot, RadiusTop;

        /// <summary>The shaft's top above the anchor, and the truck on it.</summary>
        public float TopAbove, TruckRadius, TruckHeight;

        /// <summary>The hoist line's distance from the axis: on the halyard, off the truck's sheave.</summary>
        public float Offset;

        /// <summary>The halyard's radius, the return line's distance beside the flag's, and the cleat's height (where both are tied off).</summary>
        public float RopeRadius, RopeGap, Cleat;

        /// <summary>The flash collar round the foot.</summary>
        public float CollarRadius, CollarHeight;

        /// <summary>The ball and the neck it sits on; the spear.</summary>
        public float BallRadius, NeckRadius, NeckHeight, SpearRadius, SpearHeight;

        /// <summary>The shortest pole drawn, metres.</summary>
        public const float MinLength = 0.5f;

        /// <summary>The shaft's top above the foot.</summary>
        public float ShaftTop
        {
            get { return Length + TopAbove; }
        }

        /// <summary>The whole pole's top above the foot: the truck, and the ball or spear on it.</summary>
        public float Tip
        {
            get
            {
                float truck = ShaftTop + TruckHeight;
                if (Style == FlagPoleStyle.Ball)
                    return truck + NeckHeight + 1.9f * BallRadius;
                if (Style == FlagPoleStyle.Spear)
                    return truck + SpearHeight;
                return truck + 0.25f * TruckRadius;
            }
        }

        /// <summary>The shaft's radius at a height above the foot (a straight taper).</summary>
        public float RadiusAt(float up)
        {
            float top = ShaftTop;
            float t = top > 0f ? Math.Max(0f, Math.Min(up / top, 1f)) : 1f;
            return RadiusFoot + (RadiusTop - RadiusFoot) * t;
        }

        /// <summary>The pole for a flag of this spec whose anchor is <paramref name="length"/> metres up its axis from the foot.</summary>
        public static FlagPoleShape For(FlagSpec spec, float length)
        {
            var s = new FlagPoleShape
            {
                Style = spec.Pole,
                Ropes = Math.Max(0, Math.Min(spec.Ropes, 2)),
                Length = Math.Max(length, MinLength),
                FlagHeight = spec.Height,
            };

            float h = Math.Max(s.Length, 1f);
            float area = Math.Max(spec.Length * spec.Height, 0.05f);
            float thickness = Math.Max(FlagSpec.MinPoleThickness, Math.Min(spec.PoleThickness, FlagSpec.MaxPoleThickness));
            float diameter = 0.04f * (float)Math.Pow(area * h, 1.0 / 3.0) * thickness;
            diameter = Math.Max(0.03f, Math.Min(diameter, 3f));

            s.RadiusFoot = 0.5f * diameter;
            s.RadiusTop = s.RadiusFoot * Math.Max(0.4f, 0.75f - 0.005f * h);
            s.TopAbove = Math.Max(0.04f, 0.03f * spec.Height);
            s.TruckRadius = s.RadiusTop * 1.35f;
            s.TruckHeight = s.TruckRadius * 1.1f;
            s.RopeRadius = Math.Max(0.004f, Math.Min(0.12f * s.RadiusTop, 0.03f));
            s.Offset = Math.Max(s.TruckRadius, s.RadiusAt(Math.Max(0f, s.Length - spec.Height))) + 1.5f * s.RopeRadius;
            s.RopeGap = Math.Max(3f * s.RopeRadius, 0.5f * s.RadiusTop);
            s.Cleat = Math.Min(1.3f, 0.25f * s.Length);
            s.CollarHeight = Math.Max(0.12f, 2.5f * s.RadiusFoot);
            s.CollarRadius = 1.9f * s.RadiusFoot;
            s.BallRadius = s.RadiusFoot;
            s.NeckRadius = 0.35f * s.RadiusTop;
            s.NeckHeight = 0.5f * s.BallRadius;
            s.SpearRadius = 0.7f * s.TruckRadius;
            s.SpearHeight = 3.5f * s.TruckRadius;
            return s;
        }

        /// <summary>
        /// The pole's length when nothing is found under it (a horizontal pole over the void): as
        /// long as the hoist, and a little more.
        /// </summary>
        public static float Fallback(FlagSpec spec)
        {
            return 1.2f * spec.Height + 0.5f;
        }

        /// <summary>
        /// The cloth keeps off this pole (FlagCloth.SetPole): its axis runs along the hoist,
        /// <see cref="Offset"/> behind it -- against the fly at rest (<paramref name="fx"/>, from
        /// FlagOrientation) -- from the top of its finial to its foot, thickening down its taper.
        /// </summary>
        public void ApplyTo(FlagCloth cloth, float fx, float fy, float fz)
        {
            float growth = ShaftTop > 0f ? (RadiusFoot - RadiusTop) / ShaftTop : 0f;
            cloth.SetPole(-Offset * fx, -Offset * fy, -Offset * fz, RadiusAt(Length), growth, -(Tip - Length), Length);

            // With its ropes, the flag is on a halyard: clipped by its grommets, free between.
            cloth.SetHalyard(Ropes > 0);
        }

        /// <summary>
        /// The bottom clip below the anchor, up the axis: the hoist's length, less the halyard's
        /// slack when the flag hangs on one (FlagCloth.HalyardSlack), where the lower rope ends.
        /// </summary>
        public float BottomClip
        {
            get { return Ropes > 0 ? FlagHeight * (1f - FlagCloth.HalyardSlack) : FlagHeight; }
        }

        /// <summary>The same pole to within a centimetre: its mesh need not be made again.</summary>
        public bool Same(FlagPoleShape other)
        {
            return Style == other.Style && Ropes == other.Ropes && Math.Abs(Length - other.Length) < 0.01f &&
                   Math.Abs(FlagHeight - other.FlagHeight) < 0.001f && Math.Abs(RadiusFoot - other.RadiusFoot) < 0.0005f;
        }
    }
}
