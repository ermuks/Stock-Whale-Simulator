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
        { confirm.GetComponentInChildren<TextMeshProUGUI>().text = "기업 설정하고 시작"; }
        if (manager.economy.retailPlayerStart)
        { companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false; }
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
        confirm.interactable = valid;
        error.text = reason;
        if (manager.economy.retailPlayerStart)
        {
            if (startBalance != null)
            { startBalance.text = manager.economy.retailStartingCash.ToString("N0") + "원"; }
            preview.text = "보유 주식 0주 · 개인 투자자로 시작\n\n봇 운영 리딩방에 가입해 실제 주문과 체결을 살펴보세요.\n매매 성과를 쌓으면 자신의 리딩방을 열 수 있습니다.";
            if (retailPreview != null)
            { retailPreview.text = "첫 가입 · 새벽 관찰실"; }
            return;
        }
        preview.text = data.Shares == 0 ? "입력값을 확인하세요." : $"시작 단가 {data.Price:N0}원  ·  실제 시가총액 {data.MarketCap:N0}원\n공개 {data.PublicShares:N0}개  ·  본인 보유 {data.OwnShares:N0}개\n조달 현금 {data.Funding:N0}원\n최종 초기 현금 {data.FinalCash:N0}원\n거래소 초기 현금 {data.ExchangeCash:N0}원";
    }

    public void Confirm()
    {
        if (!StockManager.Instance.ConfirmPlayer(companyName.text, issuedShares.text, publicPercentage.text, out string reason))
        {
            error.text = reason;
            return;
        }

        confirm.interactable = false;
        NpcLeadingRoom.Instance?.RequestJoin();
        companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false;
        error.text = "초기 자산 설정 중…";
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
