using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>업종. 기업의 업종 픽토그램과 로고 바탕 도형을 정한다.</summary>
public enum UiSector { Insurance, AI, Food, Bio, Display, Finance, Electronics, Semiconductor, EV, Tech }

/// <summary>
/// 정보를 운반하는 그림들(업종 픽토그램 · 작전 상징 · 기업 로고 · 채팅 아바타 · 메시지 표지).
/// 그림은 한 장의 아틀라스(Resources/UI/Pictograms/Pictograms.png, Tools/UiArt/BakePictograms.ps1로 굽는다)에 흰색으로 들어 있고,
/// 색은 쓰는 자리에서 Image.color로 칠한다.
/// 로고와 아바타는 손으로 그리지 않고 규칙으로 조립한다: 업종이 바탕 도형을, 이름(또는 봇 번호)의 해시가 색·분할·잘린 자리를 정한다.
/// 같은 회사는 언제나 같은 로고, 같은 사람은 언제나 같은 얼굴이다. 새로 생기는 소기업도 따로 그릴 것이 없다.
/// </summary>
public static class UiPictograms
{
    // 아틀라스의 칸 번호(BakePictograms.ps1의 표와 같아야 한다).
    public const int SectorFirst = 0, OpFirst = 10, LogoFirst = 16, Ring = 26, AvatarFirst = 27, AvatarShapes = 6, Dot = 33, Bar = 34;
    public const int MarkSystem = 35, MarkAnalysis = 36, MarkLeading = 37, MarkProof = 38, CutFirst = 39, Cuts = 3, OpHeavyFirst = 42;
    private const int Columns = 8;

    private static Texture2D atlas;
    private static readonly Dictionary<int, Sprite> cells = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { atlas = null; cells.Clear(); sectors.Clear(); logoAtlas = emblemAtlas = null; bakedLogos.Clear(); Array.Clear(emblems, 0, emblems.Length); }

    /// <summary>아틀라스의 한 칸. 아틀라스가 없으면 null(그림 없이 글자만 보인다).</summary>
    public static Sprite Cell(int index)
    {
        if (cells.TryGetValue(index, out var sprite)) { return sprite; }
        atlas ??= Resources.Load<Texture2D>("UI/Pictograms/Pictograms");
        if (atlas == null) { return null; }
        float size = atlas.width / (float)Columns;
        int row = index / Columns, column = index % Columns;
        // 텍스처의 원점은 왼쪽 아래다. 굽는 쪽은 왼쪽 위부터 채웠다.
        sprite = Sprite.Create(atlas, new Rect(column * size, atlas.height - (row + 1) * size, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        sprite.name = "Pictogram " + index; sprite.hideFlags = HideFlags.DontSave;
        cells[index] = sprite; return sprite;
    }

    // ── 업종 ──
    private static readonly Dictionary<string, UiSector> sectors = new(StringComparer.Ordinal);
    private static readonly (string word, UiSector sector)[] SectorWords =
    {
        ("보험", UiSector.Insurance), ("인공지능", UiSector.AI), ("에이아이", UiSector.AI), ("식품", UiSector.Food), ("푸드", UiSector.Food),
        ("바이오", UiSector.Bio), ("제약", UiSector.Bio), ("디스플레이", UiSector.Display), ("금융", UiSector.Finance), ("은행", UiSector.Finance), ("증권", UiSector.Finance),
        ("반도체", UiSector.Semiconductor), ("하이테크", UiSector.Semiconductor), ("전자", UiSector.Electronics), ("전기차", UiSector.EV), ("모터", UiSector.EV), ("자동차", UiSector.EV),
        ("기술", UiSector.Tech), ("테크", UiSector.Tech), ("소프트", UiSector.Tech)
    };
    /// <summary>종목의 업종. 기사용 기업 정보의 업종을 먼저 보고, 없으면 회사 이름의 낱말로, 그래도 없으면 이름의 해시로 정한다.</summary>
    public static UiSector SectorOf(Stock stock)
    {
        if (stock == null) { return UiSector.Tech; }
        string key = stock.symbol ?? stock.stockName ?? "";
        if (sectors.TryGetValue(key, out var found)) { return found; }
        string written = null;
        try { written = NewsManager.Instance != null && NewsManager.Instance.Writing != null ? NewsManager.Instance.Writing.Profile(stock.symbol)?.sector : null; } catch (Exception) { written = null; }
        found = Match(written) ?? Match(stock.stockName) ?? (UiSector)(int)(Hash(key) % 10u);
        // 기사 정보가 아직 안 읽혔으면 저장하지 않는다(다음에 다시 본다).
        if (NewsManager.Instance != null) { sectors[key] = found; }
        return found;
    }
    private static UiSector? Match(string text)
    {
        if (string.IsNullOrEmpty(text)) { return null; }
        foreach (var (word, sector) in SectorWords) { if (text.Contains(word)) { return sector; } }
        return null;
    }
    public static Sprite SectorIcon(UiSector sector) => Cell(SectorFirst + (int)sector);
    public static string SectorName(UiSector sector)
    {
        string code = GameLocale.Code;
        return sector switch
        {
            UiSector.Insurance => code == "ja" ? "保険" : code == "en" ? "Insurance" : "보험",
            UiSector.AI => code == "ja" ? "AI" : code == "en" ? "AI" : "인공지능",
            UiSector.Food => code == "ja" ? "食品" : code == "en" ? "Food" : "식품",
            UiSector.Bio => code == "ja" ? "バイオ" : code == "en" ? "Biotech" : "바이오",
            UiSector.Display => code == "ja" ? "ディスプレイ" : code == "en" ? "Displays" : "디스플레이",
            UiSector.Finance => code == "ja" ? "金融" : code == "en" ? "Finance" : "금융",
            UiSector.Electronics => code == "ja" ? "電子" : code == "en" ? "Electronics" : "전자",
            UiSector.Semiconductor => code == "ja" ? "半導体" : code == "en" ? "Chips" : "반도체",
            UiSector.EV => code == "ja" ? "EV" : code == "en" ? "EVs" : "전기차",
            _ => code == "ja" ? "テック" : code == "en" ? "Tech" : "기술"
        };
    }

    // ── 작전 상징 ──
    public static Sprite OpIcon(PlayerTactics.Tool tool, bool heavy = false)
    {
        int index = tool switch
        {
            PlayerTactics.Tool.Ignite => 0, PlayerTactics.Tool.Unload => 1, PlayerTactics.Tool.Slam => 2,
            PlayerTactics.Tool.Close => 3, PlayerTactics.Tool.Wash => 4, _ => 5
        };
        return Cell((heavy ? OpHeavyFirst : OpFirst) + index);
    }

    // ── 색 ──
    // 로고와 아바타에 쓰는 색. 채도를 낮춘 열두 빛깔이라 어두운 방에서 튀지 않고, 등락의 빨강·파랑과도 겹치지 않게 그 둘레는 비켜 간다.
    private static readonly float[] Hues = { .04f, .09f, .13f, .2f, .3f, .38f, .45f, .5f, .55f, .7f, .76f, .84f };
    public static Color Tone(uint hash, float saturation, float value) => Color.HSVToRGB(Hues[hash % (uint)Hues.Length], saturation, value);
    public static uint Hash(string text)
    {
        unchecked
        {
            uint h = 2166136261u;
            foreach (char c in text ?? "") { h = (h ^ c) * 16777619u; }
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return h;
        }
    }

    // ── 로고 ──
    /// <summary>
    /// holder 안에 그 회사의 로고를 조립한다(이미 있으면 다시 칠한다). 층은 셋:
    /// 바탕 도형(업종) · 같은 도형을 반만 밝게 채운 층(가로/세로/반원 중 해시로) · 바탕색으로 잘라 낸 작은 자국(해시로 모양과 자리).
    /// background는 로고가 놓인 자리의 바탕색(잘라 낸 자국을 그 색으로 칠한다).
    /// </summary>
    // ── 그려 둔 로고와 작전 엠블럼(Tools/UiArt/BakeBrand.ps1로 굽는다) ──
    // 상장사 열네 곳은 손으로 정한 로고(색 · 바깥 모양 · 머리글자)를 쓴다. 표에 없는 회사(새 소기업·내 법인)는 아래의 규칙 로고를 쓴다.
    private static readonly string[] BakedSymbols = { "NM", "DSA", "HBF", "MBI", "DDP", "KMF", "GFD", "CBT", "CED", "NXT", "SSE", "SQH", "TZM", "AMV" };
    private static Texture2D logoAtlas, emblemAtlas;
    private static readonly Dictionary<string, Sprite> bakedLogos = new(StringComparer.Ordinal);
    private static readonly Sprite[] emblems = new Sprite[6];
    /// <summary>그 회사의 그려 둔 로고. 없으면 null.</summary>
    public static Sprite BakedLogo(Stock stock)
    {
        if (stock == null || string.IsNullOrEmpty(stock.symbol)) { return null; }
        if (bakedLogos.TryGetValue(stock.symbol, out var sprite)) { return sprite; }
        int index = Array.IndexOf(BakedSymbols, stock.symbol);
        logoAtlas ??= Resources.Load<Texture2D>("UI/Brand/Logos");
        if (index >= 0 && logoAtlas != null)
        {
            float size = logoAtlas.width / 4f;
            sprite = Sprite.Create(logoAtlas, new Rect(index % 4 * size, logoAtlas.height - (index / 4 + 1) * size, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "Logo " + stock.symbol; sprite.hideFlags = HideFlags.DontSave;
        }
        bakedLogos[stock.symbol] = sprite; return sprite;
    }
    /// <summary>작전의 큰 상징 그림(작은 메뉴 아이콘과 다르다: 그 작전이 시세에 무엇을 하는지 그린 도식). 흰색이라 쓰는 자리에서 칠한다.</summary>
    public static Sprite Emblem(PlayerTactics.Tool tool)
    {
        int index = tool switch { PlayerTactics.Tool.Ignite => 0, PlayerTactics.Tool.Unload => 1, PlayerTactics.Tool.Slam => 2, PlayerTactics.Tool.Close => 3, PlayerTactics.Tool.Wash => 4, _ => 5 };
        if (emblems[index] != null) { return emblems[index]; }
        emblemAtlas ??= Resources.Load<Texture2D>("UI/Brand/Emblems");
        if (emblemAtlas == null) { return null; }
        float w = emblemAtlas.width / 2f, h = emblemAtlas.height / 4f;
        var sprite = Sprite.Create(emblemAtlas, new Rect(index % 2 * w, emblemAtlas.height - (index / 2 + 1) * h, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        sprite.name = "Emblem " + tool; sprite.hideFlags = HideFlags.DontSave; emblems[index] = sprite; return sprite;
    }

    public static void Logo(RectTransform holder, Stock stock, Color background)
    {
        if (holder == null || stock == null) { return; }
        var baked = BakedLogo(stock);
        var bakedLayer = Layer(holder, "Logo Baked", baked, Color.white);
        bakedLayer.gameObject.SetActive(baked != null);
        foreach (string part in new[] { "Image :: Logo Base", "Image :: Logo Half", "Image :: Logo Cut" }) { var old = holder.Find(part); if (old != null) { old.gameObject.SetActive(baked == null); } }
        if (baked != null) { bakedLayer.transform.SetAsLastSibling(); return; }
        var sector = SectorOf(stock);
        uint hash = Hash(stock.symbol ?? stock.stockName);
        var main = Tone(hash, .48f, .62f); var light = Tone(hash, .36f, .84f);
        var baseSprite = Cell(LogoFirst + (int)sector);
        var bottom = Layer(holder, "Logo Base", baseSprite, main);
        var half = Layer(holder, "Logo Half", baseSprite, light);
        half.type = Image.Type.Filled;
        switch ((hash >> 4) % 4u)
        {
            case 0: half.fillMethod = Image.FillMethod.Horizontal; half.fillOrigin = (int)((hash >> 7) % 2u); break;
            case 1: half.fillMethod = Image.FillMethod.Vertical; half.fillOrigin = (int)((hash >> 7) % 2u); break;
            case 2: half.fillMethod = Image.FillMethod.Radial180; half.fillOrigin = (int)((hash >> 7) % 4u); break;
            default: half.fillMethod = Image.FillMethod.Radial90; half.fillOrigin = (int)((hash >> 7) % 4u); break;
        }
        half.fillAmount = (hash >> 10) % 3u == 0u ? .35f : .5f;
        // 열 개 중 넷은 자국 없이 둔다(모두 자국이 있으면 그것도 규칙처럼 보인다).
        bool cut = (hash >> 12) % 10u >= 4u;
        var mark = Layer(holder, "Logo Cut", Cell(CutFirst + (int)((hash >> 14) % (uint)Cuts)), background);
        mark.gameObject.SetActive(cut);
        if (cut)
        {
            var rect = mark.rectTransform; float size = .34f;
            float x = .5f + ((hash >> 17) % 3u - 1f) * .18f, y = .5f + ((hash >> 19) % 3u - 1f) * .18f;
            rect.anchorMin = new Vector2(x - size / 2f, y - size / 2f); rect.anchorMax = new Vector2(x + size / 2f, y + size / 2f);
            rect.localRotation = Quaternion.Euler(0, 0, 90f * ((hash >> 21) % 4u));
        }
        bottom.gameObject.SetActive(true); half.gameObject.SetActive(true);
        bottom.transform.SetSiblingIndex(0); half.transform.SetSiblingIndex(1); mark.transform.SetSiblingIndex(2);
    }

    // ── 아바타 ──
    /// <summary>
    /// 채팅의 얼굴. 봇 번호가 바깥 도형·안쪽 무늬·색을 정하고(같은 사람은 늘 같은 얼굴), 고리의 색은 지금 기분이다.
    /// mood가 투명하면 고리를 숨긴다.
    /// </summary>
    public static void Avatar(RectTransform holder, int id, Color mood)
    {
        if (holder == null) { return; }
        uint hash = Hash("bot:" + id);
        var ring = Layer(holder, "Avatar Ring", Cell(Ring), mood);
        ring.gameObject.SetActive(mood.a > .01f);
        var face = Layer(holder, "Avatar Face", Cell(AvatarFirst + (int)(hash % (uint)AvatarShapes)), Tone(hash >> 3, .34f, .62f));
        Inset(face.rectTransform, .16f);
        int inner = (int)((hash >> 7) % 4u);
        var detail = Layer(holder, "Avatar Detail", Cell(inner == 0 ? Dot : inner == 1 ? Bar : AvatarFirst + (int)((hash >> 9) % (uint)AvatarShapes)), Tone(hash >> 3, .16f, .95f));
        Inset(detail.rectTransform, inner == 1 ? .30f : .34f);
        detail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f * ((hash >> 13) % 4u));
        ring.transform.SetSiblingIndex(0); face.transform.SetSiblingIndex(1); detail.transform.SetSiblingIndex(2);
    }

    // ── 조립 도우미 ──
    /// <summary>holder 안의 이름 붙은 그림 한 겹(없으면 만든다). 꽉 채우고 비율을 지킨다.</summary>
    public static Image Layer(RectTransform holder, string name, Sprite sprite, Color color)
    {
        string full = "Image :: " + name;
        var found = holder.Find(full);
        Image image;
        if (found != null) { image = found.GetComponent<Image>(); }
        else
        {
            var rect = new GameObject(full, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(holder, false); rect.gameObject.layer = holder.gameObject.layer;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
        }
        if (image.sprite != sprite) { image.sprite = sprite; }
        if (image.color != color) { image.color = color; }
        return image;
    }
    private static void Inset(RectTransform rect, float margin) { rect.anchorMin = new Vector2(margin, margin); rect.anchorMax = new Vector2(1f - margin, 1f - margin); rect.offsetMin = rect.offsetMax = Vector2.zero; }

    /// <summary>parent 안에 그림 하나를 놓는다(앵커 비율). 이미 같은 이름이 있으면 그것을 돌려준다.</summary>
    public static Image Icon(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        var rect = TabletKitStyle.Area(name, parent, x, y, w, h);
        var image = rect.gameObject.AddComponent<Image>(); image.name = "Image :: " + name; image.raycastTarget = false;
        image.sprite = sprite; image.color = color; image.preserveAspect = true;
        return image;
    }
}
