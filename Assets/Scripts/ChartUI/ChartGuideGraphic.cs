using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 World Space Canvas 안에서 눈금, 점선 십자선 또는 계좌 가격선을 한 메시로 그린다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ChartGuideGraphic : MaskableGraphic
{
    public readonly struct LevelLine
    {
        public readonly float Y, LabelY, LabelWidth;
        public readonly Color Tint;
        public LevelLine(float y, float labelY, float labelWidth, Color tint)
        { Y = y; LabelY = labelY; LabelWidth = labelWidth; Tint = tint; }
    }
    private enum Mode { Grid, Crosshair, Levels }
    private Mode mode;
    private float width, height, volumeHeight;
    private int intervals;
    private Vector2 cursor;
    private readonly List<LevelLine> lines = new(16);

    public ChartGuideGraphic()
    { useLegacyMeshGeneration = false; }

    protected override void Awake()
    { base.Awake(); raycastTarget = false; maskable = false; }

    public void SetGrid(float plotWidth, float plotHeight, int divisions)
    {
        if (mode == Mode.Grid && width == plotWidth && height == plotHeight && intervals == divisions)
        { return; }
        mode = Mode.Grid; width = plotWidth; height = plotHeight; intervals = divisions;
        SetVerticesDirty();
    }
    public void SetCrosshair(float plotWidth, float plotHeight, float volume, Vector2 position)
    {
        if (mode == Mode.Crosshair && width == plotWidth && height == plotHeight && volumeHeight == volume && cursor == position)
        { return; }
        mode = Mode.Crosshair; width = plotWidth; height = plotHeight; volumeHeight = volume; cursor = position;
        SetVerticesDirty();
    }
    public void SetLevels(float plotWidth, float plotHeight, List<LevelLine> source)
    {
        bool changed = mode != Mode.Levels || width != plotWidth || height != plotHeight || lines.Count != source.Count;
        for (int i = 0; !changed && i < lines.Count; i++)
        {
            var a = lines[i]; var b = source[i];
            changed = a.Y != b.Y || a.LabelY != b.LabelY || a.LabelWidth != b.LabelWidth || a.Tint != b.Tint;
        }
        if (!changed)
        { return; }
        mode = Mode.Levels; width = plotWidth; height = plotHeight;
        lines.Clear(); lines.AddRange(source); SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (width <= 0f || height <= 0f)
        { return; }
        if (mode == Mode.Crosshair)
        {
            // 짧은 사각형을 한 Graphic에 묶는다. 텍스처 생성/LineRenderer 인스턴스 없음.
            const float dash = 10f, gap = 7f, half = .8f;
            for (float x = 0f; x < width; x += dash + gap)
            { Quad(vh, x, cursor.y - half, Mathf.Min(width, x + dash), cursor.y + half, color); }
            for (float y = -volumeHeight; y < height; y += dash + gap)
            { Quad(vh, cursor.x - half, y, cursor.x + half, Mathf.Min(height, y + dash), color); }
        }
        else if (mode == Mode.Levels)
        {
            foreach (var line in lines)
            {
                float y = Mathf.Clamp(line.Y, 1f, height - 1f);
                float stride = Mathf.Max(20f, width / 96f);
                for (float x = 0f; x < width; x += stride)
                { Quad(vh, x, y - 1f, x + stride >= width ? width : x + stride * .65f, y + 1f, line.Tint); }
                // 겹치는 라벨만 세로로 옮긴다. 선 자체는 실제 가격 위치에 남는다.
                if (line.LabelWidth > 0f && Mathf.Abs(line.LabelY - y) > 1f)
                {
                    float x = line.LabelWidth + 6f;
                    Quad(vh, x, Mathf.Min(y, line.LabelY), x + 1f, Mathf.Max(y, line.LabelY), line.Tint);
                }
            }
        }
        else
        {
            for (int i = 0; i <= intervals; i++)
            {
                float y = height * i / Mathf.Max(1, intervals);
                Quad(vh, 0f, Mathf.Max(0f, y - 1f), width, Mathf.Min(height, y + 1f), color);
            }
            Quad(vh, width - 1f, 0f, width, height, color);
        }
    }
    private static void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(x0, y0), tint, Vector2.zero);
        vh.AddVert(new Vector3(x0, y1), tint, Vector2.zero);
        vh.AddVert(new Vector3(x1, y1), tint, Vector2.zero);
        vh.AddVert(new Vector3(x1, y0), tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
