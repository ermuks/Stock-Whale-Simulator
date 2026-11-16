using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Cached pages on one translucent, blurred game backdrop. All text remains in TMP/localization.</summary>
public sealed class SystemMenuUI : IDisposable
{
    private readonly PauseMenuController owner;
    public readonly GameObject Root;
    public readonly RectTransform Paper; // Layout root only, no settings backplate.
    public readonly MenuBackdrop Backdrop;
    private readonly Dictionary<string,RectTransform> pages = new(), categories = new();
    private readonly List<Action> refreshers = new();
    private readonly TMP_FontAsset font;
    private readonly Sprite controlSprite;
    private readonly TextMeshProUGUI title, rest, reportDate, reportTotal, checkpointStatus, dialogText, marketSummary;
    private readonly GameObject savingRoot;
    private readonly TextMeshProUGUI[] reportValues;
    private readonly List<TextMeshProUGUI[]> companyRows = new();
    private readonly RectTransform companyContent;
    private readonly Button close, sleep, reportSleep, retrySave, confirm, cancel, continueSession;
    private readonly Button[] tabs;
    private readonly string[] categoryNames = { "그래픽","오디오","게임","UI" };
    private string category = "게임";
    private bool reportSaved;
    private static readonly Color Ink = new(.92f,.94f,.96f), Muted = new(.63f,.67f,.71f);
    public SystemMenuUI(PauseMenuController controller)
    {
        owner=controller; font=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        controlSprite=Resources.Load<Sprite>("UI/SystemMenu/MenuControl-v2");
        Root=new GameObject("Area :: System Menu",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); Root.transform.SetParent(owner.transform,false);
        var canvas=Root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=30000;
        var scaler=Root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=1;
        var background=Area(Root.transform,"Image :: Blurred Game",0,0,0,0); Stretch(background);
        var raw=background.gameObject.AddComponent<RawImage>(); raw.raycastTarget=false; Backdrop=new MenuBackdrop(raw);
        var shade=Box(Root.transform,"Image :: Translucent Black",new Color(0,0,0,.64f)); Stretch(shade); shade.GetComponent<Image>().raycastTarget=true;
        Paper=Area(Root.transform,"Area :: Menu Layout",0,0,1200,700); Paper.anchorMin=Paper.anchorMax=Paper.pivot=new Vector2(.5f,.5f); Paper.anchoredPosition=Vector2.zero;
        title=Label(Paper,"일시정지",0,0,1200,64,38,FontStyles.Bold);
        close=Button(Paper,"Button :: Close","돌아가기",1000,640,200,48,owner.Back);
        var pause=NewPage("pause");
        var actions=new (string,Action)[] { ("이어서 하기",owner.Resume),("설정",()=>owner.Settings()),("세션 떠나기",owner.ConfirmDiscard),("게임 종료",owner.ConfirmQuit) };
        for (int i=0;i<actions.Length;i++) { Button(pause,"Button :: Pause "+i,actions[i].Item1,390,120+i*66,420,52,actions[i].Item2,i==0); }
        sleep=Button(pause,"Button :: Sleep","08:00까지 취침",390,416,420,52,owner.Sleep);
        rest=Label(pause,"",270,500,660,42,22); rest.alignment=TextAlignmentOptions.Center;
        var home=NewPage("home");
        continueSession=Button(home,"Button :: Continue Session","마지막 마감에서 계속",390,120,420,52,owner.LoadSession,true);
        Button(home,"Button :: New Session","새 세션",390,186,420,52,owner.NewSession);
        Button(home,"Button :: Home Settings","설정",390,252,420,52,()=>owner.Settings());
        Button(home,"Button :: Home Quit","게임 종료",390,318,420,52,owner.Quit);
        var settings=NewPage("settings"); tabs=new Button[4];
        for (int i=0;i<categoryNames.Length;i++)
        {
            string name=categoryNames[i]; tabs[i]=Button(settings,"Button :: Category "+i,name,i*300,90,288,48,()=>Settings(name));
            categories.Add(name,Area(settings,"Area :: "+name,40,180,1120,420));
        }
        BuildSettings(); Button(settings,"Button :: Reset Preferences","기본값 복원",0,640,230,48,owner.ConfirmDefaults);
        var dialog=NewPage("dialog"); dialogText=Label(dialog,"",250,156,700,150,25); dialogText.alignment=TextAlignmentOptions.Center;
        confirm=Button(dialog,"Button :: Confirm Action","확인",295,350,290,52,()=> { },true);
        cancel=Button(dialog,"Button :: Cancel Action","취소",615,350,290,52,owner.Back);
        var report=NewPage("report"); reportDate=Label(report,"",0,72,400,32,22);
        reportTotal=Label(report,"",0,120,560,80,48,FontStyles.Bold); Label(report,"당일 합계",0,202,520,30,21);
        string[] rows={ "당일 실현손익","기본 수입","이용료 수입","기업 배당","손실 보전","당일 세금","납부 세금","미납 세금","총자산" };
        reportValues=new TextMeshProUGUI[rows.Length];
        for (int i=0;i<rows.Length;i++)
        {
            float x=i/5*640, y=250+i%5*50; Label(report,rows[i],x,y,240,40,22);
            reportValues[i]=Label(report,"",x+235,y,325,40,25); reportValues[i].alignment=TextAlignmentOptions.MidlineRight; Line(report,x,y+44,560);
        }
        // 오늘 시장 한 줄 요약과 저장 상태.
        marketSummary=Label(report,"",0,502,1200,32,21);
        checkpointStatus=Label(report,"",0,540,1200,32,21);
        // 저장 중 표시: 메뉴와 별개의 작은 캔버스라 메뉴를 닫고 게임으로 돌아가도 보인다. 화면 오른쪽 아래, 흐리게, 클릭을 막지 않는다.
        savingRoot=new GameObject("Area :: Saving Indicator",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)); savingRoot.transform.SetParent(owner.transform,false);
        var savingCanvas=savingRoot.GetComponent<Canvas>(); savingCanvas.renderMode=RenderMode.ScreenSpaceOverlay; savingCanvas.sortingOrder=30001;
        var savingScaler=savingRoot.GetComponent<CanvasScaler>(); savingScaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; savingScaler.referenceResolution=new Vector2(1600,900); savingScaler.matchWidthOrHeight=1;
        var savingText=Label(savingRoot.transform,"저장 중…",0,0,220,28,17); savingText.alignment=TextAlignmentOptions.MidlineRight; savingText.raycastTarget=false;
        savingText.color=new Color(Ink.r,Ink.g,Ink.b,.32f); savingText.name="Text :: Saving";
        var savingRect=savingText.rectTransform; savingRect.anchorMin=savingRect.anchorMax=savingRect.pivot=new Vector2(1,0); savingRect.anchoredPosition=new Vector2(-22,14); savingRect.sizeDelta=new Vector2(220,28);
        savingRoot.SetActive(false);
        retrySave=Button(report,"Button :: Retry Save","다시 저장",0,586,300,52,owner.ReceiveClosingReport);
        Button(report,"Button :: Company Statements","기업 결산",320,586,230,52,ReportCompanies);
        Button(report,"Button :: Prepare Night","준비 계속",575,586,300,52,owner.ContinueAfterReport);
        reportSleep=Button(report,"Button :: Report Sleep","08:00까지 취침",900,586,300,52,owner.Sleep,true);
        var business=NewPage("companies");
        Label(business,"기업",0,92,350,36,23); Label(business,"순이익",390,92,250,36,23).alignment=TextAlignmentOptions.MidlineRight;
        Label(business,"내 배당금",650,92,250,36,23).alignment=TextAlignmentOptions.MidlineRight; Label(business,"기업 현금",910,92,290,36,23).alignment=TextAlignmentOptions.MidlineRight;
        var viewport=Box(business,"Area :: Company Statements",Color.clear); Place(viewport,0,140,1200,410); viewport.GetComponent<Image>().raycastTarget=true;
        viewport.gameObject.AddComponent<RectMask2D>(); var scroll=viewport.gameObject.AddComponent<ScrollRect>();
        companyContent=Area(viewport,"Area :: Company Statement Rows",0,0,1200,410); companyContent.anchorMin=new Vector2(0,1); companyContent.anchorMax=new Vector2(1,1); companyContent.pivot=new Vector2(0,1); companyContent.sizeDelta=new Vector2(0,410);
        scroll.viewport=viewport; scroll.content=companyContent; scroll.horizontal=false; scroll.vertical=true; scroll.scrollSensitivity=30;
        Button(business,"Button :: Back To Closing","정산으로",900,586,300,52,()=>Report(reportSaved));
        var death=NewPage("death"); var deathText=Label(death,"4일 연속 밤샘",250,156,700,80,25); deathText.alignment=TextAlignmentOptions.Center;
        Button(death,"Button :: Restart Checkpoint","마지막 마감으로",390,310,420,52,()=> { if (SessionStore.Exists) { owner.LoadSession(); } else { owner.NewSession(); } },true);
        Root.SetActive(false);
    }
    private RectTransform NewPage(string key)
    { var rect=Area(Paper,"Area :: Menu "+key,0,0,1200,700); pages.Add(key,rect); rect.gameObject.SetActive(false); return rect; }
    private void Show(string key,string heading)
    {
        foreach (var pair in pages) { pair.Value.gameObject.SetActive(pair.Key==key); }
        title.alignment=key=="settings" || key=="report" ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
        GameLocalizedText.Set(title,heading); close.gameObject.SetActive(key=="settings"); close.transform.SetAsLastSibling();
        EventSystem.current?.SetSelectedGameObject(null); Refresh();
    }
    public void Pause() { Show("pause","일시정지"); }
    public void Home() { Show("home","DONDON"); continueSession.interactable=SessionStore.Exists; }
    public void Settings(string selected=null)
    {
        if (selected!=null) { category=selected; }
        foreach (var pair in categories) { pair.Value.gameObject.SetActive(pair.Key==category); }
        Show("settings","설정");
    }
    public void Refresh()
    {
        Paper.localScale=Vector3.one*GameSettings.Current.menuScale;
        foreach (var refresh in refreshers) { refresh(); }
        for (int i=0;i<tabs.Length;i++) { Tint(tabs[i],categoryNames[i]==category); }
        var player=PlayerRestController.Instance; sleep.gameObject.SetActive(player!=null && player.CanSleep);
        if (player!=null) { GameLocalizedText.Set(rest,$"피로 {player.State.fatigue:0}% · 밤샘 {player.State.sleeplessNights}/3"); }
    }
    private void BuildSettings()
    {
        var body=categories["그래픽"]; Header(body,"디스플레이",0);
        Choice(body,"화면 모드",48,new[]{"창 모드","전체 화면"},()=>GameSettings.Current.fullscreen ? 1 : 0,v=>GameSettings.Current.fullscreen=v==1);
        Header(body,"렌더링",130);
        Choice(body,"품질",178,new[]{"낮음","높음"},()=>GameSettings.Current.quality,v=>GameSettings.Current.quality=v);
        Choice(body,"프레임 제한",242,new[]{"30","60","120"},()=>Array.IndexOf(new[]{30,60,120},GameSettings.Current.frameRate),v=>GameSettings.Current.frameRate=new[]{30,60,120}[v]);
        Choice(body,"수직 동기화",306,new[]{"끔","켬"},()=>GameSettings.Current.vSync ? 1 : 0,v=>GameSettings.Current.vSync=v==1);
        body=categories["오디오"]; Header(body,"출력",0);
        Slider(body,"전체 음량",55,0,1,()=>GameSettings.Current.volume,v=>GameSettings.Current.volume=v,v=>Mathf.RoundToInt(v*100)+"%");
        Choice(body,"다른 창 사용 시 음소거",128,new[]{"끔","켬"},()=>GameSettings.Current.muteUnfocused ? 1 : 0,v=>GameSettings.Current.muteUnfocused=v==1);
        body=categories["게임"]; Header(body,"언어",0);
        Choice(body,"표시 언어",48,new[]{"한국어","English","日本語"},()=>Array.IndexOf(new[]{"ko","en","ja"},GameLocale.Code),v=>GameLocale.SetLanguage(new[]{"ko","en","ja"}[v]),false);
        Header(body,"마우스",130);
        Slider(body,"시점 감도",185,.25f,2.5f,()=>GameSettings.Current.sensitivity,v=>GameSettings.Current.sensitivity=v,v=>v.ToString("0.00")+"×");
        Slider(body,"휠 감도",258,1,6,()=>GameSettings.Current.wheel,v=>GameSettings.Current.wheel=v,v=>v.ToString("0.0"));
        body=categories["UI"]; Header(body,"시스템 메뉴",0);
        Slider(body,"메뉴 크기",55,.9f,1.1f,()=>GameSettings.Current.menuScale,v=>GameSettings.Current.menuScale=v,v=>Mathf.RoundToInt(v*100)+"%");
        Header(body,"시세 색상",150);
        MarketColorChoices(body,210);
    }
    private void MarketColorChoices(Transform body,float y)
    {
        for (int i=0;i<2;i++)
        {
            int index=i;
            var frame=Box(body,"Button :: 상승 / 하락 "+i,Color.white); Place(frame,i*368,y,344,168);
            var border=frame.GetComponent<Image>(); border.raycastTarget=true;
            var button=frame.gameObject.AddComponent<Button>(); button.targetGraphic=border;
            var colors=button.colors; colors.normalColor=colors.selectedColor=Color.white;
            colors.highlightedColor=new Color(1.3f,1.3f,1.3f,1); colors.pressedColor=new Color(.7f,.7f,.7f,1); colors.fadeDuration=.1f; button.colors=colors;
            var surface=Box(frame,"Image :: Candle Option Surface",new Color32(19,26,32,255)); Place(surface,2,2,340,164);
            var preview=Box(frame,"Image :: Candle Theme",Color.white); Place(preview,40,6,264,156);
            var image=preview.GetComponent<Image>(); image.sprite=Resources.Load<Sprite>("UI/SystemMenu/"+(i==0 ? "CandleThemeRedBlue" : "CandleThemeGreenRed"));
            image.type=Image.Type.Simple; image.preserveAspect=true; image.raycastTarget=false;
            button.onClick.AddListener(()=> { GameSettings.Current.marketColors=index; GameSettings.Commit(); });
            refreshers.Add(()=>border.color=GameSettings.Current.marketColors==index ? new Color(1,1,1,.9f) : new Color(1,1,1,.16f));
        }
    }
    public void Report(bool saved)
    {
        reportSaved=saved;
        Show("report","장 마감 정산"); var reports=DailyIncomeController.Instance?.Ledger.Reports;
        if (reports==null || reports.Count==0) { return; }
        var report=reports[reports.Count-1]; GameLocalizedText.Set(reportDate,"DAY "+report.day+"  17:00",true);
        GameLocalizedText.Set(reportTotal,report.DayResult.ToString("+#,##0;-#,##0;0")+"원");
        reportTotal.color=MarketColors.ForChange(report.DayResult,Ink);
        decimal[] values={report.Realized,report.company,report.fees,report.Dividends,report.Relief,report.Tax,report.TaxPaid,report.UnpaidTax,report.Assets};
        for (int i=0;i<values.Length;i++) { GameLocalizedText.Set(reportValues[i],values[i].ToString("N0")+"원"); }
        GameLocalizedText.Set(marketSummary,MarketSummary(),true);
        ReportSaveState(saved,owner.ClosingSaveRunning);
        reportSleep.interactable=PlayerRestController.Instance!=null && PlayerRestController.Instance.CanSleep;
    }
    /// <summary>정산 화면의 저장 상태 줄. 저장이 잘 되면 아무것도 쓰지 않는다. 실패했을 때만 알리고 다시 저장을 내놓는다.</summary>
    public void ReportSaveState(bool saved,bool running)
    {
        reportSaved=saved;
        GameLocalizedText.Set(checkpointStatus,running || saved ? "" : "저장 실패 · 이전 마감 유지");
        retrySave.gameObject.SetActive(!saved && !running);
    }
    /// <summary>화면 구석의 흐린 "저장 중…" 표시.</summary>
    public void SavingIndicator(bool visible)
    {
        if (savingRoot!=null) { savingRoot.SetActive(visible); }
    }
    // 오늘 상장 종목의 등락만 읽어 한 줄로 요약한다. 상태를 바꾸지 않는다.
    private static string MarketSummary()
    {
        var manager=StockManager.Instance; var stats=StockStatistics.Instance;
        if (manager==null || stats==null) { return ""; }
        int up=0, down=0; Stock best=null, worst=null; decimal bestChange=decimal.MinValue, worstChange=decimal.MaxValue;
        foreach (var stock in manager.stocks)
        {
            if (!stats.TryGetDailyChange(stock,out decimal change)) { continue; }
            if (change>0) { up++; } else if (change<0) { down++; }
            if (change>bestChange) { bestChange=change; best=stock; }
            if (change<worstChange) { worstChange=change; worst=stock; }
        }
        if (best==null) { return ""; }
        // 낱말과 종목명을 각각 번역해 조립한다.
        return GameLocale.Text("오늘 시장")+" · "+GameLocale.Text("상승")+" "+up+" · "+GameLocale.Text("하락")+" "+down+" · "+GameLocale.Text("최고")+" "+GameLocale.Text(best.stockName)+" "+bestChange.ToString("+0.0;-0.0;0.0")+"% · "+GameLocale.Text("최저")+" "+GameLocale.Text(worst.stockName)+" "+worstChange.ToString("+0.0;-0.0;0.0")+"%";
    }
    public void Death() { Show("death","과로로 사망했습니다"); }
    public void ReportCompanies()
    {
        Show("companies","기업 결산"); int i=0;
        foreach (var stock in CorporateEconomy.Instance.Companies())
        {
            var data=stock.business; if (data==null || !data.CloseFinished) { continue; }
            if (i==companyRows.Count)
            {
                float y=i*64;
                companyRows.Add(new[] { Label(companyContent,"",0,y,350,56,24), Label(companyContent,"",390,y,250,56,24), Label(companyContent,"",650,y,250,56,24), Label(companyContent,"",910,y,290,56,24) });
                for (int col=1;col<4;col++) { companyRows[i][col].alignment=TextAlignmentOptions.MidlineRight; }
            }
            string[] row={stock.stockName,data.Profit.ToString("N0")+"원",data.PlayerDividend.ToString("N0")+"원",data.Cash.ToString("N0")+"원"};
            for (int col=0;col<4;col++) { companyRows[i][col].gameObject.SetActive(true); GameLocalizedText.Set(companyRows[i][col],row[col]); }
            i++;
        }
        for (int unused=i;unused<companyRows.Count;unused++) { foreach (var label in companyRows[unused]) { label.gameObject.SetActive(false); } }
        companyContent.sizeDelta=new Vector2(0,Mathf.Max(410,i*64)); companyContent.anchoredPosition=Vector2.zero;
    }
    public void Confirm(string heading,string message,Action accept,string caption)
    {
        Show("dialog",heading); GameLocalizedText.Set(dialogText,message); GameLocalizedText.Set(confirm.GetComponentInChildren<TextMeshProUGUI>(),caption);
        confirm.onClick.RemoveAllListeners(); confirm.onClick.AddListener(()=>accept()); cancel.gameObject.SetActive(true); confirm.interactable=true;
    }
    public void Busy(string heading) { Confirm(heading,"",()=> { },heading); confirm.interactable=false; cancel.gameObject.SetActive(false); }
    public void Error(string message) { Confirm("처리하지 못했습니다",message,owner.Back,"돌아가기"); }
    private void Header(Transform body,string text,float y) { Label(body,text,0,y,1120,30,21,FontStyles.Bold); Line(body,0,y+36,1120); }
    private void Choice(Transform body,string label,float y,string[] names,Func<int> get,Action<int> set,bool save=true)
    {
        Label(body,label,0,y,380,48,23); var buttons=new Button[names.Length]; float width=(700-8*(names.Length-1))/names.Length;
        for (int i=0;i<names.Length;i++)
        {
            int index=i; buttons[i]=Button(body,"Button :: "+label+" "+i,names[i],420+i*(width+8),y,width,48,()=> { set(index); if (save) { GameSettings.Commit(); } Refresh(); });
            if (!save) { GameLocalizedText.Set(buttons[i].GetComponentInChildren<TextMeshProUGUI>(),names[i],true); }
        }
        refreshers.Add(()=> { for (int i=0;i<buttons.Length;i++) { Tint(buttons[i],get()==i); } });
    }
    private void Slider(Transform body,string label,float y,float min,float max,Func<float> get,Action<float> set,Func<float,string> format)
    {
        Label(body,label,0,y,380,48,23); var frame=Box(body,"Area :: "+label,new Color(1,1,1,.14f)); Place(frame,420,y,700,48);
        var slider=frame.gameObject.AddComponent<Slider>(); slider.targetGraphic=frame.GetComponent<Image>(); frame.GetComponent<Image>().raycastTarget=true; slider.transition=Selectable.Transition.None;
        var inset=Area(frame,"Area :: Gauge Interior",2,2,696,44); var fill=Box(inset,"Image :: Gauge Fill",new Color(.76f,.82f,.87f,.37f)); Stretch(fill); slider.fillRect=fill;
        slider.minValue=min; slider.maxValue=max; slider.SetValueWithoutNotify(get());
        var number=Label(frame,format(get()),16,0,668,48,22,FontStyles.Bold); number.alignment=TextAlignmentOptions.MidlineRight; number.color=Color.white;
        slider.onValueChanged.AddListener(value=> { set(value); GameSettings.Commit(); });
        refreshers.Add(()=> { slider.SetValueWithoutNotify(get()); GameLocalizedText.Set(number,format(get()),true); });
    }
    private Button Button(Transform parent,string name,string caption,float x,float y,float w,float h,Action action,bool primary=false)
    {
        var rect=Box(parent,name,Color.white); Place(rect,x,y,w,h); var graphic=rect.GetComponent<Image>(); graphic.raycastTarget=true;
        graphic.sprite=controlSprite; graphic.type=Image.Type.Sliced; graphic.pixelsPerUnitMultiplier=2f;
        var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=graphic;
        var text=Label(rect,caption,16,0,w-32,h,23,primary ? FontStyles.Bold : FontStyles.Normal); text.alignment=TextAlignmentOptions.Center;
        button.onClick.AddListener(()=>action()); Tint(button,primary); return button;
    }
    private static void Tint(Button button,bool selected)
    {
        var colors=button.colors; colors.normalColor=new Color(1,1,1,selected ? .24f : .09f); colors.highlightedColor=new Color(1,1,1,.31f);
        colors.selectedColor=colors.normalColor; colors.pressedColor=new Color(1,1,1,.4f); colors.disabledColor=new Color(1,1,1,.035f); colors.fadeDuration=.1f; button.colors=colors;
    }
    private TextMeshProUGUI Label(Transform parent,string text,float x,float y,float w,float h,float size,FontStyles style=FontStyles.Normal)
    {
        var rect=Area(parent,"Text :: "+text,x,y,w,h); var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font=font; label.fontSize=size; label.enableAutoSizing=false; label.fontStyle=style; label.color=size<23 ? Muted : Ink;
        label.alignment=TextAlignmentOptions.MidlineLeft; label.textWrappingMode=TextWrappingModes.Normal; label.raycastTarget=false; GameLocalizedText.Set(label,text); return label;
    }
    private static void Line(Transform parent,float x,float y,float width)
    { var rect=Box(parent,"Image :: Divider",new Color(1,1,1,.12f)); Place(rect,x,y,width,1); }
    private static RectTransform Box(Transform parent,string name,Color color)
    { var rect=Area(parent,name,0,0,0,0); var image=rect.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return rect; }
    private static RectTransform Area(Transform parent,string name,float x,float y,float w,float h)
    { var go=new GameObject(name,typeof(RectTransform)); var rect=go.GetComponent<RectTransform>(); rect.SetParent(parent,false); Place(rect,x,y,w,h); return rect; }
    private static void Place(RectTransform rect,float x,float y,float w,float h)
    { rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h); }
    private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
    public void Dispose() { Backdrop.Dispose(); }
}
