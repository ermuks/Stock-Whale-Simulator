using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메모지의 글자를 종이 그림(텍스처)에 구워 넣는다.
/// 전에는 글자가 종이 앞에 뜬 평평한 판에 따로 그려져서, 종이가 나풀거리면 종이가 글자보다 앞으로 나와 글자를 가렸다.
/// 이제 글자가 종이 표면의 일부라서 종이가 휘는 대로 같이 휜다. 글자가 바뀔 때만 다시 굽는다.
/// </summary>
public sealed class StickyNoteInk : MonoBehaviour
{
    /// <summary>굽는 순간에만 글자 판을 올려 두는 레이어. 다른 것은 이 레이어를 쓰지 않는다.</summary>
    private const int BakeLayer = 31;
    private const int TextureWidth = 512;
    private static Camera baker;
    private Canvas canvas;
    private SkinnedMeshRenderer paper;
    private RenderTexture texture;
    private Material material;
    private TextMeshProUGUI[] labels;
    private readonly StringBuilder snapshot = new();
    private string baked = null;
    private float next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { baker = null; }

    /// <summary>종이에 글자 굽기를 붙인다. 글자 판이 없으면 아무 일도 없다.</summary>
    public static void Attach(StickyNotePaper note)
    {
        if (note == null || note.paperRenderer == null) { return; }
        var self = note.GetComponent<StickyNoteInk>();
        // 이미 준비된 종이는 그대로 둔다. 찍어 낸 종이(복제본)는 부품만 따라오고 속은 비어 있으므로 다시 준비한다.
        if (self != null && self.texture != null) { return; }
        var ink = note.transform.Find("Area :: Paper Ink");
        var found = ink == null ? null : ink.GetComponent<Canvas>();
        if (found == null) { return; }
        if (self == null) { self = note.gameObject.AddComponent<StickyNoteInk>(); }
        self.canvas = found; self.paper = note.paperRenderer; self.Prepare();
    }

    private void Prepare()
    {
        var rect = (RectTransform)canvas.transform;
        int height = Mathf.Max(64, Mathf.RoundToInt(TextureWidth * rect.rect.height / Mathf.Max(1f, rect.rect.width)));
        texture = new RenderTexture(TextureWidth, height, 16) { name = "Sticky Note Ink", useMipMap = true, autoGenerateMips = true, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        // 이 종이만의 재질: 종이 그림 자리에 "종이 그림 + 글자"를 구운 것을 넣는다.
        material = paper.material;
        // 글자 판 맨 뒤에 원래 종이 그림을 깔아, 구운 그림이 종이 결을 그대로 갖게 한다.
        // 복제본은 이 그림을 이미 갖고 있다(그 재질에는 원본의 구운 그림이 들어 있으므로 재질에서 읽으면 안 된다).
        var existing = canvas.transform.Find("Image :: Paper Grain");
        if (existing == null)
        {
            var grain = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
            var back = new GameObject("Image :: Paper Grain", typeof(RectTransform), typeof(RawImage)); back.transform.SetParent(canvas.transform, false); back.transform.SetAsFirstSibling();
            var backRect = (RectTransform)back.transform; backRect.anchorMin = Vector2.zero; backRect.anchorMax = Vector2.one; backRect.offsetMin = backRect.offsetMax = Vector2.zero;
            var image = back.GetComponent<RawImage>(); image.texture = grain; image.color = Color.white; image.raycastTarget = false;
        }
        baked = null;
        labels = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var item in canvas.GetComponentsInChildren<Transform>(true)) { item.gameObject.layer = BakeLayer; }
        if (material.HasProperty("_BaseMap")) { material.SetTexture("_BaseMap", texture); }
        material.mainTexture = texture;
        Bake();
    }

    private string Snapshot()
    {
        snapshot.Clear();
        foreach (var label in labels)
        {
            if (label == null) { continue; }
            var color = (Color32)label.color;
            snapshot.Append(label.text).Append('|').Append(color.r).Append(color.g).Append(color.b).Append(color.a).Append(label.gameObject.activeSelf ? '1' : '0').Append('\n');
        }
        return snapshot.ToString();
    }

    void Update()
    {
        if (Time.unscaledTime < next || canvas == null) { return; }
        next = Time.unscaledTime + .4f;
        if (Snapshot() != baked) { Bake(); }
    }

    private void Bake()
    {
        if (canvas == null || texture == null) { return; }
        if (baker == null)
        {
            var holder = new GameObject("Camera :: Sticky Note Ink") { hideFlags = HideFlags.HideAndDontSave };
            baker = holder.AddComponent<Camera>(); baker.enabled = false; baker.orthographic = true; baker.clearFlags = CameraClearFlags.SolidColor; baker.backgroundColor = Color.white;
            baker.cullingMask = 1 << BakeLayer; baker.nearClipPlane = .01f; baker.farClipPlane = 2f; baker.allowHDR = false; baker.allowMSAA = false;
        }
        var rect = (RectTransform)canvas.transform; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * .5f; float tall = Vector3.Distance(corners[0], corners[1]), wide = Vector3.Distance(corners[1], corners[2]);
        if (tall <= 0f || wide <= 0f) { return; }
        // 글자 판을 잠깐 켜서 굽고 다시 끈다. 평소에는 판을 그리지 않는다(글자는 종이 그림 안에 있다).
        canvas.enabled = true;
        foreach (var label in labels) { if (label != null && label.gameObject.activeInHierarchy) { label.ForceMeshUpdate(); } }
        Canvas.ForceUpdateCanvases();
        baker.transform.SetPositionAndRotation(center - canvas.transform.forward * 1f, canvas.transform.rotation);
        baker.orthographicSize = tall * .5f; baker.aspect = wide / tall; baker.targetTexture = texture;
        baker.Render();
        baker.targetTexture = null;
        canvas.enabled = false;
        baked = Snapshot();
    }

    void OnDestroy()
    {
        if (texture != null) { texture.Release(); Destroy(texture); }
        if (material != null) { Destroy(material); }
    }
}
