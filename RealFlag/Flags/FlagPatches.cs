using ColossalFramework.Math;
using HarmonyLib;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// A flag is picked by its cloth. The game picks a prop with PropInstance.RayCast, a thin
    /// vertical cylinder rising from the prop's position and sized by its prefab (read from the
    /// IL) -- for a flag, whose position is its top hoist corner, a spot above the flag. Move It
    /// (PropsManager.RaycastHoverInstance), the bulldozer and the game's own tools all call it, so
    /// this one prefix makes every one of them find the flag where it is drawn.
    /// </summary>
    [HarmonyPatch(typeof(PropInstance), "RayCast")]
    public static class FlagRayCastPatch
    {
        public static bool Prefix(ref PropInstance __instance, ushort propID, Segment3 ray, out float t, out float targetSqr, ref bool __result)
        {
            t = 2f;
            targetSqr = 0f;

            if (!FlagPrefab.IsFlagIndex(__instance.m_infoIndex))
                return true;

            // No renderer (the mod stood down in this city: nothing drawn, no pick volume): the
            // game's own test, so an unseen flag can still be found by the bulldozer and Move It.
            if (FlagRenderer.Instance == null)
                return true;

            __result = FlagPicking.RayCast(propID, ray, out t, out targetSqr);
            return false;
        }
    }

    /// <summary>
    /// A flag's outline where Move It (and the bulldozer) highlight a prop: the game's
    /// PropTool.RenderOverlay draws a disc the prefab's width round the prop's position, which for
    /// a flag is a small ring at its top corner. This draws it round everywhere the flag's cloth
    /// can reach, and its pole, instead (FlagOutline: from its place and size, still while it waves).
    /// </summary>
    [HarmonyPatch(typeof(PropTool), "RenderOverlay",
                  new[] { typeof(RenderManager.CameraInfo), typeof(PropInfo), typeof(Vector3), typeof(float), typeof(float), typeof(Color) })]
    public static class FlagOverlayPatch
    {
        public static bool Prefix(RenderManager.CameraInfo cameraInfo, PropInfo info, Vector3 position, float scale, float angle, Color color)
        {
            if (!FlagPrefab.IsFlag(info))
                return true;

            FlagRenderer renderer = FlagRenderer.Instance;
            FlagOutline outline;
            if (renderer == null || !renderer.TryFindAt(position, out outline))
                return true;

            FlagRenderer.Outline(cameraInfo, outline, color);
            return false;
        }
    }
}
