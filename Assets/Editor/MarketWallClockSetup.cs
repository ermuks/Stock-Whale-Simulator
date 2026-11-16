using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class MarketWallClockSetup
{
    private const string Folder = "Assets/Resources/Environment/MarketClock";
    public const string PrefabPath = Folder + "/MarketWallClock.prefab";
    [MenuItem("DonDon/Environment/Install Market Wall Clock")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
        { throw new InvalidOperationException("Stop Play Mode before installing the wall clock."); }
        var existing = Object.FindAnyObjectByType<MarketWallClock>();
        var wall = GameObject.Find("Area :: Damp Back Wall");
        var desk = Object.FindAnyObjectByType<DeskViewController>();
        if (wall == null || desk == null || desk.ScreenFor(DeskViewTarget.Chart) == null)
        { throw new InvalidOperationException("Existing back wall and central monitor are required."); }
        EnsureFolder(Folder);
        var lit = Material("Clock Red LED",new Color(.14f,.008f,.004f),new Color(1.6f,.018f,.005f),.25f);
        var unlit = Material("Clock Inactive LED",new Color(.028f,.008f,.006f),Color.black,.1f);
        var casing = Material("Clock Housing",new Color(.028f,.031f,.033f),Color.black,.28f);
        var glass = Material("Clock Face",new Color(.009f,.006f,.006f),Color.black,.25f);
        if (existing != null) { AssetDatabase.SaveAssets(); return; }
        string meshPath = Folder + "/SevenSegmentFace.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null) { mesh = MarketWallClock.CreateFace(); AssetDatabase.CreateAsset(mesh,meshPath); }
        var root = new GameObject("Area :: Market Wall Clock");
        var clock = root.AddComponent<MarketWallClock>();
        Box(root.transform,"Clock Housing",new Vector3(1.9f,.64f,.16f),Vector3.zero,casing);
        Box(root.transform,"Clock Glass",new Vector3(1.76f,.52f,.018f),new Vector3(0,0,-.09f),glass);
        var face = new GameObject("Mesh :: Clock HH MM",typeof(MeshFilter),typeof(MeshRenderer)); face.transform.SetParent(root.transform,false);
        clock.digits = face.GetComponent<MeshFilter>(); clock.digits.sharedMesh = mesh;
        var renderer = face.GetComponent<MeshRenderer>(); renderer.sharedMaterials = new[] {lit,unlit}; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        var glow = new GameObject("Light :: Clock Wall Wash"); glow.transform.SetParent(root.transform,false); glow.transform.localPosition = new Vector3(0,0,-.34f);
        clock.wallGlow = glow.AddComponent<Light>(); clock.wallGlow.type = LightType.Point; clock.wallGlow.color = new Color(1f,.065f,.025f);
        clock.wallGlow.intensity = .35f; clock.wallGlow.range = 1.7f; clock.wallGlow.shadows = LightShadows.None;
        clock.wallGlow.lightmapBakeType = LightmapBakeType.Realtime; clock.wallGlow.bounceIntensity = 0f;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root,PrefabPath); Object.DestroyImmediate(root);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var screen = desk.ScreenFor(DeskViewTarget.Chart);
        instance.transform.position = new Vector3(screen.TransformPoint(screen.rect.center).x,7.05f,wall.transform.position.z-.15f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        EditorSceneManager.MarkSceneDirty(instance.scene); AssetDatabase.SaveAssets();
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        { return; }
        string parent = Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    private static Material Material(string name,Color color,Color emission,float smoothness)
    {
        string path = Folder + "/" + name.Replace(' ','_') + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result != null)
        {
            if (emission.maxColorComponent > 0f)
            {
                result.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                result.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(result);
            }
            return result;
        }
        result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        result.SetColor("_BaseColor",color); result.SetFloat("_Smoothness",smoothness);
        if (emission.maxColorComponent > 0f) { result.EnableKeyword("_EMISSION"); result.SetColor("_EmissionColor",emission); }
        // URP derives the emission keyword from AnyEmissive during material validation.
        result.globalIlluminationFlags = emission.maxColorComponent > 0f ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(result,path); return result;
    }
    private static void Box(Transform root,string name,Vector3 size,Vector3 position,Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = "Mesh :: " + name; obj.transform.SetParent(root,false);
        obj.transform.localPosition = position; obj.transform.localScale = size;
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
    }
}
