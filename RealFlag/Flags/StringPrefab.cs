using System;
using System.Collections;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Globalization;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The network every string of flags is (2026-11-07; the author: string flags "should behave
    /// like networks, draggable by MoveIt!"): a NetInfo of the mod's own, made while a city loads
    /// as the flag prop is, so that each SPAN of a string is a real NetSegment between two real
    /// NetNodes -- which Move It selects, moves (a node drags both its spans), copies and pastes,
    /// the bulldozer removes, and a save keeps with both ends' heights. The mod draws the string
    /// and its flags along each span (StringRenderer); the game draws nothing.
    /// </summary>
    /// <remarks>
    /// Read from the game's IL (2026-11-07):
    /// - NetInfo.InitializePrefab needs a class ("Class missing") and walks its segments', nodes'
    ///   and lanes' arrays: empty arrays, no meshes, no lanes -- nothing to draw, nothing to route.
    /// - NetAI's AfterTerrainUpdate (both), CreateNode/CreateSegment, NodeLoaded/SegmentLoaded,
    ///   UpdateNode, UpdateNodeFlags and UpdateSegmentFlags are empty: a node stays at the height it
    ///   was put at, on a wall or a roof (StringAI is NetAI as it is).
    /// - m_canCollide false: buildings (BuildingAI.CheckBuildPosition), zones (ZoneBlock) and other
    ///   networks (NetSegment.OverlapQuad) never meet it; m_flattenTerrain / m_lowerTerrain /
    ///   m_clipTerrain false: the ground under it is never touched.
    /// - NetManager.Data.Serialize writes each node's position (WriteVector3): a span's ends come
    ///   back at their heights. Its AfterDeserialize binds infos BY NAME and skips a segment whose
    ///   info is missing (no info, NodeLoaded/SegmentLoaded not called): without the mod a string
    ///   stays in the city unseen, as any unsubscribed network asset does.
    /// - NetSegment.RayCast picks a segment along the line between its nodes, from m_minHeight to
    ///   m_maxHeight about it (NetAI.GetRayCastHeights), m_halfWidth across: a string hangs below
    ///   that line, so the band reaches down.
    /// </remarks>
    public static class StringPrefab
    {
        /// <summary>The network's name, which is what a save stores. PERMANENT.</summary>
        public const string Name = "RealFlag String";

        private static NetInfo _info;

        public static NetInfo Info
        {
            get { return _info != null ? _info : null; }
        }

        /// <summary>True for this network's info (by reference: the hot test of a scan).</summary>
        public static bool Is(NetInfo info)
        {
            return !ReferenceEquals(info, null) && ReferenceEquals(info, _info);
        }

        /// <summary>The network's index in the collection (what NetSegment.m_infoIndex holds), or -1.</summary>
        public static int Index
        {
            get { return _info != null ? (int)_info.m_prefabDataIndex : -1; }
        }

        /// <summary>Queued by FlagLoadingHook while a city loads, beside the flag prop.</summary>
        public static IEnumerator CreateDuringLoading()
        {
            EnsureCreated(false);
            yield return null;
        }

        /// <summary>Makes the network and puts it in the game's collection, unless it is there (see FlagPrefab.EnsureCreated).</summary>
        public static void EnsureCreated(bool bind)
        {
            try
            {
                if (PrefabCollection<NetInfo>.LoadedExists(Name))
                {
                    NetInfo existing = PrefabCollection<NetInfo>.FindLoaded(Name);
                    if (existing != null)
                    {
                        if (!ReferenceEquals(existing, _info))
                            Log.Msg("strings: '" + Name + "' already in the network collection (index " + existing.m_prefabDataIndex + ")");
                        _info = existing;
                        HookLocale();
                        return;
                    }
                }

                NetInfo info = Build();
                PrefabCollection<NetInfo>.InitializePrefabs("RealFlag", info, null);
                _info = info;
                HookLocale();
                Log.Msg("strings: '" + Name + "' made and added to the network collection (initialised=" + info.m_prefabInitialized + ")" +
                        (bind ? "; binding the collection again" : ""));

                if (bind)
                {
                    Singleton<SimulationManager>.instance.AddAction(() =>
                    {
                        PrefabCollection<NetInfo>.BindPrefabs();
                        Log.Msg("strings: network bound, index " + info.m_prefabDataIndex);
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error("strings: could not make '" + Name + "'; strings of flags cannot be placed or loaded in this city.", e);
            }
        }

        public static void Forget()
        {
            _info = null;
        }

        private static bool _localeHooked;

        /// <summary>
        /// The network's name where the game or another mod shows one: "NET_TITLE" and "NET_DESC"
        /// under the prefab's name, as FlagPrefab names the prop (without them the game logged
        /// "NET_TITLE[RealFlag String] ... not found" twice at every city load), added again
        /// whenever the game reloads its locale.
        /// </summary>
        private static void HookLocale()
        {
            AddLocale();
            if (_localeHooked)
                return;

            _localeHooked = true;
            LocaleManager.eventLocaleChanged += AddLocale;
        }

        /// <summary>Called on unload of the mod (FlagLoadingHook.Unregister), so a disabled mod leaves no handler behind.</summary>
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

                Add(locale, "NET_TITLE", Localization.Get("StringNet.Title"));
                Add(locale, "NET_DESC", Localization.Get("StringNet.Description"));
            }
            catch (Exception e)
            {
                Log.Warn("strings: could not name the network in the game's locale: " + e.Message);
            }
        }

        private static void Add(Locale locale, string id, string text)
        {
            var key = new Locale.Key { m_Identifier = id, m_Key = Name };
            if (!locale.Exists(key))
                locale.AddLocalizedString(key, text);
        }

        /// <summary>The pick band: how far below the line between the nodes a span is found, how far above, and across.</summary>
        public const float PickBelow = 3f, PickAbove = 0.6f, PickHalfWidth = 0.6f;

        private static NetInfo Build()
        {
            var go = new GameObject(Name);
            go.SetActive(false);

            // The AI first: NetInfo.InitializePrefab takes the NetAI component it finds.
            go.AddComponent<StringAI>();
            NetInfo info = go.AddComponent<NetInfo>();

            ItemClass itemClass = ScriptableObject.CreateInstance<ItemClass>();
            itemClass.name = "RealFlag String";
            itemClass.m_service = ItemClass.Service.Beautification;
            itemClass.m_subService = ItemClass.SubService.None;
            itemClass.m_level = ItemClass.Level.Level1;
            itemClass.m_layer = ItemClass.Layer.Default;
            info.m_class = itemClass;
            info.m_connectionClass = itemClass;
            info.m_intersectClass = itemClass;

            info.m_placementStyle = ItemClass.Placement.Manual;
            // The game only: in the editors nothing draws it, and maps or assets would carry an
            // invisible missing network.
            info.m_availableIn = ItemClass.Availability.Game;

            info.m_halfWidth = PickHalfWidth;
            info.m_pavementWidth = 0f;
            info.m_segmentLength = 64f;
            info.m_minHeight = -PickBelow;
            info.m_maxHeight = PickAbove;
            info.m_maxSlope = 10f;
            info.m_maxBuildAngle = 180f;
            info.m_maxTurnAngle = 180f;
            info.m_minCornerOffset = 0f;
            info.m_maxCornerOffset = 0f;
            info.m_buildHeight = 0f;
            info.m_surfaceLevel = 0f;

            info.m_createPavement = false;
            info.m_createGravel = false;
            info.m_createRuining = false;
            info.m_flattenTerrain = false;
            info.m_lowerTerrain = false;
            info.m_clipTerrain = false;
            info.m_followTerrain = false;
            info.m_flatJunctions = false;
            info.m_clipSegmentEnds = false;
            info.m_twistSegmentEnds = false;
            info.m_straightSegmentEnds = false;
            info.m_enableBendingSegments = false;
            info.m_enableBendingNodes = false;
            info.m_enableMiddleNodes = false;
            info.m_requireContinuous = false;
            info.m_canCrossLanes = true;
            info.m_canCollide = false;
            info.m_blockWater = false;
            info.m_autoRemove = false;
            info.m_overlayVisible = true;
            info.m_useFixedHeight = true;
            info.m_snapBuildingNodes = false;
            info.m_canDisable = false;

            info.m_lanes = new NetInfo.Lane[0];
            info.m_segments = new NetInfo.Segment[0];
            info.m_nodes = new NetInfo.Node[0];
            info.m_color = new Color(0.9f, 0.85f, 0.75f);
            info.m_isCustomContent = false;
            return info;
        }
    }

    /// <summary>
    /// The string network's AI: NetAI's own, which does nothing a string should not (see
    /// StringPrefab's remarks). A type of the mod's: a save never names it (it names the network),
    /// and without the mod the network is simply missing.
    /// </summary>
    public class StringAI : NetAI
    {
        public override bool IsOverground()
        {
            return true;
        }

        public override bool RaiseTerrain()
        {
            return false;
        }

        public override bool CanModify()
        {
            return true;
        }

        public override int GetConstructionCost(Vector3 startPos, Vector3 endPos, float startHeight, float endHeight)
        {
            return 0;
        }

        public override int GetMaintenanceCost()
        {
            return 0;
        }
    }
}
