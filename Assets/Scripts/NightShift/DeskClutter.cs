using System.Collections.Generic;
using UnityEngine;

// 책상 위의 어질러진 물건들. 기능은 없고, 며칠째 치우지 않은 자리처럼 보이게 한다.
// 식은 커피가 담긴 머그컵, 말라붙은 빈 컵, 아무렇게나 쌓인 서류, 구겨 던진 종이, 빈 캔, 먹다 만 컵라면, 굴러다니는 펜, 로션 통, 공책.
// 모양은 PropMesh로 빚는다(돌려 깎은 곡면, 굴린 모서리, 구긴 덩어리). 삼각형 수는 아끼지 않는다.
// 모니터 받침과 태블릿이 놓인 자리는 피해서 책상 앞쪽 양옆과 받침 사이에 둔다. 씬 파일은 그대로다(실행 중에 만든다).
public sealed class DeskClutter : MonoBehaviour
{
    private bool built;
    private Transform root;
    private float unit = 1f;

    void Update()
    {
        if (built) { return; }
        var views = DeskViewController.Instance;
        if (views != null && (!views.Ready || DeskViewController.OwnsView)) { return; }
        var desk = GameObject.Find("desk_body");
        if (desk == null)
        {
            if (Time.timeSinceLevelLoad > 10f) { built = true; }
            return;
        }
        built = true;
        var bounds = desk.GetComponent<Renderer>().bounds;
        var camera = Camera.main;
        // 책상의 긴 쪽이 가로(x), 플레이어 쪽이 앞(-z)이 되게 자리를 잡는다. 책상이 다르게 놓여 있어도 따라간다.
        Vector3 toPlayer = camera != null ? camera.transform.position - bounds.center : Vector3.back; toPlayer.y = 0f;
        toPlayer = Mathf.Abs(toPlayer.z) >= Mathf.Abs(toPlayer.x) ? new Vector3(0f, 0f, Mathf.Sign(toPlayer.z)) : new Vector3(Mathf.Sign(toPlayer.x), 0f, 0f);
        root = new GameObject("Area :: Desk Clutter").transform;
        root.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), Quaternion.LookRotation(-toPlayer, Vector3.up));
        float length = Mathf.Abs(toPlayer.z) > 0f ? bounds.size.x : bounds.size.z;
        unit = length / 7.49f; root.localScale = Vector3.one * unit;

        // 왼쪽 앞: 서류 더미와 식은 커피.
        Papers(new Vector3(-2.95f, 0f, -1.30f), 14f);
        Pen(new Vector3(-2.85f, .075f, -1.22f), 62f, new Color(.05f, .07f, .16f));
        Mug(new Vector3(-1.95f, 0f, -1.42f), 35f, new Color(.30f, .31f, .30f), true);
        Ball(new Vector3(-3.35f, 0f, -.62f), .17f, 1); Ball(new Vector3(-2.35f, 0f, -.82f), .14f, 2); Ball(new Vector3(-3.05f, 0f, -.40f), .12f, 3);
        // 받침 사이: 말라붙은 빈 컵. 자리에 앉아서도 보인다.
        Mug(new Vector3(-1.22f, 0f, .12f), -60f, new Color(.34f, .14f, .10f), false);
        // 오른쪽 앞: 먹다 만 컵라면과 빈 캔.
        Noodles(new Vector3(2.70f, 0f, -1.52f));
        Can(new Vector3(3.18f, 0f, -.78f), new Color(.06f, .10f, .22f), false, 0f);
        Can(new Vector3(3.36f, 0f, -1.22f), new Color(.05f, .16f, .09f), false, 0f);
        Can(new Vector3(3.10f, 0f, -1.30f), new Color(.24f, .24f, .25f), true, 38f);
        Wrapper(new Vector3(2.22f, 0f, -1.66f), -24f);
        Pen(new Vector3(.32f, 0f, -1.62f), -12f, new Color(.30f, .05f, .05f));
        Ball(new Vector3(3.40f, 0f, -.30f), .15f, 4);
        // 더 어질러 놓는다: 며칠 치 물건이 그대로 쌓여 있다.
        Books(new Vector3(-3.22f, 0f, -.72f), 9f, 3, 0);
        Ball(new Vector3(-3.18f, .40f, -.74f), .13f, 5);
        Calendar(new Vector3(-2.28f, 0f, -.60f), 28f);
        PenCup(new Vector3(-3.42f, 0f, -1.78f));
        Bottle(new Vector3(-1.62f, 0f, -1.86f), new Color(.05f, .03f, .02f), new Color(.10f, .16f, .42f), .35f, false, 0f);
        Tissue(new Vector3(-2.48f, 0f, -1.84f), 1); Tissue(new Vector3(-2.12f, 0f, -1.12f), 2); Tissue(new Vector3(-.62f, 0f, -.28f), 3);
        Cord(new Vector3(-1.72f, 0f, -.80f), 40f);
        Remote(new Vector3(.30f, 0f, -.98f), 74f);
        SmallBox(new Vector3(.24f, 0f, -1.34f), -16f, new Color(.36f, .38f, .37f), new Color(.06f, .26f, .30f));
        Bottle(new Vector3(-2.50f, 0f, -1.66f), new Color(.16f, .20f, .22f), new Color(.30f, .34f, .36f), .12f, true, 62f);
        Receipt(new Vector3(1.55f, 0f, -1.86f), 12f); Receipt(new Vector3(.18f, 0f, -1.90f), -31f); Receipt(new Vector3(-1.05f, 0f, -.36f), 55f);
        Books(new Vector3(2.78f, 0f, -.42f), -14f, 2, 3);
        Bottle(new Vector3(2.34f, 0f, -1.02f), new Color(.14f, .18f, .20f), new Color(.08f, .24f, .30f), .55f, true, 24f);
        Bag(new Vector3(3.32f, 0f, -1.80f));
        TissueBox(new Vector3(2.62f, 0f, -1.92f), 21f);
        Can(new Vector3(1.04f, 0f, .24f), new Color(.30f, .06f, .05f), false, 0f);
        Ball(new Vector3(1.48f, 0f, -.12f), .12f, 6); Ball(new Vector3(-.92f, 0f, -1.90f), .11f, 7);
        SmallBox(new Vector3(-2.70f, 0f, -.22f), 33f, new Color(.30f, .27f, .20f), new Color(.34f, .10f, .08f));
        Lotion(new Vector3(-1.48f, 0f, -.52f), 150f);
        Notebook(new Vector3(2.05f, 0f, -1.42f), -11f);
        Pen(new Vector3(2.02f, .062f, -1.36f), 38f, new Color(.05f, .05f, .05f));
        // 다 놓은 다음, 화면이나 태블릿에 걸치는 것들을 정리한다.
        Tidy(bounds, toPlayer);
    }

    /// <summary>몇 개를 옮겼고 몇 개를 치웠는지(확인용).</summary>
    public int Moved { get; private set; }
    public int Removed { get; private set; }

    /// <summary>
    /// 자리에 앉아서 볼 때 모니터 화면과 겹쳐 보이는 물건, 태블릿 위에 걸치는 물건을 정리한다.
    /// 겹치는 물건은 플레이어 쪽으로 조금씩 당겨 보고, 책상 끝까지 가도 겹치면 치운다.
    /// 모니터 뒤나 밑에 박혀 있는 물건도 자리에서 보면 화면과 겹치므로 같이 걸러진다.
    /// </summary>
    private void Tidy(Bounds desk, Vector3 toPlayer)
    {
        var views = DeskViewController.Instance;
        if (views == null || views.ViewCamera == null) { return; }
        var camera = views.ViewCamera; var corners = new Vector3[4];
        float margin = camera.pixelHeight * .02f;
        var screens = new System.Collections.Generic.List<Rect>(); var pads = new System.Collections.Generic.List<Bounds>();
        for (int i = 0; i < 5; i++)
        {
            var screen = views.ScreenFor((DeskViewTarget)i);
            if (screen == null) { continue; }
            screen.GetWorldCorners(corners);
            if (i < 3)
            {
                // 모니터: 자리에서 본 화면의 네모(테두리 여유 포함).
                float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue; bool seen = true;
                foreach (var corner in corners)
                {
                    Vector3 point = camera.WorldToScreenPoint(corner);
                    if (point.z <= 0f) { seen = false; break; }
                    left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x); bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
                }
                if (seen) { screens.Add(Rect.MinMaxRect(left - margin, bottom - margin, right + margin, top + margin)); }
            }
            else
            {
                // 태블릿: 책상 위에서 차지하는 자리(조금 넉넉하게).
                var pad = new Bounds(corners[0], Vector3.zero);
                foreach (var corner in corners) { pad.Encapsulate(corner); }
                pad.Expand(new Vector3(.16f, 4f, .16f) * unit); pads.Add(pad);
            }
        }
        // 겹치는 물건은 가까운 빈자리로 옮긴다: 플레이어 쪽과 양옆으로 조금씩 넓혀 가며 찾고, 책상 위에 자리가 없을 때만 치운다.
        float step = .14f * unit;
        Vector3 sideways = Vector3.Cross(Vector3.up, toPlayer);
        var offsets = new System.Collections.Generic.List<Vector2>();
        for (int forward = 0; forward <= 26; forward++)
        {
            for (int side = -22; side <= 22; side++) { if (forward != 0 || side != 0) { offsets.Add(new Vector2(side, forward)); } }
        }
        offsets.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
        var settled = new System.Collections.Generic.List<Bounds>();
        // 큰 물건(머그컵, 컵라면, 서류, 책…)부터 자리를 잡는다. 자잘한 것(구겨진 종이, 휴지, 영수증…)은 남는 자리에 놓고, 자리가 없으면 그것부터 치운다.
        var large = new System.Collections.Generic.List<Transform>(); var small = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in root) { if (Filler(child.name)) { small.Add(child); } else { large.Add(child); } }
        foreach (var group in new[] { large, small })
        {
            var waiting = new System.Collections.Generic.List<Transform>();
            foreach (var item in group)
            {
                var box = Box(item);
                if (Blocks(item, camera, screens, pads) || group == small && Crowded(box, settled)) { waiting.Add(item); } else { settled.Add(box); }
            }
            foreach (var item in waiting)
            {
                Vector3 origin = item.position; bool placed = false;
                foreach (var offset in offsets)
                {
                    item.position = origin + (sideways * offset.x + toPlayer * offset.y) * step;
                    var box = Box(item);
                    // 책상 밖으로 나가면 안 된다.
                    if (box.min.x < desk.min.x + .05f * unit || box.max.x > desk.max.x - .05f * unit || box.min.z < desk.min.z + .05f * unit || box.max.z > desk.max.z - .05f * unit) { continue; }
                    if (Blocks(item, camera, screens, pads) || Crowded(box, settled)) { continue; }
                    settled.Add(box); placed = true; Moved++; break;
                }
                if (!placed) { item.gameObject.SetActive(false); Destroy(item.gameObject); Removed++; }
            }
        }
    }
    private static bool Filler(string name) => name.Contains("Crumpled") || name.Contains("Tissue") && !name.Contains("Box") || name.Contains("Receipt") || name.Contains("Pen") && !name.Contains("Cup")
        || name.Contains("Small Box") || (name.Contains("Cord") || name.Contains("Earphones")) || name.Contains("Can") || name.Contains("Wrapper");
    private static bool Crowded(Bounds box, System.Collections.Generic.List<Bounds> settled)
    {
        foreach (var other in settled)
        {
            if (box.min.x < other.max.x && box.max.x > other.min.x && box.min.z < other.max.z && box.max.z > other.min.z) { return true; }
        }
        return false;
    }
    private static Bounds Box(Transform item)
    {
        var renderers = item.GetComponentsInChildren<Renderer>();
        var box = new Bounds(item.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++) { if (i == 0) { box = renderers[i].bounds; } else { box.Encapsulate(renderers[i].bounds); } }
        return box;
    }
    private static bool Blocks(Transform item, Camera camera, System.Collections.Generic.List<Rect> screens, System.Collections.Generic.List<Bounds> pads)
    {
        var box = Box(item);
        foreach (var pad in pads)
        {
            if (box.min.x < pad.max.x && box.max.x > pad.min.x && box.min.z < pad.max.z && box.max.z > pad.min.z) { return true; }
        }
        float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = box.center + Vector3.Scale(box.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 point = camera.WorldToScreenPoint(corner);
            if (point.z <= 0f) { continue; }
            left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x); bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
        }
        if (left > right) { return false; }
        var seen = Rect.MinMaxRect(left, bottom, right, top);
        foreach (var screen in screens) { if (screen.Overlaps(seen)) { return true; } }
        return false;
    }

    // ── 재질과 조립 도구 ─────────────────────────────────────────────────────
    private static readonly Dictionary<string, Material> materials = new();
    private static readonly Dictionary<string, Mesh> meshes = new();
    /// <summary>같은 설정의 재질은 하나만 만들어 같이 쓴다.</summary>
    private static Material Lit(Color color, float smoothness, float metallic = 0f, float alpha = 1f, Texture texture = null)
    {
        string key = ColorUtility.ToHtmlStringRGBA(color) + "/" + smoothness.ToString("0.00") + "/" + metallic.ToString("0.00") + "/" + alpha.ToString("0.00") + "/" + (texture == null ? "" : texture.name);
        if (materials.TryGetValue(key, out var found) && found != null) { return found; }
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = "Clutter " + key };
        color.a = alpha; material.SetColor("_BaseColor", color); material.color = color;
        material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", metallic);
        if (texture != null) { material.SetTexture("_BaseMap", texture); material.mainTexture = texture; }
        if (alpha < 1f)
        {
            // 비쳐 보이는 플라스틱·유리.
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f); material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
        }
        materials[key] = material;
        return material;
    }
    /// <summary>같은 모양은 한 번만 빚어 같이 쓴다.</summary>
    private static Mesh Shape(string key, System.Func<Mesh> build)
    {
        if (meshes.TryGetValue(key, out var mesh) && mesh != null) { return mesh; }
        mesh = build(); mesh.name = key; meshes[key] = mesh;
        return mesh;
    }
    private Transform Group(string name, Vector3 position, float turn)
    {
        var group = new GameObject(name).transform; group.SetParent(root, false);
        group.localPosition = position; group.localRotation = Quaternion.Euler(0f, turn, 0f);
        return group;
    }
    private static Transform Part(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
        item.SetParent(parent, false); item.localPosition = position; item.localRotation = rotation; item.localScale = scale;
        item.GetComponent<MeshFilter>().sharedMesh = mesh; item.GetComponent<MeshRenderer>().sharedMaterial = material;
        return item;
    }
    private static Transform Part(string name, Transform parent, Mesh mesh, Material material, Vector3 position)
    { return Part(name, parent, mesh, material, position, Quaternion.identity, Vector3.one); }
    private static Vector2 P(float radius, float height) => new Vector2(radius, height);
    /// <summary>둥근 단면: from도(-90이면 아래 꼭지, 0이면 가장 넓은 허리)에서 위 꼭지(90도)까지. 아래 꼭지에서 시작하면 높이가 0부터 2×height까지다.</summary>
    private static Vector2[] Arc(float radius, float height, float from, int steps)
    {
        var points = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float angle = Mathf.Lerp(from, 90f, i / (float)steps) * Mathf.Deg2Rad;
            points[i] = new Vector2(i == steps ? 0f : radius * Mathf.Cos(angle), height * (Mathf.Sin(angle) - Mathf.Sin(from * Mathf.Deg2Rad)));
        }
        if (from <= -90f) { points[0] = new Vector2(0f, 0f); }
        return points;
    }
    /// <summary>위를 보는 원판(가장자리에서 가운데로).</summary>
    private static Mesh Disc(float radius) => Shape("disc" + radius.ToString("0.000"), () => PropMesh.Lathe(new[] { P(radius, 0f), P(0f, 0f) }, 48));

    // 인쇄된 종이처럼 보이는 작은 그림들.
    private static Texture2D pageTexture, calendarTexture, receiptTexture;
    private static Texture2D Printed(string name, int width, int height, System.Func<int, int, float> ink)
    {
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float shade = Mathf.Clamp01(ink(x, height - 1 - y));
                byte value = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 70f, shade));
                pixels[y * width + x] = new Color32(value, value, value, 255);
            }
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
        texture.SetPixels32(pixels); texture.Apply(true);
        return texture;
    }
    /// <summary>글줄이 있는 문서: 제목 한 줄과 길이가 제각각인 본문.</summary>
    private static Texture2D Page()
    {
        if (pageTexture != null) { return pageTexture; }
        pageTexture = Printed("Printed Page", 256, 360, (x, y) =>
        {
            if (x < 26 || x > 230 || y < 28 || y > 332) { return 0f; }
            if (y < 44) { return x < 150 && y > 32 ? .85f : 0f; }
            int line = (y - 56) / 11; if (y < 56 || (y - 56) % 11 > 4) { return 0f; }
            int length = 150 + (line * 53 % 55); if (line % 6 == 5) { length = 60 + line * 17 % 70; }
            // 낱말 사이의 빈칸.
            return x - 26 < length && (x * 7 + line * 13) % 23 > 3 ? .7f : 0f;
        });
        return pageTexture;
    }
    private static Texture2D CalendarPage()
    {
        if (calendarTexture != null) { return calendarTexture; }
        calendarTexture = Printed("Calendar Page", 256, 176, (x, y) =>
        {
            if (y < 34) { return x > 14 && x < 70 && y > 8 && y < 28 ? .9f : .12f; }
            int column = (x - 12) / 33, row = (y - 40) / 26; if (x < 12 || column > 6 || row > 4 || y < 40) { return 0f; }
            int cx = (x - 12) % 33, cy = (y - 40) % 26;
            if (cx < 1 || cy < 1) { return .25f; }
            return cx > 4 && cx < 12 && cy > 4 && cy < 11 ? (column == 0 ? .45f : .8f) : 0f;
        });
        return calendarTexture;
    }
    private static Texture2D ReceiptPaper()
    {
        if (receiptTexture != null) { return receiptTexture; }
        receiptTexture = Printed("Receipt", 64, 256, (x, y) =>
        {
            if (x < 6 || x > 57 || y < 10 || y > 244) { return 0f; }
            if (y < 26) { return x > 14 && x < 50 ? .8f : 0f; }
            int line = (y - 34) / 9; if (y < 34 || (y - 34) % 9 > 3) { return 0f; }
            if (line % 7 == 6) { return .5f; }
            return x < 30 + line * 11 % 14 || x > 44 ? .65f : 0f;
        });
        return receiptTexture;
    }

    // ── 물건들 ───────────────────────────────────────────────────────────────
    /// <summary>머그컵. 속이 실제로 파여 있고 두께가 있다. 커피가 남아 있으면 검은 수면이, 비었으면 바닥에 말라붙은 자국이 보인다.</summary>
    private void Mug(Vector3 position, float turn, Color glaze, bool coffee)
    {
        var mug = Group("Model :: Mug", position, turn);
        const float R = .17f, H = .36f, wall = .02f;
        var body = Shape("mug", () => PropMesh.Lathe(new[]
        {
            P(0f, 0f), P(R - .035f, 0f), P(R - .012f, .006f), P(R - .002f, .02f), P(R, .04f), P(R, H - .02f), P(R - .002f, H - .008f), P(R - .007f, H), P(R - .013f, H), P(R - wall + .002f, H - .008f),
            P(R - wall, H - .02f), P(R - wall, .06f), P(R - wall - .012f, .034f), P(R - wall - .04f, .026f), P(0f, .026f)
        }, 72));
        var ceramic = Lit(glaze, .62f);
        Part("Mug Body", mug, body, ceramic, Vector3.zero);
        // 손잡이: 옆으로 휘어 나온 굵은 고리.
        var handle = Shape("mug handle", () => PropMesh.Tube(PropMesh.Smooth(new[]
        {
            new Vector3(R - .012f, H * .80f, 0f), new Vector3(R + .05f, H * .82f, 0f), new Vector3(R + .115f, H * .66f, 0f), new Vector3(R + .12f, H * .44f, 0f), new Vector3(R + .07f, H * .24f, 0f), new Vector3(R - .012f, H * .20f, 0f)
        }, 10), t => .021f + .005f * Mathf.Abs(t - .5f) * 2f, 18));
        Part("Mug Handle", mug, handle, ceramic, Vector3.zero);
        if (coffee)
        {
            // 식은 커피: 가장자리가 살짝 올라붙은 수면.
            var surface = Shape("coffee", () => PropMesh.Lathe(new[] { P(R - wall, H * .80f + .006f), P(R - wall - .012f, H * .80f + .001f), P(R - wall - .03f, H * .80f), P(0f, H * .80f) }, 64));
            Part("Cold Coffee", mug, surface, Lit(new Color(.035f, .018f, .008f), .93f), Vector3.zero);
            // 컵 안쪽에 남은 커피 띠.
            var ring = Shape("coffee ring", () => PropMesh.Lathe(new[] { P(R - wall - .001f, H * .86f), P(R - wall - .001f, H * .80f) }, 64));
            Part("Coffee Line", mug, ring, Lit(new Color(.10f, .055f, .025f), .3f), Vector3.zero);
        }
        else
        {
            Part("Dried Coffee", mug, Disc(R - wall - .05f), Lit(new Color(.11f, .06f, .03f), .12f), new Vector3(0f, .0275f, 0f));
        }
        // 컵 밑에 번진 얼룩과, 바깥으로 흘러내린 자국.
        Part("Stain", mug, Disc(R * 1.22f), Lit(new Color(.10f, .06f, .035f), .25f), new Vector3(.025f, .0012f, .018f));
        var drip = Shape("drip", () => PropMesh.Tube(new[] { new Vector3(0f, H - .012f, 0f), new Vector3(0f, H * .7f, 0f), new Vector3(0f, H * .42f, 0f), new Vector3(0f, H * .30f, 0f) }, t => .008f * (1f - t * .5f) + (t > .85f ? .005f : 0f), 10));
        Part("Drip", mug, drip, Lit(new Color(.11f, .06f, .03f), .35f), new Vector3(-(R + .002f) * .7071f, 0f, (R + .002f) * .7071f));
    }

    /// <summary>페트병: 비쳐 보이는 몸통, 굴곡진 허리, 어깨와 목, 홈이 파인 뚜껑, 띠지, 안에 남은 음료.</summary>
    private void Bottle(Vector3 position, Color drink, Color label, float fill, bool lying, float turn)
    {
        const float R = .095f, H = .52f;
        var bottle = Group("Model :: Plastic Bottle", position + (lying ? Vector3.up * R : Vector3.zero), turn);
        var holder = new GameObject("Bottle Body").transform; holder.SetParent(bottle, false);
        holder.localRotation = lying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
        var shell = Shape("pet bottle", () =>
        {
            var profile = new List<Vector2> { P(0f, .016f), P(R * .45f, .012f), P(R * .7f, 0f), P(R * .9f, .008f), P(R, .035f) };
            // 아래쪽의 가로 홈 네 줄.
            for (int i = 0; i < 4; i++) { float y = .07f + i * .038f; profile.Add(P(R, y)); profile.Add(P(R * .95f, y + .012f)); profile.Add(P(R, y + .024f)); }
            profile.AddRange(new[] { P(R, H * .44f), P(R * .96f, H * .47f), P(R * .96f, H * .68f), P(R, H * .70f), P(R * .97f, H * .76f), P(R * .86f, H * .82f), P(R * .66f, H * .88f), P(R * .46f, H * .925f), P(R * .37f, H * .945f), P(R * .37f, H * .975f), P(R * .42f, H * .98f), P(R * .42f, H * .99f), P(R * .34f, H), P(0f, H) });
            return PropMesh.Lathe(profile, 64);
        });
        Part("Plastic", holder, shell, Lit(new Color(.70f, .78f, .80f), .92f, 0f, .28f), Vector3.zero);
        if (fill > 0f)
        {
            float top = Mathf.Min(H * .66f, H * fill);
            var liquid = Shape("drink" + fill.ToString("0.00"), () => PropMesh.Lathe(new[] { P(0f, .02f), P(R * .88f, .02f), P(R * .93f, .045f), P(R * .93f, top), P(0f, top) }, 48));
            Part("Drink", holder, liquid, Lit(drink, .9f), Vector3.zero);
        }
        var band = Shape("bottle label", () => PropMesh.Lathe(new[] { P(R * .965f, H * .475f), P(R * .972f, H * .48f), P(R * .972f, H * .67f), P(R * .965f, H * .675f) }, 64));
        Part("Label", holder, band, Lit(label, .35f), Vector3.zero);
        var cap = Shape("bottle cap", () =>
        {
            // 손에 잡히는 세로 홈: 단면을 톱니처럼 흔들 수는 없으니 층층이 가는 띠로 낸다.
            var profile = new List<Vector2> { P(R * .44f, H * .985f) };
            for (int i = 0; i < 6; i++) { float y = H * .985f + i * .0075f; profile.Add(P(R * .47f, y + .002f)); profile.Add(P(R * .455f, y + .0055f)); }
            profile.AddRange(new[] { P(R * .47f, H + .034f), P(R * .44f, H + .042f), P(R * .36f, H + .045f), P(0f, H + .045f) });
            return PropMesh.Lathe(profile, 48);
        });
        Part("Cap", holder, cap, Lit(new Color(Mathf.Min(1f, label.r * 1.25f), Mathf.Min(1f, label.g * 1.25f), Mathf.Min(1f, label.b * 1.25f)), .55f), Vector3.zero);
    }

    /// <summary>로션 통: 어깨가 둥근 병에 눌러 쓰는 펌프가 달려 있다. 입구에 흘러나온 로션이 굳어 있다.</summary>
    private void Lotion(Vector3 position, float turn)
    {
        var lotion = Group("Model :: Lotion Bottle", position, turn);
        const float R = .115f, H = .40f;
        var body = Shape("lotion", () => PropMesh.Lathe(new[]
        {
            P(0f, 0f), P(R * .82f, 0f), P(R * .95f, .008f), P(R, .028f), P(R, H * .78f), P(R * .97f, H * .86f), P(R * .86f, H * .93f), P(R * .62f, H * .98f), P(R * .40f, H), P(R * .40f, H + .012f), P(0f, H + .012f)
        }, 64));
        Part("Bottle", lotion, body, Lit(new Color(.50f, .47f, .40f), .5f), Vector3.zero);
        var band = Shape("lotion label", () => PropMesh.Lathe(new[] { P(R * 1.004f, H * .22f), P(R * 1.008f, H * .225f), P(R * 1.008f, H * .66f), P(R * 1.004f, H * .665f) }, 64));
        Part("Label", lotion, band, Lit(new Color(.12f, .26f, .24f), .3f), Vector3.zero);
        var collar = Shape("lotion collar", () => PropMesh.Lathe(new[] { P(R * .44f, H + .008f), P(R * .46f, H + .014f), P(R * .46f, H + .05f), P(R * .42f, H + .058f), P(R * .16f, H + .058f), P(R * .16f, H + .12f), P(0f, H + .12f) }, 48));
        var white = Lit(new Color(.58f, .58f, .56f), .55f);
        Part("Collar", lotion, collar, white, Vector3.zero);
        Part("Pump Head", lotion, Shape("pump head", () => PropMesh.RoundedBox(new Vector3(.13f, .04f, .075f), .016f, 6)), white, new Vector3(.02f, H + .135f, 0f));
        var nozzle = Shape("pump nozzle", () => PropMesh.Tube(PropMesh.Smooth(new[] { new Vector3(.06f, H + .135f, 0f), new Vector3(.13f, H + .135f, 0f), new Vector3(.165f, H + .125f, 0f), new Vector3(.175f, H + .10f, 0f) }, 8), t => .014f - t * .003f, 14));
        Part("Nozzle", lotion, nozzle, white, Vector3.zero);
        Part("Dried Lotion", lotion, Shape("lotion blob", () => PropMesh.Crumple(3, 21, .18f, .1f, new Vector3(1f, 1.3f, 1f))), Lit(new Color(.60f, .58f, .50f), .25f), new Vector3(.176f, H + .085f, 0f), Quaternion.identity, Vector3.one * .014f);
    }

    /// <summary>빈 캔: 위아래가 오목하고 테두리가 말려 있다. 따개가 젖혀져 있다.</summary>
    private void Can(Vector3 position, Color paint, bool lying, float turn)
    {
        const float R = .085f, H = .31f;
        var can = Group("Model :: Empty Can", position + (lying ? Vector3.up * R : Vector3.zero), turn);
        var holder = new GameObject("Can Body").transform; holder.SetParent(can, false);
        holder.localRotation = lying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
        holder.localPosition = lying ? new Vector3(H * .5f, 0f, 0f) : Vector3.zero;
        var side = Shape("can side", () => PropMesh.Lathe(new[] { P(R * .80f, .004f), P(R * .93f, .010f), P(R, .034f), P(R, H - .040f), P(R * .93f, H - .014f), P(R * .90f, H - .004f) }, 64));
        Part("Can Side", holder, side, Lit(paint, .78f, .35f), Vector3.zero);
        var ends = Shape("can ends", () => PropMesh.Lathe(new[]
        {
            P(0f, .020f), P(R * .5f, .016f), P(R * .72f, .004f), P(R * .80f, .004f),
            P(R * .80f, .004f), P(R * .90f, H - .004f), P(R * .90f, H - .004f),
            P(R * .90f, H), P(R * .86f, H), P(R * .84f, H - .012f), P(R * .78f, H - .014f), P(0f, H - .014f)
        }, 64));
        var metal = Lit(new Color(.50f, .51f, .52f), .82f, .85f);
        // 옆면은 따로 칠했으므로, 이 메시에서는 위아래만 보이면 된다(옆 부분은 칠한 면 안쪽에 숨는다).
        Part("Can Ends", holder, ends, metal, Vector3.zero, Quaternion.identity, new Vector3(.995f, 1f, .995f));
        Part("Opening", holder, Disc(R * .30f), Lit(new Color(.01f, .01f, .01f), .2f), new Vector3(R * .34f, H - .0135f, 0f), Quaternion.identity, new Vector3(1f, 1f, .72f));
        Part("Pull Tab", holder, Shape("pull tab", () => PropMesh.RoundedBox(new Vector3(.062f, .004f, .034f), .0018f, 3)), metal, new Vector3(-R * .12f, H - .008f, 0f), Quaternion.Euler(0f, 0f, 14f), Vector3.one);
    }

    /// <summary>먹다 만 컵라면: 위로 벌어진 컵, 남은 국물과 면발, 반쯤 젖힌 은박 뚜껑, 걸쳐 놓은 젓가락.</summary>
    private void Noodles(Vector3 position)
    {
        var cup = Group("Model :: Cup Noodles", position, 18f);
        const float Rb = .15f, Rt = .215f, H = .34f, wall = .012f;
        var shell = Shape("noodle cup", () => PropMesh.Lathe(new[]
        {
            P(0f, .01f), P(Rb - .02f, .01f), P(Rb - .02f, 0f), P(Rb, 0f), P(Rb + .004f, .012f), P(Rt - .006f, H - .02f), P(Rt + .010f, H - .012f), P(Rt + .014f, H - .002f), P(Rt + .006f, H + .004f), P(Rt - .004f, H),
            P(Rt - wall, H - .014f), P(Rb + .01f, H * .34f), P(0f, H * .34f)
        }, 72));
        Part("Cup", cup, shell, Lit(new Color(.56f, .55f, .52f), .3f), Vector3.zero);
        var band = Shape("noodle band", () => PropMesh.Lathe(new[] { P(Mathf.Lerp(Rb, Rt, .30f) + .0045f, H * .30f), P(Mathf.Lerp(Rb, Rt, .62f) + .0025f, H * .62f) }, 72));
        Part("Band", cup, band, Lit(new Color(.42f, .06f, .04f), .35f), Vector3.zero);
        float level = H * .52f, radius = Mathf.Lerp(Rb, Rt, .52f) - wall;
        Part("Broth", cup, Disc(radius), Lit(new Color(.22f, .09f, .02f), .9f), new Vector3(0f, level, 0f));
        // 불어 터진 면발 몇 가닥.
        var noodle = Lit(new Color(.50f, .40f, .20f), .45f);
        for (int i = 0; i < 7; i++)
        {
            int seed = i; var strand = Shape("noodle" + i, () =>
            {
                var random = new System.Random(900 + seed); var points = new List<Vector3>();
                float angle = (float)random.NextDouble() * 6.28f, reach = radius * (.35f + (float)random.NextDouble() * .5f);
                for (int k = 0; k < 6; k++)
                {
                    angle += 1.1f + (float)random.NextDouble() * 1.4f; float r = reach * (.4f + (float)random.NextDouble() * .6f);
                    points.Add(new Vector3(Mathf.Cos(angle) * r, level + .004f + (float)random.NextDouble() * .012f, Mathf.Sin(angle) * r));
                }
                return PropMesh.Tube(PropMesh.Smooth(points, 8), _ => .0075f, 8);
            });
            Part("Noodle " + i, cup, strand, noodle, Vector3.zero);
        }
        // 은박 뚜껑: 한쪽만 붙은 채 뒤로 말려 넘어가 있다.
        var lid = Shape("noodle lid", () => PropMesh.Sheet(Rt * 2.05f, Rt * 2.05f, 28, 28, (u, v) =>
        {
            float dx = u - .5f, dz = v - .5f;
            // 둥글게 보이도록 모서리 쪽은 아래로 접어 숨긴다.
            float outside = Mathf.Max(0f, Mathf.Sqrt(dx * dx + dz * dz) - .5f);
            return Mathf.Pow(u, 2.2f) * .30f - outside * 2.2f + Mathf.Sin(v * 9f + u * 5f) * .004f;
        }));
        Part("Lid", cup, lid, Lit(new Color(.62f, .62f, .63f), .78f, .9f), new Vector3(-Rt * 1.05f, H * .78f, 0f), Quaternion.Euler(0f, 0f, 118f), Vector3.one);
        var stick = Shape("chopstick", () => PropMesh.Tube(new[] { new Vector3(0f, 0f, -.36f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, .36f) }, t => .0115f - t * .005f, 10));
        var wood = Lit(new Color(.46f, .34f, .18f), .25f);
        Part("Chopstick A", cup, stick, wood, new Vector3(.02f, H + .016f, .02f), Quaternion.Euler(-4f, 24f, 0f), Vector3.one);
        Part("Chopstick B", cup, stick, wood, new Vector3(-.01f, H + .016f, -.03f), Quaternion.Euler(-3f, 36f, 0f), Vector3.one);
    }

    /// <summary>책 한 권: 앞뒤 표지와 책등, 그 사이에 살짝 들어간 종이 묶음.</summary>
    private static void Book(Transform parent, Vector3 position, float turn, float wide, float thick, float deep, Color cover)
    {
        var book = new GameObject("Book").transform; book.SetParent(parent, false);
        book.localPosition = position; book.localRotation = Quaternion.Euler(0f, turn, 0f);
        const float board = .011f, inset = .014f;
        var skin = Lit(cover, .35f); var pages = Lit(new Color(.55f, .53f, .46f), .12f);
        Part("Back Cover", book, PropMesh.RoundedBox(new Vector3(wide, board, deep), .004f, 3), skin, new Vector3(0f, board * .5f, 0f));
        Part("Front Cover", book, PropMesh.RoundedBox(new Vector3(wide, board, deep), .004f, 3), skin, new Vector3(0f, thick - board * .5f, 0f));
        Part("Spine", book, PropMesh.RoundedBox(new Vector3(board * 1.6f, thick, deep), .006f, 4), skin, new Vector3(-wide * .5f + board * .8f, thick * .5f, 0f));
        Part("Pages", book, PropMesh.RoundedBox(new Vector3(wide - inset - board, thick - board * 2f, deep - inset * 2f), .003f, 2), pages, new Vector3(board * .5f - inset * .5f, thick * .5f, 0f));
        // 표지의 제목 띠.
        Part("Title Band", book, PropMesh.RoundedBox(new Vector3(wide * .5f, .002f, deep * .16f), .001f, 2), Lit(new Color(.62f, .58f, .46f), .3f), new Vector3(.02f, thick + .0008f, deep * .16f));
    }
    private void Books(Vector3 position, float turn, int count, int seed)
    {
        var stack = Group("Model :: Book Stack", position, turn);
        Color[] covers = { new Color(.30f, .08f, .07f), new Color(.07f, .15f, .30f), new Color(.10f, .24f, .14f), new Color(.34f, .27f, .08f), new Color(.20f, .20f, .23f) };
        float y = 0f;
        for (int i = 0; i < count; i++)
        {
            float thick = .09f + (i + seed) * 37 % 5 * .02f, wide = .92f - (i + seed) * 13 % 4 * .07f, deep = .66f - (i + seed) * 7 % 3 * .05f;
            Book(stack, new Vector3((i * 17 % 5 - 2) * .025f, y, (i * 11 % 3 - 1) * .03f), (i * 23 % 9 - 4) * 3.5f, wide, thick, deep, covers[(i + seed) % covers.Length]);
            y += thick;
        }
    }

    /// <summary>스프링 공책: 두꺼운 표지, 종이 묶음, 한쪽을 따라 감긴 철사 고리.</summary>
    private void Notebook(Vector3 position, float turn)
    {
        var note = Group("Model :: Spiral Notebook", position, turn);
        const float wide = .70f, deep = .96f, thick = .05f;
        Part("Pages", note, Shape("notebook pages", () => PropMesh.RoundedBox(new Vector3(wide - .02f, thick - .012f, deep - .02f), .003f, 2)), Lit(new Color(.56f, .55f, .50f), .1f), new Vector3(.006f, thick * .5f, 0f));
        Part("Back", note, Shape("notebook board", () => PropMesh.RoundedBox(new Vector3(wide, .006f, deep), .0025f, 3)), Lit(new Color(.16f, .14f, .12f), .25f), new Vector3(0f, .003f, 0f));
        // 표지는 살짝 들떠 있다.
        Part("Cover", note, Shape("notebook board", () => PropMesh.RoundedBox(new Vector3(wide, .006f, deep), .0025f, 3)), Lit(new Color(.10f, .17f, .26f), .4f), new Vector3(0f, thick + .004f, 0f), Quaternion.Euler(0f, 0f, 1.6f), Vector3.one);
        Part("Label", note, Shape("notebook label", () => PropMesh.RoundedBox(new Vector3(wide * .52f, .002f, deep * .20f), .001f, 2)), Lit(new Color(.58f, .56f, .50f), .2f), new Vector3(.04f, thick + .0085f, deep * .18f), Quaternion.Euler(0f, 0f, 1.6f), Vector3.one);
        var wire = Lit(new Color(.40f, .41f, .42f), .8f, .9f);
        var ring = Shape("notebook ring", () => PropMesh.Ring(.036f, .0042f, 28, 8));
        for (int i = 0; i < 22; i++)
        { Part("Ring " + i, note, ring, wire, new Vector3(-wide * .5f + .012f, thick * .5f + .002f, -deep * .5f + .04f + i * (deep - .08f) / 21f), Quaternion.Euler(0f, 6f, 0f), Vector3.one); }
    }

    /// <summary>서류 더미: 한 장 한 장이 비뚤게 놓여 있고, 위쪽 종이는 모서리가 들려 있다.</summary>
    private void Papers(Vector3 position, float turn)
    {
        var stack = Group("Model :: Paper Stack", position, turn);
        float[] turns = { 0f, 7f, -5f, 13f, -9f, 4f, -17f, 9f, -3f };
        for (int i = 0; i < turns.Length; i++)
        {
            int index = i; float curl = i >= turns.Length - 3 ? .05f + (i % 3) * .03f : .006f;
            var sheet = Shape("paper sheet " + (i >= turns.Length - 3 ? i : 0), () => PropMesh.Sheet(.84f, 1.18f, 14, 20, (u, v) =>
            {
                float corner = Mathf.Max(0f, (index % 2 == 0 ? u : 1f - u) + v - 1.45f);
                return corner * corner * curl * 6f + Mathf.Sin(u * 5f + index) * Mathf.Sin(v * 4f + index * 2f) * .0015f;
            }));
            float shade = .50f + (i * 37 % 9) * .012f;
            Part("Sheet " + i, stack, sheet, Lit(new Color(shade * 1.03f, shade, shade * .92f), .1f, 0f, 1f, Page()), new Vector3((i * 13 % 7 - 3) * .02f, .003f + i * .0045f, (i * 29 % 5 - 2) * .025f), Quaternion.Euler(0f, turns[i], 0f), Vector3.one);
        }
    }

    /// <summary>구겨 던진 종이: 날카롭게 접힌 자국이 있는 덩어리.</summary>
    private void Ball(Vector3 position, float size, int seed)
    {
        var ball = Group("Model :: Crumpled Paper", position + Vector3.up * size * .44f, seed * 71f);
        var mesh = Shape("crumpled" + seed, () => PropMesh.Crumple(5, seed * 7 + 3, .30f, .9f, new Vector3(1f, .86f, .94f)));
        Part("Paper", ball, mesh, Lit(new Color(.52f, .51f, .47f), .06f), Vector3.zero, Quaternion.Euler(seed * 23f, seed * 51f, seed * 41f), Vector3.one * size * .5f);
    }

    /// <summary>쓰고 던져 둔 휴지: 납작하고 뭉실한 덩어리.</summary>
    private void Tissue(Vector3 position, int seed)
    {
        var tissue = Group("Model :: Used Tissue", position + Vector3.up * .038f, seed * 53f);
        var mesh = Shape("tissue" + seed, () => PropMesh.Crumple(5, seed * 11 + 40, .34f, .35f, new Vector3(1f, .42f, .82f)));
        Part("Wad", tissue, mesh, Lit(new Color(.62f, .62f, .60f), .03f), Vector3.zero, Quaternion.Euler(0f, 0f, seed * 4f - 6f), Vector3.one * .115f);
    }

    private void TissueBox(Vector3 position, float turn)
    {
        var box = Group("Model :: Tissue Box", position, turn);
        Part("Box", box, Shape("tissue box", () => PropMesh.RoundedBox(new Vector3(.50f, .15f, .26f), .010f, 4)), Lit(new Color(.12f, .26f, .32f), .3f), new Vector3(0f, .075f, 0f));
        Part("Pattern", box, Shape("tissue box band", () => PropMesh.RoundedBox(new Vector3(.502f, .05f, .262f), .010f, 4)), Lit(new Color(.50f, .50f, .47f), .3f), new Vector3(0f, .05f, 0f));
        Part("Slot", box, Disc(.5f), Lit(new Color(.02f, .02f, .02f), .1f), new Vector3(0f, .1506f, 0f), Quaternion.identity, new Vector3(.26f, 1f, .075f));
        // 뽑다 만 휴지 한 장: 구멍에서 솟아 접혀 있다.
        var tuft = Shape("tissue tuft", () => PropMesh.Crumple(4, 77, .30f, .5f, new Vector3(.85f, 1.25f, .22f)));
        Part("Tissue", box, tuft, Lit(new Color(.64f, .64f, .62f), .03f), new Vector3(.01f, .215f, 0f), Quaternion.Euler(0f, 14f, 9f), Vector3.one * .075f);
    }

    /// <summary>볼펜: 금속 촉, 고무 손잡이, 몸통, 누름 단추, 옆의 클립.</summary>
    private static Transform PenShape(Transform parent, Color ink, Vector3 position, Quaternion rotation, float length)
    {
        var pen = new GameObject("Pen").transform; pen.SetParent(parent, false); pen.localPosition = position; pen.localRotation = rotation;
        var tip = Shape("pen tip", () => PropMesh.Lathe(new[] { P(0f, 0f), P(.0035f, .004f), P(.008f, .03f), P(.012f, .045f) }, 20));
        Part("Tip", pen, tip, Lit(new Color(.48f, .48f, .50f), .8f, .9f), Vector3.zero);
        var grip = Shape("pen grip", () =>
        {
            var profile = new List<Vector2> { P(.012f, .045f), P(.0165f, .052f) };
            for (int i = 0; i < 6; i++) { float y = .058f + i * .014f; profile.Add(P(.0175f, y)); profile.Add(P(.0162f, y + .007f)); }
            profile.Add(P(.0175f, .145f)); profile.Add(P(.016f, .15f));
            return PropMesh.Lathe(profile, 24);
        });
        Part("Grip", pen, grip, Lit(new Color(.06f, .06f, .07f), .25f), Vector3.zero);
        var barrel = Shape("pen barrel" + length.ToString("0.00"), () => PropMesh.Lathe(new[] { P(.016f, .15f), P(.016f, length - .05f), P(.0165f, length - .045f), P(.0165f, length - .02f), P(.012f, length - .016f), P(.012f, length), P(.009f, length + .004f), P(0f, length + .004f) }, 24));
        Part("Barrel", pen, barrel, Lit(ink, .65f), Vector3.zero);
        Part("Clip", pen, Shape("pen clip", () => PropMesh.RoundedBox(new Vector3(.007f, .13f, .012f), .003f, 3)), Lit(new Color(.48f, .48f, .50f), .8f, .9f), new Vector3(.021f, length - .095f, 0f));
        return pen;
    }
    private void Pen(Vector3 position, float turn, Color ink)
    {
        var pen = Group("Model :: Pen", position + Vector3.up * .0175f, turn);
        PenShape(pen, ink, new Vector3(-.26f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f), .52f);
    }

    /// <summary>필통으로 쓰는 깡통: 테두리가 말린 얇은 통에 펜과 자가 삐죽삐죽 꽂혀 있다.</summary>
    private void PenCup(Vector3 position)
    {
        var cup = Group("Model :: Pen Cup", position, 0f);
        const float R = .15f, H = .34f;
        var tin = Shape("pen tin", () =>
        {
            var profile = new List<Vector2> { P(0f, 0f), P(R - .01f, 0f), P(R, .01f) };
            for (int i = 0; i < 5; i++) { float y = .05f + i * .05f; profile.Add(P(R, y)); profile.Add(P(R - .004f, y + .008f)); profile.Add(P(R, y + .016f)); }
            profile.AddRange(new[] { P(R, H - .012f), P(R + .006f, H - .004f), P(R + .004f, H + .002f), P(R - .004f, H), P(R - .006f, H - .012f), P(R - .006f, .012f), P(0f, .012f) });
            return PropMesh.Lathe(profile, 64);
        });
        Part("Tin", cup, tin, Lit(new Color(.10f, .28f, .32f), .7f, .6f), Vector3.zero);
        Color[] inks = { new Color(.05f, .08f, .24f), new Color(.34f, .05f, .05f), new Color(.40f, .34f, .06f), new Color(.05f, .05f, .05f), new Color(.06f, .26f, .12f), new Color(.40f, .40f, .40f) };
        for (int i = 0; i < inks.Length; i++)
        {
            float angle = i * 61f * Mathf.Deg2Rad, lean = 8f + i * 13 % 9;
            PenShape(cup, inks[i], new Vector3(Mathf.Cos(angle) * .07f, .014f, Mathf.Sin(angle) * .07f), Quaternion.Euler(lean * Mathf.Sin(angle), i * 40f, -lean * Mathf.Cos(angle)), .46f + i % 3 * .03f);
        }
        Part("Ruler", cup, Shape("ruler", () => PropMesh.RoundedBox(new Vector3(.075f, .56f, .006f), .002f, 2)), Lit(new Color(.55f, .50f, .30f), .5f, 0f, .6f), new Vector3(-.03f, .29f, .06f), Quaternion.Euler(9f, 30f, 7f), Vector3.one);
    }

    /// <summary>탁상 달력: 두꺼운 받침 두 장이 세모로 서 있고, 위에 철사 고리로 종이가 걸려 있다. 넘기다 만 지난달 그대로다.</summary>
    private void Calendar(Vector3 position, float turn)
    {
        var item = Group("Model :: Desk Calendar", position, turn);
        var board = Shape("calendar board", () => PropMesh.RoundedBox(new Vector3(.62f, .42f, .012f), .004f, 3));
        var card = Lit(new Color(.34f, .10f, .05f), .3f);
        var front = Quaternion.Euler(-24f, 0f, 0f); var back = Quaternion.Euler(24f, 0f, 0f);
        Part("Front Board", item, board, card, new Vector3(0f, .19f, -.085f), front, Vector3.one);
        Part("Back Board", item, board, card, new Vector3(0f, .19f, .085f), back, Vector3.one);
        Part("Base", item, Shape("calendar base", () => PropMesh.RoundedBox(new Vector3(.62f, .008f, .36f), .003f, 2)), card, new Vector3(0f, .004f, 0f));
        // 달력 종이: 앞판 위에 몇 장이 겹쳐 있고 아래쪽이 살짝 들떠 있다.
        var page = Shape("calendar page", () => PropMesh.Sheet(.58f, .385f, 12, 10, (u, v) => (1f - v) * (1f - v) * .012f));
        for (int i = 0; i < 3; i++)
        { Part("Page " + i, item, page, Lit(new Color(.62f, .61f, .57f), .15f, 0f, 1f, CalendarPage()), front * new Vector3(0f, -.005f, -.0075f - i * .0025f) + new Vector3(0f, .19f, -.085f), front * Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, 0f, 0f), Vector3.one); }
        var ring = Shape("calendar ring", () => PropMesh.Ring(.022f, .003f, 24, 8)); var wire = Lit(new Color(.42f, .42f, .44f), .8f, .9f);
        for (int i = 0; i < 16; i++) { Part("Ring " + i, item, ring, wire, new Vector3(-.28f + i * .0373f, .385f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one); }
    }

    /// <summary>엉킨 이어폰: 한 줄이 여러 번 꼬여 있고 양 끝에 이어폰 머리가 달려 있다.</summary>
    private void Cord(Vector3 position, float turn)
    {
        var cord = Group("Model :: Tangled Earphones", position, turn);
        var black = Lit(new Color(.025f, .025f, .028f), .55f);
        var wire = Shape("earphone cord", () =>
        {
            var random = new System.Random(311); var points = new List<Vector3>(); float angle = 0f;
            for (int i = 0; i < 26; i++)
            {
                angle += 1.2f + (float)random.NextDouble() * 1.9f; float r = .05f + (float)random.NextDouble() * .16f;
                // 겹치는 자리는 조금씩 높이가 달라 서로 올라탄다.
                points.Add(new Vector3(Mathf.Cos(angle) * r, .008f + (i % 3) * .009f + (float)random.NextDouble() * .004f, Mathf.Sin(angle) * r * .8f));
            }
            return PropMesh.Tube(PropMesh.Smooth(points, 10), _ => .0065f, 10);
        });
        Part("Cord", cord, wire, black, Vector3.zero);
        var bud = Shape("earbud", () => PropMesh.Lathe(new[] { P(0f, 0f), P(.010f, .002f), P(.018f, .012f), P(.020f, .026f), P(.016f, .038f), P(.008f, .046f), P(.006f, .075f), P(0f, .078f) }, 24));
        Part("Bud A", cord, bud, black, new Vector3(.20f, .02f, .07f), Quaternion.Euler(80f, 20f, 0f), Vector3.one);
        Part("Bud B", cord, bud, black, new Vector3(-.17f, .02f, -.10f), Quaternion.Euler(95f, 200f, 0f), Vector3.one);
        Part("Plug", cord, Shape("earphone plug", () => PropMesh.Lathe(new[] { P(0f, 0f), P(.004f, .002f), P(.004f, .03f), P(.008f, .034f), P(.008f, .07f), P(0f, .072f) }, 16)), Lit(new Color(.45f, .45f, .46f), .8f, .9f), new Vector3(.03f, .012f, -.19f), Quaternion.Euler(90f, 70f, 0f), Vector3.one);
    }

    /// <summary>리모컨: 모서리가 둥근 몸통에 고무 단추가 줄지어 있다.</summary>
    private void Remote(Vector3 position, float turn)
    {
        var remote = Group("Model :: Remote", position, turn);
        Part("Body", remote, Shape("remote body", () => PropMesh.RoundedBox(new Vector3(.54f, .05f, .15f), .022f, 7)), Lit(new Color(.035f, .035f, .04f), .5f), new Vector3(0f, .025f, 0f));
        var key = Lit(new Color(.17f, .17f, .18f), .3f); var button = Shape("remote key", () => PropMesh.RoundedBox(new Vector3(.046f, .012f, .026f), .005f, 4));
        for (int i = 0; i < 15; i++) { Part("Key " + i, remote, button, key, new Vector3(-.17f + i / 3 * .062f, .052f, (i % 3 - 1) * .04f)); }
        var round = Shape("remote dial", () => PropMesh.Lathe(new[] { P(.034f, 0f), P(.034f, .006f), P(.028f, .01f), P(0f, .01f) }, 32));
        Part("Dial", remote, round, key, new Vector3(-.225f, .048f, 0f));
        Part("Power", remote, Shape("remote power", () => PropMesh.Lathe(new[] { P(.016f, 0f), P(.016f, .006f), P(.012f, .009f), P(0f, .009f) }, 24)), Lit(new Color(.42f, .05f, .04f), .4f), new Vector3(.225f, .049f, .04f));
    }

    /// <summary>작은 종이 상자(약 상자 같은 것): 띠가 둘려 있고 뚜껑 날개가 열려 있다.</summary>
    private void SmallBox(Vector3 position, float turn, Color paper, Color band)
    {
        var box = Group("Model :: Small Box", position, turn);
        Part("Box", box, Shape("small box", () => PropMesh.RoundedBox(new Vector3(.34f, .12f, .20f), .005f, 3)), Lit(paper, .2f), new Vector3(0f, .06f, 0f));
        Part("Band", box, Shape("small box band", () => PropMesh.RoundedBox(new Vector3(.10f, .1215f, .2015f), .005f, 3)), Lit(band, .3f), new Vector3(-.08f, .06f, 0f));
        var flap = Shape("small box flap", () => PropMesh.Sheet(.10f, .198f, 4, 6, (u, v) => u * u * .012f));
        Part("Open Flap", box, flap, Lit(paper, .2f), new Vector3(.205f, .145f, 0f), Quaternion.Euler(0f, 0f, 42f), Vector3.one);
        // 열린 쪽 안이 어둡게 들여다보인다.
        Part("Mouth", box, Shape("small box mouth", () => PropMesh.RoundedBox(new Vector3(.003f, .100f, .180f), .001f, 1)), Lit(new Color(.05f, .05f, .05f), .1f), new Vector3(.1702f, .06f, 0f));
        // 상자에서 꺼내 책상에 놓아 둔 알약 판: 은박 판 위에 투명한 볼록 칸이 두 줄로 있고, 칸마다 알약이 들어 있다. 몇 칸은 이미 눌러 빼 먹었다.
        var foil = Part("Blister", box, Shape("blister", () => PropMesh.RoundedBox(new Vector3(.20f, .004f, .13f), .0015f, 2)), Lit(new Color(.62f, .63f, .65f), .75f, .9f), new Vector3(.305f, .002f, .025f), Quaternion.Euler(0f, 17f, 0f), Vector3.one);
        var dome = Shape("blister dome", () => PropMesh.Lathe(Arc(.0150f, .0110f, 0f, 8), 24));
        var pill = Shape("blister pill", () => PropMesh.Lathe(Arc(.0115f, .0080f, 0f, 6), 24));
        var hole = Shape("blister hole", () => PropMesh.Lathe(new[] { P(.0150f, 0f), P(.0125f, .0006f), P(0f, .0002f) }, 24));
        var clear = Lit(new Color(.86f, .90f, .92f), .9f, 0f, .30f); var white = Lit(new Color(.88f, .86f, .80f), .35f); var torn = Lit(new Color(.30f, .31f, .33f), .5f, .6f);
        var oblong = new Vector3(1f, 1f, 1.45f);
        for (int i = 0; i < 8; i++)
        {
            var spot = new Vector3(-.066f + i / 2 * .044f, .002f, -.031f + i % 2 * .062f);
            // 눌러 뺀 칸은 은박이 찢겨 움푹하다.
            if (i == 0 || i == 1 || i == 5) { Part("Empty Pocket " + i, foil, hole, torn, spot, Quaternion.identity, oblong); continue; }
            Part("Pill " + i, foil, pill, white, spot, Quaternion.identity, oblong);
            Part("Pocket " + i, foil, dome, clear, spot, Quaternion.identity, oblong);
        }
        // 빼 놓고 안 먹은 알약 두 알: 위아래가 볼록한 납작한 알.
        var loose = Shape("loose pill", () => PropMesh.Lathe(Arc(.0125f, .0045f, -90f, 12), 24));
        Part("Loose Pill 0", box, loose, white, new Vector3(.215f, 0f, -.135f), Quaternion.identity, oblong);
        Part("Loose Pill 1", box, loose, white, new Vector3(.262f, 0f, -.118f), Quaternion.Euler(0f, 64f, 0f), oblong);
    }

    /// <summary>영수증: 길고 얇은 감열지가 끝이 말려 있다.</summary>
    private void Receipt(Vector3 position, float turn)
    {
        var receipt = Group("Model :: Receipt", position, turn);
        var mesh = Shape("receipt", () => PropMesh.Sheet(.16f, .60f, 4, 48, (u, v) =>
        {
            float curl = Mathf.Max(0f, v - .78f) / .22f, start = Mathf.Max(0f, .12f - v) / .12f;
            return curl * curl * .06f + start * start * .018f + Mathf.Sin(v * 19f) * .0012f;
        }));
        Part("Strip", receipt, mesh, Lit(new Color(.60f, .60f, .57f), .08f, 0f, 1f, ReceiptPaper()), new Vector3(0f, .003f, 0f));
    }

    /// <summary>뭔가 담긴 채 묶어 둔 비닐봉지: 울퉁불퉁한 덩어리와 묶은 매듭.</summary>
    private void Bag(Vector3 position)
    {
        var bag = Group("Model :: Plastic Bag", position, 17f);
        var film = Lit(new Color(.52f, .53f, .53f), .82f);
        Part("Bag", bag, Shape("plastic bag", () => PropMesh.Crumple(5, 9, .22f, .55f, new Vector3(1f, .58f, .84f))), film, new Vector3(0f, .14f, 0f), Quaternion.identity, Vector3.one * .27f);
        Part("Knot", bag, Shape("bag knot", () => PropMesh.Crumple(4, 14, .35f, .7f, new Vector3(.7f, 1.3f, .7f))), film, new Vector3(.02f, .31f, .01f), Quaternion.Euler(8f, 0f, -12f), Vector3.one * .05f);
        var ear = Shape("bag ear", () => PropMesh.Sheet(.07f, .16f, 4, 8, (u, v) => Mathf.Sin(v * 3f) * .02f + (u - .5f) * (u - .5f) * .06f));
        Part("Handle A", bag, ear, film, new Vector3(-.02f, .40f, .03f), Quaternion.Euler(70f, 20f, 24f), Vector3.one);
        Part("Handle B", bag, ear, film, new Vector3(.06f, .39f, -.02f), Quaternion.Euler(70f, -40f, -28f), Vector3.one);
    }

    /// <summary>과자 봉지: 구겨진 은박. 뜯은 쪽이 벌어져 있다.</summary>
    private void Wrapper(Vector3 position, float turn)
    {
        var bag = Group("Model :: Snack Wrapper", position, turn);
        var foil = Shape("snack foil", () => PropMesh.Sheet(.52f, .34f, 36, 24, (u, v) =>
        {
            float crinkle = Mathf.Abs(Mathf.PerlinNoise(u * 9f + 3f, v * 7f + 1f) - .5f) * .05f + Mathf.Abs(Mathf.PerlinNoise(u * 21f, v * 17f + 8f) - .5f) * .016f;
            float puff = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI) * .035f;
            return .004f + puff + crinkle + Mathf.Max(0f, u - .86f) * .25f;
        }));
        Part("Foil", bag, foil, Lit(new Color(.50f, .30f, .05f), .85f, .7f), Vector3.zero);
    }
}
