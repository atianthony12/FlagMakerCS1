using UnityEngine;

/// <summary>
/// The preview's copy of what Dynamic Resolution (Workshop 812713438) and Lumina's Dynamic
/// Resolution / SSAA (2983036781) do to the game's camera, read from their IL: the game's camera
/// is disabled; an empty camera of their own (culling mask 0, depth -3, tagged MainCamera, the
/// game camera's pixel rect) copies its pose, field of view and clip planes every Update, and in
/// its OnRenderImage renders the game's camera by hand into a target the screen's size times a
/// factor -- rect set to the whole target, targetTexture set, Render(), targetTexture back to null,
/// rect back -- then blits that down to its own destination. So Camera.main is this empty camera,
/// and the game's camera renders once (or, with both mods on, twice) a frame at another size.
/// FlagPreview drives it (RF_DYNRES); here outside Assets/Editor, so it can be a component.
/// </summary>
[ExecuteInEditMode]
public sealed class DynResStandIn : MonoBehaviour
{
    /// <summary>The camera rendered by hand (the game's), its factor of the screen, and the screen's size.</summary>
    public Camera Game;
    public float Factor = 1f;
    public int ScreenWidth, ScreenHeight;

    /// <summary>The target the game's camera last rendered into, made again whenever its size changes.</summary>
    public RenderTexture Target;

    /// <summary>For the log: OnRenderImage calls, and the target made afresh.</summary>
    public int Renders, Recreated;

    /// <summary>What the mods do in Update: the game camera's pose, field of view, clip planes and rect.</summary>
    public void CopyPose()
    {
        Camera own = GetComponent<Camera>();
        own.fieldOfView = Game.fieldOfView;
        own.nearClipPlane = Game.nearClipPlane;
        own.farClipPlane = Game.farClipPlane;
        own.rect = Game.rect;
        transform.position = Game.transform.position;
        transform.rotation = Game.transform.rotation;
    }

    private void Update()
    {
        if (Game != null)
            CopyPose();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RenderGame(destination);
        Renders++;
    }

    /// <summary>The mods' OnRenderImage: the game's camera rendered into the target at the factor, then blitted to <paramref name="destination"/>.</summary>
    public void RenderGame(RenderTexture destination)
    {
        int w = Mathf.Max(1, Mathf.RoundToInt(ScreenWidth * Factor));
        int h = Mathf.Max(1, Mathf.RoundToInt(ScreenHeight * Factor));
        if (Target == null || Target.width != w || Target.height != h)
        {
            if (Target != null)
            {
                Target.Release();
                DestroyImmediate(Target);
            }

            Target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { name = "RFDynResTarget" };
            Target.Create();
            Recreated++;
        }

        Rect rect = Game.rect;
        Game.rect = new Rect(0f, 0f, 1f, 1f);
        Game.targetTexture = Target;
        Game.Render();
        Game.targetTexture = null;
        Game.rect = rect;
        Graphics.Blit(Target, destination);
    }

    private void OnDestroy()
    {
        if (Target != null)
        {
            Target.Release();
            DestroyImmediate(Target);
        }
    }
}
