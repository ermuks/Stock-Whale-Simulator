using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기본 계좌 태블릿의 해금 페이지. 표시만 갱신하며, 상태는 '해금하기' 버튼에서만 바뀐다.</summary>
public sealed class UnlockGridUI : MonoBehaviour
{
    private const int Columns = 3, MinimumRows = 4;
    private const float GridWidth = .60f, DetailX = .64f, DetailWidth = .36f, Gap = .02f;

    private sealed class Cell
    {
        public PlayerProgression.UnlockItem item;
        public Button button;
        public TextMeshProUGUI title, cost;
        public Image state, selected;
    }

    private readonly List<Cell> cells = new();
    private RectTransform grid;
    private TextMeshProUGUI title, description, cost, duration, remainingCaption, remaining, status;
    private Image statusIcon;
    private Button start;
    private PlayerProgression bound;
    private string selectedId;
    private float next;

    public static UnlockGridUI Install(TabletAccountUI tablet)
    {
        if (tablet == null || tablet.second)
        { return null; }

        var image = TabletKitStyle.Surface("Unlocks", tablet.contentRoot.transform, .02f, .18f, .96f, .80f, TabletKitStyle.Background);
        image.name = "Panel :: Unlocks";
        // 다른 페이지를 열 때 기존 화면들과 함께 닫히도록 같은 목록에 넣는다.
        var screens = new List<GameObject>(tablet.screens) { image.gameObject };
        tablet.screens = screens.ToArray();
        var view = image.gameObject.AddComponent<UnlockGridUI>();
        view.Build();
        image.gameObject.SetActive(false);
        return view;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        next = 0f;
        Refresh();
    }

    private void Build()
    {
        grid = TabletKitStyle.Area("Unlock Grid", transform, 0, 0, GridWidth, 1);
        TabletKitStyle.Surface("Unlock Divider", transform, GridWidth + .02f, 0, .0015f, 1, TabletKitStyle.Line);

        title = TabletKitStyle.Label("Unlock Title", transform, "", DetailX, 0, DetailWidth, .075f, 36);
        description = TabletKitStyle.Label("Unlock Description", transform, "", DetailX, .09f, DetailWidth, .36f, 25);
        description.color = TabletKitStyle.Muted;
        description.alignment = TextAlignmentOptions.TopLeft;
        description.textWrappingMode = TextWrappingModes.Normal;
        TabletKitStyle.Surface("Unlock Detail Divider", transform, DetailX, .47f, DetailWidth, .0015f, TabletKitStyle.Line);

        TabletKitStyle.Label("Unlock Cost Caption", transform, "필요 금액", DetailX, .50f, .13f, .06f, 27).color = TabletKitStyle.Muted;
        cost = TabletKitStyle.Label("Unlock Cost", transform, "—", DetailX + .13f, .50f, DetailWidth - .13f, .06f, 30, true);
        TabletKitStyle.TextStyle(cost, 30, true);
        cost.alignment = TextAlignmentOptions.MidlineRight;
        TabletKitStyle.Label("Unlock Duration Caption", transform, "소요 시간", DetailX, .57f, .13f, .06f, 27).color = TabletKitStyle.Muted;
        duration = TabletKitStyle.Label("Unlock Duration", transform, "—", DetailX + .13f, .57f, DetailWidth - .13f, .06f, 28, true);
        remainingCaption = TabletKitStyle.Label("Unlock Remaining Caption", transform, "남은 시간", DetailX, .64f, .13f, .06f, 27);
        remainingCaption.color = TabletKitStyle.Muted;
        remaining = TabletKitStyle.Label("Unlock Remaining", transform, "", DetailX + .13f, .64f, DetailWidth - .13f, .06f, 28, true);
        remaining.color = TabletKitStyle.Accent;

        statusIcon = TabletKitStyle.Glyph("lock", transform, DetailX, .775f, .03f, .05f);
        statusIcon.name = "Image :: Unlock Status";
        status = TabletKitStyle.Label("Unlock Status", transform, "", DetailX + .04f, .77f, DetailWidth - .04f, .06f, 26);
        status.color = TabletKitStyle.Muted;
        start = TabletKitStyle.Action("Start Unlock", transform, "해금하기", DetailX, .86f, DetailWidth, .09f, StartSelected, true);
    }

    private void BuildCells(PlayerProgression progression)
    {
        var items = progression.UnlockItems;
        if (items.Count == 0)
        { return; }

        int rows = Mathf.Max(MinimumRows, (items.Count + Columns - 1) / Columns);
        float width = (1f - Gap * (Columns - 1)) / Columns, height = (1f - Gap * (rows - 1)) / rows;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            float x = i % Columns * (width + Gap), y = i / Columns * (height + Gap);
            var button = TabletKitStyle.Action("Unlock " + i, grid, item.title, x, y, width, height, () => Select(item.id));
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            TabletKitStyle.Place(label.rectTransform, .07f, .40f, .86f, .34f);
            label.fontSize = 26;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            var cell = new Cell { item = item, button = button, title = label };
            cell.state = TabletKitStyle.Glyph("lock", button.transform, .07f, .12f, .16f, .24f);
            cell.state.name = "Image :: Unlock State";
            cell.cost = TabletKitStyle.Label("Unlock Cell Cost", button.transform, "", .07f, .76f, .86f, .18f, 24, true);
            TabletKitStyle.TextStyle(cell.cost, 24, true);
            cell.cost.alignment = TextAlignmentOptions.MidlineRight;
            cell.cost.color = TabletKitStyle.Muted;
            cell.selected = TabletKitStyle.Surface("Unlock Selected", button.transform, 0, .97f, 1, .03f, TabletKitStyle.Accent);
            cells.Add(cell);
        }

        if (string.IsNullOrEmpty(selectedId))
        {
            selectedId = progression.FindUnlock(progression.PendingUnlock) == null ? items[0].id : progression.PendingUnlock;
        }
    }

    private void Select(string id)
    {
        selectedId = id;
        Refresh();
    }

    private void StartSelected()
    {
        var progression = PlayerProgression.Instance;
        if (progression == null || string.IsNullOrEmpty(selectedId))
        { return; }

        progression.TryStartUnlock(selectedId);
        Refresh();
    }

    private void Bind(PlayerProgression progression)
    {
        if (bound == progression)
        { return; }

        if (bound != null)
        {
            bound.UnlocksChanged -= Refresh;
        }

        bound = progression;
        if (bound != null)
        {
            bound.UnlocksChanged += Refresh;
        }
    }

    void OnEnable()
    {
        next = 0f;
        Bind(PlayerProgression.Instance);
    }

    void OnDisable()
    {
        Bind(null);
    }

    void Update()
    {
        if (Time.unscaledTime < next)
        { return; }

        next = Time.unscaledTime + .5f;
        Refresh();
    }

    /// <summary>읽기 전용 갱신. 계좌·해금 상태를 바꾸지 않는다.</summary>
    public void Refresh()
    {
        var progression = PlayerProgression.Instance;
        if (isActiveAndEnabled)
        {
            Bind(progression);
        }

        if (progression != null && cells.Count == 0)
        {
            BuildCells(progression);
        }

        PlayerProgression.UnlockItem shown = null;
        foreach (var cell in cells)
        {
            bool active = cell.item.id == selectedId;
            if (active)
            {
                shown = cell.item;
            }

            var state = progression == null ? PlayerProgression.UnlockState.Locked : progression.StateOf(cell.item);
            SetIcon(cell.state, StateIcon(state), StateColor(state));
            TabletKitStyle.Flat(cell.button.image, active ? TabletKitStyle.Hover : TabletKitStyle.Panel);
            cell.title.color = active ? TabletKitStyle.Accent : state == PlayerProgression.UnlockState.Locked ? TabletKitStyle.Muted : TabletKitStyle.Ink;
            if (cell.selected.gameObject.activeSelf != active)
            {
                cell.selected.gameObject.SetActive(active);
            }

            DeviceTerminalUI.Set(cell.title, cell.item.title);
            DeviceTerminalUI.Set(cell.cost, state == PlayerProgression.UnlockState.Unlocked ? "" : DeviceTerminalUI.Money(cell.item.cost));
        }

        if (progression == null || shown == null)
        {
            DeviceTerminalUI.Set(title, progression == null ? "준비 중" : "항목 없음");
            DeviceTerminalUI.Set(description, "");
            DeviceTerminalUI.Set(cost, "—");
            DeviceTerminalUI.Set(duration, "—");
            ShowRemaining(false, 0);
            statusIcon.gameObject.SetActive(false);
            DeviceTerminalUI.Set(status, "");
            start.gameObject.SetActive(true);
            start.interactable = false;
            TabletControlStyle.Emphasize(start, false);
            return;
        }

        var selectedState = progression.StateOf(shown);
        bool can = progression.CanStartUnlock(shown, out string reason);
        DeviceTerminalUI.Set(title, shown.title);
        DeviceTerminalUI.Set(description, shown.description);
        DeviceTerminalUI.Set(cost, DeviceTerminalUI.Money(shown.cost));
        DeviceTerminalUI.Set(duration, GameTime(shown.minutes));
        ShowRemaining(selectedState == PlayerProgression.UnlockState.InProgress, progression.UnlockRemainingMinutes);

        bool done = selectedState == PlayerProgression.UnlockState.Unlocked;
        bool idle = selectedState == PlayerProgression.UnlockState.Available;
        statusIcon.gameObject.SetActive(!can);
        SetIcon(statusIcon, idle ? "info" : StateIcon(selectedState), done ? TabletKitStyle.Accent : TabletKitStyle.Muted);
        DeviceTerminalUI.Set(status, reason);
        status.color = done ? TabletKitStyle.Accent : TabletKitStyle.Muted;
        if (start.gameObject.activeSelf == done)
        {
            start.gameObject.SetActive(!done);
        }

        start.interactable = can;
        TabletControlStyle.Emphasize(start, can);
    }

    private void ShowRemaining(bool visible, double minutes)
    {
        if (remainingCaption.gameObject.activeSelf != visible)
        {
            remainingCaption.gameObject.SetActive(visible);
            remaining.gameObject.SetActive(visible);
        }

        if (visible)
        {
            DeviceTerminalUI.Set(remaining, GameTime(minutes));
        }
    }

    private static void SetIcon(Image image, string icon, Color color)
    {
        bool visible = icon != null;
        if (image.enabled != visible)
        {
            image.enabled = visible;
        }

        if (!visible)
        { return; }

        var sprite = TabletKitStyle.Icon(icon);
        if (image.sprite != sprite)
        {
            image.sprite = sprite;
        }

        image.color = color;
    }

    private static string StateIcon(PlayerProgression.UnlockState state)
    {
        if (state == PlayerProgression.UnlockState.Locked)
        { return "lock"; }

        if (state == PlayerProgression.UnlockState.InProgress)
        { return "timer"; }

        if (state == PlayerProgression.UnlockState.Unlocked)
        { return "check"; }

        return null;
    }

    private static Color StateColor(PlayerProgression.UnlockState state)
    {
        return state == PlayerProgression.UnlockState.Locked ? TabletKitStyle.Muted : TabletKitStyle.Ink;
    }

    /// <summary>게임 분을 "게임 2시간 30분" 형태로 표시한다. 현실 시간으로 환산하지 않는다.</summary>
    private static string GameTime(double gameMinutes)
    {
        long total = (long)Math.Ceiling(Math.Max(0, gameMinutes));
        long hours = total / 60, minutes = total % 60;
        if (hours > 0 && minutes > 0)
        { return "게임 " + hours + "시간 " + minutes + "분"; }

        if (hours > 0)
        { return "게임 " + hours + "시간"; }

        return "게임 " + minutes + "분";
    }
}
