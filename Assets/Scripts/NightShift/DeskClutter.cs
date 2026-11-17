using UnityEngine;

// 책상 위의 어질러진 물건들. 기능은 없고, 며칠째 치우지 않은 자리처럼 보이게 한다.
// 식은 커피가 담긴 머그컵, 말라붙은 빈 컵, 아무렇게나 쌓인 서류, 구겨 던진 종이, 빈 캔, 먹다 만 컵라면, 굴러다니는 펜.
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
        || name.Contains("Small Box") || name.Contains("Cord") || name.Contains("Can") || name.Contains("Wrapper");
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

    private static Material Lit(Color color, float smoothness)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader); material.SetColor("_BaseColor", color); material.color = color; material.SetFloat("_Smoothness", smoothness);
        return material;
    }
    private Transform Group(string name, Vector3 position, float turn)
    {
        var group = new GameObject(name).transform; group.SetParent(root, false);
        group.localPosition = position; group.localRotation = Quaternion.Euler(0f, turn, 0f);
        return group;
    }
    private static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        var item = GameObject.CreatePrimitive(type); item.name = name;
        Destroy(item.GetComponent<Collider>());
        item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localRotation = rotation; item.transform.localScale = scale;
        item.GetComponent<Renderer>().sharedMaterial = material;
        return item.transform;
    }
    /// <summary>원기둥은 높이 2, 지름 1이 기본이다. 반지름과 높이로 크기를 준다.</summary>
    private static Vector3 Tube(float radius, float height) => new Vector3(radius * 2f, height * .5f, radius * 2f);

    /// <summary>머그컵. 커피가 남아 있으면 검은 수면이 보이고, 비었으면 바닥에 말라붙은 자국만 있다. 컵 밖으로 흘러내린 줄도 있다.</summary>
    private void Mug(Vector3 position, float turn, Color glaze, bool coffee)
    {
        var mug = Group("Model :: Mug", position, turn);
        var body = Lit(glaze, .55f); var inside = Lit(new Color(.42f, .41f, .38f), .35f);
        var liquid = Lit(new Color(.045f, .025f, .012f), .9f); var crust = Lit(new Color(.12f, .07f, .035f), .15f);
        const float radius = .17f, height = .36f;
        Shape(PrimitiveType.Cylinder, "Mug Body", mug, new Vector3(0f, height * .5f, 0f), Tube(radius, height), Quaternion.identity, body);
        // 입구 안쪽: 몸통보다 조금 좁은 원판을 위에 얹어서 속이 파인 것처럼 보이게 한다.
        Shape(PrimitiveType.Cylinder, "Mug Rim", mug, new Vector3(0f, height + .001f, 0f), Tube(radius * .88f, .004f), Quaternion.identity, inside);
        Shape(PrimitiveType.Cylinder, coffee ? "Cold Coffee" : "Dried Coffee", mug, new Vector3(0f, height + .003f, 0f), Tube(radius * (coffee ? .82f : .60f), .004f), Quaternion.identity, coffee ? liquid : crust);
        // 손잡이: 네모진 고리.
        Shape(PrimitiveType.Cube, "Handle Top", mug, new Vector3(radius + .05f, height * .72f, 0f), new Vector3(.12f, .04f, .05f), Quaternion.identity, body);
        Shape(PrimitiveType.Cube, "Handle Bottom", mug, new Vector3(radius + .05f, height * .30f, 0f), new Vector3(.12f, .04f, .05f), Quaternion.identity, body);
        Shape(PrimitiveType.Cube, "Handle Side", mug, new Vector3(radius + .11f, height * .51f, 0f), new Vector3(.04f, height * .46f, .05f), Quaternion.identity, body);
        // 흘러내린 자국과, 컵 밑에 번진 얼룩.
        Shape(PrimitiveType.Cube, "Drip", mug, new Vector3(-radius * .72f, height * .62f, radius * .72f), new Vector3(.025f, height * .7f, .012f), Quaternion.Euler(0f, 45f, 0f), crust);
        Shape(PrimitiveType.Cylinder, "Stain", mug, new Vector3(.03f, .002f, .02f), Tube(radius * 1.25f, .002f), Quaternion.identity, crust);
    }

    /// <summary>서류 더미. 한 장씩 비뚤게 쌓여 있다.</summary>
    private void Papers(Vector3 position, float turn)
    {
        var stack = Group("Model :: Paper Stack", position, turn);
        float[] turns = { 0f, 7f, -5f, 13f, -9f, 4f, -17f };
        for (int i = 0; i < turns.Length; i++)
        {
            float shade = .30f + (i * 37 % 9) * .012f;
            var sheet = Lit(new Color(shade * 1.04f, shade, shade * .90f), .1f);
            Shape(PrimitiveType.Cube, "Sheet " + i, stack, new Vector3((i * 13 % 7 - 3) * .02f, .006f + i * .010f, (i * 29 % 5 - 2) * .025f), new Vector3(.84f, .008f, 1.18f), Quaternion.Euler(0f, turns[i], 0f), sheet);
        }
    }

    /// <summary>구겨 던진 종이. 찌그러진 덩어리 몇 개를 겹쳐서 구김을 낸다.</summary>
    private void Ball(Vector3 position, float size, int seed)
    {
        var ball = Group("Model :: Crumpled Paper", position + Vector3.up * size * .42f, seed * 71f);
        var paper = Lit(new Color(.33f, .32f, .29f), .05f); var fold = Lit(new Color(.24f, .23f, .21f), .05f);
        Shape(PrimitiveType.Sphere, "Lump", ball, Vector3.zero, new Vector3(size, size * .84f, size * .92f), Quaternion.Euler(seed * 23f, 0f, seed * 41f), paper);
        Shape(PrimitiveType.Cube, "Fold A", ball, new Vector3(size * .12f, size * .08f, 0f), Vector3.one * size * .62f, Quaternion.Euler(31f + seed * 17f, 44f, 12f), fold);
        Shape(PrimitiveType.Cube, "Fold B", ball, new Vector3(-size * .10f, -size * .02f, size * .08f), Vector3.one * size * .58f, Quaternion.Euler(-22f, 15f + seed * 29f, 51f), paper);
    }

    /// <summary>빈 캔. 서 있거나 쓰러져 있다.</summary>
    private void Can(Vector3 position, Color paint, bool lying, float turn)
    {
        const float radius = .085f, height = .31f;
        var can = Group("Model :: Empty Can", position + (lying ? Vector3.up * radius : Vector3.zero), turn);
        var body = Lit(paint, .75f); var metal = Lit(new Color(.40f, .41f, .42f), .8f);
        var pose = lying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
        Vector3 axis = lying ? Vector3.right : Vector3.up, start = lying ? -Vector3.right * height * .5f : Vector3.zero;
        Shape(PrimitiveType.Cylinder, "Can Body", can, start + axis * height * .5f, Tube(radius, height), pose, body);
        Shape(PrimitiveType.Cylinder, "Can Top", can, start + axis * (height + .003f), Tube(radius * .86f, .006f), pose, metal);
        Shape(PrimitiveType.Cylinder, "Can Bottom", can, start - axis * .003f, Tube(radius * .86f, .006f), pose, metal);
    }

    /// <summary>먹다 만 컵라면. 뚜껑은 반쯤 젖혀 있고 젓가락이 걸쳐 있다.</summary>
    private void Noodles(Vector3 position)
    {
        var cup = Group("Model :: Cup Noodles", position, 18f);
        var white = Lit(new Color(.40f, .40f, .38f), .25f); var red = Lit(new Color(.38f, .07f, .05f), .3f);
        var broth = Lit(new Color(.20f, .09f, .03f), .85f); var wood = Lit(new Color(.36f, .27f, .15f), .15f); var foil = Lit(new Color(.42f, .42f, .43f), .7f);
        const float radius = .21f, height = .34f;
        Shape(PrimitiveType.Cylinder, "Cup", cup, new Vector3(0f, height * .5f, 0f), Tube(radius, height), Quaternion.identity, white);
        Shape(PrimitiveType.Cylinder, "Band", cup, new Vector3(0f, height * .55f, 0f), Tube(radius * 1.015f, height * .34f), Quaternion.identity, red);
        Shape(PrimitiveType.Cylinder, "Broth", cup, new Vector3(0f, height + .002f, 0f), Tube(radius * .90f, .004f), Quaternion.identity, broth);
        Shape(PrimitiveType.Cylinder, "Lid", cup, new Vector3(-radius * 1.55f, height * .62f, 0f), Tube(radius * 1.02f, .006f), Quaternion.Euler(0f, 0f, 58f), foil);
        Shape(PrimitiveType.Cylinder, "Chopstick A", cup, new Vector3(.03f, height + .03f, .03f), Tube(.012f, .62f), Quaternion.Euler(84f, 20f, 0f), wood);
        Shape(PrimitiveType.Cylinder, "Chopstick B", cup, new Vector3(.03f, height + .03f, -.02f), Tube(.012f, .62f), Quaternion.Euler(86f, 31f, 0f), wood);
    }

    private void Pen(Vector3 position, float turn, Color ink)
    {
        var pen = Group("Model :: Pen", position + Vector3.up * .02f, turn);
        Shape(PrimitiveType.Cylinder, "Barrel", pen, Vector3.zero, Tube(.02f, .52f), Quaternion.Euler(0f, 0f, 90f), Lit(ink, .6f));
        Shape(PrimitiveType.Cylinder, "Tip", pen, new Vector3(.28f, 0f, 0f), Tube(.012f, .05f), Quaternion.Euler(0f, 0f, 90f), Lit(new Color(.40f, .40f, .40f), .8f));
    }

    /// <summary>과자 봉지: 납작하게 구겨진 은박.</summary>
    private void Wrapper(Vector3 position, float turn)
    {
        var bag = Group("Model :: Snack Wrapper", position, turn);
        Shape(PrimitiveType.Cube, "Bag", bag, new Vector3(0f, .018f, 0f), new Vector3(.52f, .03f, .34f), Quaternion.Euler(4f, 0f, -3f), Lit(new Color(.36f, .22f, .04f), .8f));
        Shape(PrimitiveType.Cube, "Torn Edge", bag, new Vector3(.25f, .03f, 0f), new Vector3(.08f, .012f, .32f), Quaternion.Euler(0f, 0f, 24f), Lit(new Color(.42f, .42f, .43f), .85f));
    }

    /// <summary>책 더미. 두께와 크기가 제각각이고 비뚤게 쌓여 있다.</summary>
    private void Books(Vector3 position, float turn, int count, int seed)
    {
        var stack = Group("Model :: Book Stack", position, turn);
        Color[] covers = { new Color(.26f, .08f, .07f), new Color(.07f, .14f, .26f), new Color(.10f, .22f, .13f), new Color(.30f, .24f, .08f), new Color(.20f, .20f, .22f) };
        var pages = Lit(new Color(.36f, .35f, .31f), .1f);
        float y = 0f;
        for (int i = 0; i < count; i++)
        {
            float thick = .09f + (i + seed) * 37 % 5 * .02f, wide = .92f - (i + seed) * 13 % 4 * .07f, deep = .66f - (i + seed) * 7 % 3 * .05f;
            var book = Group("Book " + i, new Vector3((i * 17 % 5 - 2) * .025f, y, (i * 11 % 3 - 1) * .03f), (i * 23 % 9 - 4) * 3.5f); book.SetParent(stack, false);
            Shape(PrimitiveType.Cube, "Cover", book, new Vector3(0f, thick * .5f, 0f), new Vector3(wide, thick, deep), Quaternion.identity, Lit(covers[(i + seed) % covers.Length], .3f));
            // 책등 반대쪽으로 종이 단면이 살짝 보인다.
            Shape(PrimitiveType.Cube, "Pages", book, new Vector3(.012f, thick * .5f, .012f), new Vector3(wide, thick * .78f, deep), Quaternion.identity, pages);
            y += thick;
        }
    }

    /// <summary>탁상 달력. 넘기다 만 지난달 그대로다.</summary>
    private void Calendar(Vector3 position, float turn)
    {
        var item = Group("Model :: Desk Calendar", position, turn);
        var card = Lit(new Color(.38f, .37f, .34f), .15f); var band = Lit(new Color(.40f, .12f, .05f), .2f); var ring = Lit(new Color(.30f, .30f, .31f), .7f);
        Shape(PrimitiveType.Cube, "Front", item, new Vector3(0f, .19f, -.085f), new Vector3(.62f, .42f, .012f), Quaternion.Euler(-24f, 0f, 0f), card);
        Shape(PrimitiveType.Cube, "Back", item, new Vector3(0f, .19f, .085f), new Vector3(.62f, .42f, .012f), Quaternion.Euler(24f, 0f, 0f), Lit(new Color(.30f, .09f, .04f), .2f));
        Shape(PrimitiveType.Cube, "Band", item, new Vector3(0f, .055f, -.146f), new Vector3(.62f, .09f, .014f), Quaternion.Euler(-24f, 0f, 0f), band);
        Shape(PrimitiveType.Cylinder, "Rings", item, new Vector3(0f, .385f, 0f), Tube(.014f, .60f), Quaternion.Euler(0f, 0f, 90f), ring);
        // 날짜 칸: 작은 점들이 줄지어 있다.
        var ink = Lit(new Color(.10f, .10f, .10f), .1f);
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 7; column++)
            { Shape(PrimitiveType.Cube, "Day", item, Quaternion.Euler(-24f, 0f, 0f) * new Vector3((column - 3) * .075f, .06f - row * .065f + .05f, -.0075f) + new Vector3(0f, .19f, -.085f), new Vector3(.03f, .022f, .004f), Quaternion.Euler(-24f, 0f, 0f), ink); }
        }
    }

    /// <summary>필통으로 쓰는 깡통. 펜이 삐죽삐죽 꽂혀 있다.</summary>
    private void PenCup(Vector3 position)
    {
        var cup = Group("Model :: Pen Cup", position, 0f);
        Shape(PrimitiveType.Cylinder, "Tin", cup, new Vector3(0f, .17f, 0f), Tube(.15f, .34f), Quaternion.identity, Lit(new Color(.10f, .26f, .30f), .6f));
        Shape(PrimitiveType.Cylinder, "Inside", cup, new Vector3(0f, .342f, 0f), Tube(.135f, .004f), Quaternion.identity, Lit(new Color(.03f, .03f, .03f), .1f));
        Color[] inks = { new Color(.05f, .07f, .20f), new Color(.30f, .05f, .05f), new Color(.34f, .30f, .06f), new Color(.06f, .06f, .06f), new Color(.06f, .22f, .10f), new Color(.34f, .34f, .34f) };
        for (int i = 0; i < inks.Length; i++)
        {
            float angle = i * 61f, lean = 9f + i * 13 % 9;
            Shape(PrimitiveType.Cylinder, "Pen " + i, cup, new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * .06f, .30f, Mathf.Sin(angle * Mathf.Deg2Rad) * .06f), Tube(.016f, .30f + i % 3 * .04f), Quaternion.Euler(lean * Mathf.Sin(angle * Mathf.Deg2Rad), 0f, -lean * Mathf.Cos(angle * Mathf.Deg2Rad)), Lit(inks[i], .6f));
        }
    }

    /// <summary>페트병. 서 있거나 쓰러져 있고, 남은 양이 제각각이다.</summary>
    private void Bottle(Vector3 position, Color drink, Color label, float fill, bool lying, float turn)
    {
        const float radius = .095f, height = .52f;
        var bottle = Group("Model :: Plastic Bottle", position + (lying ? Vector3.up * radius : Vector3.zero), turn);
        var holder = new GameObject("Bottle Body").transform; holder.SetParent(bottle, false);
        holder.localRotation = lying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
        var plastic = Lit(new Color(.20f, .23f, .24f), .9f);
        Shape(PrimitiveType.Cylinder, "Plastic", holder, new Vector3(0f, height * .5f, 0f), Tube(radius, height), Quaternion.identity, plastic);
        if (fill > 0f) { Shape(PrimitiveType.Cylinder, "Drink", holder, new Vector3(0f, height * fill * .5f, 0f), Tube(radius * 1.01f, height * fill), Quaternion.identity, Lit(drink, .85f)); }
        Shape(PrimitiveType.Cylinder, "Label", holder, new Vector3(0f, height * .62f, 0f), Tube(radius * 1.02f, height * .22f), Quaternion.identity, Lit(label, .4f));
        Shape(PrimitiveType.Cylinder, "Neck", holder, new Vector3(0f, height + .03f, 0f), Tube(radius * .42f, .07f), Quaternion.identity, plastic);
        Shape(PrimitiveType.Cylinder, "Cap", holder, new Vector3(0f, height + .075f, 0f), Tube(radius * .48f, .035f), Quaternion.identity, Lit(label * 1.2f, .5f));
    }

    /// <summary>쓰고 던져 둔 휴지.</summary>
    private void Tissue(Vector3 position, int seed)
    {
        var tissue = Group("Model :: Used Tissue", position + Vector3.up * .04f, seed * 53f);
        var white = Lit(new Color(.40f, .40f, .39f), .02f);
        Shape(PrimitiveType.Sphere, "Wad", tissue, Vector3.zero, new Vector3(.20f, .09f, .16f), Quaternion.Euler(0f, 0f, 9f), white);
        Shape(PrimitiveType.Cube, "Corner A", tissue, new Vector3(.07f, .01f, .03f), new Vector3(.13f, .012f, .11f), Quaternion.Euler(18f, 30f + seed * 40f, -14f), white);
        Shape(PrimitiveType.Cube, "Corner B", tissue, new Vector3(-.06f, .02f, -.04f), new Vector3(.11f, .012f, .12f), Quaternion.Euler(-21f, 62f, 17f), white);
    }

    private void TissueBox(Vector3 position, float turn)
    {
        var box = Group("Model :: Tissue Box", position, turn);
        Shape(PrimitiveType.Cube, "Box", box, new Vector3(0f, .075f, 0f), new Vector3(.50f, .15f, .26f), Quaternion.identity, Lit(new Color(.12f, .24f, .30f), .3f));
        Shape(PrimitiveType.Cube, "Slot", box, new Vector3(0f, .151f, 0f), new Vector3(.24f, .004f, .07f), Quaternion.identity, Lit(new Color(.03f, .03f, .03f), .1f));
        Shape(PrimitiveType.Cube, "Tissue", box, new Vector3(.01f, .20f, 0f), new Vector3(.15f, .11f, .012f), Quaternion.Euler(0f, 12f, 14f), Lit(new Color(.40f, .40f, .39f), .02f));
    }

    /// <summary>엉킨 이어폰 줄.</summary>
    private void Cord(Vector3 position, float turn)
    {
        var cord = Group("Model :: Tangled Cord", position + Vector3.up * .012f, turn);
        var black = Lit(new Color(.02f, .02f, .02f), .5f);
        float[] angles = { 0f, 47f, 101f, 152f, 23f, 78f, 131f };
        for (int i = 0; i < angles.Length; i++)
        {
            Shape(PrimitiveType.Cylinder, "Loop " + i, cord, new Vector3((i * 31 % 7 - 3) * .03f, i % 2 * .012f, (i * 17 % 5 - 2) * .035f), Tube(.007f, .30f + i % 3 * .08f), Quaternion.Euler(90f, angles[i], 0f), black);
        }
        Shape(PrimitiveType.Sphere, "Bud A", cord, new Vector3(.17f, .01f, .06f), new Vector3(.04f, .03f, .03f), Quaternion.identity, black);
        Shape(PrimitiveType.Sphere, "Bud B", cord, new Vector3(-.14f, .01f, -.09f), new Vector3(.04f, .03f, .03f), Quaternion.identity, black);
    }

    private void Remote(Vector3 position, float turn)
    {
        var remote = Group("Model :: Remote", position, turn);
        Shape(PrimitiveType.Cube, "Body", remote, new Vector3(0f, .02f, 0f), new Vector3(.52f, .04f, .14f), Quaternion.identity, Lit(new Color(.03f, .03f, .035f), .45f));
        var key = Lit(new Color(.16f, .16f, .17f), .3f);
        for (int i = 0; i < 12; i++)
        { Shape(PrimitiveType.Cube, "Key " + i, remote, new Vector3(-.19f + i / 3 * .09f, .042f, (i % 3 - 1) * .038f), new Vector3(.05f, .008f, .025f), Quaternion.identity, key); }
        Shape(PrimitiveType.Cylinder, "Power", remote, new Vector3(.21f, .042f, 0f), Tube(.02f, .008f), Quaternion.identity, Lit(new Color(.36f, .05f, .04f), .4f));
    }

    /// <summary>작은 상자(약 상자 같은 것). 한쪽에 색 띠가 둘려 있다.</summary>
    private void SmallBox(Vector3 position, float turn, Color paper, Color band)
    {
        var box = Group("Model :: Small Box", position, turn);
        Shape(PrimitiveType.Cube, "Box", box, new Vector3(0f, .06f, 0f), new Vector3(.34f, .12f, .20f), Quaternion.identity, Lit(paper, .2f));
        Shape(PrimitiveType.Cube, "Band", box, new Vector3(-.08f, .06f, 0f), new Vector3(.10f, .122f, .202f), Quaternion.identity, Lit(band, .3f));
        Shape(PrimitiveType.Cube, "Open Flap", box, new Vector3(.19f, .13f, 0f), new Vector3(.10f, .006f, .20f), Quaternion.Euler(0f, 0f, 38f), Lit(paper, .2f));
    }

    /// <summary>영수증: 길고 얇은 종이가 끝이 말려 있다.</summary>
    private void Receipt(Vector3 position, float turn)
    {
        var receipt = Group("Model :: Receipt", position, turn);
        var paper = Lit(new Color(.39f, .39f, .37f), .05f);
        Shape(PrimitiveType.Cube, "Strip", receipt, new Vector3(0f, .004f, 0f), new Vector3(.16f, .004f, .52f), Quaternion.identity, paper);
        Shape(PrimitiveType.Cube, "Curl", receipt, new Vector3(0f, .03f, .28f), new Vector3(.16f, .004f, .10f), Quaternion.Euler(-48f, 0f, 0f), paper);
        var ink = Lit(new Color(.14f, .14f, .14f), .05f);
        for (int i = 0; i < 6; i++) { Shape(PrimitiveType.Cube, "Print " + i, receipt, new Vector3((i % 2 == 0 ? -.01f : .015f), .0065f, -.20f + i * .065f), new Vector3(i % 3 == 0 ? .07f : .11f, .001f, .012f), Quaternion.identity, ink); }
    }

    /// <summary>뭔가 담긴 채 구겨진 비닐봉지.</summary>
    private void Bag(Vector3 position)
    {
        var bag = Group("Model :: Plastic Bag", position, 17f);
        var film = Lit(new Color(.30f, .31f, .31f), .75f); var knot = Lit(new Color(.24f, .25f, .25f), .7f);
        Shape(PrimitiveType.Sphere, "Lump A", bag, new Vector3(0f, .13f, 0f), new Vector3(.50f, .28f, .42f), Quaternion.Euler(0f, 0f, 6f), film);
        Shape(PrimitiveType.Sphere, "Lump B", bag, new Vector3(.14f, .10f, .10f), new Vector3(.34f, .22f, .30f), Quaternion.Euler(12f, 30f, -9f), film);
        Shape(PrimitiveType.Sphere, "Lump C", bag, new Vector3(-.15f, .09f, -.08f), new Vector3(.30f, .20f, .28f), Quaternion.Euler(-8f, 0f, 14f), film);
        Shape(PrimitiveType.Cube, "Handle A", bag, new Vector3(-.03f, .30f, .02f), new Vector3(.05f, .14f, .012f), Quaternion.Euler(0f, 20f, 24f), knot);
        Shape(PrimitiveType.Cube, "Handle B", bag, new Vector3(.05f, .29f, -.02f), new Vector3(.05f, .13f, .012f), Quaternion.Euler(0f, -30f, -28f), knot);
    }
}
