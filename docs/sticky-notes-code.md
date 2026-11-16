# 포스트잇 코드 기록

최종 사용자 지시: 클릭하지 않는 메모, 실제 Cloth, 목표별 낙하, 영어·한국어·일본어 손글씨 SDF, HUD 가이드 제거.

## 신규 스크립트 전문

### Assets/Scripts/NightShift/InvestorJourneyNotes.cs

```csharp
using System;
using TMPro;
using UnityEngine;

public enum StickyNoteLanguage { English, Korean, Japanese }

/// <summary>Passive handwritten goals, localized only for the game's three supported languages.</summary>
public sealed class InvestorJourneyNotes : MonoBehaviour
{
    [Serializable]
    public sealed class Note
    {
        public StickyNotePaper paper;
        public TextMeshProUGUI number, title, detail, footer, status;
    }
    [Serializable]
    public sealed class Copy
    {
        public string[] titles;
        public string observing, waiting, firstSignal, turnover, profit, preparing, eligible, operate, complete, current;
        public string tenThousand, hundredMillion, trillion, won;
    }
    public Note[] notes = new Note[3];
    public Transform framingBottom;
    public StickyNoteLanguage language = StickyNoteLanguage.Korean;
    public int CurrentStage { get; private set; } = -1;
    private static readonly Color Ink = new Color32(46, 43, 35, 255);
    private static readonly Color SecondaryInk = new Color32(88, 80, 65, 255);
    private static readonly Color ActiveInk = new Color32(122, 45, 30, 255);
    private int completedMask;
    private bool initialized;
    private Copy copy;
    private StickyNoteLanguage loadedLanguage = (StickyNoteLanguage)(-1);

    public void InitializeMilestones(int savedMask)
    {
        if (initialized)
        { return; }
        initialized = true; completedMask = savedMask & 7;
        for (int i = 0; i < notes.Length; i++)
        {
            if ((completedMask & (1 << i)) != 0)
            { notes[i].paper?.HideCompleted(); }
        }
    }

    public void SetLanguage(StickyNoteLanguage value)
    {
        if (value != StickyNoteLanguage.English && value != StickyNoteLanguage.Korean && value != StickyNoteLanguage.Japanese)
        { throw new ArgumentOutOfRangeException(nameof(value)); }
        language = value; EnsureCopy(); CurrentStage = -1;
    }

    private void EnsureCopy()
    {
        if (copy != null && loadedLanguage == language)
        { return; }
        string code;
        switch (language)
        {
            case StickyNoteLanguage.English: code = "en"; break;
            case StickyNoteLanguage.Korean: code = "ko"; break;
            case StickyNoteLanguage.Japanese: code = "ja"; break;
            default: throw new ArgumentOutOfRangeException(nameof(language));
        }
        var data = Resources.Load<TextAsset>("Localization/StickyNotes/" + code);
        if (data == null)
        { throw new InvalidOperationException("Missing sticky note language: " + code); }
        copy = JsonUtility.FromJson<Copy>(data.text); loadedLanguage = language; CurrentStage = -1;
    }

    public void Refresh(NpcLeadingRoom npc, PlayerProgression career, int step)
    {
        if (npc == null || career == null || notes.Length != 3)
        { return; }
        EnsureCopy();
        Set(notes[1].number, "02 / " + copy.won);
        if ((completedMask & 1) == 0)
        {
            Set(notes[0].title, copy.titles[0]);
            Set(notes[0].detail, npc.JoinRequested ? copy.waiting : copy.observing);
            Set(notes[0].footer, copy.firstSignal);
        }
        if ((completedMask & 2) == 0)
        {
            Set(notes[1].title, copy.titles[1]);
            Set(notes[1].detail, copy.turnover + " " + Amount(career.Turnover) + " / " + Amount(career.rules.leadingTurnover));
            Set(notes[1].footer, copy.profit + " " + Amount(career.RealizedProfit) + " / " + Amount(career.rules.leadingRealizedProfit));
        }
        if ((completedMask & 4) == 0)
        {
            Set(notes[2].title, copy.titles[2]);
            Set(notes[2].detail, career.Eligible ? copy.eligible : copy.preparing);
            Set(notes[2].footer, copy.operate);
        }
        int changed = career.JourneyMilestones & ~completedMask;
        float delay = .45f;
        for (int i = 0; i < notes.Length; i++)
        {
            if ((changed & (1 << i)) == 0)
            { continue; }
            Set(notes[i].status, copy.complete); notes[i].paper?.Release(delay); delay += .65f;
        }
        completedMask |= changed;
        int current = (completedMask & 1) == 0 ? 0 : (completedMask & 2) == 0 ? 1 : (completedMask & 4) == 0 ? 2 : -1;
        if (current == CurrentStage)
        { return; }
        CurrentStage = current;
        for (int i = 0; i < notes.Length; i++)
        {
            Set(notes[i].status, (completedMask & (1 << i)) != 0 ? copy.complete : i == current ? copy.current : "");
            notes[i].title.color = i == current ? ActiveInk : Ink;
            notes[i].status.color = i == current ? ActiveInk : SecondaryInk;
        }
    }

    private string Amount(decimal value)
    {
        decimal magnitude = Math.Abs(value);
        if (language == StickyNoteLanguage.English)
        {
            decimal divisor = magnitude >= 1000000000000m ? 1000000000000m : magnitude >= 1000000000m ? 1000000000m : magnitude >= 1000000m ? 1000000m : magnitude >= 1000m ? 1000m : 1m;
            string suffix = divisor == 1000000000000m ? "T" : divisor == 1000000000m ? "B" : divisor == 1000000m ? "M" : divisor == 1000m ? "K" : "";
            return (value / divisor).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + suffix;
        }
        if (magnitude < 10000m)
        { return value.ToString("#,##0;-#,##0;0", System.Globalization.CultureInfo.InvariantCulture); }
        decimal unit = magnitude >= 1000000000000m ? 1000000000000m : magnitude >= 100000000m ? 100000000m : 10000m;
        string ending = unit == 1000000000000m ? copy.trillion : unit == 100000000m ? copy.hundredMillion : copy.tenThousand;
        return (value / unit).ToString("#,##0.#;-#,##0.#;0", System.Globalization.CultureInfo.InvariantCulture) + ending;
    }
    private static void Set(TMP_Text label, string value)
    {
        if (label != null && label.text != value)
        { label.text = value; }
    }
}
```

### Assets/Scripts/NightShift/StickyNotePaper.cs

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A stiff, pinned Cloth sheet. A completed goal peels away once, lands, then stops simulating.</summary>
public sealed class StickyNotePaper : MonoBehaviour
{
    public Cloth paperCloth;
    public SkinnedMeshRenderer paperRenderer;
    public Rigidbody body;
    public BoxCollider landingCollider;
    public float settleAfterSeconds = 4f;
    public bool Released { get; private set; }
    public bool Settled { get; private set; }
    public int ReleaseCount { get; private set; }
    private Mesh settledMesh;

    public void Release(float delay)
    {
        if (Released || !gameObject.activeInHierarchy)
        { return; }
        Released = true; ReleaseCount++;
        StartCoroutine(Fall(delay));
    }

    private IEnumerator Fall(float delay)
    {
        // Scaled time: pause freezes both the detachment and the physical fall.
        yield return new WaitForSeconds(delay);
        var constraints = paperCloth.coefficients;
        for (int i = 0; i < constraints.Length; i++)
        { constraints[i].maxDistance = Mathf.Max(constraints[i].maxDistance, .006f); }
        paperCloth.coefficients = constraints;
        landingCollider.enabled = true;
        body.isKinematic = false; body.useGravity = true;
        body.linearVelocity = -transform.forward * .24f + Vector3.down * .08f;
        body.angularVelocity = transform.right * 2.2f + transform.forward * .15f;
        float elapsed = 0f;
        while (elapsed < settleAfterSeconds)
        {
            yield return new WaitForFixedUpdate(); elapsed += Time.fixedDeltaTime;
            if (elapsed > 1f && body.IsSleeping())
            { break; }
        }
        Settle();
    }

    private void Settle()
    {
        if (Settled)
        { return; }
        Settled = true;
        settledMesh = new Mesh { name = "Settled Sticky Paper" };
        // Paper meshes are authored at world size with unit-scale transforms: Cloth baking cannot double-scale them.
        paperRenderer.BakeMesh(settledMesh, false);
        var resting = new GameObject("Mesh :: Resting Paper", typeof(MeshFilter), typeof(MeshRenderer)); resting.transform.SetParent(transform, false);
        var filter = resting.GetComponent<MeshFilter>(); filter.sharedMesh = settledMesh;
        var renderer = resting.GetComponent<MeshRenderer>(); renderer.sharedMaterials = paperRenderer.sharedMaterials;
        renderer.shadowCastingMode = ShadowCastingMode.TwoSided; renderer.receiveShadows = true;
        paperCloth.enabled = false; paperRenderer.enabled = false;
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        body.isKinematic = true; body.Sleep(); landingCollider.enabled = false;
    }

    public void HideCompleted()
    {
        Released = Settled = true;
        paperCloth.enabled = false; body.isKinematic = true;
        gameObject.SetActive(false);
    }
    void OnDestroy()
    {
        if (settledMesh != null)
        { Destroy(settledMesh); }
    }
}
```

### Assets/Editor/InvestorJourneyNotesSetup.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Builds three real paper meshes under the existing monitor. Never moves a device or camera.</summary>
public static class InvestorJourneyNotesSetup
{
    public const string Folder = "Assets/Resources/Environment/InvestorNotes";
    public const string PrefabPath = Folder + "/InvestorJourneyNotes.prefab";
    private const int Columns = 6, Rows = 8;
    private const float PaperHeight = .60f;
    private static readonly Color Ink = new Color32(46, 43, 35, 255);

    [MenuItem("DonDon/Environment/Rebuild Investor Sticky Notes")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlaying)
        { throw new InvalidOperationException("Stop Play Mode before rebuilding sticky notes."); }
        var existing = Object.FindAnyObjectByType<InvestorJourneyNotes>(FindObjectsInactive.Include);
        if (existing != null) { Object.DestroyImmediate(existing.gameObject); }
        Install();
    }

    [MenuItem("DonDon/Environment/Install Investor Sticky Notes")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
        { throw new InvalidOperationException("Stop Play Mode before installing the sticky notes."); }
        var desk = Object.FindAnyObjectByType<DeskViewController>();
        var journey = Object.FindAnyObjectByType<InvestorJourneyUI>();
        if (desk == null || journey == null || desk.ScreenFor(DeskViewTarget.Chart) == null)
        { throw new InvalidOperationException("Existing chart monitor and investor journey are required."); }
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var texture = ImportPaper();
        StickyNoteFontSetup.Ensure();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StickyNoteFontSetup.PrimaryPath);
        if (font == null)
        { throw new InvalidOperationException("The multilingual handwriting SDF is required."); }
        var existing = Object.FindAnyObjectByType<InvestorJourneyNotes>(FindObjectsInactive.Include);
        if (existing == null)
        {
            var corners = new Vector3[4]; desk.ScreenFor(DeskViewTarget.Chart).GetWorldCorners(corners);
            var prefab = BuildPrefab(texture, font, Vector3.Distance(corners[0], corners[3]) * .16f);
            existing = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<InvestorJourneyNotes>();
            Position(existing, desk.ScreenFor(DeskViewTarget.Chart));
        }
        journey.paperNotes = existing; desk.journeyNotes = existing;
        if (journey.card != null) { journey.card.SetActive(false); }
        if (journey.reopen != null) { journey.reopen.gameObject.SetActive(false); }
        RemoveHudGuidance();
        foreach (var chat in Object.FindObjectsByType<TabletGroupChatUI>(FindObjectsInactive.Include))
        {
            if (chat.hudStatus != null)
            { chat.hudStatus.gameObject.SetActive(false); EditorUtility.SetDirty(chat.hudStatus.gameObject); }
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(existing);
        EditorUtility.SetDirty(journey); EditorUtility.SetDirty(desk);
        EditorSceneManager.MarkSceneDirty(journey.gameObject.scene); AssetDatabase.SaveAssets();
    }

    private static Texture2D ImportPaper()
    {
        string path = Folder + "/StickyPaper_Albedo.png";
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        { throw new InvalidOperationException("Generate the sticky paper albedo before installation."); }
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None; importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4; importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.isReadable = false;
        importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void RemoveHudGuidance()
    {
        var feed = Object.FindAnyObjectByType<MarketEventFeed>();
        if (feed != null && feed.eventText != null)
        {
            var label = feed.eventText.gameObject; feed.eventText = null;
            Object.DestroyImmediate(label); EditorUtility.SetDirty(feed);
        }
        var phone = Object.FindAnyObjectByType<PhoneCallController>();
        if (phone != null && phone.alert != null)
        {
            var label = phone.alert.gameObject; phone.alert = null;
            Object.DestroyImmediate(label); EditorUtility.SetDirty(phone);
        }
        foreach (var hints in Object.FindObjectsByType<DeskControlHints>(FindObjectsInactive.Include))
        { hints.showShortcutHints = false; EditorUtility.SetDirty(hints); }
    }

    private static GameObject BuildPrefab(Texture2D texture, TMP_FontAsset font, float width)
    {
        var root = new GameObject("Area :: Investor Sticky Notes");
        var notes = root.AddComponent<InvestorJourneyNotes>();
        var colors = new[] { new Color(1f,.89f,.47f), new Color(1f,.72f,.51f), new Color(.74f,.87f,.61f) };
        string[] names = { "Observation", "Performance", "Own Room" };
        for (int i = 0; i < 3; i++)
        {
            Mesh mesh = PaperMesh(i, width);
            var obj = new GameObject("Area :: Sticky Note " + names[i]); obj.layer = 2; obj.transform.SetParent(root.transform, false);
            var renderer = obj.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh;
            renderer.sharedMaterial = PaperMaterial(names[i], colors[i], texture);
            renderer.bones = new[] { obj.transform }; renderer.rootBone = obj.transform;
            renderer.updateWhenOffscreen = false; renderer.localBounds = new Bounds(new Vector3(0,-PaperHeight*.5f,0)*width,new Vector3(1.15f,PaperHeight+.17f,.3f)*width);
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided; renderer.receiveShadows = true;
            var cloth = obj.AddComponent<Cloth>();
            cloth.stretchingStiffness = 1f; cloth.bendingStiffness = .96f; cloth.damping = .82f;
            cloth.clothSolverFrequency = 30f; cloth.stiffnessFrequency = 60f;
            cloth.useGravity = true; cloth.useTethers = true; cloth.useVirtualParticles = 0f;
            cloth.enableContinuousCollision = false; cloth.selfCollisionDistance = 0f;
            cloth.worldAccelerationScale = .04f; cloth.worldVelocityScale = .06f;
            cloth.externalAcceleration = new Vector3(.015f,0,-.16f);
            cloth.randomAcceleration = new Vector3(.025f,.01f,.05f);
            var coefficients = new ClothSkinningCoefficient[mesh.vertexCount];
            var vertices = mesh.vertices;
            for (int v = 0; v < vertices.Length; v++)
            {
                float t = -vertices[v].y / (PaperHeight * width);
                float edge = Mathf.Abs(vertices[v].x) > .33f * width || t > .74f ? 1f : .2f;
                coefficients[v].maxDistance = t <= .13f ? 0f : .016f * width * t * edge;
                coefficients[v].collisionSphereDistance = 0f;
            }
            cloth.coefficients = coefficients;
            var body = obj.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            body.mass = .003f; body.linearDamping = .6f; body.angularDamping = 1.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var landing = obj.AddComponent<BoxCollider>(); landing.center = new Vector3(0,-PaperHeight*.5f,-.01f)*width;
            landing.size = new Vector3(.98f,PaperHeight,.016f)*width; landing.enabled = false;
            var paper = obj.AddComponent<StickyNotePaper>(); paper.paperCloth = cloth; paper.paperRenderer = renderer;
            paper.body = body; paper.landingCollider = landing;
            var canvas = new GameObject("Area :: Paper Ink",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup)).GetComponent<Canvas>();
            var input = canvas.GetComponent<CanvasGroup>(); input.blocksRaycasts = false; input.interactable = false;
            canvas.renderMode = RenderMode.WorldSpace; canvas.transform.SetParent(obj.transform, false);
            var rect = (RectTransform)canvas.transform; rect.sizeDelta = new Vector2(512,PaperHeight*512);
            rect.pivot = new Vector2(.5f,1f); rect.localPosition = new Vector3(0,0,-.029f)*width; rect.localScale = Vector3.one*width/512f;
            var number = Text(canvas.transform, font, "Number", (i+1).ToString("00"),34,4,230,48,32,false);
            var status = Text(canvas.transform,font,"Status","",340,4,138,48,32,false); status.alignment = TextAlignmentOptions.TopRight;
            notes.notes[i] = new InvestorJourneyNotes.Note {
                paper = paper, status = status, number = number,
                title = Text(canvas.transform,font,"Title",new[] {"리딩방 가입","매매 성과","내 리딩방"}[i],34,40,444,98,66,false),
                detail = Text(canvas.transform,font,"Detail",new[] {"운영자 관찰","거래 0 / 10만","개설 준비"}[i],34,145,444,74,50,false),
                footer = Text(canvas.transform,font,"Goal",new[] {"첫 신호 기다리기","손익 0 / 5,000","직접 운영하기"}[i],34,223,444,72,48,false)
            };
        }
        notes.framingBottom = new GameObject("Area :: Notes Framing Bottom").transform; notes.framingBottom.SetParent(root.transform,false);
        var glow = new GameObject("Light :: Monitor Paper Spill",typeof(Light)); glow.transform.SetParent(root.transform,false);
        var light = glow.GetComponent<Light>(); light.type = LightType.Spot; light.color = new Color(.98f,.99f,1f);
        light.intensity = .6f; light.range = 3f; light.spotAngle = 120f; light.innerSpotAngle = 100f;
        light.shadows = LightShadows.None; light.bounceIntensity = 0f;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root,PrefabPath); Object.DestroyImmediate(root); return prefab;
    }

    private static void Position(InvestorJourneyNotes notes, RectTransform screen)
    {
        var corners = new Vector3[4]; screen.GetWorldCorners(corners);
        Vector3 right = (corners[3]-corners[0]).normalized, up = (corners[1]-corners[0]).normalized;
        Vector3 forward = Vector3.Cross(right,up).normalized;
        float width = Vector3.Distance(corners[3],corners[0])*.16f;
        // Root scale remains 1. Paper size follows the authored monitor, not its model's unusual pivot.
        notes.transform.SetPositionAndRotation(corners[3]-right*.025f+up*.095f-forward*.055f,Quaternion.LookRotation(forward,up));
        float[] angles = { -3.3f, 2.1f, -1.5f };
        for (int i=0; i<3; i++)
        {
            var paper = notes.notes[i].paper.transform;
            paper.localPosition = new Vector3(-(2.5f-i)*width*1.04f, i==1?-.024f:0f, -i*.002f);
            paper.localRotation = Quaternion.Euler(0,0,angles[i]); paper.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(paper);
        }
        notes.framingBottom.localPosition = new Vector3(-width*1.5f,-width*(PaperHeight+.12f),0);
        PrefabUtility.RecordPrefabInstancePropertyModifications(notes.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(notes.framingBottom);
        var light = notes.GetComponentInChildren<Light>().transform;
        light.localPosition = new Vector3(-width*1.5f,.1f,-.95f);
        light.localRotation = Quaternion.LookRotation(new Vector3(0,-width*.3f-.1f,.95f));
        PrefabUtility.RecordPrefabInstancePropertyModifications(light);
    }

    private static Mesh PaperMesh(int variant, float width)
    {
        string path = Folder + "/StickyPaper_" + variant + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool created = mesh == null;
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); var weights = new List<BoneWeight>();
        for (int r=0; r<=Rows; r++)
        {
            float t = (float)r/Rows;
            for (int c=0; c<=Columns; c++)
            {
                float u = (float)c/Columns, x=u-.5f;
                float curl = Mathf.Pow(Mathf.Max(0f,(t-.76f)/.24f),2f)*(.025f+variant*.006f)*(1f+x*(variant==1?-1f:1f));
                float z = -.005f*t*t-curl-.004f*Mathf.Pow(Mathf.Abs(x)*2f,4f)*t;
                vertices.Add(new Vector3(x,-t*PaperHeight,z)*width); uv.Add(new Vector2(u,1f-t));
                weights.Add(new BoneWeight { boneIndex0=0,weight0=1f });
                if (r<Rows && c<Columns)
                {
                    int a=r*(Columns+1)+c, b=a+Columns+1;
                    triangles.AddRange(new[] {a,a+1,b,a+1,b+1,b});
                }
            }
        }
        if (created) { mesh = new Mesh { name="Stiff Sticky Paper "+variant }; } else { mesh.Clear(); }
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
        mesh.boneWeights = weights.ToArray(); mesh.bindposes = new[] { Matrix4x4.identity };
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        if (created) { AssetDatabase.CreateAsset(mesh,path); } else { EditorUtility.SetDirty(mesh); }
        return mesh;
    }

    private static Material PaperMaterial(string name, Color color, Texture texture)
    {
        string path = Folder + "/Paper_" + name.Replace(' ','_') + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        { return material; }
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Sticky Paper " + name };
        material.SetTexture("_BaseMap",texture); material.SetColor("_BaseColor",color);
        material.SetFloat("_Smoothness",.06f); material.SetFloat("_Metallic",0); material.SetFloat("_Cull",0);
        material.doubleSidedGI = true; material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(material,path); return material;
    }

    private static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, string name, string value, float x, float y, float w, float h, float size, bool bold)
    {
        var rect = new GameObject("Text :: Sticky "+name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(w,h);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSharedMaterial = font.material;
        text.text = value; text.fontSize = size; text.fontStyle = bold?FontStyles.Bold:FontStyles.Normal; text.color = Ink;
        text.enableAutoSizing = false; text.richText = false; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis; text.alignment = TextAlignmentOptions.TopLeft; return text;
    }
}
```

### Assets/Editor/StickyNoteFontSetup.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Korean/English pen lettering plus Japanese handwriting fallback. Source fonts stay in the build.</summary>
public static class StickyNoteFontSetup
{
    public const string Folder = "Assets/Resources/UI/StickyNotes";
    public const string PrimaryPath = Folder + "/Paper Handwriting KR Latin.asset";
    public const string JapanesePath = Folder + "/Paper Handwriting JP.asset";
    [MenuItem("DonDon/UI/Prepare Sticky Note Handwriting")]
    public static void Ensure()
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var japanese = Create(JapanesePath,"Yomogi-Regular.ttf",new[] {"ja"});
        var primary = Create(PrimaryPath,"NanumPenScript-Regular.ttf",new[] {"ko","en"});
        primary.fallbackFontAssetTable = new List<TMP_FontAsset> { japanese };
        primary.material.SetFloat(ShaderUtilities.ID_FaceDilate,.08f);
        japanese.material.SetFloat(ShaderUtilities.ID_FaceDilate,.08f);
        EditorUtility.SetDirty(primary.material); EditorUtility.SetDirty(japanese.material);
        EditorUtility.SetDirty(primary); AssetDatabase.SaveAssets();
    }

    private static TMP_FontAsset Create(string path, string filename, string[] languages)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null)
        { return existing; }
        string sourcePath = "Assets/Fonts/StickyNotes/" + filename;
        var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
        { throw new FileNotFoundException("Handwriting source missing: " + sourcePath); }
        var font = TMP_FontAsset.CreateFontAsset(source,72,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic);
        font.name = Path.GetFileNameWithoutExtension(path); font.isMultiAtlasTexturesEnabled = true;
        font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        AssetDatabase.CreateAsset(font,path); AssetDatabase.AddObjectToAsset(font.material,font);
        foreach (var texture in font.atlasTextures) { AssetDatabase.AddObjectToAsset(texture,font); }
        string corpus = "0123456789+-/.,:() ";
        foreach (string language in languages)
        {
            var copy = JsonUtility.FromJson<InvestorJourneyNotes.Copy>(File.ReadAllText("Assets/Resources/Localization/StickyNotes/"+language+".json"));
            corpus += string.Join("",copy.titles)+copy.observing+copy.waiting+copy.firstSignal+copy.turnover+copy.profit+copy.preparing+copy.eligible+copy.operate+copy.complete+copy.current+copy.tenThousand+copy.hundredMillion+copy.trillion+copy.won;
        }
        if (!font.TryAddCharacters(new string(corpus.Distinct().ToArray()),out string missing))
        { throw new InvalidOperationException(filename+" is missing required glyphs: "+missing); }
        EditorUtility.SetDirty(font); return font;
    }
}
```

## 기존 함수 원본·수정본

추가 필드: InvestorJourneyUI.paperNotes와 DeskViewController.journeyNotes는 실제 종이를 참조한다. DeskControlHints.showShortcutHints는 기본 false이며 원형 마커는 유지한다. PlayerProgression.JourneyMilestones와 Snapshot.journeyMilestones는 세 장의 완료 이력만 저장한다. 기존 버전 1 저장 구조에 선택 필드를 추가하여 이전 데이터의 기본값은 0이다.


### Assets/Scripts/NightShift/InvestorJourneyUI.cs · Start

원본:

```csharp
void Start()
    {
        action.onClick.AddListener(Act); dismiss.onClick.AddListener(Hide); reopen.onClick.AddListener(Show);
    }
```

수정본:

```csharp
void Start()
    {
        if (action != null) { action.onClick.AddListener(Act); }
        if (dismiss != null) { dismiss.onClick.AddListener(Hide); }
        if (reopen != null) { reopen.onClick.AddListener(Show); }
    }
```

### Assets/Scripts/NightShift/InvestorJourneyUI.cs · Update

원본:

```csharp
void Update()
    {
        if (Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .5f;
        bool visible = StockManager.Instance != null && StockManager.Instance.SetupConfirmed && !DeskViewController.OwnsView && !TabletViewController.IsTabletHeld && !PhoneCallController.IsRaised;
        card.SetActive(visible && !hidden); reopen.gameObject.SetActive(visible && hidden);
        if (!visible)
        { return; }
        var npc = NpcLeadingRoom.Instance; var career = PlayerProgression.Instance;
        if (npc == null || career == null)
        { return; }
        Step = career.CanOperateRooms ? 4 : career.Eligible ? 3 : career.Turnover > 0m ? 2 : npc.PlayerJoined ? 1 : 0;
        string title, body, button;
        if (Step == 0)
        {
            title = "리딩방 참여"; body = npc.JoinRequested ? "가입 대기" : "운영자 관찰"; button = "방 보기";
        }
        else if (Step == 1)
        {
            title = "첫 매매"; body = npc.HasSignal ? "새 주문 신호" : "신호 대기"; button = "신호 보기";
        }
        else if (Step == 2)
        {
            title = "내 리딩방 준비"; body = "실현손익 " + career.RealizedProfit.ToString("+#,##0;-#,##0;0") + "원"; button = "성과 보기";
        }
        else if (Step == 3)
        {
            title = "개설 준비 완료"; body = "운영 조건 달성"; button = "방 열기";
        }
        else
        {
            title = "리딩방 운영자"; body = "내 방 1"; button = "방 관리";
        }
        DeviceTerminalUI.Set(heading,title); DeviceTerminalUI.Set(detail,body); DeviceTerminalUI.Set(actionLabel,button);
        if (stages != null)
        {
            int current = Step <= 1 ? 0 : Step == 2 ? 1 : 2;
            for (int i = 0; i < stages.Length; i++)
            { stages[i].SetTint(i == current ? new Color32(37,175,192,255) : i < current ? Color.white : new Color(.35f,.4f,.43f)); }
        }
    }
```

수정본:

```csharp
void Update()
    {
        if (Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .5f;
        if (paperNotes != null)
        {
            if (card != null && card.activeSelf) { card.SetActive(false); }
            if (reopen != null && reopen.gameObject.activeSelf) { reopen.gameObject.SetActive(false); }
            bool ready = StockManager.Instance != null && StockManager.Instance.SetupConfirmed;
            if (paperNotes.gameObject.activeSelf != ready) { paperNotes.gameObject.SetActive(ready); }
            var sourceRoom = NpcLeadingRoom.Instance; var progress = PlayerProgression.Instance;
            if (ready && sourceRoom != null && progress != null)
            {
                paperNotes.InitializeMilestones(progress.JourneyMilestones);
                progress.RememberJourneyMilestones(sourceRoom.PlayerJoined);
                Step = ResolveStep(sourceRoom, progress);
                paperNotes.Refresh(sourceRoom, progress, Step);
            }
            return;
        }
        bool visible = StockManager.Instance != null && StockManager.Instance.SetupConfirmed && !DeskViewController.OwnsView && !TabletViewController.IsTabletHeld && !PhoneCallController.IsRaised;
        card.SetActive(visible && !hidden); reopen.gameObject.SetActive(visible && hidden);
        if (!visible)
        { return; }
        var npc = NpcLeadingRoom.Instance; var career = PlayerProgression.Instance;
        if (npc == null || career == null)
        { return; }
        Step = ResolveStep(npc, career);
        string title, body, button;
        if (Step == 0)
        {
            title = "리딩방 참여"; body = npc.JoinRequested ? "가입 대기" : "운영자 관찰"; button = "방 보기";
        }
        else if (Step == 1)
        {
            title = "첫 매매"; body = npc.HasSignal ? "새 주문 신호" : "신호 대기"; button = "신호 보기";
        }
        else if (Step == 2)
        {
            title = "내 리딩방 준비"; body = "실현손익 " + career.RealizedProfit.ToString("+#,##0;-#,##0;0") + "원"; button = "성과 보기";
        }
        else if (Step == 3)
        {
            title = "개설 준비 완료"; body = "운영 조건 달성"; button = "방 열기";
        }
        else
        {
            title = "리딩방 운영자"; body = "내 방 1"; button = "방 관리";
        }
        DeviceTerminalUI.Set(heading,title); DeviceTerminalUI.Set(detail,body); DeviceTerminalUI.Set(actionLabel,button);
        if (stages != null)
        {
            int current = Step <= 1 ? 0 : Step == 2 ? 1 : 2;
            for (int i = 0; i < stages.Length; i++)
            { stages[i].SetTint(i == current ? new Color32(37,175,192,255) : i < current ? Color.white : new Color(.35f,.4f,.43f)); }
        }
    }
```

### Assets/Scripts/NightShift/InvestorJourneyUI.cs · ResolveStep

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
private static int ResolveStep(NpcLeadingRoom npc, PlayerProgression career)
    { return career.CanOperateRooms ? 4 : career.Eligible ? 3 : career.Turnover > 0m ? 2 : npc.PlayerJoined ? 1 : 0; }
    void OnDestroy()
    {
        if (action != null) { action.onClick.RemoveListener(Act); }
        if (dismiss != null) { dismiss.onClick.RemoveListener(Hide); }
        if (reopen != null) { reopen.onClick.RemoveListener(Show); }
    }
```

### Assets/Scripts/DeskViewController.cs · TryMonitorPose

원본:

```csharp
private bool TryMonitorPose(DeskViewTarget target, out Pose pose)
    {
        if (!DeskViewFraming.TryPose(ScreenFor(target), viewCamera, monitorFill, corners, out pose, focusFieldOfView))
        { return false; }
        if (target == DeskViewTarget.Stocks || target == DeskViewTarget.Chat)
        {
            Vector3 angles = pose.rotation.eulerAngles;
            angles.y = target == DeskViewTarget.Stocks ? stocksViewYaw : chatViewYaw;
            pose.rotation = Quaternion.Euler(angles);
        }
        return true;
    }
```

수정본:

```csharp
private bool TryMonitorPose(DeskViewTarget target, out Pose pose)
    {
        if (!DeskViewFraming.TryPose(ScreenFor(target), viewCamera, monitorFill, corners, out pose, focusFieldOfView))
        { return false; }
        if (target == DeskViewTarget.Stocks || target == DeskViewTarget.Chat)
        {
            Vector3 angles = pose.rotation.eulerAngles;
            angles.y = target == DeskViewTarget.Stocks ? stocksViewYaw : chatViewYaw;
            pose.rotation = Quaternion.Euler(angles);
        }
        else if (target == DeskViewTarget.Chart && journeyNotes != null && journeyNotes.framingBottom != null)
        {
            // Fit the hanging notes as well as the screen; the actual monitor transform stays untouched.
            Vector3 up = (corners[1] - corners[0]).normalized;
            Vector3 forward = pose.rotation * Vector3.forward;
            float extra = Mathf.Max(0f, Vector3.Dot(corners[0] - journeyNotes.framingBottom.position, up));
            float height = Vector3.Distance(corners[0], corners[1]) + extra;
            float width = Vector3.Distance(corners[0], corners[3]);
            float distance = DeskViewFraming.Distance(width, height, focusFieldOfView, viewCamera.aspect, monitorFill);
            Vector3 center = (corners[0] + corners[2]) * .5f - up * (extra * .5f);
            pose.position = center - forward * Mathf.Max(viewCamera.nearClipPlane + .1f, distance);
        }
        return true;
    }
```

### Assets/Scripts/DeskControlHints.cs · LateUpdate

원본:

```csharp
void LateUpdate()
    {
        if (views == null || hints == null)
        { return; }
        bool shown = views.Ready && !MouseCameraController.TextEntryActive && !ViewingOrderForm();
        if (shown != wasVisible)
        { hints.gameObject.SetActive(shown); wasVisible = shown; }
        bool phone = PhoneCallController.IsRaised;
        bool paused = mouseLook != null && mouseLook.IsPaused;
        int next = ((int)views.Target + 1) * 4 + (phone ? 2 : 0) + (paused ? 1 : 0);
        if (shown && next != state)
        { state = next; RefreshLabels(phone, paused); }
        bool desk = shown && !views.IsFocused && !views.IsTransitioning && !phone;
        for (int i = 0; i < 5; i++)
        {
            RectTransform screen = views.ScreenFor((DeskViewTarget)i);
            Vector2 local = Vector2.zero; bool visible = false;
            if (desk && screen != null && screen.gameObject.activeInHierarchy)
            {
                Vector3 center = screen.TransformPoint(screen.rect.center);
                Vector3 point = views.ViewCamera.WorldToScreenPoint(center);
                visible = point.z > views.ViewCamera.nearClipPlane && views.ViewCamera.pixelRect.Contains(point) && Vector3.Dot(screen.forward, center - views.ViewCamera.transform.position) > 0f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, point, null, out local);
            }
            markers.SetMarker(i, local, visible, desk && (int)views.Hovered == i);
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, MouseCameraController.GetAimScreenPosition(), null, out Vector2 aim);
        markers.SetAim(aim, desk && MouseCameraController.IsCursorLocked && views.Hovered == DeskViewTarget.None);
    }
```

수정본:

```csharp
void LateUpdate()
    {
        if (views == null || hints == null)
        { return; }
        bool shown = views.Ready && !MouseCameraController.TextEntryActive && !ViewingOrderForm();
        bool showLabels = shown && showShortcutHints;
        if (showLabels != wasVisible)
        { hints.gameObject.SetActive(showLabels); wasVisible = showLabels; }
        bool phone = PhoneCallController.IsRaised;
        bool paused = mouseLook != null && mouseLook.IsPaused;
        int next = ((int)views.Target + 1) * 4 + (phone ? 2 : 0) + (paused ? 1 : 0);
        if (showLabels && next != state)
        { state = next; RefreshLabels(phone, paused); }
        bool desk = shown && !views.IsFocused && !views.IsTransitioning && !phone;
        for (int i = 0; i < 5; i++)
        {
            RectTransform screen = views.ScreenFor((DeskViewTarget)i);
            Vector2 local = Vector2.zero; bool visible = false;
            if (desk && screen != null && screen.gameObject.activeInHierarchy)
            {
                Vector3 center = screen.TransformPoint(screen.rect.center);
                Vector3 point = views.ViewCamera.WorldToScreenPoint(center);
                visible = point.z > views.ViewCamera.nearClipPlane && views.ViewCamera.pixelRect.Contains(point) && Vector3.Dot(screen.forward, center - views.ViewCamera.transform.position) > 0f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, point, null, out local);
            }
            markers.SetMarker(i, local, visible, desk && (int)views.Hovered == i);
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, MouseCameraController.GetAimScreenPosition(), null, out Vector2 aim);
        markers.SetAim(aim, desk && MouseCameraController.IsCursorLocked && views.Hovered == DeskViewTarget.None);
    }
```

### Assets/Scripts/MarketDay/TabletGroupChatUI.cs · Update

원본:

```csharp
void Update()
    {
        if (day == null)
        { return; }
        EnsureMentionRoster();
        RefreshTypingIndicator();

        int participantCount = SelectedRoom == 0 ? day.Bots.Count : Room == null ? 0 : Room.Members.Count;
        if (SelectedRoom == NpcLeadingRoom.RoomId && NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined)
        { participantCount++; }
        if (participants != null && lastParticipantCount != participantCount)
        {
            lastParticipantCount = participantCount;
            participants.text = lastParticipantCount.ToString("N0");
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            hudStatus.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}";
        }

        if (!monitorChat && !TabletViewController.IsTabletHeld)
        {
            wasHeld = false;
            return;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            dirty = true;
            lastSecond = -1;
        }

        using (UiMarker.Auto())
        {
            int second = Mathf.CeilToInt(day.RemainingSeconds);
            if (second != lastSecond || dirty || minute != lastMinute || day.Day != lastDay)
            {
                lastSecond = second;
                lastMinute = minute;
                lastDay = day.Day;
                string phase = PhaseText(day.Phase);
                status.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}";
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - 154f * layoutScale);
            if (version != MessageVersion || !Mathf.Approximately(width, measuredWidth))
            {
                RebuildMessageLayout(width);
            }

            int start = FindFirstVisible(scroll.content.anchoredPosition.y);
            if (firstVisible != start)
            {
                dirty = true;
                firstVisible = start;
            }

            if (dirty)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int index = start + i;
                    bool valid = index < MessageCount;
                    row.Rect.gameObject.SetActive(valid);
                    if (!valid)
                    { continue; }

                    var message = Message(index);
                    row.Rect.anchoredPosition = new Vector2(0f, -rowOffsets[index]);
                    row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[index]);
                    if (row.Sequence != message.Sequence)
                    {
                        row.Sequence = message.Sequence;
                        row.BotId = message.BotId;
                        row.SocialMessage = message.Social;
                        SetNoticeMode(row, message.IsRoomNotice);
                        row.Nickname.text = message.Nickname;
                        row.Time.text = $"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}";
                        row.Text.text = DisplayMessage(message);
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }
                    if (row.IsNotice)
                    { LayoutNotice(row, message); continue; }
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(row.Nickname.text, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader);
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(row.Text.text, width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            if (Time.time >= nextAngerRefresh)
            {
                nextAngerRefresh = Time.time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null) { DeviceTerminalUI.Set(row.Nickname, name + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : "")); }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(Time.time * 5f) * .5f + .5f) : 1f;
                    row.AngerDot.color = color;
                }
            }
            if (selectedBot > 0 && second != lastProfileSecond)
            {
                lastProfileSecond = second;
                var bot = day.FindBot(selectedBot);
                if (visualProfile != null)
                { visualProfile.Bind(bot == null ? null : bot.Social); }
                else if (bot == null)
                {
                    profile.text = "참여자가 퇴장했습니다.";
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    profile.text = $"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}";
                }
            }
        }
    }
```

수정본:

```csharp
void Update()
    {
        if (day == null)
        { return; }
        EnsureMentionRoster();
        RefreshTypingIndicator();

        int participantCount = SelectedRoom == 0 ? day.Bots.Count : Room == null ? 0 : Room.Members.Count;
        if (SelectedRoom == NpcLeadingRoom.RoomId && NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined)
        { participantCount++; }
        if (participants != null && lastParticipantCount != participantCount)
        {
            lastParticipantCount = participantCount;
            participants.text = lastParticipantCount.ToString("N0");
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && hudStatus.isActiveAndEnabled && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            hudStatus.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}";
        }

        if (!monitorChat && !TabletViewController.IsTabletHeld)
        {
            wasHeld = false;
            return;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            dirty = true;
            lastSecond = -1;
        }

        using (UiMarker.Auto())
        {
            int second = Mathf.CeilToInt(day.RemainingSeconds);
            if (second != lastSecond || dirty || minute != lastMinute || day.Day != lastDay)
            {
                lastSecond = second;
                lastMinute = minute;
                lastDay = day.Day;
                string phase = PhaseText(day.Phase);
                status.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}";
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - 154f * layoutScale);
            if (version != MessageVersion || !Mathf.Approximately(width, measuredWidth))
            {
                RebuildMessageLayout(width);
            }

            int start = FindFirstVisible(scroll.content.anchoredPosition.y);
            if (firstVisible != start)
            {
                dirty = true;
                firstVisible = start;
            }

            if (dirty)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int index = start + i;
                    bool valid = index < MessageCount;
                    row.Rect.gameObject.SetActive(valid);
                    if (!valid)
                    { continue; }

                    var message = Message(index);
                    row.Rect.anchoredPosition = new Vector2(0f, -rowOffsets[index]);
                    row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[index]);
                    if (row.Sequence != message.Sequence)
                    {
                        row.Sequence = message.Sequence;
                        row.BotId = message.BotId;
                        row.SocialMessage = message.Social;
                        SetNoticeMode(row, message.IsRoomNotice);
                        row.Nickname.text = message.Nickname;
                        row.Time.text = $"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}";
                        row.Text.text = DisplayMessage(message);
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }
                    if (row.IsNotice)
                    { LayoutNotice(row, message); continue; }
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(row.Nickname.text, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader);
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(row.Text.text, width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            if (Time.time >= nextAngerRefresh)
            {
                nextAngerRefresh = Time.time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null) { DeviceTerminalUI.Set(row.Nickname, name + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : "")); }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(Time.time * 5f) * .5f + .5f) : 1f;
                    row.AngerDot.color = color;
                }
            }
            if (selectedBot > 0 && second != lastProfileSecond)
            {
                lastProfileSecond = second;
                var bot = day.FindBot(selectedBot);
                if (visualProfile != null)
                { visualProfile.Bind(bot == null ? null : bot.Social); }
                else if (bot == null)
                {
                    profile.text = "참여자가 퇴장했습니다.";
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    profile.text = $"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}";
                }
            }
        }
    }
```

### Assets/Scripts/NightShift/PhoneCallController.cs · Update

원본:

```csharp
void Update()
    {
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed)
        { return; }
        if (DeskViewController.Instance == null && !MouseCameraController.TextEntryActive && Input.GetKeyDown(KeyCode.Tab) && !TabletViewController.IsTabletHeld)
        { if (raised) { Lower(); } else { Raise(); } }
        if (raised && Input.GetKeyDown(KeyCode.Escape)) { Lower(); }
        if (MarketDayController.CanTrade)
        {
            if (!Ringing && Time.time >= nextCall) { Ring((Caller)random.Next(3)); }
            if (Ringing && Time.time >= expires) { Decline(); message = "부재중 전화. 같은 기회가 다시 오지는 않습니다."; }
        }
        if (Time.unscaledTime < nextUi)
        { return; }
        nextUi = Time.unscaledTime + .25f;
        if (screenUI != null)
        { screenUI.Refresh(this); }
        else
        {
            title.text = Ringing ? (CurrentCaller == Caller.Institution ? "발신자: 기관 담당자 (자칭)" : CurrentCaller == Caller.Research ? "발신자: 리서치 데스크" : "발신자: 알 수 없는 번호") : "연락 기록";
            body.text = Ringing ? "전화를 받으시겠습니까?\n발신자의 주장과 실제 체결 흐름은 다를 수 있습니다.\n\n" + RemainingSeconds + "초 후 종료" : message;
        }
        answer.interactable = decline.interactable = Ringing && MarketDayController.CanTrade;
        alert.text = Ringing ? "전화 수신 · Tab" : "";
    }
```

수정본:

```csharp
void Update()
    {
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed)
        { return; }
        if (DeskViewController.Instance == null && !MouseCameraController.TextEntryActive && Input.GetKeyDown(KeyCode.Tab) && !TabletViewController.IsTabletHeld)
        { if (raised) { Lower(); } else { Raise(); } }
        if (raised && Input.GetKeyDown(KeyCode.Escape)) { Lower(); }
        if (MarketDayController.CanTrade)
        {
            if (!Ringing && Time.time >= nextCall) { Ring((Caller)random.Next(3)); }
            if (Ringing && Time.time >= expires) { Decline(); message = "부재중 전화. 같은 기회가 다시 오지는 않습니다."; }
        }
        if (Time.unscaledTime < nextUi)
        { return; }
        nextUi = Time.unscaledTime + .25f;
        if (screenUI != null)
        { screenUI.Refresh(this); }
        else
        {
            title.text = Ringing ? (CurrentCaller == Caller.Institution ? "발신자: 기관 담당자 (자칭)" : CurrentCaller == Caller.Research ? "발신자: 리서치 데스크" : "발신자: 알 수 없는 번호") : "연락 기록";
            body.text = Ringing ? "전화를 받으시겠습니까?\n발신자의 주장과 실제 체결 흐름은 다를 수 있습니다.\n\n" + RemainingSeconds + "초 후 종료" : message;
        }
        answer.interactable = decline.interactable = Ringing && MarketDayController.CanTrade;
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · Capture

원본:

```csharp
public string Capture()
    {
        return JsonUtility.ToJson(new Snapshot { worldId = WorldId, turnover = Turnover.ToString(CultureInfo.InvariantCulture), realizedProfit = RealizedProfit.ToString(CultureInfo.InvariantCulture), roomsOpened = CanOperateRooms,
            invitedRoomMask = InvestorSociety.Instance?.InvitedRoomMask ?? 0, newsCooldownRemaining = FakeNewsManager.Instance?.PlayerCooldown ?? 0, newsSavedUtc = DateTime.UtcNow.Ticks,
            income = DailyIncomeController.Instance?.Capture() });
    }
```

수정본:

```csharp
public string Capture()
    {
        return JsonUtility.ToJson(new Snapshot { worldId = WorldId, turnover = Turnover.ToString(CultureInfo.InvariantCulture), realizedProfit = RealizedProfit.ToString(CultureInfo.InvariantCulture), roomsOpened = CanOperateRooms,
            invitedRoomMask = InvestorSociety.Instance?.InvitedRoomMask ?? 0, journeyMilestones = JourneyMilestones, newsCooldownRemaining = FakeNewsManager.Instance?.PlayerCooldown ?? 0, newsSavedUtc = DateTime.UtcNow.Ticks,
            income = DailyIncomeController.Instance?.Capture() });
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · Restore

원본:

```csharp
public bool Restore(string json, string restoredWorldId)
    {
        // A progress fragment is not a world save. Never grant a new economy the previous world's rights.
        Snapshot saved;
        try { saved = JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return false; }
        if (saved == null || saved.version != 1 || saved.worldId != WorldId || restoredWorldId != WorldId || saved.invitedRoomMask < 0 || saved.invitedRoomMask >= 1 << InvestorSociety.PlayerRoomCount ||
            !decimal.TryParse(saved.turnover, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal turnover) || turnover < 0m ||
            !decimal.TryParse(saved.realizedProfit, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal profit))
        { return false; }
        if (double.IsNaN(saved.newsCooldownRemaining) || double.IsInfinity(saved.newsCooldownRemaining) || saved.newsCooldownRemaining < 0 || saved.newsCooldownRemaining > 600 ||
            saved.newsSavedUtc < 0 || saved.newsSavedUtc > DateTime.MaxValue.Ticks)
        { return false; }
        if (saved.income != null && (DailyIncomeController.Instance == null || !DailyIncomeController.Instance.CanRestore(saved.income,WorldId)))
        { return false; }
        Turnover = turnover; RealizedProfit = profit; CanOperateRooms = saved.roomsOpened;
        if (saved.income != null) { DailyIncomeController.Instance.Restore(saved.income,WorldId); }
        FakeNewsManager.Instance?.RestorePlayerCooldown(saved.newsCooldownRemaining, saved.newsSavedUtc);
        InvestorSociety.Instance?.RestoreInvitedRooms(saved.invitedRoomMask);
        return true;
    }
```

수정본:

```csharp
public bool Restore(string json, string restoredWorldId)
    {
        // A progress fragment is not a world save. Never grant a new economy the previous world's rights.
        Snapshot saved;
        try { saved = JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return false; }
        if (saved == null || saved.version != 1 || saved.worldId != WorldId || restoredWorldId != WorldId || saved.invitedRoomMask < 0 || saved.invitedRoomMask >= 1 << InvestorSociety.PlayerRoomCount || saved.journeyMilestones < 0 || saved.journeyMilestones > 7 ||
            !decimal.TryParse(saved.turnover, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal turnover) || turnover < 0m ||
            !decimal.TryParse(saved.realizedProfit, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal profit))
        { return false; }
        if (double.IsNaN(saved.newsCooldownRemaining) || double.IsInfinity(saved.newsCooldownRemaining) || saved.newsCooldownRemaining < 0 || saved.newsCooldownRemaining > 600 ||
            saved.newsSavedUtc < 0 || saved.newsSavedUtc > DateTime.MaxValue.Ticks)
        { return false; }
        if (saved.income != null && (DailyIncomeController.Instance == null || !DailyIncomeController.Instance.CanRestore(saved.income,WorldId)))
        { return false; }
        Turnover = turnover; RealizedProfit = profit; CanOperateRooms = saved.roomsOpened;
        JourneyMilestones = saved.journeyMilestones;
        if (saved.income != null) { DailyIncomeController.Instance.Restore(saved.income,WorldId); }
        FakeNewsManager.Instance?.RestorePlayerCooldown(saved.newsCooldownRemaining, saved.newsSavedUtc);
        InvestorSociety.Instance?.RestoreInvitedRooms(saved.invitedRoomMask);
        return true;
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · RememberJourneyMilestones

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
public void RememberJourneyMilestones(bool joinedNpcRoom)
    {
        if (joinedNpcRoom) { JourneyMilestones |= 1; }
        if (Eligible || CanOperateRooms) { JourneyMilestones |= 2; }
        if (CanOperateRooms) { JourneyMilestones |= 4; }
    }
```
