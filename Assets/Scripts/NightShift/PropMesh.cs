using System;
using System.Collections.Generic;
using UnityEngine;

// 소품용 메시를 코드로 빚는 도구. 기본 도형(원기둥·상자·공)을 이어 붙이던 것을 대신한다.
// 돌려 깎기(컵·병·캔), 길 따라 늘인 관(손잡이·줄·철사), 모서리를 굴린 상자(책·상자·리모컨), 구긴 덩어리(종이·휴지), 휜 종이를 만든다.
// 삼각형 수는 아끼지 않는다. 한 번 만들어 두고 다시 계산하지 않는다.
public static class PropMesh
{
    /// <summary>
    /// 단면(반지름, 높이)을 Y축 둘레로 돌려 만든 회전체. 같은 점을 두 번 넣으면 그 자리가 각진 모서리가 된다.
    /// 반지름 0에서 시작하거나 끝나면 그쪽이 막힌다.
    /// </summary>
    public static Mesh Lathe(IList<Vector2> profile, int segments = 64)
    {
        int rings = profile.Count, columns = segments + 1;
        var vertices = new Vector3[rings * columns]; var uv = new Vector2[rings * columns]; var triangles = new List<int>(rings * segments * 6);
        float length = 0f; var along = new float[rings];
        for (int i = 1; i < rings; i++) { length += Vector2.Distance(profile[i - 1], profile[i]); along[i] = length; }
        for (int i = 0; i < rings; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                float angle = j / (float)segments * Mathf.PI * 2f;
                vertices[i * columns + j] = new Vector3(Mathf.Cos(angle) * profile[i].x, profile[i].y, Mathf.Sin(angle) * profile[i].x);
                uv[i * columns + j] = new Vector2(j / (float)segments, length > 0f ? along[i] / length : 0f);
            }
        }
        for (int i = 0; i < rings - 1; i++)
        {
            // 같은 점을 겹쳐 둔 자리(모서리)는 면을 만들지 않는다.
            if ((profile[i] - profile[i + 1]).sqrMagnitude < 1e-12f) { continue; }
            for (int j = 0; j < segments; j++)
            {
                int a = i * columns + j, b = a + 1, c = a + columns, d = c + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
        }
        var mesh = new Mesh { name = "Lathe", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        // 이음매(첫 줄과 마지막 줄)는 자리가 같으므로 법선도 같게 맞춘다. 맞추지 않으면 세로로 금이 보인다.
        var normals = mesh.normals;
        for (int i = 0; i < rings; i++)
        {
            Vector3 joined = (normals[i * columns] + normals[i * columns + segments]).normalized;
            normals[i * columns] = normals[i * columns + segments] = joined;
        }
        mesh.normals = normals; mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    /// <summary>길을 따라 늘인 둥근 관. radius는 길의 위치(0~1)에 따라 달라질 수 있다.</summary>
    public static Mesh Tube(IList<Vector3> path, Func<float, float> radius, int sides = 16, bool capped = true)
    {
        int count = path.Count, columns = sides + 1;
        var vertices = new List<Vector3>(count * columns + 2); var triangles = new List<int>();
        Vector3 tangent = (path[1] - path[0]).normalized;
        Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < .9f ? Vector3.up : Vector3.right).normalized;
        for (int i = 0; i < count; i++)
        {
            Vector3 next = (i == count - 1 ? path[i] - path[i - 1] : i == 0 ? path[1] - path[0] : path[i + 1] - path[i - 1]).normalized;
            // 앞 단면의 방향을 그대로 옮겨 와서 관이 꼬이지 않게 한다.
            Vector3 axis = Vector3.Cross(tangent, next);
            if (axis.sqrMagnitude > 1e-10f) { normal = Quaternion.AngleAxis(Mathf.Asin(Mathf.Clamp(axis.magnitude, -1f, 1f)) * Mathf.Rad2Deg, axis.normalized) * normal; }
            tangent = next; normal = (normal - Vector3.Dot(normal, tangent) * tangent).normalized;
            Vector3 side = Vector3.Cross(tangent, normal);
            float size = radius(count == 1 ? 0f : i / (float)(count - 1));
            for (int j = 0; j < columns; j++)
            {
                float angle = j / (float)sides * Mathf.PI * 2f;
                vertices.Add(path[i] + (normal * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * size);
            }
        }
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int a = i * columns + j, b = a + 1, c = a + columns, d = c + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(b); triangles.Add(d); triangles.Add(c);
            }
        }
        if (capped)
        {
            int start = vertices.Count; vertices.Add(path[0]);
            for (int j = 0; j < sides; j++) { triangles.Add(start); triangles.Add(j + 1); triangles.Add(j); }
            int end = vertices.Count; vertices.Add(path[count - 1]); int last = (count - 1) * columns;
            for (int j = 0; j < sides; j++) { triangles.Add(end); triangles.Add(last + j); triangles.Add(last + j + 1); }
        }
        var mesh = new Mesh { name = "Tube", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        return Finish(mesh, columns, count, sides);
    }
    private static Mesh Finish(Mesh mesh, int columns, int rings, int sides)
    {
        mesh.RecalculateNormals();
        var normals = mesh.normals;
        for (int i = 0; i < rings; i++)
        {
            Vector3 joined = (normals[i * columns] + normals[i * columns + sides]).normalized;
            normals[i * columns] = normals[i * columns + sides] = joined;
        }
        mesh.normals = normals; mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>모서리를 둥글게 굴린 상자. 가운데가 원점이다.</summary>
    public static Mesh RoundedBox(Vector3 size, float bevel, int steps = 5)
    {
        Vector3 half = size * .5f; bevel = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .98f);
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
        // 한 축의 눈금: 굴린 구간은 촘촘하게, 가운데는 한 칸.
        float[] Marks(float h)
        {
            var marks = new List<float>();
            for (int i = 0; i <= steps; i++) { marks.Add(-h + bevel * (1f - Mathf.Cos(i / (float)steps * Mathf.PI * .5f))); }
            for (int i = steps; i >= 0; i--) { marks.Add(h - bevel * (1f - Mathf.Cos(i / (float)steps * Mathf.PI * .5f))); }
            return marks.ToArray();
        }
        float[] mx = Marks(half.x), my = Marks(half.y), mz = Marks(half.z);
        Vector3 inner = half - Vector3.one * bevel;
        void Face(Func<float, float, Vector3> place, float[] first, float[] second, bool flip)
        {
            int start = vertices.Count, width = first.Length;
            foreach (float b in second)
            {
                foreach (float a in first)
                {
                    Vector3 raw = place(a, b);
                    var core = new Vector3(Mathf.Clamp(raw.x, -inner.x, inner.x), Mathf.Clamp(raw.y, -inner.y, inner.y), Mathf.Clamp(raw.z, -inner.z, inner.z));
                    Vector3 direction = raw - core; direction = direction.sqrMagnitude < 1e-12f ? Vector3.zero : direction.normalized;
                    vertices.Add(core + direction * bevel); normals.Add(direction);
                }
            }
            for (int j = 0; j < second.Length - 1; j++)
            {
                for (int i = 0; i < width - 1; i++)
                {
                    int a = start + j * width + i, b = a + 1, c = a + width, d = c + 1;
                    if (flip) { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(b); triangles.Add(d); triangles.Add(c); }
                    else { triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(b); triangles.Add(c); triangles.Add(d); }
                }
            }
        }
        Face((a, b) => new Vector3(a, half.y, b), mx, mz, false); Face((a, b) => new Vector3(a, -half.y, b), mx, mz, true);
        Face((a, b) => new Vector3(half.x, a, b), my, mz, true); Face((a, b) => new Vector3(-half.x, a, b), my, mz, false);
        Face((a, b) => new Vector3(a, b, half.z), mx, my, true); Face((a, b) => new Vector3(a, b, -half.z), mx, my, false);
        var mesh = new Mesh { name = "Rounded Box", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>잘게 나눈 공을 잡음으로 찌그러뜨린 덩어리. crease가 클수록 접힌 자국이 날카롭다(구긴 종이), 작으면 뭉실하다(휴지·비닐).</summary>
    public static Mesh Crumple(int detail, int seed, float amount, float crease, Vector3 squash)
    {
        // 정이십면체에서 시작해 면을 넷으로 쪼개기를 되풀이한다.
        float t = (1f + Mathf.Sqrt(5f)) * .5f;
        var points = new List<Vector3> { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
        for (int i = 0; i < points.Count; i++) { points[i] = points[i].normalized; }
        var faces = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        var middle = new Dictionary<long, int>();
        int Middle(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (middle.TryGetValue(key, out int index)) { return index; }
            points.Add(((points[a] + points[b]) * .5f).normalized); middle[key] = points.Count - 1;
            return points.Count - 1;
        }
        for (int level = 0; level < detail; level++)
        {
            var next = new List<int>(faces.Count * 4);
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2], ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            faces = next;
        }
        var offset = new Vector3(seed * 12.9898f % 31f, seed * 78.233f % 17f, seed * 37.719f % 23f);
        var vertices = new Vector3[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i]; float sum = 0f, weight = 1f, scale = 1.6f, total = 0f;
            for (int octave = 0; octave < 5; octave++)
            {
                Vector3 q = p * scale + offset * (octave + 1);
                // 세 방향의 잡음을 섞어 공 위의 3차원 잡음처럼 쓴다.
                float n = (Mathf.PerlinNoise(q.x, q.y) + Mathf.PerlinNoise(q.y + 5.2f, q.z + 1.3f) + Mathf.PerlinNoise(q.z + 9.1f, q.x + 2.7f)) / 3f;
                // 접힌 자국: 잡음의 가운데를 뾰족하게 꺾는다.
                float folded = 1f - Mathf.Abs(n * 2f - 1f);
                sum += Mathf.Lerp(n, folded * folded, crease) * weight; total += weight; weight *= .55f; scale *= 2.05f;
            }
            float height = 1f + (sum / total - .5f) * 2f * amount;
            vertices[i] = Vector3.Scale(p * height, squash);
        }
        // 면이 안쪽을 보고 있으면 전부 뒤집는다(정이십면체의 면 순서가 이 좌표계와 반대일 수 있다).
        if (Vector3.Dot(Vector3.Cross(points[faces[1]] - points[faces[0]], points[faces[2]] - points[faces[0]]), points[faces[0]]) < 0f)
        {
            for (int i = 0; i < faces.Count; i += 3) { (faces[i + 1], faces[i + 2]) = (faces[i + 2], faces[i + 1]); }
        }
        var mesh = new Mesh { name = "Crumple", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = vertices; mesh.SetTriangles(faces, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>얇은 종이 한 장(양면). lift(u, v)는 그 자리가 바닥에서 얼마나 들떴는지다. u는 가로, v는 세로(0~1).</summary>
    public static Mesh Sheet(float width, float depth, int columns, int rows, Func<float, float, float> lift)
    {
        int stride = columns + 1, count = stride * (rows + 1);
        var vertices = new Vector3[count * 2]; var uv = new Vector2[count * 2]; var triangles = new List<int>(columns * rows * 12);
        for (int j = 0; j <= rows; j++)
        {
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns, v = j / (float)rows; int index = j * stride + i;
                vertices[index] = vertices[index + count] = new Vector3((u - .5f) * width, lift == null ? 0f : lift(u, v), (v - .5f) * depth);
                uv[index] = uv[index + count] = new Vector2(u, v);
            }
        }
        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < columns; i++)
            {
                int a = j * stride + i, b = a + 1, c = a + stride, d = c + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(b); triangles.Add(c); triangles.Add(d);
                // 뒷면: 같은 자리에 뒤집힌 면을 한 장 더 둔다.
                triangles.Add(a + count); triangles.Add(b + count); triangles.Add(c + count); triangles.Add(b + count); triangles.Add(d + count); triangles.Add(c + count);
            }
        }
        var mesh = new Mesh { name = "Sheet", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>부드럽게 이은 길: 지나갈 점들 사이를 매끈한 곡선으로 채운다.</summary>
    public static List<Vector3> Smooth(IList<Vector3> points, int perSegment = 10)
    {
        var path = new List<Vector3>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(points.Count - 1, i + 2)];
            for (int step = 0; step < perSegment; step++)
            {
                float t = step / (float)perSegment, t2 = t * t, t3 = t2 * t;
                path.Add(.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
            }
        }
        path.Add(points[points.Count - 1]);
        return path;
    }

    /// <summary>도넛 모양 고리.</summary>
    public static Mesh Ring(float radius, float thickness, int segments = 32, int sides = 12)
    {
        var path = new List<Vector3>(segments + 1);
        for (int i = 0; i <= segments; i++) { float angle = i / (float)segments * Mathf.PI * 2f; path.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f)); }
        return Tube(path, _ => thickness, sides, false);
    }
}
