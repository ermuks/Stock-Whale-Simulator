using UnityEngine;
using UnityEngine.UI;

// 눈꺼풀. 위아래에서 내려오고 올라오는 검은 판 두 장이다.
// 잠자기는 침대를 보고 F를 누르는 것으로 바뀌었다(BedSleep). 여기는 눈이 감기고 떠지는 모습만 그린다.
// 일시정지 메뉴의 잠자기 단추는 그대로 남아 있다.
public sealed class SleepPrompt : MonoBehaviour
{
    public static SleepPrompt Instance { get; private set; }
    public GameObject Root { get; private set; }
    private RectTransform upper, lower;
    /// <summary>눈이 감긴 정도(0~1).</summary>
    public float Closed { get; private set; }

    void Awake()
    {
        Instance = this;
        Root = new GameObject("Area :: Eyelids", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Root.transform.SetParent(transform, false);
        var canvas = Root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 28900;
        var scaler = Root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        upper = Panel("Image :: Upper Eyelid", Root.transform); lower = Panel("Image :: Lower Eyelid", Root.transform);
        Root.SetActive(false);
    }
    private static RectTransform Panel(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>(); image.color = Color.black; image.raycastTarget = false;
        return rect;
    }

    /// <summary>눈을 closed(0~1)만큼 감는다. 다 감기면 두 판이 화면 가운데에서 만난다(조금 겹쳐서 틈이 없다).</summary>
    public void Lids(float closed)
    {
        Closed = closed = Mathf.Clamp01(closed);
        bool visible = closed > .001f;
        if (Root.activeSelf != visible) { Root.SetActive(visible); }
        upper.anchorMin = new Vector2(0f, 1f - closed * .52f); upper.anchorMax = Vector2.one;
        lower.anchorMin = Vector2.zero; lower.anchorMax = new Vector2(1f, closed * .52f);
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; }
    }
}
