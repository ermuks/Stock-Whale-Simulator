using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Actual unlock progress on two visual meters; rules and explanations expand on demand.</summary>
public sealed class CompactGrowthUI : MonoBehaviour
{
    public TerminalGlyph turnover, profit;
    public TextMeshProUGUI title, turnoverAmount, profitAmount, detailText;
    public GameObject overview;
    public Button details;
    private bool expanded;
    private TextMeshProUGUI nextStep, hint, actionLabel, turnoverCaption, profitCaption;
    private Button primary;
    private bool configured;
    private string feedbackMessage;
    private float feedbackUntil;
    public void Configure(Button action,Button close)
    {
        if (configured)
        { return; }
        configured = true; primary = action; actionLabel = action.GetComponentInChildren<TextMeshProUGUI>();
        Layout((RectTransform)transform,.015f,.012f,.97f,.976f);
        Layout(title.rectTransform,.045f,.04f,.74f,.075f); title.fontSize = 52;
        Layout((RectTransform)details.transform,.83f,.045f,.125f,.06f);
        var detailsLabel = details.GetComponentInChildren<TextMeshProUGUI>(); detailsLabel.text = "조건 보기"; detailsLabel.fontSize = 30;
        nextStep = Caption("Next Step",.045f,.15f,.91f,.085f,49);
        hint = Caption("Action Hint",.045f,.25f,.91f,.055f,31); hint.color = new Color32(166,177,186,255);
        Layout((RectTransform)overview.transform,.045f,.35f,.91f,.36f);
        Layout(turnover.rectTransform,.10f,0,.26f,.42f); Layout(profit.rectTransform,.64f,0,.26f,.42f);
        turnoverCaption = overview.transform.Find("Text :: Turnover Caption").GetComponent<TextMeshProUGUI>();
        profitCaption = overview.transform.Find("Text :: Profit Caption").GetComponent<TextMeshProUGUI>();
        turnoverCaption.fontSize = profitCaption.fontSize = 32;
        turnoverAmount.fontSize = profitAmount.fontSize = 38;
        Layout(detailText.rectTransform,.045f,.35f,.91f,.38f); detailText.fontSize = 32;
        Layout((RectTransform)action.transform,.045f,.80f,.66f,.115f); actionLabel.fontSize = 43;
        Layout((RectTransform)close.transform,.73f,.82f,.225f,.085f); close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;
        primary.image.color = new Color32(18,124,137,255);
        primary.transition = Selectable.Transition.ColorTint;
    }
    private TextMeshProUGUI Caption(string name,float x,float y,float w,float h,float size)
    {
        var label = Instantiate(title,transform); label.name = "Text :: Growth " + name; label.text = ""; label.fontSize = size;
        label.raycastTarget = false; label.enableAutoSizing = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis; Layout(label.rectTransform,x,y,w,h); return label;
    }
    private static void Layout(RectTransform rect,float x,float y,float w,float h)
    {
        rect.anchorMin = new Vector2(x,1-y-h); rect.anchorMax = new Vector2(x+w,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
    void Start() { details.onClick.AddListener(Toggle); }
    private void Toggle()
    {
        expanded = !expanded; overview.SetActive(!expanded); detailText.gameObject.SetActive(expanded);
    }
    public void Refresh(PlayerProgression career)
    {
        DeviceTerminalUI.Set(title,"내 리딩방 개설");
        decimal turnoverLeft = System.Math.Max(0m,career.rules.leadingTurnover-career.Turnover);
        decimal profitLeft = System.Math.Max(0m,career.rules.leadingRealizedProfit-career.RealizedProfit);
        string headline = career.CanOperateRooms ? "리딩방 개설 완료" : career.Eligible ? "이제 내 리딩방을 열 수 있습니다" : turnoverLeft > 0m ? "거래대금 " + turnoverLeft.ToString("N0") + "원 더 필요" : "실현수익 " + profitLeft.ToString("N0") + "원 더 필요";
        DeviceTerminalUI.Set(nextStep,headline);
        DeviceTerminalUI.Set(hint,Time.unscaledTime < feedbackUntil ? feedbackMessage : career.CanOperateRooms ? "내 방에서 참가자를 회유하고 투자 의견을 전달하세요." : career.Eligible ? "아래 개설 버튼을 누르면 운영 권한이 열립니다." : turnoverLeft > 0m ? "매수·매도를 체결해 거래 실적을 쌓으세요." : "보유 주식을 매도해 확정한 손익으로 계산합니다.");
        DeviceTerminalUI.Set(actionLabel,career.CanOperateRooms ? "내 리딩방으로" : career.Eligible ? "리딩방 개설하기" : "매매하러 가기");
        DeviceTerminalUI.Set(turnoverCaption,turnoverLeft == 0m ? "거래대금 · 달성" : "거래대금 · 진행 중");
        DeviceTerminalUI.Set(profitCaption,profitLeft == 0m ? "실현손익 · 달성" : "실현손익 · 진행 중");
        if (primary != null) { primary.interactable = true; }
        turnover.SetValue(career.rules.leadingTurnover > 0 ? (float)(100m * career.Turnover / career.rules.leadingTurnover) : 0f);
        profit.SetValue(career.rules.leadingRealizedProfit > 0 ? (float)(100m * career.RealizedProfit / career.rules.leadingRealizedProfit) : 0f);
        DeviceTerminalUI.Set(turnoverAmount,career.Turnover.ToString("N0") + "원\n/ " + career.rules.leadingTurnover.ToString("N0") + "원");
        DeviceTerminalUI.Set(profitAmount,career.RealizedProfit.ToString("+#,##0;-#,##0;0") + "원\n/ " + career.rules.leadingRealizedProfit.ToString("N0") + "원");
        if (expanded)
        {
            DeviceTerminalUI.Set(detailText,"실제 체결 거래대금 " + career.rules.leadingTurnover.ToString("N0") + "원\n누적 실현손익 " + career.rules.leadingRealizedProfit.ToString("N0") + "원\n두 조건을 충족하면 리딩방을 열 수 있습니다.\n\n미체결 주문·평가이익은 성과에 포함되지 않습니다.\n참가자는 자금과 신뢰에 따라 독립적으로 판단합니다.\n기업 경영은 실제 지분 50% 초과 시 가능합니다.");
        }
    }
    public void ShowFeedback(string message)
    { feedbackMessage = message; feedbackUntil = Time.unscaledTime + 6f; DeviceTerminalUI.Set(hint,message); }
    void OnDestroy()
    { if (details != null) { details.onClick.RemoveListener(Toggle); } }
}
