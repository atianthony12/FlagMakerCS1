using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The sky a glossy pole reflects (FlagPole.shader's _RFSky, _RFSkyInfo), chosen each frame:
    /// Real Glass's live sky when that mod draws one -- its "_RGSky", a cube of the game's own skybox
    /// with Volumetric Weather's clouds in it, following the time of day (Real Glass's GlassSky.cs) --
    /// else the game's environment map (_EnvironmentCubemap), dimmed as the game dims its own day sky
    /// (Real Glass's GameSkyDay, read from DayNightProperties' IL there), since that map is fixed.
    /// Engine code (Shader globals), shared with the GPU preview. Nothing of Real Glass is
    /// referenced (Invariant 11): its sky is a shader global, read by name.
    /// </summary>
    public static class PoleSky
    {
        private static readonly int IdSky = Shader.PropertyToID("_RFSky");
        private static readonly int IdInfo = Shader.PropertyToID("_RFSkyInfo");
        private static readonly int IdRealGlass = Shader.PropertyToID("_RGSky");
        private static readonly int IdGame = Shader.PropertyToID("_EnvironmentCubemap");

        /// <summary>Which sky was bound last: 0 none, 1 the game's map, 2 Real Glass's live sky (for the log).</summary>
        public static int Source { get; private set; }

        /// <summary>Binds the sky for this frame; <paramref name="sunY"/> the sun's direction's height (-1..1).</summary>
        public static void Bind(float sunY)
        {
            Texture sky = null;
            float gain = 0f;
            int source = 0;

            Texture glass = Shader.GetGlobalTexture(IdRealGlass);
            if (glass != null && glass.dimension == UnityEngine.Rendering.TextureDimension.Cube)
            {
                sky = glass;
                source = 2;
                // Its live cube is the sky as it is now; the game's map bound under its name is not.
                gain = glass is RenderTexture ? 1f : GameSkyDay(sunY);
            }
            else
            {
                Texture game = Shader.GetGlobalTexture(IdGame);
                if (game != null && game.dimension == UnityEngine.Rendering.TextureDimension.Cube)
                {
                    sky = game;
                    source = 1;
                    gain = GameSkyDay(sunY);
                }
            }

            if (source != Source)
            {
                Source = source;
                Log.Msg("poles: a glossy pole reflects " + (source == 2 ? "Real Glass's sky" : source == 1 ? "the game's environment map" : "no sky (none found)"));
            }

            if (sky != null)
                Shader.SetGlobalTexture(IdSky, sky);
            Shader.SetGlobalVector(IdInfo, new Vector4(Mathf.Max(Mips(sky) - 1, 0), gain, sky != null ? 1f : 0f, 0f));
        }

        private static int Mips(Texture t)
        {
            Cubemap cube = t as Cubemap;
            if (cube != null)
                return cube.mipmapCount;
            RenderTexture rt = t as RenderTexture;
            if (rt != null && rt.useMipMap)
            {
                int mips = 1;
                for (int s = rt.width; s > 1; s >>= 1)
                    mips++;
                return mips;
            }

            return 1;
        }

        /// <summary>
        /// The game's own sky by the sun's height, as a share of its sky by day: DayNightProperties'
        /// DayTime, clamp01(atan(5.35 max(sun y, -0.1975)) / 1.1 + 0.739) (Real Glass's GameSkyDay,
        /// from the IL): 1 with the sun above 3 degrees, 0.74 at the horizon, 0 near -11.
        /// </summary>
        public static float GameSkyDay(float sunY)
        {
            return Mathf.Clamp01(Mathf.Atan(5.35f * Mathf.Max(sunY, -0.1975f)) / 1.1f + 0.739f);
        }
    }
}
