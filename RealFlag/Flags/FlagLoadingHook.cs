using System;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The mod's place in the game's list of renderable managers (RenderManager.m_renderables,
    /// the list the prop, tree, building and network managers are in), for two calls it makes:
    /// CheckReferences, which is when the flag prefab is made, and EndOverlay, which is when the
    /// hovered flag is outlined. Everything else is a no-op.
    /// </summary>
    /// <remarks>
    /// Why CheckReferences (LoadingManager's LoadLevelCoroutine, read from the IL 2026-09-25):
    /// after the prefab scenes are loaded it queues, on the main thread's loading queue,
    ///   QueueLoadingAction(LoadCustomContent());      // Workshop and local assets
    ///   RenderManager.Managers_CheckReferences();      // every renderable's CheckReferences, NOW
    ///   QueueLoadingAction(EssentialScenesLoaded());   // lets the simulation thread go on to
    ///                                                  // PropManager's AfterDeserialize, which
    ///                                                  // binds the save's props to prefabs
    /// so an action queued from CheckReferences runs after every asset is loaded and before any
    /// prop is bound: the one moment the flag prefab can join the collection and have the city's
    /// saved flags find it. No Harmony, only the game's own public RegisterRenderableManager.
    /// (A city loaded from inside another skips all three; its prefabs, ours included, stay.)
    /// </remarks>
    public sealed class FlagLoadingHook : IRenderableManager
    {
        private static FlagLoadingHook _registered;

        /// <summary>Joins the list; once per session (Mod.OnEnabled, before any city loads).</summary>
        public static void Register()
        {
            if (_registered != null)
                return;

            try
            {
                _registered = new FlagLoadingHook();
                RenderManager.RegisterRenderableManager(_registered);

                IRenderableManager[] managers;
                int count;
                RenderManager.GetManagers(out managers, out count);
                Log.Msg("loading hook: registered with the render manager (" + count + " renderables)");
            }
            catch (Exception e)
            {
                _registered = null;
                Log.Error("loading hook: could not register; saved flags may not load.", e);
            }
        }

        /// <summary>
        /// Leaves the list (Mod.OnDisabled): a disabled or reloaded mod leaves nothing of it
        /// behind. The list (RenderManager.m_renderables) is private; the game has no call to leave it.
        /// </summary>
        public static void Unregister()
        {
            if (_registered == null)
                return;

            try
            {
                FieldInfo field = typeof(RenderManager).GetField("m_renderables", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                var list = field != null ? field.GetValue(null) as FastList<IRenderableManager> : null;
                if (list != null)
                    list.Remove(_registered);
                else
                    Log.Warn("loading hook: the render manager's list was not found; the hook stays (harmless: it only queues the prefab)");
            }
            catch (Exception e)
            {
                Log.Warn("loading hook: could not leave the render manager's list: " + e.Message);
            }

            _registered = null;
            FlagPrefab.UnhookLocale();
        }

        public void CheckReferences()
        {
            try
            {
                Singleton<LoadingManager>.instance.QueueLoadingAction(FlagPrefab.CreateDuringLoading());
                Singleton<LoadingManager>.instance.QueueLoadingAction(StringPrefab.CreateDuringLoading());
                Log.Msg("loading hook: the flag prefab is queued behind the custom assets, ahead of binding the city's props");
            }
            catch (Exception e)
            {
                Log.Error("loading hook: could not queue the flag prefab.", e);
            }
        }

        public void EndOverlay(RenderManager.CameraInfo cameraInfo)
        {
            try
            {
                FlagRenderer renderer = FlagRenderer.Instance;
                if (renderer != null)
                    renderer.RenderOverlay(cameraInfo);
            }
            catch (Exception e)
            {
                Log.Detail("overlay: " + e.Message);
            }
        }

        public string GetName()
        {
            return "RealFlag";
        }

        public DrawCallData GetDrawCallData()
        {
            return default(DrawCallData);
        }

        public void BeginRendering(RenderManager.CameraInfo cameraInfo)
        {
        }

        public void EndRendering(RenderManager.CameraInfo cameraInfo)
        {
        }

        public void BeginOverlay(RenderManager.CameraInfo cameraInfo)
        {
        }

        public void UndergroundOverlay(RenderManager.CameraInfo cameraInfo)
        {
        }

        public void InitRenderData()
        {
        }

        public bool CalculateGroupData(int groupX, int groupZ, int layer, ref int vertexCount, ref int triangleCount,
                                       ref int objectCount, ref RenderGroup.VertexArrays vertexArrays)
        {
            return false;
        }

        public void PopulateGroupData(int groupX, int groupZ, int layer, ref int vertexIndex, ref int triangleIndex,
                                      Vector3 groupPosition, RenderGroup.MeshData data, ref Vector3 min, ref Vector3 max,
                                      ref float maxRenderDistance, ref float maxInstanceDistance, ref bool requireSurfaceMaps)
        {
        }
    }
}
