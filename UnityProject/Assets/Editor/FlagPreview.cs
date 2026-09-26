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
///   RF_SUN 1.3               the sun's intensity by day (Render It!'s "Sunny Default" has 2.7 in the game)
///   RF_WET 0..1              the game's ground wetness (_WeatherParams.w): the cloth wet in the rain
///   RF_POLE 1..3 (the pole's top: FlagPoleStyle), RF_ROPES 0..2, RF_THICK, RF_POLECOLOURS "#pole,#top,#rope"
///   RF_TILT, RF_LEAN    the hoist's tilt and lean, degrees (FlagOrientation)
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

    /// <summary>RF_WINDYAW: the way the wind blows, degrees from +x (the flag's heading) towards +z; 180 blows it back over its pole.</summary>
    private static float _windYaw;

    /// <summary>A drawn pole for the frame: its mesh and where it goes (none: the preview's plain grey stand-in).</summary>
    private sealed class PoleDraw
    {
        public Mesh Mesh;
        public Matrix4x4 Matrix;
    }

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
        if (countries.Length > 0)
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

        // The hoist's line and the fly at rest, for a flag heading along +x.
        float hx, hy, hz, fx, fy, fz;
        FlagOrientation.Frame(0f, spec.Tilt, spec.Lean, out hx, out hy, out hz, out fx, out fy, out fz);
        var hoist = new Vector3(hx, hy, hz);
        var fly = new Vector3(fx, fy, fz);

        foreach (int detail in details)
        foreach (int refine in refines)
        foreach (float wind in winds)
        {
            foreach (FlagSpec design in designs)
            {
                FlagSpec flag = design;
                int pattern = flag.Pattern;
                Vector3 anchor = new Vector3(0f, elevation + flag.Height, 0f);

                // The wind blows along +x (the flag's own direction, the panel's default mode).
                FlagCloth cloth = new FlagCloth(flag.Length, flag.Height, Fabric(flag.Waviness, fabrics[row % fabrics.Length]), 1f, 0f, detail,
                                                hx, hy, hz);
                cloth.SelfCollision = collide;
                cloth.GroundY = -anchor.y;
                FlagMesh mesh = new FlagMesh(cloth, refine);

                // The pole: from the ground (y = 0) up its axis to the anchor's level, as the game casts it.
                PoleDraw pole = null;
                if (flag.HasPole)
                {
                    float length = hy < -0.05f ? anchor.y / -hy : FlagPoleShape.Fallback(flag);
                    FlagPoleShape shape = FlagPoleShape.For(flag, length);
                    shape.ApplyTo(cloth, fx, fy, fz);
                    pole = new PoleDraw { Mesh = FlagPoleMesh.Build(shape, null), Matrix = FlagPoleMesh.Placement(anchor, shape, hoist, fly) };
                    Debug.Log("RF PREVIEW: pole " + FlagPoleStyle.LogName(flag.Pole) + ", " + flag.Ropes + " rope(s): foot " +
                              (200f * shape.RadiusFoot).ToString("F1", CultureInfo.InvariantCulture) + " cm, top " +
                              (200f * shape.RadiusTop).ToString("F1", CultureInfo.InvariantCulture) + " cm across, " +
                              length.ToString("F1", CultureInfo.InvariantCulture) + " m to the anchor, hoist " +
                              (100f * shape.Offset).ToString("F1", CultureInfo.InvariantCulture) + " cm off the axis, " +
                              pole.Mesh.vertexCount + " vertices");
                }

                float t = 0f;
                int col = 0;

                foreach (float moment in moments)
                {
                    while (t + Step <= moment + 1e-4f)
                    {
                        StepCloth(cloth, wind, gusts, elevation + flag.Height, flag, t, anchor);
                        t += Step;
                    }

                    mesh.Upload(1f);
                    string info;
                    Color[] pixels = RenderFlag(s, mesh, pole, flag, anchor, yaw, pitch, dist, out info);
                    string file = Path.Combine(outDir, "rf-" + row + "-" + col + ".png");
                    SavePng(pixels, width, height, s.Exposure, file);
                    Debug.Log("RF FRAME row=" + row + " col=" + col + " t=" + moment.ToString("0.###", CultureInfo.InvariantCulture) +
                              " wind=" + wind.ToString("0.#", CultureInfo.InvariantCulture) + " pattern=" + pattern +
                              " file=" + file + " | " + Droop(cloth) + ", " + cloth.Columns + "x" + cloth.Rows +
                              (mesh.Refine > 1 ? " x" + mesh.Refine : "") + (collide ? "" : " no collision") +
                              (flag.IsCountry ? ", " + flag.Country : "") +
                              (flag.Darkness > 0f ? ", darkness " + (flag.Darkness * 100f).ToString("0", CultureInfo.InvariantCulture) + "%" : "") +
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
        // Looking at the flag's middle (or RF_LOOK off it) from its side, slightly down on it.
        Bounds b = mesh.Bounds;
        Vector3 centre = anchor + new Vector3(0.5f * flag.Length, -0.5f * flag.Height, 0f) + _look;
        float range = dist * Mathf.Max(flag.Length, flag.Height);
        Quaternion look = Quaternion.Euler(pitchDeg, yawDeg, 0f);
        Vector3 fwd = look * Vector3.forward;
        s.Camera.transform.position = centre - fwd * range;
        s.Camera.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

        FlagLook.Apply(s.Block, flag, Vector4.zero);
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
