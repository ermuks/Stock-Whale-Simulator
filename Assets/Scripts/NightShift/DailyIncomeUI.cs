using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 시작 화면, 채팅 Canvas, 1번 태블릿에 필요한 짧은 입력/정산만 추가한다.</summary>
public sealed class DailyIncomeUI : MonoBehaviour
{
    private TextMeshProUGUI currentFee, paidCount, escrow, result, day, company;
    private TextMeshProUGUI[] values;
    private TextMeshProUGUI[] history;
    private TextMeshProUGUI transfer, closingAssets, dayResult, closingCash, reliefState, dividends;
    private TMP_InputField feeInput;
    private GameObject panel;
    private TMP_FontAsset font;
    private float scale, next;
    private bool roomMode;
    private int originalSibling;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(146,161,172,255), Dark = new Color32(13,23,29,255);

    public static TMP_InputField CompanyField(PlayerCompanySetupUI setup)
    {
        if (setup.retailIntroduction == null)
        { return null; }
        var root = setup.retailIntroduction.transform;
        var caption = Instantiate(setup.retailPreview,root); caption.name = "Text :: Private Company Name"; GameLocalizedText.Set(caption,"회사 이름");
        Place(caption.rectTransform,root,0,.62f,.51f,.045f); caption.raycastTarget = false;
        var field = Instantiate(setup.companyName,root); field.name = "Input :: Private Company Name";
        Place((RectTransform)field.transform,root,0,.675f,.51f,.075f); field.gameObject.SetActive(true); field.interactable = true;
        field.onValueChanged.RemoveAllListeners(); field.characterLimit = 20; field.contentType = TMP_InputField.ContentType.Standard; field.richText = false;
        field.SetTextWithoutNotify(DailyIncomeController.Instance?.CompanyName ?? "내 작은 회사");
        TabletControlStyle.Input(field); field.textComponent.alignment = TextAlignmentOptions.MidlineLeft; field.textComponent.fontSize = 30f;
        field.onValueChanged.AddListener(_ => setup.Refresh());
        return field;
    }
    public static void InstallRoom(LeadingRoomUI rooms)
    {
        if (rooms.playerControls == null)
        { return; }
        var view = rooms.playerControls.AddComponent<DailyIncomeUI>(); view.roomMode = true; view.Initialize();
        var status = rooms.commandStatus.rectTransform; status.anchorMax = new Vector2(.72f,status.anchorMax.y);
        view.Action(rooms.playerControls.transform,"Room Fee","이용료",.74f,.778f,.23f,.029f,() => view.Open(),false,38);
        view.panel = view.Box("Room Fee",rooms.playerControls.transform,.035f,.59f,.93f,.39f,true).gameObject;
        Transform root = view.panel.transform;
        view.Label(root,"Fee Heading","하루 이용료",.045f,.04f,.6f,.065f,58);
        view.Action(root,"Close Fee","닫기",.79f,.035f,.16f,.07f,view.Close,false,44);
        view.currentFee = view.Metric(root,"오늘",.17f,48);
        view.Label(root,"Next Fee Label","다음 장",.045f,.32f,.25f,.075f,48).color = Muted;
        view.feeInput = Instantiate(rooms.buyPrice,root); view.feeInput.name = "Input :: Daily Room Fee";
        Place((RectTransform)view.feeInput.transform,root,.34f,.32f,.61f,.075f); view.feeInput.gameObject.SetActive(true);
        view.feeInput.onValueChanged.RemoveAllListeners(); view.feeInput.characterLimit = 13; view.feeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        TabletControlStyle.Input(view.feeInput); TradingOrderView.Numeric(view.feeInput.textComponent); view.feeInput.fontAsset = view.feeInput.textComponent.font;
        view.feeInput.textComponent.fontSize = 50*view.scale; view.feeInput.textComponent.alignment = TextAlignmentOptions.MidlineRight;
        view.paidCount = view.Metric(root,"유료 회원",.47f,48,"명");
        view.escrow = view.Metric(root,"정산 대기",.60f,48);
        view.result = view.Label(root,"Fee Result","",.045f,.735f,.91f,.05f,40); view.result.color = Muted;
        view.Action(root,"Apply Fee","다음 장 적용",.045f,.83f,.91f,.08f,view.Apply,true,46);
        view.panel.SetActive(false);
    }
    public static void InstallTablet(TabletAccountUI tablet)
    {
        if (tablet.second)
        { return; }
        var root = tablet.dashboard.transform;
        var view = root.gameObject.AddComponent<DailyIncomeUI>(); view.Initialize();
        view.Action(root,"Income Statement","정산",.21f,.95f,.18f,.049f,view.Open,false,27);
        view.panel = view.Box("Income Statement",root,0,0,1,1,true).gameObject;
        root = view.panel.transform;
        view.company = view.Label(root,"Company","",.045f,.025f,.62f,.055f,32);
        view.day = view.Label(root,"Closing Day","",.60f,.095f,.35f,.045f,24); view.day.alignment = TextAlignmentOptions.MidlineRight; view.day.color = Muted;
        view.Action(root,"Close Statement","닫기",.84f,.025f,.11f,.06f,view.Close,false,26);
        view.Label(root,"Transfer Label","정산 입출금",.045f,.115f,.49f,.05f,27).color = Muted;
        view.transfer = view.Number(root,"Settlement Transfer",.045f,.18f,.49f,.10f,50);
        view.Label(root,"Assets Label","마감 총자산",.61f,.17f,.34f,.05f,25).color = Muted;
        view.closingAssets = view.Number(root,"Closing Assets",.61f,.23f,.34f,.065f,31);
        view.Rule(root,.32f);
        string[] captions = {"실현 매매손익","회사 수익","리딩방 이용료","최초 손실 구제","매매세 0.05%","이전 미납","이번 납부","남은 미납"};
        view.values = new TextMeshProUGUI[captions.Length];
        for (int i = 0; i < captions.Length; i++)
        {
            float x = i < 4 ? .045f : .535f, y = .36f + (i%4)*.078f;
            view.Label(root,captions[i]+" Label",captions[i],x,y,.165f,.06f,26).color = Muted;
            view.values[i] = view.Number(root,captions[i]+" Value",x+.17f,y,.245f,.06f,29);
        }
        view.reliefState = view.Label(root,"Relief State","",.045f,.665f,.45f,.035f,21); view.reliefState.color = Muted;
        view.Label(root,"Dividend Label","기업 배당",.535f,.665f,.165f,.035f,21).color = Muted;
        view.dividends = view.Number(root,"Company Dividend",.705f,.665f,.245f,.035f,24);
        view.Rule(root,.717f);
        view.Label(root,"Day Result Label","당일 순손익",.045f,.746f,.165f,.06f,27).color = Muted;
        view.dayResult = view.Number(root,"Day Result",.215f,.746f,.245f,.06f,30);
        view.Label(root,"Closing Cash Label","기본계좌 현금",.535f,.746f,.165f,.06f,27).color = Muted;
        view.closingCash = view.Number(root,"Closing Cash",.705f,.746f,.245f,.06f,30);
        view.history = new TextMeshProUGUI[2];
        for (int i = 0; i < view.history.Length; i++)
        { view.history[i] = view.Label(root,"Statement Row " + i,"",.045f,.855f+i*.052f,.905f,.04f,23); view.history[i].color = Muted; }
        view.panel.SetActive(false);
    }
    private void Initialize()
    {
        font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        scale = ((RectTransform)transform).rect.width / 1600f;
        if (scale <= 0f) { scale = 1f; }
    }
    public void OpenStatement()
    { GetComponentInParent<TabletAccountUI>().ShowPage(TabletAccountUI.Page.Dashboard); Open(); }
    private void Open()
    {
        if (roomMode && DailyIncomeController.Instance != null)
        { feeInput.SetTextWithoutNotify(DailyIncomeController.Instance.Ledger.NextFee.ToString()); GameLocalizedText.Set(result,""); }
        if (roomMode && !panel.activeSelf) { originalSibling = transform.GetSiblingIndex(); transform.SetAsLastSibling(); }
        panel.SetActive(true); panel.transform.SetAsLastSibling(); next = 0f; Refresh();
    }
    private void Close()
    {
        if (panel == null || !panel.activeSelf)
        { return; }
        panel.SetActive(false);
        if (roomMode) { transform.SetSiblingIndex(originalSibling); }
    }
    void OnDisable() { Close(); }
    private void Apply()
    {
        var income = DailyIncomeController.Instance;
        if (income == null)
        { return; }
        income.SetNextFee(feeInput.text); GameLocalizedText.Set(result,income.Feedback); next = 0f;
    }
    void Update()
    {
        if (panel == null || !panel.activeInHierarchy || Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .5f; Refresh();
    }
    private void Refresh()
    {
        var income = DailyIncomeController.Instance;
        if (income == null)
        { return; }
        var ledger = income.Ledger;
        if (roomMode)
        {
            DeviceTerminalUI.Set(currentFee,ledger.Fee.ToString("N0"));
            DeviceTerminalUI.Set(paidCount,ledger.PayingMembers.ToString("N0"));
            DeviceTerminalUI.Set(escrow,ledger.Escrow.ToString("N0")); return;
        }
        DeviceTerminalUI.Set(company,income.CompanyName);
        var reports = ledger.Reports;
        var last = reports.Count == 0 ? null : reports[reports.Count-1];
        DeviceTerminalUI.Set(day,last == null ? "정산 전" : "DAY " + last.day + " · 장 마감");
        string[] texts = last == null ? new[] {"—","—","—","—","—","—","—","—"} : new[] {
            InvestmentDashboard.Signed(last.Realized),last.company.ToString("N0"),last.fees.ToString("N0"),last.Relief.ToString("N0"),
            last.Tax.ToString("N0"),last.PriorTax.ToString("N0"),last.TaxPaid.ToString("N0"),last.UnpaidTax.ToString("N0")};
        for (int i=0;i<values.Length;i++) { DeviceTerminalUI.Set(values[i],texts[i]); }
        DeviceTerminalUI.Set(transfer,last == null ? "—" : InvestmentDashboard.Signed(last.Transfer));
        DeviceTerminalUI.Set(closingAssets,last == null ? "—" : last.Assets.ToString("N0"));
        DeviceTerminalUI.Set(dayResult,last == null ? "—" : InvestmentDashboard.Signed(last.DayResult));
        DeviceTerminalUI.Set(dividends,last == null ? "—" : last.Dividends.ToString("N0"));
        DeviceTerminalUI.Set(closingCash,last == null || last.version < 2 ? "—" : last.Cash.ToString("N0"));
        DeviceTerminalUI.Set(reliefState,ledger.ReliefDay == 0 ? "손실 구제 1회 남음" : "손실 구제 사용 · DAY " + ledger.ReliefDay);
        if (last != null)
        {
            values[0].color = InvestmentDashboard.ProfitColor(last.Realized); values[3].color = last.Relief > 0m ? new Color32(77,182,190,255) : Ink;
            values[7].color = last.UnpaidTax > 0m ? new Color32(222,164,96,255) : Ink;
            transfer.color = InvestmentDashboard.ProfitColor(last.Transfer); dayResult.color = InvestmentDashboard.ProfitColor(last.DayResult);
        }
        for (int i=0;i<history.Length;i++)
        {
            int index = reports.Count-1-i;
            DeviceTerminalUI.Set(history[i],index < 0 ? "" : "DAY " + reports[index].day + "     정산 " + InvestmentDashboard.Signed(reports[index].Transfer) + "원     세금 " + reports[index].Tax.ToString("N0") + "원     구제 " + reports[index].Relief.ToString("N0") + "원");
        }
    }
    private TextMeshProUGUI Number(Transform parent,string name,float x,float y,float width,float height,float size)
    {
        var value = Label(parent,name,"",x,y,width-.029f,height,size); TradingOrderView.Numeric(value); value.alignment = TextAlignmentOptions.MidlineRight;
        var unit = Label(parent,name+" Unit","원",x+width-.023f,y,.023f,height,24); unit.alignment = TextAlignmentOptions.MidlineRight; unit.color = Muted;
        return value;
    }
    private void Rule(Transform parent,float y)
    {
        var line = Box("Settlement Divider",parent,.045f,y,.905f,.0015f,false).gameObject.AddComponent<Image>();
        line.color = new Color32(53,70,77,170); line.raycastTarget = false;
    }
    private static void Place(RectTransform rect,Transform parent,float x,float y,float w,float h)
    {
        rect.SetParent(parent,false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = new Vector2(x,1-y-h); rect.anchorMax = new Vector2(x+w,1-y);
        rect.pivot = new Vector2(.5f,.5f); rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
    private RectTransform Box(string name,Transform parent,float x,float y,float w,float h,bool background)
    {
        var obj = new GameObject((background ? "Panel :: " : "Area :: ")+name,typeof(RectTransform));
        var rect = (RectTransform)obj.transform; Place(rect,parent,x,y,w,h);
        if (background) { var image = obj.AddComponent<Image>(); image.color = Dark; image.raycastTarget = true; }
        return rect;
    }
    private TextMeshProUGUI Label(Transform parent,string name,string text,float x,float y,float w,float h,float size)
    {
        var rect = Box(name,parent,x,y,w,h,false); rect.name = "Text :: " + name;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size*scale; label.fontStyle = FontStyles.Normal;
        label.raycastTarget = false; label.enableAutoSizing = false; label.color = Ink; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; GameLocalizedText.Set(label,text); return label;
    }
    private TextMeshProUGUI Metric(Transform parent,string caption,float y,float size=34,string unit="원")
    {
        Label(parent,caption+" Label",caption,.045f,y,.38f,.065f,size).color = Muted;
        var value = Label(parent,caption+" Value","",.43f,y,.47f,.065f,size+2); TradingOrderView.Numeric(value); value.alignment = TextAlignmentOptions.MidlineRight;
        var suffix = Label(parent,caption+" Unit",unit,.912f,y,.038f,.065f,size); suffix.alignment = TextAlignmentOptions.MidlineRight; suffix.color = Muted;
        return value;
    }
    private Button Action(Transform parent,string name,string caption,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,bool primary,float size)
    {
        var rect = Box(name,parent,x,y,w,h,false); rect.name = "Button :: " + name;
        var image = rect.gameObject.AddComponent<Image>(); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Label(rect,name,caption,.02f,0,.96f,1,size); label.alignment = TextAlignmentOptions.Center;
        TabletControlStyle.Button(button,primary); label.fontSize = size*scale; label.fontStyle = FontStyles.Normal;
        button.onClick.AddListener(action); return button;
    }
}
