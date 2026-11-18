using System.Collections.Generic;
using UnityEngine;

// 방의 모양. 책상은 모니터 벽의 왼쪽에 있고, 오른쪽 벽을 따라 침대가 놓인다. 책상과 침대 사이에 뚜껑 덮인 쓰레기통이 있다.
// 등 뒤 벽의 왼쪽에는 평범한 집 문이 있다. 밖으로 열리는 문이지만 잠겨 있다(엔딩에서만 열린다).
// 문 아래에는 틈이 있어서 바깥 불빛이 가늘게 새어 들어온다. 쪽지가 들어오는 자리다.
// 씬 파일은 그대로 두고 실행할 때 고친다: 바닥·천장·벽을 밀어 방을 넓히고, 문 쪽 벽은 문 구멍이 뚫린 벽으로 새로 만든다.
public sealed class RoomShell : MonoBehaviour
{
    public static RoomShell Instance { get; private set; }
    /// <summary>카메라를 연출이 쥐고 있는 동안(잠자기·일어나기·엔딩) true. 시점 조작과 단축키가 멈춘다.</summary>
    public static bool Cinematic;
    /// <summary>등 뒤 벽을 원래 자리에서 얼마나 더 뒤로 물리는가, 오른쪽 벽을 얼마나 더 오른쪽으로 물리는가(침대 자리).</summary>
    public float extraDepth = 3.6f, extraWidth = 5.6f;
    /// <summary>벽과 책상에서 이만큼은 떨어져 걷는다.</summary>
    public float wallMargin = .75f, deskMargin = .95f;
    public bool Built { get; private set; }
    /// <summary>문 아래 틈의 가운데(방 안쪽 바닥)와 문의 한가운데.</summary>
    public Vector3 DoorGap { get; private set; }
    public Vector3 DoorCenter { get; private set; }
    /// <summary>문짝. 경첩 축에 달려 있어서 Y로 돌리면 밖으로 열린다.</summary>
    public Transform DoorLeaf { get; private set; }
    public Bounds DoorBounds { get; private set; }
    public GameObject Outside { get; private set; }
    public Light OutsideLight { get; private set; }
    /// <summary>침대 전체, 누웠을 때 눈의 자리, 침대에서 나와 서는 자리.</summary>
    public Bounds BedBounds { get; private set; }
    public Vector3 PillowEye { get; private set; }
    public Vector3 BedsideStand { get; private set; }
    public Transform Bed { get; private set; }
    public Transform TrashCan { get; private set; }
    private Material wallPaint;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) { Instance = null; Cinematic = false; } }

    void Update()
    {
        if (Built) { return; }
        // "Plane"이라는 이름은 여러 개다. 그중 가장 넓은 것이 바닥이다.
        GameObject floor = null; float widest = 0f;
        foreach (var candidate in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (candidate.name != "Plane") { continue; }
            float area = candidate.bounds.size.x * candidate.bounds.size.z;
            if (area > widest) { widest = area; floor = candidate.gameObject; }
        }
        var rear = GameObject.Find("Plane (2)"); var front = GameObject.Find("Area :: Damp Back Wall");
        var left = GameObject.Find("Area :: Damp Left Wall"); var right = GameObject.Find("Area :: Damp Right Wall");
        var ceiling = GameObject.Find("Area :: Low Ceiling"); var desk = GameObject.Find("desk_body");
        if (floor == null || rear == null || left == null || right == null || desk == null)
        {
            if (Time.timeSinceLevelLoad > 10f) { Built = true; }
            return;
        }
        Built = true;
        float rearZ = rear.GetComponent<Renderer>().bounds.center.z, frontZ = front != null ? front.GetComponent<Renderer>().bounds.center.z : 2.4f;
        // 방은 뒤(-z)와 오른쪽(+x)으로 늘어난다. 바닥·천장·벽은 넉넉하게 커서, 복사하지 않고 밀기만 하면 된다.
        Vector3 back = Vector3.back * extraDepth, aside = Vector3.right * (extraWidth * .22f);
        floor.transform.position += back + aside;
        if (ceiling != null) { ceiling.transform.position += back + aside; }
        left.transform.position += back; right.transform.position += back + Vector3.right * extraWidth;
        if (front != null) { front.transform.position += aside; }
        float wallZ = rearZ - extraDepth;
        float leftX = left.GetComponent<Renderer>().bounds.center.x, rightX = right.GetComponent<Renderer>().bounds.center.x;
        float top = ceiling != null ? ceiling.GetComponent<Renderer>().bounds.center.y : 8.2f;
        var deskBounds = desk.GetComponent<Renderer>().bounds;
        // 문 쪽 벽: 원래의 통짜 벽은 감추고, 문 구멍이 난 벽을 새로 세운다.
        wallPaint = rear.GetComponent<Renderer>().sharedMaterial;
        rear.GetComponent<Renderer>().enabled = false; rear.name = "Area :: Old Rear Wall";
        var rearCollider = rear.GetComponent<Collider>(); if (rearCollider != null) { rearCollider.enabled = false; }
        const float doorWidth = 3.9f; float doorHeight = Mathf.Min(7.3f, top - .6f), doorLeft = leftX + 1.1f;
        BuildDoorWall(leftX - .3f, rightX + .3f, top + .3f, wallZ, doorLeft, doorLeft + doorWidth, doorHeight);
        BuildDoor(new Vector3(doorLeft, 0f, wallZ), doorWidth, doorHeight);
        BuildBed(rightX, frontZ);
        BuildTrashCan(new Vector3((deskBounds.max.x + BedBounds.min.x) * .5f, 0f, deskBounds.center.z - .7f));
        // 걸어 다닐 수 있는 네모: 옆벽과 문 쪽 벽 사이, 책상 앞까지. 침대 자리는 막는다.
        MouseCameraController.WalkMin = new Vector2(leftX + wallMargin, wallZ + wallMargin);
        MouseCameraController.WalkMax = new Vector2(rightX - wallMargin, deskBounds.min.z - deskMargin);
        MouseCameraController.WalkBlocks.Clear();
        MouseCameraController.WalkBlocks.Add(Rect.MinMaxRect(BedBounds.min.x - .7f, BedBounds.min.z - .7f, BedBounds.max.x + 2f, BedBounds.max.z + 2f));
        MouseCameraController.WalkEnabled = true;
    }

    private static Material Paint(Color color, float smoothness, float metallic = 0f)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = "Room Paint" };
        material.SetColor("_BaseColor", color); material.color = color; material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", metallic);
        return material;
    }
    private static Transform Piece(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Quaternion rotation)
    {
        var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform; item.SetParent(parent, false);
        item.localPosition = position; item.localRotation = rotation;
        item.GetComponent<MeshFilter>().sharedMesh = mesh; item.GetComponent<MeshRenderer>().sharedMaterial = material;
        return item;
    }

    /// <summary>문 구멍이 난 벽. 방 안쪽(+z)을 보는 판 세 장(문 왼쪽, 문 오른쪽, 문 위)과 구멍 안쪽의 벽 두께.</summary>
    private void BuildDoorWall(float x0, float x1, float top, float z, float doorLeft, float doorRight, float doorTop)
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            // 벽지는 원래 벽과 같은 크기로 깔린다(20칸에 한 번).
            foreach (var point in new[] { a, b, c, d }) { uv.Add(new Vector2((point.x + point.z + 10f) / 20f, (point.y + 10f) / 20f)); }
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        void Face(float left, float right, float bottom, float high) { Quad(new Vector3(left, bottom, z), new Vector3(right, bottom, z), new Vector3(right, high, z), new Vector3(left, high, z)); }
        Face(x0, doorLeft, 0f, top); Face(doorRight, x1, 0f, top); Face(doorLeft, doorRight, doorTop, top);
        // 구멍 안쪽: 벽 두께만큼 밖으로 들어간 면 셋.
        const float depth = .55f;
        Quad(new Vector3(doorLeft, 0f, z), new Vector3(doorLeft, 0f, z - depth), new Vector3(doorLeft, doorTop, z - depth), new Vector3(doorLeft, doorTop, z));
        Quad(new Vector3(doorRight, 0f, z - depth), new Vector3(doorRight, 0f, z), new Vector3(doorRight, doorTop, z), new Vector3(doorRight, doorTop, z - depth));
        Quad(new Vector3(doorLeft, doorTop, z - depth), new Vector3(doorRight, doorTop, z - depth), new Vector3(doorRight, doorTop, z), new Vector3(doorLeft, doorTop, z));
        var mesh = new Mesh { name = "Door Wall" };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        var wall = new GameObject("Area :: Damp Door Wall", typeof(MeshFilter), typeof(MeshRenderer)).transform; wall.SetParent(transform, false);
        wall.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        wall.GetComponent<MeshFilter>().sharedMesh = mesh; wall.GetComponent<MeshRenderer>().sharedMaterial = wallPaint;
        // 문 밖: 평소에는 꺼져 있다. 엔딩에서 문이 열리면 눈부신 빛이 쏟아진다.
        var glow = Paint(Color.white, 0f); glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(1f, .97f, .9f) * 6f);
        glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        var outside = new GameObject("Area :: Outside").transform; outside.SetParent(transform, false);
        var bright = new Mesh { name = "Outside Light" };
        float mid = (doorLeft + doorRight) * .5f;
        bright.SetVertices(new List<Vector3> { new(mid - 9f, -1f, z - 4f), new(mid + 9f, -1f, z - 4f), new(mid + 9f, 12f, z - 4f), new(mid - 9f, 12f, z - 4f) });
        bright.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); bright.RecalculateNormals(); bright.RecalculateBounds();
        Piece("Image :: Daylight", outside, bright, glow, Vector3.zero, Quaternion.identity);
        OutsideLight = new GameObject("Light :: Outside").AddComponent<Light>(); OutsideLight.transform.SetParent(outside, false);
        OutsideLight.transform.position = new Vector3(mid, doorTop * .6f, z - 1.2f); OutsideLight.type = LightType.Point; OutsideLight.range = 16f; OutsideLight.intensity = 0f;
        OutsideLight.color = new Color(1f, .96f, .88f); OutsideLight.shadows = LightShadows.None;
        Outside = outside.gameObject; Outside.SetActive(false);
    }

    /// <summary>평범한 집 문: 문틀, 문짝(오목한 판 두 칸), 레버 손잡이와 잠금쇠, 경첩 셋. 문 아래에는 틈이 있다.</summary>
    private void BuildDoor(Vector3 hingeFoot, float width, float height)
    {
        const float gap = .14f, thick = .2f, trim = .34f;
        var door = new GameObject("Area :: Room Door").transform; door.SetParent(transform, false);
        door.SetPositionAndRotation(hingeFoot, Quaternion.identity);
        var wood = Paint(new Color(.30f, .285f, .25f), .28f); var mould = Paint(new Color(.34f, .32f, .28f), .3f);
        var frame = Paint(new Color(.20f, .19f, .165f), .25f); var steel = Paint(new Color(.42f, .42f, .40f), .62f, .9f); var hingeMetal = Paint(new Color(.30f, .28f, .24f), .5f, .85f);
        // 문틀(몰딩): 벽에서 방 쪽으로 조금 튀어나와 있다.
        Piece("Model :: Casing Left", door, PropMesh.RoundedBox(new Vector3(trim, height + trim, .16f), .035f, 4), frame, new Vector3(-trim * .5f, (height + trim) * .5f, .06f), Quaternion.identity);
        Piece("Model :: Casing Right", door, PropMesh.RoundedBox(new Vector3(trim, height + trim, .16f), .035f, 4), frame, new Vector3(width + trim * .5f, (height + trim) * .5f, .06f), Quaternion.identity);
        Piece("Model :: Casing Top", door, PropMesh.RoundedBox(new Vector3(width + trim * 2f, trim, .16f), .035f, 4), frame, new Vector3(width * .5f, height + trim * .5f, .06f), Quaternion.identity);
        // 문짝은 경첩 축에 달린다. 축을 Y로 돌리면 문이 밖(-z)으로 열린다.
        var leaf = new GameObject("Area :: Door Leaf").transform; leaf.SetParent(door, false); leaf.localPosition = new Vector3(.03f, 0f, -.16f);
        float slab = height - gap, w = width - .06f, face = thick * .5f;
        Piece("Model :: Slab", leaf, PropMesh.RoundedBox(new Vector3(w, slab, thick), .03f, 5), wood, new Vector3(w * .5f, gap + slab * .5f, 0f), Quaternion.identity);
        // 오목한 판 두 칸(위는 길고 아래는 짧다): 몰딩 테두리를 둘러 가운데가 들어가 보이게 한다. 안팎 양쪽에 다 있다.
        foreach (float side in new[] { 1f, -1f })
        {
            foreach (var (center, tall) in new[] { (gap + slab * .66f, slab * .50f), (gap + slab * .21f, slab * .26f) })
            {
                float wide = w * .68f; const float rail = .11f; float z = side * (face + .012f);
                Piece("Model :: Mould Top", leaf, PropMesh.RoundedBox(new Vector3(wide + rail, rail, .045f), .02f, 4), mould, new Vector3(w * .5f, center + tall * .5f, z), Quaternion.identity);
                Piece("Model :: Mould Bottom", leaf, PropMesh.RoundedBox(new Vector3(wide + rail, rail, .045f), .02f, 4), mould, new Vector3(w * .5f, center - tall * .5f, z), Quaternion.identity);
                Piece("Model :: Mould Left", leaf, PropMesh.RoundedBox(new Vector3(rail, tall, .045f), .02f, 4), mould, new Vector3(w * .5f - wide * .5f, center, z), Quaternion.identity);
                Piece("Model :: Mould Right", leaf, PropMesh.RoundedBox(new Vector3(rail, tall, .045f), .02f, 4), mould, new Vector3(w * .5f + wide * .5f, center, z), Quaternion.identity);
            }
            // 레버 손잡이: 둥근 받침, 목, 옆으로 뻗은 손잡이. 그 위에 잠금쇠(돌리는 꼭지).
            float handleX = w - .42f, handleY = gap + slab * .46f, out0 = side * face;
            var rose = PropMesh.Lathe(new[] { new Vector2(0f, 0f), new Vector2(.17f, 0f), new Vector2(.175f, .02f), new Vector2(.16f, .05f), new Vector2(.07f, .06f), new Vector2(.06f, .16f), new Vector2(0f, .16f) }, 40);
            Quaternion outward = Quaternion.Euler(side > 0f ? 90f : -90f, 0f, 0f);
            Piece("Model :: Handle Rose", leaf, rose, steel, new Vector3(handleX, handleY, out0), outward);
            var lever = PropMesh.Tube(PropMesh.Smooth(new[] { new Vector3(0f, 0f, side * .13f), new Vector3(-.08f, 0f, side * .17f), new Vector3(-.30f, -.01f, side * .17f), new Vector3(-.62f, -.03f, side * .16f) }, 8), t => Mathf.Lerp(.055f, .042f, t), 16);
            Piece("Model :: Handle Lever", leaf, lever, steel, new Vector3(handleX, handleY, out0), Quaternion.identity);
            Piece("Model :: Lock Rose", leaf, rose, steel, new Vector3(handleX, handleY + .62f, out0), outward).localScale = new Vector3(.72f, .6f, .72f);
            Piece("Model :: Lock Turn", leaf, PropMesh.RoundedBox(new Vector3(.05f, .2f, .09f), .02f, 3), steel, new Vector3(handleX, handleY + .62f, out0 + side * .12f), Quaternion.Euler(0f, 0f, 90f));
        }
        // 경첩 셋: 문틀 쪽 날개, 문짝 쪽 날개, 그 사이의 축.
        var knuckle = PropMesh.Lathe(new[] { new Vector2(0f, 0f), new Vector2(.04f, 0f), new Vector2(.05f, .015f), new Vector2(.05f, .145f), new Vector2(.04f, .16f), new Vector2(0f, .16f) }, 24);
        var cap = PropMesh.Lathe(new[] { new Vector2(0f, 0f), new Vector2(.052f, 0f), new Vector2(.058f, .02f), new Vector2(.03f, .05f), new Vector2(0f, .055f) }, 24);
        foreach (float share in new[] { .13f, .5f, .87f })
        {
            float y = gap + slab * share;
            var hinge = new GameObject("Model :: Hinge").transform; hinge.SetParent(door, false); hinge.localPosition = new Vector3(.03f, y, -.03f);
            for (int i = 0; i < 3; i++) { Piece("Model :: Knuckle", hinge, knuckle, hingeMetal, new Vector3(0f, -.25f + i * .17f, 0f), Quaternion.identity); }
            Piece("Model :: Pin Cap", hinge, cap, hingeMetal, new Vector3(0f, .26f, 0f), Quaternion.identity);
            Piece("Model :: Pin Foot", hinge, cap, hingeMetal, new Vector3(0f, -.25f, 0f), Quaternion.Euler(180f, 0f, 0f));
            Piece("Model :: Frame Plate", hinge, PropMesh.RoundedBox(new Vector3(.2f, .5f, .025f), .008f, 2), hingeMetal, new Vector3(-.11f, 0f, .0f), Quaternion.identity);
            Piece("Model :: Door Plate", hinge, PropMesh.RoundedBox(new Vector3(.2f, .5f, .025f), .008f, 2), hingeMetal, new Vector3(.11f, 0f, -.02f), Quaternion.identity);
        }
        // 문턱과, 문틈으로 새어 드는 바깥 불빛.
        Piece("Model :: Threshold", door, PropMesh.RoundedBox(new Vector3(width, .035f, .55f), .012f, 2), frame, new Vector3(width * .5f, .017f, -.1f), Quaternion.identity);
        var glow = Paint(new Color(.9f, .78f, .5f), 0f); glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(.55f, .42f, .22f) * 1.4f);
        glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        Piece("Image :: Gap Light", door, PropMesh.RoundedBox(new Vector3(width - .1f, gap * .7f, .02f), .005f, 2), glow, new Vector3(width * .5f, gap * .42f + .03f, -.3f), Quaternion.identity);
        var lamp = new GameObject("Light :: Door Gap").AddComponent<Light>(); lamp.transform.SetParent(door, false);
        lamp.transform.localPosition = new Vector3(width * .5f, .14f, .55f); lamp.type = LightType.Point; lamp.range = 1.5f; lamp.intensity = .5f;
        lamp.color = new Color(.95f, .78f, .5f); lamp.shadows = LightShadows.None;
        DoorLeaf = leaf;
        DoorCenter = hingeFoot + new Vector3(width * .5f, height * .5f, 0f); DoorGap = hingeFoot + new Vector3(width * .5f, 0f, .12f);
        DoorBounds = new Bounds(DoorCenter, new Vector3(width + trim * 2f, height + trim, .7f));
    }

    /// <summary>침대: 나무틀과 머리판, 매트리스, 눌린 베개, 구겨진 이불. 오른쪽 벽에 붙고 머리는 모니터 벽 쪽이다.</summary>
    private void BuildBed(float wallX, float frontZ)
    {
        const float width = 5f, length = 10f, deck = 1.35f, mattress = 1.05f;
        var bed = new GameObject("Area :: Bed").transform; bed.SetParent(transform, false);
        bed.SetPositionAndRotation(new Vector3(wallX - .12f - width * .5f, 0f, frontZ - .35f - length * .5f), Quaternion.identity);
        var timber = Paint(new Color(.17f, .13f, .10f), .3f); var ticking = Paint(new Color(.46f, .44f, .40f), .12f);
        var linen = Paint(new Color(.50f, .49f, .46f), .1f); var wool = Paint(new Color(.17f, .20f, .25f), .08f);
        // 틀: 옆널 두 장과 발치널, 다리 넷, 머리판(기둥 둘 + 판 + 윗막대).
        Piece("Model :: Rail Left", bed, PropMesh.RoundedBox(new Vector3(.22f, .62f, length), .05f, 4), timber, new Vector3(-width * .5f + .11f, deck - .2f, 0f), Quaternion.identity);
        Piece("Model :: Rail Right", bed, PropMesh.RoundedBox(new Vector3(.22f, .62f, length), .05f, 4), timber, new Vector3(width * .5f - .11f, deck - .2f, 0f), Quaternion.identity);
        Piece("Model :: Foot Board", bed, PropMesh.RoundedBox(new Vector3(width, 1.0f, .22f), .05f, 4), timber, new Vector3(0f, deck, -length * .5f + .11f), Quaternion.identity);
        Piece("Model :: Slats", bed, PropMesh.RoundedBox(new Vector3(width - .3f, .12f, length - .3f), .03f, 2), timber, new Vector3(0f, deck - .06f, 0f), Quaternion.identity);
        foreach (float x in new[] { -1f, 1f })
        {
            Piece("Model :: Leg", bed, PropMesh.RoundedBox(new Vector3(.34f, deck, .34f), .05f, 4), timber, new Vector3(x * (width * .5f - .17f), deck * .5f, -length * .5f + .17f), Quaternion.identity);
            Piece("Model :: Head Post", bed, PropMesh.RoundedBox(new Vector3(.36f, 4.3f, .36f), .07f, 5), timber, new Vector3(x * (width * .5f - .18f), 2.15f, length * .5f - .18f), Quaternion.identity);
        }
        Piece("Model :: Head Board", bed, PropMesh.RoundedBox(new Vector3(width - .5f, 2.3f, .16f), .05f, 4), timber, new Vector3(0f, 2.75f, length * .5f - .18f), Quaternion.identity);
        Piece("Model :: Head Rail", bed, PropMesh.RoundedBox(new Vector3(width, .26f, .42f), .09f, 5), timber, new Vector3(0f, 4.2f, length * .5f - .18f), Quaternion.identity);
        // 매트리스: 가운데가 조금 꺼지고 가장자리에 누빔 줄이 있다.
        float topY = deck + mattress;
        Piece("Model :: Mattress", bed, PropMesh.RoundedBox(new Vector3(width - .5f, mattress, length - .6f), .24f, 8), ticking, new Vector3(0f, deck + mattress * .5f, -.05f), Quaternion.identity);
        // 베개: 가운데가 눌리고 네 귀가 납작한 솜 베개.
        float pillowZ = length * .5f - 1.55f;
        Piece("Model :: Pillow", bed, PillowMesh(3.1f, .95f, 1.8f), linen, new Vector3(-.12f, topY + .30f, pillowZ), Quaternion.Euler(0f, 5f, 0f));
        // 이불: 발치에서 가슴께까지 덮여 있고, 옆으로 늘어지고, 위쪽은 접혀 말려 있다.
        const float coverWidth = 5.2f, coverLength = 6.9f; float halfMattress = (width - .5f) * .5f;
        var cover = PropMesh.Sheet(coverWidth, coverLength, 64, 80, (u, v) =>
        {
            float x = (u - .5f) * coverWidth, over = Mathf.Max(0f, Mathf.Abs(x) - halfMattress + .1f);
            float wrinkle = .06f * Mathf.Sin(u * 17f + v * 9f + Mathf.Sin(v * 7f) * 2f) + .045f * Mathf.Sin(u * 41f - v * 23f) + .03f * Mathf.Sin(v * 57f + u * 13f);
            float body = .20f * Mathf.Exp(-(u - .46f) * (u - .46f) * 22f) * Mathf.SmoothStep(0f, 1f, v * 3f);
            float roll = .22f * Mathf.Exp(-(1f - v) * (1f - v) * 260f);
            float flat = Mathf.Clamp01(1f - over * 1.6f);
            return .19f + (wrinkle + body) * flat + roll * flat - over * 2.1f - over * over * .8f;
        });
        Piece("Model :: Blanket", bed, cover, wool, new Vector3(-.05f, topY, -length * .5f + coverLength * .5f + .15f), Quaternion.Euler(0f, -2.5f, 0f));
        // 침대 머리맡으로 번지는 모니터 불빛. 이것이 없으면 침대가 어둠에 묻혀 보이지 않는다.
        var spill = new GameObject("Light :: Monitor Spill Bed").AddComponent<Light>(); spill.transform.SetParent(bed, false);
        spill.transform.localPosition = new Vector3(-2.2f, 5.2f, 1.2f); spill.type = LightType.Point; spill.range = 10f; spill.intensity = 5.5f;
        spill.color = new Color(.62f, .74f, .9f); spill.shadows = LightShadows.None;
        Bed = bed;
        BedBounds = new Bounds(bed.position + new Vector3(0f, 2.2f, 0f), new Vector3(width, 4.4f, length));
        PillowEye = bed.position + new Vector3(-.12f, topY + 1.15f, pillowZ - .25f);
        BedsideStand = new Vector3(bed.position.x - width * .5f - 1.25f, 5.5f, bed.position.z - 2.1f);
    }

    /// <summary>둥근 상자를 솜 베개처럼 부풀린다: 가운데는 도톰하고 가장자리로 갈수록 납작하며, 머리 자리는 살짝 눌려 있다.</summary>
    private static Mesh PillowMesh(float width, float height, float depth)
    {
        var mesh = PropMesh.RoundedBox(new Vector3(width, height, depth), height * .46f, 10);
        var points = mesh.vertices;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 p = points[i]; float u = p.x / (width * .5f), v = p.z / (depth * .5f);
            float puff = Mathf.Pow(Mathf.Clamp01(1f - u * u * u * u), .9f) * Mathf.Pow(Mathf.Clamp01(1f - v * v * v * v), .9f);
            float dent = p.y > 0f ? .34f * Mathf.Exp(-(u * u * 7f + v * v * 4f)) : 0f;
            float crease = p.y > 0f ? .03f * Mathf.Sin(u * 9f + v * 4f) : 0f;
            // 귀퉁이는 뾰족하게 당겨진다.
            float corner = Mathf.Pow(Mathf.Abs(u * v), 3f) * .12f;
            points[i] = new Vector3(p.x * (1f + corner), (p.y > 0f ? p.y * (.14f + .86f * puff) * (1f - dent) : p.y * (.10f + .30f * puff)) + crease * puff, p.z * (1f + corner));
        }
        mesh.vertices = points; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>뚜껑이 덮인 쓰레기통: 위가 조금 넓은 통, 테두리, 볼록한 뚜껑과 꼭지, 발판.</summary>
    private void BuildTrashCan(Vector3 foot)
    {
        var can = new GameObject("Area :: Trash Can").transform; can.SetParent(transform, false);
        can.SetPositionAndRotation(foot, Quaternion.Euler(0f, 24f, 0f));
        var body = Paint(new Color(.13f, .14f, .15f), .42f, .1f); var lidPaint = Paint(new Color(.17f, .18f, .19f), .5f, .15f); var metal = Paint(new Color(.36f, .36f, .35f), .6f, .9f);
        const float r0 = .50f, r1 = .60f, h = 1.55f;
        Piece("Model :: Bin", can, PropMesh.Lathe(new[]
        {
            new Vector2(0f, 0f), new Vector2(r0 - .03f, 0f), new Vector2(r0, .03f), new Vector2(r0 + .012f, .10f), new Vector2(r0 + .004f, .13f),
            new Vector2(r1 - .01f, h - .12f), new Vector2(r1 + .01f, h - .10f), new Vector2(r1 + .025f, h - .03f), new Vector2(r1 + .01f, h), new Vector2(r1 - .03f, h), new Vector2(r1 - .04f, h - .06f), new Vector2(0f, h - .06f),
        }, 72), body, Vector3.zero, Quaternion.identity);
        // 통 옆의 세로 홈 여덟 줄.
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad; Vector3 low = new Vector3(Mathf.Cos(angle) * (r0 + .012f), .22f, Mathf.Sin(angle) * (r0 + .012f)), high = new Vector3(Mathf.Cos(angle) * (r1 - .004f), h - .22f, Mathf.Sin(angle) * (r1 - .004f));
            Piece("Model :: Rib", can, PropMesh.Tube(new[] { low, high }, _ => .022f, 10), body, Vector3.zero, Quaternion.identity);
        }
        Piece("Model :: Lid", can, PropMesh.Lathe(new[]
        {
            new Vector2(0f, .02f), new Vector2(r1 + .045f, .02f), new Vector2(r1 + .06f, .0f), new Vector2(r1 + .07f, .04f), new Vector2(r1 + .06f, .10f), new Vector2(r1 + .02f, .13f),
            new Vector2(r1 * .8f, .19f), new Vector2(r1 * .5f, .235f), new Vector2(r1 * .2f, .255f), new Vector2(0f, .26f),
        }, 72), lidPaint, new Vector3(0f, h - .02f, 0f), Quaternion.Euler(0f, 0f, 1.6f));
        Piece("Model :: Lid Knob", can, PropMesh.Lathe(new[] { new Vector2(0f, 0f), new Vector2(.05f, 0f), new Vector2(.045f, .05f), new Vector2(.075f, .08f), new Vector2(.085f, .11f), new Vector2(.06f, .14f), new Vector2(0f, .145f) }, 32), metal, new Vector3(0f, h + .23f, 0f), Quaternion.identity);
        // 발판과 그 축.
        Piece("Model :: Pedal", can, PropMesh.RoundedBox(new Vector3(.34f, .04f, .22f), .015f, 3), metal, new Vector3(0f, .07f, -r0 - .12f), Quaternion.Euler(-12f, 0f, 0f));
        Piece("Model :: Pedal Arm", can, PropMesh.Tube(new[] { new Vector3(-.12f, .05f, -r0 - .02f), new Vector3(-.12f, .05f, -r0 + .1f) }, _ => .018f, 10), metal, Vector3.zero, Quaternion.identity);
        Piece("Model :: Pedal Arm", can, PropMesh.Tube(new[] { new Vector3(.12f, .05f, -r0 - .02f), new Vector3(.12f, .05f, -r0 + .1f) }, _ => .018f, 10), metal, Vector3.zero, Quaternion.identity);
        TrashCan = can;
    }
}
