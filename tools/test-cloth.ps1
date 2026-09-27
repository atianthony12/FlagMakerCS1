# Offline checks of the flag's physics and data -- the PURE code (FlagCloth, FlagWind,
# FlagNoise, FlagDesign) -- run against the built mod DLL, no game needed:
#
#   dotnet build RealFlag/RealFlag.csproj -t:Compile
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-cloth.ps1 [-Dll RealFlag\obj\Debug\RealFlag.dll]
#
# What is covered, and why:
# - the noise has about unit sigma (FlagNoise.Scale1/Scale3 are measured here), zero mean;
# - the wind follows EN 1991-1-4: the log law (10 m/s at 10 m, held below 5 m, more above),
#   town turbulence (I = 1 / ln(z / 0.3)), the gusts' length scale;
# - the cloth does what flags do: limp in calm air (Beaufort 0), out at a moderate breeze,
#   its ripples running from the hoist to the fly and growing towards the fly, slower than the
#   wind (flags' waves travel at a fraction of the wind speed: Taneda 1968);
# - it holds: finite at 30 m/s, stretched no more than a tenth (the long range attachments and
#   the sub-steps), its hoist exactly where the pole would be, the same run twice the same;
# - its anchor moved a little drags it, a jump carries it;
# - the save's bytes (FlagCodec) and Move It's round trip; colours (hex, HSV, sRGB to linear);
#   a spec from a save or a paste is pulled into range.
# - a flag hoisted from its top (a banner on a crossbar): its frame, hanging and blown; the
#   width and height as seen (FlagSize), linked and not, and the shapes;
# - Move It's pick holds a pointer put on a waving flag while it is clicked (PickHold), and the
#   outline (FlagOutline, from the flag's place and size alone) holds its cloth whatever it does.
# - the cloth's thread (ClothWorker) steps bit for bit as the main thread would, hands back a
#   job that throws, goes on, stops; the plan (ClothPlan) steps every 1/60 s at any frame rate
#   down to 20 fps, catches up at most three steps a frame, and keeps within its budget: the
#   farther flag waits, the nearest and the placement ghost always step.
# Prints the cost of a cloth step in .NET (Unity 5.6's Mono is five times slower; the GPU
# preview's -Bench measures it there).

param(
    [string]$Dll = (Join-Path $PSScriptRoot "..\RealFlag\obj\Debug\RealFlag.dll")
)

$ErrorActionPreference = "Stop"
$managed = "C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed"
if (-not (Test-Path $Dll)) { throw "No DLL at $Dll -- build it first (dotnet build RealFlag/RealFlag.csproj -t:Compile)" }

# Loaded from bytes: Windows' Smart App Control blocks loading a freshly built, unsigned DLL
# by path (0x800711C7), intermittently. The harness below resolves "RealFlag" to this copy.
$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Resolve-Path $Dll)))

$resolver = @'
using System;
using System.IO;
using System.Reflection;
public static class ClothTestResolver
{
    public static Assembly Mod;
    public static string Managed;
    public static void Install() { AppDomain.CurrentDomain.AssemblyResolve += Resolve; }
    static Assembly Resolve(object sender, ResolveEventArgs e)
    {
        string name = new AssemblyName(e.Name).Name;
        if (name == "RealFlag") return Mod;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            if (a.GetName().Name == name) return a;
        string path = Path.Combine(Managed, name + ".dll");
        return File.Exists(path) ? Assembly.Load(File.ReadAllBytes(path)) : null;
    }
}
'@
$rp = New-Object System.CodeDom.Compiler.CompilerParameters
$rp.GenerateInMemory = $true
Add-Type -TypeDefinition $resolver -CompilerParameters $rp
[ClothTestResolver]::Mod = $asm
[ClothTestResolver]::Managed = $managed
[ClothTestResolver]::Install()

$harness = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealFlag.Flags;

public static class ClothTests
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string NoiseStats(int dims)
    {
        double sum = 0, sum2 = 0; int n = 0;
        for (int i = 0; i < 200000; i++)
        {
            float v = dims == 1 ? FlagNoise.Value1(i * 0.137f, 7) : FlagNoise.Value3(i * 0.137f, i * 0.071f + 3.3f, i * 0.029f + 7.1f, 7);
            sum += v; sum2 += v * v; n++;
        }
        double mean = sum / n, sd = Math.Sqrt(sum2 / n - mean * mean);
        return mean.ToString("F3", Inv) + "," + sd.ToString("F3", Inv);
    }

    public static bool NoiseDeterministic()
    {
        return FlagNoise.Value3(12.3f, 4.5f, 6.7f, 17) == FlagNoise.Value3(12.3f, 4.5f, 6.7f, 17);
    }

    /// <summary>Runs a 3 x 2 m flag 10 m up in a wind (m/s at 10 m) for the given seconds, as the mod does.</summary>
    public static FlagCloth Run(float wind, float seconds, float waviness)
    {
        return RunAt(wind, seconds, waviness, FlagCloth.CellsAlong);
    }

    /// <summary>The same at a detail (cells along the longer side).</summary>
    public static FlagCloth RunAt(float wind, float seconds, float waviness, int detail)
    {
        FlagCloth c = new FlagCloth(3f, 2f, FlagFabric.Standard(waviness), 1f, 0f, detail);
        c.GroundY = -12f;
        float height = 12f;
        int sub = c.SubStepsFor(FlagWind.SpeedAt(wind, height), 1f / 60f);
        float dt = 1f / 60f / sub;
        int steps = (int)(seconds / dt);
        for (int s = 0; s < steps; s++)
        {
            FlagAir air = FlagWind.Air(wind, 1f, 1f, 0f, height, waviness, 3f, s * dt, 0f, 0f);
            c.Step(dt, ref air);
        }
        return c;
    }

    /// <summary>Degrees the fly's mean sits below the hoist's middle, and how far out it reaches (x L).</summary>
    public static string Droop(FlagCloth c)
    {
        double fx = 0, fy = 0, fz = 0;
        for (int j = 0; j < c.Rows; j++) { int k = j * c.Columns + c.Columns - 1; fx += c.X[k]; fy += c.Y[k]; fz += c.Z[k]; }
        fx /= c.Rows; fy /= c.Rows; fz /= c.Rows;
        double horiz = Math.Sqrt(fx * fx + fz * fz);
        double droop = Math.Atan2(-0.5 * c.Height - fy, horiz) * 180.0 / Math.PI;
        return droop.ToString("F1", Inv) + "," + (horiz / c.Length).ToString("F2", Inv);
    }

    /// <summary>
    /// The ripples: 20 s of every row's sideways position after 8 s to settle, each particle's
    /// slow drift taken out. Returns the rms at a third and at the fly end over every row, the
    /// waves' speed along the middle row as a share of the wind at the flag, the worst stretch
    /// seen, and the frequency the speed was read at.
    /// </summary>
    public static string Waves(float wind)
    {
        FlagCloth c = new FlagCloth(3f, 2f, FlagFabric.Standard(1f), 1f, 0f);
        float height = 12f;
        c.GroundY = -height;
        int sub = c.SubStepsFor(FlagWind.SpeedAt(wind, height), 1f / 60f);
        float dt = 1f / 60f / sub;
        int settle = (int)(8f / dt), record = (int)(20f / dt);
        int cols = c.Columns, rows = c.Rows;
        int samples = record / sub;
        float[,,] z = new float[samples, rows, cols];
        int s = 0;
        float worst = 1f;
        for (int step = 0; step < settle + record; step++)
        {
            FlagAir air = FlagWind.Air(wind, 1f, 1f, 0f, height, 1f, 3f, step * dt, 0f, 0f);
            c.Step(dt, ref air);
            if (step >= settle && (step - settle) % sub == 0 && s < samples)
            {
                for (int j = 0; j < rows; j++)
                    for (int i = 0; i < cols; i++) z[s, j, i] = c.Z[j * cols + i];
                float st = c.MaxStretch(); if (st > worst) worst = st;
                s++;
            }
        }
        samples = s;

        // Each particle's sideways motion about its 2 s running mean (the gusts' slow push).
        int win = 120;
        float[,,] w = new float[samples, rows, cols];
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
                for (int k = 0; k < samples; k++)
                {
                    int a = Math.Max(0, k - win / 2), b = Math.Min(samples - 1, k + win / 2);
                    double m = 0; for (int q = a; q <= b; q++) m += z[q, j, i]; m /= (b - a + 1);
                    w[k, j, i] = (float)(z[k, j, i] - m);
                }

        // How much a third of the way out and the fly end move, over every row: a flag's
        // flapping grows along it, the fly whipping most.
        int c1 = (cols - 1) / 3;
        double r1 = 0, rFly = 0;
        for (int j = 0; j < rows; j++)
            for (int k = 0; k < samples; k++) { r1 += w[k, j, c1] * w[k, j, c1]; rFly += w[k, j, cols - 1] * w[k, j, cols - 1]; }
        r1 = Math.Sqrt(r1 / (samples * rows)); rFly = Math.Sqrt(rFly / (samples * rows));

        // For the waves' speed: the gusts swing the whole flag round its pole, every column in
        // step, which reads as a wave infinitely fast. Each row's rigid swing about the hoist at
        // each moment (w = theta x, least squares) is taken out, leaving the cloth's own bending
        // waves.
        double xx = 0; for (int i = 0; i < cols; i++) xx += (double)i * i;
        for (int j = 0; j < rows; j++)
            for (int k = 0; k < samples; k++)
            {
                double wx = 0; for (int i = 0; i < cols; i++) wx += w[k, j, i] * (double)i;
                double theta = wx / xx;
                for (int i = 0; i < cols; i++) w[k, j, i] -= (float)(theta * i);
            }

        // The waves' speed, band by band from 1 to 6 Hz (below 1 Hz is the cloth's slow answer to
        // the gusts, not its flutter): each particle's complex amplitude in the band (a DFT over
        // the 20 s), the phase step from one column to the next as the angle of the cross
        // spectrum X(i+1) X(i)* summed over every row and every column from a sixth of the way
        // out, and c = 2 pi f cell / -step. A wave cos(kx - wt) running towards the fly keeps
        // e^(-ikx) under the transform's e^(-iwt): its phase FALLS along the flag, so c is
        // positive for waves running from the hoist to the fly. The bands are weighed by their
        // power. One row at one frequency (the first version of this test) gave 0.24 to 0.32 of
        // the wind at 12 m/s by the length of the record alone.
        int from = Math.Max(1, (cols - 1) / 6);
        double[] re = new double[cols], im = new double[cols];
        double weighed = 0, total = 0, bestPower = 0, bestF = 0;
        double wind12 = FlagWind.SpeedAt(wind, height);
        for (double f = 1.0; f <= 6.0; f += 0.25)
        {
            double power = 0, cre = 0, cim = 0;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 1; i < cols; i++)
                {
                    double r = 0, q = 0;
                    for (int k = 0; k < samples; k++) { double a = -2.0 * Math.PI * f * k / 60.0; r += w[k, j, i] * Math.Cos(a); q += w[k, j, i] * Math.Sin(a); }
                    re[i] = r; im[i] = q; power += r * r + q * q;
                }
                for (int i = from; i < cols - 1; i++)
                {
                    cre += re[i + 1] * re[i] + im[i + 1] * im[i];
                    cim += im[i + 1] * re[i] - re[i + 1] * im[i];
                }
            }

            double step = Math.Atan2(cim, cre);
            double speed = Math.Abs(step) > 1e-9 ? -2.0 * Math.PI * f * c.CellU / step : 1e3;
            weighed += power * speed / wind12;
            total += power;
            if (power > bestPower) { bestPower = power; bestF = f; }
        }

        double share = weighed / total;
        return r1.ToString("F3", Inv) + "," + rFly.ToString("F3", Inv) + "," + share.ToString("F2", Inv) + "," + worst.ToString("F3", Inv) +
               "," + bestF.ToString("F2", Inv);
    }

    static double Reach(FlagCloth c)
    {
        double fx = 0, fz = 0;
        for (int j = 0; j < c.Rows; j++) { int k = j * c.Columns + c.Columns - 1; fx += c.X[k]; fz += c.Z[k]; }
        fx /= c.Rows; fz /= c.Rows;
        return Math.Sqrt(fx * fx + fz * fz);
    }

    /// <summary>
    /// A 3 x 2 m flag flying at 6 m/s, resized in the panel (TakeShape) to 4.5 m at 1:2, and
    /// to 1.5 m at 2:3: how far its fly reaches against before (x the length change), its hoist
    /// on the pole, then 2 s flying on: finite, and the worst stretch after the first half second.
    /// </summary>
    public static string Resize(float length, int proportion)
    {
        FlagCloth a = Run(6f, 6f, 1f);
        double before = Reach(a);
        float height = length * proportion / 1000f;
        FlagCloth b = new FlagCloth(length, height, FlagFabric.Standard(1f), 1f, 0f);
        b.GroundY = -12f;
        b.TakeShape(a);
        double ratio = Reach(b) / before / (length / 3f);
        bool hoist = true;
        for (int j = 0; j < b.Rows; j++)
        {
            int k = j * b.Columns;
            if (b.X[k] != 0f || b.Z[k] != 0f || Math.Abs(b.Y[k] + j * b.CellV) > 1e-5f) hoist = false;
        }
        int sub = b.SubStepsFor(FlagWind.SpeedAt(6f, 12f), 1f / 60f);
        float dt = 1f / 60f / sub, worst = 1f;
        for (int s = 0; s < 120 * sub; s++)
        {
            FlagAir air = FlagWind.Air(6f, 1f, 1f, 0f, 12f, 1f, length, 6f + s * dt, 0f, 0f);
            b.Step(dt, ref air);
            if (s >= 30 * sub) { float st = b.MaxStretch(); if (st > worst) worst = st; }
        }
        return ratio.ToString("F2", Inv) + "," + hoist + "," + b.IsFinite() + "," + worst.ToString("F3", Inv) + "," + b.Columns + "x" + b.Rows;
    }

    public static string Storm()
    {
        FlagCloth c = Run(30f, 20f, 2f);
        return c.IsFinite() + "," + c.MaxStretch().ToString("F3", Inv);
    }

    // ---- the cloth against itself --------------------------------------------------------

    /// <summary>The mesh's triangles, as FlagMesh makes them: (k, k+1, k+cols) and (k+1, k+cols+1, k+cols).</summary>
    static int[] Triangles(int cols, int rows)
    {
        List<int> t = new List<int>();
        for (int j = 0; j < rows - 1; j++)
            for (int i = 0; i < cols - 1; i++)
            {
                int k = j * cols + i;
                t.Add(k); t.Add(k + 1); t.Add(k + cols);
                t.Add(k + 1); t.Add(k + cols + 1); t.Add(k + cols);
            }
        return t.ToArray();
    }

    /// <summary>
    /// How many pairs of the drawn cloth's triangles cross each other, among those sharing no
    /// corner: each triangle's edges against the other (Moeller and Trumbore's test on a
    /// segment), after their boxes.
    /// </summary>
    static int CrossingPairs(FlagCloth c, int[] tri)
    {
        int n = tri.Length / 3;
        float[] box = new float[n * 6];
        for (int t = 0; t < n; t++)
        {
            int a = tri[3 * t], b = tri[3 * t + 1], d = tri[3 * t + 2];
            box[6 * t] = Math.Min(c.X[a], Math.Min(c.X[b], c.X[d])); box[6 * t + 1] = Math.Max(c.X[a], Math.Max(c.X[b], c.X[d]));
            box[6 * t + 2] = Math.Min(c.Y[a], Math.Min(c.Y[b], c.Y[d])); box[6 * t + 3] = Math.Max(c.Y[a], Math.Max(c.Y[b], c.Y[d]));
            box[6 * t + 4] = Math.Min(c.Z[a], Math.Min(c.Z[b], c.Z[d])); box[6 * t + 5] = Math.Max(c.Z[a], Math.Max(c.Z[b], c.Z[d]));
        }
        int count = 0;
        for (int p = 0; p < n; p++)
        {
            int pa = tri[3 * p], pb = tri[3 * p + 1], pc = tri[3 * p + 2];
            for (int q = p + 1; q < n; q++)
            {
                if (box[6 * p] > box[6 * q + 1] || box[6 * q] > box[6 * p + 1] || box[6 * p + 2] > box[6 * q + 3] ||
                    box[6 * q + 2] > box[6 * p + 3] || box[6 * p + 4] > box[6 * q + 5] || box[6 * q + 4] > box[6 * p + 5]) continue;
                int qa = tri[3 * q], qb = tri[3 * q + 1], qc = tri[3 * q + 2];
                if (qa == pa || qa == pb || qa == pc || qb == pa || qb == pb || qb == pc || qc == pa || qc == pb || qc == pc) continue;
                if (Seg(c, pa, pb, qa, qb, qc) || Seg(c, pb, pc, qa, qb, qc) || Seg(c, pc, pa, qa, qb, qc) ||
                    Seg(c, qa, qb, pa, pb, pc) || Seg(c, qb, qc, pa, pb, pc) || Seg(c, qc, qa, pa, pb, pc)) count++;
            }
        }
        return count;
    }

    static bool Seg(FlagCloth c, int p0, int p1, int t0, int t1, int t2)
    {
        double ox = c.X[p0], oy = c.Y[p0], oz = c.Z[p0];
        double dx = c.X[p1] - ox, dy = c.Y[p1] - oy, dz = c.Z[p1] - oz;
        double e1x = c.X[t1] - c.X[t0], e1y = c.Y[t1] - c.Y[t0], e1z = c.Z[t1] - c.Z[t0];
        double e2x = c.X[t2] - c.X[t0], e2y = c.Y[t2] - c.Y[t0], e2z = c.Z[t2] - c.Z[t0];
        double px = dy * e2z - dz * e2y, py = dz * e2x - dx * e2z, pz = dx * e2y - dy * e2x;
        double det = e1x * px + e1y * py + e1z * pz;
        if (Math.Abs(det) < 1e-14) return false;
        double inv = 1.0 / det;
        double sx = ox - c.X[t0], sy = oy - c.Y[t0], sz = oz - c.Z[t0];
        double u = (sx * px + sy * py + sz * pz) * inv;
        if (u < 0 || u > 1) return false;
        double qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
        double v = (dx * qx + dy * qy + dz * qz) * inv;
        if (v < 0 || u + v > 1) return false;
        double t = (e2x * qx + e2y * qy + e2z * qz) * inv;
        return t >= 0 && t <= 1;
    }

    /// <summary>
    /// A flag 10 m up at a detail, with or without self-collision: after 8 s to settle, every
    /// 0.1 s for the given seconds, how many of its pairs of triangles cross. Returns the share
    /// of the samples with any, the mean, the most, and the worst stretch over the record.
    /// </summary>
    public static string Crossings(float wind, float waviness, int detail, bool collide, float length, float height, float seconds)
    {
        FlagCloth c = new FlagCloth(length, height, FlagFabric.Standard(waviness), 1f, 0f, detail);
        c.SelfCollision = collide;
        float up = 10f + height;
        c.GroundY = -up;
        int sub = c.SubStepsFor(FlagWind.SpeedAt(wind, up), 1f / 60f);
        float dt = 1f / 60f / sub;
        int settle = 8 * 60, record = (int)(seconds * 60f);
        int[] tris = Triangles(c.Columns, c.Rows);
        int samples = 0, any = 0, most = 0;
        long total = 0;
        float worst = 1f;
        for (int step = 0; step < settle + record; step++)
        {
            for (int i = 0; i < sub; i++)
            {
                FlagAir air = FlagWind.Air(wind, 1f, 1f, 0f, up, waviness, length, step / 60f + i * dt, 0f, 0f);
                c.Step(dt, ref air);
            }
            if (step < settle) continue;
            float st = c.MaxStretch(); if (st > worst) worst = st;
            if ((step - settle) % 6 != 0) continue;
            int n = CrossingPairs(c, tris);
            samples++; if (n > 0) any++; total += n; if (n > most) most = n;
        }
        return ((double)any / samples).ToString("F3", Inv) + "," + ((double)total / samples).ToString("F2", Inv) + "," + most + "," +
               worst.ToString("F3", Inv) + "," + c.Columns + "x" + c.Rows;
    }

    // ---- the details -----------------------------------------------------------------------

    static double MeanSpeed(FlagCloth c)
    {
        double s = 0;
        for (int k = 0; k < c.Count; k++) s += Math.Sqrt(c.Vx[k] * c.Vx[k] + c.Vy[k] * c.Vy[k] + c.Vz[k] * c.Vz[k]);
        return s / c.Count;
    }

    /// <summary>
    /// A 3 x 2 m flag flying at 6 m/s at one detail, made again at another in its shape and
    /// motion (TakeShape, keeping the motion: the renderer's change of detail): its reach and its
    /// particles' mean speed against before, then 2 s on: finite, the worst stretch after 0.5 s.
    /// </summary>
    public static string ChangeDetail(int from, int to)
    {
        FlagCloth a = RunAt(6f, 6f, 1f, from);
        FlagCloth b = new FlagCloth(a.Length, a.Height, a.Fabric, 1f, 0f, to);
        b.GroundY = a.GroundY;
        b.TakeShape(a, true);
        double reach = Reach(b) / Reach(a), speed = MeanSpeed(b) / MeanSpeed(a);
        int sub = b.SubStepsFor(FlagWind.SpeedAt(6f, 12f), 1f / 60f);
        float dt = 1f / 60f / sub, worst = 1f;
        for (int s = 0; s < 120 * sub; s++)
        {
            FlagAir air = FlagWind.Air(6f, 1f, 1f, 0f, 12f, 1f, 3f, 6f + s * dt, 0f, 0f);
            b.Step(dt, ref air);
            if (s >= 30 * sub) { float st = b.MaxStretch(); if (st > worst) worst = st; }
        }
        return reach.ToString("F2", Inv) + "," + speed.ToString("F2", Inv) + "," + b.IsFinite() + "," + worst.ToString("F3", Inv) + "," +
               a.Columns + "x" + a.Rows + "," + b.Columns + "x" + b.Rows;
    }

    /// <summary>The grid at each detail for a flag of this size.</summary>
    public static string Grids(float length, float height)
    {
        string s = "";
        foreach (int d in FlagCloth.Details)
        {
            int c, r;
            FlagCloth.GridFor(length, height, d, out c, out r);
            s += (s.Length > 0 ? " " : "") + c + "x" + r;
        }
        return s;
    }

    public static bool HoistHeld()
    {
        FlagCloth c = Run(12f, 6f, 1f);
        for (int j = 0; j < c.Rows; j++)
        {
            int k = j * c.Columns;
            if (c.X[k] != 0f || c.Z[k] != 0f || Math.Abs(c.Y[k] + j * c.CellV) > 1e-5f) return false;
        }
        return true;
    }

    public static bool Deterministic()
    {
        FlagCloth a = Run(7f, 4f, 1f), b = Run(7f, 4f, 1f);
        for (int k = 0; k < a.Count; k++)
            if (a.X[k] != b.X[k] || a.Y[k] != b.Y[k] || a.Z[k] != b.Z[k]) return false;
        return true;
    }

    /// <summary>
    /// A 5 cm move of the anchor leaves the fly behind (dragged whole); of a 0.5 m move only
    /// half a cell drags it (FlagCloth.MaxDragCells), the rest carries it; a 50 m jump carries
    /// it whole (same shape). Returns the fly's move relative to the anchor in each, and half a
    /// cell.
    /// </summary>
    public static string MoveAnchor()
    {
        FlagCloth c = Run(0f, 3f, 1f);
        int fly = c.Count - 1;
        float before = c.X[fly];
        c.MoveAnchor(0.05f, 0f, 0f);
        float small = c.X[fly] - before;
        before = c.X[fly];
        c.MoveAnchor(0.5f, 0f, 0f);
        float capped = c.X[fly] - before;
        float kept = c.X[fly];
        c.MoveAnchor(50f, 0f, 0f);
        float jumped = c.X[fly] - kept;
        float half = FlagCloth.MaxDragCells * Math.Min(c.CellU, c.CellV);
        return small.ToString("F3", Inv) + "," + capped.ToString("F3", Inv) + "," + jumped.ToString("F3", Inv) + "," + half.ToString("F3", Inv);
    }

    /// <summary>
    /// The height changed fast (the panel's slider, the gizmo): a flag of the given length at a
    /// detail, 8 s in a light wind 22 m up, its anchor moved "total" metres up or down over
    /// "frames" frames, then left for 6 s: the pairs of triangles crossing then (tangled: the
    /// first version left 42-70), and the worst stretch at the end.
    /// </summary>
    public static string Hoisted(float length, int detail, float wind, float total, int frames)
    {
        FlagCloth c = new FlagCloth(length, length * 0.667f, FlagFabric.Standard(1f), 1f, 0f, detail);
        float height = 22f;
        c.GroundY = -height;
        int[] tri = Triangles(c.Columns, c.Rows);
        float t = 0f;
        for (int f = 0; f < 480 + frames + 360; f++)
        {
            if (f >= 480 && f < 480 + frames)
            {
                c.MoveAnchor(0f, total / frames, 0f);
                height += total / frames;
                c.GroundY = -height;
            }
            int sub = c.SubStepsFor(FlagWind.SpeedAt(wind, height), 1f / 60f);
            float dt = 1f / 60f / sub;
            for (int i = 0; i < sub; i++)
            {
                FlagAir air = FlagWind.Air(wind, 1.35f, 1f, 0f, height, 1f, length, t + i * dt, 0f, 0f);
                c.Step(dt, ref air);
            }
            t += 1f / 60f;
        }
        return CrossingPairs(c, tri) + "," + c.MaxStretch().ToString("F3", Inv) + "," + c.Columns + "x" + c.Rows;
    }

    /// <summary>
    /// The pick volumes' arithmetic (FlagPicking): the closest points of two segments, crossing
    /// and parallel; a ray entering a turned box, missing it, and starting inside it.
    /// </summary>
    public static string PickMaths()
    {
        float s, t;
        float d1 = PickShape.ClosestPoints(new UnityEngine.Vector3(-1f, 0f, 0f), new UnityEngine.Vector3(1f, 0f, 0f),
                                           new UnityEngine.Vector3(0f, -1f, 2f), new UnityEngine.Vector3(0f, 1f, 2f), out s, out t);
        bool crossing = Math.Abs(d1 - 4f) < 1e-5f && Math.Abs(s - 0.5f) < 1e-5f && Math.Abs(t - 0.5f) < 1e-5f;
        float d2 = PickShape.ClosestPoints(new UnityEngine.Vector3(0f, 0f, 0f), new UnityEngine.Vector3(1f, 0f, 0f),
                                           new UnityEngine.Vector3(3f, 1f, 0f), new UnityEngine.Vector3(5f, 1f, 0f), out s, out t);
        bool apart = Math.Abs(d2 - 5f) < 1e-5f && Math.Abs(s - 1f) < 1e-5f && Math.Abs(t) < 1e-5f;

        // A box 2 x 4 x 0.2 round (10, 0, 0), turned 45 degrees about y.
        float r = 0.70710678f;
        var u = new UnityEngine.Vector3(r, 0f, r);
        var v = new UnityEngine.Vector3(0f, 1f, 0f);
        var n = new UnityEngine.Vector3(-r, 0f, r);
        var centre = new UnityEngine.Vector3(10f, 0f, 0f);
        float te;
        bool enters = PickShape.SlabEntry(new UnityEngine.Vector3(0f, 0f, 0f), new UnityEngine.Vector3(20f, 0f, 0f), centre, u, v, n, 1f, 2f, 0.1f, out te);
        // Along +x through the centre, the box's thin side is met sqrt(2) * 0.1 before it: t ~ 0.5 - 0.0071.
        bool entry = enters && Math.Abs(te - (0.5f - 0.1f * 1.41421356f / 20f)) < 1e-4f;
        bool misses = !PickShape.SlabEntry(new UnityEngine.Vector3(0f, 5f, 0f), new UnityEngine.Vector3(20f, 0f, 0f), centre, u, v, n, 1f, 2f, 0.1f, out te);
        bool inside = PickShape.SlabEntry(centre, new UnityEngine.Vector3(0f, 0f, 1f), centre, u, v, n, 1f, 2f, 0.1f, out te) && te == 0f;
        return crossing + "," + apart + "," + entry + "," + misses + "," + inside;
    }

    static ClothJob Job(FlagCloth c, float t, int steps)
    {
        ClothJob j = new ClothJob();
        j.Cloth = c; j.Steps = steps; j.Size = 1f / 60f; j.Time = t; j.Alpha = 1f;
        j.Speed = 12f; j.Gusts = 1f; j.DirX = 1f; j.DirZ = 0f; j.Height = 12f; j.Waviness = 1f; j.Length = 3f;
        return j;
    }

    /// <summary>
    /// 100 frames of three steps at 12 m/s (two sub-steps a step) on the cloth's thread, and the
    /// same on this one meanwhile: the same, bit for bit? Then a job that throws (caught, handed
    /// back, the thread goes on) and the thread stopped.
    /// </summary>
    public static string Threaded()
    {
        ClothWorker worker = new ClothWorker();
        string problem;
        if (!worker.Start(out problem)) return "no thread: " + problem;
        FlagCloth a = new FlagCloth(3f, 2f, FlagFabric.Standard(1f), 1f, 0f), b = new FlagCloth(3f, 2f, FlagFabric.Standard(1f), 1f, 0f);
        a.GroundY = -12f; b.GroundY = -12f;
        float t = 0f;
        for (int frame = 0; frame < 100; frame++)
        {
            ClothJob ja = Job(a, t, 3), jb = Job(b, t, 3);
            worker.Add(ref ja);
            worker.Run();
            ClothWorker.Execute(ref jb);
            worker.Wait();
            if (worker.Jobs[0].Failure != null) return "threw: " + worker.Jobs[0].Failure.Message;
            worker.Clear();
            t += 3f / 60f;
        }
        bool same = true;
        for (int k = 0; k < a.Count; k++) same &= a.X[k] == b.X[k] && a.Y[k] == b.Y[k] && a.Z[k] == b.Z[k];

        ClothJob broken = Job(null, t, 1);
        worker.Add(ref broken);
        worker.Run(); worker.Wait();
        bool caught = worker.Jobs[0].Failure is NullReferenceException;
        worker.Clear();

        ClothJob again = Job(a, t, 1);
        worker.Add(ref again);
        worker.Run(); worker.Wait();
        bool goesOn = worker.Jobs[0].Failure == null && worker.LastParticleSteps == 2L * a.Count;
        worker.Clear();

        bool stopped = worker.Stop() && !worker.Threaded;
        return same + "," + caught + "," + goesOn + "," + stopped;
    }

    /// <summary>
    /// Four threads, eight flags at every detail in winds of 0 to 28 m/s, 60 frames of two steps:
    /// each the same as stepped here, bit for bit, whichever thread took it; the batch's work
    /// adds up; the threads stop.
    /// </summary>
    public static string Pool()
    {
        ClothWorker worker = new ClothWorker();
        string problem;
        if (!worker.Start(4, out problem)) return "no threads: " + problem;
        int n = 8;
        FlagCloth[] a = new FlagCloth[n], b = new FlagCloth[n];
        for (int i = 0; i < n; i++)
        {
            int d = FlagCloth.Details[i % FlagCloth.Details.Length];
            a[i] = new FlagCloth(3f, 2f, FlagFabric.Standard(1f), 1f, 0f, d); a[i].GroundY = -12f;
            b[i] = new FlagCloth(3f, 2f, FlagFabric.Standard(1f), 1f, 0f, d); b[i].GroundY = -12f;
        }
        float t = 0f;
        bool counted = true;
        for (int frame = 0; frame < 60; frame++)
        {
            for (int i = 0; i < n; i++) { ClothJob j = Job(a[i], t, 2); j.Speed = 4f * i; worker.Add(ref j); }
            worker.Run();
            long expect = 0;
            for (int i = 0; i < n; i++) { ClothJob j = Job(b[i], t, 2); j.Speed = 4f * i; expect += ClothWorker.Execute(ref j); }
            worker.Wait();
            for (int i = 0; i < n; i++) if (worker.Jobs[i].Failure != null) return "threw: " + worker.Jobs[i].Failure.Message;
            if (worker.LastParticleSteps != expect) counted = false;
            worker.Clear();
            t += 2f / 60f;
        }
        bool same = true;
        for (int i = 0; i < n; i++)
            for (int k = 0; k < a[i].Count; k++) same &= a[i].X[k] == b[i].X[k] && a[i].Y[k] == b[i].Y[k] && a[i].Z[k] == b[i].Z[k];
        int threads = worker.Threads;
        bool stopped = worker.Stop() && !worker.Threaded;
        return same + "," + counted + "," + threads + "," + stopped;
    }

    /// <summary>A flag's steps over some seconds of frames at a steady rate: total, most in a frame, alpha within 0..1 throughout.</summary>
    public static string Frames(float fps, float seconds)
    {
        float size = 1f / 60f, pending = 0f;
        int steps = 0, most = 0;
        bool alphaOk = true;
        int frames = (int)Math.Round(seconds * fps);
        for (int i = 0; i < frames; i++)
        {
            double spent = 0.0;
            float behind;
            bool waiting;
            int due = ClothPlan.Due(ref pending, 1f / fps, size, 3, 0.3, 8.0, ref spent, false, out behind, out waiting);
            steps += due;
            if (due > most) most = due;
            float alpha = pending / size;
            if (alpha < 0f || alpha > 1f + 1e-4f || waiting || behind < due * size - 1e-6f) alphaOk = false;
        }
        return steps + "," + most + "," + alphaOk;
    }

    /// <summary>
    /// Three flags whose step costs 3 ms against an 8 ms budget: the third waits, its time held at
    /// one step; the next frame, alone, it takes two. A lone flag over budget still steps; the
    /// ghost steps whatever is spent.
    /// </summary>
    public static string Budget()
    {
        float size = 1f / 60f, a = 0f, b = 0f, c = 0f, behind;
        bool wa, wb, wc;
        double spent = 0.0;
        int da = ClothPlan.Due(ref a, size, size, 3, 3.0, 8.0, ref spent, false, out behind, out wa);
        int db = ClothPlan.Due(ref b, size, size, 3, 3.0, 8.0, ref spent, false, out behind, out wb);
        int dc = ClothPlan.Due(ref c, size, size, 3, 3.0, 8.0, ref spent, false, out behind, out wc);
        bool held = wc && dc == 0 && Math.Abs(c - size) < 1e-6f;

        spent = 0.0;
        int next = ClothPlan.Due(ref c, size, size, 3, 3.0, 8.0, ref spent, false, out behind, out wc);

        float lone = 0f;
        spent = 0.0;
        int big = ClothPlan.Due(ref lone, size, size, 3, 20.0, 8.0, ref spent, false, out behind, out wa);

        float ghost = 0f;
        spent = 7.9;
        int g = ClothPlan.Due(ref ghost, size, size, 3, 3.0, 8.0, ref spent, true, out behind, out wa);
        return da + "," + db + "," + held + "," + next + "," + big + "," + g;
    }

    /// <summary>
    /// Flags at 10 fps on 4 threads for 100 frames, planned as FlagRenderer.Plan does -- nearest
    /// first, or (waitedFirst) the flags that waited last frame before the rest -- against the
    /// first rule's budget (8 ms a thread whatever the frame) or ClothPlan.BudgetFor's. Steps of
    /// 4.56 ms (a 3 m flag at Ultra in 7 m/s: two sub-steps of 1014 particles at 2.25 us) and,
    /// with a big one, 2.6 ms for the second nearest (the 11 m flag: one sub-step of 1148): the
    /// author's five flags of 2026-09-26. The fewest and the most steps a flag took.
    /// </summary>
    public static string Turns(int flags, bool withBig, bool fixedBudget, bool waitedFirst)
    {
        float size = 1f / 60f, frame = 0.1f, behind;
        float[] pending = new float[flags];
        bool[] waiting = new bool[flags];
        bool[] waited = new bool[flags];
        int[] steps = new int[flags];
        double budget = 4 * (fixedBudget ? 8.0 : ClothPlan.BudgetFor(frame, size, 3, 8.0));
        for (int f = 0; f < 100; f++)
        {
            double spent = 0.0;
            for (int i = 0; i < flags; i++) waited[i] = waiting[i];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < flags; i++)
                {
                    if (waitedFirst ? waited[i] != (pass == 0) : pass == 1) continue;
                    double cost = withBig && i == 1 ? 2.6 : 4.56;
                    steps[i] += ClothPlan.Due(ref pending[i], frame, size, 3, cost, budget, ref spent, false, out behind, out waiting[i]);
                }
            }
        }
        int least = int.MaxValue, most = 0;
        for (int i = 0; i < flags; i++) { least = Math.Min(least, steps[i]); most = Math.Max(most, steps[i]); }
        return least + "," + most;
    }

    /// <summary>A 1/60 s step as the mod takes it (ClothWorker.Execute: its sub-steps included), microseconds.</summary>
    public static double MicrosecondsPerStep(float wind)
    {
        return MicrosecondsPerStepAt(wind, FlagCloth.CellsAlong);
    }

    /// <summary>The same at a detail, after 8 s to settle (hanging in folds in calm air, flying in a wind).</summary>
    public static double MicrosecondsPerStepAt(float wind, int detail)
    {
        FlagCloth c = new FlagCloth(3f, 2f, FlagFabric.Standard(1f), 1f, 0f, detail);
        c.GroundY = -12f;
        float dt = 1f / 60f;
        for (int s = 0; s < 480; s++) { ClothJob j = Job(c, s * dt, 1); j.Speed = wind; ClothWorker.Execute(ref j); }
        Stopwatch sw = Stopwatch.StartNew();
        int n = 1200;
        for (int s = 0; s < n; s++) { ClothJob j = Job(c, s * dt, 1); j.Speed = wind; ClothWorker.Execute(ref j); }
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds * 1000.0 / n;
    }

    public static string Codec()
    {
        var list = new List<KeyValuePair<uint, FlagSpec>>();
        FlagSpec a = FlagSpec.Default;
        FlagSpec b = FlagSpec.Default;
        b.Pattern = FlagPattern.StripesAndCanton; b.Colour1 = 0xB22234; b.Colour2 = 0xFFFFFF; b.Colour3 = 0x3C3B6E;
        b.Length = 4.75f; b.Proportion = 526; b.Waviness = 1.35f; b.Name = "Town hall";
        list.Add(new KeyValuePair<uint, FlagSpec>(12, a));
        list.Add(new KeyValuePair<uint, FlagSpec>(65535, b));
        byte[] bytes = FlagCodec.EncodeAll(list);
        string problem;
        var back = FlagCodec.DecodeAll(bytes, out problem);
        bool all = back != null && back.Count == 2 && back[1].Key == 65535 && back[1].Value.Name == "Town hall" &&
                   back[1].Value.Colour3 == 0x3C3B6E && back[1].Value.Proportion == 526 && Math.Abs(back[1].Value.Length - 4.75f) < 1e-6f &&
                   back[0].Value.Pattern == FlagPattern.Default;
        FlagSpec one;
        bool single = FlagCodec.TryDecodeOne(FlagCodec.EncodeOne(b), out one) && one.Name == "Town hall" && one.Pattern == b.Pattern;
        byte[] garbage = { 1, 255, 255, 255, 127 };
        var none = FlagCodec.DecodeAll(garbage, out problem);
        byte[] future = { 9, 0, 0, 0, 0 };
        string futureProblem;
        var later = FlagCodec.DecodeAll(future, out futureProblem);
        return all + "," + single + "," + (none == null) + "," + (later == null && futureProblem.Contains("newer"));
    }

    /// <summary>
    /// Format 2 (a country's code at the end): a country's flag round trips with its code and
    /// its own proportions (whatever the spec said); a format-1 save -- written here byte by
    /// byte as the first version wrote it -- still reads, as a pattern flag; a code this version
    /// does not know is kept.
    /// </summary>
    public static string CodecCountries()
    {
        FlagSpec us = FlagSpec.Default;
        us.Country = "US"; us.Proportion = 667; us.Length = 5f; us.Name = "Embassy";
        FlagSpec back;
        bool round = FlagCodec.TryDecodeOne(FlagCodec.EncodeOne(us), out back) && back.Country == "us" && back.Proportion == 527 &&
                     back.Name == "Embassy" && Math.Abs(back.Length - 5f) < 1e-6f;

        var old = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(old);
        w.Write((byte)1); w.Write(1);                  // format 1, one flag
        w.Write(7u);                                     // prop 7
        w.Write((byte)FlagPattern.Saltire); w.Write(0x0065BDu); w.Write(0xFFFFFFu); w.Write(0xFFFFFFu);
        w.Write(3f); w.Write((short)600); w.Write(1.2f);
        w.Write((byte)2); w.Write((byte)'S'); w.Write((byte)'c');
        w.Flush();
        string problem;
        var list = FlagCodec.DecodeAll(old.ToArray(), out problem);
        bool v1 = list != null && list.Count == 1 && list[0].Key == 7 && list[0].Value.Pattern == FlagPattern.Saltire &&
                  list[0].Value.Country == "" && list[0].Value.Proportion == 600 && list[0].Value.Name == "Sc";

        FlagSpec strange = FlagSpec.Default;
        strange.Country = "zz-later"; strange.Proportion = 500;
        FlagSpec kept;
        bool unknown = FlagCodec.TryDecodeOne(FlagCodec.EncodeOne(strange), out kept) && kept.Country == "zz-later" && kept.Proportion == 500;
        return round + "," + v1 + "," + unknown + "," + FlagCodec.Version;
    }

    /// <summary>The table: its size, unique normalised codes, proportions in range, lookups both ways, the search.</summary>
    public static string Countries()
    {
        int n = FlagCountries.Count;
        var seen = new HashSet<string>();
        bool codes = true, proportions = true, lookups = true;
        for (int i = 1; i <= n; i++)
        {
            FlagCountry c = FlagCountries.At(i);
            if (c == null || !seen.Add(c.Code) || FlagCountries.Normalise(c.Code) != c.Code) codes = false;
            if (c != null && (c.Proportion < 200 || c.Proportion > 2000)) proportions = false;
            if (c != null && FlagCountries.NumberOf(c.Code.ToUpperInvariant()) != i) lookups = false;
        }
        bool none = FlagCountries.At(0) == null && FlagCountries.NumberOf("") == 0 && FlagCountries.NumberOf("xx") == 0;
        string found = First("ivory") + " " + First("united k") + " " + First("gb-sct") + " " + First("C" + (char)0xF4 + "te") + " " + First("cote") + " " + First("sao tome");
        bool sorted = true;
        FlagCountry[] byName = FlagCountries.ByName;
        for (int i = 1; i < byName.Length; i++) if (string.CompareOrdinal(byName[i - 1].Folded, byName[i].Folded) > 0) sorted = false;
        return n + "," + codes + "," + proportions + "," + lookups + "," + none + "," + sorted + "|" + found + "|" +
               FlagCountries.Find("np").Proportion + " " + FlagCountries.Find("qa").Proportion + " " + FlagCountries.Find("ch").Proportion + " " +
               FlagCountries.Find("sj").Image + " " + FlagProportion.Name(FlagCountries.Find("qa").Proportion) + " " + FlagProportion.Name(667);
    }

    static string First(string query)
    {
        foreach (FlagCountry c in FlagCountries.ByName) if (FlagCountries.Matches(c, query)) return c.Code;
        return "-";
    }

    /// <summary>Every image the table names, as the cloth's and as the thumbnail, among the DLL's resources.</summary>
    public static string Images(string[] resources)
    {
        var have = new HashSet<string>(resources);
        var images = new HashSet<string>();
        int missing = 0;
        for (int i = 1; i <= FlagCountries.Count; i++)
        {
            string image = FlagCountries.At(i).Image;
            if (!images.Add(image)) continue;
            if (!have.Contains("RealFlag.Flags." + image + ".png")) missing++;
            if (!have.Contains("RealFlag.Flags.Small." + image + ".png")) missing++;
        }
        return images.Count + "," + missing;
    }

    public static string Sanitise()
    {
        FlagSpec s = FlagSpec.Default;
        s.Pattern = 99; s.Length = 500f; s.Proportion = 5; s.Waviness = float.NaN; s.Colour1 = 0xFF123456; s.Name = null;
        FlagSpec t = s.Sanitised();
        return t.Pattern + "," + t.Length.ToString("F1", Inv) + "," + t.Proportion + "," + t.Waviness.ToString("F1", Inv) + "," +
               FlagColours.Hex(t.Colour1) + "," + (t.Name == "");
    }

    // ---- tilted hoists, the pole, the drawn surface, the paused flag ------------------------

    static bool HoistOnLine(FlagCloth c)
    {
        for (int j = 0; j < c.Rows; j++)
        {
            int k = j * c.Columns;
            float d = j * c.CellV;
            if (Math.Abs(c.X[k] - d * c.HoistX) > 1e-4f || Math.Abs(c.Y[k] - d * c.HoistY) > 1e-4f || Math.Abs(c.Z[k] - d * c.HoistZ) > 1e-4f)
                return false;
        }
        return true;
    }

    /// <summary>
    /// The frames: no tilt is the old hoist (straight down, the fly along the heading); a 90 deg
    /// tilt lays the hoist level, back from the fly, and hangs the fly down; a lean turns the
    /// pole's top to the side; hoist and fly are unit and square to each other always.
    /// </summary>
    public static string Frames()
    {
        float hx, hy, hz, fx, fy, fz;
        bool square = true, unit = true;
        float[][] cases = { new float[] { 0f, 0f, 0f }, new float[] { 0f, 90f, 0f }, new float[] { 1.5707964f, 0f, 30f },
                            new float[] { 0.7f, 45f, 30f }, new float[] { 2f, -60f, -80f }, new float[] { 4f, 90f, 90f } };
        foreach (float[] c in cases)
        {
            FlagOrientation.Frame(c[0], c[1], c[2], out hx, out hy, out hz, out fx, out fy, out fz);
            if (Math.Abs(hx * fx + hy * fy + hz * fz) > 1e-5) square = false;
            if (Math.Abs(hx * hx + hy * hy + hz * hz - 1) > 1e-5 || Math.Abs(fx * fx + fy * fy + fz * fz - 1) > 1e-5) unit = false;
        }
        FlagOrientation.Frame(0f, 0f, 0f, out hx, out hy, out hz, out fx, out fy, out fz);
        bool straight = Near(hx, 0) && Near(hy, -1) && Near(hz, 0) && Near(fx, 1) && Near(fy, 0) && Near(fz, 0);
        FlagOrientation.Frame(0f, 90f, 0f, out hx, out hy, out hz, out fx, out fy, out fz);
        bool level = Near(hx, -1) && Near(hy, 0) && Near(hz, 0) && Near(fx, 0) && Near(fy, -1) && Near(fz, 0);
        // Heading 90 deg: the flag along +z, its side (X x Y) along -x; leaning 30 deg that way.
        FlagOrientation.Frame(1.5707964f, 0f, 30f, out hx, out hy, out hz, out fx, out fy, out fz);
        bool lean = Near(hx, 0.5f) && Near(hy, -0.8660254f) && Near(hz, 0) && Near(fx, 0) && Near(fy, 0) && Near(fz, 1);
        return square + "," + unit + "," + straight + "," + level + "," + lean;
    }

    static bool Near(float a, float b) { return Math.Abs(a - b) < 1e-4f; }

    /// <summary>
    /// A 3 x 2 m flag on a hoist tilted (and leaned), 10 m up, in a wind along its heading:
    /// finite, its hoist exactly on the tilted line, the worst stretch after 2 s, and how far its
    /// fly reaches from the hoist line (x L).
    /// </summary>
    public static string Tilted(float tilt, float lean, float wind, float seconds)
    {
        float hx, hy, hz, fx, fy, fz;
        FlagOrientation.Frame(0f, tilt, lean, out hx, out hy, out hz, out fx, out fy, out fz);
        FlagCloth c = new FlagCloth(3f, 2f, FlagFabric.Standard(1f), 1f, 0f, FlagCloth.CellsAlong, hx, hy, hz);
        c.GroundY = -12f;
        float height = 12f;
        int sub = c.SubStepsFor(FlagWind.SpeedAt(wind, height), 1f / 60f);
        float dt = 1f / 60f / sub, worst = 1f;
        int steps = (int)(seconds / dt);
        for (int s = 0; s < steps; s++)
        {
            FlagAir air = FlagWind.Air(wind, 1f, 1f, 0f, height, 1f, 3f, s * dt, 0f, 0f);
            c.Step(dt, ref air);
            if (s * dt > 2f) { float st = c.MaxStretch(); if (st > worst) worst = st; }
        }
        double reach = 0;
        for (int j = 0; j < c.Rows; j++)
        {
            int k = j * c.Columns + c.Columns - 1;
            double along = c.X[k] * hx + c.Y[k] * hy + c.Z[k] * hz;
            double rx = c.X[k] - along * hx, ry = c.Y[k] - along * hy, rz = c.Z[k] - along * hz;
            reach += Math.Sqrt(rx * rx + ry * ry + rz * rz);
        }
        reach /= c.Rows * c.Length;
        return c.IsFinite() + "," + HoistOnLine(c) + "," + worst.ToString("F3", Inv) + "," + reach.ToString("F2", Inv);
    }

    /// <summary>
    /// A flag flying at 6 m/s turned onto a hoist tilted 45 deg and leaned 10 (SetHoist): every
    /// pair of particles as far apart as before (a rigid turn), its hoist exactly on the new
    /// line; then 3 s on in the wind: finite, the worst stretch after the first half second.
    /// </summary>
    public static string TurnHoist()
    {
        FlagCloth c = Run(6f, 6f, 1f);
        float[] x = (float[])c.X.Clone(), y = (float[])c.Y.Clone(), z = (float[])c.Z.Clone();
        float hx, hy, hz, fx, fy, fz;
        FlagOrientation.Frame(0f, 45f, 10f, out hx, out hy, out hz, out fx, out fy, out fz);
        c.SetHoist(hx, hy, hz);
        double worstChange = 0;
        for (int a = 0; a < c.Count; a += 7)
            for (int b = a + 1; b < c.Count; b += 5)
            {
                double d0 = Math.Sqrt((x[a] - x[b]) * (x[a] - x[b]) + (y[a] - y[b]) * (y[a] - y[b]) + (z[a] - z[b]) * (z[a] - z[b]));
                double d1 = Math.Sqrt((c.X[a] - c.X[b]) * (c.X[a] - c.X[b]) + (c.Y[a] - c.Y[b]) * (c.Y[a] - c.Y[b]) + (c.Z[a] - c.Z[b]) * (c.Z[a] - c.Z[b]));
                worstChange = Math.Max(worstChange, Math.Abs(d1 - d0));
            }
        bool hoist = HoistOnLine(c);
        int sub = c.SubStepsFor(FlagWind.SpeedAt(6f, 12f), 1f / 60f);
        float dt = 1f / 60f / sub, worst = 1f;
        for (int s = 0; s < 180 * sub; s++)
        {
            FlagAir air = FlagWind.Air(6f, 1f, 1f, 0f, 12f, 1f, 3f, 6f + s * dt, 0f, 0f);
            c.Step(dt, ref air);
            if (s >= 30 * sub) { float st = c.MaxStretch(); if (st > worst) worst = st; }
        }
        return (worstChange * 1000.0).ToString("F3", Inv) + "," + hoist + "," + c.IsFinite() + "," + worst.ToString("F3", Inv) + "," + HoistOnLine(c);
    }

    /// <summary>
    /// A 3 x 2 m flag on the mod's pole (FlagPoleShape for a 10 m pole: the axis Offset behind the
    /// hoist, against the fly), 12 s in a wind of the given speed blowing the given way (180:
    /// back over the pole): finite, the worst stretch after 2 s, and the nearest any free
    /// particle came to the pole's axis less the pole's own radius there, in cm, over the last
    /// 6 s (never inside the pole: at least 0).
    /// </summary>
    public static string Pole(float wind, float yawDegrees, int ropes)
    {
        FlagSpec spec = FlagSpec.Default;
        spec.Pole = FlagPoleStyle.Ball;
        spec.Ropes = ropes;
        FlagPoleShape shape = FlagPoleShape.For(spec, 10f);
        FlagCloth c = new FlagCloth(3f, 2f, FlagFabric.Standard(1f), 1f, 0f);
        c.GroundY = -10f;
        shape.ApplyTo(c, 1f, 0f, 0f);
        float growth = (shape.RadiusFoot - shape.RadiusTop) / shape.ShaftTop;
        float ax = -shape.Offset;
        double dirX = Math.Cos(yawDegrees * Math.PI / 180.0), dirZ = Math.Sin(yawDegrees * Math.PI / 180.0);
        float height = 10f;
        int sub = c.SubStepsFor(FlagWind.SpeedAt(wind, height), 1f / 60f);
        float dt = 1f / 60f / sub, worst = 1f;
        double nearest = 1e9;
        int steps = (int)(12f / dt);
        for (int s = 0; s < steps; s++)
        {
            FlagAir air = FlagWind.Air(wind, 1f, (float)dirX, (float)dirZ, height, 1f, 3f, s * dt, 0f, 0f);
            c.Step(dt, ref air);
            if (s * dt > 2f) { float st = c.MaxStretch(); if (st > worst) worst = st; }
            if (s * dt < 6f) continue;
            // Every particle: the held ones hang off the pole on the halyard's line, and on a
            // halyard the heading between its clips is as free as the rest.
            for (int k = 0; k < c.Count; k++)
            {
                double down = -c.Y[k];
                if (down < -(shape.Tip - shape.Length) || down > shape.Length) continue;
                double r = Math.Sqrt((c.X[k] - ax) * (c.X[k] - ax) + c.Z[k] * c.Z[k]) - (shape.RadiusAt(shape.Length) + growth * down);
                if (r < nearest) nearest = r;
            }
        }
        return c.IsFinite() + "," + worst.ToString("F3", Inv) + "," + (nearest * 100.0).ToString("F1", Inv);
    }

    /// <summary>
    /// A flag on a halyard (a drawn pole with its ropes: FlagCloth.SetHalyard): 8 s to settle,
    /// then 12 s: the two clips where they are held (the worst error, mm); the heading's middle
    /// off the hoist line, on average and at most, as a share of the hoist; the worst stretch;
    /// the drawn triangles crossing (every 0.5 s); the links against the same flag sewn on; and
    /// sewn on again, every hoist particle back on its line.
    /// </summary>
    public static string Halyard(float length, int detail, float wind)
    {
        FlagSpec spec = FlagSpec.Default;
        spec.Length = length;
        spec.Pole = FlagPoleStyle.Ball;
        spec.Ropes = 2;
        float height = 12f;
        FlagPoleShape shape = FlagPoleShape.For(spec, height);
        FlagCloth c = new FlagCloth(length, spec.Height, FlagFabric.Standard(1f), 1f, 0f, detail);
        int sewnLinks = c.LinkCount;
        c.GroundY = -height;
        shape.ApplyTo(c, 1f, 0f, 0f);
        int[] tris = Triangles(c.Columns, c.Rows);
        int sub = c.SubStepsFor(FlagWind.SpeedAt(wind, height), 1f / 60f);
        float dt = 1f / 60f / sub, worst = 1f, clipError = 0f, bowMax = 0f;
        double bowSum = 0;
        int samples = 0, crossing = 0, bottom = (c.Rows - 1) * c.Columns, middle = c.Rows / 2 * c.Columns;
        for (int step = 0; step < 20 * 60; step++)
        {
            for (int i = 0; i < sub; i++)
            {
                FlagAir air = FlagWind.Air(wind, 1f, 1f, 0f, height, 1f, length, step / 60f + i * dt, 0f, 0f);
                c.Step(dt, ref air);
            }
            if (step < 8 * 60) continue;
            float st = c.MaxStretch(); if (st > worst) worst = st;
            float e = Math.Abs(c.X[0]) + Math.Abs(c.Y[0]) + Math.Abs(c.Z[0]) +
                      Math.Abs(c.X[bottom]) + Math.Abs(c.Y[bottom] + shape.BottomClip) + Math.Abs(c.Z[bottom]);
            if (e > clipError) clipError = e;
            float bow = (float)Math.Sqrt(c.X[middle] * c.X[middle] + c.Z[middle] * c.Z[middle]);
            bowSum += bow; samples++;
            if (bow > bowMax) bowMax = bow;
            if (step % 30 == 0) crossing += CrossingPairs(c, tris);
        }
        bool clipped = c.Clipped, finite = c.IsFinite();
        int halyardLinks = c.LinkCount;
        c.SetHalyard(false);
        float off = 0f;
        for (int j = 0; j < c.Rows; j++)
        {
            int k = j * c.Columns;
            off = Math.Max(off, Math.Abs(c.X[k]) + Math.Abs(c.Y[k] + j * c.CellV) + Math.Abs(c.Z[k]));
        }
        bool links = halyardLinks > sewnLinks && c.LinkCount == sewnLinks;
        return finite + "," + clipped + "," + (clipError * 1000f).ToString("F3", Inv) + "," + (bowSum / samples / spec.Height).ToString("F3", Inv) + "," +
               (bowMax / spec.Height).ToString("F3", Inv) + "," + worst.ToString("F3", Inv) + "," + crossing + "," + links + "," +
               (off * 1000f).ToString("F3", Inv) + "," + c.Columns + "x" + c.Rows;
    }

    /// <summary>Where the lower rope ends, down from the anchor: a 3 x 2 m flag with two ropes and with none.</summary>
    public static string BottomClip()
    {
        FlagSpec spec = FlagSpec.Default;
        spec.Pole = FlagPoleStyle.Ball;
        spec.Ropes = 2;
        float with = FlagPoleShape.For(spec, 10f).BottomClip;
        spec.Ropes = 0;
        float without = FlagPoleShape.For(spec, 10f).BottomClip;
        return with.ToString("F3", Inv) + "," + without.ToString("F3", Inv);
    }

    /// <summary>
    /// The pole's size, as the makers size them (FlagPoleShape): foot and top across in cm, and
    /// the hoist's distance off the axis, for a 3 x 2 m flag on 10 m, a 2.4 x 1.5 m flag on 9 m,
    /// and a 30 m United States flag on 22 m; and the hoist clear of the pole all down its length.
    /// </summary>
    public static string PoleSizes()
    {
        FlagSpec s = FlagSpec.Default;
        s.Pole = FlagPoleStyle.Ball;
        FlagPoleShape a = FlagPoleShape.For(s, 10f);
        s.Length = 2.4f; s.Proportion = 625;
        FlagPoleShape b = FlagPoleShape.For(s, 9f);
        s.Length = 30f; s.Country = "us"; s = s.Sanitised();
        FlagPoleShape c = FlagPoleShape.For(s, 22f);
        bool clear = true;
        foreach (FlagPoleShape p in new[] { a, b, c })
            for (float up = Math.Max(0f, p.Length - p.FlagHeight); up <= p.Length; up += 0.05f)
                if (p.Offset < p.RadiusAt(up) + p.RopeRadius) clear = false;
        return Cm(a) + " " + Cm(b) + " " + Cm(c) + "," + clear + "," + (a.Tip > a.ShaftTop && a.ShaftTop > a.Length);
    }

    static string Cm(FlagPoleShape p)
    {
        return (200f * p.RadiusFoot).ToString("F0", Inv) + "/" + (200f * p.RadiusTop).ToString("F0", Inv) + "/" + (100f * p.Offset).ToString("F0", Inv);
    }

    /// <summary>
    /// Format 3: the pole and the tilt round trip; a format-2 save (byte by byte) reads with no
    /// pole, straight down, the pole's defaults ready; values out of range are pulled in.
    /// </summary>
    public static string CodecPole()
    {
        FlagSpec a = FlagSpec.Default;
        a.Pole = FlagPoleStyle.Spear; a.Ropes = 1; a.PoleColour = 0x112233; a.TopColour = 0x445566; a.RopeColour = 0x778899;
        a.PoleThickness = 1.5f; a.Tilt = 45f; a.Lean = -12.5f;
        FlagSpec b;
        bool round = FlagCodec.TryDecodeOne(FlagCodec.EncodeOne(a), out b) && b.Pole == FlagPoleStyle.Spear && b.Ropes == 1 &&
                     b.PoleColour == 0x112233 && b.TopColour == 0x445566 && b.RopeColour == 0x778899 &&
                     Math.Abs(b.PoleThickness - 1.5f) < 1e-6f && Math.Abs(b.Tilt - 45f) < 1e-6f && Math.Abs(b.Lean + 12.5f) < 1e-6f;

        var old = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(old);
        w.Write((byte)2);
        w.Write((byte)FlagPattern.Cross); w.Write(0xC8102Eu); w.Write(0xFFFFFFu); w.Write(0xFFFFFFu);
        w.Write(3f); w.Write((short)667); w.Write(1f);
        w.Write((byte)0);
        w.Write((byte)2); w.Write((byte)'d'); w.Write((byte)'k');
        w.Flush();
        FlagSpec c;
        bool v2 = FlagCodec.TryDecodeOne(old.ToArray(), out c) && c.Country == "dk" && c.Pole == FlagPoleStyle.None && c.Ropes == FlagSpec.DefaultRopes &&
                  c.Tilt == 0f && c.Lean == 0f && c.PoleColour == FlagSpec.DefaultPoleColour && c.PoleThickness == 1f;

        FlagSpec d = FlagSpec.Default;
        d.Pole = 9; d.Ropes = 5; d.PoleThickness = 7f; d.Tilt = 200f; d.Lean = float.NaN; d.TopColour = 0xFF000000;
        FlagSpec e = d.Sanitised();
        bool pulled = e.Pole == FlagPoleStyle.None && e.Ropes == 2 && e.PoleThickness == 2f && e.Tilt == 90f && e.Lean == 0f && e.TopColour == 0;
        return round + "," + v2 + "," + pulled + "," + FlagCodec.Version;
    }

    /// <summary>
    /// Format 4: the cloth's darkness round trips; a format-3 save (byte by byte) reads with none,
    /// the colours as designed; out of range and NaN are pulled in; and its brightness follows
    /// the sRGB curve: 50% mid grey (0.218), 20% sRGB 0.8 (0.612), none 1.
    /// </summary>
    public static string CodecDarkness()
    {
        FlagSpec a = FlagSpec.Default;
        a.Country = "jp"; a.Darkness = 0.35f;
        FlagSpec b;
        bool round = FlagCodec.TryDecodeOne(FlagCodec.EncodeOne(a), out b) && b.Country == "jp" && Math.Abs(b.Darkness - 0.35f) < 1e-6f;

        var old = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(old);
        w.Write((byte)3);
        w.Write((byte)FlagPattern.Cross); w.Write(0xC8102Eu); w.Write(0xFFFFFFu); w.Write(0xFFFFFFu);
        w.Write(3f); w.Write((short)667); w.Write(1f);
        w.Write((byte)0);
        w.Write((byte)2); w.Write((byte)'j'); w.Write((byte)'p');
        w.Write((byte)FlagPoleStyle.Ball); w.Write((byte)2);
        w.Write(0xE4E5E2u); w.Write(0xD4AF37u); w.Write(0xF2EFE6u);
        w.Write(1.25f); w.Write(30f); w.Write(0f);
        w.Flush();
        FlagSpec c;
        bool v3 = FlagCodec.TryDecodeOne(old.ToArray(), out c) && c.Country == "jp" && c.Pole == FlagPoleStyle.Ball &&
                  Math.Abs(c.PoleThickness - 1.25f) < 1e-6f && Math.Abs(c.Tilt - 30f) < 1e-6f && c.Darkness == 0f && c.Brightness == 1f;

        FlagSpec high = FlagSpec.Default; high.Darkness = 2f;
        FlagSpec nan = FlagSpec.Default; nan.Darkness = float.NaN;
        FlagSpec low = FlagSpec.Default; low.Darkness = -0.3f;
        bool pulled = high.Sanitised().Darkness == FlagSpec.MaxDarkness && nan.Sanitised().Darkness == 0f && low.Sanitised().Darkness == 0f;

        FlagSpec half = FlagSpec.Default; half.Darkness = 0.5f;
        FlagSpec fifth = FlagSpec.Default; fifth.Darkness = 0.2f;
        string curve = half.Brightness.ToString("F3", Inv) + " " + fifth.Brightness.ToString("F3", Inv) + " " +
                       FlagSpec.Default.Brightness.ToString("F3", Inv);
        return round + "," + v3 + "," + pulled + "," + curve + "," + FlagCodec.Version;
    }

    /// <summary>
    /// Format 5: a flag hoisted from its top, at a banner's 3:1, round trips; a format-4 save (byte
    /// by byte) reads hoisted from its side, its darkness kept; an edge this version does not know
    /// and a proportion past 5:1 are pulled in; the hoist of a tall flag is held to the longest
    /// length (a 30 m flag at 5:1 is 6 m long).
    /// </summary>
    public static string CodecHoist()
    {
        FlagSpec a = FlagSpec.Default;
        a.HoistedFrom = FlagHoist.Top; a.Proportion = 3000; a.Length = 4f;
        FlagSpec b;
        bool round = FlagCodec.TryDecodeOne(FlagCodec.EncodeOne(a), out b) && b.HoistedFrom == FlagHoist.Top && b.Proportion == 3000 &&
                     Math.Abs(b.Length - 4f) < 1e-6f;

        var old = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(old);
        w.Write((byte)4);
        w.Write((byte)FlagPattern.Cross); w.Write(0xC8102Eu); w.Write(0xFFFFFFu); w.Write(0xFFFFFFu);
        w.Write(3f); w.Write((short)667); w.Write(1f);
        w.Write((byte)0);
        w.Write((byte)0);
        w.Write((byte)FlagPoleStyle.None); w.Write((byte)2);
        w.Write(0xE4E5E2u); w.Write(0xD4AF37u); w.Write(0xF2EFE6u);
        w.Write(1f); w.Write(0f); w.Write(0f);
        w.Write(0.25f);
        w.Flush();
        FlagSpec c;
        bool v4 = FlagCodec.TryDecodeOne(old.ToArray(), out c) && c.HoistedFrom == FlagHoist.Side && Math.Abs(c.Darkness - 0.25f) < 1e-6f &&
                  c.Pattern == FlagPattern.Cross;

        FlagSpec d = FlagSpec.Default; d.HoistedFrom = 7; d.Proportion = 6000;
        FlagSpec e = d.Sanitised();
        FlagSpec tall = FlagSpec.Default; tall.Proportion = 5000; tall.Length = 30f;
        FlagSpec t = tall.Sanitised();
        bool pulled = e.HoistedFrom == FlagHoist.Side && e.Proportion == FlagProportion.Default && t.Proportion == 5000 && Math.Abs(t.Length - 6f) < 1e-4f;
        return round + "," + v4 + "," + pulled + "," + FlagCodec.Version;
    }

    /// <summary>
    /// Format 6: the drawn pole's own length round trips (it moves with the flag: Move It raising
    /// a flag raises its pole); a format-5 save (byte by byte) reads with none yet, to be found
    /// down the pole's axis once; NaN and negative are pulled to none, past 1 km to 1 km.
    /// </summary>
    public static string CodecPoleLength()
    {
        FlagSpec a = FlagSpec.Default;
        a.Pole = FlagPoleStyle.Ball; a.PoleLength = 37.5f; a.HoistedFrom = FlagHoist.Top;
        FlagSpec b;
        bool round = FlagCodec.TryDecodeOne(FlagCodec.EncodeOne(a), out b) && Math.Abs(b.PoleLength - 37.5f) < 1e-6f &&
                     b.HoistedFrom == FlagHoist.Top && b.Pole == FlagPoleStyle.Ball;

        var old = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(old);
        w.Write((byte)5);
        w.Write((byte)FlagPattern.Cross); w.Write(0xC8102Eu); w.Write(0xFFFFFFu); w.Write(0xFFFFFFu);
        w.Write(3f); w.Write((short)667); w.Write(1f);
        w.Write((byte)0);
        w.Write((byte)0);
        w.Write((byte)FlagPoleStyle.Spear); w.Write((byte)2);
        w.Write(0xE4E5E2u); w.Write(0xD4AF37u); w.Write(0xF2EFE6u);
        w.Write(1f); w.Write(0f); w.Write(0f);
        w.Write(0.1f);
        w.Write((byte)FlagHoist.Side);
        w.Flush();
        FlagSpec c;
        bool v5 = FlagCodec.TryDecodeOne(old.ToArray(), out c) && c.Pole == FlagPoleStyle.Spear && c.PoleLength == 0f && Math.Abs(c.Darkness - 0.1f) < 1e-6f;

        FlagSpec nan = FlagSpec.Default; nan.PoleLength = float.NaN;
        FlagSpec neg = FlagSpec.Default; neg.PoleLength = -4f;
        FlagSpec far = FlagSpec.Default; far.PoleLength = 5000f;
        bool pulled = nan.Sanitised().PoleLength == 0f && neg.Sanitised().PoleLength == 0f && far.Sanitised().PoleLength == FlagSpec.MaxPoleLength;

        // The same pole to the renderer only at the same length (a new length remakes it).
        FlagSpec d = a; d.PoleLength = 38f;
        bool differs = !a.SamePole(d) && a.SamePole(a);
        return round + "," + v5 + "," + pulled + "," + differs + "," + FlagCodec.Version;
    }

    /// <summary>
    /// The panel's sizes (FlagSize): a 3 m flag at 2:3 seen from its side and from its top; linked,
    /// a new width or height scales it whole (stopped where a side would pass 30 m); unlinked, it
    /// changes one side, the other pushed only past 1:5 to 5:1; a shape keeps the longer side (and
    /// grows a side under half a metre); the shape as seen of a 2:3 cloth hung from its top is 3:2.
    /// Each as "width x height @ proportion".
    /// </summary>
    public static string Sizes()
    {
        float w, h, l;
        int p;
        var sb = new System.Text.StringBuilder();
        FlagSize.Shown(3f, 667, FlagHoist.Side, out w, out h); sb.Append(F(w) + "x" + F(h) + " ");
        FlagSize.Shown(3f, 667, FlagHoist.Top, out w, out h); sb.Append(F(w) + "x" + F(h) + "|");
        FlagSize.Resize(3f, 667, FlagHoist.Side, false, 4.5f, true, out l, out p); sb.Append(Size(l, p, FlagHoist.Side) + " ");
        FlagSize.Resize(3f, 667, FlagHoist.Side, true, 3f, true, out l, out p); sb.Append(Size(l, p, FlagHoist.Side) + " ");
        FlagSize.Resize(3f, 2000, FlagHoist.Side, false, 30f, true, out l, out p); sb.Append(Size(l, p, FlagHoist.Side) + " ");
        FlagSize.Resize(3f, 667, FlagHoist.Top, false, 4f, true, out l, out p); sb.Append(Size(l, p, FlagHoist.Top) + "|");
        FlagSize.Resize(3f, 667, FlagHoist.Side, true, 2.5f, false, out l, out p); sb.Append(Size(l, p, FlagHoist.Side) + " ");
        FlagSize.Resize(3f, 667, FlagHoist.Side, true, 20f, false, out l, out p); sb.Append(Size(l, p, FlagHoist.Side) + " ");
        FlagSize.Resize(3f, 667, FlagHoist.Top, false, 2.5f, false, out l, out p); sb.Append(Size(l, p, FlagHoist.Top) + "|");
        FlagSize.Reshape(3f, 667, FlagHoist.Side, 1500, out l, out p); sb.Append(Size(l, p, FlagHoist.Side) + " ");
        FlagSize.Reshape(3f, 667, FlagHoist.Top, 500, out l, out p); sb.Append(Size(l, p, FlagHoist.Top) + " ");
        FlagSize.Reshape(0.6f, 1000, FlagHoist.Side, 3000, out l, out p); sb.Append(Size(l, p, FlagHoist.Side) + "|");
        sb.Append(FlagSize.ShownProportion(667, FlagHoist.Top) + " " + FlagSize.IsShape(667, FlagHoist.Top, 1500) + " " +
                  FlagSize.IsShape(667, FlagHoist.Side, 1500) + "|");
        foreach (int v in new[] { 1500, 2000, 3000, 1219, 733, 392, 667 })
            sb.Append(FlagProportion.Name(v) + " ");
        return sb.ToString().TrimEnd();
    }

    static string F(float v) { return v.ToString("0.##", Inv); }

    static string Size(float length, int proportion, int hoist)
    {
        float w, h;
        FlagSize.Shown(length, proportion, hoist, out w, out h);
        return F(w) + "x" + F(h) + "@" + proportion;
    }

    /// <summary>
    /// Hoisted from its top (FlagHoist.Top): the hoist and the fly trade places at every heading,
    /// tilt and lean; untilted, the hoist runs along the heading and the fly hangs down; turned
    /// 90 degrees, the hoist along +z.
    /// </summary>
    public static string TopFrames()
    {
        float hx, hy, hz, fx, fy, fz, sx, sy, sz, tx, ty, tz;
        bool swapped = true;
        float[][] cases = { new float[] { 0f, 0f, 0f }, new float[] { 0.7f, 45f, 30f }, new float[] { 2f, -60f, -80f }, new float[] { 4f, 90f, 10f } };
        foreach (float[] c in cases)
        {
            FlagOrientation.Frame(c[0], c[1], c[2], FlagHoist.Top, out hx, out hy, out hz, out fx, out fy, out fz);
            FlagOrientation.Frame(c[0], c[1], c[2], FlagHoist.Side, out sx, out sy, out sz, out tx, out ty, out tz);
            if (!(Near(hx, tx) && Near(hy, ty) && Near(hz, tz) && Near(fx, sx) && Near(fy, sy) && Near(fz, sz))) swapped = false;
        }
        FlagOrientation.Frame(0f, 0f, 0f, FlagHoist.Top, out hx, out hy, out hz, out fx, out fy, out fz);
        bool banner = Near(hx, 1) && Near(hy, 0) && Near(hz, 0) && Near(fx, 0) && Near(fy, -1) && Near(fz, 0);
        FlagOrientation.Frame(1.5707964f, 0f, 0f, FlagHoist.Top, out hx, out hy, out hz, out fx, out fy, out fz);
        bool turned = Near(hx, 0) && Near(hy, 0) && Near(hz, 1) && Near(fy, -1);
        return swapped + "," + banner + "," + turned;
    }

    /// <summary>
    /// A banner hoisted from its top -- 2 m across, 3 m down: a 3 m cloth at 2:3 hung from its
    /// 2 m hoist -- 10 m up, the wind at <paramref name="yawDegrees"/> off its heading (90: on its
    /// face, 0: along its crossbar). Finite, its hoist exactly on its level line, the worst stretch
    /// after 2 s, and how far below the crossbar its fly edge hangs on average (x L: 1 straight
    /// down, 0 blown level).
    /// </summary>
    public static string TopBanner(float wind, float yawDegrees, float seconds)
    {
        float hx, hy, hz, fx, fy, fz;
        FlagOrientation.Frame(0f, 0f, 0f, FlagHoist.Top, out hx, out hy, out hz, out fx, out fy, out fz);
        double yaw = yawDegrees * Math.PI / 180.0;
        float dx = (float)Math.Cos(yaw), dz = (float)Math.Sin(yaw);
        FlagCloth c = new FlagCloth(3f, 2f, FlagFabric.Standard(1f), dx, dz, FlagCloth.CellsAlong, hx, hy, hz);
        float height = 12f;
        c.GroundY = -height;
        int sub = c.SubStepsFor(FlagWind.SpeedAt(wind, height), 1f / 60f);
        float dt = 1f / 60f / sub, worst = 1f;
        int steps = (int)(seconds / dt);
        double fall = 0;
        int falls = 0;
        for (int s = 0; s < steps; s++)
        {
            FlagAir air = FlagWind.Air(wind, 1f, dx, dz, height, 1f, 3f, s * dt, 0f, 0f);
            c.Step(dt, ref air);
            if (s * dt > 2f)
            {
                float st = c.MaxStretch(); if (st > worst) worst = st;
                if (s % (6 * sub) == 0)
                {
                    for (int j = 0; j < c.Rows; j++) fall += -c.Y[j * c.Columns + c.Columns - 1];
                    falls += c.Rows;
                }
            }
        }
        fall /= Math.Max(1, falls) * c.Length;
        return c.IsFinite() + "," + HoistOnLine(c) + "," + worst.ToString("F3", Inv) + "," + fall.ToString("F2", Inv);
    }

    /// <summary>
    /// The outline (FlagOutline, from the flag's place, hoist and size alone) holds the cloth
    /// whatever it does: every particle, every 0.1 s over 20 s, of a 3 x 2 m flag flying, hanging
    /// limp, on a hoist tilted 45 degrees, hoisted from its top, on a halyard in a storm -- inside
    /// the disc and between its heights. Returns the case that came nearest its edge and how near
    /// (m; negative: outside), and the disc's radius over the flag's length.
    /// </summary>
    public static string OutlineCovers()
    {
        float[][] cases =
        {
            new float[] { 0f, 0f, 0f, 0f }, new float[] { 6f, 0f, 0f, 0f }, new float[] { 12f, 0f, 0f, 0f }, new float[] { 30f, 0f, 0f, 1f },
            new float[] { 6f, 45f, 0f, 0f }, new float[] { 0f, 0f, 1f, 0f }, new float[] { 8f, 0f, 1f, 0f }, new float[] { 2f, 0f, 0f, 1f },
        };
        double least = double.MaxValue;
        string nearest = "";
        float share = 0f;
        foreach (float[] k in cases)
        {
            float hx, hy, hz, fx, fy, fz;
            FlagOrientation.Frame(0f, k[1], 0f, k[2] > 0.5f ? FlagHoist.Top : FlagHoist.Side, out hx, out hy, out hz, out fx, out fy, out fz);
            FlagCloth c = new FlagCloth(3f, 2f, FlagFabric.Standard(k[0] > 20f ? 2f : 1f), 1f, 0f, FlagCloth.CellsAlong, hx, hy, hz);
            if (k[3] > 0.5f) c.SetHalyard(true);
            float height = 12f;
            c.GroundY = -height;
            FlagOutline o = FlagOutline.For(UnityEngine.Vector3.zero, new UnityEngine.Vector3(hx, hy, hz), c.Height, c.Length);
            share = o.Radius / c.Length;
            int sub = c.SubStepsFor(FlagWind.SpeedAt(k[0], height), 1f / 60f);
            float dt = 1f / 60f / sub;
            int steps = (int)(20f / dt);
            for (int s = 0; s < steps; s++)
            {
                FlagAir air = FlagWind.Air(k[0], 1f, 1f, 0f, height, 1f, 3f, s * dt, 0f, 0f);
                c.Step(dt, ref air);
                if (s % (6 * sub) != 0) continue;
                for (int q = 0; q < c.Count; q++)
                {
                    double dxz = Math.Sqrt((c.X[q] - o.X) * (c.X[q] - o.X) + (c.Z[q] - o.Z) * (c.Z[q] - o.Z));
                    double margin = Math.Min(o.Radius - dxz, Math.Min(c.Y[q] - o.MinY, o.MaxY - c.Y[q]));
                    if (margin < least)
                    {
                        least = margin;
                        nearest = k[0] + " m/s" + (k[1] != 0f ? " tilted " + k[1] : "") + (k[2] > 0.5f ? " from the top" : "") + (k[3] > 0.5f ? " on a halyard" : "");
                    }
                }
            }
        }
        return nearest + "," + least.ToString("F2", Inv) + "," + share.ToString("F2", Inv);
    }

    /// <summary>
    /// The pick volume stands still while the flag waves: a 3 x 2 m flag in a wind, the volume
    /// made every 0.1 s as the renderer makes it -- the first pick (the cloth's box as it was, in
    /// the axes of its fly edge as it was) against the held one (PickHold, in the wind's axes),
    /// each with the renderer's margins for a 3 m flag (0.15 m in its plane, 0.4 across). A
    /// pointer put on the cloth -- on its fly end or its middle, at every tenth of a second after
    /// 4 s -- and held still: the share of the next 2 s (the time to see the flag light up and
    /// click) each volume held it, and of the whole 16 s from the first; and how much each
    /// volume's size changed from one 0.1 s to the next (rms, m).
    /// </summary>
    public static string PickSteady(float wind)
    {
        FlagCloth c = new FlagCloth(3f, 2f, FlagFabric.Standard(1f), 1f, 0f);
        float height = 12f;
        c.GroundY = -height;
        int sub = c.SubStepsFor(FlagWind.SpeedAt(wind, height), 1f / 60f);
        float dt = 1f / 60f / sub;
        int every = 6 * sub, steps = (int)(20f / dt);
        var hold = new PickHold();
        const float inPlane = 0.15f, across = 0.4f;
        UnityEngine.Vector3 h = new UnityEngine.Vector3(0f, -1f, 0f), wu = new UnityEngine.Vector3(1f, 0f, 0f);
        UnityEngine.Vector3 wn = UnityEngine.Vector3.Cross(h, wu).normalized;
        var oldAxes = new List<UnityEngine.Vector3[]>();
        var oldBoxes = new List<float[]>();
        var heldBoxes = new List<float[]>();
        var pointers = new List<UnityEngine.Vector3[]>();
        double lastOld = -1, lastNew = -1, changeOld = 0, changeNew = 0;
        int changes = 0;
        for (int s = 0; s < steps; s++)
        {
            FlagAir air = FlagWind.Air(wind, 1f, 1f, 0f, height, 1f, 3f, s * dt, 0f, 0f);
            c.Step(dt, ref air);
            if (s % every != 0) continue;
            float t = s * dt;

            // The first pick's axes: out along the fly edge's mean now.
            UnityEngine.Vector3 m = UnityEngine.Vector3.zero;
            for (int j = 0; j < c.Rows; j++) { int k = j * c.Columns + c.Columns - 1; m += new UnityEngine.Vector3(c.X[k], c.Y[k], c.Z[k]); }
            m /= c.Rows;
            UnityEngine.Vector3 ou = m - h * UnityEngine.Vector3.Dot(m, h);
            ou = ou.sqrMagnitude > 1e-6f ? ou.normalized : wu;
            UnityEngine.Vector3 on = UnityEngine.Vector3.Cross(h, ou).normalized;

            float[] oldBox = Extents(c, ou, h, on), now = Extents(c, wu, h, wn);
            hold.Take(now[0], now[1], now[2], now[3], now[4], now[5], 0.1f, 4f);
            float[] held = { hold.MinU, hold.MaxU, hold.MinV, hold.MaxV, hold.MinN, hold.MaxN };
            if (t < 4f) continue;

            int fly = (c.Rows / 2) * c.Columns + c.Columns - 1, middle = (c.Rows / 2) * c.Columns + c.Columns / 2;
            pointers.Add(new[] { new UnityEngine.Vector3(c.X[fly], c.Y[fly], c.Z[fly]), new UnityEngine.Vector3(c.X[middle], c.Y[middle], c.Z[middle]) });
            oldAxes.Add(new[] { ou, on });
            oldBoxes.Add(oldBox);
            heldBoxes.Add(held);

            double sizeOld = (oldBox[1] - oldBox[0]) + (oldBox[3] - oldBox[2]) + (oldBox[5] - oldBox[4]);
            double sizeNew = (held[1] - held[0]) + (held[3] - held[2]) + (held[5] - held[4]);
            if (lastOld >= 0) { changeOld += (sizeOld - lastOld) * (sizeOld - lastOld); changeNew += (sizeNew - lastNew) * (sizeNew - lastNew); changes++; }
            lastOld = sizeOld; lastNew = sizeNew;
        }

        int pairs = 0, keptOld = 0, keptNew = 0, whole = 0, wholeOld = 0, wholeNew = 0;
        for (int a = 0; a < pointers.Count; a++)
            for (int b = a; b < pointers.Count; b++)
                foreach (UnityEngine.Vector3 p in pointers[a])
                {
                    bool o = Inside(p, oldAxes[b][0], h, oldAxes[b][1], oldBoxes[b], inPlane, across);
                    bool n = Inside(p, wu, h, wn, heldBoxes[b], inPlane, across);
                    if (b - a <= 20) { pairs++; if (o) keptOld++; if (n) keptNew++; }
                    if (a == 0) { whole++; if (o) wholeOld++; if (n) wholeNew++; }
                }
        int m2 = Math.Max(1, changes);
        return ((double)keptOld / pairs).ToString("F3", Inv) + "," + ((double)keptNew / pairs).ToString("F3", Inv) + "," +
               ((double)wholeOld / whole).ToString("F2", Inv) + "," + ((double)wholeNew / whole).ToString("F2", Inv) + "," +
               Math.Sqrt(changeOld / m2).ToString("F3", Inv) + "," + Math.Sqrt(changeNew / m2).ToString("F3", Inv);
    }

    static float[] Extents(FlagCloth c, UnityEngine.Vector3 u, UnityEngine.Vector3 v, UnityEngine.Vector3 n)
    {
        float[] e = { float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue };
        for (int k = 0; k < c.Count; k++)
        {
            var p = new UnityEngine.Vector3(c.X[k], c.Y[k], c.Z[k]);
            float a = UnityEngine.Vector3.Dot(p, u), b = UnityEngine.Vector3.Dot(p, v), d = UnityEngine.Vector3.Dot(p, n);
            e[0] = Math.Min(e[0], a); e[1] = Math.Max(e[1], a);
            e[2] = Math.Min(e[2], b); e[3] = Math.Max(e[3], b);
            e[4] = Math.Min(e[4], d); e[5] = Math.Max(e[5], d);
        }
        return e;
    }

    static bool Inside(UnityEngine.Vector3 p, UnityEngine.Vector3 u, UnityEngine.Vector3 v, UnityEngine.Vector3 n, float[] e, float inPlane, float across)
    {
        float a = UnityEngine.Vector3.Dot(p, u), b = UnityEngine.Vector3.Dot(p, v), d = UnityEngine.Vector3.Dot(p, n);
        return a >= e[0] - inPlane && a <= e[1] + inPlane && b >= e[2] - inPlane && b <= e[3] + inPlane && d >= e[4] - across && d <= e[5] + across;
    }

    /// <summary>The held extents: a wider one at once; a narrower one given back to over the release (e^-1 of the gap after one time constant).</summary>
    public static string Hold()
    {
        var h = new PickHold();
        h.Take(0f, 1f, 0f, 1f, 0f, 1f, 0.1f, 4f);
        h.Take(-0.5f, 2f, 0f, 1f, 0f, 1f, 0.1f, 4f);
        string grown = F(h.MinU) + " " + F(h.MaxU);
        for (int i = 0; i < 40; i++) h.Take(0f, 1f, 0f, 1f, 0f, 1f, 0.1f, 4f);
        string after = h.MaxU.ToString("F3", Inv);
        h.Clear();
        h.Take(0f, 1f, 0f, 1f, 0f, 1f, 0.1f, 4f);
        return grown + "|" + after + "|" + F(h.MaxU);
    }

    /// <summary>
    /// The drawn surface (ClothSurface: what the shader evaluates): through every particle; the
    /// same on both sides of a cell's edge; its normal on the front, as the cloth's own
    /// (FlagCloth.ComputeNormals), everywhere on a flag flying at 12 m/s. Returns the worst
    /// miss at the particles (mm), the worst jump across cells (mm), and the least cosine
    /// between the two normals at the particles.
    /// </summary>
    public static string Surface()
    {
        FlagCloth c = Run(12f, 6f, 1f);
        int cols = c.Columns, rows = c.Rows;
        float[] p = new float[3], pu = new float[3], pv = new float[3], q = new float[3], qu = new float[3], qv = new float[3];
        double miss = 0, jump = 0, cos = 1;
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                int k = j * cols + i;
                ClothSurface.Evaluate(c.X, c.Y, c.Z, cols, rows, i, j, p, pu, pv);
                miss = Math.Max(miss, Math.Sqrt((p[0] - c.X[k]) * (p[0] - c.X[k]) + (p[1] - c.Y[k]) * (p[1] - c.Y[k]) + (p[2] - c.Z[k]) * (p[2] - c.Z[k])));
                double nx = pu[1] * pv[2] - pu[2] * pv[1], ny = pu[2] * pv[0] - pu[0] * pv[2], nz = pu[0] * pv[1] - pu[1] * pv[0];
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > 1e-9) cos = Math.Min(cos, (nx * c.Nx[k] + ny * c.Ny[k] + nz * c.Nz[k]) / len);
                if (i < cols - 1 && j < rows - 1 && i > 0)
                {
                    // Just either side of the edge between cells i - 1 and i, halfway down a row.
                    ClothSurface.Evaluate(c.X, c.Y, c.Z, cols, rows, i - 1e-4f, j + 0.5f, p, pu, pv);
                    ClothSurface.Evaluate(c.X, c.Y, c.Z, cols, rows, i + 1e-4f, j + 0.5f, q, qu, qv);
                    jump = Math.Max(jump, Math.Sqrt((p[0] - q[0]) * (p[0] - q[0]) + (p[1] - q[1]) * (p[1] - q[1]) + (p[2] - q[2]) * (p[2] - q[2])));
                }
            }
        return (miss * 1000).ToString("F3", Inv) + "," + (jump * 1000).ToString("F3", Inv) + "," + cos.ToString("F3", Inv);
    }

    /// <summary>
    /// The edges' smoothing (ClothSurface.SmoothEdges): a zigzag of 1 cm along the fly edge is
    /// gone (its rms after, mm), a wave six particles long along the top keeps most of its height
    /// (share), the hoist and the corners do not move.
    /// </summary>
    public static string Smoothing()
    {
        int cols = 19, rows = 13, n = cols * rows;
        float[] x = new float[n], y = new float[n], z = new float[n], scratch = new float[3 * Math.Max(cols, rows)];
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                int k = j * cols + i;
                x[k] = i * 0.16f; y[k] = -j * 0.16f;
                z[k] = i == cols - 1 ? ((j & 1) == 0 ? 0.01f : -0.01f) : 0f;
                if (j == 0) z[k] += 0.05f * (float)Math.Sin(2 * Math.PI * i / 6.0);
            }
        float[] x0 = (float[])x.Clone(), y0 = (float[])y.Clone(), z0 = (float[])z.Clone();
        ClothSurface.SmoothEdges(x, y, z, cols, rows, scratch);
        double zig = 0; int m = 0;
        for (int j = 1; j < rows - 1; j++) { int k = j * cols + cols - 1; zig += z[k] * z[k]; m++; }
        zig = Math.Sqrt(zig / m);
        double before = 0, after = 0;
        for (int i = 1; i < cols - 1; i++)
        {
            before += z0[i] * z0[i];
            after += z[i] * z[i];
        }
        bool held = true;
        for (int j = 0; j < rows; j++) { int k = j * cols; if (x[k] != x0[k] || y[k] != y0[k] || z[k] != z0[k]) held = false; }
        foreach (int k in new[] { cols - 1, n - 1, (rows - 1) * cols })
            if (x[k] != x0[k] || y[k] != y0[k] || z[k] != z0[k]) held = false;
        return (zig * 1000).ToString("F2", Inv) + "," + Math.Sqrt(after / before).ToString("F2", Inv) + "," + held;
    }

    /// <summary>The paused flag's time: the frame's while handled, slowing to none over the fade, none once left alone.</summary>
    public static string Paused()
    {
        float f = 1f / 60f;
        return (ClothPlan.PausedTime(f, 1.5f, 0.5f) / f).ToString("F2", Inv) + "," + (ClothPlan.PausedTime(f, 0.25f, 0.5f) / f).ToString("F2", Inv) + "," +
               (ClothPlan.PausedTime(f, 0f, 0.5f) / f).ToString("F2", Inv) + "," + (ClothPlan.PausedTime(f, -3f, 0.5f) / f).ToString("F2", Inv) + "," +
               (ClothPlan.PausedTime(0f, 1f, 0.5f)).ToString("F2", Inv);
    }

    /// <summary>
    /// The flags a search along a whole ray walks (FlagPicking.Props, for Move It's pointer):
    /// three published, one taken away, one published twice, then all cleared -- and every
    /// snapshot handed out left as it was.
    /// </summary>
    public static string PickRegistry()
    {
        FlagPicking.Clear();
        FlagPicking.Publish(5, new PickShape());
        FlagPicking.Publish(9, new PickShape());
        uint[] before = FlagPicking.Props;
        FlagPicking.Publish(700, new PickShape());
        FlagPicking.Publish(9, null);
        FlagPicking.Publish(5, new PickShape());
        string now = string.Join(" ", Array.ConvertAll(FlagPicking.Props, p => p.ToString()));
        string kept = string.Join(" ", Array.ConvertAll(before, p => p.ToString()));
        FlagPicking.Clear();
        return now + "|" + kept + "|" + FlagPicking.Props.Length;
    }

    /// <summary>What the colour picker's Paste and its field take: the first colour code in the text, or none.</summary>
    public static string Pasted()
    {
        string[] texts = { "#1a2b3c", " 1A2B3C ", "#abc", "abc", "color: #FF8800;", "0x00FF00", "  #ff0000 and #00ff00", "#12345", "#1234567", "rgb(1,2,3)", "#GG0000", "" };
        string result = "";
        foreach (string t in texts)
        {
            uint v;
            result += (result.Length > 0 ? " " : "") + (FlagColours.FindHex(t, out v) ? FlagColours.Hex(v) : "refused");
        }
        return result;
    }

    public static string Colours()
    {
        uint v;
        string hex = (FlagColours.TryParseHex("#1a2B3c", out v) ? FlagColours.Hex(v) : "refused") + " " +
                     (FlagColours.TryParseHex("0x00FF00", out v) ? FlagColours.Hex(v) : "refused") + " " +
                     (FlagColours.TryParseHex("12345", out v) ? FlagColours.Hex(v) : "refused") + " " +
                     (FlagColours.TryParseHex("#GG0000", out v) ? FlagColours.Hex(v) : "refused");
        int bad = 0;
        foreach (uint c in FlagColours.Palette)
        {
            float h, s, val;
            FlagColours.ToHsv(c, out h, out s, out val);
            if (FlagColours.FromHsv(h, s, val) != c) bad++;
        }
        return hex + "|" + bad + "|" + FlagColours.ToLinear(0.5f).ToString("F4", Inv) + "|" + FlagColours.ToLinear(1f).ToString("F4", Inv);
    }
}
'@
$hp = New-Object System.CodeDom.Compiler.CompilerParameters
$hp.GenerateInMemory = $true
$hp.CompilerOptions = "/optimize+"
[void]$hp.ReferencedAssemblies.Add("System.dll")
[void]$hp.ReferencedAssemblies.Add("System.Core.dll")
[void]$hp.ReferencedAssemblies.Add((Resolve-Path $Dll).Path)
# Unity's structs (Vector3) for the pick volumes' arithmetic: plain IL, no engine needed.
[void]$hp.ReferencedAssemblies.Add((Join-Path $managed "UnityEngine.dll"))
Add-Type -TypeDefinition $harness -CompilerParameters $hp

$failed = 0
function Check([string]$name, [bool]$ok, $detail) {
    if ($ok) { Write-Host "ok    $name  ($detail)" } else { Write-Host "FAIL  $name  ($detail)"; $script:failed++ }
}
$inv = [Globalization.CultureInfo]::InvariantCulture
function N([string]$s) { return [double]::Parse($s, $inv) }

# ---- noise and wind -----------------------------------------------------------------------

$n1 = [ClothTests]::NoiseStats(1) -split ","; $n3 = [ClothTests]::NoiseStats(3) -split ","
Check "1-D noise: mean ~0, sigma ~1" ([math]::Abs((N $n1[0])) -lt 0.1 -and (N $n1[1]) -gt 0.75 -and (N $n1[1]) -lt 1.3) ("mean " + $n1[0] + ", sigma " + $n1[1])
Check "3-D noise: mean ~0, sigma ~1" ([math]::Abs((N $n3[0])) -lt 0.1 -and (N $n3[1]) -gt 0.75 -and (N $n3[1]) -lt 1.3) ("mean " + $n3[0] + ", sigma " + $n3[1])
Check "noise is deterministic" ([ClothTests]::NoiseDeterministic()) "same inputs, same value"

$w = $asm.GetType("RealFlag.Flags.FlagWind", $true)
Check "the wind setting is the speed at 10 m" ([math]::Abs($w::SpeedAt(10, 10) - 10) -lt 1e-4) ("{0:F3} m/s" -f $w::SpeedAt(10, 10))
Check "held below 5 m (EN 1991-1-4 z_min)" ($w::SpeedAt(10, 1) -eq $w::SpeedAt(10, 5)) ("{0:F2} m/s at 1 m and at 5 m" -f $w::SpeedAt(10, 1))
Check "more wind higher up (log law)" ($w::SpeedAt(10, 30) -gt 12.5 -and $w::SpeedAt(10, 30) -lt 13.5) ("{0:F2} m/s at 30 m" -f $w::SpeedAt(10, 30))
Check "held to 40 m/s: 30 m/s in rain at 200 m would be 80" ($w::SpeedAt(45, 200) -eq 40) ("{0:F1} m/s" -f $w::SpeedAt(45, 200))
$clothType = [RealFlag.Flags.FlagCloth]
$step = [single](1.0 / 60.0)
$subSteps = (@(0, 9, 9.1, 18, 27.5, 40) | ForEach-Object { $clothType::SubSteps($_, $step, 3.0 / 18.0) }) -join ","
Check "a 3 m flag's 1/60 s step: a sub-step per 9 m/s at the flag (0.9 cells a sub-step)" ($subSteps -eq "1,1,2,2,4,5") ("0, 9, 9.1, 18, 27.5, 40 m/s: " + $subSteps)
$other = "" + $clothType::SubSteps(6.3, $step, 1.5 / 18.0) + "," + $clothType::SubSteps(6.3, 2 * $step, 3.0 / 18.0) + "," +
         $clothType::SubSteps(40, 2 * $step, 0.075) + "," + $clothType::SubSteps(80, 2 * $step, 0.075)
Check "half the cell, or twice the step, twice the sub-steps; 20 at the most" ($other -eq "2,2,20,20") ("1.5 m flag at 6.3 m/s, 3 m flag's 1/30 s step, the smallest cells at 40 m/s and 80 m/s in 1/30 s: " + $other)
$grids = @(@(3, 2), @(1, 0.667), @(0.5, 0.333), @(30, 20)) | ForEach-Object {
    $c = 0; $r = 0; $clothType::GridFor($_[0], $_[1], [ref]$c, [ref]$r); "$c" + "x" + "$r" }
Check "grids: 18 cells along, none shorter than 8 cm" (($grids -join " ") -eq "19x13 13x9 7x5 19x13") ("3 x 2 m: " + $grids[0] + "; 1 m: " + $grids[1] + "; 0.5 m: " + $grids[2] + "; 30 m: " + $grids[3])
Check "town turbulence at 10 m: I = 1 / ln(10 / 0.3)" ([math]::Abs($w::Intensity(10) - 0.2852) -lt 0.001) ("{0:F4}" -f $w::Intensity(10))
Check "the gusts' length scale at 10 m ~ 50 m" ($w::LengthScale(10) -gt 40 -and $w::LengthScale(10) -lt 60) ("{0:F1} m" -f $w::LengthScale(10))

# ---- the cloth ----------------------------------------------------------------------------

$calm = ([ClothTests]::Droop([ClothTests]::Run(0, 12, 1)) -split ",")
Check "calm air (Beaufort 0): the flag hangs down the pole" ((N $calm[0]) -gt 65) ("droop " + $calm[0] + " deg, reach " + $calm[1] + " L")
$light = ([ClothTests]::Droop([ClothTests]::Run(2, 12, 1)) -split ",")
Check "a light breeze (2 m/s): it lifts, still drooping" ((N $light[0]) -gt 10 -and (N $light[0]) -lt 75) ("droop " + $light[0] + " deg, reach " + $light[1] + " L")
# A flag flapping in a breeze reaches a little short of its length (its waves take some of it):
# 0.99 while the friction along it (C_T 0.08) held it flat, 0.90 at 0.01.
$breeze = ([ClothTests]::Droop([ClothTests]::Run(6, 12, 1)) -split ",")
Check "a moderate breeze (6 m/s): it flies out" ((N $breeze[0]) -lt 15 -and (N $breeze[1]) -ge 0.85) ("droop " + $breeze[0] + " deg, reach " + $breeze[1] + " L")

# The worst link of a flag flapping as real ones do -- at the fly's corner as it whips -- is
# stretched a little more than a flat one's: 10.4% at 12 m/s (C_T 0.01), 7.6% at C_T 0.08.
foreach ($speed in 6, 12) {
    $wv = ([ClothTests]::Waves($speed) -split ",")
    Check "$speed m/s: the flapping grows towards the fly" ((N $wv[1]) -gt 1.8 * (N $wv[0])) ("rms " + $wv[0] + " m at a third, " + $wv[1] + " m at the fly")
    Check "$speed m/s: they run from the hoist to the fly, slower than the wind" ((N $wv[2]) -gt 0.25 -and (N $wv[2]) -lt 1.1) ($wv[2] + " x the wind over 1-6 Hz, most flutter at " + $wv[4] + " Hz")
    Check "$speed m/s: stretched at most 12%" ((N $wv[3]) -lt 1.12) ("worst " + $wv[3])
}

$storm = [ClothTests]::Storm() -split ","
Check "a storm (30 m/s, waviness 200%): still finite" ($storm[0] -eq "True") "20 s"
Check "a storm: stretched at most 15%" ((N $storm[1]) -lt 1.15) ("worst " + $storm[1])
Check "the hoist stays where the pole would be" ([ClothTests]::HoistHeld()) "every hoist particle at (0, -j * cell, 0)"
Check "the same run twice is the same" ([ClothTests]::Deterministic()) "bit for bit"

foreach ($size in @(@(4.5, 500), @(1.5, 667))) {
    $rs = [ClothTests]::Resize($size[0], $size[1]) -split ","
    Check ("resized to {0} m at {1}: it keeps flying, its hoist on the pole" -f $size[0], $size[1]) ([math]::Abs((N $rs[0]) - 1) -lt 0.1 -and $rs[1] -eq "True" -and $rs[2] -eq "True" -and (N $rs[3]) -lt 1.10) ("reach x" + $rs[0] + " of the length change, hoist held " + $rs[1] + ", " + $rs[4] + ", worst stretch after 0.5 s " + $rs[3])
}

$move = [ClothTests]::MoveAnchor() -split ","
Check "a small move drags the cloth; of a bigger one half a cell; a jump carries it" ([math]::Abs((N $move[0]) + 0.05) -lt 1e-3 -and [math]::Abs((N $move[1]) + (N $move[3])) -lt 1e-3 -and [math]::Abs((N $move[2])) -lt 1e-3) ("fly moved " + $move[0] + " m against a 5 cm move, " + $move[1] + " m against 0.5 m (half a cell: " + $move[3] + " m), " + $move[2] + " m relative on a 50 m jump")
foreach ($case in @(@(8.1, 40, 2, -10, 10), @(8.1, 40, 2, 10, 10), @(3, 40, 0, -10, 20), @(3, 18, 0, 10, 5))) {
    $ho = [ClothTests]::Hoisted($case[0], $case[1], $case[2], $case[3], $case[4]) -split ","
    Check ("the height changed {3} m in {4} frames: a {0} m flag at {1} cells does not tangle" -f $case[0], $case[1], $case[2], $case[3], $case[4]) ((N $ho[0]) -eq 0 -and (N $ho[1]) -lt 1.2) ($ho[2] + ": " + $ho[0] + " pairs crossing 6 s after (the first version left 42-70), stretch " + $ho[1])
}
$pm = [ClothTests]::PickMaths()
Check "the pick volumes' arithmetic: segments' closest points, a turned box's entry" ($pm -eq "True,True,True,True,True") $pm
$hold = [ClothTests]::Hold()
Check "the pick's held extents: wider at once, given back over the release (e^-1 of the gap in 4 s), afresh after a clear" ($hold -eq "-0.5 2|1.368|1") $hold
foreach ($speed in 2, 6, 12) {
    $ps = [ClothTests]::PickSteady($speed) -split ","
    Check ("$speed m/s: a pointer put on the flag and held still stays on it while it waves; the pick volume steadier") ((N $ps[1]) -ge 0.99 -and (N $ps[5]) -lt (N $ps[4])) ("held " + $ps[1] + " of the next 2 s (the first pick: " + $ps[0] + "), " + $ps[3] + " of 16 s (" + $ps[2] + "); its size changing " + $ps[5] + " m rms a tenth of a second (" + $ps[4] + ")")
}
$oc = [ClothTests]::OutlineCovers() -split ","
Check "the outline holds the cloth whatever it does: flying, limp, tilted, from its top, on a halyard in a storm" ((N $oc[1]) -ge 0) ("nearest its edge: " + $oc[0] + ", " + $oc[1] + " m inside; the disc " + $oc[2] + " L round the hoist")

# ---- tilted hoists and the pole ------------------------------------------------------------

$fr = [ClothTests]::Frames() -split ","
Check "the hoist's frame: unit, square; straight down untilted; level at 90 deg; leaning to the side" (($fr | Where-Object { $_ -ne "True" }).Count -eq 0) ($fr -join ",")
$tf = [ClothTests]::TopFrames()
Check "hoisted from the top: the hoist and the fly trade places at any heading, tilt and lean; the hoist along the heading, the fly down" ($tf -eq "True,True,True") $tf
$hang = [ClothTests]::TopBanner(0, 90, 12) -split ","
Check "a banner hoisted from its top, calm: it hangs straight down from its crossbar" ($hang[0] -eq "True" -and $hang[1] -eq "True" -and (N $hang[2]) -lt 1.15 -and (N $hang[3]) -gt 0.85) ("fly edge " + $hang[3] + " L below the crossbar, worst stretch " + $hang[2])
foreach ($case in @(@(6, 90), @(12, 90), @(6, 0), @(6, 45))) {
    $b = [ClothTests]::TopBanner($case[0], $case[1], 12) -split ","
    Check ("a banner hoisted from its top, {0} m/s {1} deg off its crossbar: finite, crossbar held, stretched at most 15%" -f $case[0], $case[1]) ($b[0] -eq "True" -and $b[1] -eq "True" -and (N $b[2]) -lt 1.15 -and (N $b[3]) -lt (N $hang[3])) ("fly edge " + $b[3] + " L below the crossbar (calm: " + $hang[3] + "), worst stretch " + $b[2])
}
# Placing, a right click turns the new flag as PropTool.ToggleAngle turns a prop (its IL):
# round(a / 45 - 1) x 45, Mathf.Round's banker's rounding at the halves.
$orient = $asm.GetType("RealFlag.Flags.FlagOrientation", $true)
$walk = @(); $heading = 0.0
for ($i = 0; $i -lt 9; $i++) { $heading = $orient::StepTurn($heading); $walk += $heading }
$offSteps = @(101, 100, 67.5, 112.5, 22.4, 359.6) | ForEach-Object { $orient::StepTurn($_) }
$wholes = @(-45, 359.6, 360, 720.4, -0.4, 12.5, 13.5, [single]::NaN) | ForEach-Object { $orient::WholeDegrees($_) }
$rightTurns = ($walk -join " ") + " | " + ($offSteps -join " ") + " | " + ($wholes -join " ")
Check "a right click while placing turns the flag as the game turns a prop: 45 deg down, from off them the nearest one's next; whole degrees 0..359" ($rightTurns -eq "315 270 225 180 135 90 45 0 315 | 45 45 0 90 315 315 | 315 0 0 0 0 12 14 0") $rightTurns
foreach ($case in @(@(45, 0, 0), @(45, 0, 6), @(45, 0, 12), @(90, 0, 6), @(30, 20, 12), @(-60, 0, 6))) {
    $t = [ClothTests]::Tilted($case[0], $case[1], $case[2], 12) -split ","
    Check ("hoist tilted {0} deg, leaning {1}, {2} m/s: finite, hoist on its line, stretched at most 15%" -f $case[0], $case[1], $case[2]) ($t[0] -eq "True" -and $t[1] -eq "True" -and (N $t[2]) -lt 1.15) ("worst stretch " + $t[2] + ", fly " + $t[3] + " L off the hoist line")
}
$turn = [ClothTests]::TurnHoist() -split ","
Check "a flying flag's hoist tilted 45 deg: the cloth turns whole, its hoist on the new line, flies on" ((N $turn[0]) -lt 0.5 -and $turn[1] -eq "True" -and $turn[2] -eq "True" -and (N $turn[3]) -lt 1.12 -and $turn[4] -eq "True") ("distances changed at most " + $turn[0] + " mm, then 3 s: worst stretch " + $turn[3])
foreach ($ropes in @(0, 2)) {
    foreach ($case in @(@(0, 0), @(2, 180), @(6, 180), @(12, 180), @(6, 90))) {
        $pl = [ClothTests]::Pole($case[0], $case[1], $ropes) -split ","
        # Contact stretches the links that hold the cloth against the pole, as it does against itself
        # (the self-collision checks allow 30%). On a halyard the free heading is pressed round the
        # pole between its clips, and the link next to a clip takes it: 20-22% at worst for moments
        # (median 6-8%, 95% of frames under 16%; the scratchpad's profile, 2026-09-26).
        $limit = $(if ($ropes -gt 0) { 1.25 } else { 1.20 })
        Check ("the cloth keeps off its pole ({2}): {0} m/s, wind {1} deg off its heading" -f $case[0], $case[1], $(if ($ropes -gt 0) { "on its halyard" } else { "sewn on" })) ($pl[0] -eq "True" -and (N $pl[2]) -ge -0.05 -and (N $pl[1]) -lt $limit) ("nearest particle " + $pl[2] + " cm off the pole's surface, worst stretch " + $pl[1])
    }
}
# On a halyard: held by the two clips alone; the heading between bows out -- at most as far as the
# middle of a string 3% longer than the line between its ends can, 0.12 of the hoist.
foreach ($case in @(@(3, 18, 0), @(3, 18, 6), @(3, 18, 12), @(3, 32, 6), @(8.1, 32, 6))) {
    $h = [ClothTests]::Halyard($case[0], $case[1], $case[2]) -split ","
    Check ("on its halyard: a {0} m flag at {1} cells, {2} m/s, held by its clips alone, its heading bowing, whole" -f $case[0], $case[1], $case[2]) ($h[0] -eq "True" -and $h[1] -eq "True" -and (N $h[2]) -lt 0.01 -and (N $h[3]) -gt 0.03 -and (N $h[4]) -lt 0.14 -and (N $h[5]) -lt 1.15 -and (N $h[6]) -eq 0 -and $h[7] -eq "True" -and (N $h[8]) -lt 0.01) ($h[9] + ": clips off by " + $h[2] + " mm; the heading's middle " + $h[3] + " of the hoist off its line on average, " + $h[4] + " at most; worst stretch " + $h[5] + "; " + $h[6] + " crossing pairs; its links made again both ways: " + $h[7] + "; sewn on again, off its line by " + $h[8] + " mm")
}
$bc = [ClothTests]::BottomClip() -split ","
Check "the lower rope ends at the bottom clip: 3% of the hoist up on a halyard, at the hoist's foot without ropes" ([Math]::Abs((N $bc[0]) - 0.97 * (N $bc[1])) -lt 0.001) ($bc[1] + " m hoist: " + $bc[0] + " m down with ropes, " + $bc[1] + " m without")
$ps = [ClothTests]::PoleSizes() -split ","
$sizes = $ps[0] -split " " | ForEach-Object { [int](($_ -split "/")[0]) }
Check "the pole's foot as the makers size it: ~16 cm for 3 x 2 m on 10 m, ~13 for 2.4 x 1.5 m on 9 m, 80-100 for a 30 m flag on 22 m" ($sizes[0] -ge 14 -and $sizes[0] -le 18 -and $sizes[1] -ge 11 -and $sizes[1] -le 15 -and $sizes[2] -ge 80 -and $sizes[2] -le 100 -and $ps[1] -eq "True" -and $ps[2] -eq "True") ("foot/top/hoist off the axis, cm: " + $ps[0] + "; hoist clear of the pole: " + $ps[1])

# ---- the cloth against itself --------------------------------------------------------------
# Every 0.1 s over 20 s, after 8 s to settle: the pairs of the drawn triangles that cross.

$off = [ClothTests]::Crossings(0, 1, 18, $false, 3, 2, 20) -split ","
Check "without self-collision, calm air: the cloth goes through itself (the check sees it)" ((N $off[0]) -gt 0.5) ("in " + $off[0] + " of the samples, " + $off[1] + " pairs on average, " + $off[2] + " at most")
foreach ($case in @(@(0, 1, 18, 3, 2), @(2, 1, 18, 3, 2), @(12, 1, 18, 3, 2), @(0, 2, 18, 3, 2), @(0, 1, 32, 3, 2), @(2, 1, 32, 3, 2), @(1.5, 2, 18, 14.5, 7.25), @(0, 1, 40, 3, 2))) {
    $x = [ClothTests]::Crossings($case[0], $case[1], $case[2], $true, $case[3], $case[4], 20) -split ","
    Check ("self-collision: {0} m flag, {1} m/s, waviness {2}%, {3} cells: never through itself" -f $case[3], $case[0], (100 * $case[1]), $case[2]) ((N $x[0]) -eq 0 -and (N $x[3]) -lt 1.30) ($x[4] + ": crossing in " + $x[0] + " of the samples, worst stretch " + $x[3])
}

# ---- the details ------------------------------------------------------------------------

$g = [ClothTests]::Grids(3, 2); $g1 = [ClothTests]::Grids(1, 0.667); $g15 = [ClothTests]::Grids(14.5, 7.25)
Check "the details' grids: finer on big flags, none under 8 cm" ($g -eq "19x13 25x17 33x22 39x26" -and $g1 -eq "13x9 13x9 13x9 13x9" -and $g15 -eq "19x10 25x13 33x17 41x21") ("3 x 2 m: " + $g + "; 1 m: " + $g1 + "; 14.5 m at 1:2: " + $g15)
foreach ($d in 24, 32, 40) {
    $c0 = [ClothTests]::Droop([ClothTests]::RunAt(0, 12, 1, $d)) -split ","
    $c2 = [ClothTests]::Droop([ClothTests]::RunAt(2, 12, 1, $d)) -split ","
    $c6 = [ClothTests]::Droop([ClothTests]::RunAt(6, 12, 1, $d)) -split ","
    Check "$d cells: hangs in calm air, lifts at 2 m/s, flies at 6 m/s" ((N $c0[0]) -gt 65 -and (N $c2[0]) -gt 10 -and (N $c2[0]) -lt 80 -and (N $c6[0]) -lt 15 -and (N $c6[1]) -gt 0.9) ("droop " + $c0[0] + " / " + $c2[0] + " / " + $c6[0] + " deg, reach at 6 m/s " + $c6[1] + " L")
}
foreach ($pair in @(@(18, 32), @(32, 18), @(18, 40))) {
    $cd = [ClothTests]::ChangeDetail($pair[0], $pair[1]) -split ","
    Check ("a flying flag changing detail {0} -> {1} cells keeps its shape and motion" -f $pair[0], $pair[1]) ([math]::Abs((N $cd[0]) - 1) -lt 0.05 -and [math]::Abs((N $cd[1]) - 1) -lt 0.2 -and $cd[2] -eq "True" -and (N $cd[3]) -lt 1.15) ($cd[4] + " -> " + $cd[5] + ": reach x" + $cd[0] + ", mean speed x" + $cd[1] + ", worst stretch after 0.5 s " + $cd[3])
}

# ---- the cloth's thread and the plan ------------------------------------------------------

$thread = [ClothTests]::Threaded() -split ","
Check "the cloth's thread steps as this one does" ($thread[0] -eq "True") "100 frames of 3 steps at 12 m/s, bit for bit"
Check "a job that throws is caught and handed back" ($thread[1] -eq "True") "NullReferenceException in the job"
Check "and the thread goes on" ($thread[2] -eq "True") "the next job done, its work counted"
Check "the thread stops" ($thread[3] -eq "True") "within 2 s"
$pool = [ClothTests]::Pool() -split ","
Check "four threads step eight flags as this one does" ($pool[0] -eq "True" -and $pool[2] -eq "4") ("every detail, 0-28 m/s, 60 frames, bit for bit: " + $pool[0] + ", " + $pool[2] + " threads")
Check "their work adds up, and they stop" ($pool[1] -eq "True" -and $pool[3] -eq "True") ("particle steps counted: " + $pool[1] + ", stopped: " + $pool[3])
$tf = @(4, 8, 12, 16, 28, 64) | ForEach-Object { [RealFlag.Flags.ClothWorker]::ThreadsFor($_) }
Check "threads for 4, 8, 12, 16, 28, 64 processors" (($tf -join ",") -eq "1,1,2,2,4,4") ($tf -join ",")

foreach ($rate in @(@(60, 600, 1), @(144, 600, 1), @(30, 600, 2), @(20, 600, 3), @(10, 300, 3))) {
    $fr = [ClothTests]::Frames($rate[0], 10) -split ","
    $what = if ($rate[0] -ge 20) { "every 1/60 s stepped" } else { "3 a frame at most, the rest let go" }
    Check ("{0} fps for 10 s: {1}" -f $rate[0], $what) ([math]::Abs([int]$fr[0] - $rate[1]) -le 1 -and [int]$fr[1] -eq $rate[2] -and $fr[2] -eq "True") ($fr[0] + " steps, at most " + $fr[1] + " a frame, alpha in 0..1: " + $fr[2])
}

$budget = [ClothTests]::Budget()
Check "over the budget the farther flag waits, then catches up; the nearest and the ghost always step" ($budget -eq "1,1,True,2,1,1") $budget
$plan = [RealFlag.Flags.ClothPlan]
$bf = @(144, 60, 30, 20, 10, 4) | ForEach-Object { $plan::BudgetFor(1.0 / $_, 1.0 / 60, 3, 8.0).ToString("F0") }
Check "the budget grows with the frame, to the 3 steps a flag takes in one (144, 60, 30, 20, 10, 4 fps)" (($bf -join ",") -eq "8,8,16,24,24,24") ($bf -join ",")
$sf = @($plan::SmoothFrame(1.0 / 60, 0.25), $plan::SmoothFrame(0.04, 1.0 / 60), $plan::SmoothFrame(0, 0.1)) | ForEach-Object { $_.ToString("F4") }
Check "the frame it is planned on: a tenth of the way up to a hitch, down at once" (($sf -join ",") -eq "0.0400,0.0167,0.1000") ($sf -join ",")
$was = [ClothTests]::Turns(5, $true, $true, $false) -split ","
$now = [ClothTests]::Turns(5, $true, $false, $true) -split ","
Check "the author's five flags at 10 fps: 3 stood still under the first rule, all move now" ([int]$was[0] -eq 0 -and [int]$now[0] -eq 300) ("fewest steps in 10 s: " + $was[0] + " then, " + $now[0] + " now (most " + $now[1] + ")")
$crowd = [ClothTests]::Turns(12, $false, $false, $true) -split ","
$crowdNearest = [ClothTests]::Turns(12, $false, $false, $false) -split ","
Check "more flags than the threads have time for: they take turns, none stands still" ([int]$crowdNearest[0] -eq 0 -and [int]$crowd[0] -ge 0.5 * [int]$crowd[1]) ("12 at 10 fps, fewest/most steps: nearest first " + ($crowdNearest -join "/") + ", the waited first " + ($crowd -join "/"))
$paused = [ClothTests]::Paused()
Check "paused: a handled flag steps the frame's time, slowing to none over the fade, none left alone" ($paused -eq "1.00,0.50,0.00,0.00,0.00") $paused

# ---- the drawn surface ---------------------------------------------------------------------

$sf = [ClothTests]::Surface() -split ","
Check "the drawn surface passes through every particle, joins cell to cell, faces as the cloth" ((N $sf[0]) -lt 0.01 -and (N $sf[1]) -lt 0.5 -and (N $sf[2]) -gt 0.8) ("worst miss " + $sf[0] + " mm, worst jump across a cell edge " + $sf[1] + " mm, least cosine to the cloth's normal " + $sf[2])
$sm = [ClothTests]::Smoothing() -split ","
Check "the edges' smoothing takes out a zigzag, keeps a wave, holds the hoist and corners" ((N $sm[0]) -lt 1.5 -and (N $sm[1]) -gt 0.9 -and $sm[2] -eq "True") ("a 10 mm zigzag left at " + $sm[0] + " mm rms (next to the held corners), a six-particle wave keeps " + $sm[1] + " of its height, held: " + $sm[2])

# ---- data ---------------------------------------------------------------------------------

# A `dotnet build -t:Compile` once left them out (RealFlag.csproj's PrepareResourcesBeforeCompile).
$resources = @($asm.GetManifestResourceNames() | Sort-Object)
$others = @($resources | Where-Object { $_ -notlike "RealFlag.Flags.*" })
Check "the DLL carries its shader bundle, icon and language files" (($resources -contains "RealFlag.Resources.realflag-win.bundle") -and ($resources -contains "RealFlag.Resources.FlagIcon.png") -and ($resources -contains "Localization.en.xml")) (($others -join ", ") + " and " + ($resources.Count - $others.Count) + " flag images")

$codec = [ClothTests]::Codec() -split ","
Check "the city's flags survive the save" ($codec[0] -eq "True") "two specs, name and colours"
Check "Move It's copy survives its clipboard" ($codec[1] -eq "True") "EncodeOne / TryDecodeOne"
Check "garbage is refused whole" ($codec[2] -eq "True") "a count past the data"
Check "a newer format is refused, and says so" ($codec[3] -eq "True") "version 9"
$cc = [ClothTests]::CodecCountries() -split ","
Check "a country's flag survives the save, at its own proportions" ($cc[0] -eq "True") ("format " + $cc[3] + ": 'US' -> us, 10:19 whatever the spec said, name and size kept")
Check "a save of the first format still reads" ($cc[1] -eq "True") "format 1, byte by byte: a saltire, no country"
Check "a country this version does not know is kept" ($cc[2] -eq "True") "zz-later, drawn as its pattern"
$cp = [ClothTests]::CodecPole() -split ","
Check "the pole and the tilt survive the save" ($cp[0] -eq "True") ("format " + $cp[3] + ": spear, one rope, three colours, 150%, tilt 45, lean -12.5")
Check "a save of format 2 still reads: no pole, straight down" ($cp[1] -eq "True") "format 2, byte by byte: Denmark's code, the pole's defaults ready"
Check "a pole or tilt out of range is pulled in" ($cp[2] -eq "True") "style 9, 5 ropes, 700%, tilt 200, lean NaN"
$cd = [ClothTests]::CodecDarkness() -split ","
Check "the cloth's darkness survives the save" ($cd[0] -eq "True") ("format " + $cd[4] + ": Japan at 35%")
Check "a save of format 3 still reads: the colours as designed" ($cd[1] -eq "True") "format 3, byte by byte: Japan on a ball-topped pole, tilted 30 deg"
Check "a darkness out of range is pulled in" ($cd[2] -eq "True") "200%, NaN, -30%"
Check "the darkness darkens evenly to the eye: 20% is sRGB 0.8, 50% mid grey" ($cd[3] -eq "0.218 0.612 1.000") ("brightness at 50%, 20%, none: " + $cd[3])
$ch = [ClothTests]::CodecHoist() -split ","
Check "a flag hoisted from its top survives the save, at a banner's 3:1" ($ch[0] -eq "True") ("format " + $ch[3])
Check "a save of format 4 still reads: hoisted from its side" ($ch[1] -eq "True") "format 4, byte by byte: a cross, 25% darker"
Check "an edge or a proportion out of range is pulled in; a tall flag's hoist held to 30 m" ($ch[2] -eq "True") "edge 7, 6:1; 30 m at 5:1 -> 6 m"
$pl = [ClothTests]::CodecPoleLength() -split ","
Check "a drawn pole keeps its own length in the save (Move It moves it with the flag)" ($pl[0] -eq "True" -and $pl[3] -eq "True") ("format " + $pl[4] + ": 37.5 m, from the top; another length is another pole")
Check "a save of format 5 still reads: the pole's length found again, once" ($pl[1] -eq "True") "format 5, byte by byte: a spear-topped pole, no length yet"
Check "a pole length out of range is pulled in" ($pl[2] -eq "True") "NaN, -4 m, 5 km"
$sizes = [ClothTests]::Sizes()
$sizesWanted = "3x2 2x3|4.5x3@667 4.5x3@667 15x30@2000 4x6@667|3x2.5@833 4x20@5000 2.5x3@833|2x3@1500 3x1.5@2000 0.5x1.5@3000|1499 True False|3:2 2:1 3:1 1.22:1 1:1.36 1:2.55 2:3"
Check "width and height as seen: linked, the whole flag; unlinked, a side (the other pushed past 1:5..5:1); a shape keeps the longer side; the shapes' names" ($sizes -eq $sizesWanted) $sizes

$ct = [ClothTests]::Countries() -split "\|"
$ctf = $ct[0] -split ","
Check "the countries: 256, codes unique, proportions in range, found both ways" ($ctf[0] -eq "256" -and $ctf[1] -eq "True" -and $ctf[2] -eq "True" -and $ctf[3] -eq "True" -and $ctf[4] -eq "True" -and $ctf[5] -eq "True") ($ct[0])
Check "the search: words of the name, accents aside, or the code" ($ct[1] -eq "ci gb gb-sct ci ci st") $ct[1]
Check "their own proportions: Nepal taller than long, Qatar 11:28, Switzerland square; Svalbard flies Norway's" ($ct[2] -eq "1219 392 1000 no 1:2.55 2:3") $ct[2]
$img = [ClothTests]::Images($asm.GetManifestResourceNames()) -split ","
Check "every flag's image and thumbnail are in the DLL" ($img[1] -eq "0") ($img[0] + " images, " + $img[1] + " missing")
Check "a spec from a save or a paste is pulled into range" ([ClothTests]::Sanitise() -eq "5,30.0,667,1.0,#123456,True") ([ClothTests]::Sanitise())

$colours = [ClothTests]::Colours() -split "\|"
Check "hex colours read and written" ($colours[0] -eq "#1A2B3C #00FF00 refused refused") $colours[0]
$reg = [ClothTests]::PickRegistry()
Check "the flags Move It's pointer walks: published, taken away, once each; a snapshot is never changed" ($reg -eq "5 700|5 9|0") $reg

# The game's Harmony (2.0.4, CitiesHarmony) writes a patch's __args back over the original's ref
# arguments after the patch runs: FlagMoveItPick's postfix took it, and Move It's search lost every
# prop it found, the flags too (see FlagMoveItPick's remarks). No method of the mod may take it.
$types = @()
try { $types = $asm.GetTypes() }
catch {
    $e = $_.Exception
    while ($e -ne $null -and -not ($e -is [Reflection.ReflectionTypeLoadException])) { $e = $e.InnerException }
    if ($e -ne $null) { $types = @($e.Types | Where-Object { $_ -ne $null }) }
}
$argsTakers = @()
foreach ($t in $types) {
    $methods = @()
    try { $methods = $t.GetMethods([Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly') } catch { }
    foreach ($m in $methods) {
        try { if (@($m.GetParameters() | Where-Object { $_.Name -eq "__args" }).Count -gt 0) { $argsTakers += $t.Name + "." + $m.Name } } catch { }
    }
}
Check "no Harmony patch takes __args (Harmony 2.0.4 writes it back over the ref arguments)" ($types.Count -gt 50 -and $argsTakers.Count -eq 0) ($(if ($argsTakers.Count -eq 0) { "none in " + $types.Count + " types" } else { $argsTakers -join ", " }))
$pasted = [ClothTests]::Pasted()
Check "a pasted colour code: alone, short, inside other text; the first of two; nothing that is not one" ($pasted -eq "#1A2B3C #1A2B3C #AABBCC #AABBCC #FF8800 #00FF00 #FF0000 refused refused refused refused refused") $pasted
Check "every palette colour survives HSV and back" ($colours[1] -eq "0") ($colours[1] + " changed")
Check "sRGB 0.5 is linear 0.2140, 1 is 1" ($colours[2] -eq "0.2140" -and $colours[3] -eq "1.0000") ($colours[2] + ", " + $colours[3])

$p = $asm.GetType("RealFlag.Flags.FlagProportion", $true)
Check "2:3 is 0.667 of the length" ([math]::Abs($p::Ratio(667) - 0.667) -lt 1e-6 -and $p::Name(667) -eq "2:3") ($p::Name(667))

# ---- cost ---------------------------------------------------------------------------------

foreach ($speed in 0, 6, 12, 20, 30) {
    $us = [ClothTests]::MicrosecondsPerStep($speed)
    $sub = $clothType::SubSteps($w::SpeedAt($speed, 12), $step, 3.0 / 18.0)
    Write-Host ("      cost: {0,6:F1} us per 1/60 s step at {1} m/s in .NET ({2} sub-step(s); x5 in Unity 5.6's Mono: gpu-render.ps1 -Bench)" -f $us, $speed, $sub)
}
foreach ($d in 24, 32, 40) {
    $line = @(0, 6, 12) | ForEach-Object { "{0:F0} us at {1} m/s" -f [ClothTests]::MicrosecondsPerStepAt($_, $d), $_ }
    Write-Host ("      cost at {0} cells: {1}" -f $d, ($line -join ", "))
}

Write-Host ""
if ($failed -eq 0) { Write-Host "all passed" } else { Write-Host "$failed FAILED"; exit 1 }
