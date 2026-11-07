# Offline checks of string flags' PURE code (StringDesign.cs: StringSpec, StringCodec, StringCurve,
# StringLayout, StringSwing, StringFlags; StringMotion.cs), against the built mod DLL, no game needed:
#
#   dotnet build RealFlag/RealFlag.csproj -p:Deploy=false
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-strings.ps1 [-Dll RealFlag\obj\Debug\RealFlag.dll]
#
# What is covered:
# - the span hangs as a catenary through both ends at any heights: its sag as set (a level span),
#   its length the parabola's slack over the chord, its arc length and its inverse agreeing;
#   nearly upright or very short, a straight line;
# - the flags along it: the gap or the count, centred between the bare ends, none too many;
# - the swing: hanging straight in calm air, blown out further in more wind (tan phi = load /
#   weight), settling there as a damped pendulum of the period a sag that deep has;
# - the sequences: designs in turn, back and forth, shuffled rounds (each design once a round,
#   never twice running); countries all, by region, the player's list as picked or sorted, the
#   lead flag first and every n; the same seed, the same order;
# - the save's bytes: a round trip byte for byte, the flags' own format inside it, garbage and
#   newer formats refused whole (Invariant 18 keeps them);
# - shared motion (StringMotion): the ring of a source's steps, read back between two steps.
param(
    [string]$Dll = (Join-Path $PSScriptRoot "..\RealFlag\obj\Debug\RealFlag.dll")
)

$ErrorActionPreference = "Stop"
$managed = "C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed"
if (-not (Test-Path $Dll)) { throw "No DLL at $Dll -- build it first (dotnet build RealFlag/RealFlag.csproj -p:Deploy=false)" }

# Loaded from bytes: Smart App Control can block a fresh unsigned DLL by path.
$asm = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes((Resolve-Path $Dll)))

$resolver = @'
using System;
using System.IO;
using System.Reflection;
public static class StringTestResolver
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
[StringTestResolver]::Mod = $asm
[StringTestResolver]::Managed = $managed
[StringTestResolver]::Install()

$harness = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RealFlag.Flags;

public static class StringTests
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) { return v.ToString("0.####", Inv); }

    // level, rise, sag -> deepest drop, ends' heights, length vs arc, inverse error
    public static string Curve(float level, float rise, float sag)
    {
        StringCurve c = StringCurve.Through(level, rise, sag);
        double worst = 0;
        for (int i = 0; i <= 50; i++)
        {
            float x = level * i / 50f;
            worst = Math.Max(worst, Math.Abs(c.XAt(c.ArcTo(x)) - x));
        }
        return string.Join(",", new[] { F(c.Deepest), F(c.HeightAt(0f)), F(c.HeightAt(level)), F(c.Length), F(c.ArcTo(level)), F(worst), c.Straight ? "1" : "0" });
    }

    public static string Layout(float length, int spacing, float gap, int count, float along, float margin)
    {
        StringSpec s = StringSpec.Default;
        s.Spacing = spacing; s.Gap = gap; s.Count = count; s.Margin = margin;
        s.Mount = StringMount.Hoist; s.Height = along;
        StringLayout l = StringLayout.For(s.Sanitised(), length);
        float end = l.Count > 0 ? l.First + (l.Count - 1) * l.Pitch + l.Along : 0f;
        // count, first, pitch, the bare string after the last flag
        return string.Join(",", new[] { l.Count.ToString(Inv), F(l.First), F(l.Pitch), F(length - end) });
    }

    // the held angle at speeds; after 20 s of steps at 6 m/s, the angle; the period of a 0.5 m sag
    public static string Swing()
    {
        float area = 0.3f * 0.25f / 0.33f;
        float h0 = StringSwing.Held(0f, area, 0.004f, StringKind.Cord);
        float h3 = StringSwing.Held(3f, area, 0.004f, StringKind.Cord);
        float h6 = StringSwing.Held(6f, area, 0.004f, StringKind.Cord);
        float h12 = StringSwing.Held(12f, area, 0.004f, StringKind.Cord);
        var sw = new StringSwing();
        float omega = StringSwing.Omega(0.5f), most = 0f;
        for (int i = 0; i < 1200; i++)
        {
            sw.Step(1f / 60f, h6, omega);
            most = Math.Max(most, sw.Angle);
        }
        double period = 2 * Math.PI / omega;
        return string.Join(",", new[] { F(h0), F(h3), F(h6), F(h12), F(sw.Angle), F(most), F(period) });
    }

    static StringSpec Alternating(int order, int n)
    {
        StringSpec s = StringSpec.Default;
        s.Sequence = StringSequence.Alternate;
        s.DesignOrder = order;
        var d = new FlagSpec[n];
        for (int i = 0; i < n; i++) { d[i] = FlagSpec.Default; d[i].Pattern = FlagPattern.Plain; d[i].Colour1 = (uint)(0x100000 * (i + 1)); }
        s.Designs = d;
        return s;
    }

    public static string Designs(int order, int n, int flags)
    {
        var f = new StringFlags(Alternating(order, n), FlagCountries.ByName);
        var r = new List<string>();
        for (int i = 0; i < flags; i++) r.Add(f.DesignIndex(i).ToString(Inv));
        return string.Join("", r.ToArray());
    }

    // shuffled rounds: every round holds each design once; never the same design twice running
    public static string Rounds(int n, int rounds)
    {
        var f = new StringFlags(Alternating(StringOrder.Random, n), FlagCountries.ByName);
        int bad = 0, repeats = 0, last = -1;
        for (int r = 0; r < rounds; r++)
        {
            var seen = new bool[n];
            for (int k = 0; k < n; k++)
            {
                int d = f.DesignIndex(r * n + k);
                if (seen[d]) bad++;
                seen[d] = true;
                if (d == last) repeats++;
                last = d;
            }
        }
        return bad + "," + repeats;
    }

    static StringSpec Countries(int which, int region, string[] list, int order, string lead, int every, int seed)
    {
        StringSpec s = StringSpec.Default;
        s.Sequence = StringSequence.Countries;
        s.Which = which; s.Region = region; s.Countries = list; s.CountryOrder = order;
        s.Lead = lead; s.LeadEvery = every; s.Seed = seed;
        return s;
    }

    public static string CountryCount(int which, int region)
    {
        var f = new StringFlags(Countries(which, region, new string[0], CountryOrder.AToZ, "", 0, 1), FlagCountries.ByName);
        return f.Countries.Length.ToString(Inv);
    }

    public static string CountrySeq(int which, string list, int order, string lead, int every, int seed, int flags)
    {
        var f = new StringFlags(Countries(which, FlagRegions.Europe, list.Length > 0 ? list.Split(',') : new string[0], order, lead, every, seed), FlagCountries.ByName);
        var r = new List<string>();
        for (int i = 0; i < flags; i++) r.Add(f.CountryAt(i) ?? "-");
        return string.Join(" ", r.ToArray());
    }

    // every country in a region-ordered all: regions never go back
    public static string RegionsInOrder()
    {
        var f = new StringFlags(Countries(CountryWhich.All, 0, new string[0], CountryOrder.ByRegion, "", 0, 1), FlagCountries.ByName);
        int back = 0, last = -1;
        foreach (string c in f.Countries) { int r = FlagRegions.Of(c); if (r < last) back++; last = r; }
        return back + "," + FlagRegions.Of("fr") + "," + FlagRegions.Of("jp") + "," + FlagRegions.Of("br") + "," + FlagRegions.Of("ng") + "," + FlagRegions.Of("au") + "," + FlagRegions.Of("gb-sct") + "," + FlagRegions.Of("eu");
    }

    public static StringSpec Rich()
    {
        StringSpec s = Alternating(StringOrder.BackAndForth, 3);
        s.Designs[1].Country = "fr";
        s.Designs[2].Image = "My flag.png";
        s.Designs[2].Wear = 0.4f;
        s.Countries = new[] { "jp", "de", "fr" };
        s.Lead = "ca";
        s.LeadEvery = 4;
        s.Name = "F" + (char)0xEA + "te du quartier " + (char)0x56FD;
        s.Kind = StringKind.Tape;
        s.Thickness = 0.02f;
        s.Sag = 0.05f;
        s.Mount = StringMount.Top;
        return s.Sanitised();
    }

    public static string RoundTrip()
    {
        StringSpec s = Rich();
        byte[] a = StringCodec.EncodeOne(s);
        StringSpec back;
        bool ok = StringCodec.TryDecodeOne(a, out back);
        byte[] b = StringCodec.EncodeOne(back);
        bool same = a.Length == b.Length;
        for (int i = 0; same && i < a.Length; i++) same = a[i] == b[i];
        return (ok ? "1" : "0") + "," + (same ? "1" : "0") + "," + back.Name + "," + back.Designs[1].Country + "," + back.Designs[2].Image + "," + string.Join("|", back.Countries) + "," + a.Length;
    }

    public static string All()
    {
        var spans = new List<KeyValuePair<ushort, StringSpec>>();
        spans.Add(new KeyValuePair<ushort, StringSpec>(12, Rich()));
        spans.Add(new KeyValuePair<ushort, StringSpec>(40000, StringSpec.Default));
        byte[] data = StringCodec.EncodeAll(spans);
        string problem;
        var back = StringCodec.DecodeAll(data, out problem);
        string r = back != null ? back.Count + ":" + back[0].Key + ":" + back[1].Key : "null " + problem;

        // newer format, a cut-off copy, garbage: refused whole
        byte[] newer = (byte[])data.Clone(); newer[0] = (byte)(StringCodec.Version + 1);
        byte[] cut = new byte[data.Length - 3]; Array.Copy(data, cut, cut.Length);
        byte[] longer = new byte[data.Length + 2]; Array.Copy(data, longer, data.Length);
        var g = new byte[64]; new Random(3).NextBytes(g); g[0] = 1;
        string p1, p2, p3, p4;
        r += "," + (StringCodec.DecodeAll(newer, out p1) == null ? "refused" : "read");
        r += "," + (StringCodec.DecodeAll(cut, out p2) == null ? "refused" : "read");
        r += "," + (StringCodec.DecodeAll(longer, out p3) == null ? "refused" : "read");
        r += "," + (StringCodec.DecodeAll(g, out p4) == null ? "refused" : "read");
        r += "," + (StringCodec.DecodeAll(null, out p4).Count);
        return r;
    }

    // a span whose flags are of a newer flag format than this copy: refused, not misread
    public static string NewerFlags()
    {
        byte[] a = StringCodec.EncodeOne(StringSpec.Default);
        // format byte, then width, height, mount, shape, depth, spacing, gap, count, margin, sequence; then the flags' format
        int at = 1 + 4 + 4 + 1 + 1 + 4 + 1 + 4 + 2 + 4 + 1;
        int was = a[at];
        a[at] = (byte)(FlagCodec.Version + 1);
        StringSpec s;
        return was + "," + (StringCodec.TryDecodeOne(a, out s) ? "read" : "refused");
    }

    public static string Sanitise()
    {
        StringSpec s = StringSpec.Default;
        s.Width = float.NaN; s.Height = 99f; s.Sag = -1f; s.Kind = 77; s.Designs = new FlagSpec[12]; s.Countries = new[] { "FR", "fr", " de ", "" };
        s.Lead = "JP"; s.Motions = 99; s.Gap = float.PositiveInfinity;
        s = s.Sanitised();
        return string.Join(",", new[] { F(s.Width), F(s.Height), F(s.Sag), s.Kind.ToString(Inv), s.Designs.Length.ToString(Inv), string.Join("|", s.Countries), s.Lead, s.Motions.ToString(Inv), F(s.Gap) });
    }

    // the shared cloths take the string's cut (FlagCut; the cut itself is test-cloth's): a pennant
    // string's and a rectangle's, their cuts, and the same key twice the same, another depth another
    public static string Cuts()
    {
        var pennant = StringMotion.SourceKey.For(0.25f, 0.3f, FlagCut.Pennant, 0.3f, 1f, 18, 0, false);
        var swallow = StringMotion.SourceKey.For(0.25f, 0.3f, FlagCut.Swallowtail, 0.3f, 1f, 18, 0, false);
        var swallow2 = StringMotion.SourceKey.For(0.25f, 0.3f, FlagCut.Swallowtail, 0.5f, 1f, 18, 0, false);
        var pennantDeep = StringMotion.SourceKey.For(0.25f, 0.3f, FlagCut.Pennant, 0.5f, 1f, 18, 0, false);
        var rect = StringMotion.SourceKey.For(0.25f, 0.3f, FlagCut.Rectangle, 0.3f, 1f, 18, 0, false);
        var a = new StringMotion.Source(pennant, 0f);
        var b = new StringMotion.Source(rect, 0f);
        return a.Cloth.Cut + "," + b.Cloth.Cut + "," + (swallow.Equals(swallow2) ? "same" : "differ") + "," + (pennant.Equals(pennantDeep) ? "same" : "differ");
    }

    // The author's runs (2026-11-07): 1446 sources for two spans (the wind's speed and angle in the
    // key), then a fresh cloth every 10-40 s as Volumetric Weather's wind crossed three wind
    // classes. Ten minutes of a gusty wind wandering ALL the way round (2-9 m/s) on a span of 30
    // pennants: the keys its flags ask for (only the slots now), and how often the flags swing
    // round to the other side (held 15 deg past the string's line).
    public static string Churn()
    {
        var keys = new HashSet<StringMotion.SourceKey>();
        float side = 0f;
        int swings = 0;
        var rnd = new Random(11);
        double dir = 80.0;
        for (int f = 0; f < 36000; f++)
        {
            double t = f / 60.0;
            dir += (rnd.NextDouble() - 0.5) * 2.0 + 0.6 * Math.Sin(t * 0.05);
            float was = side;
            side = StringMotion.OnString.SideFor((float)dir, side);
            if (was != 0f && was != side) swings++;
            for (int k = 0; k < 30; k++)
                keys.Add(StringMotion.SourceKey.For(0.25f, 0.3f, FlagCut.Pennant, 0.33f, 1f, 18, StringMotion.SlotFor(k, StringMotion.AutoMotions(30)), false));
        }
        double turned = Math.Abs(dir - 80.0);
        return keys.Count + "," + swings + "," + F(turned);
    }

    // the side: from -Z's side out to +Z (+1) and back; held until 15 deg past the string's line
    public static string Side()
    {
        float[] v = {
            StringMotion.OnString.SideFor(90f, 0f), StringMotion.OnString.SideFor(270f, 0f),
            StringMotion.OnString.SideFor(350f, 1f), StringMotion.OnString.SideFor(340f, 1f),
            StringMotion.OnString.SideFor(190f, -1f), StringMotion.OnString.SideFor(160f, -1f) };
        var r = new string[v.Length];
        for (int i = 0; i < v.Length; i++) r[i] = v[i].ToString(Inv);
        return string.Join(",", r);
    }

    // swinging round: 2 s from one side to the other at 60 fps, smooth at the ends; the speed's share
    public static string SwingRound()
    {
        float side = 1f;
        int steps = 0;
        while (side > -1f && steps < 10000) { side = StringMotion.OnString.Ease(side, -1f, 1f / 60f); steps++; }
        return steps + "," + F(StringMotion.OnString.ZScale(1f)) + "," + F(StringMotion.OnString.ZScale(0f)) + "," + F(StringMotion.OnString.ZScale(0.5f)) + "," +
               F(StringMotion.OnString.Share(0f)) + "," + F(StringMotion.OnString.Share(90f)) + "," + F(StringMotion.OnString.Share(180f)) + "," + F(StringMotion.OnString.Share(-90f));
    }

    // a source nobody plays is held, not stepped, and carries on from there when played again (no
    // fresh cloth, no rest); no self-collision on string cloths
    public static string Held()
    {
        var key = StringMotion.SourceKey.For(0.25f, 0.3f, FlagCut.Pennant, 0.33f, 1f, 18, 0, false);
        var s = new StringMotion.Source(key, 0f);
        ClothJob job;
        for (int i = 0; i < 400; i++) { s.Want(5f); if (s.Plan(1f / 60f, 4, 12, 1f, false, out job)) { ClothWorker.Execute(ref job); s.Took(ref job); } }
        float clock = s.Clock, x = s.Cloth.X[s.Cloth.Count - 1];
        int planned = 0;
        for (int i = 0; i < 600; i++) { if (s.Plan(1f / 60f, 4, 12, 1f, false, out job)) planned++; }
        bool still = s.Clock == clock && s.Cloth.X[s.Cloth.Count - 1] == x && s.Ready;
        s.Want(5f);
        bool again = s.Plan(1f / 60f, 4, 12, 1f, false, out job);
        int steps = again ? job.Steps : 0;
        return planned + "," + (still ? "held" : "moved") + "," + steps + "," + (s.Cloth.SelfCollision ? "collides" : "no-self");
    }

    // a source's wind: what its flags want, eased (not jumping with a gust), never a new cloth
    public static string Eased()
    {
        var key = StringMotion.SourceKey.For(0.25f, 0.3f, FlagCut.Pennant, 0.33f, 1f, 18, 0, false);
        var s = new StringMotion.Source(key, 0f);
        ClothJob job;
        for (int i = 0; i < 400; i++) { s.Want(4f); if (s.Plan(1f / 60f, 4, 12, 1f, false, out job)) { ClothWorker.Execute(ref job); s.Took(ref job); } }
        float before = s.Speed;
        s.Want(12f);
        s.Plan(1f / 60f, 4, 12, 1f, false, out job);
        float after = s.Speed;
        return before.ToString("0.###", Inv) + "," + after.ToString("0.###", Inv) + "," + (s.Ready ? "ready" : "warming");
    }

    // the default string: a pennant, of the flags' cuts
    public static string DefaultCut()
    {
        StringSpec s = StringSpec.Default;
        StringSpec bad = s; bad.Shape = 99; bad.Depth = float.NaN;
        bad = bad.Sanitised();
        return s.Shape + "," + bad.Shape + "," + bad.Depth.ToString("0.###", Inv);
    }

    // the ring: a cloth's steps kept, read between two
    public static string Ring()
    {
        var cloth = new FlagCloth(0.3f, 0.25f, FlagFabric.Standard(1f), 0f, 1f, 18, 1f, 0f, 0f);
        var ring = new StringMotion.Ring(cloth.Count);
        for (int i = 0; i < 300; i++)
        {
            for (int k = 0; k < cloth.Count; k++) { cloth.X[k] = i; cloth.Y[k] = -i; cloth.Z[k] = 2 * i; }
            ring.Add(cloth, i / 60f);
        }
        var x = new float[cloth.Count]; var y = new float[cloth.Count]; var z = new float[cloth.Count];
        bool ok1 = ring.Sample(299 / 60f - 1f, x, y, z);                 // exactly step 239
        float a = x[0];
        bool ok2 = ring.Sample(299 / 60f - 1f - 0.5f / 60f, x, y, z);    // half way to 238
        float b = x[0], bz = z[0];
        bool ok3 = ring.Sample(-100f, x, y, z);                           // older than kept: the oldest
        float c = x[0];
        return string.Join(",", new[] { ok1 ? "1" : "0", F(a), ok2 ? "1" : "0", F(b), F(bz), ok3 ? "1" : "0", F(c), F(ring.Span) });
    }
}
'@
$hp = New-Object System.CodeDom.Compiler.CompilerParameters
$hp.GenerateInMemory = $true
$hp.CompilerOptions = "/optimize+"
[void]$hp.ReferencedAssemblies.Add("System.dll")
[void]$hp.ReferencedAssemblies.Add("System.Core.dll")
[void]$hp.ReferencedAssemblies.Add((Resolve-Path $Dll).Path)
[void]$hp.ReferencedAssemblies.Add((Join-Path $managed "UnityEngine.dll"))
Add-Type -TypeDefinition $harness -CompilerParameters $hp

$failed = 0
function Check([string]$name, [bool]$ok, $detail) {
    if ($ok) { Write-Host "ok    $name  ($detail)" } else { Write-Host "FAIL  $name  ($detail)"; $script:failed++ }
}
$inv = [Globalization.CultureInfo]::InvariantCulture
function N([string]$s) { return [double]::Parse($s, $inv) }

# ---- the curve ------------------------------------------------------------------------------
$c = [StringTests]::Curve(10, 0, 0.05) -split ","
Check "a level 10 m span at 5% sags 0.5 m at its middle" ([math]::Abs((N $c[0]) - 0.5) -lt 0.01) ("deepest " + $c[0] + " m")
Check "its ends at the clicked heights" ((N $c[1]) -eq 0 -and [math]::Abs((N $c[2])) -lt 1e-3) ("0 m: " + $c[1] + ", 10 m: " + $c[2])
Check "its length the parabola's, its arc the same" ([math]::Abs((N $c[3]) - 10.0667) -lt 1e-3 -and [math]::Abs((N $c[3]) - (N $c[4])) -lt 1e-3) ("length " + $c[3] + ", arc " + $c[4])
Check "arc length and its inverse agree" ((N $c[5]) -lt 1e-3) ("worst " + $c[5] + " m")

$c = [StringTests]::Curve(12, 4, 0.03) -split ","
Check "a sloping span (12 m along, 4 m up) through both ends" ([math]::Abs((N $c[1])) -lt 1e-3 -and [math]::Abs((N $c[2]) - 4) -lt 2e-3) ("ends " + $c[1] + " / " + $c[2] + " m")
Check "its slack over the chord, its arc the same" ([math]::Abs((N $c[3]) - (N $c[4])) -lt 2e-3 -and (N $c[0]) -gt 0.2 -and (N $c[0]) -lt 0.6) ("length " + $c[3] + ", arc " + $c[4] + ", sag below the chord " + $c[0] + " m")
Check "arc length and its inverse agree, sloping" ((N $c[5]) -lt 1e-3) ("worst " + $c[5] + " m")

$c = [StringTests]::Curve(0.01, 5, 0.03) -split ","
Check "nearly upright: straight" ($c[6] -eq "1" -and [math]::Abs((N $c[3]) - 5) -lt 1e-3) ("straight " + $c[6] + ", length " + $c[3])
$c = [StringTests]::Curve(40, -15, 0.15) -split ","
Check "a long steep deep span stays finite and through its ends" ([math]::Abs((N $c[2]) + 15) -lt 0.01 -and (N $c[5]) -lt 0.01) ("end " + $c[2] + ", inverse " + $c[5])

# ---- the layout ------------------------------------------------------------------------------
$l = [StringTests]::Layout(10, 0, 0.08, 0, 0.25, 0.3) -split ","
Check "by gap: 28 flags of 25 cm 8 cm apart on 9.4 m" ($l[0] -eq "28") ("count " + $l[0] + ", pitch " + $l[2])
Check "centred: the bare string the same at both ends" ([math]::Abs((N $l[1]) - (N $l[3])) -lt 1e-3) ("first at " + $l[1] + ", after the last " + $l[3])
$l = [StringTests]::Layout(10, 1, 0, 12, 0.25, 0.3) -split ","
Check "by count: 12 flags from margin to margin" ($l[0] -eq "12" -and [math]::Abs((N $l[1]) - 0.3) -lt 1e-3 -and [math]::Abs((N $l[3]) - 0.3) -lt 1e-3) ("first " + $l[1] + ", end " + $l[3] + ", pitch " + $l[2])
$l = [StringTests]::Layout(10, 1, 0, 200, 0.25, 0.3) -split ","
Check "by count: never more than fit" ($l[0] -eq "37") ("count " + $l[0])
$l = [StringTests]::Layout(0.5, 0, 0.08, 0, 0.25, 0.3) -split ","
Check "a span shorter than its bare ends: no flags" ($l[0] -eq "0") ("count " + $l[0])

# ---- the swing ------------------------------------------------------------------------------
$s = [StringTests]::Swing() -split ","
Check "calm: hangs straight down" ((N $s[0]) -eq 0) ("held " + $s[0])
Check "more wind, blown out further, never past level" ((N $s[1]) -lt (N $s[2]) -and (N $s[2]) -lt (N $s[3]) -and (N $s[3]) -lt [math]::PI / 2) ("3/6/12 m/s: " + $s[1] + " / " + $s[2] + " / " + $s[3] + " rad")
Check "settles where the wind holds it" ([math]::Abs((N $s[4]) - (N $s[2])) -lt 0.01) ("after 20 s " + $s[4] + ", held " + $s[2])
Check "damped: overshoots little" ((N $s[5]) -lt (N $s[2]) * 1.5) ("most " + $s[5])
Check "a 0.5 m sag swings in about 1.27 s" ([math]::Abs((N $s[6]) - 1.27) -lt 0.02) ("period " + $s[6] + " s")

# ---- the sequences --------------------------------------------------------------------------
$d = [StringTests]::Designs(0, 3, 9)
Check "in turn" ($d -eq "012012012") $d
$d = [StringTests]::Designs(1, 3, 9)
Check "back and forth" ($d -eq "012101210") $d
$d = [StringTests]::Designs(1, 2, 6)
Check "back and forth of two is in turn" ($d -eq "010101") $d
$r = [StringTests]::Rounds(4, 200) -split ","
Check "shuffled: each design once a round, never twice running" ($r[0] -eq "0" -and $r[1] -eq "0") ("doubles in a round " + $r[0] + ", twice running " + $r[1])
$x = [StringTests]::Designs(2, 5, 20); $y = [StringTests]::Designs(2, 5, 20)
Check "shuffled: the same every time" ($x -eq $y) $x

$all = [int]([StringTests]::CountryCount(0, 0))
Check "all countries: every flag" ($all -eq 256) ("$all flags")
$eu = [int]([StringTests]::CountryCount(1, 3))
Check "Europe's flags" ($eu -ge 50 -and $eu -le 60) ("$eu flags")
$ro = [StringTests]::RegionsInOrder() -split ","
Check "by region: Africa, the Americas, Asia, Europe, Oceania, then the rest" ($ro[0] -eq "0") ("steps back " + $ro[0])
Check "France Europe, Japan Asia, Brazil the Americas, Nigeria Africa, Australia Oceania, Scotland Europe, the EU international" (($ro[1..7] -join ",") -eq "3,2,1,0,4,3,5") ($ro[1..7] -join ",")

$q = [StringTests]::CountrySeq(2, "jp,de,fr", 3, "", 0, 1, 6)
Check "my list as picked, round again" ($q -eq "jp de fr jp de fr") $q
$q = [StringTests]::CountrySeq(2, "jp,de,fr,xx", 0, "", 0, 1, 3)
Check "my list A to Z, a code this copy does not know left out" ($q -eq "fr de jp") $q
$q = [StringTests]::CountrySeq(2, "jp,de,fr", 3, "ca", 2, 1, 9)
Check "the lead first and every 2" ($q -eq "ca jp de ca fr jp ca de fr") $q
$q = [StringTests]::CountrySeq(2, "jp,de,fr", 3, "ca", 0, 1, 5)
Check "the lead first only" ($q -eq "ca jp de fr jp") $q
$q = [StringTests]::CountrySeq(2, "jp,de,fr,ca", 3, "ca", 0, 1, 5)
Check "the lead not flown twice from the list" ($q -eq "ca jp de fr jp") $q
$a1 = [StringTests]::CountrySeq(0, "", 2, "", 0, 5, 12); $a2 = [StringTests]::CountrySeq(0, "", 2, "", 0, 5, 12); $a3 = [StringTests]::CountrySeq(0, "", 2, "", 0, 6, 12)
Check "shuffled countries: one seed one order, another another" ($a1 -eq $a2 -and $a1 -ne $a3) $a1

# ---- the save ------------------------------------------------------------------------------
$t = [StringTests]::RoundTrip() -split ","
Check "a span's design round trip, byte for byte" ($t[0] -eq "1" -and $t[1] -eq "1") ("read " + $t[0] + ", same bytes " + $t[1] + ", " + $t[6] + " bytes")
$name = "F" + [char]0xEA + "te du quartier " + [char]0x56FD
Check "names (UTF-8), countries, pictures kept" ($t[2] -eq $name -and $t[3] -eq "fr" -and $t[4] -eq "My flag.png" -and $t[5] -eq "jp|de|fr") ($t[2..5] -join " / ")
$t = [StringTests]::All() -split ","
Check "the city's spans by segment" ($t[0] -eq "2:12:40000") $t[0]
Check "a newer format, a cut copy, a longer one, garbage: refused whole; none: none" (($t[1..5] -join ",") -eq "refused,refused,refused,refused,0") ($t[1..5] -join ",")
$t = [StringTests]::NewerFlags() -split ","
Check "flags of a newer format inside: refused, not misread" ($t[0] -eq [string][RealFlag.Flags.FlagCodec]::Version -and $t[1] -eq "refused") ("written with flags format " + $t[0] + ": " + $t[1])
$t = [StringTests]::Sanitise() -split ","
Check "a paste or a save pulled into range" (($t -join ",") -eq "0.3,3,0.002,0,8,fr|de,jp,8,0.08") ($t -join ",")

# ---- the cuts (the flags' own: FlagCut) -----------------------------------------------------
$t = [StringTests]::Cuts() -split ","
Check "the shared cloths take the string's cut: a pennant's cut, a rectangle's whole" ($t[0] -eq "3" -and $t[1] -eq "0") ("cuts " + $t[0] + ", " + $t[1])
Check "a deeper notch is another cloth; a pennant has no depth to tell apart" ($t[2] -eq "differ" -and $t[3] -eq "same") ($t[2] + ", " + $t[3])
$t = [StringTests]::DefaultCut() -split ","
Check "a new string's flags are pennants; an unknown cut a rectangle, its depth the default" ($t[0] -eq "3" -and $t[1] -eq "0" -and $t[2] -eq "0.333") ($t -join ", ")

# ---- the shared cloths stay put in a changing wind ----------------------------------------
$t = [StringTests]::Churn() -split ","
Check "ten minutes of gusty wind all the way round on 30 pennants: one cloth a slot, never another" ([int]$t[0] -eq 3) ("$($t[0]) cloth(s); the flags swung round $($t[1]) time(s), the wind turned $($t[2]) deg")
$t = [StringTests]::Side()
Check "the side: out to +Z / -Z, held until 15 deg past the string's line" ($t -eq "1,-1,1,-1,-1,1") $t
$t = [StringTests]::SwingRound() -split ","
Check "swung round in 2 s, smooth at the ends, flat halfway" ([int]$t[0] -ge 120 -and [int]$t[0] -le 121 -and $t[1] -eq "1" -and $t[2] -eq "0" -and $t[3] -eq "0.6875") ($t[0..3] -join ",")
Check "the wind's share: 30% along the string, all of it across, either side" (($t[4..7] -join ",") -eq "0.3,1,0.3,1") ($t[4..7] -join ",")
$t = [StringTests]::Held() -split ","
Check "a source nobody plays is held, then carries on (no fresh cloth); no self-collision" ($t[0] -eq "0" -and $t[1] -eq "held" -and [int]$t[2] -ge 1 -and $t[3] -eq "no-self") ($t -join ",")
$t = [StringTests]::Eased() -split ","
Check "a gust eases the cloth's wind, the same cloth" ([math]::Abs((N $t[0]) - 4) -lt 0.01 -and (N $t[1]) -gt 4 -and (N $t[1]) -lt 4.2 -and $t[2] -eq "ready") ("4 m/s, then a 12 m/s gust: " + $t[1] + " m/s after a step, " + $t[2])

# ---- shared motion's ring -------------------------------------------------------------------
$t = [StringTests]::Ring() -split ","
Check "the ring reads a step a second back" ($t[0] -eq "1" -and (N $t[1]) -eq 239) ("step " + $t[1])
Check "between two steps, interpolated" ($t[2] -eq "1" -and [math]::Abs((N $t[3]) - 238.5) -lt 1e-3 -and [math]::Abs((N $t[4]) - 477) -lt 1e-3) ("x " + $t[3] + ", z " + $t[4])
$kept = [int][math]::Round((N $t[7]) * 60)
Check "older than it keeps: its oldest" ($t[5] -eq "1" -and (N $t[6]) -eq (299 - $kept)) ("x " + $t[6] + ", span " + $t[7] + " s")

if ($failed -gt 0) { Write-Host "$failed FAILED"; exit 1 }
Write-Host "all passed"
