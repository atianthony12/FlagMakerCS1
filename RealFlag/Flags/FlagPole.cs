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
    ///
    /// A flag hoisted from its TOP (FlagHoist) has the two the other way round: the hoist runs
    /// across along the heading from the anchor and the fly hangs down (untilted), in the same
    /// plane, the same face to the same side. Its top corner on the hoist is the anchor still, and
    /// the design, laid from the hoist, reads turned as a flag hung upright is (FlagHoist).
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
            Frame(heading, tilt, lean, FlagHoist.Side, out hx, out hy, out hz, out fx, out fy, out fz);
        }

        /// <summary>The same for a flag hoisted from <paramref name="hoistedFrom"/> (FlagHoist): from its top, the hoist and the fly trade places.</summary>
        public static void Frame(float heading, float tilt, float lean, int hoistedFrom, out float hx, out float hy, out float hz,
                                 out float fx, out float fy, out float fz)
        {
            if (hoistedFrom == FlagHoist.Top)
            {
                Frame(heading, tilt, lean, FlagHoist.Side, out fx, out fy, out fz, out hx, out hy, out hz);
                return;
            }

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

        /// <summary>A heading as the new flag's rotation holds it: whole degrees, 0..359 (0 for NaN).</summary>
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
    /// Where a flag being placed stands: the lowest of it at rest ON the surface under the pointer,
    /// raised or lowered whole by the keys -- a standing pole's foot, the flag at its top as tall
    /// as its Pole height makes it; with no pole (or a banner's crossbar, found along to its wall),
    /// the hoist, or the cloth a banner hangs down from its crossbar. What is under the pointer
    /// never changes the pole: the first rule put the flag's top its Elevation over the surface
    /// and cast the pole down from there, so over a building -- the pointer on a wall, part way up
    /// -- the pole grew with the height the pointer found ("the flagpole's height is being affected
    /// by the altitude"). A flag turned onto a new hoist line stands again where it stood the same
    /// way (<see cref="Base"/>, <see cref="Restands"/>). PURE.
    /// </summary>
    public static class FlagStand
    {
        /// <summary>
        /// A pole stands on its foot while its hoist runs within 60 degrees of straight down (the
        /// cosine: the hoist's downward share); nearer level it is a crossbar reaching out to a
        /// wall, and its length is found along it (FlagRenderer's cast).
        /// </summary>
        public const float UprightShare = 0.5f;

        /// <summary>A hoist running this way down (its y) is along a standing pole.</summary>
        public static bool Upright(float hoistY)
        {
            return -hoistY > UprightShare;
        }

        /// <summary>The flag has a pole that stands on its foot (its length the player's), rather than none or a crossbar.</summary>
        public static bool PoleStands(FlagSpec spec)
        {
            float hx, hy, hz, fx, fy, fz;
            FlagOrientation.Frame(0f, spec.Tilt, spec.Lean, spec.HoistedFrom, out hx, out hy, out hz, out fx, out fy, out fz);
            return spec.HasPole && Upright(hy);
        }

        /// <summary>
        /// The anchor (the top hoist corner) of a flag of <paramref name="spec"/> turned to
        /// <paramref name="heading"/> standing at (x, y, z): a standing pole
        /// <paramref name="poleLength"/> long has its foot there, and the anchor up its axis and
        /// its Offset out towards the fly (FlagPoleShape: the hoist hangs beside the pole); else the
        /// anchor is straight above it by the flag's own depth at rest -- its hoist's, or the
        /// length a banner hangs down from its crossbar.
        /// </summary>
        public static void Anchor(FlagSpec spec, float heading, float poleLength, float x, float y, float z,
                                  out float ax, out float ay, out float az)
        {
            float hx, hy, hz, fx, fy, fz;
            FlagOrientation.Frame(heading, spec.Tilt, spec.Lean, spec.HoistedFrom, out hx, out hy, out hz, out fx, out fy, out fz);
            if (spec.HasPole && Upright(hy))
            {
                FlagPoleShape shape = FlagPoleShape.For(spec, poleLength);
                ax = x - hx * shape.Length + fx * shape.Offset;
                ay = y - hy * shape.Length + fy * shape.Offset;
                az = z - hz * shape.Length + fz * shape.Offset;
                return;
            }

            ax = x;
            ay = y + spec.Height * Math.Max(0f, -hy) + spec.Length * Math.Max(0f, -fy);
            az = z;
        }

        /// <summary>
        /// Where a flag of <paramref name="spec"/> whose anchor is at (ax, ay, az) stands: the inverse
        /// of <see cref="Anchor"/> -- a standing pole's foot (the pole <paramref name="poleLength"/>
        /// long: where the renderer draws it), else the point straight under the anchor by the flag's
        /// depth at rest.
        /// </summary>
        public static void Base(FlagSpec spec, float heading, float poleLength, float ax, float ay, float az,
                                out float x, out float y, out float z)
        {
            float hx, hy, hz, fx, fy, fz;
            FlagOrientation.Frame(heading, spec.Tilt, spec.Lean, spec.HoistedFrom, out hx, out hy, out hz, out fx, out fy, out fz);
            if (spec.HasPole && Upright(hy))
            {
                FlagPoleShape shape = FlagPoleShape.For(spec, poleLength);
                x = ax + hx * shape.Length - fx * shape.Offset;
                y = ay + hy * shape.Length - fy * shape.Offset;
                z = az + hz * shape.Length - fz * shape.Offset;
                return;
            }

            x = ax;
            y = ay - (spec.Height * Math.Max(0f, -hy) + spec.Length * Math.Max(0f, -fy));
            z = az;
        }

        /// <summary>
        /// A flag with the mod's pole turns onto a new hoist line (the edge hoisted, its tilt or lean),
        /// from <paramref name="old"/> to <paramref name="spec"/>: it stands again where it stood, as
        /// the tool stands a new one (<see cref="Base"/> under the old, <see cref="Anchor"/> under the
        /// new), when a pole that stands is part of the change -- one that stood, its length known (its
        /// foot is where the flag stands), or one that will. A pole that stands keeps its height: only
        /// the Pole height changes it. A crossbar turned to another, or a pole whose length is still
        /// being found: the flag's top stays, the length found along the new line.
        /// </summary>
        /// <remarks>
        /// The first rule found the length again down every new line from where the flag's top was:
        /// switched to a banner, a 25 m pole became a 4 m crossbar with the banner left up where the
        /// pole's top had been, and turned down again (the gizmo's tilt ring) it reached the ground
        /// from there, 29 m long -- "it shrunk but the flagpole height remained the same ... the pole
        /// increased in height" (the author, 2026-09-29).
        /// </remarks>
        public static bool Restands(FlagSpec old, FlagSpec spec)
        {
            // Chosen, not drawn: a flag hoisted from its top has none, and comes down to stand on
            // its pole's foot, and back up it hoisted from its side again.
            if (!old.PoleChosen || !spec.PoleChosen)
                return false;
            return PoleStands(old) ? old.PoleLength > 0f : PoleStands(spec);
        }
    }

    /// <summary>
    /// The raise and lower keys on the flag being placed or the one selected (the game's
    /// elevation keys, Page Up and Page Down unless the player changed them -- Move It's own
    /// defaults too), as Move It moves its selection with them (MoveItTool.ProcessMoveKeys and
    /// OnToolGUI, their IL, Move It 2.10): the step is its magnitude 8 times its 0.015625 for
    /// heights, 0.125 m -- Shift x8 (1 m), Alt /8 (1.5625 cm), both /64 (2 mm); a press steps once
    /// at once, then, held, not again for <see cref="RepeatDelay"/> ms, then at every GUI event
    /// after (Layout and Repaint each frame, and each of the keyboard's repeats: 15-22 m/s at 60
    /// fps). Both keys at once cancel, and start the wait again. One per tool. PURE.
    /// </summary>
    public struct ElevationKeys
    {
        /// <summary>Metres a step without Shift or Alt.</summary>
        public const float Step = 0.125f;

        /// <summary>Held, the keys wait this long after the first step before they repeat, milliseconds.</summary>
        public const long RepeatDelay = 333;

        /// <summary>The Stopwatch time the keys were pressed (0: not held).</summary>
        private long _pressed;

        /// <summary>A step's metres with these modifiers held.</summary>
        public static float StepFor(bool shift, bool alt)
        {
            float magnitude = 8f;
            if (alt && shift)
            {
                magnitude /= 64f;
            }
            else
            {
                if (shift)
                    magnitude *= 8f;
                if (alt)
                    magnitude /= 8f;
            }

            return magnitude * 0.015625f;
        }

        /// <summary>
        /// One GUI event: how far the flag goes up (down: less than 0; 0: not now), given the keys
        /// held, the modifiers, and the time (Stopwatch ticks and their frequency).
        /// </summary>
        public float Event(bool up, bool down, bool shift, bool alt, long now, long frequency)
        {
            float step = StepFor(shift, alt);
            float dy = (up ? step : 0f) - (down ? step : 0f);
            if (dy == 0f)
            {
                _pressed = 0;
                return 0f;
            }

            if (_pressed == 0)
            {
                _pressed = now;
                return dy;
            }

            long elapsed = Math.Abs(now - _pressed) / Math.Max(1L, frequency / 1000L);
            return elapsed >= RepeatDelay ? dy : 0f;
        }

        /// <summary>The keys are held (a step, or waiting to repeat).</summary>
        public bool Held
        {
            get { return _pressed != 0; }
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

        /// <summary>The shaft's FlagPoleProfile; a sectional one's <see cref="Sections"/>.</summary>
        public int Profile;
        public int Sections;

        /// <summary>A fluted shaft's flutes, and how deep they are (a share of its radius).</summary>
        public const int Flutes = 16;
        public const float FluteDepth = 0.06f;

        /// <summary>An ornate pole's base: its widest radius and its height (the shaft starts on its top).</summary>
        public float BaseRadius, BaseHeight;

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

        /// <summary>
        /// The shaft's radius at a height above the foot: a straight taper (a straight pole's two
        /// radii are one), or a sectional pole's section there (<see cref="SectionRadius"/>). A
        /// fluted one's is to its ribs, the grooves within it.
        /// </summary>
        public float RadiusAt(float up)
        {
            float top = ShaftTop;
            if (Profile == FlagPoleProfile.Sectional && Sections > 1 && top > 0f)
                return SectionRadius(SectionAt(up));

            float t = top > 0f ? Math.Max(0f, Math.Min(up / top, 1f)) : 1f;
            return RadiusFoot + (RadiusTop - RadiusFoot) * t;
        }

        /// <summary>A sectional pole's section at a height (0 the lowest).</summary>
        public int SectionAt(float up)
        {
            float top = ShaftTop;
            if (Sections <= 1 || top <= 0f)
                return 0;
            int k = (int)Math.Floor(up / top * Sections);
            return Math.Max(0, Math.Min(k, Sections - 1));
        }

        /// <summary>Section <paramref name="k"/>'s radius: the foot's at the bottom, the top's at the top, in even steps.</summary>
        public float SectionRadius(int k)
        {
            if (Sections <= 1)
                return RadiusFoot;
            return RadiusFoot + (RadiusTop - RadiusFoot) * k / (Sections - 1f);
        }

        /// <summary>Where section <paramref name="k"/> begins above the foot (each the same length).</summary>
        public float SectionBottom(int k)
        {
            return Sections > 0 ? ShaftTop * k / Sections : 0f;
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
            s.Profile = FlagPoleProfile.IsKnown(spec.PoleProfile) ? spec.PoleProfile : FlagPoleProfile.Tapered;
            if (s.Profile == FlagPoleProfile.Straight)
            {
                // One width: a little less than a tapered pole's foot, as a straight tube of the same strength.
                s.RadiusFoot *= 0.9f;
                s.RadiusTop = s.RadiusFoot;
            }
            else if (s.Profile == FlagPoleProfile.Sectional)
            {
                // Telescoping and sectional poles: about one section every 4 m, 2 to 5 of them.
                s.Sections = Math.Max(2, Math.Min(5, 1 + (int)(h / 4f)));
            }
            else if (s.Profile == FlagPoleProfile.Fluted)
            {
                // A ceremonial column-like pole: a gentler taper.
                s.RadiusTop = s.RadiusFoot * Math.Max(0.6f, 0.85f - 0.004f * h);
            }

            s.TopAbove = Math.Max(0.04f, 0.03f * spec.Height);
            s.TruckRadius = s.RadiusTop * 1.35f;
            s.TruckHeight = s.TruckRadius * 1.1f;
            float ropes = Math.Max(FlagSpec.MinRopeThickness, Math.Min(float.IsNaN(spec.RopeThickness) ? 1f : spec.RopeThickness, FlagSpec.MaxRopeThickness));
            s.RopeRadius = Math.Max(0.004f, Math.Min(0.12f * s.RadiusTop, 0.03f)) * ropes;
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
            if (s.Profile == FlagPoleProfile.Ornate)
            {
                // A cast base: three times the foot's radius, about a tenth of the pole high.
                s.BaseRadius = 3f * s.RadiusFoot;
                s.BaseHeight = Math.Min(0.12f * s.Length, Math.Max(0.3f, 9f * s.RadiusFoot));
                s.Cleat = Math.Max(s.Cleat, s.BaseHeight + 0.2f);
            }

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
            if (Profile == FlagPoleProfile.Sectional && FlagHeight > 0f)
            {
                // Steps, not a cone: a cone through the section at the anchor and the one at the
                // hoist's foot holds the cloth off every step between (none of it is wider).
                float below = RadiusAt(Math.Max(0f, Length - FlagHeight));
                growth = Math.Max(0f, (below - RadiusAt(Length)) / FlagHeight);
            }

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
                   Math.Abs(FlagHeight - other.FlagHeight) < 0.001f && Math.Abs(RadiusFoot - other.RadiusFoot) < 0.0005f &&
                   Profile == other.Profile && Math.Abs(RopeRadius - other.RopeRadius) < 0.0002f;
        }
    }
}
