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
        public Image[] border;
        public Image track, fill;
    }
    // 해금한 칸은 초록 테두리로, 잠긴 칸은 채도를 뺀 회색으로 보여 준다.
    private static readonly Color Done = new Color32(61, 220, 132, 255), DoneFill = new Color32(17, 40, 33, 255), DoneHover = new Color32(22, 54, 44, 255),
        LockedFill = new Color32(27, 28, 30, 255), LockedHover = new Color32(40, 41, 44, 255), LockedInk = new Color32(108, 110, 114, 255);

    private readonly List<Cell> cells = new();
    private RectTransform grid;
    private TextMeshProUGUI title, description, cost, duration, remainingCaption, remaining, status;
    private Image statusIcon, detailTrack, detailFill;
    private MiniPreview preview;
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
        // 고른 항목은 글로 길게 설명하지 않는다. 쓰면 차트에 무슨 일이 생기는지 움직이는 그림으로 보여 주고, 그 아래에 한 줄만 적는다.
        var frame = TabletKitStyle.Surface("Unlock Preview", transform, DetailX, .09f, DetailWidth, .28f, TabletKitStyle.Panel);
        preview = MiniPreview.Create("Unlock Preview Picture", frame.transform, title.font, 20f);
        description = TabletKitStyle.Label("Unlock Description", transform, "", DetailX, .385f, DetailWidth, .075f, 22);
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
        // 해금이 얼마나 진행됐는지 보여 주는 막대.
        detailTrack = TabletKitStyle.Surface("Unlock Progress Track", transform, DetailX, .715f, DetailWidth, .014f, TabletKitStyle.Line);
        detailFill = TabletKitStyle.Surface("Unlock Progress Fill", detailTrack.transform, 0, 0, 1, 1, TabletKitStyle.Accent);

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
            cell.border = new[]
            {
                TabletKitStyle.Surface("Unlock Border Top", button.transform, 0, 0, 1, .018f, Done), TabletKitStyle.Surface("Unlock Border Bottom", button.transform, 0, .982f, 1, .018f, Done),
                TabletKitStyle.Surface("Unlock Border Left", button.transform, 0, 0, .011f, 1, Done), TabletKitStyle.Surface("Unlock Border Right", button.transform, .989f, 0, .011f, 1, Done)
            };
            foreach (var edge in cell.border) { edge.raycastTarget = false; }
            cell.track = TabletKitStyle.Surface("Unlock Cell Track", button.transform, .07f, .70f, .86f, .035f, TabletKitStyle.Line);
            cell.fill = TabletKitStyle.Surface("Unlock Cell Fill", cell.track.transform, 0, 0, 1, 1, TabletKitStyle.Accent);
            cell.track.raycastTarget = cell.fill.raycastTarget = false;
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
            bool unlocked = state == PlayerProgression.UnlockState.Unlocked, locked = state == PlayerProgression.UnlockState.Locked;
            SetIcon(cell.state, StateIcon(state), unlocked ? Done : locked ? LockedInk : TabletKitStyle.Accent);
            TabletKitStyle.Flat(cell.button.image, unlocked ? (active ? DoneHover : DoneFill) : locked ? (active ? LockedHover : LockedFill) : active ? TabletKitStyle.Hover : TabletKitStyle.Panel);
            cell.title.color = unlocked ? TabletKitStyle.Ink : locked ? LockedInk : active ? TabletKitStyle.Accent : TabletKitStyle.Ink;
            bool buyable = state == PlayerProgression.UnlockState.Available && progression != null && progression.CanStartUnlock(cell.item, out _);
            cell.cost.color = locked ? LockedInk : buyable ? Done : TabletKitStyle.Muted;
            foreach (var edge in cell.border) { if (edge.enabled != unlocked) { edge.enabled = unlocked; } }
            // 고른 칸의 밑줄: 해금한 칸은 테두리가 대신한다(고르면 더 밝은 바탕).
            bool underline = active && !unlocked;
            if (cell.selected.gameObject.activeSelf != underline)
            {
                cell.selected.gameObject.SetActive(underline);
            }
            cell.selected.color = locked ? LockedInk : TabletKitStyle.Accent;

            // 해금 중인 칸: 진행 막대와 남은 시간.
            bool running = state == PlayerProgression.UnlockState.InProgress;
            if (cell.track.gameObject.activeSelf != running) { cell.track.gameObject.SetActive(running); }
            if (running)
            {
                cell.fill.rectTransform.anchorMax = new Vector2(Progress(progression, cell.item), 1f);
                GameLocalizedText.Set(cell.cost, LocalTime(progression.UnlockRemainingMinutes), true); cell.cost.color = TabletKitStyle.Accent;
            }
            else if (cell.item.Written)
            {
                GameLocalizedText.Set(cell.title, cell.item.LocalTitle, true);
                GameLocalizedText.Set(cell.cost, state == PlayerProgression.UnlockState.Unlocked ? "" : cell.item.cost > 0 ? DeviceTerminalUI.Money(cell.item.cost) : Condition, true);
            }
            else
            {
                DeviceTerminalUI.Set(cell.title, cell.item.title);
                DeviceTerminalUI.Set(cell.cost, state == PlayerProgression.UnlockState.Unlocked ? "" : DeviceTerminalUI.Money(cell.item.cost));
            }
        }

        if (progression == null || shown == null)
        {
            DeviceTerminalUI.Set(title, progression == null ? "준비 중" : "항목 없음");
            DeviceTerminalUI.Set(description, "");
            DeviceTerminalUI.Set(cost, "—");
            DeviceTerminalUI.Set(duration, "—");
            ShowRemaining(false, 0, 0f);
            statusIcon.gameObject.SetActive(false);
            DeviceTerminalUI.Set(status, "");
            start.gameObject.SetActive(true);
            start.interactable = false;
            TabletCall.Set(start, false);
            return;
        }

        var selectedState = progression.StateOf(shown);
        bool can = progression.CanStartUnlock(shown, out string reason);
        if (shown.Written)
        {
            GameLocalizedText.Set(title, shown.LocalTitle, true); GameLocalizedText.Set(description, shown.LocalSummary, true);
            preview.Show(shown.Preview);
            GameLocalizedText.Set(cost, shown.cost > 0 ? DeviceTerminalUI.Money(shown.cost) : Condition, true);
            if (shown.external) { GameLocalizedText.Set(duration, "—", true); } else { DeviceTerminalUI.Set(duration, GameTime(shown.minutes)); }
        }
        else
        {
            DeviceTerminalUI.Set(title, shown.title);
            DeviceTerminalUI.Set(description, shown.description);
            DeviceTerminalUI.Set(cost, DeviceTerminalUI.Money(shown.cost));
            DeviceTerminalUI.Set(duration, GameTime(shown.minutes));
        }
        bool inProgress = selectedState == PlayerProgression.UnlockState.InProgress; float done01 = inProgress ? Progress(progression, shown) : 0f;
        ShowRemaining(inProgress, progression.UnlockRemainingMinutes, done01);

        bool done = selectedState == PlayerProgression.UnlockState.Unlocked;
        bool idle = selectedState == PlayerProgression.UnlockState.Available;
        statusIcon.gameObject.SetActive(!can);
        SetIcon(statusIcon, idle ? "info" : StateIcon(selectedState), done ? Done : TabletKitStyle.Muted);
        if (shown.external) { GameLocalizedText.Set(status, reason, true); } else { DeviceTerminalUI.Set(status, reason); }
        status.color = done ? Done : TabletKitStyle.Muted;
        if (start.gameObject.activeSelf == done)
        {
            start.gameObject.SetActive(!done);
        }

        // 조건만으로 열리는 해금(작전 도구)은 누를 단추가 없다.
        bool pressable = !done && !(shown.external && shown.cost <= 0);
        if (start.gameObject.activeSelf != pressable) { start.gameObject.SetActive(pressable); }
        start.interactable = can;
        TabletCall.Set(start, can);
    }
    private static string Condition => GameLocale.Code == "ja" ? "条件達成" : GameLocale.Code == "en" ? "By condition" : "조건 달성";

    /// <summary>남은 게임 시간을 지금 언어로 적는다.</summary>
    private static string LocalTime(double gameMinutes)
    {
        long total = (long)Math.Ceiling(Math.Max(0, gameMinutes)), hours = total / 60, minutes = total % 60; string code = GameLocale.Code;
        if (code == "en") { return (hours > 0 ? hours + "h " : "") + (minutes > 0 || hours == 0 ? minutes + "m " : "") + "in-game"; }
        if (code == "ja") { return "ゲーム内 " + (hours > 0 ? hours + "時間" : "") + (minutes > 0 || hours == 0 ? minutes + "分" : ""); }
        return "게임 " + (hours > 0 ? hours + "시간" + (minutes > 0 ? " " : "") : "") + (minutes > 0 || hours == 0 ? minutes + "분" : "");
    }

    /// <summary>해금이 진행된 정도(0~1).</summary>
    private static float Progress(PlayerProgression progression, PlayerProgression.UnlockItem item)
    { return Mathf.Clamp01(1f - (float)(progression.UnlockRemainingMinutes / Math.Max(1, item.minutes))); }

    private void ShowRemaining(bool visible, double minutes, float progress)
    {
        if (detailTrack.gameObject.activeSelf != visible) { detailTrack.gameObject.SetActive(visible); }
        if (visible) { detailFill.rectTransform.anchorMax = new Vector2(progress, 1f); }
        if (remainingCaption.gameObject.activeSelf != visible)
        {
            remainingCaption.gameObject.SetActive(visible);
            remaining.gameObject.SetActive(visible);
        }

        if (visible)
        {
            GameLocalizedText.Set(remaining, LocalTime(minutes) + " · " + Mathf.FloorToInt(progress * 100f) + "%", true);
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
