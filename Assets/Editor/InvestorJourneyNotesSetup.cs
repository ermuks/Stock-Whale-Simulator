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
