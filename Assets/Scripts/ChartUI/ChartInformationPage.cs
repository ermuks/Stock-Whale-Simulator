using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 메인 모니터(차트 화면)의 정보 페이지.
// 머리줄의 정보 아이콘을 누르면 차트 자리에 펼쳐진다. 왼쪽은 종목의 공개 정보, 오른쪽은 기사다.
// 씬은 건드리지 않고 시작할 때 만든다. 시장·계좌는 읽기만 한다.

/// <summary>글자 대신 쓰는 단순한 도형 아이콘. 그림 파일 없이 그린다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ChartIconGraphic : MaskableGraphic
{
    public enum Shape { Information, Previous, Next, List, Chart, Play, Settings, Leave, Power, Sleep, Globe, Plus }
    public Shape shape;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect(); float size = Mathf.Min(rect.width, rect.height);
        if (size <= 0f) { return; }
        Vector2 center = rect.center;
        switch (shape)
        {
            case Shape.Information:
                // 동그라미 안의 i.
                Ring(mesh, center, size * .44f, size * .075f);
                Box(mesh, center + new Vector2(0f, size * .20f), size * .11f, size * .11f);
                Box(mesh, center + new Vector2(0f, -size * .075f), size * .11f, size * .31f);
                break;
            case Shape.Previous:
                Triangle(mesh, center + new Vector2(-size * .30f, 0f), center + new Vector2(size * .22f, size * .34f), center + new Vector2(size * .22f, -size * .34f));
                break;
            case Shape.Next:
                Triangle(mesh, center + new Vector2(size * .30f, 0f), center + new Vector2(-size * .22f, -size * .34f), center + new Vector2(-size * .22f, size * .34f));
                break;
            case Shape.Play:
                Triangle(mesh, center + new Vector2(size * .34f, 0f), center + new Vector2(-size * .24f, -size * .36f), center + new Vector2(-size * .24f, size * .36f));
                break;
            case Shape.Settings:
                // 톱니바퀴: 고리와 이 여덟 개.
                Arc(mesh, center, size * .30f, size * .13f, 0f, 360f);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f * Mathf.Deg2Rad; Vector2 out_ = new(Mathf.Cos(angle), Mathf.Sin(angle));
                    Stroke(mesh, center + out_ * size * .27f, center + out_ * size * .44f, size * .13f);
                }
                break;
            case Shape.Leave:
                // 문틀과 밖으로 나가는 화살표.
                Stroke(mesh, center + new Vector2(-size * .36f, -size * .38f), center + new Vector2(-size * .36f, size * .38f), size * .09f);
                Stroke(mesh, center + new Vector2(-size * .40f, size * .34f), center + new Vector2(size * .02f, size * .34f), size * .09f);
                Stroke(mesh, center + new Vector2(-size * .40f, -size * .34f), center + new Vector2(size * .02f, -size * .34f), size * .09f);
                Stroke(mesh, center + new Vector2(-size * .12f, 0f), center + new Vector2(size * .26f, 0f), size * .09f);
                Triangle(mesh, center + new Vector2(size * .44f, 0f), center + new Vector2(size * .20f, -size * .20f), center + new Vector2(size * .20f, size * .20f));
                break;
            case Shape.Power:
                // 위가 열린 고리와 세로 막대.
                Arc(mesh, center + new Vector2(0f, -size * .04f), size * .38f, size * .10f, 125f, 415f);
                Stroke(mesh, center + new Vector2(0f, size * .06f), center + new Vector2(0f, size * .46f), size * .10f);
                break;
            case Shape.Sleep:
                Crescent(mesh, center, size * .42f, size * .22f, size * .36f);
                break;
            case Shape.Globe:
                // 지구본: 바깥 원, 적도, 세로 타원.
                Arc(mesh, center, size * .44f, size * .07f, 0f, 360f);
                Stroke(mesh, center + new Vector2(-size * .40f, 0f), center + new Vector2(size * .40f, 0f), size * .06f);
                Oval(mesh, center, size * .20f, size * .42f, size * .06f);
                break;
            case Shape.Plus:
                Box(mesh, center, size * .72f, size * .13f); Box(mesh, center, size * .13f, size * .72f);
                break;
            case Shape.Chart:
                // 봉 셋: 차트.
                Candle(mesh, center + new Vector2(-size * .30f, -size * .06f), size, .50f, .30f);
                Candle(mesh, center + new Vector2(0f, size * .10f), size, .62f, .36f);
                Candle(mesh, center + new Vector2(size * .30f, -size * .02f), size, .44f, .24f);
                break;
            default:
                // 가로줄 셋: 목록.
                for (int i = -1; i <= 1; i++) { Box(mesh, center + new Vector2(0f, i * size * .25f), size * .70f, size * .10f); }
                break;
        }
    }
    private void Candle(VertexHelper mesh, Vector2 center, float size, float wick, float body)
    {
        Box(mesh, center, size * .045f, size * wick);
        Box(mesh, center, size * .17f, size * body);
    }
    /// <summary>두께가 있는 호. 각도는 도 단위이고 오른쪽이 0, 시계 반대 방향이다.</summary>
    private void Arc(VertexHelper mesh, Vector2 center, float radius, float thickness, float from, float to)
    {
        int segments = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(to - from) / 9f));
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)segments) * Mathf.Deg2Rad, b = Mathf.Lerp(from, to, (i + 1) / (float)segments) * Mathf.Deg2Rad;
            Vector2 va = new(Mathf.Cos(a), Mathf.Sin(a)), vb = new(Mathf.Cos(b), Mathf.Sin(b));
            Quad(mesh, center + va * (radius - thickness), center + va * radius, center + vb * radius, center + vb * (radius - thickness));
        }
    }
    private void Oval(VertexHelper mesh, Vector2 center, float radiusX, float radiusY, float thickness)
    {
        const int segments = 36;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
            Vector2 va = new(Mathf.Cos(a), Mathf.Sin(a)), vb = new(Mathf.Cos(b), Mathf.Sin(b));
            Quad(mesh, center + Vector2.Scale(va, new Vector2(radiusX - thickness, radiusY - thickness)), center + Vector2.Scale(va, new Vector2(radiusX, radiusY)),
                center + Vector2.Scale(vb, new Vector2(radiusX, radiusY)), center + Vector2.Scale(vb, new Vector2(radiusX - thickness, radiusY - thickness)));
        }
    }
    /// <summary>두 점을 잇는 굵은 선.</summary>
    private void Stroke(VertexHelper mesh, Vector2 from, Vector2 to, float thickness)
    {
        Vector2 direction = (to - from).normalized, side = new Vector2(-direction.y, direction.x) * (thickness * .5f);
        Quad(mesh, from - side, from + side, to + side, to - side);
    }
    /// <summary>초승달: 큰 원에서 오른쪽으로 비켜 놓은 작은 원을 뺀 모양.</summary>
    private void Crescent(VertexHelper mesh, Vector2 center, float outer, float shift, float inner)
    {
        float x = (shift * shift + outer * outer - inner * inner) / (2f * shift), y = Mathf.Sqrt(Mathf.Max(0f, outer * outer - x * x));
        float outerStart = Mathf.Atan2(y, x), innerStart = Mathf.Atan2(y, x - shift);
        const int segments = 28;
        for (int i = 0; i < segments; i++)
        {
            float t0 = i / (float)segments, t1 = (i + 1) / (float)segments;
            float a0 = Mathf.Lerp(outerStart, Mathf.PI * 2f - outerStart, t0), a1 = Mathf.Lerp(outerStart, Mathf.PI * 2f - outerStart, t1);
            float b0 = Mathf.Lerp(innerStart, Mathf.PI * 2f - innerStart, t0), b1 = Mathf.Lerp(innerStart, Mathf.PI * 2f - innerStart, t1);
            Quad(mesh, center + new Vector2(shift + Mathf.Cos(b0) * inner, Mathf.Sin(b0) * inner), center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * outer,
                center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outer, center + new Vector2(shift + Mathf.Cos(b1) * inner, Mathf.Sin(b1) * inner));
        }
    }
    private void Ring(VertexHelper mesh, Vector2 center, float radius, float thickness)
    {
        const int segments = 40;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
            Vector2 va = new(Mathf.Cos(a), Mathf.Sin(a)), vb = new(Mathf.Cos(b), Mathf.Sin(b));
            Quad(mesh, center + va * (radius - thickness), center + va * radius, center + vb * radius, center + vb * (radius - thickness));
        }
    }
    private void Box(VertexHelper mesh, Vector2 center, float width, float height)
    {
        Vector2 half = new(width * .5f, height * .5f);
        Quad(mesh, center + new Vector2(-half.x, -half.y), center + new Vector2(-half.x, half.y), center + new Vector2(half.x, half.y), center + new Vector2(half.x, -half.y));
    }
    private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero); mesh.AddVert(c, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
    }
    private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero); mesh.AddVert(c, color, Vector2.zero); mesh.AddVert(d, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
}

public sealed class ChartInformationPage : MonoBehaviour
{
    private const int RowLimit = 40;
    private const float RowHeight = 66f;
    private static readonly Color Ground = new Color32(12, 18, 22, 255), Surface = new Color32(18, 27, 32, 255), Cell = new Color32(25, 36, 42, 255),
        Chosen = new Color32(23, 53, 60, 255), Line = new Color32(38, 52, 59, 255), Ink = new Color32(230, 235, 239, 255),
        Muted = new Color32(138, 150, 163, 255), Faint = new Color32(96, 110, 120, 255), Teal = new Color32(72, 196, 188, 255);

    /// <summary>이 화면의 글자. 한국어·영어·일본어를 각각 적는다.</summary>
    private static readonly Dictionary<string, (string en, string ja)> Words = new()
    {
        ["종목 정보"] = ("Company profile", "銘柄情報"), ["기사"] = ("News", "記事"), ["{0}건"] = ("{0} articles", "{0}件"),
        ["업종"] = ("Sector", "業種"), ["시가총액"] = ("Market cap", "時価総額"), ["발행 주식"] = ("Shares issued", "発行株式"),
        ["유통 주식"] = ("Free float", "流通株式"), ["대주주 지분"] = ("Largest holder", "大株主の持分"), ["전일 종가"] = ("Previous close", "前日終値"),
        ["52주 최고"] = ("52-week high", "52週高値"), ["52주 최저"] = ("52-week low", "52週安値"), ["기업 실제 가치 (주당)"] = ("Real value / share", "企業の実際価値(1株)"),
        ["최근 결산 매출"] = ("Last revenue", "直近の売上"), ["최근 결산 순이익"] = ("Last net profit", "直近の純利益"), ["주당 배당"] = ("Dividend / share", "1株配当"),
        ["해금 필요"] = ("Locked", "未解放"), ["첫 결산 전"] = ("No results yet", "初回決算前"), ["관련 기업"] = ("Related companies", "関連企業"),
        ["이해관계자"] = ("Stakeholders", "利害関係者"), ["관련 기업 정보가 없습니다"] = ("No related companies on record", "関連企業の情報はありません"),
        ["아직 기사가 없습니다"] = ("No articles yet", "まだ記事がありません"), ["미확인"] = ("Unconfirmed", "未確認"), ["후속"] = ("Follow-up", "続報"),
        ["{0} / {1}"] = ("{0} / {1}", "{0} / {1}"), ["시장"] = ("Market", "市場"), ["차트"] = ("Chart", "チャート"),
    };
    private static string T(string korean) => GameLocale.Code == "ko" || !Words.TryGetValue(korean, out var other) ? korean : GameLocale.Code == "ja" ? other.ja : other.en;

    private ChartManager chart;
    private RectTransform screen, page;
    private TMP_Text like;
    private readonly List<(GameObject item, bool active)> hidden = new();
    // 왼쪽: 종목 정보.
    private TextMeshProUGUI companyName, companySymbol, profileHeading, relatedHeading, relatedText, stakeholderHeading, stakeholderText;
    private static readonly string[] Captions = { "업종", "시가총액", "발행 주식", "유통 주식", "대주주 지분", "전일 종가", "52주 최고", "52주 최저", "기업 실제 가치 (주당)", "최근 결산 매출", "최근 결산 순이익", "주당 배당" };
    private readonly TextMeshProUGUI[] captions = new TextMeshProUGUI[12], values = new TextMeshProUGUI[12];
    // 오른쪽: 기사.
    private TextMeshProUGUI newsHeading, newsCount, emptyNews, articleTitle, articleMeta, articleBody, articlePosition;
    private GameObject listView, readView;
    private ScrollRect listScroll, bodyScroll;
    private RectTransform listContent, bodyContent;
    private Button previous, next, back;
    private readonly List<Button> rows = new(RowLimit);
    private readonly List<TextMeshProUGUI> rowTitles = new(RowLimit), rowMeta = new(RowLimit);
    private readonly List<NewsArticle> articles = new(RowLimit);
    private readonly StringBuilder buffer = new();
    private NewsArticle reading;
    private Stock shown;
    private int newsVersion = -1, locale = -1;
    private float nextRefresh;
    private TextMeshProUGUI chartLabel;
    public bool Open { get; private set; }

    public static ChartInformationPage Install(ChartManager owner)
    {
        if (owner == null || owner.textName == null || !(owner.textName.transform.parent is RectTransform header) || !(header.parent is RectTransform root)) { return null; }
        var existing = root.GetComponentInChildren<ChartInformationPage>(true);
        if (existing != null) { return existing; }
        var holder = ChatFooterKit.Area("Area :: Information Page", root, 0f, 1f - header.anchorMin.y, 1f, header.anchorMin.y);
        var view = holder.gameObject.AddComponent<ChartInformationPage>();
        view.chart = owner; view.screen = root; view.page = holder; view.like = owner.textName;
        view.Build();
        return view;
    }

    private void Build()
    {
        // 정보 단추: 차트의 오른쪽 위 모서리 바로 위, 시간 단추와 같은 줄에 같은 높이로 둔다.
        var toggle = LabelButton("Information", screen, ChartIconGraphic.Shape.Information, "정보", Toggle);
        var toggleRect = (RectTransform)toggle.transform;
        var plot = chart.candleRenderer;
        Vector3 corner = screen.InverseTransformPoint(plot.transform.TransformPoint(new Vector3(plot.PlotWidth, plot.chartHeight, 0f)));
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(.5f, .5f); toggleRect.pivot = new Vector2(1f, 0f);
        toggleRect.sizeDelta = new Vector2(112f, 36f);
        toggleRect.localPosition = new Vector3(corner.x, corner.y + 6f, 0f);

        var ground = ChatFooterKit.Flat("Image :: Information Background", page, 0, 0, 1, 1, Ground); ground.raycastTarget = true;
        BuildProfile(Panel("Company Profile", .012f, .02f, .592f, .96f));
        BuildNews(Panel("Company News", .612f, .02f, .376f, .96f));
        // 차트로 돌아가기: 종목 정보 패널의 오른쪽 위.
        var back = LabelButton("Back To Chart", page, ChartIconGraphic.Shape.Chart, "차트", Toggle);
        ChatFooterKit.Place((RectTransform)back.transform, page, .012f + .592f - .012f - .075f, .02f + .018f, .075f, .05f);
        chartLabel = back.GetComponentInChildren<TextMeshProUGUI>(true);
        page.gameObject.SetActive(false);
    }
    private RectTransform Panel(string name, float x, float y, float w, float h)
    {
        var panel = ChatFooterKit.Area("Panel :: " + name, page, x, y, w, h);
        ChatFooterKit.Flat("Image :: Panel Background", panel, 0, 0, 1, 1, Surface);
        return panel;
    }
    private TextMeshProUGUI Text(string name, Transform parent, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool wrap = false)
    {
        var label = ChatFooterKit.Area("Text :: " + name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = like.font; label.fontSharedMaterial = like.font.material;
        ChatFooterKit.Restyle(label, size, color, align, wrap);
        return label;
    }
    /// <summary>아이콘과 글자가 나란히 있는 단추. 테두리와 바탕이 있어 누르는 곳임이 보인다.</summary>
    private Button LabelButton(string name, Transform parent, ChartIconGraphic.Shape shape, string caption, UnityEngine.Events.UnityAction action)
    {
        var item = new GameObject("Button :: " + name, typeof(RectTransform), typeof(Image), typeof(Button)) { layer = parent.gameObject.layer };
        item.transform.SetParent(parent, false);
        var border = item.GetComponent<Image>(); border.color = new Color32(72, 96, 108, 255);
        var button = item.GetComponent<Button>(); button.targetGraphic = border; button.onClick.AddListener(action);
        var colors = button.colors; colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f); colors.pressedColor = new Color(.8f, .8f, .8f, 1f); button.colors = colors;
        var fill = ChatFooterKit.Flat("Image :: " + name + " Fill", item.transform, 0, 0, 1, 1, new Color32(30, 108, 119, 255));
        fill.rectTransform.offsetMin = new Vector2(2f, 2f); fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
        var glyph = new GameObject("Image :: " + name + " Icon", typeof(RectTransform), typeof(CanvasRenderer)) { layer = item.layer };
        var rect = (RectTransform)glyph.transform; rect.SetParent(item.transform, false);
        rect.anchorMin = new Vector2(0f, .5f); rect.anchorMax = new Vector2(0f, .5f); rect.pivot = new Vector2(0f, .5f);
        rect.anchoredPosition = new Vector2(10f, 0f); rect.sizeDelta = new Vector2(24f, 24f);
        var icon = glyph.AddComponent<ChartIconGraphic>(); icon.shape = shape; icon.color = Ink; icon.raycastTarget = false;
        var label = Text(name + " Label", item.transform, 0, 0, 1, 1, 22f, Ink, TextAlignmentOptions.Center);
        label.rectTransform.offsetMin = new Vector2(38f, 0f); label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        GameLocalizedText.Set(label, caption);
        return button;
    }
    private Button IconButton(string name, Transform parent, ChartIconGraphic.Shape shape, UnityEngine.Events.UnityAction action, out ChartIconGraphic icon)
    {
        var item = new GameObject("Button :: " + name, typeof(RectTransform), typeof(Image), typeof(Button)) { layer = parent.gameObject.layer };
        item.transform.SetParent(parent, false);
        var image = item.GetComponent<Image>(); image.color = Cell;
        var button = item.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        var glyph = new GameObject("Image :: " + name + " Icon", typeof(RectTransform), typeof(CanvasRenderer)) { layer = item.layer };
        var rect = (RectTransform)glyph.transform; rect.SetParent(item.transform, false);
        rect.anchorMin = new Vector2(.2f, .2f); rect.anchorMax = new Vector2(.8f, .8f); rect.offsetMin = rect.offsetMax = Vector2.zero;
        icon = glyph.AddComponent<ChartIconGraphic>(); icon.shape = shape; icon.color = Ink; icon.raycastTarget = false;
        return button;
    }

    private void BuildProfile(RectTransform panel)
    {
        profileHeading = Text("Profile Heading", panel, .03f, .025f, .40f, .05f, 22f, Muted);
        companyName = Text("Company Name", panel, .03f, .08f, .70f, .08f, 40f, Ink);
        companySymbol = Text("Company Symbol", panel, .73f, .08f, .24f, .08f, 24f, Muted, TextAlignmentOptions.MidlineRight);
        ChatFooterKit.Flat("Image :: Profile Divider", panel, .03f, .175f, .94f, .003f, Line);
        // 값 칸 열두 개: 세 칸씩 네 줄.
        for (int i = 0; i < captions.Length; i++)
        {
            float x = .03f + i % 3 * .318f, y = .20f + i / 3 * .125f;
            var cell = ChatFooterKit.Flat("Panel :: Profile Cell " + i, panel, x, y, .304f, .11f, Cell).transform;
            captions[i] = Text("Caption", cell, .06f, .10f, .88f, .34f, 19f, Muted);
            values[i] = Text("Value", cell, .06f, .46f, .88f, .46f, 27f, Ink);
        }
        ChatFooterKit.Flat("Image :: Related Divider", panel, .03f, .715f, .94f, .003f, Line);
        relatedHeading = Text("Related Heading", panel, .03f, .73f, .94f, .045f, 20f, Muted);
        relatedText = Text("Related Companies", panel, .03f, .78f, .94f, .115f, 22f, Ink, TextAlignmentOptions.TopLeft, true);
        stakeholderHeading = Text("Stakeholder Heading", panel, .03f, .90f, .20f, .045f, 20f, Muted);
        stakeholderText = Text("Stakeholders", panel, .23f, .90f, .74f, .045f, 22f, Ink);
    }

    private void BuildNews(RectTransform panel)
    {
        newsHeading = Text("News Heading", panel, .04f, .025f, .40f, .05f, 22f, Muted);
        newsCount = Text("News Count", panel, .44f, .025f, .40f, .05f, 20f, Faint, TextAlignmentOptions.MidlineRight);
        // 목록으로 돌아가기. 기사를 읽는 동안에만 보인다.
        back = IconButton("Article List", panel, ChartIconGraphic.Shape.List, ShowList, out _);
        ChatFooterKit.Place((RectTransform)back.transform, panel, .88f, .02f, .08f, .06f);

        listView = ChatFooterKit.Area("Area :: Article List", panel, 0, .09f, 1, .91f).gameObject;
        listScroll = Scroll("Article Rows", listView.transform, .04f, 0f, .92f, .97f, out listContent);
        emptyNews = Text("Empty News", listView.transform, .04f, .10f, .92f, .10f, 22f, Faint, TextAlignmentOptions.Center);

        readView = ChatFooterKit.Area("Area :: Article Reader", panel, 0, .09f, 1, .91f).gameObject;
        // 제목 줄: 이전 ◀ 제목 ▶ 다음.
        var bar = ChatFooterKit.Flat("Panel :: Article Title", readView.transform, .04f, 0f, .92f, .15f, Cell).transform;
        previous = IconButton("Previous Article", bar, ChartIconGraphic.Shape.Previous, () => Step(-1), out _);
        ChatFooterKit.Place((RectTransform)previous.transform, bar, .01f, .14f, .09f, .72f);
        next = IconButton("Next Article", bar, ChartIconGraphic.Shape.Next, () => Step(1), out _);
        ChatFooterKit.Place((RectTransform)next.transform, bar, .90f, .14f, .09f, .72f);
        articleTitle = Text("Article Title", bar, .12f, .06f, .76f, .88f, 25f, Ink, TextAlignmentOptions.Center, true);
        articleTitle.fontStyle = FontStyles.Bold; articleTitle.maxVisibleLines = 3;
        articleMeta = Text("Article Meta", readView.transform, .04f, .16f, .70f, .05f, 18f, Muted);
        articlePosition = Text("Article Position", readView.transform, .74f, .16f, .22f, .05f, 18f, Faint, TextAlignmentOptions.MidlineRight);
        bodyScroll = Scroll("Article Body", readView.transform, .04f, .22f, .92f, .75f, out bodyContent);
        articleBody = Text("Article Text", bodyContent, 0, 0, 1, 1, 22f, Ink, TextAlignmentOptions.TopLeft, true);
        articleBody.richText = true; articleBody.overflowMode = TextOverflowModes.Overflow; articleBody.lineSpacing = 6f;
        readView.SetActive(false); back.gameObject.SetActive(false);
    }
    private ScrollRect Scroll(string name, Transform parent, float x, float y, float w, float h, out RectTransform content)
    {
        var root = ChatFooterKit.Area("Area :: " + name, parent, x, y, w, h);
        var surface = root.gameObject.AddComponent<Image>(); surface.color = Color.clear; surface.raycastTarget = true;
        root.gameObject.AddComponent<RectMask2D>();
        var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.viewport = root; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;
        content = ChatFooterKit.Area("Area :: " + name + " Content", root, 0, 0, 1, 1);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        scroll.content = content;
        return scroll;
    }
    private void EnsureRows(int count)
    {
        while (rows.Count < count && rows.Count < RowLimit)
        {
            int index = rows.Count;
            var item = new GameObject("Button :: Article Row " + (index + 1), typeof(RectTransform), typeof(Image), typeof(Button)) { layer = listContent.gameObject.layer };
            var rect = (RectTransform)item.transform; rect.SetParent(listContent, false);
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -index * RowHeight); rect.sizeDelta = new Vector2(0, RowHeight - 4f);
            var image = item.GetComponent<Image>(); image.color = Cell;
            var button = item.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => Read(index));
            rowTitles.Add(Text("Row Title", rect, .03f, .08f, .94f, .50f, 21f, Ink));
            rowMeta.Add(Text("Row Meta", rect, .03f, .58f, .94f, .36f, 16f, Muted));
            rows.Add(button);
        }
    }

    private void Toggle()
    {
        Open = !Open;
        if (Open)
        {
            // 차트·호가·체결 자리를 비우고 그 위에 편다. 머리줄과 커서는 그대로 둔다.
            hidden.Clear();
            foreach (Transform child in screen)
            {
                if (child == page || child == chart.textName.transform.parent || child.name.Contains("Cursor")) { continue; }
                hidden.Add((child.gameObject, child.gameObject.activeSelf)); child.gameObject.SetActive(false);
            }
            page.gameObject.SetActive(true); page.SetAsLastSibling();
            foreach (Transform child in screen) { if (child.name.Contains("Cursor")) { child.SetAsLastSibling(); break; } }
            shown = null; newsVersion = -1; nextRefresh = 0f; ShowList();
        }
        else
        {
            page.gameObject.SetActive(false);
            foreach (var entry in hidden) { if (entry.item != null) { entry.item.SetActive(entry.active); } }
            hidden.Clear();
        }
        chart.SetInformationOpen(Open);
    }

    void Update()
    {
        // 이 스크립트는 페이지에 붙어 있어 페이지가 열려 있을 때만 돈다.
        if (Time.unscaledTime < nextRefresh) { return; }
        nextRefresh = Time.unscaledTime + .3f;
        var stock = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        var news = NewsManager.Instance;
        bool relabel = locale != GameLocale.Revision; locale = GameLocale.Revision;
        RefreshProfile(stock);
        if (relabel || stock != shown || news != null && news.ArticleVersion != newsVersion)
        {
            shown = stock; newsVersion = news == null ? -1 : news.ArticleVersion;
            RefreshArticles(news, stock);
        }
    }

    private void RefreshProfile(Stock stock)
    {
        ChatFooterKit.Lit(chartLabel, T("차트"));
        ChatFooterKit.Lit(profileHeading, T("종목 정보")); ChatFooterKit.Lit(relatedHeading, T("관련 기업")); ChatFooterKit.Lit(stakeholderHeading, T("이해관계자"));
        for (int i = 0; i < captions.Length; i++) { ChatFooterKit.Lit(captions[i], T(Captions[i])); }
        if (stock == null) { return; }
        ChatFooterKit.Lit(companyName, GameLocale.Text(stock.stockName)); ChatFooterKit.Lit(companySymbol, stock.symbol);
        var profile = NewsManager.Instance == null || NewsManager.Instance.Writing == null ? null : NewsManager.Instance.Writing.Profile(stock.symbol);
        decimal issued = stock.issuedShares, held = stock.Owner == null ? 0m : stock.Owner.GetOwnedVolume(stock);
        var stats = StockStatistics.Instance == null ? default : StockStatistics.Instance.GetStats(stock);
        var business = stock.business;
        bool closed = business != null && business.LastDay > 0;
        Value(0, profile == null ? "—" : GameLocale.Text(profile.sector));
        Value(1, Amount(issued * stock.currentPrice));
        Value(2, Shares(issued));
        Value(3, Shares(Math.Max(0m, issued - held)));
        Value(4, issued > 0m ? (held / issued * 100m).ToString("0.#") + "%" : "—");
        Value(5, Price(stats.prevDayClose));
        Value(6, Price(stats.week52High));
        Value(7, Price(stats.week52Low));
        // 기업의 실제 가치는 해금한 뒤에만 보인다. 머리줄의 가치 칸과 같은 규칙이다.
        bool unlocked = PlayerProgression.Has(PlayerProgression.RealValueUnlock);
        Value(8, unlocked ? Price(stock.realValue) : T("해금 필요"), unlocked);
        Value(9, closed ? Amount(business.Revenue) : T("첫 결산 전"), closed);
        Value(10, closed ? Amount(business.Profit) : T("첫 결산 전"), closed);
        Value(11, closed && issued > 0m ? Price(business.Dividend / issued) : T("첫 결산 전"), closed);

        buffer.Clear();
        if (profile != null && profile.links != null && StockManager.Instance != null)
        {
            foreach (var link in profile.links)
            {
                Stock other = null;
                foreach (var candidate in StockManager.Instance.stocks) { if (candidate.symbol == link.symbol && !candidate.IsDelisted) { other = candidate; break; } }
                if (other == null) { continue; }
                if (buffer.Length > 0) { buffer.Append('\n'); }
                buffer.Append(GameLocale.Text(other.stockName)).Append("  —  ").Append(GameLocale.Text(link.relation));
            }
        }
        bool related = buffer.Length > 0;
        ChatFooterKit.Lit(relatedText, related ? buffer.ToString() : T("관련 기업 정보가 없습니다")); relatedText.color = related ? Ink : Faint;
        ChatFooterKit.Lit(stakeholderText, profile == null ? "—" : GameLocale.Text(profile.community));
    }
    private void Value(int index, string text, bool known = true)
    { ChatFooterKit.Lit(values[index], text); values[index].color = known ? Ink : Faint; }
    private static string Price(decimal price) => price > 0m ? DeviceTerminalUI.Money(PriceTickPolicy.RoundToTick(price)) : "—";
    private static string Shares(decimal shares)
        => shares.ToString("N0") + (GameLocale.Code == "ko" ? "주" : GameLocale.Code == "ja" ? "株" : " sh");
    /// <summary>큰 금액은 조·억 단위로 줄여 적는다.</summary>
    private static string Amount(decimal amount)
    {
        string sign = amount < 0m ? "−" : ""; decimal value = Math.Abs(amount);
        if (GameLocale.Code == "en")
        {
            return sign + (value >= 1_000_000_000_000m ? (value / 1_000_000_000_000m).ToString("0.##") + "T" : value >= 1_000_000_000m ? (value / 1_000_000_000m).ToString("0.#") + "B" :
                value >= 1_000_000m ? (value / 1_000_000m).ToString("0.#") + "M" : value.ToString("N0")) + " KRW";
        }
        string trillion = GameLocale.Code == "ja" ? "兆ウォン" : "조 원", hundredMillion = GameLocale.Code == "ja" ? "億ウォン" : "억 원", won = GameLocale.Code == "ja" ? "ウォン" : "원";
        return sign + (value >= 1_000_000_000_000m ? (value / 1_000_000_000_000m).ToString("0.##") + trillion : value >= 100_000_000m ? (value / 100_000_000m).ToString("N0") + hundredMillion : value.ToString("N0") + won);
    }

    private void RefreshArticles(NewsManager news, Stock stock)
    {
        articles.Clear();
        if (news != null)
        {
            // 지금 보고 있는 종목의 기사를 먼저, 그 뒤에 나머지를 올라온 순서대로(최신이 위).
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = news.Articles.Count - 1; i >= 0 && articles.Count < RowLimit; i--)
                {
                    var article = news.Articles[i];
                    bool own = stock != null && article.symbol == stock.symbol;
                    if (own == (pass == 0)) { articles.Add(article); }
                }
            }
        }
        EnsureRows(articles.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i < articles.Count; rows[i].gameObject.SetActive(active);
            if (!active) { continue; }
            var article = articles[i]; bool own = stock != null && article.symbol == stock.symbol;
            DeviceTerminalUI.Set(rowTitles[i], article.title);
            ChatFooterKit.Lit(rowMeta[i], Meta(article));
            rowMeta[i].color = own ? Teal : Muted;
            rows[i].image.color = article == reading ? Chosen : Cell;
        }
        listContent.sizeDelta = new Vector2(0, articles.Count * RowHeight);
        ChatFooterKit.Lit(newsHeading, T("기사")); ChatFooterKit.Lit(newsCount, string.Format(T("{0}건"), articles.Count));
        emptyNews.gameObject.SetActive(articles.Count == 0); ChatFooterKit.Lit(emptyNews, T("아직 기사가 없습니다"));
        if (readView.activeSelf)
        {
            // 읽던 기사가 목록에서 밀려났으면 목록으로 돌아간다.
            if (reading == null || !articles.Contains(reading)) { ShowList(); }
            else { RenderArticle(false); }
        }
    }
    private static string Meta(NewsArticle article)
    {
        string who = string.IsNullOrEmpty(article.company) ? GameLocale.Text(article.source ?? "") : GameLocale.Text(article.company);
        return who + "  ·  " + GameLocale.Text(article.time ?? "") + (article.credibility < .5 ? "  ·  " + T("미확인") : article.correction ? "  ·  " + T("후속") : "");
    }

    private void ShowList()
    {
        reading = null; listView.SetActive(true); readView.SetActive(false); back.gameObject.SetActive(false);
        foreach (var row in rows) { row.image.color = Cell; }
    }
    private void Read(int index)
    {
        if (index < 0 || index >= articles.Count) { return; }
        reading = articles[index]; listView.SetActive(false); readView.SetActive(true); back.gameObject.SetActive(true);
        RenderArticle(true);
    }
    private void Step(int direction)
    {
        int index = articles.IndexOf(reading) + direction;
        if (reading == null || index < 0 || index >= articles.Count) { return; }
        reading = articles[index]; RenderArticle(true);
    }
    private static string Safe(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");
    private void RenderArticle(bool top)
    {
        int index = articles.IndexOf(reading);
        previous.interactable = index > 0; next.interactable = index >= 0 && index < articles.Count - 1;
        Dim(previous); Dim(next);
        DeviceTerminalUI.Set(articleTitle, reading.title);
        ChatFooterKit.Lit(articleMeta, Meta(reading));
        ChatFooterKit.Lit(articlePosition, string.Format(T("{0} / {1}"), index + 1, articles.Count));
        buffer.Clear();
        buffer.Append("<color=#76CAD4>").Append(Safe(reading.categoryLabel)).Append("  ·  ").Append(Safe(reading.source)).Append("  ·  ").Append(Safe(reading.verification)).Append("</color>\n\n")
            .Append(Safe(reading.summary)).Append("\n\n");
        string body = Safe(reading.body);
        foreach (string heading in new[] { "무슨 일이 있었나", "전해진 주장", "후속 확인", "배경과 이해관계", "사회가 받아들이는 방식", "다음 확인 지점" })
        { body = body.Replace(heading + "\n", "<b>" + heading + "</b>\n"); }
        buffer.Append(body);
        string text = buffer.ToString();
        float before = bodyScroll.verticalNormalizedPosition;
        DeviceTerminalUI.Set(articleBody, text);
        float width = Mathf.Max(100f, bodyScroll.viewport.rect.width - 12f);
        float height = articleBody.GetPreferredValues(GameLocale.Text(text), width, 0).y + 30f;
        bodyContent.sizeDelta = new Vector2(0, height);
        var rect = articleBody.rectTransform;
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0, 1); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(-12f, height);
        bodyScroll.verticalNormalizedPosition = top ? 1f : before;
    }
    private static void Dim(Button button)
    {
        var icon = button.GetComponentInChildren<ChartIconGraphic>(true);
        if (icon != null) { icon.color = button.interactable ? Ink : Faint; }
    }
}
