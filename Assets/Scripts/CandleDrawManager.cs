using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CandleDrawManager : MonoBehaviour
{
    [Header("Candle Data")]
    public List<CandleData> candles = new List<CandleData>();
    [Header("Chart Layout")]
    public float candleWidth = 0.6f;
    public float spacing = 0.2f;
    public float wickWidth = 0.1f;
    public float chartHeight = 10f;
    [Header("Volume Chart (캔들과 동일 너비·간격, 높이만 조절)")]
    [Tooltip("거래량 막대 영역 최대 높이")]
    public float volumeChartHeight = 3f;
    [Tooltip("거래량 막대 색 (Material 미사용 시)")]
    public Color volumeColor = new Color(0.4f, 0.5f, 0.6f, 0.8f);
    public Material materialVolume;
    [Header("Materials (캔들 상승/하락)")]
    [Tooltip("??? ??(?? ? ??)? ??")]
    public Material materialUp;
    [Tooltip("??? ??(?? < ??)? ??")]
    public Material materialDown;
    [Header("Colors (Material ??? ? ??)")]
    public Color upColor = Color.green;
    public Color downColor = Color.red;
    private Mesh mesh;
    private readonly List<Vector3> upVertices = new();
    private readonly List<Vector3> downVertices = new();
    private readonly List<Vector3> volVertices = new();
    private readonly List<Vector3> allVertices = new();
    private readonly List<Vector3> vertices = new();
    private readonly List<int> upTriangles = new();
    private readonly List<int> downTriangles = new();
    private readonly List<int> volTriangles = new();
    private readonly List<int> downTrianglesOffset = new();
    private readonly List<int> volTrianglesOffset = new();
    private readonly List<int> triangles = new();
    private readonly List<Color> colors = new();
    private readonly Material[] _materials = new Material[3];
    void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    public ChartPriceScale PriceScale { get; private set; }
    public int PlotCapacity { get; private set; } = 56;
    public int MaximumGridIntervals { get; private set; } = 10;
    private float rightPadding, timePadding;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private BoxCollider chartCollider;
    public float ChartWidth => Mathf.Max(1, PlotCapacity) * Mathf.Max(.001f, candleWidth + spacing);
    public float PlotWidth => Mathf.Max(ChartWidth * .5f, ChartWidth - rightPadding);
    public float CandleStep => PlotWidth / Mathf.Max(1, PlotCapacity);
    public float DrawnCandleWidth => CandleStep * candleWidth / Mathf.Max(.001f, candleWidth + spacing);
    private float DrawnWickWidth => CandleStep * wickWidth / Mathf.Max(.001f, candleWidth + spacing);
    public float VolumePlotHeight => Mathf.Max(.1f, volumeChartHeight - timePadding);

    public void ConfigurePlot(int capacity, float priceAxisPadding, float timeAxisPadding, int maxGridIntervals)
    {
        PlotCapacity = Mathf.Max(1, capacity); rightPadding = Mathf.Max(0f, priceAxisPadding);
        timePadding = Mathf.Max(0f, timeAxisPadding); MaximumGridIntervals = Mathf.Clamp(maxGridIntervals, 2, 12);
    }

    public bool TryCandleAt(float x, out int index) => ChartPriceScale.TryCandleIndex(x, CandleStep, candles == null ? 0 : candles.Count, out index);
    void Start()
    {
        GenerateMesh();
    }

#region Mesh Generation
    public void GenerateMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = "CandleMesh"
            };
            mesh.MarkDynamic();
            meshFilter = GetComponent<MeshFilter>(); meshRenderer = GetComponent<MeshRenderer>();
            chartCollider = GetComponent<BoxCollider>();
            if (chartCollider == null) { chartCollider = gameObject.AddComponent<BoxCollider>(); }
            meshFilter.sharedMesh = mesh;
        }

        mesh.Clear();
        if (candles == null || candles.Count == 0)
        {
            PriceScale = ChartPriceScale.Create(0d, 0d, MaximumGridIntervals);
            UpdateChartCollider();
            return;
        }

        PriceScale = ChartPriceScale.FromCandles(candles, MaximumGridIntervals);
        float maxVol = GetMaxVolume();
        bool useMaterials = materialUp != null && materialDown != null;
        if (useMaterials)
        {
            GenerateMeshWithMaterials(maxVol);
        }
        else
        {
            GenerateMeshWithColors(maxVol);
        }
    }

    float GetMaxVolume()
    {
        float m = 0f;
        foreach (var c in candles)
        {
            if (c.volume > m)
            {
                m = c.volume;
            }
        }

        return m > 0f ? m : 1f;
    }

    void GenerateMeshWithMaterials(float maxVolume)
    {
        upVertices.Clear();
        upTriangles.Clear();
        downVertices.Clear();
        downTriangles.Clear();
        volVertices.Clear();
        volTriangles.Clear();
        float xOffset = 0f;
        int upIdx = 0, downIdx = 0, volIdx = 0;
        foreach (var c in candles)
        {
            float open = NormalizePrice(c.open);
            float close = NormalizePrice(c.close);
            float high = NormalizePrice(c.high);
            float low = NormalizePrice(c.low);
            bool isUp = close >= open;
            float bodyTop = Mathf.Max(open, close);
            float bodyBottom = Mathf.Min(open, close);
            ApplyMinBodyHeight(ref bodyBottom, ref bodyTop);
            float left = xOffset;
            float right = xOffset + DrawnCandleWidth;
            float wickLeft = left + DrawnCandleWidth * 0.5f - DrawnWickWidth * 0.5f;
            float wickRight = wickLeft + DrawnWickWidth;
            if (isUp)
            {
                AddQuadVertices(upVertices, upTriangles, new Vector3(left, bodyBottom, 0), new Vector3(right, bodyBottom, 0), new Vector3(right, bodyTop, 0), new Vector3(left, bodyTop, 0), ref upIdx);
                AddQuadVertices(upVertices, upTriangles, new Vector3(wickLeft, low, 0), new Vector3(wickRight, low, 0), new Vector3(wickRight, high, 0), new Vector3(wickLeft, high, 0), ref upIdx);
            }
            else
            {
                AddQuadVertices(downVertices, downTriangles, new Vector3(left, bodyBottom, 0), new Vector3(right, bodyBottom, 0), new Vector3(right, bodyTop, 0), new Vector3(left, bodyTop, 0), ref downIdx);
                AddQuadVertices(downVertices, downTriangles, new Vector3(wickLeft, low, 0), new Vector3(wickRight, low, 0), new Vector3(wickRight, high, 0), new Vector3(wickLeft, high, 0), ref downIdx);
            }

            float volBarH = maxVolume > 0f ? (c.volume / maxVolume) * VolumePlotHeight : 0f;
            float volBottom = -VolumePlotHeight;
            float volTop = volBottom + volBarH;
            AddQuadVertices(volVertices, volTriangles, new Vector3(left, volBottom, 0), new Vector3(right, volBottom, 0), new Vector3(right, volTop, 0), new Vector3(left, volTop, 0), ref volIdx);
            xOffset += CandleStep;
        }

        allVertices.Clear();
        allVertices.AddRange(upVertices);
        allVertices.AddRange(downVertices);
        allVertices.AddRange(volVertices);
        downTrianglesOffset.Clear();
        foreach (int i in downTriangles)
        {
            downTrianglesOffset.Add(i + upVertices.Count);
        }

        int volBase = upVertices.Count + downVertices.Count;
        volTrianglesOffset.Clear();
        foreach (int i in volTriangles)
        {
            volTrianglesOffset.Add(i + volBase);
        }

        mesh.SetVertices(allVertices);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(upTriangles, 0);
        mesh.SetTriangles(downTrianglesOffset, 1);
        mesh.SetTriangles(volTrianglesOffset, 2);
        mesh.RecalculateBounds();
        UpdateChartCollider();
        _materials[0] = materialUp;
        _materials[1] = materialDown;
        _materials[2] = materialVolume != null ? materialVolume : materialDown;
        meshRenderer.sharedMaterials = _materials;
    }

    void AddQuadVertices(List<Vector3> vertices, List<int> triangles, Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, ref int vertexIndex)
    {
        vertices.Add(bl);
        vertices.Add(br);
        vertices.Add(tr);
        vertices.Add(tl);
        triangles.Add(vertexIndex + 0);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 0);
        triangles.Add(vertexIndex + 3);
        triangles.Add(vertexIndex + 2);
        vertexIndex += 4;
    }

    void GenerateMeshWithColors(float maxVolume)
    {
        vertices.Clear();
        triangles.Clear();
        colors.Clear();
        float xOffset = 0f;
        int vertexIndex = 0;
        foreach (var c in candles)
        {
            float open = NormalizePrice(c.open);
            float close = NormalizePrice(c.close);
            float high = NormalizePrice(c.high);
            float low = NormalizePrice(c.low);
            bool isUp = close >= open;
            Color bodyColor = isUp ? upColor : downColor;
            float bodyTop = Mathf.Max(open, close);
            float bodyBottom = Mathf.Min(open, close);
            ApplyMinBodyHeight(ref bodyBottom, ref bodyTop);
            float left = xOffset;
            float right = xOffset + DrawnCandleWidth;
            float wickLeft = left + DrawnCandleWidth * 0.5f - DrawnWickWidth * 0.5f;
            float wickRight = wickLeft + DrawnWickWidth;
            AddQuad(vertices, triangles, colors, new Vector3(left, bodyBottom, 0), new Vector3(right, bodyBottom, 0), new Vector3(right, bodyTop, 0), new Vector3(left, bodyTop, 0), bodyColor, ref vertexIndex);
            AddQuad(vertices, triangles, colors, new Vector3(wickLeft, low, 0), new Vector3(wickRight, low, 0), new Vector3(wickRight, high, 0), new Vector3(wickLeft, high, 0), bodyColor, ref vertexIndex);
            float volBarH = maxVolume > 0f ? (c.volume / maxVolume) * VolumePlotHeight : 0f;
            float volBottom = -VolumePlotHeight;
            float volTop = volBottom + volBarH;
            AddQuad(vertices, triangles, colors, new Vector3(left, volBottom, 0), new Vector3(right, volBottom, 0), new Vector3(right, volTop, 0), new Vector3(left, volTop, 0), volumeColor, ref vertexIndex);
            xOffset += CandleStep;
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
        mesh.RecalculateBounds();
        UpdateChartCollider();
    }

    void AddQuad(List<Vector3> vertices, List<int> triangles, List<Color> colors, Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Color color, ref int vertexIndex)
    {
        vertices.Add(bl);
        vertices.Add(br);
        vertices.Add(tr);
        vertices.Add(tl);
        triangles.Add(vertexIndex + 0);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 0);
        triangles.Add(vertexIndex + 3);
        triangles.Add(vertexIndex + 2);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        vertexIndex += 4;
    }

    /// <summary>차트 영역에 BoxCollider 설정. Raycast 드래그용. 캔들 없을 때도 최소 크기로 두어 드래그가 잡히게 함.</summary>
    void UpdateChartCollider()
    {
        float totalH = chartHeight + volumeChartHeight;
        chartCollider.size = new Vector3(Mathf.Max(0.1f, ChartWidth), Mathf.Max(0.1f, totalH), 0.1f);
        chartCollider.center = new Vector3(ChartWidth * 0.5f, (chartHeight - volumeChartHeight) * 0.5f, 0f);
    }

#endregion
#region Price Normalize
    float NormalizePrice(float price)
    {
        return (float)PriceScale.Normalize(price) * chartHeight;
    }

    /// <summary>봉(몸통)이 선(심지)보다 얇아지지 않게, 시가=종가여도 최소 두께로 보이게 함.</summary>
    void ApplyMinBodyHeight(ref float bodyBottom, ref float bodyTop)
    {
        float minHeight = wickWidth;
        float height = bodyTop - bodyBottom;
        if (height >= minHeight)
        { return; }

        float mid = (bodyTop + bodyBottom) * 0.5f;
        float half = minHeight * 0.5f;
        bodyBottom = mid - half;
        bodyTop = mid + half;
    }

#endregion
}
