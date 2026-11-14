using TMPro;
using UnityEngine;

/// <summary>사건을 알려주기만 한다. 시장 속도와 일시정지를 변경하지 않는다.</summary>
public sealed class MarketEventFeed : MonoBehaviour
{
    public static MarketEventFeed Instance { get; private set; }
    public TextMeshProUGUI eventText;
    public string LatestEvent { get; private set; } = "시장이 열렸습니다. 호가와 실제 체결, 대화를 함께 살펴보세요.";
    private bool dirty = true;
    void Awake() { Instance = this; }
    public void Notify(string message) { LatestEvent = message; dirty = true; }
    void Update()
    {
        if (!dirty || eventText == null)
        { return; }
        eventText.text = LatestEvent; dirty = false;
    }
    void OnDestroy() { if (Instance == this) { Instance = null; } }
}
