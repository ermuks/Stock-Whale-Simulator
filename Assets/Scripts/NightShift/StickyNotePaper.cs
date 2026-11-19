using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 모니터 아래에 붙어 있는 메모지(Cloth). 목표를 이루면 한 번 떨어져 책상에 내려앉고, 그 뒤로는 계산을 멈춘다.
/// 붙어 있는 동안에는 윗부분만 고정돼 있고 아래로 갈수록 자유롭게 움직여서, 방의 바람에 살짝 들썩인다.
/// 떨어질 때는 종이처럼 좌우로 흔들리며 천천히 내려온다.
/// </summary>
public sealed class StickyNotePaper : MonoBehaviour
{
    public Cloth paperCloth;
    public SkinnedMeshRenderer paperRenderer;
    public Rigidbody body;
    public BoxCollider landingCollider;
    public float settleAfterSeconds = 6f;
    public bool Released { get; private set; }
    public bool Settled { get; private set; }
    public int ReleaseCount { get; private set; }
    private Mesh settledMesh;
    private readonly System.Collections.Generic.List<Material> restingMaterials = new();
    private float width = 1f, height = 1f;

    void Start()
    {
        if (paperCloth == null || paperRenderer == null || paperRenderer.sharedMesh == null) { return; }
        Loosen();
    }

    /// <summary>
    /// 씬에 저장된 값은 종이를 거의 판자처럼 묶어 두고 있었다(움직일 수 있는 거리가 너비의 1.6%).
    /// 실행할 때 풀어 준다: 접착면(위 13%)만 고정하고, 아래로 갈수록, 가장자리일수록 더 많이 움직이게 한다.
    /// </summary>
    private void Loosen()
    {
        var mesh = paperRenderer.sharedMesh; var vertices = mesh.vertices; var size = mesh.bounds.size;
        width = Mathf.Max(.0001f, size.x); height = Mathf.Max(.0001f, size.y);
        var coefficients = paperCloth.coefficients;
        if (coefficients.Length != vertices.Length) { return; }
        float top = mesh.bounds.max.y;
        for (int i = 0; i < vertices.Length; i++)
        {
            float t = Mathf.Clamp01((top - vertices[i].y) / height), side = Mathf.Clamp01(Mathf.Abs(vertices[i].x - mesh.bounds.center.x) / (width * .5f));
            float reach = t <= .13f ? 0f : (t - .13f) / .87f;
            coefficients[i].maxDistance = width * (.22f * reach * reach + .05f * reach) * (.75f + .25f * side);
        }
        paperCloth.coefficients = coefficients;
        // 종이는 잘 늘어나지 않지만 휘기는 한다. 너무 출렁이지 않게 조금만 눌러 둔다.
        paperCloth.stretchingStiffness = 1f; paperCloth.bendingStiffness = .45f; paperCloth.damping = .28f;
        paperCloth.worldVelocityScale = .5f; paperCloth.worldAccelerationScale = .5f;
        // 방 안의 약한 바람(환풍기): 일정하게 미는 힘은 거의 없고, 불규칙하게 들썩이게 하는 힘이 있다.
        paperCloth.externalAcceleration = new Vector3(.05f, 0f, -.35f) * width;
        paperCloth.randomAcceleration = new Vector3(.9f, .25f, 2.4f) * width;
    }

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
        // 떼어지면 접착면도 풀린다. 종이 전체가 휘청이며 떨어진다.
        var constraints = paperCloth.coefficients;
        for (int i = 0; i < constraints.Length; i++)
        { constraints[i].maxDistance = Mathf.Max(constraints[i].maxDistance, width * .12f); }
        paperCloth.coefficients = constraints;
        // 종이는 책상 위에서 잘 미끄러진다. 아랫변이 앞으로 미끄러져 나와야 글자 면이 위를 보고 눕는다.
        landingCollider.material = new PhysicsMaterial("Paper") { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
        landingCollider.enabled = true;
        body.isKinematic = false; body.useGravity = true;
        // 종이는 공기에 걸려 천천히 내려온다.
        body.linearDamping = 4.5f; body.angularDamping = 2.2f;
        // 아래쪽이 앞으로 밀려 나오면서 글자 면이 위를 보게 눕는다(떨어지는 높이가 낮아서 처음부터 그쪽으로 돌려 준다).
        Vector3 faceUp = Vector3.Cross(-transform.forward, Vector3.up).normalized;
        body.linearVelocity = -transform.forward * .9f + Vector3.down * .05f;
        body.angularVelocity = faceUp * 5.5f + transform.forward * .4f;
        float elapsed = 0f, phase = Random.value * 6.28f, landedAt = -1f;
        Vector3 side = transform.right, out_ = -transform.forward;
        var loose = paperCloth.coefficients; var tight = paperCloth.coefficients;
        while (elapsed < settleAfterSeconds)
        {
            yield return new WaitForFixedUpdate(); elapsed += Time.fixedDeltaTime;
            // 글자가 적힌 면이 위를 보도록 조금씩 돌린다. 책상에 닿을 무렵에는 거의 눕는다.
            body.AddTorque(Vector3.Cross(-transform.forward, Vector3.up) * 42f, ForceMode.Acceleration);
            float facing = Vector3.Dot(-transform.forward, Vector3.up);
            if (facing < .92f)
            {
                // 아랫변은 앞으로, 윗변은 뒤로: 모니터에 기대지 않고 앞으로 미끄러지며 눕는다.
                float half = landingCollider.size.y * .5f * transform.lossyScale.y; Vector3 middle = landingCollider.bounds.center;
                body.AddForceAtPosition(out_ * 26f * width, middle - transform.up * half, ForceMode.Acceleration);
                body.AddForceAtPosition(-out_ * 9f * width, middle + transform.up * half, ForceMode.Acceleration);
            }
            // 내려앉았다(거의 멈췄다): 천은 책상과 부딪치지 않아서, 그대로 두면 종이가 책상 속으로 처진다.
            // 그래서 닿은 뒤에는 천을 몸통의 면에 바짝 붙여 종이가 책상 위에 납작하게 눕게 한다.
            // 납작하게 누웠을 때만 내려앉은 것으로 친다(모니터에 기대 선 채로 굳지 않게). 너무 오래 걸리면 그대로 둔다.
            if (landedAt < 0f && elapsed > .5f && (body.IsSleeping() || body.linearVelocity.sqrMagnitude < .02f) && (Mathf.Abs(facing) > .9f || elapsed > settleAfterSeconds - 1f)) { landedAt = elapsed; }
            if (landedAt >= 0f)
            {
                float flat = Mathf.Clamp01((elapsed - landedAt) / .5f);
                for (int i = 0; i < tight.Length; i++) { tight[i].maxDistance = Mathf.Lerp(loose[i].maxDistance, width * .004f, flat); }
                paperCloth.coefficients = tight;
                if (flat >= 1f) { break; }
                continue;
            }
            // 낙엽처럼 좌우로 미끄러지며 흔들린다. 내려갈수록 흔들림이 잦아든다.
            float fade = Mathf.Clamp01(1.2f - elapsed * .35f);
            body.AddForce((side * Mathf.Sin(elapsed * 5.1f + phase) * 3.2f + out_ * (.9f + Mathf.Cos(elapsed * 3.3f + phase) * 1.4f)) * width * fade, ForceMode.Acceleration);
            body.AddTorque((out_ * Mathf.Cos(elapsed * 5.1f + phase) * 5f + side * Mathf.Sin(elapsed * 2.7f) * 1f) * fade, ForceMode.Acceleration);
        }
        Settle();
    }

    private void Settle()
    {
        if (Settled)
        { return; }
        Settled = true;
        var baked = new Mesh { name = "Baked Sticky Paper" };
        // Paper meshes are authored at world size with unit-scale transforms: Cloth baking cannot double-scale them.
        paperRenderer.BakeMesh(baked, false);
        settledMesh = TwoSided(baked); Destroy(baked);
        var resting = new GameObject("Mesh :: Resting Paper", typeof(MeshFilter), typeof(MeshRenderer)); resting.transform.SetParent(transform, false);
        // 책상 면과 겹쳐 깜빡이지 않게 아주 조금 띄운다.
        resting.transform.position += Vector3.up * (width * .02f);
        var filter = resting.GetComponent<MeshFilter>(); filter.sharedMesh = settledMesh;
        // 앞장과 뒷장이 같은 자리에 겹쳐 있으므로, 내려앉은 종이는 뒷면을 그리지 않는 재질을 쓴다.
        var materials = paperRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null) { continue; }
            materials[i] = new Material(materials[i]) { name = materials[i].name + " (resting)" }; materials[i].SetFloat("_Cull", 2f);
            restingMaterials.Add(materials[i]);
        }
        var renderer = resting.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.TwoSided; renderer.receiveShadows = true;
        paperCloth.enabled = false; paperRenderer.enabled = false;
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        body.isKinematic = true; body.Sleep(); landingCollider.enabled = false;
    }

    /// <summary>
    /// 내려앉은 종이는 뒷면이 위를 볼 수 있다. 재질은 뒷면의 법선을 뒤집어 주지 않아서 뒷면이 새까맣게 보였다.
    /// 그래서 같은 모양을 뒤집어 한 장 더 겹쳐서, 어느 쪽이 위를 보든 빛을 제대로 받게 한다.
    /// </summary>
    private static Mesh TwoSided(Mesh source)
    {
        var vertices = source.vertices; var uv = source.uv; var triangles = source.triangles;
        // 풀어 둔 종이는 떨어지는 동안 몸통보다 아래로 처진다. 그대로 굳히면 책상 속에 묻힌다.
        // 내려앉은 뒤에는 종이 면(가로·세로)은 그대로 두고 두께 쪽 굴곡만 조금 남겨서 몸통의 면 위에 편다.
        float middle = 0f;
        foreach (var vertex in vertices) { middle += vertex.z; }
        middle /= Mathf.Max(1, vertices.Length);
        for (int i = 0; i < vertices.Length; i++) { vertices[i].z = (vertices[i].z - middle) * .18f; }
        source.vertices = vertices;
        source.RecalculateNormals(); var normals = source.normals;
        int count = vertices.Length;
        var allVertices = new Vector3[count * 2]; var allNormals = new Vector3[count * 2]; var allUv = new Vector2[count * 2]; var allTriangles = new int[triangles.Length * 2];
        for (int i = 0; i < count; i++)
        {
            allVertices[i] = allVertices[i + count] = vertices[i];
            allNormals[i] = normals[i]; allNormals[i + count] = -normals[i];
            if (i < uv.Length) { allUv[i] = allUv[i + count] = uv[i]; }
        }
        for (int i = 0; i < triangles.Length; i += 3)
        {
            allTriangles[i] = triangles[i]; allTriangles[i + 1] = triangles[i + 1]; allTriangles[i + 2] = triangles[i + 2];
            int back = triangles.Length + i;
            allTriangles[back] = triangles[i] + count; allTriangles[back + 1] = triangles[i + 2] + count; allTriangles[back + 2] = triangles[i + 1] + count;
        }
        var mesh = new Mesh { name = "Settled Sticky Paper" };
        mesh.vertices = allVertices; mesh.normals = allNormals; mesh.uv = allUv; mesh.triangles = allTriangles;
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
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
        foreach (var material in restingMaterials) { Destroy(material); }
    }
}
