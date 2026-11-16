using UnityEngine;
using UnityEngine.UI;

/// <summary>Capture only when opening a menu. Two small blur passes, never a live extra camera.</summary>
public sealed class MenuBackdrop : System.IDisposable
{
    private readonly RawImage image;
    private RenderTexture capture, blurred, scratch;
    private readonly Material material;
    public bool Ready => blurred != null && image.texture == blurred;
    public MenuBackdrop(RawImage graphic)
    {
        image = graphic; var shader = Resources.Load<Shader>("UI/SystemMenu/MenuBlur");
        if (shader != null) { material = new Material(shader); }
    }
    public void Capture()
    {
        var camera = Camera.main;
        if (camera == null || material == null) { return; }
        int width = 768, height = Mathf.Max(1, Mathf.RoundToInt(width / camera.aspect));
        if (capture == null || capture.height != height)
        {
            Release(); capture = Make(width, height, 24); scratch = Make(width, height, 0); blurred = Make(width, height, 0);
        }
        var previous = camera.targetTexture; var active = RenderTexture.active;
        try
        {
            camera.targetTexture = capture; camera.Render();
            material.SetVector("_Direction", new Vector4(2f / width, 0, 0, 0)); Graphics.Blit(capture, scratch, material);
            material.SetVector("_Direction", new Vector4(0, 2f / height, 0, 0)); Graphics.Blit(scratch, blurred, material);
            image.texture = blurred;
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; }
    }
    private static RenderTexture Make(int w, int h, int depth)
    { var texture = new RenderTexture(w,h,depth) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; texture.Create(); return texture; }
    private void Release()
    {
        image.texture = null;
        foreach (var texture in new[] { capture, scratch, blurred })
        { if (texture != null) { texture.Release(); Object.Destroy(texture); } }
        capture = scratch = blurred = null;
    }
    public void Dispose() { Release(); if (material != null) { Object.Destroy(material); } }
}
