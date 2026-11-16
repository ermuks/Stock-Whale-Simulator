using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Navigation inside the existing tablet Canvas. No camera or device pose changes.</summary>
[DefaultExecutionOrder(300)]
public sealed class TabletAccountUI : MonoBehaviour
{
    public enum Page { Dashboard, Events, Records, Detail, Orders, Automation, Management, Special, Unlocks }
    private TabletKitLayout kit;
    public PlayerAccounts accounts;
    public Trader account;
    public bool second;
    public DeviceTerminalUI orders, events;
    public InvestmentDashboard dashboard;
    public HoldUIManager holdings;
    public GameObject contentRoot, lockRoot, recordsPanel, transferPanel;
    public GameObject[] screens;
    public Button[] tabs;
    public Image[] tabLines;
    public Button orderButton, transferButton, autoButton, managementButton, transferSubmit, transferCancel;
    public TMP_InputField transferAmount;
    public TextMeshProUGUI clock, accountTitle, ledger, transferStatus, transferTitle;
    public RectTransform ledgerContent;
    public Page CurrentPage { get; private set; }
    private float next;
    private int seenVersion = -1;
    private readonly StringBuilder text = new();
    public bool Locked => second && !accounts.AutomatedUnlocked;
    void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        { int index = i; tabs[i].onClick.AddListener(() => ShowPage((Page)index)); }
        orderButton.onClick.AddListener(() => ShowPage(Page.Orders));
        autoButton.onClick.AddListener(() => ShowPage(Page.Automation));
        managementButton.onClick.AddListener(() => ShowPage(Page.Management));
        transferButton.onClick.AddListener(OpenTransfer);
        transferCancel.onClick.AddListener(() => transferPanel.SetActive(false));
        transferSubmit.onClick.AddListener(Transfer);
        accounts.Unlocked += Unlock;
        DeviceTerminalUI.Set(accountTitle,second ? "2번 계좌 · 자동 운용" : "1번 계좌 · 내 투자");
        lockRoot.SetActive(Locked); contentRoot.SetActive(!Locked);
        events.newsReader = NewsDeskUI.Install(this);
        TabletControlStyle.Apply(this);
        if (second) { SpreadTradingUI.Install(this); }
        else { DailyIncomeUI.InstallTablet(this); }
        CompanyDashboardUI.Install(this);
        kit=TabletKitLayout.Install(this);
        ShowPage(second ? Page.Automation : Page.Dashboard);
    }
    private void Unlock()
    {
        if (!second)
        { return; }
        lockRoot.SetActive(false); contentRoot.SetActive(true); ShowPage(Page.Automation);
    }
    public void OpenExisting(DeviceTerminalUI screen)
    { ShowPage(screen == orders ? Page.Orders : Page.Events); }
    public void ShowPage(Page page)
    {
        if (Locked)
        { return; }
        if (page == Page.Automation && !second)
        { return; }
        if (page == Page.Unlocks && second)
        { return; }
        if (page == Page.Detail && (dashboard.Selected == null || account.GetOwnedVolume(dashboard.Selected) <= 0m))
        { page = Page.Dashboard; }
        foreach (var screen in screens) { screen.SetActive(false); }
        transferPanel.SetActive(false);
        CurrentPage = page;
        clock.gameObject.SetActive(true);
        if (page == Page.Dashboard) { dashboard.gameObject.SetActive(true); dashboard.Refresh(); }
        else if (page == Page.Events) { events.ShowLocal(); events.newsReader.OpenReader(); }
        else if (page == Page.Orders) { orders.ShowLocal(); }
        else if (page == Page.Detail)
        { holdings.selectedOverride = dashboard.Selected; holdings.gameObject.SetActive(true); }
        else if (page == Page.Records) { recordsPanel.SetActive(true); seenVersion = -1; }
        else if (page == Page.Automation) { orders.legacy.ShowDesk(); }
        else if (page == Page.Management) { events.legacy.ShowDesk(); GetComponentInChildren<CompanyDashboardUI>(true)?.OpenGrowth(); }
        else if (page == Page.Special && second) { events.ShowLocal(); events.newsReader.OpenStudio(); }
        else if (page == Page.Unlocks) { GetComponentInChildren<UnlockGridUI>(true)?.Show(); }
        for (int i = 0; i < tabs.Length; i++)
        { tabs[i].image.color = (int)page == i ? new Color32(26,43,49,255) : Color.clear; tabLines[i].enabled = (int)page == i; }
        next = 0f;
        kit?.RefreshTabs();
    }
    void Update()
    {
        if (Locked || Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .3f;
        var day = MarketDayController.Instance;
        DeviceTerminalUI.Set(clock,day == null ? "DAY —  —:—" : "DAY " + day.CalendarDay + "  " + DeviceMarketJournal.Stamp());
        transferButton.interactable = accounts.AutomatedUnlocked;
        tabs[3].interactable = dashboard.Selected != null && account.GetOwnedVolume(dashboard.Selected) > 0m;
        int version=events.records.Version+orders.session.Version;
        if (CurrentPage != Page.Records || seenVersion == version)
        { return; }
        seenVersion = version;
        text.Clear();
        foreach (var fill in events.records.Fills)
        {
            text.Append(fill.Time).Append("   ").Append(fill.Stock.stockName).Append("   ").Append(fill.Buy ? "매수" : "매도")
                .Append("   ").Append(fill.Quantity.ToString("N0")).Append("주   ").Append(DeviceTerminalUI.Money(fill.Price));
            if (!fill.Buy) { text.Append("   실현 ").Append(InvestmentDashboard.Signed(fill.Realized)).Append("원"); }
            text.AppendLine().AppendLine();
        }
        foreach (var request in orders.session.History)
        {
            text.Append(request.UpdatedAt ?? "—").Append("   ").Append(request.Stock.stockName).Append("   ").Append(request.Status)
                .Append("   ").Append(request.Filled.ToString("N0")).Append(" / ").Append(request.Quantity.ToString("N0")).AppendLine("주").AppendLine();
        }
        DeviceTerminalUI.Set(ledger,text.Length == 0 ? "—" : text.ToString());
        ledgerContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Math.Max(120,(events.records.Fills.Count+orders.session.History.Count) * 110));
    }
    private void OpenTransfer()
    {
        if (!accounts.AutomatedUnlocked)
        { return; }
        transferAmount.SetTextWithoutNotify("");
        DeviceTerminalUI.Set(transferTitle,second ? "2번 계좌 → 1번 계좌" : "1번 계좌 → 2번 계좌");
        DeviceTerminalUI.Set(transferStatus,"이체 가능 " + DeviceTerminalUI.Money(PlayerAccounts.Available(account)));
        transferPanel.SetActive(true);
    }
    private void Transfer()
    {
        decimal.TryParse(transferAmount.text,NumberStyles.None,CultureInfo.InvariantCulture,out decimal value);
        bool success = accounts.Transfer(account,value,out string reason);
        DeviceTerminalUI.Set(transferStatus,reason);
        if (success) { transferAmount.SetTextWithoutNotify(""); dashboard.Refresh(); }
    }
    void OnDestroy()
    { if (accounts != null) { accounts.Unlocked -= Unlock; } }
}
