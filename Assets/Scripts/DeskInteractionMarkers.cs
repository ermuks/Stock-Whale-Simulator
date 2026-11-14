using UnityEngine;
using UnityEngine.UI;

/// <summary>장치 다섯 개의 반투명 원을 하나의 UI 메시로 그린다. 입력을 가로채지 않는다.</summary>
public sealed class DeskInteractionMarkers : MaskableGraphic
{
    private readonly Vector2[] points = new Vector2[5];
    private readonly bool[] visible = new bool[5];
    private int hovered = -1;
    private bool showAim;
    private Vector2 aim;
    private const int Segments = 40;

    protected override void Awake()
    { base.Awake(); raycastTarget = false; maskable = false; }

    public void SetMarker(int index, Vector2 position, bool shown, bool selected)
    {
        int next = selected ? index : hovered == index ? -1 : hovered;
        if (visible[index] == shown && (!shown || (points[index] - position).sqrMagnitude < .04f) && hovered == next)
        { return; }
        points[index] = position; visible[index] = shown; hovered = next; SetVerticesDirty();
    }

    public void SetAim(Vector2 point, bool shown)
    {
        if (showAim == shown && (!shown || (aim - point).sqrMagnitude < .04f))
        { return; }
        aim = point; showAim = shown; SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        for (int i = 0; i < points.Length; i++)
        {
            if (!visible[i])
            { continue; }
            if (hovered == i)
            { Circle(vh, points[i], 13f, 0f, new Color(1f, 1f, 1f, .46f)); }
            else
            { Circle(vh, points[i], 8f, 6.8f, new Color(1f, 1f, 1f, .32f)); }
        }
        if (showAim)
        { Circle(vh, aim, 1.6f, 0f, new Color(1f, 1f, 1f, .32f)); }
    }

    private static void Circle(VertexHelper vh, Vector2 center, float outer, float inner, Color tint)
    {
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments, b = (i + 1) * Mathf.PI * 2f / Segments;
            Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), q = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            int start = vh.currentVertCount;
            vh.AddVert(center + p * inner, tint, Vector2.zero); vh.AddVert(center + p * outer, tint, Vector2.zero);
            vh.AddVert(center + q * outer, tint, Vector2.zero); vh.AddVert(center + q * inner, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
