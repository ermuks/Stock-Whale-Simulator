using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// uGUI 위의 작은 선 그래프(자산 곡선 같은 것). 점들을 꺾은선으로 잇고, 기준값(baseline) 위아래를 옅게 칠한다.
/// 기준값보다 높은 구간은 up 색, 낮은 구간은 down 색. 마지막 점은 작은 사각 점으로 강조한다.
/// 값은 SetPoints로 넣고, 그 범위에 맞춰 세로 눈금이 정해진다(기준값은 늘 범위 안에 들어간다).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UiLineChart : MaskableGraphic
{
    private readonly List<float> values = new(256);
    private float baseline;
    public Color up = new Color32(239, 89, 100, 255), down = new Color32(76, 150, 232, 255), guide = new Color32(42, 59, 69, 255);
    public float thickness = 2f, fillAlpha = .14f, dot = 5f;
    /// <summary>x는 0~1에서 이 몫까지만 쓴다(장이 다 끝나지 않았으면 선은 지금 시각에서 멈춘다).</summary>
    public float span = 1f;

    public void SetPoints(IReadOnlyList<float> points, float reference, float widthShare)
    {
        values.Clear(); for (int i = 0; i < points.Count; i++) { values.Add(points[i]); }
        baseline = reference; span = Mathf.Clamp01(widthShare); SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = GetPixelAdjustedRect();
        float min = baseline, max = baseline;
        foreach (float v in values) { if (v < min) { min = v; } if (v > max) { max = v; } }
        float pad = Mathf.Max((max - min) * .12f, Mathf.Abs(baseline) * .0005f, 1f);
        min -= pad; max += pad;
        float Y(float v) => rect.yMin + (v - min) / (max - min) * rect.height;
        // 기준선(점선 대신 얇은 선).
        float by = Y(baseline);
        Quad(vh, new Vector2(rect.xMin, by - .5f), new Vector2(rect.xMax, by - .5f), new Vector2(rect.xMax, by + .5f), new Vector2(rect.xMin, by + .5f), guide);
        if (values.Count < 2) { if (values.Count == 1) { Dot(vh, new Vector2(rect.xMin, Y(values[0])), values[0] >= baseline ? up : down); } return; }
        float step = rect.width * span / (values.Count - 1);
        for (int i = 1; i < values.Count; i++)
        {
            var a = new Vector2(rect.xMin + step * (i - 1), Y(values[i - 1])); var b = new Vector2(rect.xMin + step * i, Y(values[i]));
            bool above = (values[i - 1] + values[i]) * .5f >= baseline;
            var tone = above ? up : down;
            // 기준선까지의 옅은 면.
            var fill = new Color(tone.r, tone.g, tone.b, fillAlpha);
            Quad(vh, new Vector2(a.x, by), a, b, new Vector2(b.x, by), fill);
            // 선.
            var dir = (b - a).normalized; var n = new Vector2(-dir.y, dir.x) * (thickness * .5f);
            Quad(vh, a - n, a + n, b + n, b - n, tone);
        }
        var last = new Vector2(rect.xMin + step * (values.Count - 1), Y(values[values.Count - 1]));
        Dot(vh, last, values[values.Count - 1] >= baseline ? up : down);
    }
    private void Dot(VertexHelper vh, Vector2 at, Color tone)
    {
        float r = dot * .5f;
        Quad(vh, at + new Vector2(-r, -r), at + new Vector2(-r, r), at + new Vector2(r, r), at + new Vector2(r, -r), tone);
    }
    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;
        var vertex = UIVertex.simpleVert; vertex.color = color;
        vertex.position = a; vh.AddVert(vertex); vertex.position = b; vh.AddVert(vertex);
        vertex.position = c; vh.AddVert(vertex); vertex.position = d; vh.AddVert(vertex);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
