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
