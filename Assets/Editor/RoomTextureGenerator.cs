using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// 방의 벽지와 책상 상판 그림을 4096×4096으로 만들어 재질에 연결한다.
// 벽지: 낡고 얼룩진 줄무늬 벽지. 물 자국이 흘러내리고, 곰팡이가 피고, 군데군데 들떠서 속의 회벽이 보인다.
// 책상: 어두운 나무 널. 결, 널 사이 틈, 긁힌 자국, 컵 자국이 있다.
// 네 그림 모두 상하좌우로 이어 붙여도 이음매가 없다. 메뉴: DonDon/Room/Rebuild 4K textures
public static class RoomTextureGenerator
{
    private const int Size = 4096;
    private const string Folder = "Assets/Resources/Environment/Room";

    [MenuItem("DonDon/Room/Rebuild 4K textures")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        var color = new Color32[Size * Size]; var height = new float[Size * Size];
        Wallpaper(color, height); Save("Wallpaper", color, height, 5.5f);
        Table(color, height); Save("Table", color, height, 4.5f);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string name in new[] { "Wallpaper", "Table" })
        {
            Import(Folder + "/" + name + "_Albedo.jpg", false); Import(Folder + "/" + name + "_Normal.png", true);
        }
        // 벽은 그림 한 장이 너무 크게 펴져서 무늬가 커 보였다. 가로세로 세 번씩 되풀이하고, 색은 눌러서 어둡게 쓴다.
        Assign("Assets/Resources/UI/TradingTerminal/NightShift_DampConcrete.mat", "Wallpaper", new Color(.50f, .50f, .47f), .10f, 1f, 3f);
        Assign("Assets/Resources/UI/TradingTerminal/NightShift_Desk.mat", "Table", new Color(.78f, .76f, .74f), .40f, 1f, 1f);
        AssetDatabase.SaveAssets();
        Debug.Log("[방] 벽지·책상 4K 그림을 다시 만들었습니다: " + Folder);
    }

    // ── 이어 붙여도 끊기지 않는 잡음 ─────────────────────────────────────────────
    private static float Hash(int x, int y, int seed)
    {
        uint h = unchecked((uint)(x * 374761393 + y * 668265263 + seed * 1442695041));
        h = unchecked((h ^ (h >> 13)) * 1274126177u); h ^= h >> 16;
        return (h & 0xFFFFFF) / 16777215f;
    }
    /// <summary>u, v는 0~1. 가로로 px번, 세로로 py번 되풀이되는 격자 잡음.</summary>
    private static float Value(float u, float v, int px, int py, int seed)
    {
        float x = u * px, y = v * py; int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
        float fx = x - xi, fy = y - yi; fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int x0 = ((xi % px) + px) % px, y0 = ((yi % py) + py) % py, x1 = (x0 + 1) % px, y1 = (y0 + 1) % py;
        float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }
    private static float Fbm(float u, float v, int px, int py, int octaves, int seed)
    {
        float sum = 0f, amplitude = .5f, total = 0f;
        for (int i = 0; i < octaves; i++) { sum += Value(u, v, px << i, py << i, seed + i * 17) * amplitude; total += amplitude; amplitude *= .5f; }
        return sum / total;
    }
    private static float Step(float low, float high, float value)
    { float t = Mathf.Clamp01((value - low) / (high - low)); return t * t * (3f - 2f * t); }
    private static float Frac(float value) => value - Mathf.Floor(value);

    // ── 벽지 ─────────────────────────────────────────────────────────────────
    private static void Wallpaper(Color32[] color, float[] height)
    {
        const int stripes = 16;
        Vector3 light = new(.43f, .41f, .32f), band = new(.36f, .35f, .27f), ink = new(.25f, .23f, .17f);
        Vector3 plaster = new(.24f, .23f, .21f), mold = new(.05f, .07f, .05f), rust = new(.30f, .19f, .10f);
        Parallel.For(0, Size, y =>
        {
            float v = (y + .5f) / Size;
            for (int x = 0; x < Size; x++)
            {
                float u = (x + .5f) / Size;
                // 줄무늬: 밝은 띠와 어두운 띠가 번갈아 서고, 띠 사이에 가는 줄이 있다. 밝은 띠 한가운데에는 마름모 무늬가 찍혀 있다.
                float s = Frac(u * stripes), wobble = (Fbm(u, v, 8, 8, 3, 91) - .5f) * .02f;
                bool dark = Frac(u * stripes * .5f + wobble) >= .5f;
                Vector3 paper = dark ? band : light;
                float line = 1f - Step(.012f, .03f, Mathf.Min(s, 1f - s));
                float cu = Mathf.Abs(s - .5f) * 2f, cv = Mathf.Abs(Frac(v * stripes * .5f) - .5f) * 2f, diamond = cu * 1.6f + cv;
                float motif = dark ? 0f : (Step(.30f, .34f, diamond) - Step(.42f, .46f, diamond)) + (1f - Step(.06f, .10f, diamond));
                float mark = Mathf.Clamp01(line + motif * .8f);
                paper = Vector3.Lerp(paper, ink, mark * .75f);
                // 종이 결.
                float fiber = Fbm(u, v, 256, 512, 3, 7);
                paper *= .94f + fiber * .12f;
                // 오래된 때: 넓게 얼룩덜룩하다.
                float grime = Fbm(u, v, 4, 4, 6, 23);
                paper *= Mathf.Lerp(.50f, 1.06f, grime);
                // 물이 흘러내린 자국: 세로로 길게.
                float streak = Step(.52f, .78f, Fbm(u, v, 48, 2, 4, 51)) * Step(.35f, .7f, Fbm(u, v, 6, 3, 3, 53));
                paper = Vector3.Lerp(paper, Vector3.Scale(paper, new Vector3(.62f, .52f, .40f)) + rust * .06f, streak * .8f);
                // 곰팡이: 덩어리지고, 가장자리는 점점이 흩어진다.
                float patch = Fbm(u, v, 5, 5, 6, 77), speck = Fbm(u, v, 180, 180, 2, 79);
                float rot = Step(.60f, .74f, patch) * Step(.30f, .62f, speck + (patch - .6f) * 2.2f);
                paper = Vector3.Lerp(paper, mold, rot * .88f);
                // 들뜬 자리: 벽지가 떨어져 나가 회벽이 드러난다. 가장자리는 그늘지고 말려 올라가 있다.
                float lift = Fbm(u, v, 3, 3, 6, 131) + (Fbm(u, v, 40, 40, 2, 133) - .5f) * .05f;
                float gone = Step(.672f, .680f, lift), edge = Step(.640f, .672f, lift) * (1f - gone), shadow = Step(.680f, .700f, lift);
                Vector3 wall = plaster * (.72f + Fbm(u, v, 96, 96, 4, 137) * .42f) * Mathf.Lerp(.62f, 1f, shadow);
                Vector3 result = Vector3.Lerp(paper * (1f + edge * .22f), wall, gone);
                float h = .55f + fiber * .05f - mark * .03f + edge * .22f - rot * .04f;
                h = Mathf.Lerp(h, .18f + Fbm(u, v, 128, 128, 3, 139) * .10f, gone);
                int index = y * Size + x;
                color[index] = Pack(result); height[index] = h;
            }
        });
    }

    // ── 책상 ─────────────────────────────────────────────────────────────────
    private static void Table(Color32[] color, float[] height)
    {
        const int planks = 6;
        Vector3 deep = new(.085f, .052f, .034f), mid = new(.23f, .145f, .088f), pale = new(.33f, .22f, .14f);
        // 긁힌 자국과 컵 자국은 먼저 따로 그려 둔다.
        var scratch = new float[Size * Size]; var random = new System.Random(4096);
        for (int i = 0; i < 340; i++)
        {
            float x = (float)random.NextDouble() * Size, y = (float)random.NextDouble() * Size, angle = (float)(random.NextDouble() * Math.PI * 2);
            // 대부분 짧고, 가끔 길게 그어진 것이 있다.
            float length = random.NextDouble() < .12 ? 500f + (float)random.NextDouble() * 900f : 30f + (float)random.NextDouble() * 220f;
            float depth = .35f + (float)random.NextDouble() * .65f, bend = ((float)random.NextDouble() - .5f) * .0012f;
            for (float t = 0f; t < length; t += .5f)
            {
                angle += bend; x += Mathf.Cos(angle) * .5f; y += Mathf.Sin(angle) * .5f;
                int px = ((int)x % Size + Size) % Size, py = ((int)y % Size + Size) % Size;
                float fade = Mathf.Sin(t / length * Mathf.PI);
                scratch[py * Size + px] = Mathf.Max(scratch[py * Size + px], depth * fade);
            }
        }
        var rings = new (float x, float y, float radius)[7];
        for (int i = 0; i < rings.Length; i++) { rings[i] = ((float)random.NextDouble(), (float)random.NextDouble(), .028f + (float)random.NextDouble() * .012f); }
        Parallel.For(0, Size, y =>
        {
            float v = (y + .5f) / Size;
            for (int x = 0; x < Size; x++)
            {
                float u = (x + .5f) / Size;
                // 널: 세로로 여섯 장. 널마다 색과 결의 흐름이 조금씩 다르다.
                float across = v * planks; int plank = (int)across; float inside = across - plank;
                float tone = .82f + Hash(plank, 3, 11) * .34f, shift = Hash(plank, 5, 13);
                float uu = Frac(u + shift);
                // 결: 길게 늘어진 잡음으로 휘어진 나이테 줄.
                float warp = Fbm(uu, v, 3, 24, 4, 31 + plank) * 5.5f + Fbm(uu, v, 24, 192, 3, 41) * .5f;
                float ring = .5f + .5f * Mathf.Sin((inside * 9f + warp) * Mathf.PI * 2f);
                float fine = Fbm(uu, v, 12, 768, 3, 47), pore = Fbm(uu, v, 96, 1536, 2, 49);
                float grain = Mathf.Clamp01(ring * .55f + fine * .45f);
                Vector3 wood = Vector3.Lerp(deep, mid, grain) * tone;
                wood *= .86f + pore * .28f;
                // 닳은 자리: 칠이 벗겨져 조금 밝고 푸석하다.
                float worn = Step(.56f, .74f, Fbm(u, v, 3, 3, 5, 61));
                wood = Vector3.Lerp(wood, Vector3.Lerp(wood, pale * tone, .5f), worn * .55f);
                // 얼룩: 무언가 흘리고 닦지 않은 자리.
                float stain = Step(.63f, .78f, Fbm(u, v, 5, 5, 5, 67));
                wood *= 1f - stain * .42f;
                float cup = 0f;
                foreach (var item in rings)
                {
                    float dx = Mathf.Abs(u - item.x), dy = Mathf.Abs(v - item.y); dx = Mathf.Min(dx, 1f - dx); dy = Mathf.Min(dy, 1f - dy);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    cup = Mathf.Max(cup, (1f - Step(.0010f, .0032f, Mathf.Abs(distance - item.radius))) * (.45f + Fbm(u, v, 64, 64, 2, 71) * .55f));
                }
                wood *= 1f - cup * .30f;
                // 널 사이 틈과, 널이 이어지는 자리.
                float gap = 1f - Step(.004f, .012f, Mathf.Min(inside, 1f - inside));
                float joint = 1f - Step(.0006f, .0016f, Mathf.Abs(Frac(u + shift * 3.1f) - .5f));
                float seam = Mathf.Max(gap, joint * .8f);
                wood *= 1f - seam * .82f;
                int index = y * Size + x; float mark = scratch[index];
                // 긁힌 자국은 칠이 벗겨져 밝다.
                wood = Vector3.Lerp(wood, pale * 1.15f, mark * .55f);
                color[index] = Pack(wood);
                height[index] = .5f + grain * .07f + pore * .03f - seam * .40f - mark * .16f - worn * .02f;
            }
        });
    }

    private static Color32 Pack(Vector3 value)
    {
        // 재질은 sRGB 그림을 읽으므로 선형 값을 감마로 바꿔 적는다.
        return new Color32(Channel(value.x), Channel(value.y), Channel(value.z), 255);
    }
    private static byte Channel(float linear) => (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace(Mathf.Clamp01(linear)) * 255f), 0, 255);

    private static void Save(string name, Color32[] color, float[] height, float strength)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.SetPixels32(color); texture.Apply(false);
        File.WriteAllBytes(Folder + "/" + name + "_Albedo.jpg", texture.EncodeToJPG(93));
        // 높이에서 법선을 뽑는다. 가장자리는 반대편으로 이어진다.
        var normal = new Color32[Size * Size];
        Parallel.For(0, Size, y =>
        {
            int up = (y + 1) % Size, down = (y + Size - 1) % Size;
            for (int x = 0; x < Size; x++)
            {
                int right = (x + 1) % Size, left = (x + Size - 1) % Size;
                float dx = (height[y * Size + right] - height[y * Size + left]) * strength * 8f, dy = (height[up * Size + x] - height[down * Size + x]) * strength * 8f;
                var n = new Vector3(-dx, -dy, 1f).normalized;
                normal[y * Size + x] = new Color32((byte)(n.x * 127.5f + 127.5f), (byte)(n.y * 127.5f + 127.5f), (byte)(n.z * 127.5f + 127.5f), 255);
            }
        });
        texture.SetPixels32(normal); texture.Apply(false);
        File.WriteAllBytes(Folder + "/" + name + "_Normal.png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void Import(string path, bool normal)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal; importer.maxTextureSize = Size; importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 8; importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void Assign(string materialPath, string name, Color tint, float smoothness, float bump, float tiling)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + name + "_Albedo.jpg");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + name + "_Normal.png");
        if (material == null || albedo == null || normal == null) { throw new InvalidOperationException("Missing material or texture: " + materialPath); }
        material.SetTexture("_BaseMap", albedo); material.SetTexture("_MainTex", albedo); material.SetTexture("_BumpMap", normal);
        material.EnableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale", bump);
        foreach (string map in new[] { "_BaseMap", "_MainTex", "_BumpMap" }) { material.SetTextureScale(map, new Vector2(tiling, tiling)); }
        material.SetColor("_BaseColor", tint); material.SetColor("_Color", tint); material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
    }
}
