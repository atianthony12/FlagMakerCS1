using System;
using System.Collections.Generic;
using ColossalFramework;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The string network in the game's NetManager: which segments are spans, where their ends
    /// are, the chains they make, and spans made and removed -- made and removed on the
    /// SIMULATION thread (SimulationManager.AddAction), as the game's own tools do; read anywhere
    /// (a read of a segment that changes under it is at worst a frame old, the renderer checks
    /// again).
    /// </summary>
    public static class StringNet
    {
        /// <summary>A span joins the network only this far apart, metres (shorter is a click on the same spot).</summary>
        public const float MinSpan = 0.5f;

        /// <summary>The longest span, metres: a string pulled across a wide street, a square.</summary>
        public const float MaxSpan = 120f;

        public static int SegmentCount
        {
            get { return Singleton<NetManager>.instance.m_segments.m_size > 0 ? (int)Singleton<NetManager>.instance.m_segments.m_size : 0; }
        }

        /// <summary>The segment is a span of the string network, made and not deleted.</summary>
        public static bool IsSpan(ushort segment)
        {
            if (segment == 0 || StringPrefab.Info == null)
                return false;

            NetSegment[] buffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            if (segment >= buffer.Length)
                return false;

            NetSegment.Flags flags = buffer[segment].m_flags;
            return (flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) == NetSegment.Flags.Created &&
                   buffer[segment].m_infoIndex == StringPrefab.Info.m_prefabDataIndex;
        }

        /// <summary>A span's two ends, and its nodes.</summary>
        public static bool TryRead(ushort segment, out Vector3 start, out Vector3 end, out ushort startNode, out ushort endNode)
        {
            start = end = Vector3.zero;
            startNode = endNode = 0;
            if (!IsSpan(segment))
                return false;

            NetManager net = Singleton<NetManager>.instance;
            NetSegment s = net.m_segments.m_buffer[segment];
            startNode = s.m_startNode;
            endNode = s.m_endNode;
            if (startNode == 0 || endNode == 0)
                return false;

            start = net.m_nodes.m_buffer[startNode].m_position;
            end = net.m_nodes.m_buffer[endNode].m_position;
            return true;
        }

        public static Vector3 NodePosition(ushort node)
        {
            return Singleton<NetManager>.instance.m_nodes.m_buffer[node].m_position;
        }

        /// <summary>The spans whose segments are in [from, from + count) of the buffer, added to <paramref name="into"/>.</summary>
        public static void Scan(int from, int count, List<ushort> into)
        {
            if (StringPrefab.Info == null)
                return;

            NetSegment[] buffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            int index = StringPrefab.Info.m_prefabDataIndex;
            int to = Math.Min(buffer.Length, from + count);
            for (int i = Math.Max(1, from); i < to; i++)
            {
                if ((buffer[i].m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) == NetSegment.Flags.Created && buffer[i].m_infoIndex == index)
                    into.Add((ushort)i);
            }
        }

        /// <summary>The string spans at a node (at most 8, the game's).</summary>
        public static int SpansAt(ushort node, ushort[] into)
        {
            int n = 0;
            if (node == 0)
                return 0;
            NetNode nn = Singleton<NetManager>.instance.m_nodes.m_buffer[node];
            for (int i = 0; i < 8; i++)
            {
                ushort s = nn.GetSegment(i);
                if (s != 0 && IsSpan(s))
                    into[n++] = s;
            }

            return n;
        }

        /// <summary>
        /// The chain <paramref name="segment"/> is in, in order from one end: each span, and
        /// whether the chain runs along it from its start node to its end node. A chain goes on
        /// through a node with exactly two string spans; it stops at an end, a fork, or where it
        /// closes on itself.
        /// </summary>
        public static void ChainOf(ushort segment, List<ushort> spans, List<bool> forward)
        {
            spans.Clear();
            forward.Clear();
            Vector3 a, b;
            ushort startNode, endNode;
            if (!TryRead(segment, out a, out b, out startNode, out endNode))
                return;

            var at = new ushort[8];
            var seen = new HashSet<ushort> { segment };

            // Back from the start node to the chain's first span.
            var back = new List<ushort>();
            var backForward = new List<bool>();
            ushort node = startNode, from = segment;
            while (true)
            {
                if (SpansAt(node, at) != 2)
                    break;
                ushort next = at[0] == from ? at[1] : at[0];
                if (seen.Contains(next))
                    break;
                ushort ns, ne;
                if (!TryRead(next, out a, out b, out ns, out ne))
                    break;
                seen.Add(next);
                // Walking backwards, reaching it at `node`: it runs forward (start -> end) if its END is that node.
                bool fwd = ne == node;
                back.Add(next);
                backForward.Add(fwd);
                node = fwd ? ns : ne;
                from = next;
            }

            for (int i = back.Count - 1; i >= 0; i--)
            {
                spans.Add(back[i]);
                forward.Add(backForward[i]);
            }

            spans.Add(segment);
            forward.Add(true);

            // On from the end node.
            node = endNode;
            from = segment;
            while (true)
            {
                if (SpansAt(node, at) != 2)
                    break;
                ushort next = at[0] == from ? at[1] : at[0];
                if (seen.Contains(next))
                    break;
                ushort ns, ne;
                if (!TryRead(next, out a, out b, out ns, out ne))
                    break;
                seen.Add(next);
                bool fwd = ns == node;
                spans.Add(next);
                forward.Add(fwd);
                node = fwd ? ne : ns;
                from = next;
            }
        }

        /// <summary>The end node of the last span made (the simulation thread's): the next click's start, if it is there.</summary>
        private static ushort _lastEnd;

        /// <summary>
        /// A chain of spans through <paramref name="points"/> on the simulation thread, each point
        /// a new node or, where <paramref name="nodes"/> has one, that node (a click on a string's
        /// end); every span gets <paramref name="spec"/>. <paramref name="made"/> runs there too,
        /// with the spans made, in order.
        /// </summary>
        public static void Create(IList<Vector3> points, IList<ushort> nodes, StringSpec spec, Action<List<ushort>> made)
        {
            var p = new List<Vector3>(points);
            var n = new List<ushort>(nodes);
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                var spans = new List<ushort>();
                try
                {
                    NetInfo info = StringPrefab.Info;
                    if (info == null || p.Count < 2)
                        return;

                    NetManager net = Singleton<NetManager>.instance;
                    SimulationManager sim = Singleton<SimulationManager>.instance;
                    var ids = new ushort[p.Count];
                    for (int i = 0; i < p.Count; i++)
                    {
                        ushort given = i < n.Count ? n[i] : (ushort)0;

                        // The chain's last end, made by the action before this one (a click came before the tool heard of it).
                        if (given == 0 && _lastEnd != 0 && (net.m_nodes.m_buffer[_lastEnd].m_flags & NetNode.Flags.Created) != 0 &&
                            (net.m_nodes.m_buffer[_lastEnd].m_position - p[i]).sqrMagnitude < 0.0025f)
                            given = _lastEnd;
                        if (given != 0 && (net.m_nodes.m_buffer[given].m_flags & NetNode.Flags.Created) != 0)
                        {
                            ids[i] = given;
                            continue;
                        }

                        ushort node;
                        if (!net.CreateNode(out node, ref sim.m_randomizer, info, p[i], sim.m_currentBuildIndex))
                        {
                            Log.Warn("strings: the game had no room for another node (" + net.m_nodeCount + " in use): the string stops there");
                            break;
                        }

                        sim.m_currentBuildIndex++;
                        ids[i] = node;
                    }

                    for (int i = 0; i + 1 < p.Count; i++)
                    {
                        if (ids[i] == 0 || ids[i + 1] == 0 || ids[i] == ids[i + 1])
                            continue;

                        Vector3 a = net.m_nodes.m_buffer[ids[i]].m_position, b = net.m_nodes.m_buffer[ids[i + 1]].m_position;
                        Vector3 dir = new Vector3(b.x - a.x, 0f, b.z - a.z);
                        if (dir.sqrMagnitude < 1e-6f)
                            dir = Vector3.right;
                        dir.Normalize();

                        ushort segment;
                        if (!net.CreateSegment(out segment, ref sim.m_randomizer, info, ids[i], ids[i + 1], dir, -dir, sim.m_currentBuildIndex,
                                               sim.m_currentBuildIndex, false))
                        {
                            Log.Warn("strings: the game had no room for another segment (" + net.m_segmentCount + " in use): the string stops there");
                            break;
                        }

                        sim.m_currentBuildIndex++;
                        StringRegistry.Set(segment, spec);
                        spans.Add(segment);
                        _lastEnd = ids[i + 1];
                    }
                }
                catch (Exception e)
                {
                    Log.Error("strings: making a string threw.", e);
                }
                finally
                {
                    if (made != null)
                        made(spans);
                }
            });
        }

        /// <summary>The spans removed on the simulation thread, their nodes with them where no other segment holds them.</summary>
        public static void Delete(IList<ushort> segments)
        {
            var list = new List<ushort>(segments);
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                NetManager net = Singleton<NetManager>.instance;
                int removed = 0;
                foreach (ushort s in list)
                {
                    if (!IsSpan(s))
                        continue;
                    net.ReleaseSegment(s, false);
                    StringRegistry.Remove(s);
                    removed++;
                }

                Log.Msg("strings: " + removed + " span(s) removed");
            });
        }

        /// <summary>A node of the string network near <paramref name="point"/>: a click there joins it (0 for none).</summary>
        public static ushort NodeNear(Vector3 point, float radius, IList<ushort> spans)
        {
            ushort best = 0;
            float bestDist = radius * radius;
            NetManager net = Singleton<NetManager>.instance;
            foreach (ushort s in spans)
            {
                if (!IsSpan(s))
                    continue;
                NetSegment seg = net.m_segments.m_buffer[s];
                for (int k = 0; k < 2; k++)
                {
                    ushort node = k == 0 ? seg.m_startNode : seg.m_endNode;
                    if (node == 0)
                        continue;
                    float d = (net.m_nodes.m_buffer[node].m_position - point).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = node;
                    }
                }
            }

            return best;
        }
    }
}
