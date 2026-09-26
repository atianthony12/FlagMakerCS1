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

        /// <summary>The flag's clock, seconds, and how fast the eddies change while they pass (noise units per second).</summary>
        public float Time, Evolution;

        /// <summary>Where the flag's anchor is along and across the wind, metres (world): flags share one field.</summary>
        public float OriginAlong, OriginAcross;

        /// <summary>
        /// The wind at a point of the cloth, given relative to the anchor (world axes, metres),
        /// in m/s. The mean plus the eddies there.
        /// </summary>
        public void At(float x, float y, float z, out float ux, out float uy, out float uz)
        {
            float along = OriginAlong + x * DirX + z * DirZ;
            float a = (along - Carry * Time) * InvScale;
            float b = (y + OriginAcross * 0.37f) * InvScale;
            float c = Time * Evolution;

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
    }

    /// <summary>
    /// The wind over the city, as each flag feels it: the log-law profile with height and the
    /// turbulence of built-up ground, both from EN 1991-1-4 (Eurocode 1, wind actions, section 4),
    /// the ratios of the turbulence's three components from the atmospheric surface layer
    /// (Panofsky and Dutton 1984), and the inertial range for the eddies the size of a flag
    /// (Kolmogorov 1941: velocity differences over a distance r go as r^(1/3)).
    /// </summary>
    public static class FlagWind
    {
        /// <summary>
        /// Roughness length z0, metres: EN 1991-1-4 terrain category III, "regular cover of
        /// vegetation or buildings, or isolated obstacles" -- suburbs, villages, towns.
        /// </summary>
        public const float RoughnessLength = 0.3f;

        /// <summary>Below this the profile is held (the Eurocode's z_min for category III), metres.</summary>
        public const float MinimumHeight = 5f;

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
        /// of a tower (the log law: x1.8 at 200 m) would be 80 m/s, which the cloth is not built
        /// for (FlagCloth.MaxSubSteps is sized for this).
        /// </summary>
        public const float MaxSpeed = 40f;

        /// <summary>The mean speed at <paramref name="height"/> metres over the ground, from the speed at 10 m (the log law), at most <see cref="MaxSpeed"/>.</summary>
        public static float SpeedAt(float speed10, float height)
        {
            float z = Math.Max(height, MinimumHeight);
            float speed = speed10 * (float)(Math.Log(z / RoughnessLength) / Math.Log(ReferenceHeight / RoughnessLength));
            return Math.Min(speed, MaxSpeed);
        }

        /// <summary>Turbulence intensity sigma_u / U at a height (EN 1991-1-4 (4.7) with k_I = c_0 = 1).</summary>
        public static float Intensity(float height)
        {
            float z = Math.Max(height, MinimumHeight);
            return 1f / (float)Math.Log(z / RoughnessLength);
        }

        /// <summary>
        /// The turbulence's integral length scale at a height, metres (EN 1991-1-4 annex B:
        /// L = 300 (z / 200)^alpha, alpha = 0.67 + 0.05 ln z0): the size of the big gusts.
        /// </summary>
        public static float LengthScale(float height)
        {
            float z = Math.Max(height, MinimumHeight);
            double alpha = 0.67 + 0.05 * Math.Log(RoughnessLength);
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
            float speed = SpeedAt(Math.Max(speed10, 0f), height);
            float turbulence = Intensity(height) * speed;
            float sigma = turbulence * Math.Max(gusts, 0f);
            float scale = LengthScale(height);

            float along = anchorX * dirX + anchorZ * dirZ;
            float across = -anchorX * dirZ + anchorZ * dirX;

            // The big gusts: one value over the whole flag, carried at the mean speed, changing
            // as they pass (a quarter of the rate at which they are carried past).
            float a = (along - speed * time) / scale;
            float b = across / scale;
            float c = time * Math.Max(0.25f * speed / scale, 0.02f);
            float gustAlong = FlagNoise.Value3(a, b, c, 3) * sigma;
            float gustAcross = FlagNoise.Value3(a, b, c + 19.1f, 7) * sigma * AcrossRatio;
            float gustUp = FlagNoise.Value3(a, b, c + 37.9f, 13) * sigma * UpRatio;

            // A gust that turns the wind round would not: the along-wind speed is kept forward.
            float forward = Math.Max(speed + gustAlong, 0.05f * speed);

            var air = new FlagAir
            {
                MeanX = forward * dirX - gustAcross * dirZ,
                MeanY = gustUp,
                MeanZ = forward * dirZ + gustAcross * dirX,
                DirX = dirX,
                DirZ = dirZ,
                Carry = speed,
                Time = time,
                OriginAlong = along,
                OriginAcross = across,
            };

            // The eddies the size of the flag: the inertial range's r^(1/3) of the whole
            // turbulence (not of the Gusts setting, which is about the big slow ones), scaled by
            // the flag's own waviness.
            float eddy = Math.Max(flagLength, 0.5f);
            float small = turbulence * (float)Math.Pow(Math.Min(eddy / scale, 1f), 1.0 / 3.0);
            small = Math.Max(small, CalmStir) * Math.Max(waviness, 0f);

            air.SigmaAlong = small;
            air.SigmaAcross = small * AcrossRatio;
            air.SigmaUp = small * UpRatio;
            air.InvScale = 1f / eddy;
            air.Evolution = Math.Max(0.35f * speed / eddy, 0.25f);
            return air;
        }
    }
}
