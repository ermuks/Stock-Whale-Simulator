using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 해금 항목이나 작전 도구를 골랐을 때 보여 주는 작은 움직이는 그림. 글로 길게 설명하는 대신, 쓰면 차트에 무슨 일이 생기는지를 보여 준다.
/// 값의 흐름이 왼쪽에서 오른쪽으로 그려지고(몇 초마다 되풀이), 그 도구가 만드는 변화가 색으로 드러난다:
/// 오르는 구간은 빨강, 내리는 구간은 파랑, 내가 얻는 정보나 내가 한 일은 청록과 주황.
/// 그림 파일 없이 선과 네모로 그린다. 보여 주기만 한다(시세·계좌와 무관한 예시 그림이다).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MiniPreview : MaskableGraphic
{
    public enum Kind { None, CrowdValue, RealValue, NightTip, NightPlant, NightTease, Dawn, Found, Close, Ignite, Slam, Wash, Wall }
    private const float Loop = 4.4f, DrawShare = .7f;
    private const int Points = 56, Bars = 26;
    private static readonly Color Ink = new Color(.78f, .82f, .86f), Rise = new Color(.96f, .34f, .34f), Fall = new Color(.32f, .56f, .98f);
    private static readonly Color Teal = new Color(.24f, .90f, .82f), Amber = new Color(1f, .74f, .30f), Dim = new Color(1f, 1f, 1f, .10f);
    private Kind kind;
    private float started;
    private TextMeshProUGUI first, second;
    private int locale = -1;

    /// <summary>parent를 가득 채우는 프리뷰를 만든다. 글자 크기는 그 화면의 단위로 준다.</summary>
    public static MiniPreview Create(string name, Transform parent, TMP_FontAsset font, float fontSize)
    {
        var item = new GameObject("Image :: " + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(MiniPreview)) { layer = parent.gameObject.layer };
        var rect = (RectTransform)item.transform; rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var view = item.GetComponent<MiniPreview>(); view.raycastTarget = false;
        view.first = Note("Text :: Preview Note A", rect, font, fontSize); view.second = Note("Text :: Preview Note B", rect, font, fontSize);
        return view;
    }
    private static TextMeshProUGUI Note(string name, RectTransform parent, TMP_FontAsset font, float size)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)) { layer = parent.gameObject.layer };
        var label = item.GetComponent<TextMeshProUGUI>(); label.rectTransform.SetParent(parent, false);
        label.rectTransform.pivot = new Vector2(0f, .5f); label.rectTransform.sizeDelta = new Vector2(size * 22f, size * 1.7f);
        if (font != null) { label.font = font; }
        label.fontSize = size; label.enableAutoSizing = false; label.raycastTarget = false; label.richText = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow; label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }

    /// <summary>보여 줄 그림을 바꾼다. 같은 그림이면 그대로 둔다(처음부터 다시 그리지 않는다).</summary>
    public void Show(Kind next)
    {
        if (kind == next && locale == GameLocale.Revision) { return; }
        if (kind != next) { started = Time.unscaledTime; }
        kind = next; locale = GameLocale.Revision; WriteNotes(); SetVerticesDirty();
    }
    void Update()
    {
        if (kind == Kind.None) { return; }
        if (locale != GameLocale.Revision) { locale = GameLocale.Revision; WriteNotes(); }
        SetVerticesDirty();
    }

    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    private void Place(TextMeshProUGUI label, string text, float x, float y, Color tint)
    {
        if (label == null) { return; }
        bool shown = !string.IsNullOrEmpty(text);
        if (label.gameObject.activeSelf != shown) { label.gameObject.SetActive(shown); }
        if (!shown) { return; }
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(x, y); label.rectTransform.anchoredPosition = Vector2.zero;
        label.color = tint; GameLocalizedText.Set(label, text, true);
    }
    /// <summary>그림마다 짧은 이름표 한두 개만 붙인다.</summary>
    private void WriteNotes()
    {
        string a = "", b = ""; float ax = .04f, ay = .91f, bx = .58f, by = .91f; Color at = Ink, bt = Ink;
        switch (kind)
        {
            case Kind.CrowdValue: a = Word("개미들이 믿는 가격", "What the crowd believes", "個人が信じる値段"); ay = Level(.74f) + .07f; at = Teal; b = Word("지금 값", "Price now", "今の値"); bx = .04f; by = Level(.30f) - .09f; break;
            case Kind.RealValue: a = Word("회사의 진짜 가치", "Real value", "本当の価値"); ay = Level(.32f) - .08f; at = Amber; b = Word("거품", "Bubble", "バブル"); bx = .80f; by = Level(.58f); bt = Rise; break;
            case Kind.NightTip: a = Word("오늘 밤 미리 읽기", "Read it tonight", "今夜先に読む"); b = Word("내일 · 호재 기사", "Tomorrow · good news", "明日 · 好材料"); bt = Teal; break;
            case Kind.NightPlant: a = Word("오늘 밤 기사 맡기기", "Order a story tonight", "今夜記事を手配"); b = Word("내일 · 내가 정한 기사", "Tomorrow · your story", "明日 · 自分の記事"); bt = Teal; break;
            case Kind.NightTease: a = Word("오늘 밤 종목 예고", "Tease a stock tonight", "今夜銘柄を予告"); b = Word("내일 · 회원들이 삽니다", "Tomorrow · members buy", "明日 · 会員が買う"); bt = Teal; break;
            case Kind.Dawn: a = Word("세력의 매수가 보입니다", "You see the whale buying", "仕手筋の買いが見える"); at = Amber; b = Word("그 뒤에 오릅니다", "Then it rises", "その後に上がる"); bx = .62f; by = Level(.52f); bt = Rise; break;
            case Kind.Found: a = Word("매일 들어오는 사업 수입", "Business income every day", "毎日入る事業収入"); at = Teal; break;
            case Kind.Close: a = Word("마감 직전", "Just before the close", "引けの直前"); ax = .70f; at = Teal; break;
            case Kind.Ignite: a = Word("한 번에 삽니다", "One big buy", "一気に買う"); ax = .52f; ay = Level(.50f); at = Rise; break;
            case Kind.Slam: a = Word("한 번에 팝니다", "One big sell", "一気に売る"); ax = .52f; ay = Level(.52f); at = Fall; break;
            case Kind.Wash: a = Word("거래량만 늘어납니다", "Only volume grows", "出来高だけ増える"); ax = .30f; ay = .30f; at = Teal; break;
            case Kind.Wall: a = Word("내 매수벽", "Your bid wall", "自分の買い壁"); ax = .16f; ay = Level(.34f) - .075f; at = Teal; break;
        }
        Place(first, a, ax, ay, at); Place(second, b, bx, by, bt);
    }

    // ── 예시 흐름 ────────────────────────────────────────────────────────────
    private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    /// <summary>값(0~1)을 그림 안의 높이로. 아래쪽 띠는 거래량 자리다.</summary>
    private static float Level(float price) => .24f + Mathf.Clamp01(price) * .62f;
    /// <summary>그 도구의 효과가 시작되는 자리(0~1). 여기서부터 흐름의 색이 바뀐다. 없으면 2.</summary>
    private float Trigger => kind == Kind.NightTip || kind == Kind.NightPlant || kind == Kind.NightTease ? .55f : kind == Kind.Dawn ? .60f : kind == Kind.Close ? .80f : kind == Kind.Ignite || kind == Kind.Slam ? .50f : 2f;
    private float Price(float x)
    {
        float wobble = .030f * Mathf.Sin(x * 19f + 1f) + .018f * Mathf.Sin(x * 41f);
        switch (kind)
        {
            case Kind.CrowdValue: return Mathf.Lerp(.30f, .66f, Ease(x)) + wobble;
            case Kind.RealValue: return Mathf.Lerp(.36f, .84f, x * x) + wobble;
            case Kind.NightTip: case Kind.NightTease: return x < .55f ? .42f + wobble : Mathf.Lerp(.46f, .82f, Ease((x - .55f) / .45f)) + wobble * .6f;
            case Kind.NightPlant: return x < .55f ? .42f + wobble : Mathf.Lerp(.60f, .84f, Ease((x - .55f) / .45f)) + wobble * .6f;
            case Kind.Dawn: return x < .60f ? .40f + wobble * .7f : Mathf.Lerp(.40f, .84f, Ease((x - .60f) / .40f)) + wobble * .4f;
            case Kind.Close: return x < .80f ? .45f + wobble : Mathf.Lerp(.45f, .80f, (x - .80f) / .20f);
            case Kind.Ignite: return x < .50f ? .36f + wobble : Mathf.Lerp(.66f, .84f, Ease((x - .50f) / .50f)) + wobble * .5f;
            case Kind.Slam: return x < .50f ? .68f + wobble : Mathf.Lerp(.38f, .26f, Ease((x - .50f) / .50f)) + wobble * .5f;
            case Kind.Wash: return .58f + wobble * .8f;
            case Kind.Wall: return Mathf.Max(.44f, .50f + .06f * Mathf.Sin(x * 9f) + wobble) + x * .14f;
            default: return .5f;
        }
    }
    private bool ShowsVolume => kind == Kind.Ignite || kind == Kind.Slam || kind == Kind.Wash || kind == Kind.Close || kind == Kind.NightTease;
    /// <summary>거래량 막대의 높이(0~1)와, 그 도구가 만든 거래량인지.</summary>
    private float Volume(float x, out bool mine)
    {
        float usual = .22f + .10f * Mathf.Abs(Mathf.Sin(x * 37f + 2f));
        mine = kind == Kind.Ignite || kind == Kind.Slam ? Mathf.Abs(x - .50f) < .05f : kind == Kind.Wash ? x > .32f && x < .80f : kind == Kind.Close ? x >= .80f : kind == Kind.NightTease && x >= .55f && x < .72f;
        if (mine) { return kind == Kind.Wash ? .78f + .2f * Mathf.Abs(Mathf.Sin(x * 53f)) : 1f; }
        return (kind == Kind.Ignite || kind == Kind.Slam) && x > .50f ? usual * 1.9f : usual;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (kind == Kind.None) { return; }
        Rect rect = GetPixelAdjustedRect();
        float unit = Mathf.Min(rect.width, rect.height) * .012f;
        float shown = Mathf.Clamp01(Mathf.Repeat(Time.unscaledTime - started, Loop) / (Loop * DrawShare));
        Vector2 At(float x, float y) => new Vector2(rect.xMin + rect.width * x, rect.yMin + rect.height * y);

        // 바닥선.
        Box(vh, At(0f, .215f), At(1f, .221f), Dim);

        if (kind == Kind.Found)
        {
            // 날마다 쌓이는 수입: 막대가 하나씩 올라온다.
            for (int i = 0; i < 7; i++)
            {
                float x = .10f + i * .12f, grown = Mathf.Clamp01(shown * 7f - i);
                if (grown <= 0f) { continue; }
                Box(vh, At(x, .221f), At(x + .08f, .221f + (.16f + i * .075f) * grown), new Color(Teal.r, Teal.g, Teal.b, .45f + .07f * i));
            }
            return;
        }

        float trigger = Trigger;
        // 그 도구가 일하는 구간을 옅게 칠한다.
        if (kind == Kind.Close) { Box(vh, At(.80f, .221f), At(1f, .93f), new Color(Teal.r, Teal.g, Teal.b, .10f)); }
        if (kind == Kind.NightTip || kind == Kind.NightPlant || kind == Kind.NightTease)
        {
            // 오늘과 내일을 가르는 선(밤).
            for (float y = .24f; y < .88f; y += .06f) { Box(vh, At(.548f, y), At(.552f, y + .03f), new Color(1f, 1f, 1f, .28f)); }
        }
        // 기준이 되는 선들.
        if (kind == Kind.CrowdValue) { Dashes(vh, rect, Level(.74f), Teal, unit); }
        if (kind == Kind.RealValue) { Dashes(vh, rect, Level(.90f), new Color(Teal.r, Teal.g, Teal.b, .55f), unit); Dashes(vh, rect, Level(.32f), Amber, unit); }
        if (kind == Kind.Wall) { Box(vh, At(.12f, Level(.34f) - .028f), At(1f, Level(.34f) + .028f), new Color(Teal.r, Teal.g, Teal.b, .55f)); }

        // 거래량.
        if (ShowsVolume)
        {
            for (int i = 0; i < Bars; i++)
            {
                float x = (i + .5f) / Bars;
                if (x > shown) { break; }
                float height = Volume(x, out bool mine);
                Box(vh, At(x - .012f, .03f), At(x + .012f, .03f + .16f * height), mine ? new Color(Teal.r, Teal.g, Teal.b, .85f) : new Color(1f, 1f, 1f, .20f));
            }
        }

        // 값의 흐름.
        Vector2 previous = At(0f, Level(Price(0f))); float previousPrice = Price(0f);
        for (int i = 1; i <= Points; i++)
        {
            float x = i / (float)Points;
            if (x > shown) { break; }
            float price = Price(x); Vector2 point = At(x, Level(price));
            bool effect = x > trigger;
            Color tint = !effect ? Ink : price >= previousPrice || kind != Kind.Slam ? (kind == Kind.Slam ? Fall : Rise) : Fall;
            // 거품: 진짜 가치 위로 뜬 부분을 붉게 채운다.
            if (kind == Kind.RealValue && price > .34f) { Box(vh, At(x - 1f / Points, Level(.32f)), new Vector2(point.x, point.y), new Color(Rise.r, Rise.g, Rise.b, .13f)); }
            Segment(vh, previous, point, unit * (effect ? 1.5f : 1.1f), tint);
            previous = point; previousPrice = price;
        }
        // 끝점.
        if (shown > 0f) { Disc(vh, previous, unit * 2.1f, kind == Kind.Slam && shown > trigger ? Fall : shown > trigger ? Rise : Ink); }

        // 표시들.
        if (kind == Kind.Dawn)
        {
            foreach (float x in new[] { .16f, .29f, .42f, .55f }) { if (x <= shown) { Disc(vh, At(x, Level(Price(x))), unit * 2.6f, Amber); } }
        }
        if ((kind == Kind.NightTip || kind == Kind.NightPlant) && shown > .40f)
        {
            // 기사 한 장과 그 위의 화살표.
            Vector2 sheet = At(.47f, .66f);
            Box(vh, sheet + new Vector2(-unit * 5f, -unit * 6f), sheet + new Vector2(unit * 5f, unit * 6f), new Color(1f, 1f, 1f, .16f));
            Triangle(vh, sheet + new Vector2(0f, unit * 3.2f), sheet + new Vector2(-unit * 3.2f, -unit * 2.2f), sheet + new Vector2(unit * 3.2f, -unit * 2.2f), Teal);
        }
        if (kind == Kind.NightTease && shown > .50f)
        {
            // 방의 회원들.
            for (int i = 0; i < 6; i++) { Disc(vh, At(.60f + i * .055f, .30f), unit * 2.0f, new Color(Teal.r, Teal.g, Teal.b, .9f)); }
        }
    }

    private static void Box(VertexHelper vh, Vector2 low, Vector2 high, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector2(low.x, low.y), tint, Vector2.zero); vh.AddVert(new Vector2(low.x, high.y), tint, Vector2.zero);
        vh.AddVert(new Vector2(high.x, high.y), tint, Vector2.zero); vh.AddVert(new Vector2(high.x, low.y), tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
    private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); vh.AddVert(c, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
    }
    private static void Segment(VertexHelper vh, Vector2 from, Vector2 to, float width, Color tint)
    {
        Vector2 along = to - from;
        if (along.sqrMagnitude <= 0f) { return; }
        Vector2 side = new Vector2(-along.y, along.x).normalized * width;
        int start = vh.currentVertCount;
        vh.AddVert(from - side, tint, Vector2.zero); vh.AddVert(from + side, tint, Vector2.zero); vh.AddVert(to + side, tint, Vector2.zero); vh.AddVert(to - side, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        Disc(vh, to, width, tint, 8);
    }
    private static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint, int sides = 16)
    {
        int start = vh.currentVertCount;
        vh.AddVert(center, tint, Vector2.zero);
        for (int i = 0; i <= sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            if (i > 0) { vh.AddTriangle(start, start + i, start + i + 1); }
        }
    }
    /// <summary>가로로 끝까지 가는 점선.</summary>
    private static void Dashes(VertexHelper vh, Rect rect, float height, Color tint, float unit)
    {
        float y = rect.yMin + rect.height * height;
        for (float x = 0f; x < 1f; x += .045f)
        { Box(vh, new Vector2(rect.xMin + rect.width * x, y - unit * .55f), new Vector2(rect.xMin + rect.width * (x + .026f), y + unit * .55f), tint); }
    }
}
