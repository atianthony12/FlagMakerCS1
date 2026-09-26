using System;
using System.Diagnostics;
using System.Threading;

namespace RealFlag.Flags
{
    /// <summary>
    /// One flag's work for one frame: its cloth's steps in its air, then its mesh's vertices
    /// between the last two steps. Filled on the main thread, done on the cloth's thread.
    /// </summary>
    public struct ClothJob
    {
        public FlagCloth Cloth;

        /// <summary>Refreshed after the steps at <see cref="Alpha"/>; null leaves the mesh as it is.</summary>
        public FlagMesh Mesh;

        /// <summary>How many steps of <see cref="Size"/> seconds, and the flag's clock at the first.</summary>
        public int Steps;
        public float Size;
        public float Time;
        public float Alpha;

        /// <summary>The air, as FlagWind.Air takes it.</summary>
        public float Speed, Gusts, DirX, DirZ, Height, Waviness, Length, AnchorX, AnchorZ;

        /// <summary>Set by the thread: the cloth stopped being numbers and was laid out again.</summary>
        public bool WentWrong;

        /// <summary>Set by the thread: what a job threw (its cloth is laid out again by the owner).</summary>
        public Exception Failure;
    }

    /// <summary>
    /// How many steps a flag takes this frame, within the cloth thread's budget. PURE: the
    /// arithmetic of FlagRenderer's planning, tested by tools/test-cloth.ps1.
    /// </summary>
    public static class ClothPlan
    {
        /// <summary>
        /// A frame's steps for one flag.
        /// </summary>
        /// <param name="pending">Time not yet stepped, seconds: the frame's time is added and the steps' taken off.</param>
        /// <param name="dt">The frame's time, seconds.</param>
        /// <param name="size">The flag's step, seconds.</param>
        /// <param name="maxSteps">The most it catches up in one frame; time beyond that is let go (a hitch is not worth a burst of physics).</param>
        /// <param name="stepCost">What one of its steps costs the thread, milliseconds.</param>
        /// <param name="budget">The thread's work for the frame, milliseconds.</param>
        /// <param name="spent">The work planned so far this frame (nearer flags first), milliseconds: its steps are added.</param>
        /// <param name="always">Steps whatever the budget (the placement ghost).</param>
        /// <param name="behind">How far behind the frame its first step starts, seconds (its clock: now - behind).</param>
        /// <param name="waiting">No room for it: it takes no step, and keeps one step's time for when there is.</param>
        /// <returns>The steps due now.</returns>
        public static int Due(ref float pending, float dt, float size, int maxSteps, double stepCost, double budget, ref double spent,
                              bool always, out float behind, out bool waiting)
        {
            waiting = false;
            pending += dt;
            behind = pending;

            // A hair of tolerance: 0.05 s is three steps of 1/60 s, not two and a rounding error.
            int due = Math.Min((int)((pending + 1e-5f) / size), maxSteps);
            if (due <= 0)
                return 0;

            double cost = due * stepCost;
            if (!always && spent > 0.0 && spent + cost > budget)
            {
                waiting = true;
                if (pending > size)
                    pending = size;
                return 0;
            }

            spent += cost;
            pending = Math.Max(0f, pending - due * size);
            if (pending >= size - 1e-5f)
                pending = 0f;
            return due;
        }

        /// <summary>
        /// The time a flag steps this frame while the game is paused and the flags are frozen:
        /// none while it is left alone; while it is handled -- dragged, turned, redesigned -- and
        /// for a moment after (<paramref name="awake"/> seconds still to go), the frame's own,
        /// slowing to nothing over the last <paramref name="fade"/> seconds: a still that comes to
        /// life under the player's hand and settles back into a still, not one that freezes
        /// mid-swing or lets its hoist be dragged away from its cloth.
        /// </summary>
        public static float PausedTime(float frame, float awake, float fade)
        {
            if (awake <= 0f || frame <= 0f)
                return 0f;

            return frame * Math.Min(1f, awake / Math.Max(fade, 1e-4f));
        }
    }

    /// <summary>
    /// Steps the flags' cloth on threads of its own, while the game draws the frame. PURE of
    /// the engine: FlagMesh.Refresh is managed arithmetic on Unity's structs (Vector3.Min, Max,
    /// magnitude, Bounds.SetMinMax are IL, not internal calls -- read from the game's
    /// UnityEngine.dll), and nothing else it does touches the engine.
    /// </summary>
    /// <remarks>
    /// The frame: the renderer waits for the last batch (<see cref="Wait"/>) -- long done, as a
    /// rule -- then reads the props, moves and redesigns cloths, hands the refreshed vertices
    /// to their meshes and draws, then fills the next batch and <see cref="Run"/>s it. So a cloth
    /// is touched by the main thread only between Wait and Run, and by the cloth's threads only
    /// between Run and Wait; every flag is drawn one frame after it was stepped, which nobody
    /// can see in a flag. Unity's own job system came with 2018.1; this is the plain pattern of
    /// workers and events (the game itself runs its simulation on a thread of its own).
    ///
    /// With several threads (<see cref="ThreadsFor"/>: one per six of the machine's, at most
    /// four), each takes the batch's next job until none is left -- a flag's job is its own
    /// cloth and mesh, so they need nothing from each other -- and the last one done says so.
    /// A cloth is stepped by one thread per frame, whichever: the result is the same, bit for
    /// bit.
    ///
    /// If no thread can start, <see cref="Run"/> does the batch at once, on the caller's
    /// thread: slower, the same result.
    /// </remarks>
    public sealed class ClothWorker
    {
        /// <summary>The most threads the cloth takes, whatever the machine.</summary>
        public const int MaxThreads = 4;

        private ClothJob[] _jobs = new ClothJob[32];
        private int _count;

        private Thread[] _threads = new Thread[0];
        private AutoResetEvent[] _go = new AutoResetEvent[0];
        private readonly ManualResetEvent _idle = new ManualResetEvent(true);
        private volatile bool _stop;
        private bool _running, _stuck;

        /// <summary>The next job to take, the threads still working, and the batch's totals (Interlocked).</summary>
        private int _next, _busy;
        private long _work, _ticks, _started;

        /// <summary>
        /// The last batch: the time its jobs took on the cloth's threads (added up), how long it
        /// was out (from Run to the last thread done), and its work in particle steps (sub-steps x
        /// particles).
        /// </summary>
        public double LastMilliseconds { get; private set; }
        public double LastWallMilliseconds { get; private set; }
        public long LastParticleSteps { get; private set; }

        /// <summary>True when the batches run on threads of their own (false: on the caller's).</summary>
        public bool Threaded
        {
            get { return _threads.Length > 0; }
        }

        /// <summary>How many threads step the cloth (0: the caller's).</summary>
        public int Threads
        {
            get { return _threads.Length; }
        }

        /// <summary>
        /// The cloth's threads for a machine of this many (logical) processors: one per six, one
        /// to <see cref="MaxThreads"/>. The game keeps its main, simulation and render threads
        /// busy and other mods have theirs; a 4-core machine gets one, an 8-core, 16-thread one
        /// two.
        /// </summary>
        public static int ThreadsFor(int processors)
        {
            return Math.Max(1, Math.Min(MaxThreads, processors / 6));
        }

        public int Count
        {
            get { return _count; }
        }

        /// <summary>The batch's jobs (after <see cref="Wait"/>, with the thread's results in them).</summary>
        public ClothJob[] Jobs
        {
            get { return _jobs; }
        }

        /// <summary>Starts one thread; false, with the reason, if it could not (the batches then run inline).</summary>
        public bool Start(out string problem)
        {
            return Start(1, out problem);
        }

        /// <summary>
        /// Starts up to <paramref name="threads"/> threads (<see cref="MaxThreads"/> at most); false,
        /// with the reason, if none could start (the batches then run inline). If only some
        /// start, those do the work.
        /// </summary>
        public bool Start(int threads, out string problem)
        {
            problem = null;
            if (_threads.Length > 0)
                return true;

            threads = Math.Max(1, Math.Min(threads, MaxThreads));
            _stop = false;
            var started = new Thread[threads];
            var go = new AutoResetEvent[threads];
            int n = 0;
            for (int i = 0; i < threads; i++)
            {
                try
                {
                    var signal = new AutoResetEvent(false);
                    var thread = new Thread(() => Loop(signal)) { IsBackground = true, Name = "RealFlag cloth " + (n + 1) };
                    thread.Start();
                    go[n] = signal;
                    started[n++] = thread;
                }
                catch (Exception e)
                {
                    problem = e.GetType().Name + ": " + e.Message;
                    break;
                }
            }

            if (n < threads)
            {
                Array.Resize(ref started, n);
                Array.Resize(ref go, n);
            }

            _go = go;
            _threads = started;
            return n > 0;
        }

        /// <summary>
        /// Waits for the threads to finish the batch, then stops them (the city is unloading).
        /// False if they did not end within 2 s: they are background threads, so they end with
        /// the game.
        /// </summary>
        public bool Stop()
        {
            Thread[] threads = _threads;
            if (threads.Length == 0)
                return true;

            bool idle = Wait();
            _stop = true;
            foreach (AutoResetEvent signal in _go)
                signal.Set();

            bool ended = true;
            var clock = Stopwatch.StartNew();
            foreach (Thread thread in threads)
                ended &= thread.Join(Math.Max(0, 2000 - (int)clock.ElapsedMilliseconds));

            _threads = new Thread[0];
            _go = new AutoResetEvent[0];
            if (idle)
                Clear();
            return idle && ended;
        }

        /// <summary>
        /// How long <see cref="Wait"/> waits, milliseconds: a batch takes a few. Past it the thread
        /// is taken to be stuck, and the game must not hang with it.
        /// </summary>
        public const int WaitLimit = 5000;

        /// <summary>
        /// Blocks until the last <see cref="Run"/> is done; at once when nothing is running. False
        /// when the thread has not finished within <see cref="WaitLimit"/>: its jobs are then still
        /// its own, and the caller must leave their cloths alone for good.
        /// </summary>
        public bool Wait()
        {
            if (!_running)
                return true;

            if (_stuck || !_idle.WaitOne(WaitLimit))
            {
                _stuck = true;
                return false;
            }

            _running = false;
            return true;
        }

        /// <summary>A <see cref="Wait"/> ran out: the thread still has its batch, and is never waited for again.</summary>
        public bool Stuck
        {
            get { return _stuck; }
        }

        /// <summary>True while a batch is out on the thread (between <see cref="Run"/> and <see cref="Wait"/>).</summary>
        public bool Running
        {
            get { return _running; }
        }

        /// <summary>Empties the batch (only while nothing is running: after <see cref="Wait"/>).</summary>
        public void Clear()
        {
            if (_running)
                throw new InvalidOperationException("the cloth's batch is still running: Wait first");

            for (int i = 0; i < _count; i++)
                _jobs[i] = default(ClothJob);
            _count = 0;
        }

        public void Add(ref ClothJob job)
        {
            if (_running)
                throw new InvalidOperationException("the cloth's batch is still running: Wait first");

            if (_count == _jobs.Length)
                Array.Resize(ref _jobs, _count * 2);
            _jobs[_count++] = job;
        }

        /// <summary>Starts the batch on the threads (or does it now, without them).</summary>
        public void Run()
        {
            if (_count == 0 || _running)
                return;

            _next = 0;
            _work = 0;
            _ticks = 0;
            _started = Stopwatch.GetTimestamp();

            if (_threads.Length == 0)
            {
                TakeJobs();
                Finish();
                return;
            }

            // As many threads as there are jobs, up to all of them.
            int wake = Math.Min(_threads.Length, _count);
            _busy = wake;
            _idle.Reset();
            _running = true;
            for (int i = 0; i < wake; i++)
                _go[i].Set();
        }

        private void Loop(AutoResetEvent go)
        {
            while (true)
            {
                go.WaitOne();
                if (_stop)
                    break;

                TakeJobs();

                // The last one done ends the batch.
                if (Interlocked.Decrement(ref _busy) == 0)
                {
                    Finish();
                    _idle.Set();
                }
            }
        }

        /// <summary>The batch's next job, until none is left: each is timed, and what it throws is kept in it.</summary>
        private void TakeJobs()
        {
            long work = 0, ticks = 0;
            while (true)
            {
                int i = Interlocked.Increment(ref _next) - 1;
                if (i >= _count)
                    break;

                long start = Stopwatch.GetTimestamp();
                try
                {
                    work += Execute(ref _jobs[i]);
                }
                catch (Exception e)
                {
                    _jobs[i].Failure = e;
                }

                ticks += Stopwatch.GetTimestamp() - start;
            }

            Interlocked.Add(ref _work, work);
            Interlocked.Add(ref _ticks, ticks);
        }

        private void Finish()
        {
            double perTick = 1000.0 / Stopwatch.Frequency;
            LastMilliseconds = Interlocked.Read(ref _ticks) * perTick;
            LastWallMilliseconds = (Stopwatch.GetTimestamp() - _started) * perTick;
            LastParticleSteps = Interlocked.Read(ref _work);
        }

        /// <summary>
        /// One job, on whatever thread calls it: the steps (each split by FlagCloth.SubStepsFor
        /// for the wind at the flag), a cloth that went wrong laid out again, then the mesh.
        /// Returns the work done, in particle steps.
        /// </summary>
        public static long Execute(ref ClothJob job)
        {
            FlagCloth cloth = job.Cloth;
            long work = 0;

            if (job.Steps > 0)
            {
                int sub = cloth.SubStepsFor(FlagWind.SpeedAt(job.Speed, job.Height), job.Size);
                float dt = job.Size / sub;

                for (int s = 0; s < job.Steps; s++)
                {
                    for (int i = 0; i < sub; i++)
                    {
                        FlagAir air = FlagWind.Air(job.Speed, job.Gusts, job.DirX, job.DirZ, job.Height, job.Waviness, job.Length,
                                                   job.Time + s * job.Size + i * dt, job.AnchorX, job.AnchorZ);
                        cloth.Step(dt, ref air);
                    }
                }

                work = (long)job.Steps * sub * cloth.Count;

                if (!cloth.IsFinite())
                {
                    cloth.Reset(job.DirX, job.DirZ);
                    job.WentWrong = true;
                }
            }

            if (job.Mesh != null)
                job.Mesh.Refresh(job.Alpha);

            return work;
        }
    }
}
