using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 HUD 안의 작은 키캡 안내. 비트맵에는 문자를 넣지 않고 모든 문구를 SDF TMP로 분리한다.</summary>
public sealed class DeskControlHints : MonoBehaviour
{
    [Serializable]
    public sealed class Labels
    {
        public string chart = "차트 확대", interact = "기기 열기", tablets = "태블릿", phone = "휴대폰";
        public string stocks = "종목 선택", pause = "일시정지", resume = "계속하기";
        public string back = "돌아가기", screens = "화면 이동", accounts = "계좌 이동", close = "내려놓기";
    }
    public DeskViewController views;
    public TMP_FontAsset sdfFont;
    public Sprite keycap;
    public Labels labels = new Labels();
    public bool showShortcutHints;
    private RectTransform hints, markerRect;
    private DeskInteractionMarkers markers;
    private readonly TextMeshProUGUI[] keys = new TextMeshProUGUI[6], captions = new TextMeshProUGUI[6];
    private readonly RectTransform[] cells = new RectTransform[6];
    private MouseCameraController mouseLook;
    private TabletAccountUI[] tabletNavigation;
    private int state = -1;
    private bool wasVisible;

    void Start()
    {
        if (views == null)
        { views = DeskViewController.Instance; }
        if (views == null)
        { enabled = false; return; }
        mouseLook = views.GetComponent<MouseCameraController>();
        tabletNavigation = FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include);
        if (sdfFont == null)
        { sdfFont = Resources.Load<TMP_FontAsset>("Chat/Chat Korean"); }
        if (keycap == null)
        { keycap = Resources.Load<Sprite>("UI/TradingTerminal/Keycap_Background"); }
        hints = Rect("Area :: Shortcut Hints", transform, new Vector2(18f, 16f), new Vector2(600f, 70f));
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = Rect("Area :: Shortcut " + i, hints, new Vector2(i % 3 * 200f, (1 - i / 3) * 36f), new Vector2(194f, 32f));
            var cap = Rect("Image :: Keycap", cells[i], Vector2.zero, new Vector2(70f, 32f));
            var image = cap.gameObject.AddComponent<Image>(); image.sprite = keycap; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 32f; image.fillCenter = true; image.color = new Color(1f, 1f, 1f, .82f); image.raycastTarget = false;
            keys[i] = Text("Text :: Key", cap, Vector2.zero, new Vector2(70f, 32f), 17f, new Color32(230, 235, 239, 245));
            keys[i].alignment = TextAlignmentOptions.Center;
            captions[i] = Text("Text :: Action", cells[i], new Vector2(78f, 0f), new Vector2(116f, 32f), 15f, new Color32(174, 187, 195, 235));
            captions[i].alignment = TextAlignmentOptions.MidlineLeft;
        }
        markerRect = Rect("Image :: Device Interaction Circles", transform, Vector2.zero, Vector2.zero);
        markerRect.anchorMax = Vector2.one; markerRect.offsetMin = markerRect.offsetMax = Vector2.zero;
        markers = markerRect.gameObject.AddComponent<DeskInteractionMarkers>();
        hints.gameObject.SetActive(false);
    }

    /// <summary>번역 시스템에서 문구와 해당 언어의 SDF 폰트/폴백을 교체하는 진입점.</summary>
    public void SetLanguage(Labels translated, TMP_FontAsset font)
    {
        if (translated != null)
        { labels = translated; }
        if (font != null)
        { sdfFont = font; }
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == null)
            { continue; }
            keys[i].font = sdfFont; keys[i].fontSharedMaterial = sdfFont.material;
            captions[i].font = sdfFont; captions[i].fontSharedMaterial = sdfFont.material;
        }
        state = -1;
    }

    void LateUpdate()
    {
        if (views == null || hints == null)
        { return; }
        bool shown = !PauseMenuController.BlocksInput && views.Ready && !MouseCameraController.TextEntryActive && !ViewingOrderForm();
        bool showLabels = shown && showShortcutHints;
        if (showLabels != wasVisible)
        { hints.gameObject.SetActive(showLabels); wasVisible = showLabels; }
        bool phone = PhoneCallController.IsRaised;
        bool paused = mouseLook != null && mouseLook.IsPaused;
        int next = ((int)views.Target + 1) * 4 + (phone ? 2 : 0) + (paused ? 1 : 0);
        if (showLabels && next != state)
        { state = next; RefreshLabels(phone, paused); }
        bool desk = shown && !views.IsFocused && !views.IsTransitioning && !phone;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, MouseCameraController.GetAimScreenPosition(), null, out Vector2 aim);
        // 원은 늘 떠 있지 않는다. 화면 가운데(조준점)가 이 거리 안으로 들어와야 테두리 원이 보이기 시작한다.
        float range = markerRect.rect.height * .17f;
        for (int i = 0; i < 5; i++)
        {
            RectTransform screen = views.ScreenFor((DeskViewTarget)i);
            Vector2 local = Vector2.zero; bool visible = false; float closeness = 0f;
            if (desk && screen != null && screen.gameObject.activeInHierarchy)
            {
                Vector3 center = screen.TransformPoint(screen.rect.center);
                Vector3 point = views.ViewCamera.WorldToScreenPoint(center);
                visible = point.z > views.ViewCamera.nearClipPlane && views.ViewCamera.pixelRect.Contains(point) && Vector3.Dot(screen.forward, center - views.ViewCamera.transform.position) > 0f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, point, null, out local);
                // 화면은 넓으므로 가운데가 아니라 화면의 테두리까지의 거리로 잰다(화면 안이면 0).
                if (visible) { closeness = Closeness(DistanceToScreen(screen, aim), range); }
            }
            markers.SetMarker(i, local, visible, desk && (int)views.Hovered == i, closeness);
        }
        // 서랍도 모니터처럼 원으로 표시한다. 바라보면 원이 채워진다.
        var drawer = DrawerProp.Instance;
        Vector2 drawerLocal = Vector2.zero; bool drawerVisible = false;
        if (desk && drawer != null && drawer.MarkerPoint(out Vector3 handle))
        { drawerVisible = MarkerAt(handle, out drawerLocal); }
        bool drawerHovered = drawerVisible && drawer.Hovered;
        markers.SetMarker(DeskInteractionMarkers.Drawer, drawerLocal, drawerVisible, drawerHovered, Closeness((aim - drawerLocal).magnitude, range * 1.25f));
        // 책상 위의 휴대폰.
        var handset = DeskPhone.Instance;
        Vector2 phoneLocal = Vector2.zero; bool phoneVisible = false;
        if (desk && handset != null && handset.MarkerPoint(out Vector3 spot))
        { phoneVisible = MarkerAt(spot, out phoneLocal); }
        bool phoneHovered = phoneVisible && DeskPhone.Aiming;
        markers.SetMarker(DeskInteractionMarkers.Phone, phoneLocal, phoneVisible, phoneHovered, Closeness((aim - phoneLocal).magnitude, range * 1.25f));
        markers.SetAim(aim, desk && MouseCameraController.IsCursorLocked && views.Hovered == DeskViewTarget.None && !drawerHovered && !phoneHovered);
    }

    /// <summary>세계의 한 점을 원을 그리는 판 위의 자리로 옮긴다. 화면 밖이면 false.</summary>
    private bool MarkerAt(Vector3 world, out Vector2 local)
    {
        Vector3 point = views.ViewCamera.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, point, null, out local);
        return point.z > views.ViewCamera.nearClipPlane && views.ViewCamera.pixelRect.Contains(point);
    }

    private readonly Vector3[] screenCorners = new Vector3[4];
    /// <summary>조준점에서 이 화면(자리에서 보이는 네모)까지의 거리. 화면 안을 가리키면 0.</summary>
    private float DistanceToScreen(RectTransform screen, Vector2 aim)
    {
        screen.GetWorldCorners(screenCorners);
        float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
        foreach (var corner in screenCorners)
        {
            Vector3 point = views.ViewCamera.WorldToScreenPoint(corner);
            if (point.z <= 0f) { return float.MaxValue; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, point, null, out Vector2 local);
            left = Mathf.Min(left, local.x); right = Mathf.Max(right, local.x); bottom = Mathf.Min(bottom, local.y); top = Mathf.Max(top, local.y);
        }
        float dx = Mathf.Max(0f, Mathf.Max(left - aim.x, aim.x - right)), dy = Mathf.Max(0f, Mathf.Max(bottom - aim.y, aim.y - top));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
    /// <summary>거리를 가까운 정도(0~1)로 바꾼다. range에서 보이기 시작해 그 절반 거리에서 다 진해진다.</summary>
    private static float Closeness(float distance, float range)
    { return Mathf.Clamp01(Mathf.InverseLerp(range, range * .5f, distance)); }

    private bool ViewingOrderForm()
    {
        if (PhoneCallController.IsRaised || views.Target < DeskViewTarget.TabletOne || tabletNavigation == null)
        { return false; }
        bool second = views.Target == DeskViewTarget.TabletTwo;
        foreach (var tablet in tabletNavigation)
        {
            if (tablet != null && tablet.second == second && !tablet.Locked && tablet.CurrentPage == TabletAccountUI.Page.Orders)
            { return true; }
        }
        return false;
    }

    private void RefreshLabels(bool phone, bool paused)
    {
        if (phone)
        {
            Arrange(3, true);
            Hint(0, DeskPhone.Raised ? "Esc" : "Tab", labels.close); Hint(1, "Q / E", labels.tablets); Hint(2, PauseKey(), paused ? labels.resume : labels.pause);
            return;
        }
        bool focused = views.IsFocused;
        if (focused)
        {
            // 모니터를 확대해 보는 동안에는 스페이스바로 가진 종목만 차례로 고른다.
            bool monitor = DeskViewController.MonitorZoomed;
            Arrange(monitor ? 5 : 4, true);
            Hint(0, GameKeyBindings.Interact.ToString(), labels.back); Hint(1, views.Target >= DeskViewTarget.TabletOne ? "Q / E" : "1 / 2 / 3", views.Target >= DeskViewTarget.TabletOne ? labels.accounts : labels.screens);
            Hint(2, "↑ / ↓", labels.stocks);
            if (PhoneCallController.Available) { Hint(3, "Tab", labels.phone); } else { cells[3].gameObject.SetActive(false); }
            if (monitor) { string code = GameLocale.Code; Hint(4, "Space", code == "ja" ? "保有銘柄" : code == "en" ? "Holdings" : "보유 종목", true); }
            return;
        }
        if (PhoneCallController.Available)
        {
            Arrange(6, false);
            Hint(0, "1 / 2 / 3", labels.screens); Hint(1, "Click", labels.interact);
            Hint(2, "Q / E", labels.tablets); Hint(3, "Tab", labels.phone); Hint(4, "↑ / ↓", labels.stocks);
            Hint(5, PauseKey(), paused ? labels.resume : labels.pause);
            return;
        }
        Arrange(5, false);
        Hint(0, "1 / 2 / 3", labels.screens); Hint(1, "Click", labels.interact);
        Hint(2, "Q / E", labels.tablets); Hint(3, "↑ / ↓", labels.stocks);
        Hint(4, PauseKey(), paused ? labels.resume : labels.pause);
    }

    private void Arrange(int count, bool singleRow)
    {
        hints.anchoredPosition = new Vector2(18f, singleRow ? 8f : 16f);
        hints.sizeDelta = new Vector2(singleRow ? count * 200f : 600f, singleRow ? 32f : 70f);
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].gameObject.SetActive(i < count);
            cells[i].anchoredPosition = singleRow ? new Vector2(i * 200f, 0f) : new Vector2(i % 3 * 200f, (1 - i / 3) * 36f);
        }
    }

    private string PauseKey()
    {
        KeyCode key = mouseLook == null || mouseLook.useSavedPauseKey ? GameKeyBindings.Pause : mouseLook.pauseKey;
        return key == KeyCode.Space || key == KeyCode.Tab ? "P" : key.ToString();
    }

    private void Hint(int index, string key, string caption, bool literal = false)
    {
        cells[index].gameObject.SetActive(true);
        if (!GameLocalizedText.Matches(keys[index],key))
        { GameLocalizedText.Set(keys[index],key); }
        if (!GameLocalizedText.Matches(captions[index],caption))
        { GameLocalizedText.Set(captions[index],caption,literal); }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    private TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, Color tint)
    {
        var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = sdfFont; text.fontSharedMaterial = sdfFont.material; text.fontSize = fontSize; text.color = tint;
        text.enableAutoSizing = false; text.richText = false; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
}
