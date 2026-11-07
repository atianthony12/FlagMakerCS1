using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prints every error and warning Unity holds against the mod's shaders, and fails if there is
/// one. A SURFACE shader that does not compile is replaced, silently, by a one-pass stand-in
/// that draws the vertex colour: no "Shader error" in the batch log (the messages stay on the
/// imported asset, for the inspector), a bundle that builds, a flag drawn flat white. That was
/// the first render of FlagCloth.shader (2026-09-25). build-bundle.ps1 runs this first.
///   Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod ShaderCheck.Run
/// </summary>
public static class ShaderCheck
{
    /// <summary>
    /// Every shader the mod ships, and the passes it must have: a surface shader with addshadow
    /// has five (PREPASSBASE, PREPASSFINAL, FORWARDBASE, FORWARDADD, SHADOWCASTER); the stand-in
    /// a failed one becomes has one. The gizmo is two plain passes.
    /// </summary>
    private static readonly string[] Shaders =
    {
        "Assets/RealFlag/FlagCloth.shader",
        "Assets/RealFlag/FlagPole.shader",
        "Assets/RealFlag/FlagGizmo.shader",
    };

    private static readonly int[] Passes = { 5, 5, 2 };

    public static void Run()
    {
        bool failed = false;
        int index = -1;

        // Internal in 5.6: ShaderUtil.GetShaderErrors(Shader) -> ShaderError[] { message,
        // messageDetails, platform, file, line, warning }.
        MethodInfo getErrors = typeof(ShaderUtil).GetMethod("GetShaderErrors", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        MethodInfo hasError = typeof(ShaderUtil).GetMethod("ShaderHasError", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (string path in Shaders)
        {
            index++;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                Debug.Log("SHADER CHECK: " + path + " NOT FOUND");
                failed = true;
                continue;
            }

            int passes = new Material(shader).passCount;
            bool error = hasError != null && (bool)hasError.Invoke(null, new object[] { shader });
            Debug.Log("SHADER CHECK: " + shader.name + " passes=" + passes + " (wants " + Passes[index] + ") hasError=" + error +
                      " supported=" + shader.isSupported);
            if (passes != Passes[index])
            {
                Debug.Log("SHADER CHECK ERROR: " + shader.name + " has " + passes + " pass(es), not " + Passes[index] +
                          ": a surface shader that did not compile is a one-pass stand-in");
                failed = true;
            }

            if (getErrors != null)
            {
                var errors = getErrors.Invoke(null, new object[] { shader }) as Array;
                if (errors != null)
                {
                    foreach (object e in errors)
                    {
                        Type t = e.GetType();
                        string message = Field(t, e, "message");
                        string details = Field(t, e, "messageDetails");
                        string file = Field(t, e, "file");
                        string line = Field(t, e, "line");
                        string warning = Field(t, e, "warning");
                        Debug.Log("SHADER CHECK " + (warning == "1" ? "WARNING" : "ERROR") + ": " + shader.name + " " + file + ":" + line +
                                  " " + message + (string.IsNullOrEmpty(details) ? "" : " | " + details));
                        failed = true;
                    }
                }
            }

            if (error)
                failed = true;
        }

        Debug.Log(failed ? "SHADER CHECK FAILED" : "SHADER CHECK OK");
        EditorApplication.Exit(failed ? 1 : 0);
    }

    private static string Field(Type t, object o, string name)
    {
        FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object v = f != null ? f.GetValue(o) : null;
        return v != null ? v.ToString() : "";
    }
}
