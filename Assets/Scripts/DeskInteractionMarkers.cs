using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장치 다섯 개, 서랍, 휴대폰의 상호작용 표시를 하나의 UI 메시로 그린다. 입력을 가로채지 않는다.
/// 평소에는 아무것도 그리지 않는다(늘 떠 있으면 화면의 글자를 가린다).
///  - 화면 가운데가 그 물건에 조금 가까워지면: 반투명한 테두리 원.
///  - 상호작용 키를 누를 수 있는 상태가 되면: 은은하게 빛나는 꽉 찬 원(숨 쉬듯 밝아지고, 바깥으로 물결이 퍼진다).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DeskInteractionMarkers : MaskableGraphic
{
    /// <summary>원의 수: 장치 다섯 개(DeskViewTarget의 차례), 서랍, 휴대폰.</summary>
    public const int Count = 7;
    public const int Drawer = 5;
    public const int Phone = 6;
    private readonly Vector2[] points = new Vector2[Count];
    private readonly bool[] visible = new bool[Count], selected = new bool[Count];
    /// <summary>가까운 정도(0 멀다 ~ 1 가깝다). 테두리 원의 진하기가 된다.</summary>
    private readonly float[] near = new float[Count];
    /// <summary>지금 그려지는 값. 목표를 향해 부드럽게 따라간다.</summary>
    private readonly float[] ring = new float[Count], fill = new float[Count];
    private bool showAim;
    private Vector2 aim;
    private const int Segments = 48;
    private const float RingSeconds = .16f, FillSeconds = .14f, PulseSeconds = 1.7f;
    private static readonly Color Core = new Color(1f, 1f, 1f, 1f), Glow = new Color(1f, .95f, .80f, 1f);

    protected override void Awake()
    { base.Awake(); raycastTarget = false; maskable = false; }

    /// <summary>
    /// 표시 하나의 상태를 정한다. shown: 화면 안에 있는가. isSelected: 지금 상호작용 키를 누르면 이것이 반응하는가.
    /// closeness: 화면 가운데(조준점)가 얼마나 가까운가(0~1).
    /// </summary>
    public void SetMarker(int index, Vector2 position, bool shown, bool isSelected, float closeness = 1f)
    {
        // 사라지는 동안에는 마지막 자리에 그대로 두고 흐려지게 한다.
        if (shown) { points[index] = position; }
        visible[index] = shown; selected[index] = shown && isSelected; near[index] = shown ? Mathf.Clamp01(closeness) : 0f;
    }

    public void SetAim(Vector2 point, bool shown)
    {
        if (showAim == shown && (!shown || (aim - point).sqrMagnitude < .04f))
        { return; }
        aim = point; showAim = shown; SetVerticesDirty();
    }

    void Update()
    {
        bool moved = false, glowing = false;
        float step = Time.unscaledDeltaTime;
        for (int i = 0; i < Count; i++)
        {
            float ringTarget = selected[i] ? 1f : near[i], fillTarget = selected[i] ? 1f : 0f;
            float nextRing = Mathf.MoveTowards(ring[i], ringTarget, step / RingSeconds), nextFill = Mathf.MoveTowards(fill[i], fillTarget, step / FillSeconds);
            if (!Mathf.Approximately(nextRing, ring[i]) || !Mathf.Approximately(nextFill, fill[i])) { moved = true; }
            ring[i] = nextRing; fill[i] = nextFill;
            if (fill[i] > 0f) { glowing = true; }
        }
        // 빛나는 원은 계속 움직이므로 매 프레임 다시 그린다. 테두리 원만 있을 때는 자리가 바뀔 수 있으니 역시 다시 그린다.
        if (moved || glowing || AnyRing()) { SetVerticesDirty(); }
    }
    private bool AnyRing()
    {
        for (int i = 0; i < Count; i++) { if (ring[i] > 0f) { return true; } }
        return false;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float beat = Mathf.Repeat(Time.unscaledTime / PulseSeconds, 1f), breath = .5f + .5f * Mathf.Sin(beat * Mathf.PI * 2f);
        for (int i = 0; i < Count; i++)
        {
            float shown = ring[i], filled = fill[i] * fill[i] * (3f - 2f * fill[i]);
            if (shown <= 0f && filled <= 0f)
            { continue; }
            Vector2 center = points[i];
            // 테두리 원: 가까워질수록 진해지고, 꽉 찬 원으로 바뀌는 동안에는 물러난다.
            float outline = shown * (1f - filled);
            if (outline > .01f)
            { Ring(vh, center, 7.4f, 9f, Tint(Core, .42f * outline), Tint(Core, .42f * outline)); }
            if (filled <= .01f)
            { continue; }
            float radius = Mathf.Lerp(7f, 11.5f, filled) + breath * .6f;
            // 번지는 빛: 원의 가장자리에서 바깥으로 흐려진다. 숨 쉬듯 조금 커졌다 작아진다.
            Ring(vh, center, radius, radius + 13f + breath * 5f, Tint(Glow, (.26f + .14f * breath) * filled), Tint(Glow, 0f));
            // 꽉 찬 원: 가운데가 더 밝다. 반투명이라 뒤의 화면이 비친다.
            Ring(vh, center, 0f, radius, Tint(Core, (.70f + .10f * breath) * filled), Tint(Core, .40f * filled));
            // 밝은 가장자리 한 줄.
            Ring(vh, center, radius - 1.3f, radius, Tint(Core, .55f * filled), Tint(Core, .55f * filled));
            // 물결: 가장자리에서 시작해 바깥으로 퍼지며 사라진다.
            float wave = radius + beat * 20f, fade = (1f - beat) * (1f - beat) * .38f * filled;
            Ring(vh, center, wave, wave + 1.6f, Tint(Glow, fade), Tint(Glow, fade));
        }
        if (showAim)
        { Ring(vh, aim, 0f, 1.6f, Tint(Core, .32f), Tint(Core, .32f)); }
    }

    private static Color Tint(Color color, float alpha)
    { color.a = alpha; return color; }

    /// <summary>안쪽 반지름에서 바깥 반지름까지의 고리(안쪽이 0이면 원판). 안과 밖의 색을 따로 주어 번지는 빛을 만든다.</summary>
    private static void Ring(VertexHelper vh, Vector2 center, float inner, float outer, Color innerTint, Color outerTint)
    {
        if (innerTint.a <= 0f && outerTint.a <= 0f)
        { return; }
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments, b = (i + 1) * Mathf.PI * 2f / Segments;
            Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), q = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            int start = vh.currentVertCount;
            vh.AddVert(center + p * inner, innerTint, Vector2.zero); vh.AddVert(center + p * outer, outerTint, Vector2.zero);
            vh.AddVert(center + q * outer, outerTint, Vector2.zero); vh.AddVert(center + q * inner, innerTint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
