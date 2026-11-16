using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>새 게임 확정 전 입력·미리보기. 기존 게임에서는 초기 지급을 다시 실행하지 않는다.</summary>
public class PlayerCompanySetupUI : MonoBehaviour
{
    public TMP_InputField companyName, issuedShares, publicPercentage;
    public TextMeshProUGUI preview, error;
    public Button confirm;
    public MouseCameraController mouseController;
    public GameObject retailIntroduction, companySetup, controlsPanel;
    public TextMeshProUGUI startBalance, retailPreview;
    public Button controlsToggle;
    private CursorLockMode previousCursor;
    private bool previousVisible, listenersReady;
    [System.NonSerialized] public TMP_InputField businessName;
    void Start()
    {
        var manager = StockManager.Instance;
        if (manager == null || !manager.useInitialEconomy || manager.EconomyInitialized)
        {
            gameObject.SetActive(false);
            return;
        }

        previousCursor = Cursor.lockState;
        previousVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        companyName.richText = false;
        companyName.SetTextWithoutNotify(manager.economy.companies[0].name);
        issuedShares.SetTextWithoutNotify(manager.economy.companies[0].issuedShares.ToString(System.Globalization.CultureInfo.InvariantCulture));
        publicPercentage.SetTextWithoutNotify(manager.economy.defaultPublicPercentage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        companyName.onValueChanged.AddListener(Changed);
        issuedShares.onValueChanged.AddListener(Changed);
        publicPercentage.onValueChanged.AddListener(Changed);
        confirm.onClick.AddListener(Confirm);
        StockManager.OnInitialEconomyReady += OnReady;
        listenersReady = true;
        if (retailIntroduction != null)
        { retailIntroduction.SetActive(manager.economy.retailPlayerStart); }
        if (companySetup != null)
        { companySetup.SetActive(!manager.economy.retailPlayerStart); }
        if (controlsToggle != null)
        { controlsToggle.onClick.AddListener(ToggleControls); controlsToggle.gameObject.SetActive(manager.economy.retailPlayerStart); }
        if (!manager.economy.retailPlayerStart)
        { GameLocalizedText.Set(confirm.GetComponentInChildren<TextMeshProUGUI>(),"기업 설정하고 시작"); }
        if (manager.economy.retailPlayerStart)
        { companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false; }
        if (manager.economy.retailPlayerStart) { businessName = DailyIncomeUI.CompanyField(this); }
        Refresh();
    }

    void OnEnable()
    {
        if (StockManager.Instance != null && StockManager.Instance.EconomyInitialized)
        {
            gameObject.SetActive(false);
        }
    }

    private void Changed(string value)
    {
        Refresh();
    }

    public void Refresh()
    {
        var manager = StockManager.Instance;
        bool valid = manager.PreviewPlayer(companyName.text, issuedShares.text, publicPercentage.text, out var data, out var reason);
        if (businessName != null && !DailyIncomeController.ValidName(businessName.text))
        { valid = false; reason = "회사 이름 · 공백·태그 없이 2~20자"; }
        confirm.interactable = valid;
        GameLocalizedText.Set(error,reason);
        if (manager.economy.retailPlayerStart)
        {
            if (startBalance != null)
            { GameLocalizedText.Set(startBalance,manager.economy.retailStartingCash.ToString("N0") + "원"); }
            GameLocalizedText.Set(preview,"내 기업 지분 100% · 비상장\n기업 성장 · 주식 투자 · 리딩방");
            if (retailPreview != null)
            { GameLocalizedText.Set(retailPreview,"기업 평가액 " + CorporateEconomy.LoadGrowthRules().founderValue.ToString("N0") + "원"); }
            return;
        }
        GameLocalizedText.Set(preview,data.Shares == 0 ? "입력값을 확인하세요." : $"시작 단가 {data.Price:N0}원  ·  실제 시가총액 {data.MarketCap:N0}원\n공개 {data.PublicShares:N0}개  ·  본인 보유 {data.OwnShares:N0}개\n조달 현금 {data.Funding:N0}원\n최종 초기 현금 {data.FinalCash:N0}원\n거래소 초기 현금 {data.ExchangeCash:N0}원");
    }

    public void Confirm()
    {
        if (businessName != null && (DailyIncomeController.Instance == null || !DailyIncomeController.Instance.NameCompany(businessName.text)))
        { GameLocalizedText.Set(error,"회사 이름을 확인하세요."); return; }
        if (!StockManager.Instance.ConfirmPlayer(companyName.text, issuedShares.text, publicPercentage.text, out string reason))
        {
            GameLocalizedText.Set(error,reason);
            return;
        }

        confirm.interactable = false;
        if (!StockManager.Instance.economy.retailPlayerStart) { GameLocale.ProtectLiteral(companyName.text); }
        NpcLeadingRoom.Instance?.RequestJoin();
        companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false;
        GameLocalizedText.Set(error,"초기 자산 설정 중…");
    }

    private void OnReady()
    {
        Cursor.lockState = previousCursor;
        Cursor.visible = previousVisible;
        if (mouseController != null)
        {
            mouseController.CompleteInitialSetup();
        }

        gameObject.SetActive(false);
    }
    private void ToggleControls()
    {
        if (controlsPanel != null)
        { controlsPanel.SetActive(!controlsPanel.activeSelf); }
    }

    void OnDestroy()
    {
        if (!listenersReady)
        { return; }

        companyName.onValueChanged.RemoveListener(Changed);
        issuedShares.onValueChanged.RemoveListener(Changed);
        publicPercentage.onValueChanged.RemoveListener(Changed);
        confirm.onClick.RemoveListener(Confirm);
        if (controlsToggle != null)
        { controlsToggle.onClick.RemoveListener(ToggleControls); }
        StockManager.OnInitialEconomyReady -= OnReady;
    }
}
