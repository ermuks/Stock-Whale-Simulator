using System.Collections.Generic;
using UnityEngine;

/// <summary>실제 게임 시각을 표시하는 7세그먼트 월시계. 한 메시와 두 재질, 그림자 없는 국소 조명만 사용한다.</summary>
public sealed class MarketWallClock : MonoBehaviour
{
    public MeshFilter digits;
    public Light wallGlow;
    public int DisplayMinute { get; private set; } = -1;
    public string DisplayTime => DisplayMinute < 0 ? "--:--" : (DisplayMinute/60).ToString("00") + ":" + (DisplayMinute%60).ToString("00");
    private Mesh ownedMesh;
    private float next;
    private readonly List<int> lit = new(360), dark = new(360);
    private static readonly int[] Masks = {0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f};
    private static readonly float[] Centers = {-.61f,-.23f,.23f,.61f};
    private static readonly Vector2[] Segments = {new(0,.20f),new(.135f,.10f),new(.135f,-.10f),new(0,-.20f),new(-.135f,-.10f),new(-.135f,.10f),new(0,0)};

    void Start()
    {
        if (digits == null)
        { return; }
        ownedMesh = CreateFace(); ownedMesh.name = "Market Clock Runtime Digits"; ownedMesh.MarkDynamic();
        digits.sharedMesh = ownedMesh; RefreshFromMarket();
    }
    void Update()
    {
        if (Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .25f; RefreshFromMarket();
    }
    public void RefreshFromMarket()
    {
        if (ownedMesh == null)
        { return; }
        int minute = MarketDayController.Instance == null ? 480 : MarketDayController.Instance.ClockMinute;
        minute = Mathf.Clamp(minute,0,1439);
        if (minute == DisplayMinute)
        { return; }
        DisplayMinute = minute; Paint(ownedMesh,minute,lit,dark);
    }
    public static Mesh CreateFace(int minute = 540)
    {
        var vertices = new List<Vector3>(180);
        for (int digit=0;digit<4;digit++)
        {
            for (int segment=0;segment<7;segment++)
            {
                bool vertical = segment != 0 && segment != 3 && segment != 6;
                float length = vertical ? .17f : .24f, thickness = .032f;
                var polygon = new[] {new Vector2(-length/2,0),new Vector2(-length/2+thickness/2,thickness/2),new Vector2(length/2-thickness/2,thickness/2),
                    new Vector2(length/2,0),new Vector2(length/2-thickness/2,-thickness/2),new Vector2(-length/2+thickness/2,-thickness/2)};
                foreach (var point in polygon)
                {
                    Vector2 p = vertical ? new Vector2(-point.y,point.x) : point;
                    p += Segments[segment] + new Vector2(Centers[digit],0);
                    vertices.Add(new Vector3(p.x,p.y,-.107f));
                }
            }
        }
        foreach (float y in new[] {-.075f,.075f})
        {
            vertices.Add(new Vector3(-.018f,y-.018f,-.107f)); vertices.Add(new Vector3(-.018f,y+.018f,-.107f));
            vertices.Add(new Vector3(.018f,y+.018f,-.107f)); vertices.Add(new Vector3(.018f,y-.018f,-.107f));
        }
        var mesh = new Mesh { name = "Clock Seven Segment Face" }; mesh.SetVertices(vertices);
        var normals = new Vector3[vertices.Count];
        for (int i=0;i<normals.Length;i++) { normals[i] = Vector3.back; }
        mesh.normals = normals; mesh.subMeshCount = 2;
        Paint(mesh,Mathf.Clamp(minute,0,1439),new List<int>(360),new List<int>(360)); mesh.RecalculateBounds(); return mesh;
    }
    private static void Paint(Mesh mesh,int minute,List<int> lit,List<int> dark)
    {
        lit.Clear(); dark.Clear(); int hours=minute/60, minutes=minute%60;
        for (int digit=0;digit<4;digit++)
        {
            int number = digit == 0 ? hours/10 : digit == 1 ? hours%10 : digit == 2 ? minutes/10 : minutes%10;
            for (int segment=0;segment<7;segment++)
            {
                var target = (Masks[number] & (1<<segment)) != 0 ? lit : dark;
                int start = (digit*7+segment)*6;
                for (int i=1;i<5;i++) { target.Add(start); target.Add(start+i); target.Add(start+i+1); }
            }
        }
        for (int i=0;i<2;i++)
        {
            int start=168+i*4;
            lit.Add(start); lit.Add(start+1); lit.Add(start+2); lit.Add(start); lit.Add(start+2); lit.Add(start+3);
        }
        mesh.SetTriangles(lit,0,false); mesh.SetTriangles(dark,1,false);
    }
    void OnDestroy()
    {
        if (ownedMesh != null) { Destroy(ownedMesh); }
    }
}
