using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>채팅 줄의 상호작용 단추에 쓰는 작은 그림. 그림 파일 없이 그 자리에서 그린다(흰색, 단추가 색을 입힌다).</summary>
public static class ChatActionIcons
{
    public const int Size = 64;
    private static readonly Dictionary<string, Sprite> made = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { made.Clear(); }

    /// <summary>invite(초대), warn(경고), mute(채팅 금지), kick(추방), delete(메시지 삭제).</summary>
    public static Sprite Get(string name)
    {
        if (made.TryGetValue(name, out var sprite) && sprite != null) { return sprite; }
        var ink = new float[Size * Size];
        switch (name)
        {
            case "invite":
                // 사람과 더하기.
                Disc(ink, 24, 44, 9); Arc(ink, 24, 12, 17, 12);
                Line(ink, 48, 22, 48, 42, 3); Line(ink, 38, 32, 58, 32, 3);
                break;
            case "warn":
                Line(ink, 32, 54, 8, 12, 3); Line(ink, 32, 54, 56, 12, 3); Line(ink, 8, 12, 56, 12, 3);
                Line(ink, 32, 42, 32, 28, 3); Disc(ink, 32, 20, 3.4f);
                break;
            case "mute":
                // 말풍선에 빗금.
                Box(ink, 10, 22, 54, 52, 3); Line(ink, 20, 22, 14, 10, 3); Line(ink, 14, 10, 30, 22, 3);
                Line(ink, 10, 56, 54, 8, 4);
                break;
            case "kick":
                // 문과 밖으로 나가는 화살표.
                Line(ink, 34, 54, 12, 54, 3); Line(ink, 12, 54, 12, 10, 3); Line(ink, 12, 10, 34, 10, 3);
                Line(ink, 26, 32, 56, 32, 3); Line(ink, 56, 32, 45, 43, 3); Line(ink, 56, 32, 45, 21, 3);
                break;
            default:
                // 휴지통.
                Line(ink, 12, 48, 52, 48, 3); Line(ink, 26, 54, 38, 54, 3);
                Line(ink, 18, 48, 21, 10, 3); Line(ink, 46, 48, 43, 10, 3); Line(ink, 21, 10, 43, 10, 3);
                Line(ink, 27, 40, 28, 18, 2); Line(ink, 37, 40, 36, 18, 2);
                break;
        }
        var pixels = new Color32[Size * Size];
        for (int i = 0; i < pixels.Length; i++) { pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(ink[i]) * 255f)); }
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Chat Action " + name, hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        texture.SetPixels32(pixels); texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = name; sprite.hideFlags = HideFlags.DontSave; made[name] = sprite;
        return sprite;
    }

    private static void Put(float[] ink, int x, int y, float value)
    { if (x >= 0 && y >= 0 && x < Size && y < Size) { int i = y * Size + x; if (value > ink[i]) { ink[i] = value; } } }
    private static void Line(float[] ink, float x0, float y0, float x1, float y1, float half)
    {
        int left = Mathf.FloorToInt(Mathf.Min(x0, x1) - half - 1), right = Mathf.CeilToInt(Mathf.Max(x0, x1) + half + 1);
        int bottom = Mathf.FloorToInt(Mathf.Min(y0, y1) - half - 1), top = Mathf.CeilToInt(Mathf.Max(y0, y1) + half + 1);
        float dx = x1 - x0, dy = y1 - y0, length = dx * dx + dy * dy;
        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
            {
                float t = length <= 0f ? 0f : Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / length);
                float px = x0 + dx * t - x, py = y0 + dy * t - y;
                Put(ink, x, y, half + .5f - Mathf.Sqrt(px * px + py * py));
            }
        }
    }
    private static void Box(float[] ink, float x0, float y0, float x1, float y1, float half)
    { Line(ink, x0, y0, x1, y0, half); Line(ink, x1, y0, x1, y1, half); Line(ink, x1, y1, x0, y1, half); Line(ink, x0, y1, x0, y0, half); }
    private static void Disc(float[] ink, float cx, float cy, float radius)
    {
        for (int y = Mathf.FloorToInt(cy - radius - 1); y <= Mathf.CeilToInt(cy + radius + 1); y++)
        { for (int x = Mathf.FloorToInt(cx - radius - 1); x <= Mathf.CeilToInt(cx + radius + 1); x++) { Put(ink, x, y, radius + .5f - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy))); } }
    }
    /// <summary>어깨: 가운데 (cx, cy)에서 위로 볼록한 반타원.</summary>
    private static void Arc(float[] ink, float cx, float cy, float halfWidth, float height)
    {
        for (int y = Mathf.FloorToInt(cy - 1); y <= Mathf.CeilToInt(cy + height + 1); y++)
        {
            for (int x = Mathf.FloorToInt(cx - halfWidth - 1); x <= Mathf.CeilToInt(cx + halfWidth + 1); x++)
            {
                if (y < cy) { continue; }
                float nx = (x - cx) / halfWidth, ny = (y - cy) / height;
                Put(ink, x, y, (1f - Mathf.Sqrt(nx * nx + ny * ny)) * Mathf.Min(halfWidth, height) + .5f);
            }
        }
    }
}
