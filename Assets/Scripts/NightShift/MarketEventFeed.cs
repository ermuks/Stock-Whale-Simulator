using TMPro;
using UnityEngine;

/// <summary>사건을 알려주기만 한다. 시장 속도와 일시정지를 변경하지 않는다.</summary>
public sealed class MarketEventFeed : MonoBehaviour
{
    public static MarketEventFeed Instance { get; private set; }
    public TextMeshProUGUI eventText;
    public string LatestEvent { get; private set; } = "시장이 열렸습니다. 호가와 실제 체결, 대화를 함께 살펴보세요.";
    private bool dirty = true;
    [Min(1f)] public float displaySeconds = 6f;
    private float visibleUntil;
    void Awake() { Instance = this; visibleUntil = Time.unscaledTime + displaySeconds; }
    public void Notify(string message) { LatestEvent = message; dirty = true; visibleUntil = Time.unscaledTime + displaySeconds; }
    void Update()
    {
        if (eventText == null)
        { return; }
        bool visible = Time.unscaledTime < visibleUntil;
        if (eventText.enabled != visible) { eventText.enabled = visible; }
        if (dirty) { GameLocalizedText.Set(eventText,LatestEvent); dirty = false; }
    }
    void OnDestroy() { if (Instance == this) { Instance = null; } }
}
