using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using RealFlag.Flags;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// Renders flags with the REAL cloth shader on this machine's GPU, inside the editor that builds
/// the bundles, through the same Graphics.DrawMesh + MaterialPropertyBlock path the mod uses, with
/// the cloth stepped by the mod's own FlagCloth and FlagWind (copied into Assets/Sim by
/// tools/sync-sim.ps1), and reads the pixels back. tools/preview/gpu-render.ps1 drives it:
///   Unity.exe -batchmode -quit -projectPath UnityProject -executeMethod FlagPreview.Render
/// (no -nographics: it needs a Direct3D 11 device).
///
/// The scene is the game's: a LEGACY DEFERRED ("DeferredLighting") camera with HDR in linear
/// colour space (the game's, from its log), a sun that casts shadows, sky light, a ground and a
/// pole in the legacy Diffuse shader (so they go through the same prepass the flag does), and,
/// at night, a point light standing in for a street lamp (the game draws its own lights into
/// the same light buffer).
///
/// Parameters come in as environment variables (executeMethod takes none):
///   RF_OUT       directory for the frames (rf-[row]-[col].png)
///   RF_W, RF_H   frame size
///   RF_WINDS     "2,6,12"   one row per wind (m/s at 10 m) ...
///   RF_PATTERNS  "5"        ... times each pattern value
///   RF_TIMES     "8"        one column per moment (seconds after the flag appears)
///   RF_SEQ       "12,0.05"  instead of RF_TIMES: 12 frames 0.05 s apart from the first time (motion)
///   RF_LENGTH, RF_PROPORTION, RF_WAVINESS, RF_GUSTS, RF_ELEVATION, RF_COLOURS "#002664,#FFFFFF,#FFFFFF"
///   RF_NIGHT "1", RF_PATH "deferred" (the game's) or "forward", RF_YAW / RF_PITCH (degrees),
///   RF_DIST (camera distance in flag lengths), RF_SUNYAW (degrees; 180 puts the sun behind the flag)
///   RF_LOOK "x,y,z"     metres added to the point looked at (the flag's middle): "-1.5,1,0" looks at the anchor
///   RF_DETAILS "18,32"  the cloth's details (cells along), one row each (times the winds and patterns)
///   RF_REFINES "1,4"    drawn cells a cloth cell (FlagMesh.Refine), one row each; RF_SMOOTH "0": edges not smoothed
///   RF_COLLIDE "0"      self-collision off (on, as in the game, by default)
///   RF_COUNTRIES "fr,us,np"  countries' flags instead of RF_PATTERNS (images from RealFlag\Resources\Flags)
///   RF_DARKNESS "0,20,40"    the cloth's darkness, percent (FlagSpec.Darkness): each design once per value
///   RF_WORN "0,0,0|60,40,30,50" each design once per wear,dirt,holes[,torn[,torn edges]] (percent: FlagSpec.Wear, Dirt, Holes, Tear, TearEdges); RF_WEARSEED 0..255
///   RF_SUN 1.3            the sun's intensity by day (Render It!'s "Sunny Default" has 2.7 in the game)
///   RF_WET 0..1              the game's ground wetness (_WeatherParams.w): the cloth wet in the rain
///   RF_POLE 1..3 (the pole's top: FlagPoleStyle), RF_ROPES 0..2, RF_THICK, RF_POLECOLOURS "#pole,#top,#rope"
///   RF_TILT, RF_LEAN    the hoist's tilt and lean, degrees (FlagOrientation)
///   RF_HOIST "top"      hoisted from its top (a banner on a crossbar: FlagHoist), not its side
///   RF_IMAGES "a.png,b.jpg"  pictures of the player's (from RF_IMAGEDIR) instead of RF_PATTERNS / RF_COUNTRIES,
///                       each once per RF_CROPS "x,y,zoom,turn|..." (FlagSpec.Image*; default "0.5,0.5,1,0"),
///                       laid on RF_PATTERNS' first pattern; RF_THROUGH "1": their see-through parts show it
///   RF_MOTIONS "waves,still,flat"  one row per motion (FlagMotion): a still flag's column t is its shape
///                       after t seconds of its breeze (StillPose), t &lt; 0 its own time; a flat one is laid flat
///   RF_BOXES "x,z,w,d,h|..."  buildings: boxes standing on the ground, their middle (x, z) from the pole's
///                       foot, w along x, d along z, h tall -- raised into the flag's SurfaceGrid as a
///                       building's LOD triangles are (FlagSurroundings), drawn grey
///   RF_POSTS "x,z,r,h|..."    props or trees: upright capsules r round and h tall (CapsuleSet), drawn as cylinders
///   RF_CITY "0"         the cloth does not keep off them (drawn all the same: the flag as before)
///   RF_BENCH n   time n steps of the cloth instead of drawing
/// Every frame logs "RF FRAME row=.. col=.. t=.. file=.. | ..." with the flag's droop, how much
/// of the frame it covers, and its mean colour.
/// </summary>
public static class FlagPreview
{
    private const string ShaderPath = "Assets/RealFlag/FlagCloth.shader";
    private const string PoleShaderPath = "Assets/RealFlag/FlagPole.shader";
    private const float Step = 1f / 60f;

    private sealed class Setup
    {
        public Camera Camera;
        public RenderTexture Target;
        public Texture2D Readback;
        public int Width, Height;
        public Material Material;
        public MaterialPropertyBlock Block;
        public Material PoleMaterial;
        public MaterialPropertyBlock PoleBlock;
        public Light Sun;
        public Light Lamp;
        public Color Sky;
        public float Exposure;
    }

    /// <summary>RF_LOOK: metres added to the point the camera looks at.</summary>
    private static Vector3 _look;

    // RF_WEARSEED: the scatter of holes, tatters and grime (FlagWear.SeedFor a prop in the game).
    private static float _wearSeed;

    /// <summary>The hoist's line and the fly at rest (FlagOrientation): the camera looks at the flag's middle along them.</summary>
    private static Vector3 _hoist = Vector3.down, _fly = Vector3.right;

    /// <summary>RF_WINDYAW: the way the wind blows, degrees from +x (the flag's heading) towards +z; 180 blows it back over its pole.</summary>
    private static float _windYaw;

    /// <summary>A drawn pole for the frame: its mesh and where it goes (none: the preview's plain grey stand-in).</summary>
    private sealed class PoleDraw
    {
        public Mesh Mesh;
        public Matrix4x4 Matrix;
    }

    /// <summary>RF_BOXES and RF_POSTS: the city round the flag (x, z, w, d, h and x, z, r, h, metres); RF_CITY: kept off.</summary>
    private static readonly List<float[]> Boxes = new List<float[]>(), Posts = new List<float[]>();
    private static bool _city = true;
    private static readonly List<GameObject> CityObjects = new List<GameObject>();

    public static void Render()
    {
        try
        {
            RenderAll();
            Debug.Log("RF PREVIEW OK");
        }
        catch (Exception e)
        {
            Debug.LogError("RF PREVIEW FAILED: " + e);
            EditorApplication.Exit(1);
        }
    }

    private static void RenderAll()
    {
        if (PlayerSettings.colorSpace != ColorSpace.Linear)
            PlayerSettings.colorSpace = ColorSpace.Linear;

        string outDir = Env("RF_OUT", "FlagPreview");
        Directory.CreateDirectory(outDir);
        int width = int.Parse(Env("RF_W", "320"), CultureInfo.InvariantCulture);
        int height = int.Parse(Env("RF_H", "240"), CultureInfo.InvariantCulture);
        float[] winds = ParseFloats(Env("RF_WINDS", "6"));
        int[] patterns = ParseInts(Env("RF_PATTERNS", FlagPattern.Default.ToString(CultureInfo.InvariantCulture)));
        float[] times = ParseFloats(Env("RF_TIMES", "8"));
        float[] seq = ParseFloats(Env("RF_SEQ", ""));
        bool night = Env("RF_NIGHT", "0") == "1";
        string path = Env("RF_PATH", "deferred");
        float yaw = ParseFloat(Env("RF_YAW", "0"));
        float pitch = ParseFloat(Env("RF_PITCH", "8"));
        float dist = ParseFloat(Env("RF_DIST", "2.6"));
        float[] lookOffset = ParseFloats(Env("RF_LOOK", "0,0,0"));
        if (lookOffset.Length == 3)
            _look = new Vector3(lookOffset[0], lookOffset[1], lookOffset[2]);
        _windYaw = ParseFloat(Env("RF_WINDYAW", "0"));
        float sunYaw = ParseFloat(Env("RF_SUNYAW", "35"));
        int bench = int.Parse(Env("RF_BENCH", "0"), CultureInfo.InvariantCulture);
        int[] details = ParseInts(Env("RF_DETAILS", FlagCloth.CellsAlong.ToString(CultureInfo.InvariantCulture)));
        int[] refines = ParseInts(Env("RF_REFINES", "1"));
        FlagMesh.SmoothEdges = Env("RF_SMOOTH", "1") != "0";
        bool collide = Env("RF_COLLIDE", "1") != "0";
        bool plate = Env("RF_PLATE", "0") == "1";
        string[] countries = Env("RF_COUNTRIES", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        float[] darknesses = ParseFloats(Env("RF_DARKNESS", "0"));

        // The countries' flags from the repository's folder: the files the mod embeds.
        string flagsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../RealFlag/Resources/Flags"));
        FlagImages.Source = (image, small) =>
        {
            string file = Path.Combine(small ? Path.Combine(flagsDir, "Small") : flagsDir, image + ".png");
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        };
        FlagImages.Warn = Debug.LogWarning;
        FlagImages.Note = Debug.Log;

        // The player's pictures from a folder of the preview's own.
        string imageDir = Env("RF_IMAGEDIR", "");
        FlagImages.UserSource = name =>
        {
            string file = Path.Combine(imageDir, name);
            return imageDir.Length > 0 && File.Exists(file) ? File.ReadAllBytes(file) : null;
        };
        string[] images = Env("RF_IMAGES", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        string[] crops = Env("RF_CROPS", "0.5,0.5,1,0").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        bool through = Env("RF_THROUGH", "0") == "1";

        FlagSpec spec = FlagSpec.Default;
        spec.Length = ParseFloat(Env("RF_LENGTH", "3"));
        spec.Proportion = int.Parse(Env("RF_PROPORTION", FlagProportion.Default.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
        spec.Waviness = ParseFloat(Env("RF_WAVINESS", "1"));
        float gusts = ParseFloat(Env("RF_GUSTS", "1"));
        float elevation = ParseFloat(Env("RF_ELEVATION", "10"));
        string[] colours = Env("RF_COLOURS", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        uint c;
        if (colours.Length > 0 && FlagColours.TryParseHex(colours[0], out c)) spec.Colour1 = c;
        if (colours.Length > 1 && FlagColours.TryParseHex(colours[1], out c)) spec.Colour2 = c;
        if (colours.Length > 2 && FlagColours.TryParseHex(colours[2], out c)) spec.Colour3 = c;

        // The pole and the hoist's lean.
        spec.Pole = int.Parse(Env("RF_POLE", "0"), CultureInfo.InvariantCulture);
        spec.Ropes = int.Parse(Env("RF_ROPES", "2"), CultureInfo.InvariantCulture);
        spec.PoleThickness = ParseFloat(Env("RF_THICK", "1"));
        spec.Tilt = ParseFloat(Env("RF_TILT", "0"));
        spec.Lean = ParseFloat(Env("RF_LEAN", "0"));
        spec.HoistedFrom = string.Equals(Env("RF_HOIST", "side"), "top", StringComparison.OrdinalIgnoreCase) ? FlagHoist.Top : FlagHoist.Side;
        string[] poleColours = Env("RF_POLECOLOURS", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (poleColours.Length > 0 && FlagColours.TryParseHex(poleColours[0], out c)) spec.PoleColour = c;
        if (poleColours.Length > 1 && FlagColours.TryParseHex(poleColours[1], out c)) spec.TopColour = c;
        if (poleColours.Length > 2 && FlagColours.TryParseHex(poleColours[2], out c)) spec.RopeColour = c;
        spec = spec.Sanitised();

        if (bench > 0)
        {
            foreach (int detail in details)
                Bench(spec, winds, gusts, elevation, bench, detail, collide, refines[0]);
            return;
        }

        Boxes.Clear();
        Posts.Clear();
        foreach (string b in Env("RF_BOXES", "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            float[] v = ParseFloats(b);
            if (v.Length >= 5)
                Boxes.Add(v);
        }

        foreach (string p in Env("RF_POSTS", "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            float[] v = ParseFloats(p);
            if (v.Length >= 4)
                Posts.Add(v);
        }

        _city = Env("RF_CITY", "1") != "0";

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
            throw new Exception("shader not found at " + ShaderPath);

        // A surface shader that failed to compile is a silent one-pass stand-in (see ShaderCheck).
        int passes = new Material(shader).passCount;
        if (passes < 5)
            throw new Exception("the shader has " + passes + " pass(es), not the surface shader's 5: it did not compile (run ShaderCheck)");

        Debug.Log("RF PREVIEW: shader '" + shader.name + "' supported=" + shader.isSupported + " passes=" +
                  passes + " | device " + SystemInfo.graphicsDeviceType + " '" +
                  SystemInfo.graphicsDeviceName + "' | colour space " + QualitySettings.activeColorSpace +
                  " | path=" + path + " night=" + night);

        Setup s = BuildScene(width, height, path, night, sunYaw, shader);

        // The frames: one row per (wind, pattern), one column per moment.
        float[] moments = times;
        if (seq.Length >= 2)
        {
            int n = Mathf.Max(1, (int)seq[0]);
            float gap = seq[1];
            float first = times.Length > 0 ? times[0] : 8f;
            moments = new float[n];
            for (int i = 0; i < n; i++)
                moments[i] = first + i * gap;
        }

        Array.Sort(moments);
        int row = 0;

        // RF_FABRIC "A=0.5;B=0.1|A=0.8": fabric overrides per row, cycling (to compare by eye).
        string[] fabrics = Env("RF_FABRIC", "").Split('|');

        // The designs: the countries' flags if asked for, the patterns otherwise; each once per
        // darkness (RF_DARKNESS, percent as the panel shows it).
        var designs = new List<FlagSpec>();
        if (images.Length > 0)
        {
            foreach (string image in images)
            {
                foreach (string crop in crops)
                {
                    float[] c4 = ParseFloats(crop);
                    FlagSpec flag = spec;
                    flag.Pattern = patterns[0];
                    flag.Image = image.Trim();
                    flag.ImageX = c4.Length > 0 ? c4[0] : 0.5f;
                    flag.ImageY = c4.Length > 1 ? c4[1] : 0.5f;
                    flag.ImageZoom = c4.Length > 2 ? c4[2] : 1f;
                    flag.ImageTurn = c4.Length > 3 ? (int)c4[3] : 0;
                    flag.ImageAlpha = through ? FlagImageAlpha.Through : FlagImageAlpha.Cut;
                    foreach (float darkness in darknesses)
                    {
                        flag.Darkness = darkness / 100f;
                        designs.Add(flag.Sanitised());
                    }
                }
            }
        }
        else if (countries.Length > 0)
        {
            foreach (string code in countries)
            {
                FlagSpec flag = spec;
                flag.Country = code.Trim();
                foreach (float darkness in darknesses)
                {
                    flag.Darkness = darkness / 100f;
                    designs.Add(flag.Sanitised());
                }
            }
        }
        else
        {
            foreach (int pattern in patterns)
            {
                FlagSpec flag = spec;
                flag.Pattern = pattern;
                foreach (float darkness in darknesses)
                {
                    flag.Darkness = darkness / 100f;
                    designs.Add(flag);
                }
            }
        }

        // RF_WORN "wear,dirt,holes|...": each design once per entry (percent, as the panel's).
        string[] worns = Env("RF_WORN", "0,0,0").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        if (worns.Length > 1 || (worns.Length == 1 && worns[0].Trim() != "0,0,0"))
        {
            var weathered = new List<FlagSpec>();
            foreach (FlagSpec d in designs)
            {
                foreach (string worn in worns)
                {
                    float[] w3 = ParseFloats(worn);
                    FlagSpec flag = d;
                    flag.Wear = w3.Length > 0 ? w3[0] / 100f : 0f;
                    flag.Dirt = w3.Length > 1 ? w3[1] / 100f : 0f;
                    flag.Holes = w3.Length > 2 ? w3[2] / 100f : 0f;
                    flag.Tear = w3.Length > 3 ? w3[3] / 100f : 0f;
                    flag.TearEdges = w3.Length > 4 ? w3[4] / 100f : 0f;
                    weathered.Add(flag.Sanitised());
                }
            }

            designs = weathered;
        }

        // RF_CUTS "1,3,4:40|...": each design once per cut (FlagCut), a depth in percent after a colon.
        string[] cuts = Env("RF_CUTS", "0").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (cuts.Length > 1 || (cuts.Length == 1 && cuts[0].Trim() != "0"))
        {
            var cutDesigns = new List<FlagSpec>();
            foreach (FlagSpec d in designs)
            {
                foreach (string cutText in cuts)
                {
                    string[] parts = cutText.Trim().Split(':');
                    FlagSpec flag = d;
                    flag.Cut = int.Parse(parts[0], CultureInfo.InvariantCulture);
                    flag.CutDepth = parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) / 100f : FlagCut.DefaultDepth;
                    cutDesigns.Add(flag.Sanitised());
                }
            }

            designs = cutDesigns;
        }

        _wearSeed = float.Parse(Env("RF_WEARSEED", "0"), CultureInfo.InvariantCulture);

        // The hoist's line and the fly at rest, for a flag heading along +x.
        float hx, hy, hz, fx, fy, fz;
        FlagOrientation.Frame(0f, spec.Tilt, spec.Lean, spec.HoistedFrom, out hx, out hy, out hz, out fx, out fy, out fz);
        var hoist = new Vector3(hx, hy, hz);
        var fly = new Vector3(fx, fy, fz);
        _hoist = hoist;
        _fly = fly;

        var motions = new List<int>();
        foreach (string m in Env("RF_MOTIONS", "waves").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string name = m.Trim().ToLowerInvariant();
            motions.Add(name == "still" ? FlagMotion.Still : name == "flat" ? FlagMotion.Flat : FlagMotion.Waves);
        }

        foreach (int detail in details)
        foreach (int refine in refines)
        foreach (int motion in motions)
        foreach (float wind in motion == FlagMotion.Waves ? winds : new[] { StillPose.BreezeFor(spec.Length) })
        {
            foreach (FlagSpec design in designs)
            {
                FlagSpec flag = design;
                flag.Motion = motion;
                int pattern = flag.Pattern;

                // The flag's foot at rest RF_ELEVATION up: its hoist's (from its side), its fly's (a banner's).
                float drop = Mathf.Max(-(hy * flag.Height), 0f) + Mathf.Max(-(fy * flag.Length), 0f);
                Vector3 anchor = new Vector3(0f, elevation + Mathf.Max(drop, flag.Height), 0f);

                // The wind blows along +x (the flag's own direction, the panel's default mode).
                FlagCloth cloth = new FlagCloth(flag.Length, flag.Height, Fabric(flag.Waviness, fabrics[row % fabrics.Length]), 1f, 0f, detail,
                                                hx, hy, hz, flag.Cut, flag.CutDepth);
                cloth.SelfCollision = collide;
                cloth.GroundY = -anchor.y;

                // RF_PLATE: a banner from its top in front of its back plate, as the game sets it (FlagRenderer.ApplyPlate).
                flag.BackPlate = plate;
                if (motion == FlagMotion.Waves && flag.HasBackPlate)
                {
                    cloth.SetPlate(hz, -hx);
                    if (cloth.BehindPlate > 0)
                        cloth.LayOutClear(1f, 0f);
                    cloth.Calm(FlagCloth.CalmTime);
                    Debug.Log("RF PREVIEW: a back plate through the hoist, its front towards (" +
                              hz.ToString("F2", CultureInfo.InvariantCulture) + ", " + (-hx).ToString("F2", CultureInfo.InvariantCulture) + ")");
                }

                FlagMesh mesh = new FlagMesh(cloth, refine);

                // The pole: from the ground (y = 0) up its axis to the anchor's level, as the game casts it.
                PoleDraw pole = null;
                if (flag.HasPole)
                {
                    float length = hy < -0.05f ? anchor.y / -hy : FlagPoleShape.Fallback(flag);
                    FlagPoleShape shape = FlagPoleShape.For(flag, length);

                    // A still flag's shape leaves the pole out, as the game makes it (StillPose).
                    if (motion == FlagMotion.Waves)
                        shape.ApplyTo(cloth, fx, fy, fz);
                    pole = new PoleDraw { Mesh = FlagPoleMesh.Build(shape, null), Matrix = FlagPoleMesh.Placement(anchor, shape, hoist, fly) };
                    Debug.Log("RF PREVIEW: pole " + FlagPoleStyle.LogName(flag.Pole) + ", " + flag.Ropes + " rope(s): foot " +
                              (200f * shape.RadiusFoot).ToString("F1", CultureInfo.InvariantCulture) + " cm, top " +
                              (200f * shape.RadiusTop).ToString("F1", CultureInfo.InvariantCulture) + " cm across, " +
                              length.ToString("F1", CultureInfo.InvariantCulture) + " m to the anchor, hoist " +
                              (100f * shape.Offset).ToString("F1", CultureInfo.InvariantCulture) + " cm off the axis, " +
                              pole.Mesh.vertexCount + " vertices");
                }

                // The city round it (RF_BOXES, RF_POSTS), as the game gathers it; a new cloth laid out clear of it.
                string city = "";
                if (motion == FlagMotion.Waves && (Boxes.Count > 0 || Posts.Count > 0))
                    city = KeepOffCity(cloth, flag, anchor, hoist);

                float t = 0f;
                int col = 0;
                int posed = 0;
                if (motion != FlagMotion.Waves)
                {
                    cloth.GroundY = -1e6f;
                    StillPose.Lay(cloth, motion, flag.OnHalyard, fx, fy, fz);
                }

                foreach (float moment in moments)
                {
                    if (motion == FlagMotion.Still && moment < 0f)
                    {
                        // The shape the game keeps: made whole, the smoothest of its last moments (shown at the step it was).
                        StillPoseRun run = StillPose.Make(cloth, motion, flag.OnHalyard, fx, fy, fz, flag.Waviness, flag.Length);
                        posed = run.BestStep;
                    }
                    else if (motion == FlagMotion.Still)
                    {
                        // Its shape after this long in its breeze, as the game runs it.
                        int target = Mathf.RoundToInt(moment / StillPose.StepSize);
                        if (target > posed)
                        {
                            ClothJob pose = StillPose.Job(cloth, fx, fy, fz, flag.Waviness, flag.Length, posed, target - posed);
                            ClothWorker.Execute(ref pose);
                            posed = target;
                        }
                    }

                    while (motion == FlagMotion.Waves && t + Step <= moment + 1e-4f)
                    {
                        StepCloth(cloth, wind, gusts, anchor.y, flag, t, anchor);
                        t += Step;
                    }

                    mesh.Upload(1f);
                    string info;
                    Color[] pixels = RenderFlag(s, mesh, pole, flag, anchor, yaw, pitch, dist, out info);
                    string file = Path.Combine(outDir, "rf-" + row + "-" + col + ".png");
                    SavePng(pixels, width, height, s.Exposure, file);
                    float shown = motion == FlagMotion.Still ? posed * StillPose.StepSize : motion == FlagMotion.Flat ? 0f : moment;
                    Debug.Log("RF FRAME row=" + row + " col=" + col + " t=" + shown.ToString("0.###", CultureInfo.InvariantCulture) +
                              " wind=" + wind.ToString("0.#", CultureInfo.InvariantCulture) + " pattern=" + pattern +
                              " file=" + file + " | " +
                              (motion != FlagMotion.Waves
                                   ? FlagMotion.LogName(motion) + " rough " + StillPose.Roughness(cloth).ToString("0.000", CultureInfo.InvariantCulture) +
                                     " shown " + StillPose.Shown(cloth, fx, fy, fz).ToString("0.00", CultureInfo.InvariantCulture) + ", "
                                   : "") + Droop(cloth) + ", " +
                              cloth.Columns + "x" + cloth.Rows +
                              (mesh.Refine > 1 ? " x" + mesh.Refine : "") + (collide ? "" : " no collision") +
                              (flag.IsCountry ? ", " + flag.Country : "") +
                              (flag.HasImage ? ", image " + flag.Image + " at " + flag.ImageX.ToString("0.##", CultureInfo.InvariantCulture) + "/" +
                                               flag.ImageY.ToString("0.##", CultureInfo.InvariantCulture) + " x" +
                                               flag.ImageZoom.ToString("0.##", CultureInfo.InvariantCulture) + " turn " + flag.ImageTurn +
                                               (flag.ImageAlpha == FlagImageAlpha.Through ? " through" : "") : "") +
                              (flag.Darkness > 0f ? ", darkness " + (flag.Darkness * 100f).ToString("0", CultureInfo.InvariantCulture) + "%" : "") +
                              (flag.Cut != FlagCut.Rectangle ? ", cut " + FlagCut.LogName(flag.Cut) + " " +
                                                               (flag.CutDepth * 100f).ToString("0", CultureInfo.InvariantCulture) + "% (" +
                                                               cloth.ClothCount + " of " + cloth.Count + " particles)" : "") +
                              (flag.IsWeathered ? ", wear/dirt/holes " + (flag.Wear * 100f).ToString("0", CultureInfo.InvariantCulture) + "/" +
                                                  (flag.Dirt * 100f).ToString("0", CultureInfo.InvariantCulture) + "/" +
                                                  (flag.Holes * 100f).ToString("0", CultureInfo.InvariantCulture) + "% torn " +
                                                  (flag.Tear * 100f).ToString("0", CultureInfo.InvariantCulture) + "% edges " +
                                                  (flag.TearEdges * 100f).ToString("0", CultureInfo.InvariantCulture) + "%" : "") +
                              (city.Length > 0 ? " | " + city + ", " + CityProbe(cloth, anchor) : "") +
                              " | " + info);
                    col++;
                }

                mesh.Destroy();
                if (pole != null)
                    UnityEngine.Object.DestroyImmediate(pole.Mesh);
                row++;
            }
        }

        s.Camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(s.Camera.gameObject);
        UnityEngine.Object.DestroyImmediate(s.Target);
    }

    /// <summary>
    /// The boxes and posts round the flag into its cloth, as FlagSurroundings gathers a city: a
    /// SurfaceGrid a quarter wider than the flag can reach (FlagOutline), flat ground at y = 0,
    /// raised to each box's twelve triangles; a CapsuleSet of the posts. Then laid out clear of
    /// them, as FlagRenderer lays out a new cloth. For the log.
    /// </summary>
    private static string KeepOffCity(FlagCloth cloth, FlagSpec flag, Vector3 anchor, Vector3 hoist)
    {
        Vector3 end = anchor + hoist * flag.Height;
        float cx = 0.5f * (anchor.x + end.x), cz = 0.5f * (anchor.z + end.z);
        float reach = 0.5f * new Vector2(end.x - anchor.x, end.z - anchor.z).magnitude + 1.02f * flag.Length + 0.5f;
        float spacing;
        int nodes;
        SurfaceGrid.Layout(1.25f * reach, out spacing, out nodes);
        float span = (nodes - 1) * spacing;
        var grid = new SurfaceGrid(cx - 0.5f * span - anchor.x, cz - 0.5f * span - anchor.z, spacing, nodes, nodes, -anchor.y);
        foreach (float[] b in Boxes)
        {
            float x0 = b[0] - 0.5f * b[2] - anchor.x, x1 = b[0] + 0.5f * b[2] - anchor.x;
            float z0 = b[1] - 0.5f * b[3] - anchor.z, z1 = b[1] + 0.5f * b[3] - anchor.z;
            float y0 = -anchor.y, y1 = b[4] - anchor.y;
            float[] v = {
                x0, y1, z0, x1, y1, z0, x1, y1, z1,  x0, y1, z0, x1, y1, z1, x0, y1, z1,
                x0, y0, z0, x1, y0, z1, x1, y0, z0,  x0, y0, z0, x0, y0, z1, x1, y0, z1,
                x0, y0, z0, x0, y1, z0, x0, y1, z1,  x0, y0, z0, x0, y1, z1, x0, y0, z1,
                x1, y0, z0, x1, y1, z1, x1, y1, z0,  x1, y0, z0, x1, y0, z1, x1, y1, z1,
                x0, y0, z0, x1, y1, z0, x0, y1, z0,  x0, y0, z0, x1, y0, z0, x1, y1, z0,
                x0, y0, z1, x0, y1, z1, x1, y1, z1,  x0, y0, z1, x1, y1, z1, x1, y0, z1 };
            for (int i = 0; i < v.Length; i += 9)
                grid.Raise(v[i], v[i + 1], v[i + 2], v[i + 3], v[i + 4], v[i + 5], v[i + 6], v[i + 7], v[i + 8]);
        }

        grid.Finish();
        var set = new CapsuleSet();
        foreach (float[] p in Posts)
            set.AddObject(p[0] - anchor.x, -anchor.y, p[1] - anchor.z, 4f * p[2], 4f * p[2], p[3]);

        if (!_city)
            return "city off: " + Boxes.Count + " box(es), " + Posts.Count + " post(s)";

        cloth.SetSurface(grid, 0f, 0f, 0f);
        cloth.SetCapsules(set, 0f, 0f, 0f);
        int buried = cloth.LayOutClear(Mathf.Cos(_windYaw * Mathf.Deg2Rad), Mathf.Sin(_windYaw * Mathf.Deg2Rad));
        return "city: " + Boxes.Count + " box(es), " + Posts.Count + " post(s), surface " + nodes + "x" + nodes + " every " +
               spacing.ToString("F2", CultureInfo.InvariantCulture) + " m, laid out " + (buried == 0 ? "clear" : buried + " in it") +
               (cloth.Y[cloth.Count - 1] < -0.5f * cloth.Length ? " hanging" : "") +
               (cloth.SurfaceBlocked ? ", BLOCKED" : "");
    }

    /// <summary>For the log: the deepest any particle is inside a box, and the nearest to a post's surface (negative: inside), metres.</summary>
    private static string CityProbe(FlagCloth cloth, Vector3 anchor)
    {
        float deepest = 0f, nearest = float.MaxValue;
        for (int k = 0; k < cloth.Count; k++)
        {
            Vector3 p = anchor + new Vector3(cloth.X[k], cloth.Y[k], cloth.Z[k]);
            foreach (float[] b in Boxes)
            {
                float inside = Mathf.Min(Mathf.Min(p.x - (b[0] - 0.5f * b[2]), b[0] + 0.5f * b[2] - p.x),
                                         Mathf.Min(Mathf.Min(p.z - (b[1] - 0.5f * b[3]), b[1] + 0.5f * b[3] - p.z), Mathf.Min(p.y, b[4] - p.y)));
                deepest = Mathf.Max(deepest, inside);
            }

            foreach (float[] q in Posts)
            {
                if (p.y > q[3])
                    continue;
                nearest = Mathf.Min(nearest, new Vector2(p.x - q[0], p.z - q[1]).magnitude - q[2]);
            }
        }

        var inv = CultureInfo.InvariantCulture;
        return "deepest in a box " + deepest.ToString("F3", inv) + " m" + (Posts.Count > 0 ? ", nearest a post " + nearest.ToString("F3", inv) + " m" : "") +
               (cloth.Relaid > 0 ? ", laid out afresh " + cloth.Relaid : "") +
               (cloth.WallShift > 0f ? ", wall kept back " + cloth.WallShift.ToString("F2", inv) + " m" + (cloth.HoistOnEdge ? " (a roof's edge)" : "") : "") +
               (cloth.SurfaceBlocked ? ", BLOCKED" : "");
    }

    /// <summary>
    /// The standard fabric at this waviness, with overrides "A=0.8;R=1.2;F=0.08;B=0.1;S=0.7;C=0.15;D=0.115;I=6;K=0.08"
    /// (added mass, resistive drag, friction, bend, shear, compression, density, passes, damping).
    /// </summary>
    private static FlagFabric Fabric(float waviness, string overrides)
    {
        FlagFabric f = FlagFabric.Standard(waviness);
        if (string.IsNullOrEmpty(overrides))
            return f;

        foreach (string kv in overrides.Split(';'))
        {
            string[] p = kv.Split('=');
            if (p.Length != 2)
                continue;

            float v = ParseFloat(p[1].Trim());
            switch (p[0].Trim())
            {
                case "A": f.AddedMass = v; break;
                case "R": f.Drag = v; break;
                case "F": f.Friction = v; break;
                case "B": f.Bend = v; break;
                case "S": f.Shear = v; break;
                case "C": f.Compression = v; break;
                case "D": f.Density = v; break;
                case "I": f.Iterations = (int)v; break;
                case "K": f.Damping = v; break;
            }
        }

        return f;
    }

    /// <summary>
    /// One 1/60 s step as the mod takes it (ClothWorker.Execute: more, shorter steps in a
    /// strong wind), here on the calling thread.
    /// </summary>
    private static void StepCloth(FlagCloth cloth, float wind, float gusts, float heightOverGround, FlagSpec flag, float t, Vector3 anchor)
    {
        ClothJob job = Job(cloth, null, wind, gusts, heightOverGround, flag, t, anchor);
        ClothWorker.Execute(ref job);
    }

    /// <summary>A job of one 1/60 s step with the wind along +x, as FlagRenderer.PlanFlag fills it.</summary>
    private static ClothJob Job(FlagCloth cloth, FlagMesh mesh, float wind, float gusts, float heightOverGround, FlagSpec flag, float t, Vector3 anchor)
    {
        return new ClothJob
        {
            Cloth = cloth,
            Mesh = mesh,
            Steps = 1,
            Size = Step,
            Time = t,
            Alpha = 0.5f,
            Speed = wind,
            Gusts = gusts,
            DirX = Mathf.Cos(_windYaw * Mathf.Deg2Rad),
            DirZ = Mathf.Sin(_windYaw * Mathf.Deg2Rad),
            Height = heightOverGround,
            Waviness = flag.Waviness,
            Length = flag.Length,
            AnchorX = anchor.x,
            AnchorZ = anchor.z,
        };
    }

    private static Setup BuildScene(int width, int height, string path, bool night, float sunYaw, Shader shader)
    {
        var s = new Setup { Width = width, Height = height, Exposure = night ? 4f : 0.6f };

        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 120f;
        QualitySettings.shadowCascades = 2;
        QualitySettings.pixelLightCount = 4;

        GameObject camObject = new GameObject("RFPreviewCamera");
        s.Camera = camObject.AddComponent<Camera>();
        s.Camera.allowHDR = true;
        s.Camera.renderingPath = path == "forward" ? RenderingPath.Forward : RenderingPath.DeferredLighting;
        s.Camera.fieldOfView = 40f;
        s.Camera.nearClipPlane = 0.3f;
        s.Camera.farClipPlane = 2000f;
        s.Camera.clearFlags = CameraClearFlags.SolidColor;
        s.Sky = night ? new Color(0.004f, 0.006f, 0.014f, 1f) : new Color(0.55f, 0.72f, 1.0f, 1f);
        s.Camera.backgroundColor = s.Sky;
        s.Target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        s.Target.antiAliasing = 1;
        s.Target.Create();
        s.Camera.targetTexture = s.Target;
        s.Readback = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);

        // Ground and pole in the legacy Diffuse shader: it has prepass passes, as the game's do,
        // so they are in the depth buffer and take the sun's shadow (the flag's among others).
        Shader diffuse = Shader.Find("Legacy Shaders/Diffuse");
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "RFPreviewGround";
        ground.transform.localScale = new Vector3(60f, 1f, 60f);
        ground.GetComponent<Renderer>().sharedMaterial = new Material(diffuse) { color = new Color(0.36f, 0.42f, 0.30f) };

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "RFPreviewPole";
        pole.GetComponent<Renderer>().sharedMaterial = new Material(diffuse) { color = new Color(0.8f, 0.8f, 0.82f) };
        pole.transform.localScale = new Vector3(0.12f, 8f, 0.12f);
        pole.transform.position = new Vector3(-0.07f, 8f, 0f);

        // The city round the flag (RF_BOXES, RF_POSTS): stone-grey boxes, dark posts.
        CityObjects.Clear();
        var stone = new Material(diffuse) { color = new Color(0.62f, 0.6f, 0.56f) };
        var bark = new Material(diffuse) { color = new Color(0.25f, 0.22f, 0.18f) };
        foreach (float[] b in Boxes)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "RFPreviewCity";
            box.GetComponent<Renderer>().sharedMaterial = stone;
            box.transform.localScale = new Vector3(b[2], b[4], b[3]);
            box.transform.position = new Vector3(b[0], 0.5f * b[4], b[1]);
            CityObjects.Add(box);
        }

        foreach (float[] p in Posts)
        {
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "RFPreviewCity";
            post.GetComponent<Renderer>().sharedMaterial = bark;
            post.transform.localScale = new Vector3(2f * p[2], 0.5f * p[3], 2f * p[2]);
            post.transform.position = new Vector3(p[0], 0.5f * p[3], p[1]);
            CityObjects.Add(post);
        }

        GameObject sunObject = new GameObject("RFPreviewSun");
        s.Sun = sunObject.AddComponent<Light>();
        s.Sun.type = LightType.Directional;
        s.Sun.shadows = LightShadows.Soft;
        s.Sun.shadowStrength = 0.85f;
        s.Sun.color = night ? new Color(0.55f, 0.65f, 1f) : new Color(1f, 0.95f, 0.86f);
        s.Sun.intensity = night ? 0.05f : ParseFloat(Env("RF_SUN", "1.3"));
        Vector3 toSun = Quaternion.Euler(0f, sunYaw, 0f) * new Vector3(0f, 0.55f, -0.83f);
        sunObject.transform.rotation = Quaternion.LookRotation(-toSun.normalized);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = night ? new Color(0.01f, 0.012f, 0.025f) : new Color(0.45f, 0.55f, 0.72f);
        RenderSettings.ambientEquatorColor = night ? new Color(0.008f, 0.009f, 0.015f) : new Color(0.38f, 0.42f, 0.45f);
        RenderSettings.ambientGroundColor = night ? new Color(0.004f, 0.004f, 0.005f) : new Color(0.2f, 0.2f, 0.17f);

        if (night)
        {
            GameObject lampObject = new GameObject("RFPreviewLamp");
            s.Lamp = lampObject.AddComponent<Light>();
            s.Lamp.type = LightType.Point;
            s.Lamp.color = new Color(1f, 0.78f, 0.5f);
            s.Lamp.intensity = 3f;
            s.Lamp.range = 25f;
            lampObject.transform.position = new Vector3(2f, 6f, -4f);
        }

        // What FlagRenderer sets from the game's sun every frame.
        Color sunLight = s.Sun.color.linear * s.Sun.intensity;
        Shader.SetGlobalVector(FlagLook.IdSunDir, new Vector4(toSun.normalized.x, toSun.normalized.y, toSun.normalized.z, 0f));
        Shader.SetGlobalVector(FlagLook.IdSunColor, new Vector4(sunLight.r, sunLight.g, sunLight.b, 0f));

        // What the game's WeatherManager.EndRenderingImpl sets every frame: _WeatherParams =
        // (temperature, rain, fog, the ground's wetness); the cloth reads the wetness (RF_WET).
        float wet = Mathf.Clamp01(ParseFloat(Env("RF_WET", "0")));
        Shader.SetGlobalVector("_WeatherParams", new Vector4(15f, wet, 0f, wet));

        // And from the camera: half a pixel's angle, the thinnest a tube is drawn.
        float pixelsPerRadian = height / (2f * Mathf.Tan(0.5f * s.Camera.fieldOfView * Mathf.Deg2Rad));
        Shader.SetGlobalFloat(FlagPoleMesh.IdPixelRadius, 0.6f / pixelsPerRadian);

        s.Material = new Material(shader);
        s.Block = new MaterialPropertyBlock();

        Shader poleShader = AssetDatabase.LoadAssetAtPath<Shader>(PoleShaderPath);
        if (poleShader == null)
            throw new Exception("pole shader not found at " + PoleShaderPath);
        int polePasses = new Material(poleShader).passCount;
        if (polePasses < 5)
            throw new Exception("the pole shader has " + polePasses + " pass(es), not the surface shader's 5: it did not compile (run ShaderCheck)");
        s.PoleMaterial = new Material(poleShader);
        s.PoleBlock = new MaterialPropertyBlock();
        return s;
    }

    private static Color[] RenderFlag(Setup s, FlagMesh mesh, PoleDraw pole, FlagSpec flag, Vector3 anchor, float yawDeg, float pitchDeg, float dist,
                                      out string info)
    {
        // Looking at the flag's middle at rest (or RF_LOOK off it) from its side, slightly down on it.
        Bounds b = mesh.Bounds;
        Vector3 centre = anchor + _hoist * (0.5f * flag.Height) + _fly * (0.5f * flag.Length) + _look;
        float range = dist * Mathf.Max(flag.Length, flag.Height);
        Quaternion look = Quaternion.Euler(pitchDeg, yawDeg, 0f);
        Vector3 fwd = look * Vector3.forward;
        s.Camera.transform.position = centre - fwd * range;
        s.Camera.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

        FlagLook.Apply(s.Block, flag, Vector4.zero, _wearSeed);
        mesh.Bind(s.Block);

        // The flag alone first, on black (with no ground or pole in view), to measure it.
        s.Camera.backgroundColor = Color.black;
        SetScene(false);
        Graphics.DrawMesh(mesh.Mesh, Matrix4x4.TRS(anchor, Quaternion.identity, Vector3.one), s.Material, 0, s.Camera, 0, s.Block, true, true);
        s.Camera.Render();
        Color[] alone = ReadPixels(s);
        int covered = 0;
        Color sum = Color.black;
        foreach (Color p in alone)
        {
            if (p.r + p.g + p.b > 1e-4f)
            {
                covered++;
                sum += p;
            }
        }

        info = "covers " + (100.0 * covered / alone.Length).ToString("F1", CultureInfo.InvariantCulture) + "% mean " +
               (covered > 0 ? Fmt(sum / covered) : "-") + " box " + Fmt3(b.size);

        s.Camera.backgroundColor = s.Sky;
        SetScene(true, pole == null);
        Graphics.DrawMesh(mesh.Mesh, Matrix4x4.TRS(anchor, Quaternion.identity, Vector3.one), s.Material, 0, s.Camera, 0, s.Block, true, true);
        if (pole != null)
        {
            FlagPoleMesh.Look(s.PoleBlock, flag, Vector4.zero);
            Graphics.DrawMesh(pole.Mesh, pole.Matrix, s.PoleMaterial, 0, s.Camera, 0, s.PoleBlock, true, true);
        }

        s.Camera.Render();
        return ReadPixels(s);
    }

    /// <summary>The ground, and the plain grey pole the preview stands a flag by when the mod draws none.</summary>
    private static void SetScene(bool on, bool greyPole = true)
    {
        foreach (string name in new[] { "RFPreviewGround", "RFPreviewPole" })
        {
            GameObject go = GameObject.Find(name);
            if (go != null)
                go.GetComponent<Renderer>().enabled = on && (name != "RFPreviewPole" || greyPole);
        }

        foreach (GameObject go in CityObjects)
        {
            if (go != null)
                go.GetComponent<Renderer>().enabled = on;
        }
    }

    /// <summary>The fly's mean height against the hoist's middle, as an angle below the horizontal, and how far out it reaches.</summary>
    private static string Droop(FlagCloth c)
    {
        float fx = 0f, fy = 0f, fz = 0f;
        for (int j = 0; j < c.Rows; j++)
        {
            int k = j * c.Columns + c.Columns - 1;
            fx += c.X[k]; fy += c.Y[k]; fz += c.Z[k];
        }

        fx /= c.Rows; fy /= c.Rows; fz /= c.Rows;
        float horizontal = Mathf.Sqrt(fx * fx + fz * fz);
        float droop = Mathf.Atan2(-0.5f * c.Height - fy, horizontal) * Mathf.Rad2Deg;
        return "droop " + droop.ToString("F0", CultureInfo.InvariantCulture) + " deg, reach " +
               (horizontal / c.Length).ToString("F2", CultureInfo.InvariantCulture) + " L, stretch " +
               c.MaxStretch().ToString("F3", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The cloth's own cost, per step, at each wind: RF_BENCH steps each, on this thread; then
    /// the same steps on a ClothWorker thread as the mod runs them -- a batch a "frame", the
    /// mesh refreshed there, handed to the mesh here -- which must end bit for bit where this
    /// thread's did, and what it leaves the main thread (the hand-over).
    /// </summary>
    private static void Bench(FlagSpec spec, float[] winds, float gusts, float elevation, int steps, int detail, bool collide, int refine)
    {
        var worker = new ClothWorker();
        string problem;
        if (!worker.Start(out problem))
            throw new Exception("the cloth's thread did not start: " + problem);

        foreach (float wind in winds)
        {
            Vector3 anchor = new Vector3(0f, elevation + spec.Height, 0f);
            FlagCloth cloth = new FlagCloth(spec.Length, spec.Height, FlagFabric.Standard(spec.Waviness), 1f, 0f, detail);
            cloth.SelfCollision = collide;
            cloth.GroundY = -anchor.y;

            // Settled first (8 s): hanging in its folds in calm air, flying in a wind, as a flag
            // in a city is.
            float t = 0f;
            for (int i = 0; i < 480; i++)
            {
                StepCloth(cloth, wind, gusts, anchor.y, spec, t, anchor);
                t += Step;
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < steps; i++)
            {
                StepCloth(cloth, wind, gusts, anchor.y, spec, t, anchor);
                t += Step;
            }

            sw.Stop();
            double inline = sw.Elapsed.TotalMilliseconds * 1000.0 / steps;

            FlagCloth twin = new FlagCloth(spec.Length, spec.Height, FlagFabric.Standard(spec.Waviness), 1f, 0f, detail);
            twin.SelfCollision = collide;
            twin.GroundY = -anchor.y;
            FlagMesh mesh = new FlagMesh(twin, refine);
            var handOver = new Stopwatch();
            double threadMs = 0.0;
            t = 0f;
            for (int i = 0; i < 480; i++)
            {
                StepCloth(twin, wind, gusts, anchor.y, spec, t, anchor);
                t += Step;
            }

            sw = Stopwatch.StartNew();
            for (int i = 0; i < steps; i++)
            {
                ClothJob job = Job(twin, mesh, wind, gusts, anchor.y, spec, t, anchor);
                worker.Add(ref job);
                worker.Run();
                worker.Wait();
                threadMs += worker.LastMilliseconds;
                if (worker.Jobs[0].Failure != null)
                    throw new Exception("the cloth's thread threw", worker.Jobs[0].Failure);
                worker.Clear();

                handOver.Start();
                mesh.Apply();
                handOver.Stop();
                t += Step;
            }

            sw.Stop();
            bool same = true;
            for (int k = 0; k < cloth.Count; k++)
                same &= cloth.X[k] == twin.X[k] && cloth.Y[k] == twin.Y[k] && cloth.Z[k] == twin.Z[k];

            mesh.Destroy();
            Debug.Log("RF BENCH wind=" + wind.ToString("0.#", CultureInfo.InvariantCulture) + " grid " + cloth.Columns + "x" + cloth.Rows +
                      " (" + detail + " cells" + (collide ? "" : ", no self-collision") + ", drawn x" + mesh.Refine + ")" +
                      " substeps " + cloth.SubStepsFor(FlagWind.SpeedAt(wind, anchor.y), Step) + " passes " + cloth.Passes + ": " +
                      inline.ToString("F1", CultureInfo.InvariantCulture) + " us per 1/60 s on this thread (Unity's Mono); on the cloth's thread " +
                      (threadMs * 1000.0 / steps).ToString("F1", CultureInfo.InvariantCulture) + " us with the mesh refreshed, hand-over " +
                      (handOver.Elapsed.TotalMilliseconds * 1000.0 / steps).ToString("F1", CultureInfo.InvariantCulture) + " us here, round trip " +
                      (sw.Elapsed.TotalMilliseconds * 1000.0 / steps).ToString("F1", CultureInfo.InvariantCulture) + " us; same result bit for bit: " + same);
        }

        worker.Stop();
    }

    // ---- the picture ----------------------------------------------------------------------

    private static Color[] ReadPixels(Setup s)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = s.Target;
        s.Readback.ReadPixels(new Rect(0, 0, s.Width, s.Height), 0, 0, false);
        s.Readback.Apply(false);
        RenderTexture.active = previous;
        return s.Readback.GetPixels();
    }

    /// <summary>Filmic (Hable 2010, the Uncharted 2 curve) over the exposure, then sRGB: a picture for the eye.</summary>
    private static void SavePng(Color[] pixels, int width, int height, float exposure, string file)
    {
        var png = new Texture2D(width, height, TextureFormat.RGB24, false, false);
        var output = new Color[pixels.Length];
        float white = 1f / Hable(11.2f);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i] * exposure * 2f;
            output[i] = new Color(Srgb(Hable(p.r) * white), Srgb(Hable(p.g) * white), Srgb(Hable(p.b) * white), 1f);
        }

        png.SetPixels(output);
        png.Apply(false);
        File.WriteAllBytes(file, png.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(png);
    }

    private static float Hable(float x)
    {
        const float A = 0.15f, B = 0.50f, C = 0.10f, D = 0.20f, E = 0.02f, F = 0.30f;
        return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
    }

    private static float Srgb(float c)
    {
        c = Mathf.Clamp01(c);
        return c <= 0.0031308f ? 12.92f * c : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
    }

    private static string Fmt(Color c)
    {
        return "(" + c.r.ToString("F3", CultureInfo.InvariantCulture) + " " + c.g.ToString("F3", CultureInfo.InvariantCulture) + " " +
               c.b.ToString("F3", CultureInfo.InvariantCulture) + ")";
    }

    private static string Fmt3(Vector3 v)
    {
        return v.x.ToString("F2", CultureInfo.InvariantCulture) + "x" + v.y.ToString("F2", CultureInfo.InvariantCulture) + "x" +
               v.z.ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string Env(string name, string fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static float ParseFloat(string text)
    {
        return float.Parse(text, CultureInfo.InvariantCulture);
    }

    private static float[] ParseFloats(string text)
    {
        var list = new List<float>();
        foreach (string part in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            list.Add(float.Parse(part.Trim(), CultureInfo.InvariantCulture));
        return list.ToArray();
    }

    private static int[] ParseInts(string text)
    {
        var list = new List<int>();
        foreach (string part in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            list.Add(int.Parse(part.Trim(), CultureInfo.InvariantCulture));
        return list.ToArray();
    }
}
