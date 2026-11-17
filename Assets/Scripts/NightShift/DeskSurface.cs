using System.Collections.Generic;
using UnityEngine;

// 책상 무늬가 늘어나 보이지 않게 한다.
// 책상 모델의 원래 UV는 그 모델 전용 그림(조각을 한 장에 모아 놓은 것)에 맞춰져 있어서, 이어 붙이는 나무 그림을 입히면 조각마다 제멋대로 늘어난다.
// 그래서 실행할 때 책상의 면마다 실제 크기대로 다시 펴 준다: 윗면·아랫면은 위에서, 옆면은 옆에서 그림을 비춘 것처럼 입힌다.
// 널은 책상의 긴 쪽을 따라 놓인다. 모양(꼭짓점 위치)과 씬 파일은 바꾸지 않는다.
public sealed class DeskSurface : MonoBehaviour
{
    /// <summary>그림 한 장이 덮는 길이: 책상 긴 쪽 길이의 이 비율. 그림 한 장에 널이 여섯 장 들어 있다.</summary>
    [Range(.2f, 2f)] public float tileOfLength = .62f;
    /// <summary>다시 펴 줄 부분. 첫째가 책상 몸통이고, 나머지(서랍)는 몸통과 같은 나무 재질을 입힌다.</summary>
    public string[] parts = { "desk_body", "desk_drawer" };
    private bool done;

    void Update()
    {
        if (done) { return; }
        var body = GameObject.Find(parts[0]);
        if (body == null)
        {
            // 책상이 없는 장면이면 더 찾지 않는다.
            if (Time.timeSinceLevelLoad > 10f) { done = true; }
            return;
        }
        done = true;
        var wood = body.GetComponent<Renderer>() == null ? null : body.GetComponent<Renderer>().sharedMaterial;
        foreach (string part in parts)
        {
            var item = GameObject.Find(part);
            var filter = item == null ? null : item.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) { continue; }
            filter.sharedMesh = Unwrap(filter.sharedMesh, filter.transform, body.transform, tileOfLength);
            // 서랍은 원래 흰 플라스틱 재질이라 낡은 나무 책상과 따로 놀았다.
            var renderer = item.GetComponent<Renderer>();
            if (item != body && renderer != null && wood != null)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) { materials[i] = wood; }
                renderer.sharedMaterials = materials;
            }
        }
    }

    /// <summary>면마다 실제 크기대로 UV를 다시 매긴 새 메시를 만든다. 삼각형마다 꼭짓점을 따로 둔다.</summary>
    public static Mesh Unwrap(Mesh source, Transform owner, Transform desk, float tileOfLength)
    {
        var positions = source.vertices; var normals = source.normals;
        // 세계 기준의 위, 긴 쪽, 짧은 쪽.
        // 크기의 기준은 책상 몸통이다. 서랍도 같은 방향, 같은 크기로 무늬가 놓인다.
        var reference = desk.GetComponent<MeshFilter>(); var bounds = reference != null && reference.sharedMesh != null ? reference.sharedMesh.bounds : source.bounds; Vector3 up = Vector3.up;
        Vector3 size = Vector3.zero;
        foreach (var corner in new[] { Vector3.right, Vector3.up, Vector3.forward })
        {
            Vector3 world = desk.TransformVector(Vector3.Scale(corner, bounds.size));
            size += new Vector3(Mathf.Abs(world.x), Mathf.Abs(world.y), Mathf.Abs(world.z));
        }
        Vector3 along = size.x >= size.z ? Vector3.right : Vector3.forward, across = size.x >= size.z ? Vector3.forward : Vector3.right;
        float tile = Mathf.Max(.01f, Mathf.Max(size.x, size.z) * tileOfLength);

        var newPositions = new List<Vector3>(); var newNormals = new List<Vector3>(); var uvs = new List<Vector2>();
        var mesh = new Mesh { name = source.name + " (unwrapped)", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, subMeshCount = source.subMeshCount };
        var triangles = new List<int>[source.subMeshCount];
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            var indices = source.GetTriangles(sub); triangles[sub] = new List<int>(indices.Length);
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                Vector3 a = owner.TransformPoint(positions[indices[i]]), b = owner.TransformPoint(positions[indices[i + 1]]), c = owner.TransformPoint(positions[indices[i + 2]]);
                Vector3 face = Vector3.Cross(b - a, c - a).normalized;
                float vertical = Mathf.Abs(Vector3.Dot(face, up)), lengthwise = Mathf.Abs(Vector3.Dot(face, along));
                for (int k = 0; k < 3; k++)
                {
                    int index = indices[i + k]; Vector3 world = k == 0 ? a : k == 1 ? b : c;
                    Vector2 uv;
                    // 위·아래를 보는 면: 긴 쪽이 그림의 가로(결 방향), 짧은 쪽이 세로.
                    if (vertical > .6f) { uv = new Vector2(Vector3.Dot(world, along), Vector3.Dot(world, across)); }
                    // 양 끝의 옆면: 짧은 쪽과 높이.
                    else if (lengthwise > .6f) { uv = new Vector2(Vector3.Dot(world, across), Vector3.Dot(world, up)); }
                    // 앞·뒤 면: 긴 쪽과 높이.
                    else { uv = new Vector2(Vector3.Dot(world, along), Vector3.Dot(world, up)); }
                    triangles[sub].Add(newPositions.Count);
                    newPositions.Add(positions[index]); newNormals.Add(index < normals.Length ? normals[index] : owner.InverseTransformDirection(face)); uvs.Add(uv / tile);
                }
            }
        }
        mesh.SetVertices(newPositions); mesh.SetNormals(newNormals); mesh.SetUVs(0, uvs);
        for (int sub = 0; sub < triangles.Length; sub++) { mesh.SetTriangles(triangles[sub], sub); }
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }
}
