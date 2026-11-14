using System;

namespace RealFlag.Flags
{
    /// <summary>
    /// The air one flag stands in, for one step of its cloth: the mean wind at the flag's height
    /// with the gust of the moment in it, and the eddies that vary across the cloth. PURE.
    /// </summary>
    /// <remarks>
    /// The eddies are frozen turbulence (Taylor 1938): a fixed field of noise carried past the
    /// flag at the mean wind speed, changing slowly as it goes. What the cloth feels at a point
    /// is the field at that point's place along the wind MINUS the distance the wind has carried
    /// the field, so a disturbance that reaches the hoist travels on down the flag -- the
    /// running ripples of a real flag -- and two flags a few metres apart along the wind feel it
    /// one after the other.
    /// </remarks>
    public struct FlagAir
    {
        /// <summary>The wind over the whole flag this step, gust included, world axes, m/s.</summary>
        public float MeanX, MeanY, MeanZ;

        /// <summary>The mean wind's direction in the horizontal (unit), and the speed the eddies are carried at, m/s.</summary>
        public float DirX, DirZ, Carry;

        /// <summary>The eddies' strength, m/s: along the wind, across it (horizontal), and up.</summary>
        public float SigmaAlong, SigmaAcross, SigmaUp;

        /// <summary>One over the eddies' size, 1/m.</summary>
        public float InvScale;

        /// <summary>
        /// Where the anchor is in the eddies' field this step, noise units: along the wind (less
        /// how far the wind has carried the field), across it, and how far the field has changed.
        /// </summary>
        public float EddyAlong, EddyAcross, EddyChange;

        /// <summary>
        /// The eddies of several sizes (2026-11-14, FlagWind.Air's varied air): how many octaves are
        /// read, 0 for the one octave as it always was (<see cref="EddyAlong"/> .. <see cref="InvScale"/>).
        /// Octave j is the flag's size / 2^j: where the anchor stands in its field (wrapped to the
        /// noise's period), one over its size, and its share of the eddies' strength (the inertial
        /// range's 2^(-j/3), faded out where the cloth's cells are too coarse for it, all of them
        /// normalised so the eddies' total strength is the one octave's).
        /// </summary>
        public int Octaves;
        public float Along0, Across0, Change0, Inv0, Share0;
        public float Along1, Across1, Change1, Inv1, Share1;
        public float Along2, Across2, Change2, Inv2, Share2;

        /// <summary>
        /// The wind at a point of the cloth, given relative to the anchor (world axes, metres),
        /// in m/s. The mean plus the eddies there.
        /// </summary>
        public void At(float x, float y, float z, out float ux, out float uy, out float uz)
        {
            if (Octaves > 0)
            {
                AtVaried(x, y, z, out ux, out uy, out uz);
                return;
            }

            float a = EddyAlong + (x * DirX + z * DirZ) * InvScale;
            float b = EddyAcross + y * InvScale;
            float c = EddyChange;

            float n1 = FlagNoise.Value3(a, b, c, 17);
            float n2 = FlagNoise.Value3(a, b, c + 41.3f, 59);
            float n3 = FlagNoise.Value3(a, b, c + 83.7f, 101);

            float along1 = n1 * SigmaAlong;
            float across1 = n2 * SigmaAcross;

            // Across the wind, in the horizontal: (-DirZ, DirX).
            ux = MeanX + along1 * DirX - across1 * DirZ;
            uy = MeanY + n3 * SigmaUp;
            uz = MeanZ + along1 * DirZ + across1 * DirX;
        }

        /// <summary>
        /// The same with the eddies of several sizes: each octave's three components from one
        /// lattice cell (FlagNoise.Value3x3), its own seeds and its own place in the field.
        /// </summary>
        private void AtVaried(float x, float y, float z, out float ux, out float uy, out float uz)
        {
            float along = x * DirX + z * DirZ;
            float n1, n2, n3;
            FlagNoise.Value3x3(Along0 + along * Inv0, Across0 + y * Inv0, Change0, 17, 59, 101, out n1, out n2, out n3);
            float s1 = n1 * Share0, s2 = n2 * Share0, s3 = n3 * Share0;
            if (Octaves > 1)
            {
                FlagNoise.Value3x3(Along1 + along * Inv1, Across1 + y * Inv1, Change1, 137, 179, 221, out n1, out n2, out n3);
                s1 += n1 * Share1; s2 += n2 * Share1; s3 += n3 * Share1;
            }
            if (Octaves > 2)
            {
                FlagNoise.Value3x3(Along2 + along * Inv2, Across2 + y * Inv2, Change2, 7, 49, 91, out n1, out n2, out n3);
                s1 += n1 * Share2; s2 += n2 * Share2; s3 += n3 * Share2;
            }

            float along1 = s1 * SigmaAlong;
            float across1 = s2 * SigmaAcross;
            ux = MeanX + along1 * DirX - across1 * DirZ;
            uy = MeanY + s3 * SigmaUp;
            uz = MeanZ + along1 * DirZ + across1 * DirX;
        }
    }

    /// <summary>
    /// The wind over the city, as each flag feels it: the mean speed growing with height by the
    /// power law of its terrain (Davenport 1960; the exponents of ASCE 7-95's exposures), the
    /// turbulence of that terrain from EN 1991-1-4 (Eurocode 1, wind actions, section 4), the
    /// ratios of the turbulence's three components from the atmospheric surface layer (Panofsky
    /// and Dutton 1984), and the inertial range for the eddies the size of a flag (Kolmogorov
    /// 1941: velocity differences over a distance r go as r^(1/3)).
    /// </summary>
    /// <remarks>
    /// THE TERRAIN is a roughness length z0 (<see cref="CityRoughness"/>: the buildings round the
    /// flag), from <see cref="RoughnessLength"/> (towns, EN category III, the default) to
    /// <see cref="CityCentre"/> (category IV). The panel's speed is the mean at 10 m over the
    /// ground WHERE THE FLAG IS; a rougher terrain makes the wind grow faster above that (city
    /// centres: U ~ z^(1/3); towns: z^(1/4)) and fade faster below it, and gustier throughout
    /// (Eurocode: I = 1 / ln(z / z0)). So a flag over a city centre's roofs flies in more wind than
    /// one as high over a suburb, and one down in its streets in less. Until 2026-10-30 this was the
    /// log law of category III everywhere, held below 5 m: the author's 16.9 m flag on a 37 m pole
    /// downtown got 1.38 the 10 m wind, a flag on a 3 m pole 0.80 ("flags that are hoisted on tall
    /// flagpoles or ... on top of buildings should almost always react to wind way stronger").
    /// THE VARIED AIR (2026-11-14): a live waving cloth's gusts and eddies come in several sizes
    /// each, the same energy (Field.VariedAir, ClothJob.Varied, Settings.VariedWind); everything
    /// else -- still poses, loops, the tests -- flies in the one size each, bit for bit as before.
    /// </remarks>
    public static class FlagWind
    {
        /// <summary>
        /// Roughness length z0, metres: EN 1991-1-4 terrain category III, "regular cover of
        /// vegetation or buildings, or isolated obstacles" -- suburbs, villages, towns. The least a
        /// flag is given: away from the city the wind is as it always was.
        /// </summary>
        public const float RoughnessLength = 0.3f;

        /// <summary>
        /// The roughest terrain, metres: EN 1991-1-4 category IV, "at least 15 % of the surface
        /// covered with buildings whose average height exceeds 15 m" (ASCE 7-95's exposure A, large
        /// city centres). Rougher than this the Eurocode does not go.
        /// </summary>
        public const float CityCentre = 1f;

        /// <summary>Below this the mean speed is held, metres (a flag lower than that is in someone's hand).</summary>
        public const float MinimumHeight = 2f;

        /// <summary>
        /// Roughness lengths and the power-law exponents of their terrains: EN 1991-1-4's categories
        /// II (open country), III (towns) and IV (city centres) against ASCE 7-95's exposures C
        /// (1/6.5), B (1/4) and A (1/3), the same terrains; interpolated in ln z0 between.
        /// </summary>
        private static readonly float[] Roughnesses = { 0.05f, 0.3f, 1f };
        private static readonly float[] Exponents = { 1f / 6.5f, 0.25f, 1f / 3f };

        /// <summary>EN 1991-1-4 table 4.1's z_min for the same terrains, metres: the turbulence is held below it.</summary>
        private static readonly float[] TurbulenceFloors = { 2f, 5f, 10f };

        /// <summary>
        /// The panel's wind speed is the mean speed at this height, metres: where weather
        /// stations measure it, and where the Beaufort scale's descriptions apply.
        /// </summary>
        public const float ReferenceHeight = 10f;

        /// <summary>sigma_v / sigma_u and sigma_w / sigma_u near the ground (Panofsky and Dutton 1984).</summary>
        public const float AcrossRatio = 0.75f;
        public const float UpRatio = 0.5f;

        /// <summary>
        /// The eddies' speed never drops below this, m/s x waviness: calm air is never perfectly
        /// still (Beaufort 0, "calm", is under 0.5 m/s), so a limp flag still stirs.
        /// </summary>
        public const float CalmStir = 0.15f;

        /// <summary>
        /// The most a flag's cloth is given, m/s. Hurricane force (Beaufort 12) begins at
        /// 32.7 m/s, and no flag flies in more; but the panel's 30 m/s in rain (x1.5) at the top
        /// of a tower (a city centre's power law: x2.7 at 200 m) would be 120 m/s, which the cloth
        /// is not built for (FlagCloth.MaxSubSteps is sized for this).
        /// </summary>
        public const float MaxSpeed = 40f;

        /// <summary>A roughness length held to the terrains the wind knows (<see cref="RoughnessLength"/>..<see cref="CityCentre"/>); 0 or less is the default.</summary>
        public static float Terrain(float roughness)
        {
            if (!(roughness > 0f))
                return RoughnessLength;
            return Math.Min(Math.Max(roughness, RoughnessLength), CityCentre);
        }

        /// <summary>
        /// The cloth's wind is the wind as it is up to this, m/s (Beaufort 7, "near gale", begins at
        /// 13.9); above it, it grows ever more slowly towards <see cref="ClothMost"/> (2026-11-03,
        /// the author: "we need to limit how much a flag cloth can move. At extreme winds it's super
        /// fast the way the cloth behaves" -- Volumetric Weather's storms and its Now tab reach 24 m/s
        /// at 10 m, 40 at a tower's flag in a gust). The look's limit, not the air's: the log's speeds
        /// and the wind's profile stay as they are.
        /// </summary>
        public const float ClothKnee = 14f;

        /// <summary>The most wind a cloth flies in, m/s (Beaufort 9, "strong gale", ends at 24.4).</summary>
        public const float ClothMost = 24f;

        /// <summary>
        /// The wind the cloth is given for a mean speed at the flag: itself up to <see
        /// cref="ClothKnee"/>, then softly limited (tanh: the slope stays 1 at the knee) below
        /// <see cref="ClothMost"/>. A gust over a strong wind still lifts the flag, ever less.
        /// </summary>
        public static float OnCloth(float speed)
        {
            if (!(speed > ClothKnee))
                return speed;
            float room = ClothMost - ClothKnee;
            return ClothKnee + room * (float)Math.Tanh((speed - ClothKnee) / room);
        }

        /// <summary>The cloth's mean speed at its height over its terrain: <see cref="SpeedAt(float, float, float)"/> softly limited (<see cref="OnCloth"/>).</summary>
        public static float ClothSpeed(float speed10, float height, float roughness)
        {
            return OnCloth(SpeedAt(speed10, height, roughness));
        }

        /// <summary>The same in a town.</summary>
        public static float ClothSpeed(float speed10, float height)
        {
            return OnCloth(SpeedAt(speed10, height));
        }

        /// <summary>
        /// How the flags follow a change of the wind's direction, in real seconds: most of the way
        /// in <see cref="TurnSeconds"/>, never faster than <see cref="TurnMost"/> degrees a second
        /// (2026-11-03, the author: "the wind direction changing mid way is too fast"). Volumetric
        /// Weather's trees turn so (TreeWind.Turn: 8 s, 12 deg/s) but on the city's clock -- four
        /// times as fast at the game's third speed -- while the flags' cloth runs on real time.
        /// </summary>
        public const float TurnSeconds = 10f;
        public const float TurnMost = 4f;

        /// <summary>
        /// How long the flags' wind takes to follow the speed of the moment, seconds: a first-order
        /// lag of 1.5 s passes what a 3 s running mean does (equal noise bandwidths, 1/(4 tau) =
        /// 1/(2 T)) -- the WMO's gust is the 3 s mean (WMO-No. 8, Guide to Instruments and Methods
        /// of Observation, ch. 5): shorter swings the cloth does not feel as one push.
        /// </summary>
        public const float FollowSeconds = 1.5f;

        /// <summary>
        /// The wind's direction (degrees, (sin a, 0, cos a), the game's convention) turned from
        /// <paramref name="from"/> towards <paramref name="to"/> over <paramref name="seconds"/>, the
        /// short way round (<see cref="TurnSeconds"/>, <see cref="TurnMost"/>), in 0..360.
        /// </summary>
        public static float Turn(float from, float to, float seconds)
        {
            if (!(seconds > 0f))
                return from;
            float diff = to - from;
            diff -= 360f * (float)Math.Floor((diff + 180f) / 360f);
            float turn = diff * (1f - (float)Math.Exp(-seconds / TurnSeconds));
            float most = TurnMost * seconds;
            float angle = from + Math.Max(-most, Math.Min(most, turn));
            return angle - 360f * (float)Math.Floor(angle / 360f);
        }

        /// <summary>A speed followed from <paramref name="from"/> towards <paramref name="to"/> over <paramref name="seconds"/> (<see cref="FollowSeconds"/>).</summary>
        public static float Follow(float from, float to, float seconds)
        {
            if (!(seconds > 0f))
                return from;
            return from + (to - from) * (1f - (float)Math.Exp(-seconds / FollowSeconds));
        }

        /// <summary>The mean speed's power-law exponent over a terrain (its roughness length, metres).</summary>
        public static float Exponent(float roughness)
        {
            return Interpolate(Exponents, Terrain(roughness));
        }

        /// <summary>The height the turbulence is held below over a terrain, metres (EN 1991-1-4's z_min).</summary>
        public static float TurbulenceFloor(float roughness)
        {
            return Interpolate(TurbulenceFloors, Terrain(roughness));
        }

        /// <summary>A terrain's value from the table over <see cref="Roughnesses"/>, linear in ln z0, held at its ends.</summary>
        private static float Interpolate(float[] values, float z0)
        {
            if (z0 <= Roughnesses[0])
                return values[0];
            for (int i = 1; i < Roughnesses.Length; i++)
            {
                if (z0 <= Roughnesses[i])
                {
                    double t = Math.Log(z0 / Roughnesses[i - 1]) / Math.Log(Roughnesses[i] / Roughnesses[i - 1]);
                    return (float)(values[i - 1] + (values[i] - values[i - 1]) * t);
                }
            }
            return values[values.Length - 1];
        }

        /// <summary>The mean speed at <paramref name="height"/> metres over the ground in a town, from the speed at 10 m, at most <see cref="MaxSpeed"/>.</summary>
        public static float SpeedAt(float speed10, float height)
        {
            return SpeedAt(speed10, height, RoughnessLength);
        }

        /// <summary>The same over a terrain of <paramref name="roughness"/> metres (the power law: the speed at 10 m there x (z / 10)^alpha).</summary>
        public static float SpeedAt(float speed10, float height, float roughness)
        {
            float z = Math.Max(height, MinimumHeight);
            float speed = speed10 * (float)Math.Pow(z / ReferenceHeight, Exponent(roughness));
            return Math.Min(speed, MaxSpeed);
        }

        /// <summary>Turbulence intensity sigma_u / U at a height in a town (EN 1991-1-4 (4.7) with k_I = c_0 = 1).</summary>
        public static float Intensity(float height)
        {
            return Intensity(height, RoughnessLength);
        }

        /// <summary>The same over a terrain of <paramref name="roughness"/> metres: I = 1 / ln(z / z0), held below its z_min.</summary>
        public static float Intensity(float height, float roughness)
        {
            float z0 = Terrain(roughness);
            float z = Math.Max(height, TurbulenceFloor(z0));
            return 1f / (float)Math.Log(z / z0);
        }

        /// <summary>
        /// The turbulence's integral length scale at a height in a town, metres (EN 1991-1-4 annex
        /// B: L = 300 (z / 200)^alpha, alpha = 0.67 + 0.05 ln z0): the size of the big gusts.
        /// </summary>
        public static float LengthScale(float height)
        {
            return LengthScale(height, RoughnessLength);
        }

        /// <summary>The same over a terrain of <paramref name="roughness"/> metres.</summary>
        public static float LengthScale(float height, float roughness)
        {
            float z0 = Terrain(roughness);
            float z = Math.Max(height, TurbulenceFloor(z0));
            double alpha = 0.67 + 0.05 * Math.Log(z0);
            return (float)(300.0 * Math.Pow(z / 200.0, alpha));
        }

        /// <summary>
        /// The air round one flag at one moment.
        /// </summary>
        /// <param name="speed10">The wind speed setting (at 10 m), m/s.</param>
        /// <param name="gusts">How gusty, 0..2: 1 is the Eurocode's turbulence for towns.</param>
        /// <param name="dirX">The wind's direction (where it blows TO), unit, horizontal.</param>
        /// <param name="height">The flag's height over the ground, metres.</param>
        /// <param name="waviness">The flag's own setting, 0..2: how much the eddies the size of the cloth stir it.</param>
        /// <param name="flagLength">The flag's length (hoist to fly), metres: the eddies that shape it are its size.</param>
        /// <param name="anchorX">The anchor's world position (x, z): flags share one field of gusts and eddies.</param>
        public static FlagAir Air(float speed10, float gusts, float dirX, float dirZ, float height, float waviness,
                                  float flagLength, float time, float anchorX, float anchorZ)
        {
            return Air(speed10, gusts, dirX, dirZ, height, waviness, flagLength, time, anchorX, anchorZ, RoughnessLength);
        }

        /// <summary>
        /// The same over a terrain of <paramref name="roughness"/> metres (the buildings round the
        /// flag: <see cref="CityRoughness"/>), at <paramref name="time"/> of a wind that has blown
        /// the same since 0: where the field is, is the time times its speed. For a wind that does
        /// not change (the tests, the preview); a flag in the city's wind takes the one with an
        /// <see cref="AirDrift"/>.
        /// </summary>
        public static FlagAir Air(float speed10, float gusts, float dirX, float dirZ, float height, float waviness,
                                  float flagLength, float time, float anchorX, float anchorZ, float roughness)
        {
            Field f = Field.Of(speed10, gusts, height, waviness, flagLength, roughness);
            float along = anchorX * dirX + anchorZ * dirZ;
            float across = -anchorX * dirZ + anchorZ * dirX;
            float carried = along - f.Speed * time;
            return f.Air(dirX, dirZ, 1f, carried / f.Scale, across / f.Scale, time * f.GustRate,
                         carried * f.InvEddy, across * 0.37f * f.InvEddy, time * f.Evolution);
        }

        /// <summary>
        /// The same, <paramref name="varied"/>: the gusts and the eddies of several sizes each
        /// (Field.VariedAir) for a cloth of <paramref name="cells"/> cells along its fly; not varied,
        /// exactly the one above. The field's places worked in double, as the drift keeps them.
        /// </summary>
        public static FlagAir Air(float speed10, float gusts, float dirX, float dirZ, float height, float waviness,
                                  float flagLength, float time, float anchorX, float anchorZ, float roughness, bool varied, int cells)
        {
            if (!varied)
                return Air(speed10, gusts, dirX, dirZ, height, waviness, flagLength, time, anchorX, anchorZ, roughness);

            Field f = Field.Of(speed10, gusts, height, waviness, flagLength, roughness);
            double along = (double)anchorX * dirX + (double)anchorZ * dirZ;
            double across = -(double)anchorX * dirZ + (double)anchorZ * dirX;
            double carried = along - (double)f.Speed * time;
            return f.VariedAir(dirX, dirZ, 1f, carried / f.Scale, across / f.Scale, (double)time * f.GustRate,
                               carried * f.InvEddy, across * 0.37 * f.InvEddy, (double)time * f.Evolution, cells);
        }

        /// <summary>
        /// The air round one flag for its next sub-step of <paramref name="step"/> seconds, from
        /// where its field has been carried to (<paramref name="drift"/>, then moved on by this
        /// sub-step's wind). The field moves at the wind of each moment, so a wind that changes
        /// -- Volumetric Weather's gusts passing, the weather, a turn of its direction -- carries
        /// it on smoothly (until 2026-11-03 the place was the clock times the speed of the moment:
        /// with Volumetric Weather's gusts changing the speed every frame, twenty minutes into a
        /// city a 0.05 m/s change threw the field 60 m, every eddy and gust a new one each frame).
        /// <paramref name="alongGusts"/>: the share of the big gusts along the wind kept (0 when the
        /// speed of the moment has its own gust in it: Volumetric Weather's).
        /// </summary>
        public static FlagAir Air(ref AirDrift drift, float step, float speed10, float gusts, float alongGusts, float dirX, float dirZ,
                                  float height, float waviness, float flagLength, float anchorX, float anchorZ, float roughness)
        {
            return Air(ref drift, step, speed10, gusts, alongGusts, dirX, dirZ, height, waviness, flagLength, anchorX, anchorZ, roughness, false, 0);
        }

        /// <summary>
        /// The same, <paramref name="varied"/>: the gusts and the eddies of several sizes each
        /// (Field.VariedAir) for a cloth of <paramref name="cells"/> cells along its fly (Columns - 1),
        /// from the same drift. Not varied, bit for bit the air as it was before 2026-11-14 (the
        /// still poses, the loops, the tests and the preview step so).
        /// </summary>
        public static FlagAir Air(ref AirDrift drift, float step, float speed10, float gusts, float alongGusts, float dirX, float dirZ,
                                  float height, float waviness, float flagLength, float anchorX, float anchorZ, float roughness,
                                  bool varied, int cells)
        {
            Field f = Field.Of(speed10, gusts, height, waviness, flagLength, roughness);
            if (!drift.Started)
            {
                // Where the anchor stands in the field: flags start in one field, by place.
                float along = anchorX * dirX + anchorZ * dirZ;
                float across = -anchorX * dirZ + anchorZ * dirX;
                drift.Started = true;
                drift.GustAlong = along / f.Scale;
                drift.GustAcross = across / f.Scale;
                drift.GustChange = 0.0;
                drift.EddyAlong = along * f.InvEddy;
                drift.EddyAcross = across * 0.37f * f.InvEddy;
                drift.EddyChange = 0.0;
            }

            FlagAir air = varied
                ? f.VariedAir(dirX, dirZ, alongGusts, drift.GustAlong, drift.GustAcross, drift.GustChange,
                              drift.EddyAlong, drift.EddyAcross, drift.EddyChange, cells)
                : f.Air(dirX, dirZ, alongGusts, (float)drift.GustAlong, drift.GustAcross, (float)drift.GustChange,
                        (float)drift.EddyAlong, drift.EddyAcross, (float)drift.EddyChange);

            drift.GustAlong -= (double)f.Speed / f.Scale * step;
            drift.GustChange += (double)f.GustRate * step;
            drift.EddyAlong -= (double)f.Speed * f.InvEddy * step;
            drift.EddyChange += (double)f.Evolution * step;
            return air;
        }

        /// <summary>The air's numbers at a flag for a wind of the moment: its speed, turbulence, scales and rates.</summary>
        private struct Field
        {
            public float Speed, Turbulence, Sigma, Scale, GustRate, Small, InvEddy, Evolution;

            public static Field Of(float speed10, float gusts, float height, float waviness, float flagLength, float roughness)
            {
                var f = new Field();
                f.Speed = ClothSpeed(Math.Max(speed10, 0f), height, roughness);
                f.Turbulence = Intensity(height, roughness) * f.Speed;
                f.Sigma = f.Turbulence * Math.Max(gusts, 0f);
                f.Scale = LengthScale(height, roughness);

                // The big gusts change as they pass at a quarter of the rate they are carried past.
                f.GustRate = Math.Max(0.25f * f.Speed / f.Scale, 0.02f);

                // The eddies the size of the flag: the inertial range's r^(1/3) of the whole
                // turbulence (not of the Gusts setting, which is about the big slow ones), scaled by
                // the flag's own waviness.
                float eddy = Math.Max(flagLength, 0.5f);
                float small = f.Turbulence * (float)Math.Pow(Math.Min(eddy / f.Scale, 1f), 1.0 / 3.0);
                f.Small = Math.Max(small, CalmStir) * Math.Max(waviness, 0f);
                f.InvEddy = 1f / eddy;
                f.Evolution = Math.Max(0.35f * f.Speed / eddy, 0.25f);
                return f;
            }

            /// <summary>The air with the big gusts at (a, b, c) of their field and the anchor at (ea, eb, ec) of the eddies'.</summary>
            public FlagAir Air(float dirX, float dirZ, float alongGusts, float a, float b, float c, float ea, float eb, float ec)
            {
                // The big gusts: one value over the whole flag.
                float gustAlong = FlagNoise.Value3(a, b, c, 3) * Sigma * Math.Max(alongGusts, 0f);
                float gustAcross = FlagNoise.Value3(a, b, c + 19.1f, 7) * Sigma * AcrossRatio;
                float gustUp = FlagNoise.Value3(a, b, c + 37.9f, 13) * Sigma * UpRatio;

                // A gust that turns the wind round would not: the along-wind speed is kept forward.
                float forward = Math.Max(Speed + gustAlong, 0.05f * Speed);

                return new FlagAir
                {
                    MeanX = forward * dirX - gustAcross * dirZ,
                    MeanY = gustUp,
                    MeanZ = forward * dirZ + gustAcross * dirX,
                    DirX = dirX,
                    DirZ = dirZ,
                    Carry = Speed,
                    SigmaAlong = Small,
                    SigmaAcross = Small * AcrossRatio,
                    SigmaUp = Small * UpRatio,
                    InvScale = InvEddy,
                    EddyAlong = ea,
                    EddyAcross = eb,
                    EddyChange = ec,
                };
            }

            /// <summary>
            /// The varied air (2026-11-14): the same energy shared over more sizes. With the big
            /// gusts and the flag's own eddies alone (one octave of noise each), between ~0.5 s and
            /// ~8 s the wind hardly changed -- measured on a 3 m flag at 12 m in 5.6 m/s: gusts of
            /// 1.4 m/s rms over 8 s, 0.3 at 2-8 s, 0.02 at 0.5-2 s, the eddies near 0.5 s -- so a
            /// flag held one posture for many seconds with a steady ripple on it. Real turbulence
            /// has energy all through that range (Kolmogorov 1941's inertial range; Kaimal et al.
            /// 1972's surface-layer spectra), so octaves are summed (Perlin 1985) as wind
            /// generators do:
            /// - THE GUSTS: three more octaves k = 1..3 of the gusts' size / 2^k, read from the same
            ///   drift scaled by 2^k (carried at the same wind, changing 2^k times as fast), each its
            ///   own seeds and place; weights 2^(-k/3) (velocity differences over r go as r^(1/3)),
            ///   faded out (<see cref="Fade"/>) where an octave comes within twice the flag's size
            ///   (those sizes are the eddies' on the cloth); all normalised, so the gusts' total
            ///   variance is the one octave's. Along the wind the big gust is kept as alongGusts says
            ///   (none with Volumetric Weather, whose own gust is in the speed); octaves 1-3 always,
            ///   being smaller than its 64 m gust cells.
            /// - THE EDDIES: octaves j = 0..2 of the flag's size / 2^j, weights 2^(-j/3), each faded
            ///   in by the cloth's cells it spans (<see cref="Fade"/> of cells / 2: none at 2 cells,
            ///   whole from 4), normalised as the gusts are.
            /// </summary>
            public FlagAir VariedAir(float dirX, float dirZ, float alongGusts, double ga, double gb, double gc,
                                     double ea, double eb, double ec, int cells)
            {
                // ---- the gusts: the big one and three smaller, one value each over the whole flag ----
                float w1 = GustWeights[1] * Fade(Scale * 0.5f * InvEddy);
                float w2 = GustWeights[2] * Fade(Scale * 0.25f * InvEddy);
                float w3 = GustWeights[3] * Fade(Scale * 0.125f * InvEddy);
                float norm = 1f / (float)Math.Sqrt(1f + w1 * w1 + w2 * w2 + w3 * w3);

                float a = FlagNoise.Wrap(ga), b = FlagNoise.Wrap(gb), c = FlagNoise.Wrap(gc);
                float along = FlagNoise.Value3(a, b, c, 3) * Math.Max(alongGusts, 0f);
                float acrossG = FlagNoise.Value3(a, b, c + 19.1f, 7);
                float up = FlagNoise.Value3(a, b, c + 37.9f, 13);

                float n1, n2, n3;
                if (w1 > 0f)
                {
                    FlagNoise.Value3x3(FlagNoise.Wrap(ga * 2.0 + 97.31), FlagNoise.Wrap(gb * 2.0 + 53.77), FlagNoise.Wrap(gc * 2.0 + 131.59),
                                       29, 97, 163, out n1, out n2, out n3);
                    along += n1 * w1; acrossG += n2 * w1; up += n3 * w1;
                }
                if (w2 > 0f)
                {
                    FlagNoise.Value3x3(FlagNoise.Wrap(ga * 4.0 + 181.13), FlagNoise.Wrap(gb * 4.0 + 211.47), FlagNoise.Wrap(gc * 4.0 + 23.89),
                                       41, 109, 179, out n1, out n2, out n3);
                    along += n1 * w2; acrossG += n2 * w2; up += n3 * w2;
                }
                if (w3 > 0f)
                {
                    FlagNoise.Value3x3(FlagNoise.Wrap(ga * 8.0 + 37.61), FlagNoise.Wrap(gb * 8.0 + 149.29), FlagNoise.Wrap(gc * 8.0 + 227.03),
                                       53, 121, 197, out n1, out n2, out n3);
                    along += n1 * w3; acrossG += n2 * w3; up += n3 * w3;
                }

                float gustAlong = along * norm * Sigma;
                float gustAcross = acrossG * norm * Sigma * AcrossRatio;
                float gustUp = up * norm * Sigma * UpRatio;
                float forward = Math.Max(Speed + gustAlong, 0.05f * Speed);

                // ---- the eddies: octaves of the flag's size, a half and a quarter of it ----
                float e1 = EddyWeights[1] * Fade(cells * 0.25f);
                float e2 = EddyWeights[2] * Fade(cells * 0.125f);
                float enorm = 1f / (float)Math.Sqrt(1f + e1 * e1 + e2 * e2);

                return new FlagAir
                {
                    MeanX = forward * dirX - gustAcross * dirZ,
                    MeanY = gustUp,
                    MeanZ = forward * dirZ + gustAcross * dirX,
                    DirX = dirX,
                    DirZ = dirZ,
                    Carry = Speed,
                    SigmaAlong = Small,
                    SigmaAcross = Small * AcrossRatio,
                    SigmaUp = Small * UpRatio,
                    InvScale = InvEddy,
                    EddyAlong = (float)ea,
                    EddyAcross = (float)eb,
                    EddyChange = (float)ec,
                    Octaves = e2 > 0f ? 3 : e1 > 0f ? 2 : 1,
                    Along0 = FlagNoise.Wrap(ea),
                    Across0 = FlagNoise.Wrap(eb),
                    Change0 = FlagNoise.Wrap(ec),
                    Inv0 = InvEddy,
                    Share0 = enorm,
                    Along1 = FlagNoise.Wrap(ea * 2.0 + 71.37),
                    Across1 = FlagNoise.Wrap(eb * 2.0 + 113.91),
                    Change1 = FlagNoise.Wrap(ec * 2.0 + 47.23),
                    Inv1 = InvEddy * 2f,
                    Share1 = e1 * enorm,
                    Along2 = FlagNoise.Wrap(ea * 4.0 + 163.71),
                    Across2 = FlagNoise.Wrap(eb * 4.0 + 29.53),
                    Change2 = FlagNoise.Wrap(ec * 4.0 + 191.17),
                    Inv2 = InvEddy * 4f,
                    Share2 = e2 * enorm,
                };
            }
        }

        /// <summary>The varied air's octave weights, 2^(-k/3) for k = 0..3 (Kolmogorov: velocity differences over r go as r^(1/3)).</summary>
        private static readonly float[] GustWeights = { 1f, 0.7937005f, 0.6299605f, 0.5f };
        private static readonly float[] EddyWeights = { 1f, 0.7937005f, 0.6299605f };

        /// <summary>The varied air's fade, 0 at q = 1, whole from q = 2, linear between: an octave comes in smoothly as a flag grows or its cloth gets finer.</summary>
        private static float Fade(float q)
        {
            return Math.Min(Math.Max(q - 1f, 0f), 1f);
        }
    }

    /// <summary>
    /// Where one cloth's air has carried its field of gusts and eddies to (FlagWind.Air), noise
    /// units, kept with the cloth and moved on each sub-step by the wind of that sub-step. Unset,
    /// the next sub-step starts it where the anchor stands. PURE.
    /// </summary>
    public struct AirDrift
    {
        public bool Started;
        public double GustAlong, GustChange, EddyAlong, EddyChange;
        public float GustAcross, EddyAcross;
    }

    /// <summary>
    /// The roughness of the city round a flag, from its buildings: Lettau's (1969) z0 = 0.5 h
    /// lambda_f -- h their mean height, lambda_f their frontal area over the ground's -- the simplest
    /// of the morphometric rules Grimmond and Oke (1999) review, held to the terrains FlagWind knows
    /// (EN 1991-1-4 categories III to IV: Lettau's rule overshoots where the buildings stand close).
    /// A building's frontal area is its height x its plan's mean width over every wind direction
    /// (Cauchy: a convex plan's mean width is its perimeter / pi). PURE.
    /// </summary>
    public struct CityRoughness
    {
        /// <summary>
        /// How far round a flag its buildings count, metres: the wind at a few tens of metres has
        /// come over the last few hundred (an internal boundary layer grows about a tenth as fast
        /// as it goes).
        /// </summary>
        public const float Reach = 200f;

        /// <summary>The buildings counted, their plans, frontal areas and volumes added up (m^2, m^2, m^3).</summary>
        public int Buildings;
        public double Plan, Frontal, Volume;

        /// <summary>One building: its plan's width and depth and its height, metres.</summary>
        public void Add(float width, float depth, float height)
        {
            if (!(width > 0f) || !(depth > 0f) || !(height > 0f))
                return;

            double plan = (double)width * depth;
            Buildings++;
            Plan += plan;
            Frontal += height * 2.0 * (width + depth) / Math.PI;
            Volume += plan * height;
        }

        /// <summary>The ground they stand on, m^2: the disc of <see cref="Reach"/>.</summary>
        public static double Ground
        {
            get { return Math.PI * Reach * Reach; }
        }

        /// <summary>The share of the ground built on (lambda_p).</summary>
        public float PlanShare
        {
            get { return (float)(Plan / Ground); }
        }

        /// <summary>The frontal area over the ground's (lambda_f).</summary>
        public float FrontalShare
        {
            get { return (float)(Frontal / Ground); }
        }

        /// <summary>Their height, metres, weighted by their plans.</summary>
        public float MeanHeight
        {
            get { return Plan > 0.0 ? (float)(Volume / Plan) : 0f; }
        }

        /// <summary>The terrain's roughness length, metres (Lettau), within FlagWind's terrains.</summary>
        public float Roughness
        {
            get { return FlagWind.Terrain(Math.Max(0.5f * MeanHeight * FrontalShare, FlagWind.RoughnessLength)); }
        }
    }
}
