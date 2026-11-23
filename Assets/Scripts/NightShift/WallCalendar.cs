using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 왼쪽 벽의 달력. 기능은 없고, 날이 가는 것을 눈으로 보여 주는 소품이다.
//
// 하루가 지나면 그날 칸에 네임펜으로 찍 긋는다. 긋는 모양은 날마다 다르다(빗금, 가위표, 지그재그, 가로줄, 동그라미 …).
// 달력은 일요일에서 시작해 토요일로 끝난다(일 월 화 수 목 금 토).
// 날짜는 진짜 달력처럼 센다: 1일이 월요일이고, 금요일은 5일, 토요일 6일, 일요일 7일, 다음 월요일은 8일이다.
// 장이 서는 날은 월~금 닷새다. 게임의 "몇 번째 장날"(Day)은 주말을 세지 않으므로, 여섯째 장날이 달력의 8일이 된다.
// 금요일이 지나면 그 줄 끝의 토요일과 다음 줄 첫머리의 일요일을 잇달아 휙휙 그어 건너뛴다.
// 한 장은 네 주, 28일이다(1일 월요일 ~ 28일 일요일). 장날 스무 번이 지나면 다음 장으로 넘어가고 날짜는 29일부터 이어진다.
//
// 씬 파일은 그대로다. 방이 다 지어진 뒤에 실행 중에 만들어 벽에 붙인다. 게임 상태는 아무것도 바꾸지 않는다.
// 선은 그림 파일이 아니라 그때그때 그린다(날짜마다 정해진 흔들림으로, 같은 날은 늘 같은 모양이다).
public sealed class WallCalendar : MonoBehaviour
{
    public static WallCalendar Instance { get; private set; }
    private const int Weeks = 4, Columns = 7, TradingDays = 5, PageDays = Weeks * TradingDays;
    /// <summary>달력의 줄 수. 28일(일요일)이 다섯째 줄의 첫 칸에 온다.</summary>
    private const int Rows = Weeks + 1;
    private const float Pixels = 300f, SheetWidth = 2.1f, SheetHeight = 2.9f, StrokeSeconds = .45f, SweepSeconds = .55f;
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    private static readonly string[] WeekKorean = { "일", "월", "화", "수", "목", "금", "토" };
    private static readonly string[] WeekEnglish = { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };
    private static readonly string[] WeekJapanese = { "日", "月", "火", "水", "木", "金", "土" };
    /// <summary>쉬는 날의 칸인가(맨 왼쪽 일요일, 맨 오른쪽 토요일).</summary>
    private static bool Rest(int column) => column == 0 || column == Columns - 1;

    private RectTransform sheet, grid;
    private TMP_FontAsset font;
    private TextMeshProUGUI header, caption;
    private readonly TextMeshProUGUI[] weekLabels = new TextMeshProUGUI[Columns];
    private readonly TextMeshProUGUI[] dayLabels = new TextMeshProUGUI[PageDays];
    /// <summary>쉬는 날의 날짜: 주마다 토요일(6, 13, 20, 27)과 그다음 일요일(7, 14, 21, 28).</summary>
    private readonly TextMeshProUGUI[] saturdayLabels = new TextMeshProUGUI[Weeks], sundayLabels = new TextMeshProUGUI[Weeks];
    private CalendarInk ink;
    private int shownDay = -1, shownPage = -1, writtenRevision = -1;
    /// <summary>지금 그어지고 있는 선(없으면 -1)과 그 진행. 선의 번호는 0~19가 날짜 칸, 20~23이 주말 건너뛰기다.</summary>
    private int drawing = -1; private float drawn; private bool sweepNext;
    /// <summary>확인용: 0보다 크면 이 날짜인 것처럼 보인다.</summary>
    public int DayOverride;
    public bool Built => sheet != null;
    /// <summary>달력 종이의 한가운데와 바라보는 방향(확인용 사진을 찍을 때 쓴다).</summary>
    public bool Pose(out Vector3 center, out Vector3 normal, out float height)
    {
        center = Vector3.zero; normal = Vector3.right; height = SheetHeight;
        if (sheet == null) { return false; }
        center = sheet.position; normal = -sheet.forward;
        return true;
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    void Update()
    {
        var market = MarketDayController.Instance; var room = RoomShell.Instance; var views = DeskViewController.Instance;
        if (market == null || !market.Ready || room == null || !room.Built || views == null || !views.Ready) { return; }
        if (sheet == null && !Build(views.ViewCamera)) { return; }
        int day = DayOverride > 0 ? DayOverride : market.Day;
        int page = (Mathf.Max(1, day) - 1) / PageDays;
        if (page != shownPage || writtenRevision != GameLocale.Revision) { shownPage = page; writtenRevision = GameLocale.Revision; Write(page); }
        if (day != shownDay)
        {
            // 하루가 막 넘어갔으면(바로 다음 날) 어제 칸에 선을 긋는 모습을 보인다. 그 밖에는(처음 만들 때, 불러왔을 때, 장이 넘어갔을 때) 바로 그려 둔다.
            bool next = shownDay > 0 && day == shownDay + 1 && (day - 1) / PageDays == (shownDay - 1) / PageDays;
            shownDay = day;
            int done = Mathf.Clamp(day - 1 - page * PageDays, 0, PageDays);
            ink.Done = done; drawing = -1; sweepNext = false;
            if (next && done > 0)
            {
                drawing = done - 1; drawn = 0f; ink.Done = done - 1;
                sweepNext = done % TradingDays == 0;
                Sfx.Play("PenStroke", .5f);
            }
            ink.Drawing = drawing; ink.Progress = 0f; ink.SetVerticesDirty();
        }
        if (drawing >= 0)
        {
            bool sweep = drawing >= PageDays;
            drawn += Time.unscaledDeltaTime / (sweep ? SweepSeconds : StrokeSeconds);
            if (drawn >= 1f)
            {
                if (!sweep) { ink.Done = drawing + 1; }
                if (!sweep && sweepNext)
                {
                    // 금요일을 긋고 나면 곧바로 토·일을 한 번에 건너뛴다.
                    sweepNext = false; drawing = PageDays + drawing / TradingDays; drawn = 0f; ink.Sweeping = true;
                    Sfx.Play("PenStroke", .6f);
                }
                else { drawing = -1; ink.Sweeping = false; }
            }
            ink.Drawing = drawing; ink.Progress = Mathf.Clamp01(drawn); ink.SetVerticesDirty();
        }
    }

    private bool Build(Camera camera)
    {
        var wall = GameObject.Find("Area :: Damp Left Wall"); var desk = GameObject.Find("desk_body");
        if (wall == null || desk == null || camera == null) { return false; }
        var wallBox = wall.GetComponent<Renderer>().bounds; var deskBox = desk.GetComponent<Renderer>().bounds;
        // 벽은 방의 왼쪽(-x)에 있다. 방 안쪽을 보는 면에, 앉은 자리에서 고개를 왼쪽으로 돌리면 보이는 높이로 붙인다.
        bool wallOnLeft = wallBox.center.x < camera.transform.position.x;
        float x = wallOnLeft ? wallBox.max.x + .03f : wallBox.min.x - .03f;
        float z = Mathf.Min(camera.transform.position.z + 1.1f, deskBox.min.z - .4f);
        float y = camera.transform.position.y + .45f;
        Vector3 into = wallOnLeft ? Vector3.left : Vector3.right;   // 보는 사람에게서 벽 쪽으로.
        font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;

        sheet = new GameObject("Panel :: Wall Calendar", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
        sheet.SetParent(transform, false);
        sheet.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        // 살짝 비뚤게 붙어 있다.
        sheet.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.LookRotation(into, Vector3.up) * Quaternion.Euler(0f, 0f, -1.6f));
        sheet.sizeDelta = new Vector2(SheetWidth, SheetHeight) * Pixels; sheet.localScale = Vector3.one / Pixels;

        // 캔버스는 빛을 받지 않으므로 어두운 방에 맞춰 색을 눌러 둔다.
        Panel("Image :: Paper", sheet, new Color(.36f, .345f, .30f), 0f, 0f, 1f, 1f);
        Panel("Image :: Binding", sheet, new Color(.27f, .075f, .07f), 0f, 0f, 1f, .17f);
        Panel("Image :: Nail", sheet, new Color(.10f, .10f, .11f), .485f, .012f, .03f, .026f);
        header = Text("Text :: Month", Panel("Area :: Month", sheet, Color.clear, .06f, .03f, .60f, .13f), 74f, new Color(.62f, .59f, .51f), TextAlignmentOptions.MidlineLeft, true);
        caption = Text("Text :: Caption", Panel("Area :: Caption", sheet, Color.clear, .50f, .06f, .44f, .09f), 26f, new Color(.54f, .48f, .40f), TextAlignmentOptions.MidlineRight, false);
        // 요일 줄.
        var week = Panel("Area :: Weekdays", sheet, Color.clear, .04f, .19f, .92f, .07f);
        for (int i = 0; i < Columns; i++)
        {
            bool rest = Rest(i);
            weekLabels[i] = Text("Text :: Weekday " + i, Panel("Area :: Weekday " + i, week, Color.clear, i / (float)Columns, 0f, 1f / Columns, 1f), 30f,
                i == 0 ? new Color(.36f, .12f, .10f) : rest ? new Color(.13f, .19f, .36f) : new Color(.10f, .10f, .11f), TextAlignmentOptions.Center, true);
        }
        // 날짜 칸: 다섯 줄, 일곱 칸. 일요일(맨 왼쪽)과 토요일(맨 오른쪽) 칸은 조금 어둡다.
        // 첫 줄의 일요일 칸은 비어 있고(1일이 월요일이다), 다섯째 줄에는 28일(일요일) 한 칸만 있다.
        grid = Panel("Area :: Days", sheet, Color.clear, .04f, .27f, .92f, .70f);
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                if (row == Rows - 1 && column > 0) { break; }
                bool rest = Rest(column), blank = row == 0 && column == 0;
                var cell = Panel("Image :: Day Cell", grid, blank ? new Color(.33f, .315f, .275f) : rest ? new Color(.30f, .29f, .255f) : new Color(.385f, .37f, .325f),
                    column / (float)Columns + .006f, row / (float)Rows + .008f, 1f / Columns - .012f, 1f / Rows - .016f);
                if (blank) { continue; }
                Color ink = column == 0 ? new Color(.34f, .11f, .09f) : rest ? new Color(.12f, .17f, .33f) : new Color(.08f, .08f, .09f);
                var label = Text("Text :: Day", Panel("Area :: Day Number", cell, Color.clear, .10f, .06f, .80f, .46f), 50f, ink, TextAlignmentOptions.TopLeft, true);
                if (column == 0) { sundayLabels[row - 1] = label; }
                else if (rest) { saturdayLabels[row] = label; }
                else { dayLabels[row * TradingDays + column - 1] = label; }
            }
        }
        var marks = new GameObject("Image :: Pen Marks", typeof(RectTransform), typeof(CanvasRenderer));
        marks.transform.SetParent(grid, false);
        var area = (RectTransform)marks.transform; area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = area.offsetMax = Vector2.zero;
        ink = marks.AddComponent<CalendarInk>(); ink.raycastTarget = false; ink.color = new Color(.04f, .04f, .06f, .93f);
        return true;
    }

    private void Write(int page)
    {
        var names = GameLocale.Code == "ja" ? WeekJapanese : GameLocale.Code == "en" ? WeekEnglish : WeekKorean;
        for (int i = 0; i < Columns; i++) { GameLocalizedText.Set(weekLabels[i], names[i], true); }
        // 달력의 날짜: 주마다 월요일이 1, 8, 15, 22일이다. 다음 장은 29일부터 이어서 센다(게임 화면에 보이는 날짜와 같다).
        int first = page * Weeks * 7;
        for (int i = 0; i < PageDays; i++) { GameLocalizedText.Set(dayLabels[i], (first + i / TradingDays * 7 + i % TradingDays + 1).ToString(), true); }
        for (int week = 0; week < Weeks; week++)
        {
            GameLocalizedText.Set(saturdayLabels[week], (first + week * 7 + 6).ToString(), true);
            GameLocalizedText.Set(sundayLabels[week], (first + week * 7 + 7).ToString(), true);
        }
        int month = page + 1;
        GameLocalizedText.Set(header, Word(month + "번째 달", "MONTH " + month, month + "か月目"), true);
        GameLocalizedText.Set(caption, Word("장은 월~금", "Market: Mon-Fri", "立会は月〜金"), true);
        ink.Page = page; ink.SetVerticesDirty();
    }

    // 자리는 부모 안의 비율로 준다(왼쪽 위가 0,0).
    private static RectTransform Panel(string name, Transform parent, Color color, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(x, 1f - y - h); rect.anchorMax = new Vector2(x + w, 1f - y); rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (color.a > 0f) { var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; }
        return rect;
    }
    private TextMeshProUGUI Text(string name, Transform parent, float size, Color color, TextAlignmentOptions align, bool bold)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.enableAutoSizing = false; text.color = color;
        text.alignment = align; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
        if (bold) { text.fontStyle = FontStyles.Bold; }
        return text;
    }

    /// <summary>
    /// 달력 위의 펜 자국. 날짜 칸마다 하나씩, 주말 건너뛰기는 주마다 하나씩 그린다.
    /// 모양은 날짜에서 정해지는 흔들림으로 만든다: 같은 날의 선은 언제 그려도 같고, 날마다 서로 다르다.
    /// </summary>
    private sealed class CalendarInk : MaskableGraphic
    {
        /// <summary>이미 다 그어진 날의 수(이 장에서), 지금 긋고 있는 선과 그 진행, 지금 보이는 장.</summary>
        public int Done, Drawing = -1, Page; public float Progress; public bool Sweeping;
        private readonly List<Vector2> path = new();
        private readonly List<int> breaks = new();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var area = rectTransform.rect;
            float cellWidth = area.width / Columns, cellHeight = area.height / Rows;
            for (int i = 0; i < PageDays; i++)
            {
                bool live = Drawing == i;
                if (i >= Done && !live) { continue; }
                int row = i / TradingDays, column = i % TradingDays;
                // 장이 서는 날은 둘째 칸(월)부터 여섯째 칸(금)까지다.
                var cell = new Rect(area.xMin + (column + 1) * cellWidth, area.yMax - (row + 1) * cellHeight, cellWidth, cellHeight);
                uint seed = Hash((uint)(Page * 977 + i * 131 + 17));
                Shape(seed, cell, (int)(seed % 7u));
                Stroke(vh, 8f + seed % 3u, live ? Progress : 1f);
            }
            // 주말: 금요일까지 다 그어진 주는 그 줄 끝의 토요일과 다음 줄 첫머리의 일요일을 잇달아 긋는다.
            // (긋는 동안에는 앞 절반에 토요일, 뒤 절반에 일요일이 그어진다.)
            for (int week = 0; week < Weeks; week++)
            {
                bool live = Drawing == PageDays + week;
                // 금요일을 긋는 동안에는 Done이 아직 금요일 앞에 머물러 있으므로, 금요일이 다 그어진 뒤에야 주말 선이 나온다.
                if (Done < (week + 1) * TradingDays && !live) { continue; }
                uint seed = Hash((uint)(Page * 613 + week * 251 + 5));
                var saturday = new Rect(area.xMin + (Columns - 1) * cellWidth, area.yMax - (week + 1) * cellHeight, cellWidth, cellHeight);
                Sweep(seed, saturday, (int)(seed % 4u));
                Stroke(vh, 9f + seed % 3u, live ? Mathf.Clamp01(Progress * 2f) : 1f);
                var sunday = new Rect(area.xMin, area.yMax - (week + 2) * cellHeight, cellWidth, cellHeight);
                uint second = Hash(seed + 91u);
                Sweep(second, sunday, (int)(second % 4u));
                Stroke(vh, 9f + second % 3u, live ? Mathf.Clamp01(Progress * 2f - 1f) : 1f);
            }
        }

        private static uint Hash(uint value) { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; value ^= value >> 16; return value; }
        /// <summary>씨앗에서 -1~1의 흔들림 하나를 뽑는다.</summary>
        private static float Wobble(ref uint seed) { seed = Hash(seed + 0x9e3779b9u); return (seed & 0xffff) / 32767.5f - 1f; }
        private void Add(Rect box, float x, float y, ref uint seed, float jitter = .05f)
        { path.Add(new Vector2(box.xMin + (x + Wobble(ref seed) * jitter) * box.width, box.yMin + (y + Wobble(ref seed) * jitter) * box.height)); }
        private void Lift() { breaks.Add(path.Count); }

        /// <summary>날짜 칸에 긋는 선의 일곱 가지 모양.</summary>
        private void Shape(uint seed, Rect cell, int kind)
        {
            path.Clear(); breaks.Clear();
            switch (kind)
            {
                case 0:   // 오른쪽 위로 긋는 빗금.
                    Add(cell, .14f, .16f, ref seed); Add(cell, .50f, .52f, ref seed, .03f); Add(cell, .88f, .86f, ref seed); break;
                case 1:   // 오른쪽 아래로 긋는 빗금.
                    Add(cell, .12f, .86f, ref seed); Add(cell, .48f, .50f, ref seed, .03f); Add(cell, .88f, .14f, ref seed); break;
                case 2:   // 가위표(펜을 한 번 뗀다).
                    Add(cell, .16f, .84f, ref seed); Add(cell, .86f, .16f, ref seed); Lift();
                    Add(cell, .16f, .18f, ref seed); Add(cell, .84f, .84f, ref seed); break;
                case 3:   // 지그재그.
                    Add(cell, .12f, .74f, ref seed); Add(cell, .32f, .26f, ref seed); Add(cell, .50f, .74f, ref seed); Add(cell, .68f, .26f, ref seed); Add(cell, .88f, .72f, ref seed); break;
                case 4:   // 구불구불한 가로줄.
                    Add(cell, .08f, .48f, ref seed); Add(cell, .34f, .58f, ref seed, .03f); Add(cell, .62f, .44f, ref seed, .03f); Add(cell, .92f, .54f, ref seed); break;
                case 5:   // 동그라미를 대충 한 바퀴 조금 넘게.
                {
                    float turn = Wobble(ref seed) * .6f, wide = .34f + Wobble(ref seed) * .04f, tall = .32f + Wobble(ref seed) * .04f;
                    for (int i = 0; i <= 13; i++)
                    {
                        float angle = turn + i / 11f * Mathf.PI * 2f;
                        Add(cell, .5f + Mathf.Cos(angle) * wide, .5f + Mathf.Sin(angle) * tall, ref seed, .018f);
                    }
                    break;
                }
                default:  // 두 번 겹쳐 그은 빗금(성의 없이 왔다 갔다).
                    Add(cell, .16f, .22f, ref seed); Add(cell, .84f, .80f, ref seed); Add(cell, .22f, .34f, ref seed); Add(cell, .86f, .66f, ref seed); break;
            }
        }
        /// <summary>쉬는 날 칸을 휙 긋는 선의 네 가지 모양(날짜 칸의 선보다 크고 성의 없다).</summary>
        private void Sweep(uint seed, Rect span, int kind)
        {
            path.Clear(); breaks.Clear();
            switch (kind)
            {
                case 0:   // 칸을 가로지르는 긴 빗금.
                    Add(span, .04f, .12f, ref seed); Add(span, .50f, .50f, ref seed, .03f); Add(span, .97f, .90f, ref seed); break;
                case 1:   // 반대쪽 빗금.
                    Add(span, .04f, .90f, ref seed); Add(span, .52f, .50f, ref seed, .03f); Add(span, .97f, .10f, ref seed); break;
                case 2:   // 큰 물결.
                    Add(span, .04f, .46f, ref seed); Add(span, .28f, .76f, ref seed); Add(span, .50f, .28f, ref seed); Add(span, .72f, .74f, ref seed); Add(span, .96f, .40f, ref seed); break;
                default:  // 커다란 가위표.
                    Add(span, .06f, .88f, ref seed); Add(span, .94f, .12f, ref seed); Lift();
                    Add(span, .06f, .14f, ref seed); Add(span, .94f, .86f, ref seed); break;
            }
        }

        /// <summary>path의 선을 굵기 width로, 전체 길이의 share만큼만 그린다(긋는 중인 선).</summary>
        private void Stroke(VertexHelper vh, float width, float share)
        {
            if (path.Count < 2 || share <= 0f) { return; }
            float total = 0f;
            for (int i = 1; i < path.Count; i++) { if (!breaks.Contains(i)) { total += Vector2.Distance(path[i - 1], path[i]); } }
            float left = total * share, half = width * .5f;
            Color32 tint = color;
            for (int i = 1; i < path.Count && left > 0f; i++)
            {
                if (breaks.Contains(i)) { continue; }
                Vector2 from = path[i - 1], to = path[i];
                float length = Vector2.Distance(from, to);
                if (length <= .01f) { continue; }
                if (length > left) { to = Vector2.Lerp(from, to, left / length); }
                left -= length;
                Vector2 along = (to - from).normalized, side = new Vector2(-along.y, along.x) * half;
                // 끝을 조금 넘겨 그려서 꺾이는 자리가 벌어지지 않게 한다.
                from -= along * half * .6f; to += along * half * .6f;
                int start = vh.currentVertCount;
                vh.AddVert(from - side, tint, Vector2.zero); vh.AddVert(from + side, tint, Vector2.zero);
                vh.AddVert(to + side, tint, Vector2.zero); vh.AddVert(to - side, tint, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
