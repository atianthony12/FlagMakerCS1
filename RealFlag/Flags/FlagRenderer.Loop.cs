using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// One loop the renderer keeps (FlagMotion.Loop), by what makes it (LoopMotion.Key): asked for
    /// by flags, made a job at a time on the cloth's threads (<see cref="Bake"/>), then played by
    /// every flag of its shape (<see cref="Loop"/>), let go when none has wanted it for a while.
    /// </summary>
    public sealed class LoopEntry
    {
        public string Key;
        public FlagSpec Spec;
        public int Detail;
        public bool SelfCollision;

        /// <summary>Being made (its job may be out with the batch); made; not to be made until <see cref="FailedUntil"/> (real time).</summary>
        public LoopBake Bake;
        public FlagLoop Loop;
        public float FailedUntil = -1f;

        /// <summary>Real time: first asked for, last asked for, last asked for by a flag in the player's hand, and when its making began.</summary>
        public float Since, Wanted, InHand = -1e6f, Began;

        /// <summary>Real time: last asked for by a flag that waits for it undrawn (no cloth to wave meanwhile).</summary>
        public float Waited = -1e6f;

        /// <summary>No longer kept (let go, or never made): a flag still naming it looks its loop up again.</summary>
        public bool Gone;
    }

    /// <summary>
    /// Flags that play a loop (FlagMotion.Loop): the renderer's part. PURE of it: ClothLoop.cs
    /// (LoopMotion, LoopBake); the texture and the grids: FlagLoop.cs.
    /// </summary>
    /// <remarks>
    /// A flag asks for its loop by its key (<see cref="EntryFor"/>). Made, the flag takes it
    /// (<see cref="PlanLoop"/>): its cloth, mesh and city are let go, and from then on it is one
    /// draw a frame (<see cref="DrawLoop"/>) -- the loop's texture, the grid its size on the screen
    /// asks for, its own place in the loop, its heading as the draw's turn. Not made yet, a flag
    /// that has a cloth (it waved until now) or is in the player's hand (the flag being placed, the
    /// selected one, one just changed) waves meanwhile, so nothing freezes or vanishes while a
    /// slider moves; any other waits, undrawn, the second or so its loop takes -- a city's
    /// hundreds do not each settle a cloth to throw away.
    ///
    /// Loops are made <see cref="MaxBakes"/> at a time, those a flag in hand wants first, each a
    /// job of some steps a frame (LoopBake.Run on a cloth thread, <see cref="BakeMs"/> of it);
    /// begun only once a key has been wanted for <see cref="Debounce"/> (a slider on its way
    /// passes through a key a frame), dropped if no flag wants it any more.
    /// </remarks>
    public sealed partial class FlagRenderer
    {
        /// <summary>At most this many loops kept (2 MB of the graphics card's memory each at High, 3 x 2 m); past it the one longest unwanted goes, or the flag waves.</summary>
        private const int MaxLoops = 48;

        /// <summary>Loops made at once.</summary>
        private const int MaxBakes = 2;

        /// <summary>A loop is begun once its key has been wanted this long, seconds, and dropped unmade after <see cref="Unwanted"/> without.</summary>
        private const float Debounce = 0.3f;
        private const float Unwanted = 1f;

        /// <summary>
        /// A loop's job is given this much of a cloth thread a 1/60 s frame, milliseconds (more in
        /// a longer frame, as the flags' budget): one in the player's hand <see cref="BakeMsInHand"/>
        /// -- the player is watching for it.
        /// </summary>
        private const double BakeMs = 6.0;
        private const double BakeMsInHand = 12.0;

        /// <summary>
        /// A loop the player is waiting for -- one in hand, or one whose flags wait undrawn (a
        /// city just loaded) -- gets this share of the frame itself, however long the frame: its
        /// job runs on a cloth thread while the frame is drawn, so a long frame has that much
        /// room. Held to 4 frames of <see cref="BakeMs"/> (24 ms) it crawled at a low frame rate:
        /// the author's city of 3,019 loop flags, 2026-11-11, loaded at 7-32 fps and its one loop
        /// took 11.8 s to make, the flags undrawn meanwhile (3.7 s when it was first made, in
        /// hand at 77 fps; ~2.8 s of a thread's work).
        /// </summary>
        private const double BakeShareWaited = 0.75;

        /// <summary>A loop whose making failed for want of room is tried again after this long, seconds.</summary>
        private const float RetrySeconds = 60f;

        private readonly Dictionary<string, LoopEntry> _loops = new Dictionary<string, LoopEntry>();
        private readonly List<LoopEntry> _loopList = new List<LoopEntry>();

        /// <summary>The loops' clock, seconds (a double: it runs for as long as the city is open), stopped with the flags' when paused.</summary>
        private double _loopClock;

        private int _loopsMade, _loopsFailed;
        private bool _loopsFullSaid;

        /// <summary>The flag is drawn with its loop: the one its design asks for, or an older one while nothing else of it is ready.</summary>
        private static bool ShowsLoop(FlagInstance f)
        {
            LoopEntry e = f.Loop;
            if (e == null || e.Loop == null)
                return false;
            if (f.Spec.Motion == FlagMotion.Loop && (f.LoopWanted == null || f.LoopWanted == e))
                return true;
            return f.Mesh == null || !f.Settled;
        }

        /// <summary>The loop this flag's design asks for (kept on the flag until its design or the options change); null for none.</summary>
        private LoopEntry EntryFor(FlagInstance f, float now)
        {
            int detail = LoopMotion.DetailFor(_finest);
            LoopEntry e = f.LoopWanted;
            if (e != null && !e.Gone && e.Detail == detail && e.SelfCollision == _selfCollision)
                return e;

            string key = LoopMotion.Key(f.Spec, detail, _selfCollision);
            if (!_loops.TryGetValue(key, out e))
            {
                e = new LoopEntry { Key = key, Spec = f.Spec, Detail = detail, SelfCollision = _selfCollision, Since = now, Wanted = now };
                _loops[key] = e;
                _loopList.Add(e);
            }

            f.LoopWanted = e;
            return e;
        }

        /// <summary>
        /// Every frame, for every flag (after its place on the screen is known): the loops it
        /// shows and asks for are wanted while it is in view or big enough to be seen move were the
        /// camera turned to it; a loop let go meanwhile is no longer its.
        /// </summary>
        private void WantLoop(FlagInstance f, float now)
        {
            if (f.Loop != null && f.Loop.Gone)
                f.Loop = null;
            if (f.Loop == null && f.Spec.Motion != FlagMotion.Loop)
                return;
            if (!f.Visible && f.Pixels < MovePixels)
                return;

            if (f.Loop != null)
                f.Loop.Wanted = now;
            if (f.Spec.Motion == FlagMotion.Loop)
                EntryFor(f, now).Wanted = now;
        }

        /// <summary>
        /// A flag that plays a loop, planned: true when there is nothing to step -- it has its loop
        /// (taken here the frame it is made, its cloth let go), or it waits for it. False: it waves
        /// this frame (PlanFlag goes on) -- its loop is not made yet and it has a cloth or is in the
        /// player's hand, or its loop cannot be made.
        /// </summary>
        private bool PlanLoop(FlagInstance f, bool ghost)
        {
            float now = Time.realtimeSinceStartup;
            LoopEntry e = EntryFor(f, now);
            e.Wanted = now;
            bool inHand = ghost || f.Prop == FlagEditor.Selected || f.AwakeUntil > now;
            if (inHand)
                e.InHand = now;

            if (e.Loop != null)
            {
                if (f.Loop != e || f.Cloth != null)
                {
                    f.Loop = e;
                    f.LoopCells = 0;
                    LetClothGo(f);
                }

                // Its pick volume's axis out: its fly at rest, as a still flag's.
                f.WindDir = f.Fly;
                f.Pending = 0f;
                return true;
            }

            if (now < e.FailedUntil)
                return false;

            // Not made yet: a flag with a cloth, or in hand, waves meanwhile; the rest wait.
            if (f.Cloth != null || inHand)
                return false;

            e.Waited = now;
            return true;
        }

        /// <summary>
        /// The flag's cloth, mesh and city let go as it takes its loop (its pole stays: it is
        /// drawn as ever), and everything that named them; put back to how a new flag stands, so
        /// a design that waves again makes them afresh.
        /// </summary>
        private void LetClothGo(FlagInstance f)
        {
            if (f.Mesh != null)
                f.Mesh.Destroy();
            f.Mesh = null;
            f.Cloth = null;
            f.Pose = null;
            f.PoseWanted = false;
            f.Surface = null;
            f.Capsules = null;
            f.CapsulesTopFor = null;
            f.SurfaceAt = 0f;
            f.SurfaceStale = false;
            f.CapsulesAt = 0f;
            ForgetSettled(f);
            f.Follow = null;
            f.FollowCloth = null;
            f.FollowShown = float.NaN;
            f.FromX = f.FromY = f.FromZ = null;
            f.Settled = false;
            f.SettleLeft = SettleTime;
            f.LaidOut = false;
            f.WindAt10 = -1f;
            f.Waiting = false;
            f.WaitedLast = false;
            f.Pending = 0f;
            f.Fresh = false;
            f.MeshDirty = true;
            f.PoleApplied = false;
            f.UnseenSince = 0f;
            f.Hold.Clear();
            f.PickAge = 1e6f;
        }

        /// <summary>
        /// The loops, once a frame before the flags are planned: those whose making came back
        /// with the last batch are taken (their texture made) or said to have failed; those being
        /// made get their next job, the ones a flag in hand wants first; new ones begin; the ones
        /// no flag has wanted for a while are let go.
        /// </summary>
        private void PlanLoops(float now, ref double spent)
        {
            if (_loopList.Count == 0)
                return;

            int baking = 0, kept = 0;
            for (int i = _loopList.Count - 1; i >= 0; i--)
            {
                LoopEntry e = _loopList[i];
                LoopBake bake = e.Bake;
                if (bake != null && bake.Finished)
                {
                    e.Bake = null;
                    if (bake.Failed)
                    {
                        e.FailedUntil = float.MaxValue;
                        _loopsFailed++;
                        Log.Warn("flags: a loop could not be made (" + bake.Problem + "); its flags wave instead -- " + e.Spec.Describe());
                    }
                    else if (!TryMakeLoop(e, bake))
                    {
                        // Its texture could not be made (no float textures, no memory): never again
                        // this session -- left to throw, it was baked afresh every second for good.
                        e.FailedUntil = float.MaxValue;
                        _loopsFailed++;
                    }
                    else
                    {
                        _loopsMade++;
                        string line = "flags: loop made in " + (now - e.Began).ToString("F1") + " s -- " + e.Loop.Text;
                        if (_loopsMade <= 3)
                            Log.Msg(line + " (" + e.Spec.Describe() + ")" + (_loopsMade == 3 ? " (further ones are in the detailed log)" : ""));
                        else if (Log.Detailed)
                            Log.Detail(line);
                    }
                }

                // Let go: made, and wanted by no flag for as long as an unseen cloth is kept; being
                // made or never begun, and wanted by none for a moment (a slider passed through it).
                bool idle = e.Loop != null ? now - e.Wanted > ReleaseSeconds : now - e.Wanted > Unwanted && now >= e.FailedUntil;
                if (idle)
                {
                    DropLoop(i);
                    continue;
                }

                if (e.Bake != null)
                    baking++;
                if (e.Loop != null || e.Bake != null)
                    kept++;
            }

            // The next jobs, in hand first; then new ones while there is room for them.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < _loopList.Count; i++)
                {
                    LoopEntry e = _loopList[i];
                    bool inHand = now - e.InHand < Unwanted;
                    if (e.Loop != null || inHand != (pass == 0) || now < e.FailedUntil)
                        continue;

                    if (e.Bake == null)
                    {
                        if (baking >= MaxBakes || now - e.Since < Debounce)
                            continue;

                        if (kept >= MaxLoops && !MakeRoom(now))
                        {
                            e.FailedUntil = now + RetrySeconds;
                            if (!_loopsFullSaid)
                            {
                                _loopsFullSaid = true;
                                Log.Warn("flags: " + MaxLoops + " loops are kept and all are in use; flags of further shapes wave instead (said once)");
                            }

                            continue;
                        }

                        e.Bake = new LoopBake(e.Spec, e.Detail, e.SelfCollision, FlagMesh.SmoothEdges);
                        e.Began = now;
                        baking++;
                        kept++;
                    }

                    // Some steps of it, as a job: what its share of a thread's frame holds.
                    LoopBake bake = e.Bake;
                    double room = (inHand ? BakeMsInHand : BakeMs) * Mathf.Clamp(_frameTime / Step, 1f, 4f);

                    // Waited for (in hand, or its flags undrawn): most of the frame itself.
                    if (inHand || now - e.Waited < Unwanted)
                        room = Math.Max(room, BakeShareWaited * _frameTime * 1000.0);
                    double stepCost = Math.Max(1e-6, (double)bake.SubSteps * bake.Count * _unitCost);
                    bake.StepsNext = (int)Math.Max(bake.FrameSteps, Math.Min(4096.0, room / stepCost));
                    spent += bake.StepsNext * stepCost;
                    var job = new ClothJob { Work = bake };
                    Queue(null, ref job);
                }
            }
        }

        /// <summary>The finished bake's texture, as the entry's loop; false (said, the flags wave instead) when the graphics card will not hold it.</summary>
        private static bool TryMakeLoop(LoopEntry e, LoopBake bake)
        {
            try
            {
                if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
                    throw new NotSupportedException("this graphics card has no float textures");
                e.Loop = new FlagLoop(bake);
                return true;
            }
            catch (Exception x)
            {
                e.Loop = null;
                Log.Warn("flags: a loop's texture could not be made (" + x.Message + "); its flags wave instead -- " + e.Spec.Describe());
                return false;
            }
        }

        /// <summary>Room for one more loop: the made one longest unwanted goes, if none has wanted it for a moment.</summary>
        private bool MakeRoom(float now)
        {
            int oldest = -1;
            for (int i = 0; i < _loopList.Count; i++)
            {
                LoopEntry e = _loopList[i];
                if (e.Loop != null && now - e.Wanted > 10f && (oldest < 0 || e.Wanted < _loopList[oldest].Wanted))
                    oldest = i;
            }

            if (oldest < 0)
                return false;

            DropLoop(oldest);
            return true;
        }

        private void DropLoop(int i)
        {
            LoopEntry e = _loopList[i];
            if (e.Loop != null)
            {
                e.Loop.Destroy();
                if (Log.Detailed)
                    Log.Detail("flags: a loop let go, wanted by no flag for " + ReleaseSeconds.ToString("F0") + " s or to make room (" + e.Spec.Describe() + ")");
            }

            e.Loop = null;
            e.Bake = null;
            e.Gone = true;
            _loops.Remove(e.Key);
            _loopList.RemoveAt(i);
        }

        /// <summary>With the city: every loop's texture and grids.</summary>
        private void DestroyLoops()
        {
            foreach (LoopEntry e in _loopList)
            {
                if (e.Loop != null)
                    e.Loop.Destroy();
                e.Loop = null;
                e.Bake = null;
                e.Gone = true;
            }

            _loopList.Clear();
            _loops.Clear();
        }

        /// <summary>The turn that carries a loop from the flag's own frame (heading 0) to where it heads: the prop's angle runs from x towards z, Unity's turn about y the other way.</summary>
        private static Quaternion LoopTurn(float angle)
        {
            return Quaternion.AngleAxis(-angle * Mathf.Rad2Deg, Vector3.up);
        }

        /// <summary>The box of all the loop's frames where the flag stands and heads (for the camera's test and the list).</summary>
        private static Bounds LoopBox(FlagInstance f)
        {
            Bounds own = f.Loop.Loop.Bounds;
            Vector3 c = own.center, x = own.extents;
            float cos = Mathf.Cos(f.Angle), sin = Mathf.Sin(f.Angle);

            // Turned about the vertical: the box of the turned box.
            var centre = new Vector3(c.x * cos - c.z * sin, c.y, c.x * sin + c.z * cos);
            float ac = Mathf.Abs(cos), asn = Mathf.Abs(sin);
            var half = new Vector3(x.x * ac + x.z * asn, x.y, x.x * asn + x.z * ac);
            return new Bounds(f.Anchor + centre, 2f * half);
        }

        /// <summary>
        /// A flag's loop, drawn: its design in its block, the two frames it is between now (each
        /// flag at its own place in the loop), one of the loop's grids -- as fine as the flag's
        /// size on the screen asks -- turned to its heading at its anchor. A shadow as any flag's.
        /// </summary>
        private void DrawLoop(FlagInstance f)
        {
            FlagLoop loop = f.Loop.Loop;
            PutBlock(f);
            loop.Bind(f.Block, _loopClock + f.OwnTime, f.Prop);

            // The grid: cells of about PixelsPerCell on the screen, changed only once the size has
            // moved RefineBand past where another would be chosen (no flicker between two).
            int want = LoopMotion.CellsFor(f.Pixels, PixelsPerCell, loop.FinestCells);
            if (f.LoopCells == 0)
                f.LoopCells = want;
            else if (want > f.LoopCells && LoopMotion.CellsFor(f.Pixels / RefineBand, PixelsPerCell, loop.FinestCells) > f.LoopCells)
                f.LoopCells = want;
            else if (want < f.LoopCells && LoopMotion.CellsFor(f.Pixels * RefineBand, PixelsPerCell, loop.FinestCells) < f.LoopCells)
                f.LoopCells = want;

            Graphics.DrawMesh(loop.MeshFor(f.LoopCells), Matrix4x4.TRS(f.Anchor, LoopTurn(f.Angle), Vector3.one), _material, _layer, _camera, 0,
                              f.Block, f.Pixels >= ShadowPixels, true);

            if (!_loggedFirstLoop)
            {
                _loggedFirstLoop = true;
                Log.Msg("flags: first loop drawn (#" + f.Prop + ", " + f.Spec.Describe() + ", " + f.LoopCells + " cells along, at " + f.Distance.ToString("F0") + " m)");
            }

            f.PickAge += Time.unscaledDeltaTime;
            if (f.Prop != 0 && f.PickAge >= PickInterval)
            {
                float since = f.PickAge;
                f.PickAge = 0f;
                PublishPick(f, since);
            }
        }

        private bool _loggedFirstLoop;

        /// <summary>For the 5 s line: the loops kept, their memory, those being made.</summary>
        private string LoopsText()
        {
            int made = 0, baking = 0;
            long bytes = 0;
            foreach (LoopEntry e in _loopList)
            {
                if (e.Loop != null)
                {
                    made++;
                    bytes += e.Loop.Bytes;
                }
                else if (e.Bake != null)
                {
                    baking++;
                }
            }

            return made + " loop(s) kept (" + (bytes / 1048576f).ToString("F1") + " MB)" + (baking > 0 ? ", " + baking + " being made" : "") +
                   (_loopsFailed > 0 ? ", " + _loopsFailed + " failed" : "");
        }
    }
}
