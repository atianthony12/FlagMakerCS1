using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RealFlag.Flags;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// String flags in pictures, before the game (tools\preview\string-render.ps1): a span hung
/// between two posts, its flags playing their shared cloths -- the mod's own StringSpan,
/// StringMotion and StringAtlas, drawn with the REAL FlagString and FlagPole shaders in the
/// game's legacy deferred lighting, HDR, linear (as FlagPreview draws a flag). One frame per
/// scene of RF_SCENES ("key=value;..." scenes separated by "|"), logged as RF FRAME lines.
/// </summary>
public static class StringPreview
{
    private const string FlagShaderPath = "Assets/RealFlag/FlagString.shader";
    private const string PoleShaderPath = "Assets/RealFlag/FlagPole.shader";
    private const float Step = 1f / 60f;

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

        string outDir = Env("RF_OUT", "StringPreview");
        Directory.CreateDirectory(outDir);
        int width = int.Parse(Env("RF_W", "480"), CultureInfo.InvariantCulture);
        int height = int.Parse(Env("RF_H", "300"), CultureInfo.InvariantCulture);

        string flagsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../RealFlag/Resources/Flags"));
        FlagImages.Source = (image, small) =>
        {
            string file = Path.Combine(small ? Path.Combine(flagsDir, "Small") : flagsDir, image + ".png");
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        };
        FlagImages.Warn = Debug.LogWarning;
        FlagImages.Note = Debug.Log;

        Shader flagShader = AssetDatabase.LoadAssetAtPath<Shader>(FlagShaderPath);
        Shader poleShader = AssetDatabase.LoadAssetAtPath<Shader>(PoleShaderPath);
        if (flagShader == null || poleShader == null)
            throw new Exception("the string's shaders were not found");
        var flagMaterial = new Material(flagShader);
        var cordMaterial = new Material(poleShader);
        if (flagMaterial.passCount < 5 || cordMaterial.passCount < 5)
            throw new Exception("a shader did not compile (passes " + flagMaterial.passCount + ", " + cordMaterial.passCount + "): run ShaderCheck");

        Camera camera;
        RenderTexture target;
        Texture2D readback;
        Scene(width, height, out camera, out target, out readback);
        float pixelsPerRadian = height / (2f * Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad));
        Shader.SetGlobalFloat(FlagPoleMesh.IdPixelRadius, 0.6f / pixelsPerRadian);

        string[] scenes = Env("RF_SCENES", "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        if (scenes.Length == 0)
            scenes = new[] { "" };

        for (int i = 0; i < scenes.Length; i++)
        {
            Dictionary<string, string> o = Parse(scenes[i]);
            StringSpec spec = SpecOf(o);
            float span = F(o, "span", 12f), rise = F(o, "rise", 0f), up = F(o, "height", 6f);
            float wind = F(o, "wind", 6f), windYaw = F(o, "windyaw", 90f), seconds = F(o, "t", 8f);
            string view = S(o, "view", "side");

            var s = new StringSpan { Spec = spec, A = new Vector3(0f, up, 0f), B = new Vector3(span, up + rise, 0f), Seed = 1.7f };
            s.Lay();
            var sources = new Dictionary<StringMotion.SourceKey, StringMotion.Source>();
            var atlas = new StringAtlas();
            float clock = 0f;
            float across = Mathf.Sin(windYaw * Mathf.Deg2Rad);
            Func<StringMotion.SourceKey, StringMotion.Source> sourceFor = key =>
            {
                StringMotion.Source src;
                if (!sources.TryGetValue(key, out src))
                {
                    src = new StringMotion.Source(key, clock);
                    sources[key] = src;
                }

                src.LastWanted = clock;
                return src;
            };

            // Run: the swing, the sources (as the mod's thread runs them), the atlas a cell a frame.
            int steps = Mathf.CeilToInt(seconds / Step);
            int posed = 0;
            for (int k = 0; k < steps; k++)
            {
                clock += Step;
                s.StepSwing(Step, across * wind);
                s.Hang();
                atlas.Tick(clock);
                s.PoseFlags(sourceFor, atlas, FlagCountries.ByName, wind, windYaw, spec.Motion != FlagMotion.Waves, out posed);
                foreach (StringMotion.Source src in sources.Values)
                {
                    ClothJob job;
                    if (!src.Plan(Step, 4, 12, 1f, false, out job))
                        continue;
                    ClothWorker.Execute(ref job);
                    src.Took(ref job);
                }
            }

            // The atlas's last cells to the GPU, then the pose at the frame's time.
            atlas.Tick(clock + 1f);
            s.Hang();
            s.PoseFlags(sourceFor, atlas, FlagCountries.ByName, wind, windYaw, spec.Motion != FlagMotion.Waves, out posed);
            s.BuildCord();
            if (atlas.Texture != null)
            {
                flagMaterial.SetTexture("_RFAtlas", atlas.Texture);
                flagMaterial.SetVector("_RFAtlasGrid", atlas.Grid);
                flagMaterial.SetFloat("_RFAtlasInset", StringAtlas.Inset);
            }

            GameObject[] posts = Posts(s, spec);
            var cordBlock = new MaterialPropertyBlock();
            Vector4 colour = FlagLook.Linear(spec.Colour);
            cordBlock.SetVector("_RFRopeColor", colour);
            cordBlock.SetVector("_RFPoleColor", colour);

            Vector3 centre = s.A + s.Bounds.center;
            float range = F(o, "dist", 0.85f) * Mathf.Max(span, 2f);
            Vector3 eye;
            if (view == "close")
                eye = s.A + new Vector3(span * 0.3f, -0.4f, -range * 0.25f);
            else if (view == "street")
                eye = centre + new Vector3(-0.15f * span, -2.2f, -5.5f);
            else if (view == "three")
                eye = centre + new Vector3(-range * 0.45f, 0.2f * range, -range * 0.8f);
            else
                eye = centre + new Vector3(0f, 0.08f * range, -range);
            Vector3 lookAt = view == "close" ? s.A + new Vector3(span * 0.3f, -spec.Hang, 0f) : centre;
            camera.transform.position = eye;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - eye, Vector3.up);

            if (s.Flags != null && posed > 0)
                Graphics.DrawMesh(s.Flags.Mesh, Matrix4x4.TRS(s.A, Quaternion.identity, Vector3.one), flagMaterial, 0, camera, 0, new MaterialPropertyBlock(), true, true);
            Graphics.DrawMesh(s.Cord.Mesh, Matrix4x4.TRS(s.A, Quaternion.identity, Vector3.one), cordMaterial, 0, camera, 0, cordBlock, true, true);
            camera.Render();

            string file = Path.GetFullPath(Path.Combine(outDir, "string-" + i + ".png"));
            SavePng(Read(target, readback, width, height), width, height, 0.6f, file);
            foreach (GameObject p in posts)
                UnityEngine.Object.DestroyImmediate(p);

            Debug.Log("RF FRAME row=" + i + " col=0 t=" + seconds.ToString("0.#", CultureInfo.InvariantCulture) + " wind=" +
                      wind.ToString("0.#", CultureInfo.InvariantCulture) + " pattern=0 file=" + file + " | " + posed + " flags of " + s.Columns + "x" + s.Rows +
                      ", " + sources.Count + " shared cloth(s), atlas " + atlas.Count + ", swing " + (s.Swing.Angle * Mathf.Rad2Deg).ToString("F0", CultureInfo.InvariantCulture) +
                      " deg, sag " + s.Curve.Deepest.ToString("F2", CultureInfo.InvariantCulture) + " m | " + spec.Describe());
            s.Release();
            atlas.Clear();
        }
    }

    private static StringSpec SpecOf(Dictionary<string, string> o)
    {
        StringSpec s = StringSpec.Default;
        s.Width = F(o, "w", s.Width);
        s.Height = F(o, "h", s.Height);
        s.Gap = F(o, "gap", s.Gap);
        s.Sag = F(o, "sag", s.Sag);
        s.Thickness = F(o, "thick", s.Thickness * 1000f) / 1000f;
        s.Waviness = F(o, "wavy", s.Waviness);
        s.Swing = F(o, "swing", s.Swing);
        s.Depth = F(o, "depth", s.Depth);
        s.Motions = (int)F(o, "motions", s.Motions);
        s.Margin = F(o, "margin", s.Margin);

        string shape = S(o, "shape", "pennant");
        for (int i = 0; i < FlagCut.Values.Length; i++)
        {
            if (FlagCut.LogName(FlagCut.Values[i]).Replace(" ", "") == shape.Replace(" ", ""))
                s.Shape = FlagCut.Values[i];
        }

        s.Mount = S(o, "mount", "hoist") == "top" ? StringMount.Top : StringMount.Hoist;
        string kind = S(o, "kind", "cord");
        s.Kind = kind == "tape" ? StringKind.Tape : kind == "rope" ? StringKind.Rope : kind == "wire" ? StringKind.Wire : StringKind.Cord;
        uint c;
        if (FlagColours.TryParseHex(S(o, "colour", ""), out c))
            s.Colour = c;
        string motion = S(o, "motion", "waves");
        s.Motion = motion == "still" ? FlagMotion.Still : motion == "flat" ? FlagMotion.Flat : FlagMotion.Waves;

        string seq = S(o, "seq", "alt");
        if (seq == "same") s.Sequence = StringSequence.Same;
        else if (seq == "countries") s.Sequence = StringSequence.Countries;
        else s.Sequence = StringSequence.Alternate;

        string colours = S(o, "colours", "");
        if (colours.Length > 0)
        {
            var designs = new List<FlagSpec>();
            foreach (string hex in colours.Split(','))
            {
                FlagSpec d = FlagSpec.Default;
                d.Pattern = FlagPattern.Plain;
                if (FlagColours.TryParseHex(hex, out c))
                    d.Colour1 = c;
                designs.Add(d);
            }

            s.Designs = designs.ToArray();
        }

        string pattern = S(o, "pattern", "");
        if (pattern.Length > 0)
        {
            for (int i = 0; i < s.Designs.Length; i++)
                s.Designs[i].Pattern = int.Parse(pattern, CultureInfo.InvariantCulture);
        }

        string which = S(o, "which", "all");
        s.Which = which == "region" ? CountryWhich.Region : which == "list" ? CountryWhich.List : CountryWhich.All;
        s.Region = (int)F(o, "region", s.Region);
        s.Countries = S(o, "list", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        string order = S(o, "order", "az");
        s.CountryOrder = order == "random" ? CountryOrder.Random : order == "region" ? CountryOrder.ByRegion : order == "picked" ? CountryOrder.AsPicked : CountryOrder.AToZ;
        s.Lead = S(o, "lead", "");
        s.LeadEvery = (int)F(o, "every", 0f);
        return s.Sanitised();
    }

    private static GameObject[] Posts(StringSpan s, StringSpec spec)
    {
        Shader diffuse = Shader.Find("Legacy Shaders/Diffuse");
        var grey = new Material(diffuse) { color = new Color(0.62f, 0.6f, 0.56f) };
        var list = new List<GameObject>();
        foreach (Vector3 end in new[] { s.A, s.B })
        {
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "RFStringPost";
            post.GetComponent<Renderer>().sharedMaterial = grey;
            post.transform.localScale = new Vector3(0.18f, 0.5f * end.y + 0.1f, 0.18f);
            post.transform.position = new Vector3(end.x, 0.5f * end.y + 0.05f, end.z);
            list.Add(post);
        }

        return list.ToArray();
    }

    private static void Scene(int width, int height, out Camera camera, out RenderTexture target, out Texture2D readback)
    {
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 120f;
        QualitySettings.shadowCascades = 2;

        GameObject camObject = new GameObject("RFStringCamera");
        camera = camObject.AddComponent<Camera>();
        camera.allowHDR = true;
        camera.renderingPath = RenderingPath.DeferredLighting;
        camera.fieldOfView = 40f;
        camera.nearClipPlane = 0.2f;
        camera.farClipPlane = 1000f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.55f, 0.72f, 1.0f, 1f);
        target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        target.Create();
        camera.targetTexture = target;
        readback = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);

        Shader diffuse = Shader.Find("Legacy Shaders/Diffuse");
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(60f, 1f, 60f);
        ground.GetComponent<Renderer>().sharedMaterial = new Material(diffuse) { color = new Color(0.36f, 0.42f, 0.30f) };

        GameObject sunObject = new GameObject("RFStringSun");
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sun.color = new Color(1f, 0.95f, 0.86f);
        sun.intensity = 1.3f;
        Vector3 toSun = Quaternion.Euler(0f, 35f, 0f) * new Vector3(0f, 0.55f, -0.83f);
        sunObject.transform.rotation = Quaternion.LookRotation(-toSun.normalized);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.45f, 0.55f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.42f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.17f);

        Color sunLight = sun.color.linear * sun.intensity;
        Shader.SetGlobalVector(FlagLook.IdSunDir, new Vector4(toSun.normalized.x, toSun.normalized.y, toSun.normalized.z, 0f));
        Shader.SetGlobalVector(FlagLook.IdSunColor, new Vector4(sunLight.r, sunLight.g, sunLight.b, 0f));
        Shader.SetGlobalVector("_WeatherParams", new Vector4(15f, 0f, 0f, 0f));
    }

    private static Color[] Read(RenderTexture target, Texture2D readback, int width, int height)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        readback.Apply(false);
        RenderTexture.active = previous;
        return readback.GetPixels();
    }

    /// <summary>As FlagPreview's: Hable's filmic curve over the exposure, then sRGB.</summary>
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
        const float A = 0.15f, B = 0.50f, C = 0.10f, D = 0.20f, E = 0.02f, F0 = 0.30f;
        return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F0)) - E / F0;
    }

    private static float Srgb(float c)
    {
        c = Mathf.Clamp01(c);
        return c <= 0.0031308f ? 12.92f * c : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
    }

    private static Dictionary<string, string> Parse(string scene)
    {
        var d = new Dictionary<string, string>();
        foreach (string part in scene.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq > 0)
                d[part.Substring(0, eq).Trim()] = part.Substring(eq + 1).Trim();
        }

        return d;
    }

    private static float F(Dictionary<string, string> o, string key, float fallback)
    {
        string v;
        return o.TryGetValue(key, out v) ? float.Parse(v, CultureInfo.InvariantCulture) : fallback;
    }

    private static string S(Dictionary<string, string> o, string key, string fallback)
    {
        string v;
        return o.TryGetValue(key, out v) ? v : fallback;
    }

    private static string Env(string name, string fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }
}
