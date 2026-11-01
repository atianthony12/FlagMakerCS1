using System;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The props that carry the flags: reading them, and changing them the way the game's own
    /// tools do -- on the simulation thread (SimulationManager.AddAction), through PropManager.
    /// </summary>
    /// <remarks>
    /// Read from the game's IL (2026-09-25):
    /// - PropManager.CreateProp sets Created, the info, Single, the position (y included) and the
    ///   angle, then UpdateProp; PropInstance.CalculateProp, on the next update, puts a prop on
    ///   the ground UNLESS it has FixedHeight -- so every flag prop gets FixedHeight at once, in
    ///   the same action (Move It's MoveableProp keeps it, and sets it when it raises a prop).
    /// - Positions are quantised: x and z to 0.26 m in a game (PropInstance.set_Position,
    ///   3.79 steps per metre), y to 1/64 m; the angle to 1/10430 of a radian.
    /// - A prop's angle turns its mesh by Quaternion.AngleAxis(angle, Vector3.down)
    ///   (PropInstance.RenderInstance): local +x goes to (cos a, 0, sin a). A flag points that way.
    /// </remarks>
    public static class FlagProps
    {
        /// <summary>The direction (horizontal, unit) a flag with this prop angle points: local +x turned as the game turns a prop.</summary>
        public static Vector3 Direction(float angle)
        {
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        /// <summary>True if this prop exists and is a flag.</summary>
        public static bool IsFlagProp(uint prop)
        {
            try
            {
                PropInstance[] buffer = Singleton<PropManager>.instance.m_props.m_buffer;
                if (prop == 0 || prop >= buffer.Length)
                    return false;

                return (buffer[prop].m_flags & (ushort)PropInstance.Flags.Created) != 0 &&
                       (buffer[prop].m_flags & (ushort)PropInstance.Flags.Deleted) == 0 &&
                       FlagPrefab.IsFlagIndex(buffer[prop].m_infoIndex);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The flag's anchor (its prop's position: the top hoist corner) and angle, if the prop is still a flag.</summary>
        public static bool TryRead(uint prop, out Vector3 anchor, out float angle, out bool fixedHeight)
        {
            anchor = Vector3.zero;
            angle = 0f;
            fixedHeight = false;

            if (!IsFlagProp(prop))
                return false;

            PropInstance[] buffer = Singleton<PropManager>.instance.m_props.m_buffer;
            anchor = buffer[prop].Position;
            angle = buffer[prop].Angle;
            fixedHeight = buffer[prop].FixedHeight;
            return true;
        }

        /// <summary>
        /// A flag prop held at its height (FixedHeight): its m_posY (1/64 m), which the game does not
        /// save (FlagHeightCodec) -- FlagSaveData does.
        /// </summary>
        public static bool TryGetHeight(uint prop, out ushort posY)
        {
            posY = 0;
            if (!IsFlagProp(prop))
                return false;

            PropInstance[] buffer = Singleton<PropManager>.instance.m_props.m_buffer;
            if (!buffer[prop].FixedHeight)
                return false;

            posY = buffer[prop].m_posY;
            return true;
        }

        /// <summary>
        /// A flag prop's height as it loads (m_posY, 1/64 m), written straight into the buffer: only
        /// while the city loads (FlagSaveData.OnLoadData, Loader.OnLevelLoaded), before anything has
        /// read where the flag is. A flag has no renderer and nothing of the game follows its height.
        /// </summary>
        public static bool SetLoadedHeight(uint prop, ushort posY)
        {
            if (!IsFlagProp(prop))
                return false;

            PropInstance[] buffer = Singleton<PropManager>.instance.m_props.m_buffer;
            buffer[prop].FixedHeight = true;
            buffer[prop].m_posY = posY;
            return true;
        }

        /// <summary>Metres to m_posY's 1/64 m, held to its range (0 to 1024 m) as PropInstance.CalculateProp holds it.</summary>
        public static ushort PosY(float metres)
        {
            return (ushort)Mathf.Clamp(Mathf.RoundToInt(metres * 64f), 0, 65535);
        }

        /// <summary>The ground or water under a point, world metres (the Vector3 overload: world units in, metres out).</summary>
        public static float GroundAt(Vector3 at)
        {
            try
            {
                return Singleton<TerrainManager>.instance.SampleRawHeightSmoothWithWater(at, false, 0f);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>
        /// A flag prop with its anchor at <paramref name="anchor"/>, turned to <paramref name="angle"/>,
        /// its design set as soon as it exists. On the simulation thread; <paramref name="done"/>
        /// is told the prop's index there (0: the city is full).
        /// </summary>
        public static void Create(Vector3 anchor, float angle, FlagSpec spec, Action<uint> done)
        {
            PropInfo info = FlagPrefab.Info;
            if (info == null)
            {
                Log.Warn("flags: cannot place a flag, the flag prefab was not made in this city (see the start of the log)");
                return;
            }

            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                uint made = 0;
                try
                {
                    PropManager props = Singleton<PropManager>.instance;
                    ushort prop;
                    Randomizer randomizer = Singleton<SimulationManager>.instance.m_randomizer;
                    if (props.CreateProp(out prop, ref randomizer, info, anchor, angle, true))
                    {
                        props.m_props.m_buffer[prop].FixedHeight = true;
                        FlagRegistry.Set(prop, spec);
                        made = prop;
                    }
                }
                catch (Exception e)
                {
                    Log.Error("flags: placing a flag threw.", e);
                }

                if (done != null)
                    done(made);
            });
        }

        /// <summary>Moves a flag's anchor (keeping it where it is put: fixed height).</summary>
        public static void Move(uint prop, Vector3 anchor)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                if (!IsFlagProp(prop))
                    return;

                PropManager props = Singleton<PropManager>.instance;
                props.m_props.m_buffer[prop].FixedHeight = true;
                props.MoveProp((ushort)prop, anchor);
                props.UpdatePropRenderer((ushort)prop, true);
            });
        }

        /// <summary>Turns a flag.</summary>
        public static void Rotate(uint prop, float angle)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                if (!IsFlagProp(prop))
                    return;

                PropManager props = Singleton<PropManager>.instance;
                props.m_props.m_buffer[prop].Angle = angle;
                props.UpdatePropRenderer((ushort)prop, true);
            });
        }

        /// <summary>Removes a flag's prop (its design goes when the renderer's scan finds it gone).</summary>
        public static void Delete(uint prop)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                if (!IsFlagProp(prop))
                    return;

                Singleton<PropManager>.instance.ReleaseProp((ushort)prop);
            });
        }

        /// <summary>Makes a flag prop keep its height (one placed by another tool sits on the ground and would bury the flag).</summary>
        public static void FixHeight(uint prop, float height)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                if (!IsFlagProp(prop))
                    return;

                PropManager props = Singleton<PropManager>.instance;
                if (props.m_props.m_buffer[prop].FixedHeight)
                    return;

                Vector3 p = props.m_props.m_buffer[prop].Position;
                props.m_props.m_buffer[prop].FixedHeight = true;
                props.MoveProp((ushort)prop, new Vector3(p.x, p.y + height, p.z));
            });
        }
    }
}
