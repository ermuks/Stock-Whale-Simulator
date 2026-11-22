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
    private TextMeshProUGUI nextStep, hint, actionLabel, turnoverCaption;
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
        var detailsLabel = details.GetComponentInChildren<TextMeshProUGUI>(); GameLocalizedText.Set(detailsLabel,"조건 보기"); detailsLabel.fontSize = 30;
        nextStep = Caption("Next Step",.045f,.15f,.91f,.085f,49);
        hint = Caption("Action Hint",.045f,.25f,.91f,.055f,31); hint.color = new Color32(166,177,186,255);
        Layout((RectTransform)overview.transform,.045f,.35f,.91f,.36f);
        // 조건이 하나라 눈금도 하나만 쓴다. 왼쪽 눈금과 그 글자를 가운데로 옮기고 오른쪽은 끈다.
        Layout(turnover.rectTransform,.37f,0,.26f,.42f); profit.gameObject.SetActive(false);
        turnoverCaption = overview.transform.Find("Text :: Turnover Caption").GetComponent<TextMeshProUGUI>();
        var profitCaption = overview.transform.Find("Text :: Profit Caption").GetComponent<TextMeshProUGUI>();
        turnoverCaption.fontSize = 32; turnoverAmount.fontSize = 38;
        profitCaption.gameObject.SetActive(false); profitAmount.gameObject.SetActive(false);
        Shift(turnoverCaption.rectTransform,.27f); Shift(turnoverAmount.rectTransform,.27f);
        Layout(detailText.rectTransform,.045f,.35f,.91f,.38f); detailText.fontSize = 32;
        Layout((RectTransform)action.transform,.045f,.80f,.66f,.115f); actionLabel.fontSize = 43;
        Layout((RectTransform)close.transform,.73f,.82f,.225f,.085f); close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;
        TabletControlStyle.Button(primary,true);
        primary.transition = Selectable.Transition.ColorTint;
    }
    private TextMeshProUGUI Caption(string name,float x,float y,float w,float h,float size)
    {
        var label = Instantiate(title,transform); label.name = "Text :: Growth " + name; GameLocalizedText.Set(label,""); label.fontSize = size;
        label.raycastTarget = false; label.enableAutoSizing = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis; Layout(label.rectTransform,x,y,w,h); return label;
    }
    private static void Shift(RectTransform rect,float x)
    { rect.anchorMin += new Vector2(x,0); rect.anchorMax += new Vector2(x,0); }
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
        // 개설 조건은 비용 하나뿐이다. 왼쪽 눈금은 주문 가능 현금이 비용에 얼마나 찼는지 보여 준다.
        decimal cost = career.OpeningCost, cash = decimal.Floor(career.OpeningCash);
        decimal left = System.Math.Max(0m,cost-cash);
        string headline = career.CanOperateRooms ? "리딩방 개설 완료" : career.Eligible ? "이제 내 리딩방을 열 수 있습니다" : "개설 비용 " + left.ToString("N0") + "돈 더 필요";
        DeviceTerminalUI.Set(nextStep,headline);
        DeviceTerminalUI.Set(hint,Time.unscaledTime < feedbackUntil ? feedbackMessage : career.CanOperateRooms ? "내 방에서 참가자를 회유하고 투자 의견을 전달하세요." : career.Eligible ? "개설 버튼을 누르면 비용을 내고 방이 열립니다." : "주문 가능 현금을 모으면 바로 개설할 수 있습니다.");
        DeviceTerminalUI.Set(actionLabel,career.CanOperateRooms ? "내 리딩방으로" : career.Eligible ? "리딩방 개설하기" : "매매하러 가기");
        DeviceTerminalUI.Set(turnoverCaption,career.CanOperateRooms ? "개설 비용 · 납부 완료" : left == 0m ? "개설 비용 · 준비 완료" : "개설 비용 · 모으는 중");
        if (primary != null) { primary.interactable = true; }
        turnover.SetValue(career.CanOperateRooms ? 100f : cost > 0m ? (float)System.Math.Min(100m,100m * cash / cost) : 0f);
        DeviceTerminalUI.Set(turnoverAmount,career.CanOperateRooms ? cost.ToString("N0") + "돈" : cash.ToString("N0") + "돈\n/ " + cost.ToString("N0") + "돈");
        if (expanded)
        {
            DeviceTerminalUI.Set(detailText,"개설 비용 " + cost.ToString("N0") + "돈\n주문 가능 현금에서 한 번만 냅니다.\n\n참가자는 자금과 신뢰에 따라 독립적으로 판단합니다.\n기업 경영은 실제 지분 50% 초과 시 가능합니다.");
        }
    }
    public void ShowFeedback(string message)
    { feedbackMessage = message; feedbackUntil = Time.unscaledTime + 6f; DeviceTerminalUI.Set(hint,message); }
    void OnDestroy()
    { if (details != null) { details.onClick.RemoveListener(Toggle); } }
}
