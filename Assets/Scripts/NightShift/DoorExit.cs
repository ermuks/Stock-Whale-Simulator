using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 방문. 평소에는 잠겨 있어서 F를 눌러도 덜컹거리기만 한다.
// 최종 목표(내 기업 상장 + 사업 확장 + 마감 시가총액)를 이루면 잠금이 풀린다. 문을 보고 F를 누르면 문을 열고 빛 속으로 걸어 나간다. 그것이 엔딩이다.
// 엔딩 글자가 지나가면 침대에서 눈을 뜬다. 계좌에는 백만 돈도 남아 있지 않다. 전부 꿈이었다.
public sealed class DoorExit : MonoBehaviour
{
    public static DoorExit Instance { get; private set; }
    public const KeyCode Key = KeyCode.F;
    private const float OpenSeconds = 2.0f, WalkSeconds = 2.8f, DarkenSeconds = 1.3f, OpenAngle = 102f, Reach = 7f;
    /// <summary>꿈에서 깼을 때 계좌에 남아 있는 돈.</summary>
    public const decimal PocketMoney = 873400m;
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    /// <summary>0 평소, 1 문이 열림, 2 걸어 나감, 3 하얀 빛이 어두워짐, 4 엔딩 글자.</summary>
    public int Stage { get; private set; }
    private float clock, rattle, noticeUntil;
    private bool wasUnlocked;
    private Pose from;
    private GameObject hintRoot, fadeRoot;
    private TextMeshProUGUI hint;
    private Image fade;

    private static CompanyGrowth Growth => CorporateEconomy.Instance != null ? CorporateEconomy.Instance.PlayerCompany?.business?.growth : null;
    /// <summary>최종 목표를 이뤘고 아직 나가지 않았다.</summary>
    public static bool Unlocked { get { var growth = Growth; return growth != null && growth.EndingDay >= 1 && !growth.EndingShown; } }

    void Awake()
    {
        Instance = this;
        hintRoot = Canvas("Area :: Door Hint", 28845);
        var pill = new GameObject("Panel :: Door Hint", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); pill.SetParent(hintRoot.transform, false);
        pill.anchorMin = pill.anchorMax = new Vector2(.5f, 0f); pill.pivot = new Vector2(.5f, 0f); pill.anchoredPosition = new Vector2(0f, 112f); pill.sizeDelta = new Vector2(470f, 54f);
        pill.GetComponent<Image>().color = new Color(.04f, .055f, .07f, .92f); pill.GetComponent<Image>().raycastTarget = false;
        var rect = new GameObject("Text :: Door Hint", typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(pill, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        hint = rect.gameObject.AddComponent<TextMeshProUGUI>(); hint.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        hint.fontSize = 24f; hint.enableAutoSizing = false; hint.color = new Color(.90f, .92f, .94f, 1f); hint.alignment = TextAlignmentOptions.Center;
        hint.textWrappingMode = TextWrappingModes.NoWrap; hint.overflowMode = TextOverflowModes.Overflow; hint.raycastTarget = false;
        // 화면을 하얗게, 그다음 까맣게 덮는 판. 눈꺼풀보다 위, 엔딩 글자보다 아래.
        fadeRoot = Canvas("Area :: Exit Fade", 29050);
        var cover = new GameObject("Image :: Exit Fade", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); cover.SetParent(fadeRoot.transform, false);
        cover.anchorMin = Vector2.zero; cover.anchorMax = Vector2.one; cover.offsetMin = cover.offsetMax = Vector2.zero;
        fade = cover.GetComponent<Image>(); fade.raycastTarget = false; fade.color = Color.clear;
        hintRoot.SetActive(false); fadeRoot.SetActive(false);
    }
    private GameObject Canvas(string name, int order)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        return root;
    }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    void Update()
    {
        var shell = RoomShell.Instance; var views = DeskViewController.Instance;
        if (Stage != 0 || shell == null || !shell.Built || shell.DoorLeaf == null || views == null || !views.Ready) { if (hintRoot.activeSelf) { hintRoot.SetActive(false); } return; }
        bool unlocked = Unlocked;
        if (unlocked && !wasUnlocked) { noticeUntil = Time.unscaledTime + 12f; Sfx.Play("DoorUnlock", 1f, shell.DoorCenter); }
        wasUnlocked = unlocked;
        // 잠긴 문을 흔들면 문짝이 잠깐 덜컹거린다.
        if (rattle > 0f)
        {
            rattle = Mathf.Max(0f, rattle - Time.unscaledDeltaTime);
            shell.DoorLeaf.localRotation = Quaternion.Euler(0f, Mathf.Sin(rattle * 60f) * 1.1f * (rattle / .4f), 0f);
        }
        bool free = !PauseMenuController.BlocksInput && !MouseCameraController.TextEntryActive && Time.timeScale > 0f && MouseCameraController.IsCursorLocked
            && !DeskViewController.OwnsView && !DoorNotes.Holding && !DoorNotes.Aiming && !RoomShell.Cinematic;
        bool aimed = false;
        if (free)
        {
            Ray ray = views.ViewCamera.ScreenPointToRay(MouseCameraController.GetAimScreenPosition());
            aimed = shell.DoorBounds.IntersectRay(ray, out float distance) && distance < Reach;
        }
        bool notice = !aimed && unlocked && free && Time.unscaledTime < noticeUntil;
        if (hintRoot.activeSelf != (aimed || notice)) { hintRoot.SetActive(aimed || notice); }
        if (notice) { GameLocalizedText.Set(hint, Word("철컥. 등 뒤에서 문이 열렸습니다", "Click. The door behind you is unlocked", "カチャリ。後ろのドアの鍵が開いた"), true); }
        if (!aimed) { return; }
        GameLocalizedText.Set(hint, unlocked ? Key + "   " + Word("문 열고 나가기", "Open the door and leave", "ドアを開けて出る")
            : rattle > 0f ? Word("잠겨 있습니다", "It is locked", "鍵がかかっている") : Key + "   " + Word("문 열기", "Open the door", "ドアを開ける"), true);
        if (!Input.GetKeyDown(Key)) { return; }
        if (unlocked) { Leave(); } else { rattle = .4f; Sfx.Play("DoorLocked", .9f, shell.DoorCenter); }
    }

    /// <summary>문을 열고 걸어 나간다(엔딩).</summary>
    public void Leave()
    {
        var views = DeskViewController.Instance; var shell = RoomShell.Instance;
        if (Stage != 0 || views == null || shell == null || shell.DoorLeaf == null) { return; }
        from = new Pose(views.transform.position, views.transform.rotation);
        RoomShell.Cinematic = true; Stage = 1; clock = 0f; rattle = 0f; hintRoot.SetActive(false);
        shell.Outside.SetActive(true);
        Sfx.Play("DoorOpen", 1f, shell.DoorCenter);
        RoomAmbience.Instance?.PlayEnding();
    }

    /// <summary>문이 share(0~1)만큼 열린 모습으로 만든다. 바깥 빛도 그만큼 켜진다.</summary>
    public static void Swing(RoomShell shell, float share)
    {
        shell.DoorLeaf.localRotation = Quaternion.Euler(0f, OpenAngle * share, 0f);
        shell.Outside.SetActive(share > 0f); shell.OutsideLight.intensity = share * 3.2f;
    }

    void LateUpdate()
    {
        if (Stage == 0) { return; }
        var shell = RoomShell.Instance; var views = DeskViewController.Instance;
        if (shell == null || views == null) { Stage = 0; RoomShell.Cinematic = false; return; }
        clock += Time.unscaledDeltaTime; Transform eye = views.transform;
        Vector3 before = new Vector3(shell.DoorCenter.x, from.position.y, shell.DoorCenter.z + 2.7f), beyond = new Vector3(shell.DoorCenter.x, from.position.y, shell.DoorCenter.z - 2.9f);
        Quaternion facing = Quaternion.Euler(0f, 180f, 0f);
        if (Stage == 1)
        {
            // 문 앞으로 가서 서는 동안 문이 밖으로 열리고, 빛이 쏟아져 들어온다.
            float t = Mathf.Clamp01(clock / OpenSeconds);
            eye.SetPositionAndRotation(Vector3.Lerp(from.position, before, Ease(t * 1.25f)), Quaternion.Slerp(from.rotation, facing, Ease(t * 1.25f)));
            Swing(shell, Ease((t - .15f) / .85f));
            if (t >= 1f) { Stage = 2; clock = 0f; }
        }
        else if (Stage == 2)
        {
            // 빛 속으로 걸어 나간다. 화면이 하얘진다.
            float t = Mathf.Clamp01(clock / WalkSeconds);
            eye.SetPositionAndRotation(Vector3.Lerp(before, beyond, Ease(t)) + Vector3.up * Mathf.Sin(t * Mathf.PI * 5f) * .035f, facing);
            if (!fadeRoot.activeSelf) { fadeRoot.SetActive(true); }
            fade.color = new Color(1f, .98f, .94f, Ease((t - .2f) / .7f));
            if (t >= 1f) { Stage = 3; clock = 0f; }
        }
        else if (Stage == 3)
        {
            float t = Mathf.Clamp01(clock / DarkenSeconds);
            fade.color = Color.Lerp(new Color(1f, .98f, .94f, 1f), Color.black, Ease(t));
            if (t >= 1f)
            {
                var company = CorporateEconomy.Instance != null ? CorporateEconomy.Instance.PlayerCompany : null; var growth = Growth;
                if (EndingOverlay.Instance != null && company != null && growth != null) { EndingOverlay.Instance.Play(company.stockName, company.business.MarketCap, growth.EndingDay, true); }
                Stage = 4; clock = 0f;
            }
        }
        else if (EndingOverlay.Instance == null || !EndingOverlay.Instance.Playing)
        {
            // 글자가 지나갔다. 꿈에서 깬다: 문은 다시 잠겨 있고, 계좌는 비어 있고, 침대에서 눈을 뜬다.
            var growth = Growth; if (growth != null) { growth.EndingShown = true; }
            Swing(shell, 0f);
            // 리딩방과 내 기업은 처음부터 없던 것이다. 지분을 넘긴 뒤에 계좌를 턴다(순서가 바뀌면 내 기업 주식이 거래소로 간다).
            InvestorSociety.Instance?.DissolvePlayerRooms();
            PlayerProgression.Instance?.WakeFromDream();
            CorporateEconomy.Instance?.ReleaseFounderFromDream();
            var account = StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
            if (account != null) { account.WakeFromDream(FindAnyObjectByType<ExchangeBot>(), PocketMoney); }
            SleepPrompt.Instance?.Lids(1f);
            RoomAmbience.Instance?.StopEnding();
            fade.color = Color.clear; fadeRoot.SetActive(false);
            Stage = 0;
            if (BedSleep.Instance != null) { BedSleep.Instance.WakeUp(); } else { RoomShell.Cinematic = false; }
        }
    }
}
