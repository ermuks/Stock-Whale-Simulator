using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 캔들 종가를 표시하는 작은 uGUI 그래프. UI 영역 밖으로 그리지 않는다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DevicePriceGraph : MaskableGraphic
{
    private readonly float[] values = new float[40];
    private int count;
    public void SetStock(Stock stock)
    {
        var history = CandleHistoryManager.Instance;
        if (history == null || stock == null)
        { count = 0; SetVerticesDirty(); return; }
        var data = history.Completed(stock);
        int length = Mathf.Min(39, data.Count);
        bool changed = count != length + 1;
        for (int i = 0; i <= length; i++)
        {
            float value = i == length ? (float)stock.currentPrice : (float)data[data.Count - length + i].ClosePrice;
            changed |= values[i] != value; values[i] = value;
        }
        count = length + 1;
        if (changed)
        { SetVerticesDirty(); }
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (count < 2)
        { return; }
        Rect rect = rectTransform.rect;
        float low = values[0], high = values[0];
        for (int i = 1; i < count; i++)
        { low = Mathf.Min(low, values[i]); high = Mathf.Max(high, values[i]); }
        float range = Mathf.Max(1f, high - low);
        for (int i = 1; i < count; i++)
        {
            Vector2 a = new(rect.xMin + 4f + (rect.width - 8f) * (i - 1) / (count - 1), rect.yMin + 5f + (rect.height - 10f) * (values[i - 1] - low) / range);
            Vector2 b = new(rect.xMin + 4f + (rect.width - 8f) * i / (count - 1), rect.yMin + 5f + (rect.height - 10f) * (values[i] - low) / range);
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * 2.8f;
            int start = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero); mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero); mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
