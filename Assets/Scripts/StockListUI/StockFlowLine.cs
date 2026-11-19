using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 종목 목록 한 행의 흐름 선. 오늘 하루의 값을 선 하나로 그린다.
// 가운데 가로선이 전일 종가다. 그 위에 있는 구간은 상승 색, 아래에 있는 구간은 하락 색이다. 기준선을 넘는 자리에서 색이 바뀐다.
// 글자는 없다. 한눈에 "오늘 어떻게 흘렀나"만 본다.
// (직접 만든 Graphic에는 CanvasRenderer가 저절로 붙지 않는다. 없으면 아무것도 그려지지 않는다.)
[RequireComponent(typeof(CanvasRenderer))]
public sealed class StockFlowLine : MaskableGraphic
{
    private readonly List<Vector2> points = new(96);
    private float thickness = 4f;
    /// <summary>확인용: 지금 그리고 있는 점의 수.</summary>
    public int PointCount => points.Count;
    private static readonly Color32 Baseline = new(120, 134, 146, 150);

    /// <summary>점들은 0~1로 고른 좌표다(x: 장 시작~마감, y: 0.5가 전일 종가).</summary>
    public void Show(List<Vector2> normalized, float lineThickness)
    {
        points.Clear();
        if (normalized != null) { points.AddRange(normalized); }
        thickness = lineThickness;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) { return; }
        Vector2 At(Vector2 p) => new Vector2(rect.xMin + p.x * rect.width, rect.yMin + p.y * rect.height);
        // 기준선(전일 종가).
        Segment(vh, At(new Vector2(0f, .5f)), At(new Vector2(1f, .5f)), Mathf.Max(2f, thickness * .4f), Baseline);
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = points[i - 1], b = points[i];
            bool aUp = a.y >= .5f, bUp = b.y >= .5f;
            if (aUp == bUp || Mathf.Approximately(a.y, b.y)) { Segment(vh, At(a), At(b), thickness, aUp ? MarketColors.Up : MarketColors.Down); continue; }
            // 기준선을 넘는 구간은 넘는 자리에서 둘로 나눠 색을 바꾼다.
            float t = (.5f - a.y) / (b.y - a.y);
            Vector2 cross = new Vector2(a.x + (b.x - a.x) * t, .5f);
            Segment(vh, At(a), At(cross), thickness, aUp ? MarketColors.Up : MarketColors.Down);
            Segment(vh, At(cross), At(b), thickness, bUp ? MarketColors.Up : MarketColors.Down);
        }
    }

    private static void Segment(VertexHelper vh, Vector2 from, Vector2 to, float width, Color32 color)
    {
        Vector2 along = to - from;
        if (along.sqrMagnitude < .0001f) { return; }
        Vector2 side = new Vector2(-along.y, along.x).normalized * (width * .5f);
        // 꺾이는 자리가 끊겨 보이지 않게 양 끝을 굵기의 절반만큼 늘인다.
        Vector2 cap = along.normalized * (width * .5f);
        from -= cap; to += cap;
        int index = vh.currentVertCount;
        vh.AddVert(from - side, color, Vector2.zero); vh.AddVert(from + side, color, Vector2.zero);
        vh.AddVert(to + side, color, Vector2.zero); vh.AddVert(to - side, color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
    }
}
