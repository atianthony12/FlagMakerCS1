using System;
using System.Collections;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Globalization;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The prop every flag is: a prefab of the mod's own, made while a city loads, so that each
    /// flag is a real PropInstance -- which is what lets Move It select, move, rotate, raise,
    /// copy and delete it like any other prop (its PropsManager walks the prop grid and calls
    /// PropInstance.RayCast; its MoveableProp keeps position, angle and fixed height). The mod
    /// draws the cloth at the prop's position (the TOP HOIST CORNER, where a pole's halyard
    /// would clip on) and turns it with the prop's angle.
    /// </summary>
    /// <remarks>
    /// Read from the game's IL (2026-09-25):
    /// - PropInfo.InitializePrefab needs a class and a generated info of non-zero size; with no
    ///   Renderer component on its GameObject it sets m_hasRenderer false and skips the mesh,
    ///   the material and the LOD (an effect-only prop is the same), and PropInstance.RenderInstance
    ///   draws nothing for it; PropManager.InitRenderData skips a prop with no LOD mesh.
    /// - Move It's surface filter reads info.m_mesh.name (Filters.IsSurface): so the prefab has a
    ///   mesh, named, that nothing draws. It is the rest pose, for Find It's thumbnail.
    /// - PropInstance.RayCast picks a prop by a thin cylinder rising from its position, sized by
    ///   the generated info; FlagPatches replaces that for flags with the cloth itself.
    /// - A save's props are bound to their prefabs BY NAME in PropManager's AfterDeserialize
    ///   (PrefabCollection.BindPrefabs), which waits for LoadingManager's "essential scenes
    ///   loaded": the prefab must be in the collection before that, or a city's flags come back
    ///   as unknown prefabs. FlagLoadingHook queues <see cref="CreateDuringLoading"/> exactly
    ///   between the custom assets and that point.
    /// If the mod is removed, the name is simply unknown: the game drops the flags, nothing
    /// else (no missing Workshop asset to subscribe to).
    /// </remarks>
    public static class FlagPrefab
    {
        /// <summary>The prefab's name, which is what a save stores. PERMANENT.</summary>
        public const string Name = "RealFlag Cloth";

        private static PropInfo _info;
        private static bool _localeHooked;

        /// <summary>The prefab, once made; null before (or if making it failed).</summary>
        public static PropInfo Info
        {
            get { return _info != null ? _info : null; }
        }

        /// <summary>
        /// True if a prop's info index is the flag prefab's: the hot test (the Harmony raycast runs
        /// it for every prop along a ray). m_prefabDataIndex is what PropInstance.set_Info stores.
        /// </summary>
        public static bool IsFlagIndex(ushort infoIndex)
        {
            PropInfo info = _info;
            return !ReferenceEquals(info, null) && info.m_prefabDataIndex == infoIndex;
        }

        public static bool IsFlag(PropInfo info)
        {
            return !ReferenceEquals(info, null) && ReferenceEquals(info, _info);
        }

        /// <summary>Queued by FlagLoadingHook while a city loads (see the remarks).</summary>
        public static IEnumerator CreateDuringLoading()
        {
            EnsureCreated(false);
            yield return null;
        }

        /// <summary>
        /// Makes the prefab and puts it in the game's prop collection, unless it is there
        /// already (a city loaded from inside another keeps its prefabs; a hot reload of the mod
        /// finds its predecessor's). <paramref name="bind"/>: the city is ALREADY loaded (the
        /// mod was enabled in it), so bind the collection again, on the simulation thread.
        /// </summary>
        public static void EnsureCreated(bool bind)
        {
            try
            {
                if (PrefabCollection<PropInfo>.LoadedExists(Name))
                {
                    PropInfo existing = PrefabCollection<PropInfo>.FindLoaded(Name);
                    if (existing != null)
                    {
                        if (!ReferenceEquals(existing, _info))
                            Log.Msg("flag prefab: '" + Name + "' already in the prop collection (index " + existing.m_prefabDataIndex + ")");
                        _info = existing;
                        HookLocale();
                        return;
                    }
                }

                PropInfo info = Build();
                PrefabCollection<PropInfo>.InitializePrefabs("RealFlag", info, null);
                _info = info;
                HookLocale();
                Log.Msg("flag prefab: '" + Name + "' made and added to the prop collection (initialised=" + info.m_prefabInitialized +
                        ", renderer=" + info.m_hasRenderer + ")" + (bind ? "; binding the collection again" : ""));

                if (bind)
                {
                    Singleton<SimulationManager>.instance.AddAction(() =>
                    {
                        PrefabCollection<PropInfo>.BindPrefabs();
                        Log.Msg("flag prefab: bound, index " + info.m_prefabDataIndex);
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error("flag prefab: could not make '" + Name + "'; flags cannot be placed or loaded in this city.", e);
            }
        }

        /// <summary>Forgets the prefab (the city unloaded; the game destroyed its prefabs with it).</summary>
        public static void Forget()
        {
            _info = null;
        }

        private static PropInfo Build()
        {
            var go = new GameObject(Name);
            go.SetActive(false);

            PropInfo info = go.AddComponent<PropInfo>();

            ItemClass itemClass = ScriptableObject.CreateInstance<ItemClass>();
            itemClass.name = "RealFlag";
            itemClass.m_service = ItemClass.Service.Beautification;
            itemClass.m_subService = ItemClass.SubService.None;
            itemClass.m_level = ItemClass.Level.Level1;
            itemClass.m_layer = ItemClass.Layer.Default;
            info.m_class = itemClass;

            // The size the game's own code sizes the prop by where FlagPatches does not step in:
            // the overlap test (a 0.6 m square round the anchor for a placed prop, this tall above
            // it) and the vanilla pick cylinder. Small, so a flag on a facade is rarely "blocked".
            PropInfoGen generated = ScriptableObject.CreateInstance<PropInfoGen>();
            generated.name = Name;
            generated.m_size = new Vector3(1.5f, 1f, 1.5f);
            generated.m_center = new Vector3(0f, 0.5f, 0f);
            generated.m_triangleArea = 0f;
            generated.m_uvmapArea = 0f;
            info.m_generatedInfo = generated;

            info.m_mesh = RestMesh();
            Shader shader = ShaderBundle.Get(ShaderBundle.Cloth);
            if (shader != null)
                info.m_material = new Material(shader) { name = Name };

            info.m_placementStyle = ItemClass.Placement.Manual;
            // The game only: for a prop, availability is read by the placement panels and the asset
            // editor's templates alone (never at load: a save's flags bind in any mode, the IL). In the
            // editors, where nothing draws it, it would go into maps and assets as an invisible prop
            // that every player without the mod would find missing.
            info.m_availableIn = ItemClass.Availability.Game;
            info.m_minScale = 1f;
            info.m_maxScale = 1f;
            info.m_effects = new PropInfo.Effect[0];
            info.m_variations = new PropInfo.Variation[0];
            info.m_parkingSpaces = new PropInfo.ParkingSpace[0];
            info.m_specialPlaces = new PropInfo.SpecialPlace[0];
            info.m_color0 = Color.white;
            info.m_color1 = Color.white;
            info.m_color2 = Color.white;
            info.m_color3 = Color.white;
            info.m_isCustomContent = false;
            return info;
        }

        /// <summary>A 3 x 2 m flag hanging from the anchor, flat: the prefab's mesh, which the game never draws.</summary>
        private static Mesh RestMesh()
        {
            var mesh = new Mesh { name = Name };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 0f), new Vector3(0f, -2f, 0f), new Vector3(3f, -2f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            mesh.RecalculateBounds();

            // A flat flag has no depth, but other mods size a prop by its mesh: Line Tool's "Length"
            // spacing is bounds.extents.z x 2, and a spacing of 0 never ends its loop along a line
            // (LineMode.CalculatePoints, 0.7.1: the game freezes). As deep as the flag is long.
            mesh.bounds = new Bounds(new Vector3(1.5f, -1f, 0f), new Vector3(3f, 2f, 3f));
            return mesh;
        }

        /// <summary>
        /// The prop's name where the game or another mod shows one (Move It, Find It, the info
        /// tooltips): "PROPS_TITLE" and "PROPS_DESC" under the prefab's name, added again whenever
        /// the game reloads its locale (a language change wipes added strings).
        /// </summary>
        private static void HookLocale()
        {
            AddLocale();
            if (_localeHooked)
                return;

            _localeHooked = true;
            LocaleManager.eventLocaleChanged += AddLocale;
        }

        /// <summary>Called on unload of the mod (OnDisabled), so a disabled mod leaves no handler behind.</summary>
        public static void UnhookLocale()
        {
            if (!_localeHooked)
                return;

            _localeHooked = false;
            LocaleManager.eventLocaleChanged -= AddLocale;
        }

        private static void AddLocale()
        {
            try
            {
                if (!LocaleManager.exists)
                    return;

                FieldInfo field = typeof(LocaleManager).GetField("m_Locale", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                Locale locale = field != null ? field.GetValue(LocaleManager.instance) as Locale : null;
                if (locale == null)
                    return;

                Add(locale, "PROPS_TITLE", Localization.Get("Prop.Title"));
                Add(locale, "PROPS_DESC", Localization.Get("Prop.Description"));
            }
            catch (Exception e)
            {
                Log.Warn("flag prefab: could not name the prop in the game's locale: " + e.Message);
            }
        }

        private static void Add(Locale locale, string id, string text)
        {
            var key = new Locale.Key { m_Identifier = id, m_Key = Name };
            if (!locale.Exists(key))
                locale.AddLocalizedString(key, text);
        }
    }
}
