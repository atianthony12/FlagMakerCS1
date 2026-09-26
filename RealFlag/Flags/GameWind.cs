using System;
using ColossalFramework;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The game's own wind, as its weather keeps it: the flags' default (the author: "Wind should
    /// be by default based on the game's wind, and can be overridden in a 3rd tab, Settings").
    /// Main thread.
    /// </summary>
    /// <remarks>
    /// What the game has, read from its IL (2026-09-26), for there is no wind speed in metres
    /// per second anywhere in it:
    /// - a DIRECTION, WeatherManager.m_windDirection, degrees, which the simulation turns slowly
    ///   towards a new random one whenever it arrives (MoveTowardsAngle, speeding up by 0.001
    ///   degree a step); published to the shaders as _WindDirection = (sin a, 0, cos a), the way
    ///   the rain slants -- and Volumetric Weather's clouds and Volumetric Explosions' smoke
    ///   drift. (Its Unity WindZone is turned a + 180: the one reading the other way.)
    /// - a WIND MAP for its wind turbines (the Wind info view): 1 + 0.02 per metre above the
    ///   average height of the terrain and its trees over the 5 x 5 cells of 135 m round the
    ///   place (WindCell.m_totalHeight, CalculateTotalHeight), bilinear between cells
    ///   (SampleWindSpeed) -- hills windier, valleys and forests calmer;
    /// - the WEATHER's share of it (GetWindSpeedFactor): 1 + rain / 2 - fog / 2.
    /// So the game's wind here is a gentle breeze, <see cref="BaseSpeed"/> at 10 m over open
    /// level ground, times the weather's factor, times the map's at 10 m over the ground under
    /// the flag against open level ground's (1.2); FlagWind's own profile (EN 1991-1-4) takes it
    /// from 10 m to the flag's height, and its turbulence gives the gusts.
    /// </remarks>
    public static class GameWind
    {
        /// <summary>m/s at 10 m over open level ground in fair weather: Beaufort 3, "light flags extended".</summary>
        public const float BaseSpeed = 5f;

        /// <summary>The wind map's reach either way of open level ground's.</summary>
        public const float MinExposure = 0.5f, MaxExposure = 1.5f;

        /// <summary>WeatherManager.WINDGRID_RESOLUTION and WINDGRID_CELL_SIZE (the IL's constants).</summary>
        private const int Resolution = 128;
        private const float Cell = 135f;

        /// <summary>
        /// The game's wind now: the way it blows (unit, level), and its weather's factor with the
        /// rain and fog in it. False without a weather manager (the direction is then +x).
        /// </summary>
        public static bool Read(out Vector3 direction, out float weather, out float rain, out float fog)
        {
            direction = Vector3.right;
            weather = 1f;
            rain = 0f;
            fog = 0f;

            try
            {
                if (!Singleton<WeatherManager>.exists)
                    return false;

                WeatherManager manager = Singleton<WeatherManager>.instance;
                float a = manager.m_windDirection * Mathf.Deg2Rad;
                direction = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                rain = Mathf.Clamp01(manager.m_currentRain);
                fog = Mathf.Clamp01(manager.m_currentFog);
                weather = WeatherFactor(rain, fog);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's GetWindSpeedFactor: 1 + rain / 2 - fog / 2.</summary>
        public static float WeatherFactor(float rain, float fog)
        {
            return 1f + 0.5f * Mathf.Clamp01(rain) - 0.5f * Mathf.Clamp01(fog);
        }

        /// <summary>
        /// The wind map at 10 m over <paramref name="ground"/> (world height) at the flag's place,
        /// against open level ground's there: 1 on a plain, more on a hill, less in a valley or a
        /// forest; within <see cref="MinExposure"/>..<see cref="MaxExposure"/>. 1 without a map.
        /// </summary>
        public static float Exposure(Vector3 place, float ground)
        {
            try
            {
                if (!Singleton<WeatherManager>.exists)
                    return 1f;

                WeatherManager.WindCell[] grid = Singleton<WeatherManager>.instance.m_windGrid;
                if (grid == null || grid.Length < Resolution * Resolution)
                    return 1f;

                float map = 1f + 0.02f * (ground + 10f - AverageHeight(grid, place.x, place.z));
                return Mathf.Clamp(map / 1.2f, MinExposure, MaxExposure);
            }
            catch (Exception)
            {
                return 1f;
            }
        }

        /// <summary>The map's average height of terrain and trees round (x, z), metres: bilinear between its cells, as SampleWindSpeed reads it.</summary>
        private static float AverageHeight(WeatherManager.WindCell[] grid, float x, float z)
        {
            float gx = x / Cell + Resolution / 2 - 0.5f, gz = z / Cell + Resolution / 2 - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, Resolution - 1), z0 = Mathf.Clamp(Mathf.FloorToInt(gz), 0, Resolution - 1);
            int x1 = Mathf.Min(x0 + 1, Resolution - 1), z1 = Mathf.Min(z0 + 1, Resolution - 1);
            float tx = Mathf.Clamp01(gx - x0), tz = Mathf.Clamp01(gz - z0);

            float a = grid[z0 * Resolution + x0].m_totalHeight, b = grid[z0 * Resolution + x1].m_totalHeight;
            float c = grid[z1 * Resolution + x0].m_totalHeight, d = grid[z1 * Resolution + x1].m_totalHeight;
            float near = a + (b - a) * tx, far = c + (d - c) * tx;
            return (near + (far - near) * tz) / 64f;
        }
    }
}
