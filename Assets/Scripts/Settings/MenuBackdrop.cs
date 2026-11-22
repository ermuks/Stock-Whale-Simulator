using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Capture only when opening a menu, never a live extra camera.
/// 번짐은 절반 크기의 그림에서 가로·세로 가우시안을 네 번 되풀이해 만든다. 한 번만, 그것도 픽셀을 건너뛰며 읽으면 가로세로로 줄이 선 십자 모양 번짐이 된다.
/// </summary>
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
            Release(); capture = Make(width, height, 24); scratch = Make(width / 2, Mathf.Max(1, height / 2), 0); blurred = Make(width / 2, Mathf.Max(1, height / 2), 0);
        }
        var previous = camera.targetTexture; var active = RenderTexture.active;
        try
        {
            camera.targetTexture = capture; camera.Render();
            // 절반 크기로 줄여 담고(이웃 픽셀이 섞인다), 한 픽셀 간격의 가우시안을 가로·세로로 네 번 돌린다. 번짐이 둥글고 고르게 퍼진다.
            Graphics.Blit(capture, blurred);
            for (int pass = 0; pass < 4; pass++)
            {
                material.SetVector("_Direction", new Vector4(1f / blurred.width, 0, 0, 0)); Graphics.Blit(blurred, scratch, material);
                material.SetVector("_Direction", new Vector4(0, 1f / blurred.height, 0, 0)); Graphics.Blit(scratch, blurred, material);
            }
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
