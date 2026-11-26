using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 새 게임을 시작할 때 한 번 보여 주는 60초 프롤로그. 삽화 12장을 정해진 시각에 차례로 보여 주고, 나레이션은 그림과 따로 글자로 띄운다.
/// 시각·자리·글자 크기는 Resources/StoryIntro/Data/intro_timeline.json에서, 글은 Resources/StoryIntro/Localization/intro_strings.csv에서 읽는다(코드에 적어 두지 않는다).
/// 게임의 시계·시장·계좌에는 손대지 않는다. 메뉴가 열려 있는 동안(Time.timeScale 0) 그 위에 겹쳐 재생되고, 끝나거나 건너뛰면 맡겨 둔 일(finished)을 꼭 한 번 부른다.
/// 건너뛰기: 상호작용 키(F)를 1초 동안 누르고 있으면 된다. 화면 오른쪽 위에 글로 알려 준다(단추가 아니다).
/// 끝맺음: 그 장면에서 멈추고 검은 화면으로 어두워진 뒤 맡겨 둔 일을 부른다. 검은 화면은 잠깐 더 남아 있다가 사라진다(그 사이에 게임 쪽이 눈을 감은 상태로 준비한다).
/// </summary>
public sealed class StoryIntro : MonoBehaviour
{
    [Serializable] public sealed class RectPx { public float x, y, width, height; }
    [Serializable] public sealed class ImageAnimation { public int fadeInMs = 280, fadeOutMs = 380; public float scaleFrom = 1f, scaleTo = 1f; }
    [Serializable] public sealed class Caption { public string key; public int startLocalMs, endLocalMs, fadeInMs = 250, fadeOutMs = 320; public RectPx rectPx; public float fontSizePx = 45f, minFontSizePx = 34f; public int maxLines = 4; }
    [Serializable] public sealed class GoalOverlay { public string titleKey, valueKey; public int startLocalMs, endLocalMs, fadeInMs = 350; public RectPx rectPx; public float titleFontSizePx = 32f, valueFontSizePx = 54f; }
    [Serializable] public sealed class Scene { public string id, image; public int startMs, endMs; public ImageAnimation imageAnimation; public Caption caption; public GoalOverlay goalOverlay; }
    /// <summary>영상 속 빈 꼬리표에 얹는 글(예: 매수, 매도). 영상의 시각(밀리초)과 자리(px)로 정한다. 글은 언어마다 따로 읽는다.</summary>
    [Serializable] public sealed class Tag { public string key; public int startMs, endMs, fadeInMs = 120, fadeOutMs = 160; public RectPx rectPx; public float fontSizePx = 22f; public string color = "#E6EBEF"; }
    [Serializable] public sealed class SkipButton { public string key; public RectPx rectPx; public int appearAtMs = 1000; }
    [Serializable] public sealed class Size { public float width = 1920f, height = 1080f; }
    [Serializable] public sealed class Timeline { public int schemaVersion, totalDurationMs; public Size designResolutionPx; public string backgroundColor; public RectPx artAreaPx; public SkipButton skipButton; public Scene[] scenes;
        /// <summary>영상으로 트는 프롤로그: 영상 파일의 자리(Resources/StoryIntro/ 아래, 확장자 없이). 있으면 장면의 그림 대신 이 영상을 처음부터 끝까지 틀고, 장면은 자막이 바뀌는 때만 정한다.</summary>
        public string video;
        public Tag[] tags; }

    private const string Folder = "StoryIntro/", TimelinePath = Folder + "Data/intro_timeline", StringsPath = Folder + "Localization/intro_strings", MusicPath = Folder + "Audio/Intro_Opening_Theme";
    private const int SortingOrder = 30800;
    private const float CaptionFadeOutSeconds = .32f, MusicVolume = .9f;
    /// <summary>건너뛰려면 키를 누르고 있어야 하는 시간, 건너뛸 때와 끝까지 봤을 때 검게 어두워지는 시간, 다 어두워진 뒤 검은 화면이 남아 있는 시간(초).</summary>
    private const float HoldSeconds = 1f, SkipFadeSeconds = .8f, EndFadeSeconds = .35f, LingerSeconds = .45f;
    private const string HoldKey = "intro.skip.hold";

    public static StoryIntro Instance { get; private set; }
    public static bool Playing => Instance != null;
    /// <summary>끝맺음(정상 종료든 건너뛰기든)이 불린 횟수. 확인용.</summary>
    public static int FinishCount { get; private set; }

    /// <summary>0 이상이면 이 값을 재생 시각(초)으로 쓴다. 한 프레임씩 넘기며 찍을 때 쓴다(확인·녹화용).</summary>
    public float ManualClock = -1f;
    public float Elapsed { get; private set; }
    public float Duration => timeline.totalDurationMs * .001f;
    public Canvas Canvas { get; private set; }
    /// <summary>지금 보이는 장면과 자막(확인용).</summary>
    public string ShownScene { get; private set; } = "";
    public string ShownCaptionKey { get; private set; } = "";
    public float ShownImageAlpha { get; private set; }
    public float ShownCaptionAlpha { get; private set; }
    public float ShownGoalAlpha { get; private set; }
    public bool SkipVisible => skipGroup != null && skipGroup.alpha > 0f;
    /// <summary>끝맺는 중인가(멈춰서 어두워지는 중이거나, 검은 화면으로 남아 있는 중).</summary>
    public bool Ending => ending;
    public float HoldProgress => Mathf.Clamp01(held / HoldSeconds);
    public bool MusicPlaying => music != null && music.isPlaying;

    private Timeline timeline;
    private readonly Dictionary<string, string[]> strings = new();
    private readonly List<string> languages = new();
    private Texture2D[] art;
    private Action finished;
    private bool done, ending, called, listenerWasPaused;
    private float held, endClock, endFade, musicAtEnd;
    private Image cover, holdBar;
    private RawImage picture;
    private TextMeshProUGUI caption, goalTitle, goalValue, skipLabel;
    private CanvasGroup captionGroup, goalGroup, skipGroup;
    private AudioSource music;
    private int shownIndex = -2;
    // 영상으로 트는 프롤로그: 영상, 재생기, 그려 받는 판. 영상에 소리가 들어 있으면 그 소리를 쓰고 따로 둔 음악은 틀지 않는다.
    private VideoClip film;
    private VideoPlayer player;
    private RenderTexture screen;
    private bool filmStarted, filmSound, filmEnded;
    private float stalled;
    private TextMeshProUGUI[] tagLabels;
    private CanvasGroup[] tagGroups;
    /// <summary>영상으로 틀고 있는가, 영상이 실제로 돌고 있는가, 영상의 지금 시각(확인용).</summary>
    public bool UsesVideo => film != null;
    public bool VideoRunning => player != null && player.isPlaying;
    public double VideoTime => player != null ? player.time : 0;
    private string captionKey;

    /// <summary>프롤로그를 튼다. 끝나거나 건너뛰면 finished를 한 번 부른다. 자료가 없어 틀 수 없으면 바로 finished를 부르고 false를 돌려준다.</summary>
    public static bool Play(Action finished)
    {
        if (Instance != null)
        { return false; }
        var holder = new GameObject("Area :: Story Intro");
        var intro = holder.AddComponent<StoryIntro>();
        intro.finished = finished;
        if (!intro.Load())
        {
            Debug.LogWarning("[프롤로그] 자료를 읽지 못해 건너뜀");
            intro.Finish();
            return false;
        }
        Instance = intro;
        intro.Build();
        intro.Begin();
        return true;
    }

    private bool Load()
    {
        var data = Resources.Load<TextAsset>(TimelinePath);
        if (data == null)
        { return false; }
        try { timeline = JsonUtility.FromJson<Timeline>(data.text.TrimStart('﻿')); }
        catch (ArgumentException) { return false; }
        if (timeline == null || timeline.schemaVersion != 1 || timeline.totalDurationMs <= 0 || timeline.scenes == null || timeline.scenes.Length == 0)
        { return false; }
        if (timeline.designResolutionPx == null) { timeline.designResolutionPx = new Size(); }
        if (!string.IsNullOrEmpty(timeline.video))
        {
            film = Resources.Load<VideoClip>(Folder + timeline.video);
            if (film == null) { Debug.LogWarning("[프롤로그] 영상을 찾지 못했습니다: " + timeline.video); return false; }
        }
        art = new Texture2D[timeline.scenes.Length];
        for (int i = 0; i < timeline.scenes.Length; i++)
        {
            var scene = timeline.scenes[i];
            if (scene == null || scene.endMs <= scene.startMs)
            { return false; }
            if (scene.imageAnimation == null) { scene.imageAnimation = new ImageAnimation(); }
            // 영상으로 트는 프롤로그는 장면마다 그림이 없어도 된다.
            if (film != null && string.IsNullOrEmpty(scene.image)) { continue; }
            if (string.IsNullOrEmpty(scene.image))
            { return false; }
            string path = scene.image; int dot = path.LastIndexOf('.');
            if (dot > 0) { path = path.Substring(0, dot); }
            art[i] = Resources.Load<Texture2D>(Folder + path);
            if (art[i] == null && film == null)
            { return false; }
        }
        var table = Resources.Load<TextAsset>(StringsPath);
        if (table == null)
        { return false; }
        ReadStrings(table.text);
        return languages.Count > 0;
    }

    // 표의 첫 줄은 "key,ko,en,ja"다. 쉼표가 든 칸은 큰따옴표로 싸여 있다.
    private void ReadStrings(string text)
    {
        strings.Clear(); languages.Clear();
        var rows = ParseCsv(text.TrimStart('﻿'));
        if (rows.Count == 0)
        { return; }
        for (int i = 1; i < rows[0].Count; i++) { languages.Add(rows[0][i].Trim()); }
        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count < 2 || string.IsNullOrEmpty(row[0]))
            { continue; }
            var values = new string[languages.Count];
            for (int i = 0; i < values.Length; i++) { values[i] = i + 1 < row.Count ? row[i + 1] : ""; }
            strings[row[0].Trim()] = values;
        }
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new System.Text.StringBuilder(); bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') { quoted = false; }
                else { cell.Append(c); }
            }
            else if (c == '"') { quoted = true; }
            else if (c == ',') { row.Add(cell.ToString()); cell.Length = 0; }
            else if (c == '\n')
            {
                row.Add(cell.ToString()); cell.Length = 0;
                if (row.Count > 1 || row[0].Length > 0) { rows.Add(row); }
                row = new List<string>();
            }
            else if (c != '\r') { cell.Append(c); }
        }
        row.Add(cell.ToString());
        if (row.Count > 1 || row[0].Length > 0) { rows.Add(row); }
        return rows;
    }

    /// <summary>지금 언어의 글. 그 언어 칸이 비었으면 영어, 그다음 한국어, 그래도 없으면 빈 글을 준다(열쇠 이름을 화면에 내보내지 않는다).</summary>
    public string Word(string key)
    {
        if (string.IsNullOrEmpty(key) || !strings.TryGetValue(key, out var values))
        { return ""; }
        foreach (string code in new[] { GameLocale.Code, "en", "ko" })
        {
            int index = languages.IndexOf(code);
            if (index >= 0 && !string.IsNullOrEmpty(values[index]))
            { return values[index]; }
        }
        return "";
    }

    /// <summary>한국어는 낱말 가운데에서 줄이 바뀌지 않게 낱말마다 묶는다(글자를 더하거나 빼지 않는다). 다른 언어는 그대로 둔다.</summary>
    private static string Lines(string text)
    {
        if (GameLocale.Code != "ko" || string.IsNullOrEmpty(text))
        { return text; }
        return "<nobr>" + string.Join("</nobr> <nobr>", text.Split(' ')) + "</nobr>";
    }

    private void Build()
    {
        float width = timeline.designResolutionPx.width, height = timeline.designResolutionPx.height;
        Canvas = gameObject.AddComponent<Canvas>(); Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.sortingOrder = SortingOrder;
        // 기준 화면(1920×1080)이 어떤 비율에서도 통째로 들어오게 맞춘다. 남는 가장자리는 바탕색이 채운다.
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(width, height); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();

        var backdrop = Child("Image :: Intro Backdrop", transform).gameObject.AddComponent<Image>();
        Stretch(backdrop.rectTransform);
        backdrop.color = ColorUtility.TryParseHtmlString(timeline.backgroundColor, out Color tone) ? tone : new Color(.027f, .031f, .043f, 1f);
        backdrop.raycastTarget = true;

        var stage = Child("Area :: Intro Stage", transform);
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f); stage.sizeDelta = new Vector2(width, height); stage.anchoredPosition = Vector2.zero;

        picture = Child("Image :: Intro Art", stage).gameObject.AddComponent<RawImage>();
        Stretch(picture.rectTransform); picture.raycastTarget = false; picture.color = new Color(1f, 1f, 1f, 0f);
        // 그림이 놓인 자리의 가운데를 축으로 아주 조금 커진다.
        var area = timeline.artAreaPx;
        if (area != null && area.width > 0f && area.height > 0f) { picture.rectTransform.pivot = new Vector2((area.x + area.width * .5f) / width, 1f - (area.y + area.height * .5f) / height); }

        var first = timeline.scenes[0].caption;
        caption = Label("Text :: Intro Narration", stage, first != null ? first.fontSizePx : 45f);
        captionGroup = caption.gameObject.AddComponent<CanvasGroup>(); captionGroup.alpha = 0f; captionGroup.blocksRaycasts = false;
        caption.alignment = TextAlignmentOptions.MidlineLeft; caption.textWrappingMode = TextWrappingModes.Normal; caption.overflowMode = TextOverflowModes.Truncate;
        caption.enableAutoSizing = true; caption.lineSpacing = 12f;

        var goalArea = Child("Area :: Intro Goal", stage);
        goalGroup = goalArea.gameObject.AddComponent<CanvasGroup>(); goalGroup.alpha = 0f; goalGroup.blocksRaycasts = false;
        goalTitle = Label("Text :: Intro Goal Title", goalArea, 32f); goalTitle.alignment = TextAlignmentOptions.BottomLeft; goalTitle.color = new Color(.62f, .66f, .70f, 1f); goalTitle.characterSpacing = 6f;
        goalValue = Label("Text :: Intro Goal Value", goalArea, 54f); goalValue.alignment = TextAlignmentOptions.TopLeft; goalValue.fontStyle = FontStyles.Bold;

        var skip = timeline.skipButton;
        if (skip != null && skip.rectPx != null)
        {
            // 건너뛰기 안내는 글이다(누르는 단추가 아니다). 타임라인이 정한 자리의 오른쪽 끝에 맞춰 놓고, 글이 길면 왼쪽으로 늘어난다.
            var hint = Child("Area :: Intro Skip Hint", stage);
            float wide = Mathf.Max(skip.rectPx.width, 520f);
            Place(hint, new RectPx { x = skip.rectPx.x + skip.rectPx.width - wide, y = skip.rectPx.y, width = wide, height = skip.rectPx.height });
            skipGroup = hint.gameObject.AddComponent<CanvasGroup>(); skipGroup.alpha = 0f; skipGroup.interactable = false; skipGroup.blocksRaycasts = false;
            skipLabel = Label("Text :: Intro Skip", hint, 24f);
            Stretch(skipLabel.rectTransform); skipLabel.alignment = TextAlignmentOptions.Right; skipLabel.color = new Color(.70f, .73f, .76f, 1f);
            // 누르고 있는 동안 글 아래의 가는 줄이 오른쪽에서 왼쪽으로 찬다.
            holdBar = Child("Image :: Intro Skip Hold", hint).gameObject.AddComponent<Image>(); holdBar.raycastTarget = false; holdBar.color = new Color(.93f, .94f, .95f, 1f);
            var bar = holdBar.rectTransform; bar.anchorMin = new Vector2(1f, 0f); bar.anchorMax = new Vector2(1f, 0f); bar.pivot = new Vector2(1f, 1f); bar.anchoredPosition = new Vector2(0f, -4f); bar.sizeDelta = new Vector2(0f, 3f);
        }
        // 끝맺을 때 화면을 덮는 검은 판. 모든 것 위에 있다.
        cover = Child("Image :: Intro Cover", transform).gameObject.AddComponent<Image>();
        Stretch(cover.rectTransform); cover.color = new Color(0f, 0f, 0f, 0f); cover.raycastTarget = false;

        var clip = Resources.Load<AudioClip>(MusicPath);
        if (clip != null)
        {
            music = gameObject.AddComponent<AudioSource>();
            music.clip = clip; music.playOnAwake = false; music.loop = false; music.spatialBlend = 0f; music.volume = MusicVolume; music.ignoreListenerPause = true;
        }
        if (film != null) { BuildVideo(); }
        // 영상 속 꼬리표의 글. 그림(영상) 바로 위, 자막과 같은 층에 놓는다.
        int tagCount = timeline.tags == null ? 0 : timeline.tags.Length;
        tagLabels = new TextMeshProUGUI[tagCount]; tagGroups = new CanvasGroup[tagCount];
        for (int i = 0; i < tagCount; i++)
        {
            var tag = timeline.tags[i];
            if (tag == null || tag.rectPx == null || string.IsNullOrEmpty(tag.key)) { continue; }
            var label = Label("Text :: Intro Tag " + i, stage, tag.fontSizePx);
            Place(label.rectTransform, tag.rectPx); label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = true; label.fontSizeMax = tag.fontSizePx; label.fontSizeMin = tag.fontSizePx * .6f;
            if (ColorUtility.TryParseHtmlString(tag.color, out Color ink)) { label.color = ink; }
            label.transform.SetSiblingIndex(picture.transform.GetSiblingIndex() + 1);
            tagLabels[i] = label; tagGroups[i] = label.gameObject.AddComponent<CanvasGroup>(); tagGroups[i].alpha = 0f; tagGroups[i].blocksRaycasts = false;
        }
        RefreshWords();
    }

    /// <summary>
    /// 영상을 그림 자리에 튼다. 메뉴가 열려 있어 게임 시간이 멈춰 있어도 돌아야 하므로 실제 시간으로 재생한다.
    /// 영상의 소리는 다른 소리가 멈춰 있는 동안에도 들리게 따로 둔 스피커로 보낸다.
    /// </summary>
    private void BuildVideo()
    {
        screen = new RenderTexture(Mathf.Max(16, (int)film.width), Mathf.Max(16, (int)film.height), 0, RenderTextureFormat.ARGB32) { name = "Intro Video" };
        screen.Create();
        var active = RenderTexture.active; RenderTexture.active = screen; GL.Clear(true, true, Color.black); RenderTexture.active = active;
        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false; player.source = VideoSource.VideoClip; player.clip = film; player.isLooping = false; player.skipOnDrop = true;
        player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = screen; player.aspectRatio = VideoAspectRatio.FitInside;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime; player.waitForFirstFrame = true;
        if (film.audioTrackCount > 0)
        {
            var speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false; speaker.spatialBlend = 0f; speaker.volume = MusicVolume; speaker.ignoreListenerPause = true;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource; player.EnableAudioTrack(0, true); player.SetTargetAudioSource(0, speaker);
            // 영상의 소리가 주인이다. 예전 음악은 틀지 않고, 끝맺을 때는 이 스피커의 소리를 줄인다.
            if (music != null) { Destroy(music); }
            music = speaker; filmSound = true;
        }
        else { player.audioOutputMode = VideoAudioOutputMode.None; }
        picture.texture = screen; picture.color = Color.white; picture.rectTransform.localScale = Vector3.one;
        player.loopPointReached += _ => filmEnded = true;
        player.Prepare();
    }

    private void Begin()
    {
        GameLocale.Changed += RefreshWords;
        // 프롤로그 동안에는 게임의 다른 소리(방의 소음, 효과음)를 멈춰 둔다. 음악만 들린다.
        listenerWasPaused = AudioListener.pause; AudioListener.pause = true;
        Elapsed = 0f;
        if (music != null && !filmSound) { music.Play(); }
        Apply(0f);
    }

    void Update()
    {
        if (done)
        { return; }
        if (ending)
        { End(); return; }
        if (film != null)
        {
            // 영상이 준비되면 틀고, 그 뒤로는 영상의 시각을 그대로 따른다(자막이 영상과 어긋나지 않는다). 준비되는 동안에는 0초에 머문다.
            if (!filmStarted) { if (player.isPrepared) { player.Play(); filmStarted = true; } Elapsed = 0f; }
            else if (ManualClock >= 0f) { Elapsed = ManualClock; }
            else
            {
                // 영상이 돌고 있으면 영상의 시각을 그대로 쓴다. 영상이 끝까지 갔으면 그 뒤는 실제 시간으로 이어서 센다(끝맺음으로 넘어가야 한다).
                // 중간에 잠깐 멎었을 때는(창을 바꾸거나 끊겼을 때) 자막도 그 자리에서 기다린다. 다만 3초 넘게 멎어 있으면 실제 시간으로 넘긴다(게임이 여기서 멈춰 서면 안 된다).
                float at = (float)player.time;
                if (at > Elapsed + .0005f) { Elapsed = at; stalled = 0f; }
                else
                {
                    stalled += Time.unscaledDeltaTime;
                    if (filmEnded || stalled > 3f) { Elapsed += Time.unscaledDeltaTime; }
                }
            }
        }
        else if (ManualClock >= 0f) { Elapsed = ManualClock; }
        else { Elapsed += Time.unscaledDeltaTime; }
        if (Elapsed >= Duration)
        { BeginEnd(EndFadeSeconds); return; }
        Apply(Elapsed);
        // 안내가 보인 뒤부터, 상호작용 키를 1초 동안 누르고 있으면 건너뛴다. 중간에 떼면 처음부터 다시 센다.
        held = SkipVisible && Input.GetKey(GameKeyBindings.Interact) ? held + Time.unscaledDeltaTime : 0f;
        if (holdBar != null) { holdBar.rectTransform.sizeDelta = new Vector2(skipLabel.preferredWidth * HoldProgress, 3f); }
        if (held >= HoldSeconds)
        { Skip(); }
    }

    /// <summary>그 장면에서 멈추고 어두워지기 시작한다.</summary>
    private void BeginEnd(float fadeSeconds)
    {
        if (ending || done)
        { return; }
        ending = true; endClock = 0f; endFade = Mathf.Max(.01f, fadeSeconds); musicAtEnd = music != null ? music.volume : 0f;
        // 건너뛰면 영상도 그 자리에서 멈춘다.
        if (player != null && player.isPlaying && fadeSeconds >= SkipFadeSeconds) { player.Pause(); }
        if (skipGroup != null) { skipGroup.alpha = 0f; }
    }

    /// <summary>어두워지는 동안: 화면은 멈춘 채 검은 판이 짙어지고 음악이 잦아든다. 다 어두워지면 맡겨 둔 일을 부르고, 잠깐 뒤에 사라진다.</summary>
    private void End()
    {
        endClock += Time.unscaledDeltaTime;
        float dark = Mathf.Clamp01(endClock / endFade);
        cover.color = new Color(0f, 0f, 0f, dark);
        if (music != null) { music.volume = musicAtEnd * (1f - dark); }
        if (dark >= 1f && !called) { Call(); }
        if (endClock >= endFade + LingerSeconds)
        { Finish(); }
    }

    /// <summary>맡겨 둔 일을 부른다. 몇 번을 불러도 한 번만 일한다.</summary>
    private void Call()
    {
        if (called)
        { return; }
        called = true;
        if (music != null) { music.Stop(); }
        AudioListener.pause = listenerWasPaused;
        FinishCount++;
        var after = finished; finished = null;
        after?.Invoke();
    }

    /// <summary>경과 시각(초)에 맞는 장면·자막·목표 표시를 그린다.</summary>
    private void Apply(float seconds)
    {
        float now = seconds * 1000f; int index = -1;
        for (int i = 0; i < timeline.scenes.Length; i++)
        {
            if (now >= timeline.scenes[i].startMs && now < timeline.scenes[i].endMs) { index = i; break; }
        }
        if (index != shownIndex)
        {
            shownIndex = index;
            if (film == null) { picture.texture = index < 0 ? null : art[index]; }
            ShownScene = index < 0 ? "" : timeline.scenes[index].id;
            captionKey = null;
        }
        if (index < 0)
        {
            picture.color = new Color(1f, 1f, 1f, film != null ? 1f : 0f); captionGroup.alpha = 0f; goalGroup.alpha = 0f;
            ShownImageAlpha = ShownCaptionAlpha = ShownGoalAlpha = 0f; ShownCaptionKey = "";
        }
        else
        {
            var scene = timeline.scenes[index]; var motion = scene.imageAnimation;
            float local = now - scene.startMs, length = scene.endMs - scene.startMs;
            // 영상은 장면 사이를 스스로 잇는다. 그림처럼 장면마다 나타났다 사라지게 하지 않는다.
            ShownImageAlpha = film != null ? 1f : Fade(local, 0f, length, motion.fadeInMs, motion.fadeOutMs);
            picture.color = new Color(1f, 1f, 1f, ShownImageAlpha);
            float scale = film != null ? 1f : Mathf.Lerp(motion.scaleFrom, motion.scaleTo, Mathf.Clamp01(local / length));
            picture.rectTransform.localScale = new Vector3(scale, scale, 1f);

            var words = scene.caption;
            bool hasCaption = words != null && !string.IsNullOrEmpty(words.key) && words.endLocalMs > words.startLocalMs;
            if (hasCaption && captionKey != words.key)
            {
                captionKey = words.key;
                if (words.rectPx != null) { Place(caption.rectTransform, words.rectPx); }
                caption.fontSizeMax = words.fontSizePx; caption.fontSizeMin = words.minFontSizePx; caption.fontSize = words.fontSizePx; caption.maxVisibleLines = Mathf.Max(1, words.maxLines);
                GameLocalizedText.Set(caption, Lines(Word(words.key)), true);
            }
            ShownCaptionAlpha = hasCaption ? Fade(local, words.startLocalMs, words.endLocalMs, words.fadeInMs, words.fadeOutMs) : 0f;
            ShownCaptionKey = ShownCaptionAlpha > 0f ? words.key : "";
            captionGroup.alpha = ShownCaptionAlpha;

            var goal = scene.goalOverlay;
            bool hasGoal = goal != null && !string.IsNullOrEmpty(goal.valueKey) && goal.endLocalMs > goal.startLocalMs;
            if (hasGoal && goalGroup.alpha <= 0f && local >= goal.startLocalMs)
            {
                if (goal.rectPx != null)
                {
                    Place((RectTransform)goalGroup.transform, goal.rectPx);
                    // 제목은 가운데 줄 바로 위에, 값은 바로 아래에 놓는다.
                    float half = goal.rectPx.height * .5f;
                    Place(goalTitle.rectTransform, new RectPx { x = 0f, y = 0f, width = goal.rectPx.width, height = half - 8f });
                    Place(goalValue.rectTransform, new RectPx { x = 0f, y = half + 8f, width = goal.rectPx.width, height = half - 8f });
                }
                goalTitle.fontSize = goal.titleFontSizePx; goalValue.fontSize = goal.valueFontSizePx;
                GameLocalizedText.Set(goalTitle, Word(goal.titleKey), true); GameLocalizedText.Set(goalValue, Word(goal.valueKey), true);
            }
            ShownGoalAlpha = hasGoal ? Fade(local, goal.startLocalMs, goal.endLocalMs, goal.fadeInMs, CaptionFadeOutSeconds * 1000f) : 0f;
            goalGroup.alpha = ShownGoalAlpha;
        }
        for (int i = 0; tagGroups != null && i < tagGroups.Length; i++)
        {
            if (tagGroups[i] == null) { continue; }
            var tag = timeline.tags[i];
            tagGroups[i].alpha = Fade(now, tag.startMs, tag.endMs, tag.fadeInMs, tag.fadeOutMs);
        }
        if (skipGroup != null)
        {
            bool show = now >= timeline.skipButton.appearAtMs;
            skipGroup.alpha = show ? Mathf.Clamp01((now - timeline.skipButton.appearAtMs) / 300f) : 0f;
        }
    }

    /// <summary>start에서 fadeIn 동안 나타나 있다가, end에 맞춰 fadeOut 동안 사라진다(모두 밀리초).</summary>
    private static float Fade(float now, float start, float end, float fadeIn, float fadeOut)
    {
        if (now < start || now >= end)
        { return 0f; }
        float rise = fadeIn <= 0f ? 1f : Mathf.Clamp01((now - start) / fadeIn), fall = fadeOut <= 0f ? 1f : Mathf.Clamp01((end - now) / fadeOut);
        return Mathf.Min(rise, fall);
    }

    /// <summary>
    /// 이 언어로 나올 글자를 미리 글꼴 판에 올려 둔다. 처음 보는 글자는 화면에 뜨는 순간 글꼴 판에 새로 구워지는데,
    /// 일본어에서는 그 일이 둘째 자막에서 몇 초 동안 화면을 멈춰 세웠다(영상은 그동안 혼자 흘러가서 장면 하나를 통째로 놓쳤다).
    /// 시작하기 전(아직 검은 화면일 때)에 한꺼번에 해 둔다.
    /// </summary>
    private void WarmGlyphs()
    {
        if (caption == null || caption.font == null) { return; }
        // 글을 실제로 한 번씩 넣어서 그려 본다. 언어에 따라 글꼴이 바뀌므로(일본어는 일본어 글꼴), 글꼴에 글자를 직접 넣는 것으로는 모자랐다.
        float alpha = captionGroup != null ? captionGroup.alpha : 0f;
        if (captionGroup != null) { captionGroup.alpha = 0f; }
        foreach (string key in strings.Keys)
        {
            string text = Word(key);
            if (string.IsNullOrEmpty(text)) { continue; }
            GameLocalizedText.Set(caption, Lines(text), true); caption.ForceMeshUpdate();
        }
        GameLocalizedText.Set(caption, "", true);
        if (captionGroup != null) { captionGroup.alpha = alpha; }
    }

    /// <summary>언어가 바뀌면 지금 떠 있는 글도 바로 바꾼다.</summary>
    private void RefreshWords()
    {
        WarmGlyphs();
        // "{0}"에는 지금 상호작용 키의 이름이 들어간다.
        if (skipLabel != null) { GameLocalizedText.Set(skipLabel, Word(HoldKey).Replace("{0}", GameKeyBindings.Interact.ToString()), true); }
        if (!string.IsNullOrEmpty(captionKey)) { GameLocalizedText.Set(caption, Lines(Word(captionKey)), true); }
        for (int i = 0; tagLabels != null && i < tagLabels.Length; i++) { if (tagLabels[i] != null) { GameLocalizedText.Set(tagLabels[i], Word(timeline.tags[i].key), true); } }
        var scene = shownIndex >= 0 ? timeline.scenes[shownIndex] : null;
        if (scene != null && scene.goalOverlay != null && !string.IsNullOrEmpty(scene.goalOverlay.valueKey))
        {
            GameLocalizedText.Set(goalTitle, Word(scene.goalOverlay.titleKey), true); GameLocalizedText.Set(goalValue, Word(scene.goalOverlay.valueKey), true);
        }
    }

    /// <summary>건너뛴다: 지금 장면에서 멈추고 검게 어두워진다. 끝까지 봤을 때와 같은 길로 끝맺는다.</summary>
    public void Skip() { BeginEnd(SkipFadeSeconds); }

    /// <summary>화면을 치운다. 맡겨 둔 일을 아직 부르지 않았으면 여기서 부른다(자료가 없어 틀지 못한 경우).</summary>
    private void Finish()
    {
        if (done)
        { return; }
        done = true;
        GameLocale.Changed -= RefreshWords;
        Call();
        if (Instance == this) { Instance = null; }
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // 장면이 바뀌어 사라지는 경우에도 소리 멈춤과 구독을 풀어 둔다. 맡겨 둔 일은 부르지 않는다(이미 끝났거나, 게임이 닫히는 중이다).
        GameLocale.Changed -= RefreshWords;
        if (!called && Instance == this) { AudioListener.pause = listenerWasPaused; }
        if (player != null) { player.Stop(); player.targetTexture = null; }
        if (screen != null) { screen.Release(); Destroy(screen); screen = null; }
        if (Instance == this) { Instance = null; }
        done = true;
    }

    private static RectTransform Child(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    /// <summary>화면 왼쪽 위를 원점으로 한 자리(px)에 놓는다.</summary>
    private static void Place(RectTransform rect, RectPx at)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(at.x, -at.y); rect.sizeDelta = new Vector2(at.width, at.height);
    }
    private static TextMeshProUGUI Label(string name, Transform parent, float size)
    {
        var label = Child(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        label.fontSize = size; label.color = new Color(.93f, .94f, .95f, 1f); label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
}
