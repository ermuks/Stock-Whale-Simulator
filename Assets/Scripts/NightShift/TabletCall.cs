using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지금 플레이어가 눌러야 하는(누를 수 있고, 누르면 일이 진행되는) 단추를 숨 쉬듯 밝혔다 어둡혔다 한다.
/// 화면마다 한두 개만 켠다. 꺼지면 평소의 납작한 단추로 돌아간다.
/// </summary>
public sealed class TabletCall : MonoBehaviour
{
    private static readonly Color Bright = new Color32(150, 255, 240, 255);
    private Button button;
    private bool on;

    /// <summary>단추를 "지금 할 일"로 켜거나 끈다. 누를 수 없는 단추는 켜지지 않는다.</summary>
    public static void Set(Button button, bool wanted)
    {
        if (button == null)
        { return; }

        var call = button.GetComponent<TabletCall>();
        bool active = wanted && button.interactable;
        if (call == null)
        {
            if (!active)
            { return; }

            call = button.gameObject.AddComponent<TabletCall>();
            call.button = button;
        }

        if (call.on == active)
        { return; }

        call.on = active;
        TabletControlStyle.Emphasize(button, active);
        // 단추 안의 아이콘도 글자와 같은 색으로 맞춘다.
        foreach (var icon in button.GetComponentsInChildren<Image>(true))
        {
            if (icon != button.image && icon.sprite != null) { icon.color = active ? TabletKitStyle.Background : TabletKitStyle.Ink; }
        }
    }

    void Update()
    {
        if (!on || button == null || button.image == null)
        { return; }

        float wave = (Mathf.Sin(Time.unscaledTime * 3.4f) + 1f) * .5f;
        button.image.color = Color.Lerp(TabletKitStyle.Accent, Bright, wave);
    }

    /// <summary>작은 꼬리표(추천, 가능 같은 한두 단어). 채운 바탕에 어두운 글자.</summary>
    public static TextMeshProUGUI Chip(string name, Transform parent, float x, float y, float w, float h, Color fill, float size = 20)
    {
        var plate = TabletKitStyle.Surface(name + " Chip", parent, x, y, w, h, fill);
        var label = TabletKitStyle.Label(name + " Chip", plate.transform, "", 0, 0, 1, 1, size);
        label.alignment = TextAlignmentOptions.Center;
        label.color = TabletKitStyle.Background;
        return label;
    }

    /// <summary>칸 둘레의 테두리 네 변. 켜고 끄는 것은 enabled로 한다.</summary>
    public static Image[] Border(Transform parent, Color color, float thickX = .008f, float thickY = .03f)
    {
        var edges = new[]
        {
            TabletKitStyle.Surface("Border Top", parent, 0, 0, 1, thickY, color), TabletKitStyle.Surface("Border Bottom", parent, 0, 1 - thickY, 1, thickY, color),
            TabletKitStyle.Surface("Border Left", parent, 0, 0, thickX, 1, color), TabletKitStyle.Surface("Border Right", parent, 1 - thickX, 0, thickX, 1, color)
        };
        return edges;
    }

    public static void Show(Image[] edges, bool shown, Color color)
    {
        foreach (var edge in edges)
        {
            if (edge.enabled != shown) { edge.enabled = shown; }
            edge.color = color;
        }
    }
}
