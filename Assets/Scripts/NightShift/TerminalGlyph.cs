using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One generated PNG Image per symbol, plus bounded ring meshes. Only visible overheated gauges animate.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TerminalGlyph : MaskableGraphic
{
    public enum Symbol { Eye, Trade, People, Crown, Shield, BrokenShield, Coin, Warning, Bolt, Pulse, Wallet, Check }
    public Symbol symbol;
    public Image iconImage;
    public bool ring, heatColors = true, animateHeat = true;
    [SerializeField] private float value;
    public float Value => value;
    public bool Overheated => ring && heatColors && value > 100f;
    public int MeshBuilds { get; private set; }
    private Coroutine pulse;
    private readonly WaitForSecondsRealtime pulseDelay = new(.08f);
    private static readonly Color Ink = new Color32(230,235,239,255), Track = new Color32(51,64,70,255), Accent = new Color32(37,175,192,255);
    public void SetTint(Color tint)
    {
        if (color != tint) { color = tint; }
        if (iconImage != null && iconImage.color != tint) { iconImage.color = tint; }
    }
    public static Color BandColor(float amount)
    {
        if (amount >= 75f)
        { return new Color32(239,89,100,255); }
        if (amount >= 50f)
        { return new Color32(235,148,78,255); }
        if (amount >= 25f)
        { return new Color32(231,202,106,255); }
        return Ink;
    }
    public static string BandName(float amount) => amount > 100f ? "과열" : amount >= 75f ? "높음" : amount >= 50f ? "중간" : amount >= 25f ? "낮음" : "매우 낮음";
    public void SetValue(float amount)
    {
        amount = float.IsNaN(amount) || float.IsInfinity(amount) ? 0f : Mathf.Max(0f,amount);
        bool visualChange = Mathf.RoundToInt(Mathf.Min(value,200f) * 2f) != Mathf.RoundToInt(Mathf.Min(amount,200f) * 2f) || heatColors && BandColor(value) != BandColor(amount) || (value > 100f) != (amount > 100f);
        value = amount;
        if (visualChange) { SetVerticesDirty(); }
        UpdatePulse();
    }
    protected override void OnEnable()
    { base.OnEnable(); SetTint(color); UpdatePulse(); }
    protected override void OnDisable()
    {
        if (pulse != null) { StopCoroutine(pulse); pulse = null; }
        base.OnDisable();
    }
    private void UpdatePulse()
    {
        bool play = Application.isPlaying && isActiveAndEnabled && animateHeat && Overheated;
        if (play && pulse == null) { pulse = StartCoroutine(Breathe()); }
        else if (!play && pulse != null) { StopCoroutine(pulse); pulse = null; }
    }
    private IEnumerator Breathe()
    {
        while (Overheated && animateHeat)
        {
            if (!canvasRenderer.cull) { SetVerticesDirty(); }
            yield return pulseDelay;
        }
        pulse = null;
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); MeshBuilds++;
        var rect = GetPixelAdjustedRect(); float size = Mathf.Min(rect.width,rect.height);
        if (size <= 0f)
        { return; }
        Vector2 center = rect.center;
        if (ring)
        {
            Arc(mesh,center,size,.42f,.042f,0,1,Track * color);
            Color fill = (heatColors ? BandColor(value) : Accent) * color;
            Arc(mesh,center,size,.42f,.045f,0,Mathf.Clamp01(value / 100f),fill);
            if (Overheated)
            {
                float intensity = Mathf.Clamp01((value - 100f) / 100f);
                float wave = animateHeat ? .5f + .5f * Mathf.Sin(Time.unscaledTime * (2f + intensity * .8f) + (int)symbol * .6f) : .5f;
                fill.a *= .22f + wave * .18f;
                Arc(mesh,center,size,.467f + wave * (.004f + intensity * .008f),.011f + intensity * .004f,0,1,fill);
            }
        }
    }
    private static void Arc(VertexHelper mesh,Vector2 center,float size,float radius,float thickness,float begin,float fraction,Color tint)
    {
        int segments = Mathf.CeilToInt(fraction * 48f);
        if (segments == 0)
        { return; }
        for (int i = 0; i < segments; i++)
        {
            float a = (begin + fraction * i / segments) * Mathf.PI * 2f, b = (begin + fraction * (i+1) / segments) * Mathf.PI * 2f;
            Vector2 va = new(Mathf.Sin(a),Mathf.Cos(a)), vb = new(Mathf.Sin(b),Mathf.Cos(b));
            Quad(mesh,center+va*((radius-thickness*.5f)*size),center+va*((radius+thickness*.5f)*size),center+vb*((radius+thickness*.5f)*size),center+vb*((radius-thickness*.5f)*size),tint);
        }
    }
    private static void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a,tint,Vector2.zero); mesh.AddVert(b,tint,Vector2.zero); mesh.AddVert(c,tint,Vector2.zero); mesh.AddVert(d,tint,Vector2.zero);
        mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
    }
}
