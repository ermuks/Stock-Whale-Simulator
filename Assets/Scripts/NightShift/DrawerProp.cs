using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 책상 서랍. 기능은 없고, 열어 본 사람이 피식 웃고 닫는 자리다.
// 서랍을 보고 F를 누르면 서랍이 열리고 시선이 그 위로 옮겨 간다.
// 안에는 도장이 하나 모자란 치킨 쿠폰, 꽝 난 복권, 오늘의 운세 쪽지, 작은 고래 장난감이 있다.
// 다시 F나 Esc를 누르면 닫힌다. 게임 상태는 아무것도 바꾸지 않는다. 씬 파일도 그대로다(실행 중에 만든다).
public sealed class DrawerProp : MonoBehaviour
{
    public static DrawerProp Instance { get; private set; }
    private const float SlideSeconds = .5f, PullShare = .62f, Floor = .12f;
    public const KeyCode ToggleKey = KeyCode.F;
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    private Transform drawer;
    private Renderer drawerRenderer;
    private Vector3 closedPosition, pull, interior, handleOffset;
    private float slide;
    private Light lamp;
    private GameObject hintRoot;
    private TextMeshProUGUI hint;
    private TMP_FontAsset font;
    // 종이에 적힌 글자. 한국어·영어·일본어를 각각 적는다(직역이 아니다).
    private readonly List<(TextMeshProUGUI label, string korean, string english, string japanese)> writing = new();
    private int writtenRevision = -1;
    public bool Open { get; private set; }
    private bool soundedOpen, soundsReady;
    public bool Hovered { get; private set; }
    public bool Found => drawer != null;

    // 오늘의 운세 열여섯 가지. 서랍을 닫을 때마다 다른 것으로 바뀐다(열려 있는 동안에는 바뀌지 않는다).
    // 한 가지는 두 줄의 말과 행운의 무언가 한 줄이다. 한국어·영어·일본어를 각각 적었다.
    private static readonly string[][] FortunesKorean =
    {
        new[] { "사면 내리고", "팔면 오릅니다", "행운의 아이템: 서랍" },
        new[] { "오늘은 아무것도", "안 하는 게 수익입니다", "행운의 아이템: 의자" },
        new[] { "버티면 이깁니다", "(근거는 없습니다)", "행운의 숫자: -30%" },
        new[] { "귀인이 나타납니다", "리딩방에서는 아닙니다", "행운의 방향: 출구" },
        new[] { "물타기 전에", "물부터 드세요", "행운의 아이템: 생수" },
        new[] { "고점은 언제나", "당신이 산 그 자리", "행운의 색: 빨강이었으면" },
        new[] { "큰돈이 들어옵니다", "남의 계좌로", "행운의 아이템: 남의 계좌" },
        new[] { "손절은 빠를수록 좋다고", "어제도 말했습니다", "행운의 시간: 어제" },
        new[] { "오늘 제일 오를 종목은", "어제 당신이 판 그것", "행운의 아이템: 손수건" },
        new[] { "세력은 당신 편입니다", "당신이 세력이니까요", "행운의 동물: 고래" },
        new[] { "차트는 과학입니다", "별자리도 그렇다더군요", "행운의 도형: 삼각형" },
        new[] { "장기 투자 하세요", "물려서 하는 거 말고", "행운의 기간: 10년" },
        new[] { "치킨값만 벌자던", "초심을 떠올리세요", "행운의 음식: 치킨" },
        new[] { "잠을 자야", "내일도 잃을 수 있습니다", "행운의 키: Z" },
        new[] { "이 쪽지를 믿느니", "동전을 던지세요", "행운의 아이템: 동전" },
        new[] { "대길(大吉)", "단, 매매하지 않을 경우", "행운의 단추: 없음" },
    };
    private static readonly string[][] FortunesEnglish =
    {
        new[] { "Buy and it drops.", "Sell and it soars.", "Lucky item: this drawer" },
        new[] { "Your best trade today", "is no trade at all.", "Lucky item: your chair" },
        new[] { "Diamond hands always win.", "(Source: none.)", "Lucky number: -30%" },
        new[] { "A wise friend is coming.", "Not from a tip room.", "Lucky direction: the exit" },
        new[] { "Before you average down,", "drink some water.", "Lucky item: tap water" },
        new[] { "The top is wherever", "you happened to buy.", "Lucky color: not red" },
        new[] { "Big money is on its way", "to someone else's account.", "Lucky item: their account" },
        new[] { "Cut your losses early.", "We said so yesterday too.", "Lucky time: yesterday" },
        new[] { "Today's biggest winner is", "the one you sold yesterday.", "Lucky item: tissues" },
        new[] { "The whales are on your side.", "You are the whale.", "Lucky animal: whale" },
        new[] { "Charts are a science.", "So is astrology, they say.", "Lucky shape: triangle" },
        new[] { "Invest for the long term.", "On purpose, this time.", "Lucky period: 10 years" },
        new[] { "Remember when you only", "wanted lunch money?", "Lucky food: fried chicken" },
        new[] { "Get some sleep,", "so you can lose tomorrow too.", "Lucky key: Z" },
        new[] { "Don't trust this note.", "Flip a coin instead.", "Lucky item: a coin" },
        new[] { "GREAT FORTUNE", "Valid only if you don't trade.", "Lucky button: none" },
    };
    private static readonly string[][] FortunesJapanese =
    {
        new[] { "買えば下がり", "売れば上がる", "ラッキーアイテム：引き出し" },
        new[] { "今日は何もしないのが", "いちばんの利益です", "ラッキーアイテム：椅子" },
        new[] { "握り続ければ勝てます", "（根拠はありません）", "ラッキーナンバー：-30%" },
        new[] { "頼れる人が現れます", "煽り部屋にはいません", "ラッキー方角：出口" },
        new[] { "ナンピンの前に", "まず水を飲みましょう", "ラッキーアイテム：水道水" },
        new[] { "天井はいつも", "あなたが買ったところ", "ラッキーカラー：赤だったらいいな" },
        new[] { "大金が舞い込みます", "他人の口座に", "ラッキーアイテム：他人の口座" },
        new[] { "損切りは早いほどいいと", "昨日も言いました", "ラッキータイム：昨日" },
        new[] { "今日いちばん上がるのは", "昨日あなたが売った銘柄", "ラッキーアイテム：ハンカチ" },
        new[] { "仕手筋はあなたの味方です", "あなたが仕手筋ですから", "ラッキーアニマル：クジラ" },
        new[] { "チャートは科学です", "星占いもそうらしいです", "ラッキー図形：三角" },
        new[] { "長期投資をしましょう", "塩漬けではなく", "ラッキー期間：10年" },
        new[] { "昼めし代だけ稼ぐはずだった", "初心を思い出しましょう", "ラッキーフード：チキン" },
        new[] { "しっかり寝ましょう", "明日も負けるために", "ラッキーキー：Z" },
        new[] { "この紙を信じるより", "コインを投げましょう", "ラッキーアイテム：コイン" },
        new[] { "大吉", "ただし売買しない場合に限る", "ラッキーボタン：なし" },
    };
    private readonly TextMeshProUGUI[] fortuneLines = new TextMeshProUGUI[3];
    private int fortune = -1;
    private bool fortuneRead;
    /// <summary>지금 쪽지에 적힌 운세의 번호(0~15).</summary>
    public int FortuneIndex => fortune;
    public const int FortuneCount = 16;
    /// <summary>지금과 다른 운세를 하나 고른다.</summary>
    public void DrawFortune()
    {
        int next = Random.Range(0, FortuneCount - 1);
        if (fortune >= 0 && next >= fortune) { next++; }
        fortune = next; writtenRevision = -1;
    }
    private void WriteFortune()
    {
        var lines = (GameLocale.Code == "ja" ? FortunesJapanese : GameLocale.Code == "en" ? FortunesEnglish : FortunesKorean)[Mathf.Clamp(fortune, 0, FortuneCount - 1)];
        for (int i = 0; i < fortuneLines.Length; i++) { GameLocalizedText.Set(fortuneLines[i], lines[i], true); }
    }

    /// <summary>원 표시를 띄울 자리(서랍 앞면의 손잡이). 서랍이 아직 만들어지지 않았거나 열려 있으면 false.</summary>
    public bool MarkerPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (drawer == null || drawerRenderer == null || Open || slide > 0f) { return false; }
        point = drawer.position + handleOffset;
        return true;
    }
    /// <summary>
    /// 손잡이 자리(서랍 위치에서 잰 거리). 손잡이는 서랍 모델에서 플레이어 쪽으로 가장 튀어나온 부분이다.
    /// 모델의 꼭짓점을 읽을 수 있으면 그 튀어나온 꼭짓점들의 가운데를, 읽을 수 없으면 앞면의 가운데를 쓴다.
    /// 예전에는 서랍 상자의 한가운데(서랍 속)를 썼다. 그래서 원이 서랍 안쪽에 떠 보였다.
    /// </summary>
    private Vector3 FindHandle(Bounds box, Vector3 toPlayer, float depth)
    {
        Vector3 face = box.center + toPlayer * depth * .5f;
        var filter = drawer.GetComponent<MeshFilter>(); var mesh = filter != null ? filter.sharedMesh : null;
        if (mesh != null && mesh.isReadable)
        {
            var points = mesh.vertices; float far = float.MinValue;
            var world = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) { world[i] = drawer.TransformPoint(points[i]); far = Mathf.Max(far, Vector3.Dot(world[i], toPlayer)); }
            Vector3 sum = Vector3.zero; int count = 0; float band = depth * .012f;
            foreach (var p in world) { if (Vector3.Dot(p, toPlayer) >= far - band) { sum += p; count++; } }
            if (count > 0) { face = sum / count; }
        }
        // 앞면에 딱 붙으면 원이 반쯤 묻혀 보일 수 있어 아주 조금 앞으로 뺀다.
        return face + toPlayer * depth * .01f - drawer.position;
    }
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    void Update()
    {
        var views = DeskViewController.Instance;
        if (views == null || !views.Ready) { return; }
        if (drawer == null)
        {
            var item = GameObject.Find("desk_drawer");
            if (item == null) { return; }
            Build(item.transform, views.ViewCamera);
        }
        // 다른 화면으로 넘어가거나 자리로 돌아가면 서랍도 닫힌다.
        if (Open && !views.IsCustomView) { Open = false; }
        // 여닫는 소리도 미리 읽어 둔다(처음 열 때 파일을 읽느라 멎지 않게).
        if (!soundsReady && Sfx.Instance != null) { soundsReady = true; Sfx.Warm("DrawerOpen"); Sfx.Warm("DrawerClose"); }
        if (Open != soundedOpen) { soundedOpen = Open; Sfx.Play(Open ? "DrawerOpen" : "DrawerClose", .8f); }
        bool free = !PauseMenuController.BlocksInput && !MouseCameraController.TextEntryActive && Time.timeScale > 0f && !RoomShell.Cinematic;
        // 자리에서 서랍을 바라본 채 F를 누르면 열리고, 열어 둔 채 F를 누르면 닫힌다(침대·문과 같은 방식). 바라보지 않으면 열리지 않는다.
        bool key = free && Input.GetKeyDown(ToggleKey);
        Hovered = false;
        if (Open)
        {
            if (key) { views.ExitView(); Open = false; }
        }
        else if (free && !DeskViewController.OwnsView && slide <= 0f)
        {
            Ray ray = views.ViewCamera.ScreenPointToRay(MouseCameraController.GetAimScreenPosition());
            // 책상 위의 휴대폰을 내려다보는 시선은 그 아래의 서랍도 지나간다. 그때는 휴대폰이 먼저다.
            Hovered = views.Hovered == DeskViewTarget.None && !DeskPhone.Aiming && drawerRenderer.bounds.IntersectRay(ray);
            if (Hovered && key) { Open = views.FocusCustom(ViewPose(views.ViewCamera), 52f); }
        }
        // 서랍은 부드럽게 미끄러져 나오고 들어간다.
        float target = Open ? 1f : 0f;
        slide = Mathf.MoveTowards(slide, target, Time.unscaledDeltaTime / SlideSeconds);
        float eased = slide * slide * (3f - 2f * slide);
        drawer.position = closedPosition + pull * eased;
        lamp.enabled = slide > 0f; lamp.intensity = eased * .9f;
        bool show = Hovered || Open;
        if (hintRoot.activeSelf != show) { hintRoot.SetActive(show); }
        if (show) { GameLocalizedText.Set(hint, ToggleKey + "   " + (Open ? Word("서랍 닫기", "Close the drawer", "引き出しを閉める") : Word("서랍 열기", "Open the drawer", "引き出しを開ける")), true); }
        // 운세는 서랍이 다 닫힌 뒤에 바뀐다. 열려 있는 동안 글자가 바뀌는 모습은 보이지 않는다.
        if (Open) { fortuneRead = true; }
        else if (fortuneRead && slide <= 0f) { fortuneRead = false; DrawFortune(); }
        // 글자는 서랍이 닫혀 있을 때 미리 써 둔다(처음 만들 때, 언어가 바뀔 때, 닫힌 뒤 운세가 바뀔 때).
        // 예전에는 서랍이 열리기 시작하는 프레임에 스무 줄 남짓을 한꺼번에 처음 썼다. 그때 글자 모양을 글꼴 판에 새로 굽고
        // 글자 메시를 한꺼번에 만드느라 여는 순간 화면이 잠깐 멎었다.
        if (writtenRevision != GameLocale.Revision)
        {
            writtenRevision = GameLocale.Revision;
            foreach (var line in writing) { GameLocalizedText.Set(line.label, Word(line.korean, line.english, line.japanese), true); }
            WriteFortune();
        }
    }

    /// <summary>열린 서랍 위에서 내려다보는 자리.</summary>
    private Pose ViewPose(Camera camera)
    {
        Vector3 toPlayer = pull.normalized, center = interior + pull + toPlayer * pull.magnitude * .22f + Vector3.up * .25f;
        Vector3 eye = center + Vector3.up * 2.3f + toPlayer * .75f;
        return new Pose(eye, Quaternion.LookRotation(center - eye, -toPlayer));
    }

    private void Build(Transform item, Camera camera)
    {
        drawer = item; drawerRenderer = item.GetComponent<Renderer>(); closedPosition = item.position;
        // 서랍에는 충돌체가 달려 있다. 강체 없이 충돌체를 움직이면 물리 쪽이 움직일 때마다 고정 물체 목록을 다시 짠다.
        // 움직이는 물체라고 알려 두면(물리에 밀리지 않는 강체) 그 비용이 들지 않는다.
        if (item.GetComponent<Collider>() != null && item.GetComponent<Rigidbody>() == null)
        { var body = item.gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; }
        var box = drawerRenderer.bounds;
        // 서랍은 플레이어 쪽으로 나온다.
        Vector3 toPlayer = camera.transform.position - box.center; toPlayer.y = 0f;
        toPlayer = Mathf.Abs(toPlayer.z) >= Mathf.Abs(toPlayer.x) ? new Vector3(0f, 0f, Mathf.Sign(toPlayer.z)) : new Vector3(Mathf.Sign(toPlayer.x), 0f, 0f);
        float depth = Mathf.Abs(toPlayer.z) > 0f ? box.size.z : box.size.x, width = Mathf.Abs(toPlayer.z) > 0f ? box.size.x : box.size.z;
        pull = toPlayer * depth * PullShare;
        handleOffset = FindHandle(box, toPlayer, depth);
        Debug.Log($"[서랍] 원 표시 자리: 손잡이 {drawer.position + handleOffset} | 서랍 가운데 {box.center} | 앞면까지 {depth * .5f:0.###}");
        interior = new Vector3(box.center.x, box.min.y + Floor, box.center.z);
        font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;

        // 내용물은 서랍에 붙어서 함께 움직인다. 자리는 세계 기준으로 잡는다: 가로(x), 플레이어 쪽(-z).
        var contents = new GameObject("Area :: Drawer Contents").transform;
        contents.SetPositionAndRotation(interior, Quaternion.LookRotation(-toPlayer, Vector3.up));
        contents.SetParent(drawer, true);
        float unit = Mathf.Min(width, depth) / 1.58f;
        Lottery(contents, new Vector3(-.24f, .016f, -.12f) * unit, -7f, unit);
        Coupon(contents, new Vector3(.22f, .022f, -.44f) * unit, 9f, unit);
        Fortune(contents, new Vector3(-.36f, .028f, -.50f) * unit, -12f, unit);
        DrawFortune();
        Whale(contents, new Vector3(.34f, 0f, .22f) * unit, unit);

        // 열렸을 때만 켜지는 희미한 빛. 어두운 방에서 서랍 속이 겨우 보일 만큼이다.
        lamp = new GameObject("Light :: Drawer").AddComponent<Light>();
        lamp.transform.SetParent(contents, false); lamp.transform.localPosition = new Vector3(0f, 1.3f * unit, -.5f * unit);
        lamp.type = LightType.Point; lamp.range = 3.2f * unit; lamp.color = new Color(.78f, .74f, .62f); lamp.shadows = LightShadows.None; lamp.enabled = false;

        hintRoot = new GameObject("Area :: Drawer Hint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        hintRoot.transform.SetParent(transform, false);
        var canvas = hintRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 28800;
        var scaler = hintRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        var pill = Panel("Panel :: Drawer Hint", hintRoot.transform, new Color(.04f, .055f, .07f, .92f));
        pill.anchorMin = pill.anchorMax = new Vector2(.5f, 0f); pill.pivot = new Vector2(.5f, 0f); pill.anchoredPosition = new Vector2(0f, 46f); pill.sizeDelta = new Vector2(330f, 54f);
        hint = Text("Text :: Drawer Hint", pill, 24f, new Color(.90f, .92f, .94f, 1f), TextAlignmentOptions.Center);
        hintRoot.SetActive(false);
    }

    // ── 납작한 물건: 위를 보고 누운 작은 캔버스. 캔버스는 빛을 받지 않으므로 어두운 방에 맞춰 색을 눌러 둔다. ──
    private RectTransform Sheet(string name, Transform parent, Vector3 position, float turn, float width, float height)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
        root.SetParent(parent, false); root.localPosition = position; root.localRotation = Quaternion.Euler(90f, turn, 0f);
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        // 1단위를 400픽셀로 본다.
        root.sizeDelta = new Vector2(width, height) * 400f; root.localScale = Vector3.one / 400f;
        return root;
    }
    private static RectTransform Panel(string name, Transform parent, Color color)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }
    private static RectTransform Box(string name, Transform parent, Color color, float x, float y, float w, float h)
    {
        var rect = Panel(name, parent, color);
        rect.anchorMin = new Vector2(x, 1f - y - h); rect.anchorMax = new Vector2(x + w, 1f - y);
        return rect;
    }
    private TextMeshProUGUI Text(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.enableAutoSizing = false; text.color = color;
        text.alignment = align; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
        return text;
    }
    /// <summary>종이 위의 한 줄. 자리는 종이 안의 비율로 준다.</summary>
    private TextMeshProUGUI Line(Transform sheet, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align, string korean, string english, string japanese, bool bold = false)
    {
        var label = Text("Text :: Line", Box("Area :: Line", sheet, Color.clear, x, y, w, h), size, color, align);
        if (bold) { label.fontStyle = FontStyles.Bold; }
        writing.Add((label, korean, english, japanese));
        return label;
    }

    /// <summary>치킨 쿠폰. 열 개를 모으면 한 마리인데 아홉 개에서 멈춰 있다.</summary>
    private void Coupon(Transform parent, Vector3 position, float turn, float unit)
    {
        var sheet = Sheet("Panel :: Chicken Coupon", parent, position, turn, .78f * unit, .46f * unit);
        Panel("Image :: Card", sheet, new Color(.46f, .33f, .12f));
        Box("Image :: Card Inside", sheet, new Color(.50f, .46f, .36f), .025f, .045f, .95f, .91f);
        Color ink = new Color(.27f, .12f, .05f);
        Line(sheet, .05f, .07f, .90f, .22f, 30f, ink, TextAlignmentOptions.Center, "치킨 쿠폰", "CHICKEN CARD", "チキン スタンプカード", true);
        Line(sheet, .05f, .27f, .90f, .14f, 17f, ink, TextAlignmentOptions.Center, "도장 10개 = 후라이드 한 마리", "10 stamps = 1 free bucket", "スタンプ10個で1羽サービス");
        // 도장 열 칸. 아홉 칸에만 찍혀 있다.
        for (int i = 0; i < 10; i++)
        {
            float x = .075f + i % 5 * .175f, y = .45f + i / 5 * .22f;
            Box("Image :: Stamp Slot " + i, sheet, new Color(.36f, .33f, .26f), x, y, .15f, .18f);
            if (i < 9) { Box("Image :: Stamp " + i, sheet, new Color(.48f, .13f, .08f, .92f), x + .02f, y + .025f, .11f, .13f).localRotation = Quaternion.Euler(0f, 0f, (i * 53 % 31) - 15f); }
        }
        Line(sheet, .05f, .885f, .90f, .09f, 13f, new Color(.30f, .27f, .20f), TextAlignmentOptions.Center, "※ 본점 폐업으로 사용 불가", "* Store closed. No longer valid.", "※ 本店閉店のためご利用いただけません");
    }

    /// <summary>복권. 꽝이다. 그래도 아래에 다짐이 적혀 있다.</summary>
    private void Lottery(Transform parent, Vector3 position, float turn, float unit)
    {
        var sheet = Sheet("Panel :: Lottery Slip", parent, position, turn, .56f * unit, .92f * unit);
        Panel("Image :: Slip", sheet, new Color(.47f, .46f, .42f));
        Box("Image :: Slip Header", sheet, new Color(.13f, .26f, .36f), 0f, 0f, 1f, .13f);
        Line(sheet, 0f, 0f, 1f, .13f, 28f, new Color(.72f, .76f, .78f), TextAlignmentOptions.Center, "행운 복권", "LUCKY LOTTO", "ラッキー宝くじ", true);
        Color ink = new Color(.14f, .14f, .13f);
        string[] rows = { "07  13  22  31  38  44", "03  09  17  26  35  41", "11  19  24  29  33  45" };
        for (int i = 0; i < rows.Length; i++)
        { Line(sheet, .06f, .18f + i * .095f, .88f, .08f, 19f, ink, TextAlignmentOptions.Center, rows[i], rows[i], rows[i]); }
        // 큼지막한 붉은 도장.
        var stamp = Box("Area :: Lottery Stamp", sheet, Color.clear, .16f, .50f, .68f, .20f); stamp.localRotation = Quaternion.Euler(0f, 0f, 14f);
        var frame = Box("Image :: Stamp Frame", stamp, new Color(.52f, .10f, .08f, .9f), 0f, 0f, 1f, 1f);
        Box("Image :: Stamp Inside", frame, new Color(.47f, .46f, .42f), .035f, .08f, .93f, .84f);
        var word = Text("Text :: Lottery Stamp", stamp, 46f, new Color(.52f, .10f, .08f), TextAlignmentOptions.Center); word.fontStyle = FontStyles.Bold;
        writing.Add((word, "꽝", "NOPE", "ハズレ"));
        // 손으로 적은 다짐.
        Line(sheet, .06f, .77f, .88f, .09f, 18f, new Color(.10f, .16f, .34f), TextAlignmentOptions.Center, "다음 주엔 진짜 된다", "Next week. For real.", "来週こそ絶対当たる").fontStyle = FontStyles.Italic;
        Line(sheet, .06f, .87f, .88f, .08f, 13f, new Color(.22f, .22f, .20f), TextAlignmentOptions.Center, "(작년에도 그렇게 적었음)", "(wrote that last year too)", "(去年もそう書いた)");
    }

    /// <summary>오늘의 운세 쪽지. 이 게임에서 제일 쓸모없는 조언이 적혀 있고, 서랍을 닫을 때마다 바뀐다.</summary>
    private void Fortune(Transform parent, Vector3 position, float turn, float unit)
    {
        var sheet = Sheet("Panel :: Fortune Slip", parent, position, turn, .52f * unit, .40f * unit);
        Panel("Image :: Sticky", sheet, new Color(.50f, .46f, .20f));
        Color ink = new Color(.12f, .11f, .08f);
        Line(sheet, .06f, .08f, .88f, .20f, 20f, ink, TextAlignmentOptions.Center, "오늘의 운세", "TODAY'S FORTUNE", "今日の運勢", true);
        // 두 줄의 말과 행운의 무언가. 내용은 WriteFortune이 넣는다. 긴 말도 쪽지 안에 들어가게 글자 크기를 스스로 줄인다.
        fortuneLines[0] = Text("Text :: Fortune First", Box("Area :: Fortune First", sheet, Color.clear, .05f, .32f, .90f, .21f), 21f, ink, TextAlignmentOptions.Center);
        fortuneLines[1] = Text("Text :: Fortune Second", Box("Area :: Fortune Second", sheet, Color.clear, .05f, .53f, .90f, .21f), 21f, ink, TextAlignmentOptions.Center);
        fortuneLines[2] = Text("Text :: Fortune Lucky", Box("Area :: Fortune Lucky", sheet, Color.clear, .05f, .78f, .90f, .14f), 13f, new Color(.26f, .23f, .12f), TextAlignmentOptions.Center);
        foreach (var line in fortuneLines) { line.enableAutoSizing = true; line.fontSizeMax = line.fontSize; line.fontSizeMin = 8f; line.overflowMode = TextOverflowModes.Truncate; }
    }

    // ── 입체 물건 ────────────────────────────────────────────────────────────
    private static Material Lit(Color color, float smoothness)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader); material.SetColor("_BaseColor", color); material.color = color; material.SetFloat("_Smoothness", smoothness);
        return material;
    }
    private static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        var item = GameObject.CreatePrimitive(type); item.name = name;
        Destroy(item.GetComponent<Collider>());
        item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localRotation = rotation; item.transform.localScale = scale;
        item.GetComponent<Renderer>().sharedMaterial = material;
        return item.transform;
    }
    /// <summary>작은 고래 장난감. 이 게임의 이름이 된 그 고래다. 물을 뿜고 있다.</summary>
    private static void Whale(Transform parent, Vector3 position, float unit)
    {
        var blue = Lit(new Color(.16f, .30f, .46f), .55f); var belly = Lit(new Color(.58f, .62f, .64f), .4f);
        var dark = Lit(new Color(.02f, .02f, .03f), .7f); var spray = Lit(new Color(.62f, .74f, .80f), .2f);
        var root = new GameObject("Model :: Toy Whale").transform; root.SetParent(parent, false);
        root.localPosition = position + Vector3.up * .105f * unit; root.localRotation = Quaternion.Euler(0f, -28f, 0f); root.localScale = Vector3.one * unit;
        Shape(PrimitiveType.Sphere, "Body", root, Vector3.zero, new Vector3(.40f, .21f, .22f), Quaternion.identity, blue);
        Shape(PrimitiveType.Sphere, "Belly", root, new Vector3(.02f, -.035f, 0f), new Vector3(.34f, .16f, .20f), Quaternion.identity, belly);
        // 꼬리: 몸통 뒤로 가늘어지다가 양쪽으로 갈라진다.
        Shape(PrimitiveType.Sphere, "Tail Stem", root, new Vector3(-.20f, .02f, 0f), new Vector3(.20f, .09f, .09f), Quaternion.Euler(0f, 0f, -18f), blue);
        Shape(PrimitiveType.Sphere, "Fluke Left", root, new Vector3(-.30f, .075f, .05f), new Vector3(.12f, .03f, .09f), Quaternion.Euler(0f, 35f, -20f), blue);
        Shape(PrimitiveType.Sphere, "Fluke Right", root, new Vector3(-.30f, .075f, -.05f), new Vector3(.12f, .03f, .09f), Quaternion.Euler(0f, -35f, -20f), blue);
        Shape(PrimitiveType.Sphere, "Fin Left", root, new Vector3(.05f, -.05f, .11f), new Vector3(.11f, .025f, .06f), Quaternion.Euler(25f, 20f, 0f), blue);
        Shape(PrimitiveType.Sphere, "Fin Right", root, new Vector3(.05f, -.05f, -.11f), new Vector3(.11f, .025f, .06f), Quaternion.Euler(-25f, -20f, 0f), blue);
        Shape(PrimitiveType.Sphere, "Eye Left", root, new Vector3(.13f, .025f, .092f), Vector3.one * .022f, Quaternion.identity, dark);
        Shape(PrimitiveType.Sphere, "Eye Right", root, new Vector3(.13f, .025f, -.092f), Vector3.one * .022f, Quaternion.identity, dark);
        // 물줄기: 머리 위로 작은 방울 셋.
        Shape(PrimitiveType.Cylinder, "Spout", root, new Vector3(.07f, .135f, 0f), new Vector3(.012f, .035f, .012f), Quaternion.identity, spray);
        Shape(PrimitiveType.Sphere, "Drop Middle", root, new Vector3(.07f, .185f, 0f), Vector3.one * .03f, Quaternion.identity, spray);
        Shape(PrimitiveType.Sphere, "Drop Left", root, new Vector3(.045f, .170f, .02f), Vector3.one * .022f, Quaternion.identity, spray);
        Shape(PrimitiveType.Sphere, "Drop Right", root, new Vector3(.095f, .170f, -.02f), Vector3.one * .022f, Quaternion.identity, spray);
    }
}
