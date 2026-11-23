using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지금 글을 쓰고 있는 입력 칸에 밝은 테두리를 둘러, 키 입력이 어디로 들어가는지 한눈에 보이게 한다.
/// 태블릿을 든 채 걸어 다닐 수 있어서(WASD), 글을 쓰는 중인지 아닌지가 확실히 보여야 한다.
/// 모든 입력 칸(TMP_InputField)에 저절로 적용된다. 칸의 값이나 배치는 건드리지 않는다.
/// 글을 쓰는 중의 Esc는 메뉴를 열지 않고 입력만 끝낸다(PauseMenuController, DeskViewController, TabletViewController가 MouseCameraController.TextEntryEscape를 본다).
/// </summary>
public sealed class TextEntryHighlight : MonoBehaviour
{
    private static readonly Color Accent = new(.18f, .90f, .82f, 1f);
    private RectTransform frame;
    private readonly Image[] bars = new Image[4];
    private Image wash;
    private TMP_InputField current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var root = new GameObject("System :: Text Entry Highlight"); DontDestroyOnLoad(root);
        root.AddComponent<TextEntryHighlight>();
    }

    void LateUpdate()
    {
        var field = MouseCameraController.ActiveTextEntry;
        if (field != current)
        {
            current = field;
            if (field == null) { if (frame != null) { frame.gameObject.SetActive(false); frame.SetParent(transform, false); } return; }
            // Esc는 쓰던 글을 지우지 않고 입력만 끝낸다.
            field.restoreOriginalTextOnEscape = false;
            Attach(field);
        }
        if (current == null || frame == null) { return; }
        // 살짝 숨 쉬듯 밝아졌다 어두워진다.
        float pulse = .78f + .22f * Mathf.Sin(Time.unscaledTime * 5f);
        var tone = Accent; tone.a = pulse;
        foreach (var bar in bars) { if (bar != null) { bar.color = tone; } }
    }

    private void Attach(TMP_InputField field)
    {
        // 칸이 사라지면 그 자식이던 테두리도 같이 사라진다. 그때는 새로 만든다.
        if (frame == null) { Build(); }
        var host = (RectTransform)field.transform;
        frame.SetParent(host, false); frame.SetAsLastSibling();
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.offsetMin = frame.offsetMax = Vector2.zero; frame.localScale = Vector3.one; frame.localRotation = Quaternion.identity;
        SetLayer(frame, host.gameObject.layer);
        // 굵기는 칸의 높이에 맞춘다(화면마다 Canvas의 크기가 다르다). 가려지지 않게 칸 안쪽에 그린다.
        float thick = Mathf.Clamp(host.rect.height * .07f, 2f, 7f);
        Place(bars[0], new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thick), Vector2.zero);
        Place(bars[1], new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thick));
        Place(bars[2], new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thick, 0f));
        Place(bars[3], new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thick, 0f), Vector2.zero);
        frame.gameObject.SetActive(true);
    }

    private void Build()
    {
        var holder = new GameObject("Image :: Text Entry Focus", typeof(RectTransform), typeof(LayoutElement));
        frame = (RectTransform)holder.transform; holder.GetComponent<LayoutElement>().ignoreLayout = true;
        wash = Piece("Wash"); wash.color = new Color(Accent.r, Accent.g, Accent.b, .07f);
        var rect = wash.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        for (int i = 0; i < bars.Length; i++) { bars[i] = Piece("Edge " + i); }
    }

    private Image Piece(string name)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(frame, false);
        var image = item.GetComponent<Image>(); image.raycastTarget = false; image.color = Accent;
        return image;
    }

    private static void Place(Image bar, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = bar.rectTransform; rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }

    private static void SetLayer(Transform item, int layer)
    {
        item.gameObject.layer = layer;
        for (int i = 0; i < item.childCount; i++) { SetLayer(item.GetChild(i), layer); }
    }
}
