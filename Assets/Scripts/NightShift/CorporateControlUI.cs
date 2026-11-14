using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CorporateControlUI : MonoBehaviour
{
    public Trader account;
    public Button management, investment, dividend, acquisition;
    public TextMeshProUGUI status;
    private Stock controlled;
    private float next;
    void Start()
    {
        management.onClick.AddListener(Management); investment.onClick.AddListener(Investment);
        dividend.onClick.AddListener(Dividend); acquisition.onClick.AddListener(Acquisition);
    }
    private void Management() { WorldDramaDirector.Instance?.BoardDecision(controlled, 0, account); }
    private void Investment() { WorldDramaDirector.Instance?.BoardDecision(controlled, 1, account); }
    private void Dividend() { WorldDramaDirector.Instance?.BoardDecision(controlled, 2, account); }
    private void Acquisition() { WorldDramaDirector.Instance?.AcquireShares(controlled, StockManager.Instance.CurrentStock, account); }
    void Update()
    {
        if (Time.unscaledTime < next || WorldDramaDirector.Instance == null || StockManager.Instance == null)
        { return; }
        next = Time.unscaledTime + .5f;
        var current = StockManager.Instance.CurrentStock;
        if (WorldDramaDirector.Instance.Controls(current)) { controlled = current; }
        bool can = WorldDramaDirector.Instance.Controls(controlled);
        management.interactable = investment.interactable = dividend.interactable = can && MarketDayController.CanTrade;
        acquisition.interactable = can && current != controlled && MarketDayController.CanTrade;
        string text = can ? controlled.stockName + " 경영권 확보 · 매입 대상 " + current.stockName : "경영권: 실제 지분 50% 초과 필요";
        if (status.text != text) { status.text = text; }
    }
    void OnDestroy()
    {
        if (management != null) { management.onClick.RemoveListener(Management); }
        if (investment != null) { investment.onClick.RemoveListener(Investment); }
        if (dividend != null) { dividend.onClick.RemoveListener(Dividend); }
        if (acquisition != null) { acquisition.onClick.RemoveListener(Acquisition); }
    }
}
