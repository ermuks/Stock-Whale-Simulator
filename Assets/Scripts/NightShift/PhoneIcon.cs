using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 휴대폰 화면의 작은 그림 기호. 그림 파일 없이 선과 원으로 그린다(확대해도 또렷하다).
/// 색은 Graphic의 color를 그대로 쓴다. 입력을 가로채지 않는다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PhoneIcon : MaskableGraphic
{
    public enum Kind
    {
        /// <summary>오르는 꺾은선과 화살표: 종목.</summary>
        Trend,
        /// <summary>시계: 작전 시간.</summary>
        Clock,
        /// <summary>봉투: 문자, 받은 시간.</summary>
        Envelope,
        /// <summary>오른쪽 꺾쇠: 들어가기.</summary>
        Chevron,
        /// <summary>왼쪽 꺾쇠: 돌아가기.</summary>
        Back,
        /// <summary>꽉 찬 점: 안 읽은 문자.</summary>
        Dot,
        /// <summary>사람: 보낸 사람.</summary>
        Person,
        /// <summary>체크: 읽은 문자.</summary>
        Check,
    }
    [SerializeField] private Kind kind;
    private const float Stroke = .17f;
    private const int Segments = 28;
    public Kind Shape
    {
        get => kind;
        set { if (kind != value) { kind = value; SetVerticesDirty(); } }
    }

    protected override void Awake()
    { base.Awake(); raycastTarget = false; }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        // 그림은 -1~1의 네모 안에 그리고, 칸의 짧은 쪽에 맞춰 가운데에 놓는다.
        float half = Mathf.Min(rect.width, rect.height) * .5f;
        Vector2 center = rect.center;
        switch (kind)
        {
            case Kind.Trend:
                Path(vh, center, half, new Vector2(-.92f, -.55f), new Vector2(-.32f, .05f), new Vector2(.08f, -.28f), new Vector2(.82f, .52f));
                Path(vh, center, half, new Vector2(.34f, .56f), new Vector2(.86f, .56f), new Vector2(.86f, .04f));
                break;
            case Kind.Clock:
                Arc(vh, center, half, Vector2.zero, .90f - Stroke, .90f, 0f, 360f);
                Path(vh, center, half, new Vector2(0f, .52f), Vector2.zero, new Vector2(.40f, -.22f));
                break;
            case Kind.Envelope:
                Path(vh, center, half, new Vector2(-.94f, .62f), new Vector2(.94f, .62f), new Vector2(.94f, -.62f), new Vector2(-.94f, -.62f), new Vector2(-.94f, .62f));
                Path(vh, center, half, new Vector2(-.90f, .56f), new Vector2(0f, -.06f), new Vector2(.90f, .56f));
                break;
            case Kind.Chevron:
                Path(vh, center, half, new Vector2(-.30f, .62f), new Vector2(.32f, 0f), new Vector2(-.30f, -.62f));
                break;
            case Kind.Back:
                Path(vh, center, half, new Vector2(.30f, .62f), new Vector2(-.32f, 0f), new Vector2(.30f, -.62f));
                break;
            case Kind.Dot:
                Arc(vh, center, half, Vector2.zero, 0f, .80f, 0f, 360f);
                break;
            case Kind.Check:
                Path(vh, center, half, new Vector2(-.70f, -.02f), new Vector2(-.22f, -.50f), new Vector2(.72f, .48f));
                break;
            case Kind.Person:
                Arc(vh, center, half, new Vector2(0f, .42f), 0f, .40f, 0f, 360f);
                Arc(vh, center, half, new Vector2(0f, -.86f), 0f, .78f, 0f, 180f);
                break;
        }
    }

    /// <summary>점들을 잇는 굵은 선. 꺾이는 자리와 양 끝은 둥글게 메운다.</summary>
    private void Path(VertexHelper vh, Vector2 center, float half, params Vector2[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Arc(vh, center, half, points[i], 0f, Stroke * .5f, 0f, 360f, 10);
            if (i == 0) { continue; }
            Vector2 from = points[i - 1], to = points[i], along = to - from;
            if (along.sqrMagnitude <= 0f) { continue; }
            Vector2 side = new Vector2(-along.y, along.x).normalized * (Stroke * .5f);
            int start = vh.currentVertCount;
            vh.AddVert(center + (from - side) * half, color, Vector2.zero); vh.AddVert(center + (from + side) * half, color, Vector2.zero);
            vh.AddVert(center + (to + side) * half, color, Vector2.zero); vh.AddVert(center + (to - side) * half, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    /// <summary>고리의 한 토막(안쪽 반지름이 0이면 부채꼴). 각도는 도, 반시계 방향.</summary>
    private void Arc(VertexHelper vh, Vector2 center, float half, Vector2 origin, float inner, float outer, float fromDegrees, float toDegrees, int segments = Segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Lerp(fromDegrees, toDegrees, i / (float)segments) * Mathf.Deg2Rad, b = Mathf.Lerp(fromDegrees, toDegrees, (i + 1) / (float)segments) * Mathf.Deg2Rad;
            Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), q = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            int start = vh.currentVertCount;
            vh.AddVert(center + (origin + p * inner) * half, color, Vector2.zero); vh.AddVert(center + (origin + p * outer) * half, color, Vector2.zero);
            vh.AddVert(center + (origin + q * outer) * half, color, Vector2.zero); vh.AddVert(center + (origin + q * inner) * half, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
