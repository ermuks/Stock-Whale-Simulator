using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 침대에서 자고 일어나기.
// 장이 끝난 뒤 침대를 바라보고 F를 누르면 베개로 달려들어 눕고, 천장을 보며 눈을 감는다.
// 잠깐 뒤 눈을 뜨면 아침이다. 몸을 일으켜 발치를 내려다보고, 침대 밖으로 나와 정면을 본다(약 6초). 그 뒤부터 다시 움직일 수 있다.
// 잠 자체(시간을 아침으로 넘기고 피로를 푸는 일)는 PlayerRestController가 그대로 한다. 여기는 카메라와 눈꺼풀만 움직인다.
public sealed class BedSleep : MonoBehaviour
{
    public static BedSleep Instance { get; private set; }
    public const KeyCode Key = KeyCode.F;
    public const float RiseSeconds = 6f;
    private const float DiveSeconds = 1.5f, RollSeconds = 1.1f, DarkSeconds = 1.1f, OpenSeconds = 1.3f, Reach = 9f;
    private static readonly Quaternion Lying = Quaternion.Euler(-86f, 180f, 0f);
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    /// <summary>0 평소, 1 베개로 달려듦, 2 돌아누우며 눈 감음, 3 잠든 어둠, 4 눈 뜸, 5 일어남.</summary>
    public int Stage { get; private set; }
    private float clock;
    private Pose from;
    private GameObject hintRoot;
    private TextMeshProUGUI hint;

    void Awake()
    {
        Instance = this;
        hintRoot = new GameObject("Area :: Bed Hint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        hintRoot.transform.SetParent(transform, false);
        var canvas = hintRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 28840;
        var scaler = hintRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        var pill = new GameObject("Panel :: Bed Hint", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); pill.SetParent(hintRoot.transform, false);
        pill.anchorMin = pill.anchorMax = new Vector2(.5f, 0f); pill.pivot = new Vector2(.5f, 0f); pill.anchoredPosition = new Vector2(0f, 112f); pill.sizeDelta = new Vector2(470f, 54f);
        pill.GetComponent<Image>().color = new Color(.04f, .055f, .07f, .92f); pill.GetComponent<Image>().raycastTarget = false;
        var rect = new GameObject("Text :: Bed Hint", typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(pill, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        hint = rect.gameObject.AddComponent<TextMeshProUGUI>(); hint.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        hint.fontSize = 24f; hint.enableAutoSizing = false; hint.color = new Color(.90f, .92f, .94f, 1f); hint.alignment = TextAlignmentOptions.Center;
        hint.textWrappingMode = TextWrappingModes.NoWrap; hint.overflowMode = TextOverflowModes.Overflow; hint.raycastTarget = false;
        hintRoot.SetActive(false);
    }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    void Update()
    {
        var shell = RoomShell.Instance; var views = DeskViewController.Instance;
        if (Stage != 0 || shell == null || !shell.Built || shell.Bed == null || views == null || !views.Ready) { if (hintRoot.activeSelf) { hintRoot.SetActive(false); } return; }
        bool free = !PauseMenuController.BlocksInput && !MouseCameraController.TextEntryActive && Time.timeScale > 0f && MouseCameraController.IsCursorLocked
            && !DeskViewController.OwnsView && !DoorNotes.Holding && !DoorNotes.Aiming && !RoomShell.Cinematic;
        bool aimed = false;
        if (free)
        {
            Ray ray = views.ViewCamera.ScreenPointToRay(MouseCameraController.GetAimScreenPosition());
            aimed = shell.BedBounds.IntersectRay(ray, out float distance) && distance < Reach;
        }
        if (hintRoot.activeSelf != aimed) { hintRoot.SetActive(aimed); }
        if (!aimed) { return; }
        var rest = PlayerRestController.Instance; var market = MarketDayController.Instance;
        bool can = rest != null && market != null && rest.CanSleep && !market.ReportPending;
        GameLocalizedText.Set(hint, can ? Key + "   " + Word("잠자기", "Sleep", "眠る") : Word("장이 끝나야 잘 수 있습니다", "You can sleep after the close", "場が引けてから眠れます"), true);
        if (can && Input.GetKeyDown(Key)) { LieDown(); }
    }

    /// <summary>지금 자리에서 베개로 달려들어 눕는다.</summary>
    public void LieDown()
    {
        var views = DeskViewController.Instance;
        if (Stage != 0 || views == null) { return; }
        from = new Pose(views.transform.position, views.transform.rotation);
        RoomShell.Cinematic = true; Stage = 1; clock = 0f; hintRoot.SetActive(false);
    }
    /// <summary>침대에 누워 눈을 감은 채로 시작해, 눈을 뜨고 일어난다(엔딩 뒤에 꿈에서 깰 때).</summary>
    public void WakeUp()
    {
        RoomShell.Cinematic = true; Stage = 3; clock = 0f; hintRoot.SetActive(false);
        SleepPrompt.Instance?.Lids(1f);
    }

    /// <summary>일어나는 6초의 카메라 자리. 0~2.3초: 몸을 일으키며 천장에서 발치 아래로 시선이 내려간다.
    /// 2.3~4.5초: 다리를 내려 침대 옆에 선다. 4.5~6초: 고개를 들어 정면을 본다.</summary>
    public static Pose RisePose(RoomShell shell, float seconds)
    {
        Vector3 lie = shell.PillowEye, sit = lie + new Vector3(0f, 1.55f, -1.9f), stand = shell.BedsideStand;
        if (seconds < 2.3f)
        {
            float s = Ease(seconds / 2.3f);
            return new Pose(Vector3.Lerp(lie, sit, s), Quaternion.Euler(Mathf.Lerp(-86f, 38f, s), 180f, 0f));
        }
        if (seconds < 4.5f)
        {
            float s = Ease((seconds - 2.3f) / 2.2f);
            // 일어서는 동안 머리가 살짝 앞으로 숙여졌다가 올라온다.
            Vector3 position = Vector3.Lerp(sit, stand, s) + Vector3.down * Mathf.Sin(s * Mathf.PI) * .25f;
            return new Pose(position, Quaternion.Slerp(Quaternion.Euler(38f, 180f, 0f), Quaternion.Euler(24f, 258f, 0f), s));
        }
        float last = Ease((seconds - 4.5f) / (RiseSeconds - 4.5f));
        // 마지막에는 책상(모니터) 쪽, 곧 정면을 본다.
        return new Pose(stand, Quaternion.Slerp(Quaternion.Euler(24f, 258f, 0f), Quaternion.Euler(0f, 331f, 0f), last));
    }

    // 다른 스크립트가 카메라를 건드린 뒤에 덮어쓴다.
    void LateUpdate()
    {
        if (Stage == 0) { return; }
        var shell = RoomShell.Instance; var views = DeskViewController.Instance; var lids = SleepPrompt.Instance;
        if (shell == null || views == null) { Stage = 0; RoomShell.Cinematic = false; return; }
        clock += Time.unscaledDeltaTime; Transform eye = views.transform;
        if (Stage == 1)
        {
            // 달려든다: 처음엔 느리고 끝으로 갈수록 빨라진다. 시선은 베개를 향한다.
            float t = Mathf.Clamp01(clock / DiveSeconds), rush = t * t;
            Vector3 above = shell.PillowEye + Vector3.up * .85f, position = Vector3.Lerp(from.position, above, rush);
            Quaternion toward = Quaternion.LookRotation((shell.PillowEye + Vector3.down * .9f) - position, Vector3.up);
            eye.SetPositionAndRotation(position, Quaternion.Slerp(from.rotation, toward, Ease(t * 1.7f)));
            if (t >= 1f) { from = new Pose(eye.position, eye.rotation); Stage = 2; clock = 0f; Sfx.Play("BedDown", .9f); }
        }
        else if (Stage == 2)
        {
            // 베개에 닿으면 돌아누워 천장을 본다. 그러면서 눈이 감긴다.
            float t = Mathf.Clamp01(clock / RollSeconds), turn = Ease(t);
            eye.SetPositionAndRotation(Vector3.Lerp(from.position, shell.PillowEye, turn), Quaternion.Slerp(from.rotation, Lying, turn));
            lids?.Lids(Ease((t - .3f) / .7f));
            if (t >= 1f)
            {
                PlayerRestController.Instance?.SleepUntilMorning();
                Stage = 3; clock = 0f;
            }
        }
        else if (Stage == 3)
        {
            eye.SetPositionAndRotation(shell.PillowEye, Lying); lids?.Lids(1f);
            if (clock >= DarkSeconds) { Stage = 4; clock = 0f; }
        }
        else if (Stage == 4)
        {
            // 아침. 눈을 뜬다(한 번 다시 감겼다가 뜬다).
            float t = Mathf.Clamp01(clock / OpenSeconds);
            float closed = t < .35f ? 1f - Ease(t / .35f) * .55f : t < .5f ? .45f + Ease((t - .35f) / .15f) * .35f : .8f * (1f - Ease((t - .5f) / .5f));
            eye.SetPositionAndRotation(shell.PillowEye, Lying); lids?.Lids(closed);
            if (t >= 1f) { lids?.Lids(0f); Stage = 5; clock = 0f; Sfx.Play("BedUp", .9f); }
        }
        else
        {
            var pose = RisePose(shell, Mathf.Min(clock, RiseSeconds));
            eye.SetPositionAndRotation(pose.position, pose.rotation);
            if (clock >= RiseSeconds)
            {
                Stage = 0; RoomShell.Cinematic = false;
                var look = eye.GetComponent<MouseCameraController>() ?? FindAnyObjectByType<MouseCameraController>();
                if (look != null) { look.SyncViewAngles(); }
            }
        }
    }
}
