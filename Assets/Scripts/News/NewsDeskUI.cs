using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A pooled article reader inside each existing tablet Canvas. UI never modifies market accounts.</summary>
public sealed class NewsDeskUI : MonoBehaviour
{
    private TabletAccountUI navigation;
    private DeviceTerminalUI terminal;
    private TMP_FontAsset font;
    private RectTransform listContent, articleContent;
    private ScrollRect listScroll, articleScroll;
    private TextMeshProUGUI articleText, countText, empty, resultText, draftText, cooldownText;
    private Button all, owned, current, rumors, categoryButton, order, publish, studioButton, draftCategory, draftStock, positive, negative;
    private TMP_InputField search;
    private GameObject reading, studio, picker;
    private readonly List<GameObject> readerChrome=new();
    private readonly List<NewsArticle> visible = new(40);
    private readonly List<Button> rows = new(40);
    private readonly List<TextMeshProUGUI> titles = new(40), meta = new(40);
    private readonly List<Image> thumbs = new(40);
    private readonly Dictionary<DeviceMarketJournal.Entry, NewsArticle> alerts = new();
    private readonly StringBuilder buffer = new();
    private NewsArticle selected, draft;
    private Stock lastStock, previewStock;
    private string category = "all", query = "", mode = "all";
    private int categoryIndex = -1, draftCategoryIndex = 2, variant, version = -1, responseVersion = -1, alertVersion = -1;
    private bool optimistic = true;
    private int localeRevision = -1;
    private float next, resultUntil;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(153,168,179,255), Teal = new Color32(37,175,192,255),
        Surface = new Color32(16,24,29,255), Selected = new Color32(23,53,60,255), Line = new Color32(49,64,72,255);
    public NewsArticle SelectedArticle => selected;
    public int VisibleCount => visible.Count;
    public static NewsDeskUI Install(TabletAccountUI owner)
    {
        if (owner.events == null) { return null; }
        var previous = owner.events.panel.GetComponentInChildren<NewsDeskUI>(true);
        if (previous != null) { return previous; }
        foreach (Transform child in owner.events.panel.transform) { child.gameObject.SetActive(false); }
        var root = new GameObject("Area :: News Reader", typeof(RectTransform));
        root.transform.SetParent(owner.events.panel.transform, false);
        Rect((RectTransform)root.transform, 0, 0, 1, 1);
        var reader = root.AddComponent<NewsDeskUI>(); reader.navigation = owner; reader.terminal = owner.events;
        reader.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? owner.events.eventDetail.font;
        reader.Build(); return reader;
    }
    private void Build()
    {
        Panel(transform, "News Background", 0, 0, 1, 1, Surface);
        Label(transform, "News Heading", "시장 뉴스", .025f,.018f,.4f,.065f,44,Ink,true);
        search = Input(transform,.60f,.02f,.375f,.055f);
        search.onValueChanged.AddListener(value => { query = value; RefreshList(); });
        all = Button(transform,"All News","전체",.025f,.098f,.12f,.05f,() => SetMode("all"));
        owned = Button(transform,"Owned News","보유 종목",.155f,.098f,.16f,.05f,() => SetMode("owned"));
        current = Button(transform,"Current News","선택 종목",.325f,.098f,.16f,.05f,() => SetMode("current"));
        rumors = Button(transform,"Unconfirmed News","미확인",.495f,.098f,.13f,.05f,() => SetMode("rumors"));
        categoryButton = Button(transform,"News Category","분야 · 전체 ▼",.655f,.098f,.32f,.05f,ChooseCategory);
        reading = Area(transform,"News Reading",0,.17f,1,.75f).gameObject;
        countText = Label(reading.transform,"Article Count","기사를 기다리고 있습니다",.025f,0,.31f,.05f,24,Muted);
        listScroll = Scroll(reading.transform,"Article List",.025f,.06f,.31f,.93f,out listContent);
        articleScroll = Scroll(reading.transform,"Article Detail",.375f,0,.60f,.90f,out articleContent);
        Panel(reading.transform,"News Divider",.352f,0,.001f,1,Line);
        empty = Label(reading.transform,"Empty News","조건에 맞는 기사가 없습니다",.035f,.18f,.29f,.18f,30,Muted);
        articleText = Label(articleContent,"Article Text","",0,0,1,1,32,Ink);
        articleText.richText = true; articleText.overflowMode = TextOverflowModes.Overflow;
        articleText.textWrappingMode = TextWrappingModes.Normal; articleText.lineSpacing = 7;
        order = Button(reading.transform,"Article Order","이 종목 거래",.375f,.93f,.30f,.07f,OpenOrder);
        TabletControlStyle.Emphasize(order,true);
        Button(reading.transform,"Related Articles","관련 기사",.695f,.93f,.28f,.07f,() =>
        { if (selected != null) { query = selected.company; search.SetTextWithoutNotify(query); mode = "all"; category = "all"; categoryIndex = -1; RefreshList(); } });
        // Reuse the old event row pool. All item selection is mapped to the currently filtered article.
        for (int i = 0; i < terminal.eventButtons.Length; i++)
        {
            int index = i; var button = terminal.eventButtons[i]; button.transform.SetParent(listContent,false);
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => SelectRow(index));
            button.name = "Button :: News Row " + (i + 1); button.image.sprite = null; button.image.type = Image.Type.Simple;
            button.image.color = Color.clear; button.transition = Selectable.Transition.ColorTint;
            var title = terminal.eventTitles[i]; var details = terminal.eventTags[i];
            Style(title,30,Ink,true); Style(details,25,Muted,false);
            // 썸네일: 그 회사의 업종 그림. 색은 기사의 결(호재 빨강 · 악재 파랑 · 그 밖 회색), 확인 안 된 소문이면 흐리게.
            var thumb = Panel(button.transform,"Article Thumb",.02f,.14f,.13f,.72f,new Color32(16,27,34,255));
            var picture = new GameObject("Image :: Article Thumb Picture", typeof(RectTransform)).AddComponent<Image>();
            picture.rectTransform.SetParent(thumb.transform,false); picture.gameObject.layer = thumb.gameObject.layer; picture.raycastTarget = false; picture.preserveAspect = true;
            picture.rectTransform.anchorMin = new Vector2(.18f,.18f); picture.rectTransform.anchorMax = new Vector2(.82f,.82f); picture.rectTransform.offsetMin = picture.rectTransform.offsetMax = Vector2.zero;
            thumbs.Add(picture);
            Rect(title.rectTransform,.17f,.07f,.80f,.56f); title.textWrappingMode = TextWrappingModes.Normal;
            Rect(details.rectTransform,.17f,.69f,.80f,.29f); details.textWrappingMode = TextWrappingModes.Normal;
            Panel(button.transform,"Article Row Divider",0,.99f,1,.005f,Line);
            rows.Add(button); titles.Add(title); meta.Add(details);
        }
        Button(transform,"News Management","기업 경영",.025f,.94f,.19f,.045f,() => navigation.ShowPage(TabletAccountUI.Page.Management));
        Button(transform,"News Growth","성장·권한",.225f,.94f,.19f,.045f,() => terminal.growth.onClick.Invoke());
        Button(transform,"Market Alerts","알림 기록",.435f,.94f,.19f,.045f,() => SetMode("alerts"));
        if (navigation.second)
        { studioButton = Button(transform,"News Studio","기사 만들기",.73f,.94f,.245f,.045f,OpenStudio); }
        BuildStudio();
        foreach (Transform child in transform) { if (child.gameObject!=studio) { readerChrome.Add(child.gameObject); } }
        RefreshList();
    }
    private void SetMode(string value) { mode = value; RefreshList(); }
    void OnEnable() { version = -1; next = 0; }
    private void ChooseCategory()
    {
        var catalog = NewsManager.Instance?.Writing;
        if (catalog == null) { return; }
        var choices = new string[catalog.narratives.Length + 1]; choices[0] = "전체 분야";
        for (int i = 0; i < catalog.narratives.Length; i++) { choices[i + 1] = catalog.narratives[i].label; }
        Choose(choices,categoryIndex + 1,.655f,.16f,.32f,index =>
        { categoryIndex = index - 1; category = index == 0 ? "all" : catalog.narratives[categoryIndex].key; mode = mode == "alerts" ? "all" : mode; RefreshList(); });
    }
    private void Choose(string[] options, int selectedIndex, float x, float y, float width, Action<int> select)
    {
        if (picker != null) { Destroy(picker); }
        picker = Area(transform,"News Options",0,0,1,1).gameObject;
        var outside = Panel(picker.transform,"Close Options",0,0,1,1,new Color(0,0,0,.20f)); outside.raycastTarget = true;
        var close = outside.gameObject.AddComponent<Button>(); close.targetGraphic = outside;
        close.onClick.AddListener(() => Destroy(picker));
        var menu = Area(picker.transform,"News Options List",x,y,width,Math.Min(options.Length * .060f,.97f-y));
        Panel(menu,"Options Background",0,0,1,1,new Color32(25,36,43,255));
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            var button = Button(menu,"Option " + i,options[i],.025f,(float)i/options.Length,.95f,1f/options.Length,() => { Destroy(picker); select(index); });
            TabletControlStyle.Emphasize(button,i == selectedIndex);
        }
    }
    void OnDisable() { if (picker != null) { Destroy(picker); } }
    private void RefreshList()
    {
        var news = NewsManager.Instance;
        if (news == null || navigation == null) { return; }
        float scroll = listScroll.verticalNormalizedPosition;
        version = news.ArticleVersion; alertVersion = terminal.records.Version; visible.Clear();
        var stock = StockManager.Instance?.CurrentStock;
        if (mode == "alerts") { RefreshAlerts(); }
        else { foreach (var article in news.Articles)
        {
            var relatedStock = FindStock(article.symbol);
            bool held = relatedStock != null && navigation.account.GetOwnedVolume(relatedStock) > 0;
            if (NewsWriting.Matches(article,"",category,mode == "current" ? stock?.symbol : null,mode == "owned",held,mode == "rumors") && MatchesQuery(article))
            { visible.Add(article); }
        } }
        bool changedSelection = selected == null || !visible.Contains(selected);
        if (changedSelection) { selected = visible.Count > 0 ? visible[0] : null; }
        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i < visible.Count; rows[i].gameObject.SetActive(active); if (!active) { continue; }
            var rect = (RectTransform)rows[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchorMax = new Vector2(1,1); rect.anchoredPosition = new Vector2(0,-i * 157); rect.sizeDelta = new Vector2(0,156);
            var article = visible[i]; Set(titles[i],article.title);
            Set(meta[i],(string.IsNullOrEmpty(article.company) ? article.source : article.company) + "  ·  " + article.time +
                (article.credibility < .5 ? " · 미확인" : article.correction ? " · 후속" : ""));
            rows[i].image.color = article == selected ? Selected : Color.clear;
            if (i < thumbs.Count)
            {
                var company = FindStock(article.symbol);
                thumbs[i].sprite = company != null ? UiPictograms.SectorIcon(UiPictograms.SectorOf(company)) : UiPictograms.Cell(UiPictograms.MarkSystem);
                Color tone = article.sentiment > .15 ? (Color)MarketColors.Up : article.sentiment < -.15 ? (Color)MarketColors.Down : Muted;
                tone.a = article.credibility < .5 ? .45f : 1f;
                thumbs[i].color = tone;
            }
        }
        listContent.sizeDelta = new Vector2(0,visible.Count * 157); listScroll.verticalNormalizedPosition = scroll;
        empty.gameObject.SetActive(visible.Count == 0); Set(countText, (mode == "alerts" ? "시장 알림 " : "최근 기사 ") + visible.Count);
        TabletControlStyle.Emphasize(all,mode == "all"); TabletControlStyle.Emphasize(owned,mode == "owned");
        TabletControlStyle.Emphasize(current,mode == "current"); TabletControlStyle.Emphasize(rumors,mode == "rumors");
        Text(categoryButton,"분야 · " + (categoryIndex < 0 ? "전체" : news.Writing.narratives[categoryIndex].label) + " ▼");
        RenderArticle();
        if (changedSelection) { articleScroll.verticalNormalizedPosition = 1; }
    }
    private void RefreshAlerts()
    {
        var removed = new List<DeviceMarketJournal.Entry>();
        foreach (var entry in alerts.Keys) { if (!terminal.records.Events.Contains(entry)) { removed.Add(entry); } }
        foreach (var entry in removed) { alerts.Remove(entry); }
        foreach (var entry in terminal.records.Events)
        {
            if (entry.Article != null) { continue; }
            if (!alerts.TryGetValue(entry, out var article))
            {
                article = new NewsArticle { symbol = entry.Stock?.symbol, company = entry.Stock?.stockName, title = entry.Title,
                    category = "notice", categoryLabel = "시장 알림", source = entry.Source, verification = entry.Certainty,
                    time = entry.Time, credibility = 1, summary = entry.Certainty,
                    body = entry.Stock == null ? "게임 내 시장과 진행 상태에 관한 알림입니다." :
                        "발생 직전 60초 · 실제 체결 " + entry.Volume.ToString("N0") + "주\n매수 주도 비중 " +
                        (entry.Volume > 0 ? (entry.BuyVolume / entry.Volume * 100).ToString("0.0") + "%" : "집계 없음") +
                        "\n매도 호가를 소진한 체결량 / 전체 체결량입니다." };
                alerts.Add(entry, article);
            }
            if (MatchesQuery(article)) { visible.Add(article); }
        }
    }
    private bool MatchesQuery(NewsArticle article)
    {
        if (NewsWriting.Matches(article,query,"all",null,false,false,false)) { return true; }
        string needle = query.Trim();
        return (GameLocale.Text(article.title) + " " + GameLocale.Text(article.company)).IndexOf(needle,StringComparison.OrdinalIgnoreCase) >= 0;
    }
    private void SelectRow(int row)
    {
        if (row < 0 || row >= visible.Count) { return; }
        selected = visible[row];
        foreach (var button in rows) { button.image.color = Color.clear; }
        rows[row].image.color = Selected; RenderArticle();
        articleScroll.verticalNormalizedPosition = 1;
    }
    private static string Safe(string value) => (value ?? "").Replace("<","‹").Replace(">","›");
    private void RenderArticle()
    {
        order.interactable = selected != null && FindStock(selected.symbol)?.AcceptsOrders == true;
        if (selected == null) { SetArticle("아직 읽을 기사가 없습니다.\n\n필터를 바꾸거나 새 소식을 기다려 주세요."); return; }
        buffer.Clear();
        buffer.Append("<size=24><color=#99A8B3>").Append(Safe(selected.categoryLabel)).Append("  ·  ").Append(Safe(selected.source)).Append("  ·  ").Append(selected.time)
            .Append("</color></size>\n\n<size=44><b>").Append(Safe(selected.title)).Append("</b></size>\n\n")
            .Append("<color=#76CAD4>").Append(Safe(selected.company ?? "시장")).Append(" · ").Append(Safe(selected.verification)).Append("</color>\n\n")
            .Append(Safe(selected.summary)).Append("\n\n");
        string body = Safe(selected.body);
        foreach (string heading in new[] {"무슨 일이 있었나","전해진 주장","후속 확인","배경과 이해관계","사회가 받아들이는 방식","다음 확인 지점"})
        { body = body.Replace(heading + "\n","<b>" + heading + "</b>\n"); }
        buffer.Append(body);
        if (selected.links.Length > 0)
        {
            buffer.Append("\n\n<b>관련 기업 · 연결고리</b>\n");
            foreach (var link in selected.links)
            {
                var company = FindStock(link.symbol); if (company == null) { continue; }
                buffer.Append(Safe(company.stockName)).Append(" — ").Append(Safe(link.relation)).Append('\n');
            }
        }
        if (selected.correction)
        {
            var original = NewsManager.Instance.FindArticle(selected.storyId);
            if (original != null) { buffer.Append("\n\n<b>앞선 보도</b>\n").Append(Safe(original.title)); }
        }
        var entry = terminal.records.Events.Find(row => row.Id == selected.id && row.Stock != null);
        if (entry != null)
        {
            buffer.Append("\n\n<size=26><color=#99A8B3>보도 직전 60초 · 실제 체결 ").Append(entry.Volume.ToString("N0")).Append("주\n매수 주도 비중 ")
                .Append(entry.Volume > 0 ? (entry.BuyVolume / entry.Volume * 100).ToString("0.0") + "%" : "집계 없음").Append("</color></size>");
        }
        if (selected.id > 0) { buffer.Append("\n\n<b>라운지 반응</b>\n").Append(Safe(CompanyNewsAudience.Instance?.Responses(selected.id) ?? "연결된 공개 발언이 아직 없습니다.")); }
        SetArticle(buffer.ToString());
    }
    private void SetArticle(string text)
    {
        float previous = articleScroll.verticalNormalizedPosition; Set(articleText,text);
        float width = Math.Max(100,articleScroll.viewport.rect.width - 16);
        float height = articleText.GetPreferredValues(GameLocale.Text(text),width,0).y + 40;
        articleContent.sizeDelta = new Vector2(0,height);
        articleText.rectTransform.anchorMin = new Vector2(0,1); articleText.rectTransform.anchorMax = Vector2.one;
        articleText.rectTransform.pivot = new Vector2(0,1); articleText.rectTransform.anchoredPosition = Vector2.zero;
        articleText.rectTransform.sizeDelta = new Vector2(-16,height);
        articleScroll.verticalNormalizedPosition = previous;
    }
    private void OpenOrder()
    {
        var stock = selected == null ? null : FindStock(selected.symbol);
        if (stock == null || !stock.AcceptsOrders) { return; }
        StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(stock)); navigation.ShowPage(TabletAccountUI.Page.Orders);
    }
    private void BuildStudio()
    {
        studio = Area(transform,"News Studio",.025f,.17f,.95f,.75f).gameObject;
        Panel(studio.transform,"Studio Background",0,0,1,1,Surface);
        Label(studio.transform,"Studio Heading","가짜 뉴스 발행",0,0,.70f,.075f,40,Ink,true);
        Button(studio.transform,"Close Studio","기사 목록",.79f,0,.21f,.065f,() => navigation.ShowPage(TabletAccountUI.Page.Events));
        draftStock = Button(studio.transform,"Draft Stock","대상 종목",0,.10f,.46f,.07f,ChooseStock);
        draftCategory = Button(studio.transform,"Draft Theme","분야",.49f,.10f,.51f,.07f,() =>
        {
            var catalog = NewsManager.Instance.Writing; var options = new string[catalog.narratives.Length];
            for (int i = 0; i < options.Length; i++) { options[i] = catalog.narratives[i].label; }
            Choose(options,draftCategoryIndex,.50f,.31f,.475f,index => { draftCategoryIndex = index; RefreshDraft(); });
        });
        positive = Button(studio.transform,"Positive Draft","호재 방향",0,.20f,.24f,.06f,() => { optimistic = true; RefreshDraft(); });
        negative = Button(studio.transform,"Negative Draft","악재 방향",.26f,.20f,.24f,.06f,() => { optimistic = false; RefreshDraft(); });
        Button(studio.transform,"Another Draft","다른 초안",.74f,.20f,.26f,.06f,() => { variant++; RefreshDraft(); });
        var previewScroll = Scroll(studio.transform,"Draft Preview",0,.30f,1,.47f,out var previewContent);
        draftText = Label(previewContent,"Draft Text","",0,0,1,1,31,Ink); draftText.richText = true; draftText.overflowMode = TextOverflowModes.Overflow;
        draftText.textWrappingMode = TextWrappingModes.Normal; draftText.lineSpacing = 6;
        cooldownText = Label(studio.transform,"Publication Cooldown","",0,.80f,.65f,.055f,27,Muted);
        resultText = Label(studio.transform,"Publication Result","",0,.88f,.65f,.11f,28,Ink);
        publish = Button(studio.transform,"Publish Draft","이 초안 발행",.70f,.85f,.30f,.12f,Publish);
        TabletControlStyle.Emphasize(publish,true);
        studio.SetActive(false);
    }
    public void OpenStudio()
    {
        if (!navigation.second || navigation.Locked) { return; }
        foreach (var element in readerChrome) { element.SetActive(false); }
        Rect((RectTransform)studio.transform,.025f,.02f,.95f,.96f);
        studio.SetActive(true); RefreshDraft();
    }
    public void OpenReader()
    { foreach (var element in readerChrome) { element.SetActive(true); } studio.SetActive(false); RefreshList(); }
    private void ChooseStock()
    {
        var manager = StockManager.Instance;
        if (manager == null || manager.stocks.Count == 0) { return; }
        var stocks = manager.stocks.ToArray(); var options = new string[stocks.Length];
        for (int i = 0; i < stocks.Length; i++) { options[i] = stocks[i].stockName; }
        Choose(options,manager.stocks.IndexOf(manager.CurrentStock),.025f,.31f,.437f,index =>
        { int currentIndex = manager.stocks.IndexOf(stocks[index]); if (currentIndex >= 0) { manager.SelectStock(currentIndex); RefreshDraft(); } });
    }
    private void RefreshDraft()
    {
        previewStock = StockManager.Instance?.CurrentStock;
        var publisher = FakeNewsManager.Instance; var news = NewsManager.Instance;
        if (publisher == null || news == null || previewStock == null) { return; }
        var theme = news.Writing.narratives[draftCategoryIndex];
        draft = publisher.PreviewPlayer(previewStock,theme.key,optimistic,variant);
        Text(draftStock,"대상 · " + previewStock.stockName + " ▼"); Text(draftCategory,"분야 · " + theme.label + " ▼");
        TabletControlStyle.Emphasize(positive,optimistic); TabletControlStyle.Emphasize(negative,!optimistic);
        string text = "<b>" + Safe(draft.title) + "</b>\n\n" + Safe(draft.summary) + "\n\n" + Safe(draft.body);
        Set(draftText,text); var content = (RectTransform)draftText.transform.parent;
        float width = Math.Max(100,((RectTransform)content.parent).rect.width - 16);
        float height = draftText.GetPreferredValues(GameLocale.Text(text),width,0).y + 30; content.sizeDelta = new Vector2(0,height);
        Rect(draftText.rectTransform,0,0,1,1); draftText.rectTransform.offsetMax = new Vector2(-16,0);
        if (Time.unscaledTime >= resultUntil) { Set(resultText,""); }
    }
    private void Publish()
    {
        if (draft == null || !navigation.second || navigation.Locked) { return; }
        // Publish the previewed company, even if the user changes global selection before this frame.
        var publisher = FakeNewsManager.Instance;
        if (publisher == null) { return; }
        publisher.TryPublishPlayer(previewStock,draft.category,optimistic,variant,out var article,out string reason);
        if (publisher.ReasonWritten) { GameLocalizedText.Set(resultText,reason,true); } else { Set(resultText,reason); }
        resultUntil = Time.unscaledTime + 8;
        if (article != null) { selected = article; version = -1; }
        next = 0;
    }
    void Update()
    {
        if (navigation == null || navigation.Locked || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .3f;
        var news = NewsManager.Instance; if (news == null) { return; }
        if (localeRevision != GameLocale.Revision)
        {
            localeRevision = GameLocale.Revision; RefreshList();
            if (studio.activeSelf) { RefreshDraft(); }
        }
        var stock = StockManager.Instance?.CurrentStock;
        if (version != news.ArticleVersion || stock != lastStock && mode == "current" || mode == "alerts" && alertVersion != terminal.records.Version) { RefreshList(); }
        lastStock = stock;
        int responses = CompanyNewsAudience.Instance == null ? 0 : CompanyNewsAudience.Instance.Version;
        if (responses != responseVersion) { responseVersion = responses; RenderArticle(); }
        if (!studio.activeSelf) { return; }
        if (stock != previewStock) { RefreshDraft(); }
        var publisher = FakeNewsManager.Instance;
        double remaining = publisher == null ? 0 : publisher.PlayerCooldown;
        Set(cooldownText,remaining > 0 ? "재사용 대기 · " + FakeNewsManager.FormatCooldown(remaining) : "발행 가능");
        publish.interactable = publisher != null && publisher.PlayerUnlocked && remaining <= 0 && MarketDayController.CanTrade && previewStock?.AcceptsOrders == true;
        Text(publish,remaining > 0 ? "대기 " + FakeNewsManager.FormatCooldown(remaining) : "이 초안 발행");
    }
    private static Stock FindStock(string symbol)
    {
        if (StockManager.Instance == null) { return null; }
        foreach (var stock in StockManager.Instance.stocks) { if (stock.symbol == symbol && !stock.IsDelisted) { return stock; } }
        return null;
    }
    private static void Set(TextMeshProUGUI label,string value) { DeviceTerminalUI.Set(label,value); }
    private static void Text(Button button,string value) { Set(button.GetComponentInChildren<TextMeshProUGUI>(),value); }
    private static void Rect(RectTransform rect,float x,float y,float w,float h)
    {
        rect.anchorMin = new Vector2(x,1-y-h); rect.anchorMax = new Vector2(x+w,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
    private static RectTransform Area(Transform parent,string name,float x,float y,float w,float h)
    {
        var root = new GameObject("Area :: " + name,typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent,false); Rect(root,x,y,w,h); return root;
    }
    private static Image Panel(Transform parent,string name,float x,float y,float w,float h,Color color)
    {
        var rect = Area(parent,name,x,y,w,h); rect.name = "Image :: " + name;
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private void Style(TextMeshProUGUI text,float size,Color color,bool bold)
    {
        text.font = font; text.fontSharedMaterial = font.material; text.fontSize = size; text.enableAutoSizing = false;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; text.color = color; text.margin = Vector4.zero;
        text.alignment = TextAlignmentOptions.TopLeft; text.raycastTarget = false; text.richText = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
    }
    private TextMeshProUGUI Label(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color,bool bold = false)
    {
        var rect = Area(parent,name,x,y,w,h); rect.name = "Text :: " + name;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); Style(label,size,color,bold); GameLocalizedText.Set(label,text); return label;
    }
    private Button Button(Transform parent,string name,string caption,float x,float y,float w,float h,Action action)
    {
        var background = Panel(parent,name,x,y,w,h,Surface); background.name = "Button :: " + name; background.raycastTarget = true;
        var button = background.gameObject.AddComponent<Button>(); button.targetGraphic = background;
        var label = Label(background.transform,name + " Label",caption,.025f,0,.95f,1,28,Ink);
        label.alignment = TextAlignmentOptions.Center; button.onClick.AddListener(() => action()); TabletControlStyle.Button(button); return button;
    }
    private ScrollRect Scroll(Transform parent,string name,float x,float y,float w,float h,out RectTransform content)
    {
        var root = Area(parent,name,x,y,w,h); var image = root.gameObject.AddComponent<Image>(); image.color = new Color(0,0,0,0); image.raycastTarget = true;
        root.gameObject.AddComponent<RectMask2D>();
        var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.viewport = root; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 70;
        content = Area(root,name + " Content",0,0,1,1); content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
        content.anchoredPosition = Vector2.zero; scroll.content = content;
        var track = Panel(root,"Scroll Track",.989f,0,.011f,1,new Color32(36,48,55,255)); track.raycastTarget = true;
        var thumb = Panel(track.transform,"Scroll Thumb",0,0,1,1,new Color32(109,133,146,220)); thumb.raycastTarget = true;
        var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.targetGraphic = thumb;
        scrollbar.handleRect = (RectTransform)thumb.transform; scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }
    private TMP_InputField Input(Transform parent,float x,float y,float w,float h)
    {
        var rect = Area(parent,"News Search",x,y,w,h); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(25,36,43,255);
        var field = rect.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic = image;
        var text = Label(rect,"Search Text","",.04f,.08f,.92f,.84f,28,Ink); text.raycastTarget = true;
        var placeholder = Label(rect,"Search Placeholder","기업·제목 검색",.04f,.08f,.92f,.84f,28,Muted);
        field.textViewport = rect; field.textComponent = text; field.placeholder = placeholder; field.characterLimit = 60;
        field.lineType = TMP_InputField.LineType.SingleLine; TabletControlStyle.Input(field); return field;
    }
}
