# 세금·최초 손실 구제·벽시계 코드 기록

2026-11-15. 이번 작업 시작 시 백업과 비교했다. 이전 작업의 미커밋 코드를 보존했다.

## 신규 스크립트 전문

### Assets/Scripts/MarketDay/MarketWallClock.cs

```csharp
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
        int minute = MarketDayController.Instance == null ? 480 : MarketDayController.Instance.GameMinute;
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
```

### Assets/Editor/MarketWallClockSetup.cs

```csharp
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
```

## 데이터 구조 변경

DailyIncomeLedger.TaxRate는 0.0005m다. ClosingReport는 세금·구제·기납부/미납·현금 및 계산 속성을 추가했고, 신규 보고서 버전은 2다. Snapshot도 버전 2로 올리고 reliefDay와 unpaidTax를 보관한다. 이전 버전 1은 소급 과세 없이 읽는다.

DailyIncomeUI에는 입출금·자산·당일 순손익·현금·구제 상태 TMP 참조를 추가했다. 룸 이용료 입력과 시작 화면은 그대로 유지했다.

## 함수 단위 원본과 수정본

### Assets/Scripts/NightShift/DailyIncomeLedger.cs · CloseDay

원본:

```csharp
public ClosingReport CloseDay(int day, long company, decimal realized, decimal assets)
    {
        if (day != Day || day <= SettledThrough || company < 0 || company > MaximumFee)
        { return null; }
        var result = new ClosingReport { day = day, company = company, fees = Escrow, payingMembers = PayingMembers,
            realized = Money(realized), assets = Money(assets) };
        foreach (var ticket in tickets.Values)
        {
            decimal net = ticket.GrossProfit - ticket.Fee;
            if (performance.TryGetValue(ticket.BotId,out decimal prior)) { performance[ticket.BotId] = (prior + net) / 2m; }
            else
            {
                if (performanceOrder.Count >= 2000) { performance.Remove(performanceOrder.Dequeue()); }
                performanceOrder.Enqueue(ticket.BotId); performance.Add(ticket.BotId,net);
            }
        }
        reports.Add(result);
        if (reports.Count > 32) { reports.RemoveAt(0); }
        SettledThrough = day; Escrow = 0; Revision++; return result;
    }
```

수정본:

```csharp
public ClosingReport CloseDay(int day, long company, decimal realized, decimal assets, decimal availableCash = 0m)
    {
        if (day != Day || day <= SettledThrough || company < 0 || company > MaximumFee)
        { return null; }
        decimal tax = decimal.Floor(Math.Max(0m,realized) * TaxRate);
        // Whole-won relief covers the entire first realized loss, including fractional acquisition cost.
        decimal relief = realized < 0m && ReliefDay == 0 ? decimal.Ceiling(-realized) : 0m;
        decimal paid = Math.Min(UnpaidTax + tax, Math.Max(0m,decimal.Floor(availableCash + company + Escrow + relief)));
        decimal remaining = UnpaidTax + tax - paid;
        decimal transfer = company + Escrow + relief - paid;
        var result = new ClosingReport { version = 2, day = day, company = company, fees = Escrow, payingMembers = PayingMembers,
            realized = Money(realized), assets = Money(assets + transfer - remaining), cash = Money(availableCash + transfer),
            tax = Money(tax), relief = Money(relief), priorTax = Money(UnpaidTax), taxPaid = Money(paid), unpaidTax = Money(remaining) };
        if (relief > 0m) { ReliefDay = day; }
        UnpaidTax = remaining;
        foreach (var ticket in tickets.Values)
        {
            decimal net = ticket.GrossProfit - ticket.Fee;
            if (performance.TryGetValue(ticket.BotId,out decimal prior)) { performance[ticket.BotId] = (prior + net) / 2m; }
            else
            {
                if (performanceOrder.Count >= 2000) { performance.Remove(performanceOrder.Dequeue()); }
                performanceOrder.Enqueue(ticket.BotId); performance.Add(ticket.BotId,net);
            }
        }
        reports.Add(result);
        if (reports.Count > 32) { reports.RemoveAt(0); }
        SettledThrough = day; Escrow = 0; Revision++; return result;
    }
```

### Assets/Scripts/NightShift/DailyIncomeLedger.cs · Capture

원본:

```csharp
public Snapshot Capture()
    {
        var saved = new Snapshot { day = Day, settled = SettledThrough, fee = Fee, nextFee = NextFee, revision = Revision, lastOutcome = lastOutcome };
        var list = new List<TicketSnapshot>(tickets.Count);
        foreach (var item in tickets.Values)
        { list.Add(new TicketSnapshot { bot = item.BotId, access = item.Access, fee = item.Fee, unattributed = item.UnattributedFee, gross = Money(item.GrossProfit), cost = Money(item.RealizedCost), assets = Money(item.OpeningAssets) }); }
        saved.tickets = list.ToArray();
        var history = new List<PerformanceSnapshot>(performance.Count);
        foreach (int bot in performanceOrder) { history.Add(new PerformanceSnapshot { bot = bot, net = Money(performance[bot]) }); }
        saved.performance = history.ToArray(); saved.reports = reports.ConvertAll(Copy).ToArray(); return saved;
    }
```

수정본:

```csharp
public Snapshot Capture()
    {
        var saved = new Snapshot { day = Day, settled = SettledThrough, fee = Fee, nextFee = NextFee, revision = Revision, lastOutcome = lastOutcome,
            reliefDay = ReliefDay, unpaidTax = Money(UnpaidTax) };
        var list = new List<TicketSnapshot>(tickets.Count);
        foreach (var item in tickets.Values)
        { list.Add(new TicketSnapshot { bot = item.BotId, access = item.Access, fee = item.Fee, unattributed = item.UnattributedFee, gross = Money(item.GrossProfit), cost = Money(item.RealizedCost), assets = Money(item.OpeningAssets) }); }
        saved.tickets = list.ToArray();
        var history = new List<PerformanceSnapshot>(performance.Count);
        foreach (int bot in performanceOrder) { history.Add(new PerformanceSnapshot { bot = bot, net = Money(performance[bot]) }); }
        saved.performance = history.ToArray(); saved.reports = reports.ConvertAll(Copy).ToArray(); return saved;
    }
```

### Assets/Scripts/NightShift/DailyIncomeLedger.cs · Copy

원본:

```csharp
private static ClosingReport Copy(ClosingReport item) => new ClosingReport { day = item.day, company = item.company, fees = item.fees, payingMembers = item.payingMembers, realized = item.realized, assets = item.assets };
```

수정본:

```csharp
private static ClosingReport Copy(ClosingReport item) => new ClosingReport { version = item.version, day = item.day, company = item.company, fees = item.fees, payingMembers = item.payingMembers, realized = item.realized, assets = item.assets,
        tax = item.tax, relief = item.relief, taxPaid = item.taxPaid, priorTax = item.priorTax, unpaidTax = item.unpaidTax, cash = item.cash };
```

### Assets/Scripts/NightShift/DailyIncomeLedger.cs · Amount

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
private static decimal Amount(string value) => Read(value,out decimal number) ? number : 0m;
```

### Assets/Scripts/NightShift/DailyIncomeLedger.cs · Whole

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
private static bool Whole(string value) => Read(value,out decimal number) && number >= 0m && number == decimal.Floor(number);
```

### Assets/Scripts/NightShift/DailyIncomeLedger.cs · CanRestore

원본:

```csharp
public bool CanRestore(Snapshot saved,int worldDay)
    {
        if (saved == null || saved.version != 1 || saved.day < 0 || saved.day > worldDay || saved.settled < 0 || saved.settled > saved.day || saved.settled < SettledThrough ||
            saved.revision < Revision || saved.lastOutcome < 0 || saved.fee < 0 || saved.fee > MaximumFee || saved.nextFee < 0 || saved.nextFee > MaximumFee ||
            saved.tickets == null || saved.tickets.Length > 2000 || saved.performance == null || saved.performance.Length > 2000 || saved.reports == null || saved.reports.Length > 32)
        { return false; }
        var seen = new HashSet<int>();
        foreach (var item in saved.tickets)
        {
            if (item == null || item.bot <= 0 || !seen.Add(item.bot) || item.fee < 0 || item.fee > MaximumFee || !item.access && item.fee != 0 || item.unattributed < 0 || item.unattributed > item.fee ||
                !Read(item.gross,out _) || !Read(item.cost,out decimal cost) || cost < 0 || !Read(item.assets,out decimal assets) || assets < 0)
            { return false; }
        }
        seen.Clear();
        foreach (var item in saved.performance)
        { if (item == null || item.bot <= 0 || !seen.Add(item.bot) || !Read(item.net,out _)) { return false; } }
        int previous = 0;
        foreach (var item in saved.reports)
        {
            if (item == null || item.day <= previous || item.day > saved.settled || item.company < 0 || item.company > MaximumFee || item.fees < 0 || item.fees > MaximumFee*2000L ||
                item.payingMembers < 0 || item.payingMembers > 2000 || !Read(item.realized,out _) || !Read(item.assets,out _))
            { return false; }
            previous = item.day;
        }
        return true;
    }
```

수정본:

```csharp
public bool CanRestore(Snapshot saved,int worldDay)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.day < 0 || saved.day > worldDay || saved.settled < 0 || saved.settled > saved.day || saved.settled < SettledThrough ||
            saved.revision < Revision || saved.lastOutcome < 0 || saved.fee < 0 || saved.fee > MaximumFee || saved.nextFee < 0 || saved.nextFee > MaximumFee ||
            saved.tickets == null || saved.tickets.Length > 2000 || saved.performance == null || saved.performance.Length > 2000 || saved.reports == null || saved.reports.Length > 32)
        { return false; }
        if (saved.reliefDay < ReliefDay || saved.reliefDay < 0 || saved.reliefDay > saved.settled || saved.version == 1 && (saved.reliefDay != 0 || UnpaidTax != 0m) || saved.version == 2 && !Whole(saved.unpaidTax))
        { return false; }
        var seen = new HashSet<int>();
        foreach (var item in saved.tickets)
        {
            if (item == null || item.bot <= 0 || !seen.Add(item.bot) || item.fee < 0 || item.fee > MaximumFee || !item.access && item.fee != 0 || item.unattributed < 0 || item.unattributed > item.fee ||
                !Read(item.gross,out _) || !Read(item.cost,out decimal cost) || cost < 0 || !Read(item.assets,out decimal assets) || assets < 0)
            { return false; }
        }
        seen.Clear();
        foreach (var item in saved.performance)
        { if (item == null || item.bot <= 0 || !seen.Add(item.bot) || !Read(item.net,out _)) { return false; } }
        int previous = 0;
        foreach (var item in saved.reports)
        {
            if (item == null || item.day <= previous || item.day > saved.settled || item.company < 0 || item.company > MaximumFee || item.fees < 0 || item.fees > MaximumFee*2000L ||
                item.payingMembers < 0 || item.payingMembers > 2000 || !Read(item.realized,out _) || !Read(item.assets,out _))
            { return false; }
            if (item.version < 1 || item.version > 2)
            { return false; }
            if (item.version == 2)
            {
                if (!Whole(item.tax) || !Whole(item.relief) || !Whole(item.taxPaid) || !Whole(item.priorTax) || !Whole(item.unpaidTax) || !Read(item.cash,out decimal cash) || cash < 0m ||
                    item.Tax != decimal.Floor(Math.Max(0m,item.Realized)*TaxRate) || item.TaxPaid > item.PriorTax + item.Tax || item.UnpaidTax != item.PriorTax + item.Tax - item.TaxPaid ||
                    item.Relief > 0m && (saved.reliefDay != item.day || item.Realized >= 0m || item.Relief != decimal.Ceiling(-item.Realized)))
                { return false; }
            }
            previous = item.day;
        }
        if (saved.reports.Length > 0 && saved.reports[saved.reports.Length-1].version == 2 && saved.reports[saved.reports.Length-1].UnpaidTax != Amount(saved.unpaidTax))
        { return false; }
        return true;
    }
```

### Assets/Scripts/NightShift/DailyIncomeLedger.cs · Restore

원본:

```csharp
public bool Restore(Snapshot saved,int worldDay)
    {
        if (!CanRestore(saved,worldDay))
        { return false; }
        Day = saved.day; SettledThrough = saved.settled; Fee = saved.fee; NextFee = saved.nextFee; Revision = saved.revision; lastOutcome = saved.lastOutcome;
        tickets.Clear(); performance.Clear(); performanceOrder.Clear(); reports.Clear(); Escrow = 0; PayingMembers = 0;
        foreach (var item in saved.tickets)
        {
            Read(item.gross,out decimal gross); Read(item.cost,out decimal cost); Read(item.assets,out decimal assets);
            tickets.Add(item.bot,new Ticket { BotId = item.bot, Access = item.access, Fee = item.fee, UnattributedFee = item.unattributed, GrossProfit = gross, RealizedCost = cost, OpeningAssets = assets });
            if (Day > SettledThrough) { Escrow += item.fee; }
            if (item.fee > 0) { PayingMembers++; }
        }
        foreach (var item in saved.performance) { Read(item.net,out decimal net); performance.Add(item.bot,net); performanceOrder.Enqueue(item.bot); }
        foreach (var item in saved.reports) { reports.Add(Copy(item)); }
        return true;
    }
```

수정본:

```csharp
public bool Restore(Snapshot saved,int worldDay)
    {
        if (!CanRestore(saved,worldDay))
        { return false; }
        Day = saved.day; SettledThrough = saved.settled; Fee = saved.fee; NextFee = saved.nextFee; Revision = saved.revision; lastOutcome = saved.lastOutcome;
        ReliefDay = saved.reliefDay; UnpaidTax = saved.version == 1 ? 0m : Amount(saved.unpaidTax);
        tickets.Clear(); performance.Clear(); performanceOrder.Clear(); reports.Clear(); Escrow = 0; PayingMembers = 0;
        foreach (var item in saved.tickets)
        {
            Read(item.gross,out decimal gross); Read(item.cost,out decimal cost); Read(item.assets,out decimal assets);
            tickets.Add(item.bot,new Ticket { BotId = item.bot, Access = item.access, Fee = item.fee, UnattributedFee = item.unattributed, GrossProfit = gross, RealizedCost = cost, OpeningAssets = assets });
            if (Day > SettledThrough) { Escrow += item.fee; }
            if (item.fee > 0) { PayingMembers++; }
        }
        foreach (var item in saved.performance) { Read(item.net,out decimal net); performance.Add(item.bot,net); performanceOrder.Enqueue(item.bot); }
        foreach (var item in saved.reports) { reports.Add(Copy(item)); }
        return true;
    }
```

### Assets/Scripts/NightShift/DailyIncomeController.cs · Settle

원본:

```csharp
public IEnumerator Settle(int day)
    {
        var account = PlayerAccounts.Instance == null ? StockManager.Instance?.playerAccount : PlayerAccounts.Instance.primary;
        var market = MarketDayController.Instance;
        if (account == null || market == null || market.Phase != MarketPhase.Closing || market.Day != day || market.IntegrityFailed || day != Ledger.Day || day <= Ledger.SettledThrough || settlingDay == day)
        { yield break; }
        settlingDay = day;
        try
        {
            int count = 0;
            foreach (var ticket in Ledger.Tickets)
            {
                long fee = Ledger.TakeUnattributedFee(ticket.BotId);
                var bot = MarketDayController.Instance?.FindBot(ticket.BotId);
                if (fee > 0 && bot?.Social != null)
                {
                    bot.Social.Psychology.RecordRoomFee(InvestorSociety.Instance.NextEventId(),fee,ticket.OpeningAssets,Rules);
                    InvestorSociety.Instance.Trace(bot,bot.Social.Psychology.LastCause);
                }
                if (++count % 24 == 0) { yield return null; }
            }
            decimal realized = (PlayerProgression.Instance == null ? 0m : PlayerProgression.Instance.RealizedProfit) - realizedAtOpen;
            decimal deposit = Rules.companyIncome + Ledger.Escrow;
            decimal equity = account.Equity() + deposit + (PlayerAccounts.Instance?.automated == null ? 0m : PlayerAccounts.Instance.automated.Equity());
            var report = Ledger.CloseDay(day,Rules.companyIncome,realized,equity);
            if (report == null)
            { yield break; }
            // 외부 사업 수익/회원료 입금은 매매 수익률 및 리딩방 해금용 실현손익으로 계산하지 않는다.
            account.cash += report.Deposit;
            MarketEventFeed.Instance?.Notify("장 마감 · 회사 +" + report.company.ToString("N0") + "원 · 이용료 +" + report.fees.ToString("N0") + "원");
        }
        finally { settlingDay = 0; }
    }
```

수정본:

```csharp
public IEnumerator Settle(int day)
    {
        var account = PlayerAccounts.Instance == null ? StockManager.Instance?.playerAccount : PlayerAccounts.Instance.primary;
        var market = MarketDayController.Instance;
        if (account == null || market == null || market.Phase != MarketPhase.Closing || market.Day != day || market.IntegrityFailed || day != Ledger.Day || day <= Ledger.SettledThrough || settlingDay == day)
        { yield break; }
        settlingDay = day;
        try
        {
            int count = 0;
            foreach (var ticket in Ledger.Tickets)
            {
                long fee = Ledger.TakeUnattributedFee(ticket.BotId);
                var bot = MarketDayController.Instance?.FindBot(ticket.BotId);
                if (fee > 0 && bot?.Social != null)
                {
                    bot.Social.Psychology.RecordRoomFee(InvestorSociety.Instance.NextEventId(),fee,ticket.OpeningAssets,Rules);
                    InvestorSociety.Instance.Trace(bot,bot.Social.Psychology.LastCause);
                }
                if (++count % 24 == 0) { yield return null; }
            }
            decimal realized = (PlayerProgression.Instance == null ? 0m : PlayerProgression.Instance.RealizedProfit) - realizedAtOpen;
            decimal equity = account.Equity() + (PlayerAccounts.Instance?.automated == null ? 0m : PlayerAccounts.Instance.automated.Equity());
            decimal available = Math.Max(0m,account.cash - account.ReservedCash);
            var report = Ledger.CloseDay(day,Rules.companyIncome,realized,equity,available);
            if (report == null)
            { yield break; }
            // 외부 사업 수익/회원료 입금은 매매 수익률 및 리딩방 해금용 실현손익으로 계산하지 않는다.
            account.cash += report.Transfer;
            MarketEventFeed.Instance?.Notify("장 마감 정산 " + InvestmentDashboard.Signed(report.Transfer) + "원");
        }
        finally { settlingDay = 0; }
    }
```

### Assets/Scripts/NightShift/DailyIncomeUI.cs · InstallTablet

원본:

```csharp
public static void InstallTablet(TabletAccountUI tablet)
    {
        if (tablet.second)
        { return; }
        var root = tablet.dashboard.transform;
        var view = root.gameObject.AddComponent<DailyIncomeUI>(); view.Initialize();
        view.Action(root,"Income Statement","정산",.21f,.95f,.18f,.049f,view.Open,false,27);
        view.panel = view.Box("Income Statement",root,0,0,1,1,true).gameObject;
        root = view.panel.transform;
        view.company = view.Label(root,"Company","",.035f,.035f,.64f,.06f,36);
        view.day = view.Label(root,"Closing Day","",.55f,.105f,.41f,.045f,25); view.day.alignment = TextAlignmentOptions.MidlineRight; view.day.color = Muted;
        view.Action(root,"Close Statement","닫기",.79f,.035f,.17f,.065f,view.Close,false,27);
        string[] captions = {"회사 수익","리딩방 이용료","당일 실현손익","총자산"}; view.values = new TextMeshProUGUI[captions.Length];
        for (int i = 0; i < captions.Length; i++) { view.values[i] = view.Metric(root,captions[i],.20f+i*.09f,29); }
        view.Label(root,"Statement History","최근 정산",.045f,.62f,.91f,.045f,28);
        view.history = new TextMeshProUGUI[5];
        for (int i = 0; i < view.history.Length; i++)
        { view.history[i] = view.Label(root,"Statement Row " + i,"",.045f,.69f+i*.054f,.91f,.045f,25); view.history[i].color = Muted; }
        view.panel.SetActive(false);
    }
```

수정본:

```csharp
public static void InstallTablet(TabletAccountUI tablet)
    {
        if (tablet.second)
        { return; }
        var root = tablet.dashboard.transform;
        var view = root.gameObject.AddComponent<DailyIncomeUI>(); view.Initialize();
        view.Action(root,"Income Statement","정산",.21f,.95f,.18f,.049f,view.Open,false,27);
        view.panel = view.Box("Income Statement",root,0,0,1,1,true).gameObject;
        root = view.panel.transform;
        view.company = view.Label(root,"Company","",.045f,.025f,.62f,.055f,32);
        view.day = view.Label(root,"Closing Day","",.60f,.095f,.35f,.045f,24); view.day.alignment = TextAlignmentOptions.MidlineRight; view.day.color = Muted;
        view.Action(root,"Close Statement","닫기",.84f,.025f,.11f,.06f,view.Close,false,26);
        view.Label(root,"Transfer Label","정산 입출금",.045f,.115f,.49f,.05f,27).color = Muted;
        view.transfer = view.Number(root,"Settlement Transfer",.045f,.18f,.49f,.10f,50);
        view.Label(root,"Assets Label","마감 총자산",.61f,.17f,.34f,.05f,25).color = Muted;
        view.closingAssets = view.Number(root,"Closing Assets",.61f,.23f,.34f,.065f,31);
        view.Rule(root,.32f);
        string[] captions = {"실현 매매손익","회사 수익","리딩방 이용료","최초 손실 구제","매매세 0.05%","이전 미납","이번 납부","남은 미납"};
        view.values = new TextMeshProUGUI[captions.Length];
        for (int i = 0; i < captions.Length; i++)
        {
            float x = i < 4 ? .045f : .535f, y = .36f + (i%4)*.078f;
            view.Label(root,captions[i]+" Label",captions[i],x,y,.165f,.06f,26).color = Muted;
            view.values[i] = view.Number(root,captions[i]+" Value",x+.17f,y,.245f,.06f,29);
        }
        view.reliefState = view.Label(root,"Relief State","",.045f,.665f,.905f,.035f,21); view.reliefState.color = Muted;
        view.Rule(root,.717f);
        view.Label(root,"Day Result Label","당일 순손익",.045f,.746f,.165f,.06f,27).color = Muted;
        view.dayResult = view.Number(root,"Day Result",.215f,.746f,.245f,.06f,30);
        view.Label(root,"Closing Cash Label","기본계좌 현금",.535f,.746f,.165f,.06f,27).color = Muted;
        view.closingCash = view.Number(root,"Closing Cash",.705f,.746f,.245f,.06f,30);
        view.history = new TextMeshProUGUI[2];
        for (int i = 0; i < view.history.Length; i++)
        { view.history[i] = view.Label(root,"Statement Row " + i,"",.045f,.855f+i*.052f,.905f,.04f,23); view.history[i].color = Muted; }
        view.panel.SetActive(false);
    }
```

### Assets/Scripts/NightShift/DailyIncomeUI.cs · Refresh

원본:

```csharp
private void Refresh()
    {
        var income = DailyIncomeController.Instance;
        if (income == null)
        { return; }
        var ledger = income.Ledger;
        if (roomMode)
        {
            DeviceTerminalUI.Set(currentFee,ledger.Fee.ToString("N0"));
            DeviceTerminalUI.Set(paidCount,ledger.PayingMembers.ToString("N0"));
            DeviceTerminalUI.Set(escrow,ledger.Escrow.ToString("N0")); return;
        }
        DeviceTerminalUI.Set(company,income.CompanyName);
        var reports = ledger.Reports;
        var last = reports.Count == 0 ? null : reports[reports.Count-1];
        DeviceTerminalUI.Set(day,last == null ? "정산 전" : "DAY " + last.day + " · 장 마감");
        string[] texts = last == null ? new[] {"—","—","—","—"} : new[] {
            last.company.ToString("N0"),last.fees.ToString("N0"),InvestmentDashboard.Signed(decimal.Parse(last.realized,System.Globalization.CultureInfo.InvariantCulture)),decimal.Parse(last.assets,System.Globalization.CultureInfo.InvariantCulture).ToString("N0")};
        for (int i=0;i<values.Length;i++) { DeviceTerminalUI.Set(values[i],texts[i]); }
        for (int i=0;i<history.Length;i++)
        {
            int index = reports.Count-1-i;
            DeviceTerminalUI.Set(history[i],index < 0 ? "" : "DAY " + reports[index].day + "     회사 " + reports[index].company.ToString("N0") + "원     이용료 " + reports[index].fees.ToString("N0") + "원");
        }
    }
```

수정본:

```csharp
private void Refresh()
    {
        var income = DailyIncomeController.Instance;
        if (income == null)
        { return; }
        var ledger = income.Ledger;
        if (roomMode)
        {
            DeviceTerminalUI.Set(currentFee,ledger.Fee.ToString("N0"));
            DeviceTerminalUI.Set(paidCount,ledger.PayingMembers.ToString("N0"));
            DeviceTerminalUI.Set(escrow,ledger.Escrow.ToString("N0")); return;
        }
        DeviceTerminalUI.Set(company,income.CompanyName);
        var reports = ledger.Reports;
        var last = reports.Count == 0 ? null : reports[reports.Count-1];
        DeviceTerminalUI.Set(day,last == null ? "정산 전" : "DAY " + last.day + " · 장 마감");
        string[] texts = last == null ? new[] {"—","—","—","—","—","—","—","—"} : new[] {
            InvestmentDashboard.Signed(last.Realized),last.company.ToString("N0"),last.fees.ToString("N0"),last.Relief.ToString("N0"),
            last.Tax.ToString("N0"),last.PriorTax.ToString("N0"),last.TaxPaid.ToString("N0"),last.UnpaidTax.ToString("N0")};
        for (int i=0;i<values.Length;i++) { DeviceTerminalUI.Set(values[i],texts[i]); }
        DeviceTerminalUI.Set(transfer,last == null ? "—" : InvestmentDashboard.Signed(last.Transfer));
        DeviceTerminalUI.Set(closingAssets,last == null ? "—" : last.Assets.ToString("N0"));
        DeviceTerminalUI.Set(dayResult,last == null ? "—" : InvestmentDashboard.Signed(last.DayResult));
        DeviceTerminalUI.Set(closingCash,last == null || last.version < 2 ? "—" : last.Cash.ToString("N0"));
        DeviceTerminalUI.Set(reliefState,ledger.ReliefDay == 0 ? "손실 구제 1회 남음" : "손실 구제 사용 · DAY " + ledger.ReliefDay);
        if (last != null)
        {
            values[0].color = InvestmentDashboard.ProfitColor(last.Realized); values[3].color = last.Relief > 0m ? new Color32(77,182,190,255) : Ink;
            values[7].color = last.UnpaidTax > 0m ? new Color32(222,164,96,255) : Ink;
            transfer.color = InvestmentDashboard.ProfitColor(last.Transfer); dayResult.color = InvestmentDashboard.ProfitColor(last.DayResult);
        }
        for (int i=0;i<history.Length;i++)
        {
            int index = reports.Count-1-i;
            DeviceTerminalUI.Set(history[i],index < 0 ? "" : "DAY " + reports[index].day + "     정산 " + InvestmentDashboard.Signed(reports[index].Transfer) + "원     세금 " + reports[index].Tax.ToString("N0") + "원     구제 " + reports[index].Relief.ToString("N0") + "원");
        }
    }
```

### Assets/Scripts/NightShift/DailyIncomeUI.cs · Number

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
private TextMeshProUGUI Number(Transform parent,string name,float x,float y,float width,float height,float size)
    {
        var value = Label(parent,name,"",x,y,width-.029f,height,size); TradingOrderView.Numeric(value); value.alignment = TextAlignmentOptions.MidlineRight;
        var unit = Label(parent,name+" Unit","원",x+width-.023f,y,.023f,height,24); unit.alignment = TextAlignmentOptions.MidlineRight; unit.color = Muted;
        return value;
    }
```

### Assets/Scripts/NightShift/DailyIncomeUI.cs · Rule

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
private void Rule(Transform parent,float y)
    {
        var line = Box("Settlement Divider",parent,.045f,y,.905f,.0015f,false).gameObject.AddComponent<Image>();
        line.color = new Color32(53,70,77,170); line.raycastTarget = false;
    }
```

### Assets/Scripts/NightShift/PlayerAccounts.cs · EvaluateUnlock

원본:

```csharp
public void EvaluateUnlock()
    {
        if (!Ready || AutomatedUnlocked || primary == null || primary.Equity() <= unlockAssets)
        { return; }
        AutomatedUnlocked = true;
        Unlocked?.Invoke();
        MarketEventFeed.Instance?.Notify("2번 계좌가 해금되었습니다. 계좌 간 현금 이체와 자동 운용을 사용할 수 있습니다.");
    }
```

수정본:

```csharp
public void EvaluateUnlock()
    {
        if (!Ready || AutomatedUnlocked || primary == null || primary.Equity() - TaxDebt(primary) <= unlockAssets)
        { return; }
        AutomatedUnlocked = true;
        Unlocked?.Invoke();
        MarketEventFeed.Instance?.Notify("2번 계좌가 해금되었습니다. 계좌 간 현금 이체와 자동 운용을 사용할 수 있습니다.");
    }
```

### Assets/Scripts/NightShift/PlayerAccounts.cs · TaxDebt

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
public static decimal TaxDebt(Trader account)
    {
        bool primaryAccount = account != null && (Instance != null ? account == Instance.primary : StockManager.Instance != null && account == StockManager.Instance.playerAccount);
        return primaryAccount ? DailyIncomeController.Instance?.Ledger.UnpaidTax ?? 0m : 0m;
    }
```

### Assets/Scripts/NightShift/PlayerAccounts.cs · Available

원본:

```csharp
public static decimal Available(Trader account) => account == null ? 0m : Math.Max(0m, account.cash - account.ReservedCash);
```

수정본:

```csharp
public static decimal Available(Trader account) => account == null ? 0m : Math.Max(0m, account.cash - account.ReservedCash - TaxDebt(account));
```

### Assets/Scripts/NightShift/InvestmentDashboard.cs · Refresh

원본:

```csharp
public void Refresh()
    {
        bool ready = account != null && StockManager.Instance != null && StockManager.Instance.SetupConfirmed;
        if (!ready)
        {
            foreach (var row in pool) { row.gameObject.SetActive(false); }
            DeviceTerminalUI.Set(valuation,"—"); DeviceTerminalUI.Set(profit,"—"); DeviceTerminalUI.Set(available,"—"); DeviceTerminalUI.Set(total,"—");
            DeviceTerminalUI.Set(cost,"잔고 확인 중"); DeviceTerminalUI.Set(count,"보유 종목 —");
            empty.gameObject.SetActive(true); DeviceTerminalUI.Set(empty,"잔고를 불러오는 중입니다"); detail.interactable = false;
            return;
        }
        account.CopyOwnedStocks(owned);
        bool membership = owned.Count != ordered.Count;
        foreach (var stock in owned) { if (!ordered.Contains(stock)) { membership = true; } }
        if (membership)
        {
            ordered.RemoveAll(stock => !owned.Contains(stock));
            foreach (var stock in owned) { if (!ordered.Contains(stock)) { ordered.Add(stock); } }
        }
        // Re-sort only for explicit sorting or changed membership, never for price ticks.
        if ((!sorted || membership) && ordered.Count > 0)
        {
            ordered.Sort((a,b) => {
                int result = SortMode == 1 ? Return(b).CompareTo(Return(a)) : SortMode == 2 ? string.Compare(a.stockName,b.stockName,StringComparison.Ordinal) : (account.GetOwnedVolume(b)*b.currentPrice).CompareTo(account.GetOwnedVolume(a)*a.currentPrice);
                return result != 0 ? result : string.Compare(a.symbol,b.symbol,StringComparison.Ordinal);
            });
            sorted = true; membership = true;
        }
        decimal stockValue = 0m, basisTotal = 0m;
        bool priced = true;
        foreach (var stock in owned)
        {
            stockValue += account.GetOwnedVolume(stock) * stock.currentPrice;
            basisTotal += account.GetOwnedCost(stock);
            priced &= stock.currentPrice > 0m;
        }
        decimal pnl = stockValue - basisTotal;
        DeviceTerminalUI.Set(valuation, priced ? DeviceTerminalUI.Money(stockValue) : "—");
        DeviceTerminalUI.Set(profit, priced ? Signed(pnl) + "원  (" + Rate(pnl,basisTotal) + ")" : "평가가격 확인 중"); profit.color = priced ? ProfitColor(pnl) : Ink;
        DeviceTerminalUI.Set(available, DeviceTerminalUI.Money(PlayerAccounts.Available(account)));
        DeviceTerminalUI.Set(total, priced ? DeviceTerminalUI.Money(account.cash + stockValue) : "—");
        DeviceTerminalUI.Set(count,"보유 종목  " + owned.Count);
        DeviceTerminalUI.Set(cost,"총 매수원금  " + DeviceTerminalUI.Money(basisTotal));
        DeviceTerminalUI.Set(sortLabel,SortMode == 0 ? "평가금 높은 순" : SortMode == 1 ? "수익률 높은 순" : "종목명 순");
        empty.gameObject.SetActive(owned.Count == 0); DeviceTerminalUI.Set(empty,"아직 보유한 종목이 없습니다");
        while (pool.Count < ordered.Count)
        {
            var row = Instantiate(rowTemplate,content); row.name = "Button :: Holding " + pool.Count; row.Initialize(this); pool.Add(row);
        }
        if (membership)
        {
            Vector2 position = content.anchoredPosition;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ordered.Count * rowHeight);
            content.anchoredPosition = new Vector2(position.x, Mathf.Clamp(position.y,0f,Mathf.Max(0f,ordered.Count*rowHeight-scroll.viewport.rect.height)));
        }
        for (int i = 0; i < pool.Count; i++)
        {
            bool active = i < ordered.Count;
            if (pool[i].gameObject.activeSelf != active) { pool[i].gameObject.SetActive(active); }
            if (!active)
            { continue; }
            if (membership)
            {
                var rect = (RectTransform)pool[i].transform;
                rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f,1);
                rect.sizeDelta = new Vector2(0,rowHeight); rect.anchoredPosition = new Vector2(0,-i*rowHeight);
            }
            pool[i].Bind(ordered[i],account,Selected == ordered[i]);
        }
        detail.interactable = owned.Contains(Selected);
    }
```

수정본:

```csharp
public void Refresh()
    {
        bool ready = account != null && StockManager.Instance != null && StockManager.Instance.SetupConfirmed;
        if (!ready)
        {
            foreach (var row in pool) { row.gameObject.SetActive(false); }
            DeviceTerminalUI.Set(valuation,"—"); DeviceTerminalUI.Set(profit,"—"); DeviceTerminalUI.Set(available,"—"); DeviceTerminalUI.Set(total,"—");
            DeviceTerminalUI.Set(cost,"잔고 확인 중"); DeviceTerminalUI.Set(count,"보유 종목 —");
            empty.gameObject.SetActive(true); DeviceTerminalUI.Set(empty,"잔고를 불러오는 중입니다"); detail.interactable = false;
            return;
        }
        account.CopyOwnedStocks(owned);
        bool membership = owned.Count != ordered.Count;
        foreach (var stock in owned) { if (!ordered.Contains(stock)) { membership = true; } }
        if (membership)
        {
            ordered.RemoveAll(stock => !owned.Contains(stock));
            foreach (var stock in owned) { if (!ordered.Contains(stock)) { ordered.Add(stock); } }
        }
        // Re-sort only for explicit sorting or changed membership, never for price ticks.
        if ((!sorted || membership) && ordered.Count > 0)
        {
            ordered.Sort((a,b) => {
                int result = SortMode == 1 ? Return(b).CompareTo(Return(a)) : SortMode == 2 ? string.Compare(a.stockName,b.stockName,StringComparison.Ordinal) : (account.GetOwnedVolume(b)*b.currentPrice).CompareTo(account.GetOwnedVolume(a)*a.currentPrice);
                return result != 0 ? result : string.Compare(a.symbol,b.symbol,StringComparison.Ordinal);
            });
            sorted = true; membership = true;
        }
        decimal stockValue = 0m, basisTotal = 0m;
        bool priced = true;
        foreach (var stock in owned)
        {
            stockValue += account.GetOwnedVolume(stock) * stock.currentPrice;
            basisTotal += account.GetOwnedCost(stock);
            priced &= stock.currentPrice > 0m;
        }
        decimal pnl = stockValue - basisTotal;
        DeviceTerminalUI.Set(valuation, priced ? DeviceTerminalUI.Money(stockValue) : "—");
        DeviceTerminalUI.Set(profit, priced ? Signed(pnl) + "원  (" + Rate(pnl,basisTotal) + ")" : "평가가격 확인 중"); profit.color = priced ? ProfitColor(pnl) : Ink;
        DeviceTerminalUI.Set(available, DeviceTerminalUI.Money(PlayerAccounts.Available(account)));
        DeviceTerminalUI.Set(total, priced ? DeviceTerminalUI.Money(account.cash + stockValue - PlayerAccounts.TaxDebt(account)) : "—");
        DeviceTerminalUI.Set(count,"보유 종목  " + owned.Count);
        DeviceTerminalUI.Set(cost,"총 매수원금  " + DeviceTerminalUI.Money(basisTotal));
        DeviceTerminalUI.Set(sortLabel,SortMode == 0 ? "평가금 높은 순" : SortMode == 1 ? "수익률 높은 순" : "종목명 순");
        empty.gameObject.SetActive(owned.Count == 0); DeviceTerminalUI.Set(empty,"아직 보유한 종목이 없습니다");
        while (pool.Count < ordered.Count)
        {
            var row = Instantiate(rowTemplate,content); row.name = "Button :: Holding " + pool.Count; row.Initialize(this); pool.Add(row);
        }
        if (membership)
        {
            Vector2 position = content.anchoredPosition;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ordered.Count * rowHeight);
            content.anchoredPosition = new Vector2(position.x, Mathf.Clamp(position.y,0f,Mathf.Max(0f,ordered.Count*rowHeight-scroll.viewport.rect.height)));
        }
        for (int i = 0; i < pool.Count; i++)
        {
            bool active = i < ordered.Count;
            if (pool[i].gameObject.activeSelf != active) { pool[i].gameObject.SetActive(active); }
            if (!active)
            { continue; }
            if (membership)
            {
                var rect = (RectTransform)pool[i].transform;
                rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f,1);
                rect.sizeDelta = new Vector2(0,rowHeight); rect.anchoredPosition = new Vector2(0,-i*rowHeight);
            }
            pool[i].Bind(ordered[i],account,Selected == ordered[i]);
        }
        detail.interactable = owned.Contains(Selected);
    }
```

### Assets/Scripts/NightShift/IncumbentExchangeAI.cs · Observe

원본:

```csharp
public bool Observe(float now)
    {
        var manager = StockManager.Instance;
        if (now < NextObservationAt || manager == null || !manager.SetupConfirmed || manager.playerAccount == null || !MarketDayController.CanTrade)
        { return false; }
        NextObservationAt = now + Mathf.Clamp(policy.observationSeconds,.5f,60f);
        var player = manager.playerAccount;
        Snapshot.ObservedAt = now; Snapshot.Cash = player.cash; Snapshot.ReservedCash = player.ReservedCash; Snapshot.Equity = player.Equity();
        var secondary = PlayerAccounts.Instance == null ? null : PlayerAccounts.Instance.automated;
        if (secondary != null)
        { Snapshot.Cash += secondary.cash; Snapshot.ReservedCash += secondary.ReservedCash; Snapshot.Equity += secondary.Equity(); }
        Snapshot.AccountIntents.Clear();
        if (manual != null)
        {
            foreach (var request in manual.ActiveOrders)
            { Snapshot.AccountIntents.Add((request.Stock.symbol,request.Buy,request.Limit,request.Unsubmitted + request.PendingVolume)); }
        }
        var autoManual = PlayerAccounts.Instance == null ? null : PlayerAccounts.Instance.automatedOrders;
        if (autoManual != null)
        {
            foreach (var request in autoManual.ActiveOrders)
            { Snapshot.AccountIntents.Add((request.Stock.symbol,request.Buy,request.Limit,request.Unsubmitted + request.PendingVolume)); }
        }
        Snapshot.SelectedSymbol = manager.CurrentStock == null ? "" : manager.CurrentStock.symbol;
        Snapshot.OwnedCompanySymbol = player.OwnedCompany == null ? "" : player.OwnedCompany.symbol;
        var career = PlayerProgression.Instance;
        Snapshot.LeadingRights = career != null && career.CanOperateRooms;
        Snapshot.Turnover = career == null ? 0m : career.Turnover; Snapshot.RealizedProfit = career == null ? 0m : career.RealizedProfit;
        var operation = TradingOperationController.Instance;
        Snapshot.OperationMode = operation == null ? "" : operation.ModeName;
        Snapshot.OperationSymbol = operation == null || operation.Target == null ? "" : operation.Target.symbol;
        Snapshot.OperationBudget = operation == null ? 0m : operation.Budget;
        Snapshot.OperationSpent = operation == null ? 0m : operation.Spent;
        Snapshot.OperationInventory = operation == null ? 0m : operation.Inventory;
        Snapshot.OrderSymbol = manual?.Latest != null ? manual.Latest.Stock.symbol : "";
        Snapshot.ManualBuy = manual != null && manual.Buy;
        Snapshot.ManualRemaining = manual != null && manual.Active ? manual.Unsubmitted + manual.PendingVolume : 0m;
        Snapshot.ManualLimit = manual == null ? 0m : manual.Limit; Snapshot.ManualBudget = manual == null ? 0m : manual.Budget;
        Snapshot.Members = Snapshot.Supporters = Snapshot.Opponents = Snapshot.ControlledCompanies = 0;
        Snapshot.RoomCommands.Clear(); Snapshot.RoomMembers.Clear(); Snapshot.Relationships.Clear(); Snapshot.OpenOrders.Clear(); roomPressure.Clear();
        if (InvestorSociety.Instance != null)
        {
            foreach (var room in InvestorSociety.Instance.Rooms)
            {
                if (!room.PlayerOwned)
                { continue; }
                Snapshot.Members += room.Members.Count;
                foreach (int member in room.Members)
                { Snapshot.RoomMembers.Add((room.Id,member)); }
                if (room.Command != null && !room.Command.Canceled)
                { Snapshot.RoomCommands.Add((room.Id,room.Command.Stock.symbol,room.Command.Buy,room.Command.Sell)); }
            }
            if (InvestorSociety.Instance.Orders != null)
            {
                foreach (var plan in InvestorSociety.Instance.Orders.Plans)
                {
                    if (plan.Command.Canceled || plan.Bot == null || !plan.Bot.isActiveAndEnabled)
                    { continue; }
                    decimal intent = plan.State == LeadingRoomOrders.Stage.AwaitBuy || plan.State == LeadingRoomOrders.Stage.Buying ? Math.Max(0m,plan.Goal - plan.Bought) : plan.State == LeadingRoomOrders.Stage.AwaitSell || plan.State == LeadingRoomOrders.Stage.Selling ? -plan.Remaining : 0m;
                    roomPressure.TryGetValue(plan.Command.Stock,out decimal previous);
                    roomPressure[plan.Command.Stock] = previous + intent;
                }
            }
        }
        if (MarketDayController.Instance != null)
        {
            foreach (var bot in MarketDayController.Instance.Bots)
            {
                if (bot == null || bot.Social == null)
                { continue; }
                var mind = bot.Social.Psychology;
                Snapshot.Relationships.Add((bot.Social.Id,mind.Room,mind.Trust,mind.Betrayal));
                mind.RefreshQuadrant();
                if (mind.Quadrant == InvestorQuadrant.Loyal) { Snapshot.Supporters++; }
                if (mind.Quadrant == InvestorQuadrant.Hostile) { Snapshot.Opponents++; }
            }
        }
        int positionCount = manager.stocks.Count + manager.UnlistedStocks.Count;
        while (Snapshot.Positions.Count < positionCount)
        { Snapshot.Positions.Add(new Position()); }
        if (Snapshot.Positions.Count > positionCount)
        { Snapshot.Positions.RemoveRange(positionCount, Snapshot.Positions.Count - positionCount); }
        Position strongest = null; decimal strongestPressure = 0m;
        for (int i = 0; i < positionCount; i++)
        {
            var stock = i < manager.stocks.Count ? manager.stocks[i] : manager.UnlistedStocks[i - manager.stocks.Count]; var position = Snapshot.Positions[i];
            position.Stock = stock; position.Owned = player.GetOwnedVolume(stock); position.Average = player.GetOwnedAverage(stock); position.Price = stock.currentPrice;
            if (secondary != null)
            {
                position.Owned += secondary.GetOwnedVolume(stock);
                position.Average = position.Owned > 0m ? (player.GetOwnedCost(stock) + secondary.GetOwnedCost(stock)) / position.Owned : 0m;
            }
            position.Change = previousHoldings.TryGetValue(stock, out decimal previous) ? position.Owned - previous : 0m;
            previousHoldings[stock] = position.Owned;
            position.Ownership = stock.issuedShares > 0m ? position.Owned / stock.issuedShares : 0m;
            if (position.Ownership > .5m) { Snapshot.ControlledCompanies++; }
            stock.orderBook.CopyOwnerOrders(player, orders); position.PendingBuy = position.PendingSell = 0m;
            if (secondary != null) { stock.orderBook.CopyOwnerOrders(secondary,secondaryOrders); orders.AddRange(secondaryOrders); }
            foreach (var order in orders)
            {
                Snapshot.OpenOrders.Add((stock.symbol,order.isBuy,order.price,order.volume,order.timestamp));
                if (order.isBuy) { position.PendingBuy += order.volume; }
                else { position.PendingSell += order.volume; }
            }
            decimal pressure = position.Change + (position.PendingBuy - position.PendingSell) * .25m;
            if (manual != null)
            {
                foreach (var request in manual.ActiveOrders)
                { if (request.Stock == stock) { pressure += request.Unsubmitted * (request.Buy ? .25m : -.25m); } }
            }
            if (autoManual != null)
            {
                foreach (var request in autoManual.ActiveOrders)
                { if (request.Stock == stock) { pressure += request.Unsubmitted * (request.Buy ? .25m : -.25m); } }
            }
            if (roomPressure.TryGetValue(stock,out decimal roomIntent))
            { pressure += roomIntent * .1m; }
            if (stock.AcceptsOrders && Math.Abs(pressure) > Math.Abs(strongestPressure))
            { strongest = position; strongestPressure = pressure; }
        }
        ObservationCount++;
        LastReason = "자산·지분·미체결·운용·리딩방·평판 관찰 " + ObservationCount + "회";
        if (strongest != null && now >= nextResponse && (ResponseStock == null || !ResponseStock.orderBook.IsActive(ResponseOrder)))
        { Respond(strongest.Stock, strongestPressure, now); }
        return true;
    }
```

수정본:

```csharp
public bool Observe(float now)
    {
        var manager = StockManager.Instance;
        if (now < NextObservationAt || manager == null || !manager.SetupConfirmed || manager.playerAccount == null || !MarketDayController.CanTrade)
        { return false; }
        NextObservationAt = now + Mathf.Clamp(policy.observationSeconds,.5f,60f);
        var player = manager.playerAccount;
        Snapshot.ObservedAt = now; Snapshot.Cash = player.cash; Snapshot.ReservedCash = player.ReservedCash + PlayerAccounts.TaxDebt(player); Snapshot.Equity = player.Equity() - PlayerAccounts.TaxDebt(player);
        var secondary = PlayerAccounts.Instance == null ? null : PlayerAccounts.Instance.automated;
        if (secondary != null)
        { Snapshot.Cash += secondary.cash; Snapshot.ReservedCash += secondary.ReservedCash; Snapshot.Equity += secondary.Equity(); }
        Snapshot.AccountIntents.Clear();
        if (manual != null)
        {
            foreach (var request in manual.ActiveOrders)
            { Snapshot.AccountIntents.Add((request.Stock.symbol,request.Buy,request.Limit,request.Unsubmitted + request.PendingVolume)); }
        }
        var autoManual = PlayerAccounts.Instance == null ? null : PlayerAccounts.Instance.automatedOrders;
        if (autoManual != null)
        {
            foreach (var request in autoManual.ActiveOrders)
            { Snapshot.AccountIntents.Add((request.Stock.symbol,request.Buy,request.Limit,request.Unsubmitted + request.PendingVolume)); }
        }
        Snapshot.SelectedSymbol = manager.CurrentStock == null ? "" : manager.CurrentStock.symbol;
        Snapshot.OwnedCompanySymbol = player.OwnedCompany == null ? "" : player.OwnedCompany.symbol;
        var career = PlayerProgression.Instance;
        Snapshot.LeadingRights = career != null && career.CanOperateRooms;
        Snapshot.Turnover = career == null ? 0m : career.Turnover; Snapshot.RealizedProfit = career == null ? 0m : career.RealizedProfit;
        var operation = TradingOperationController.Instance;
        Snapshot.OperationMode = operation == null ? "" : operation.ModeName;
        Snapshot.OperationSymbol = operation == null || operation.Target == null ? "" : operation.Target.symbol;
        Snapshot.OperationBudget = operation == null ? 0m : operation.Budget;
        Snapshot.OperationSpent = operation == null ? 0m : operation.Spent;
        Snapshot.OperationInventory = operation == null ? 0m : operation.Inventory;
        Snapshot.OrderSymbol = manual?.Latest != null ? manual.Latest.Stock.symbol : "";
        Snapshot.ManualBuy = manual != null && manual.Buy;
        Snapshot.ManualRemaining = manual != null && manual.Active ? manual.Unsubmitted + manual.PendingVolume : 0m;
        Snapshot.ManualLimit = manual == null ? 0m : manual.Limit; Snapshot.ManualBudget = manual == null ? 0m : manual.Budget;
        Snapshot.Members = Snapshot.Supporters = Snapshot.Opponents = Snapshot.ControlledCompanies = 0;
        Snapshot.RoomCommands.Clear(); Snapshot.RoomMembers.Clear(); Snapshot.Relationships.Clear(); Snapshot.OpenOrders.Clear(); roomPressure.Clear();
        if (InvestorSociety.Instance != null)
        {
            foreach (var room in InvestorSociety.Instance.Rooms)
            {
                if (!room.PlayerOwned)
                { continue; }
                Snapshot.Members += room.Members.Count;
                foreach (int member in room.Members)
                { Snapshot.RoomMembers.Add((room.Id,member)); }
                if (room.Command != null && !room.Command.Canceled)
                { Snapshot.RoomCommands.Add((room.Id,room.Command.Stock.symbol,room.Command.Buy,room.Command.Sell)); }
            }
            if (InvestorSociety.Instance.Orders != null)
            {
                foreach (var plan in InvestorSociety.Instance.Orders.Plans)
                {
                    if (plan.Command.Canceled || plan.Bot == null || !plan.Bot.isActiveAndEnabled)
                    { continue; }
                    decimal intent = plan.State == LeadingRoomOrders.Stage.AwaitBuy || plan.State == LeadingRoomOrders.Stage.Buying ? Math.Max(0m,plan.Goal - plan.Bought) : plan.State == LeadingRoomOrders.Stage.AwaitSell || plan.State == LeadingRoomOrders.Stage.Selling ? -plan.Remaining : 0m;
                    roomPressure.TryGetValue(plan.Command.Stock,out decimal previous);
                    roomPressure[plan.Command.Stock] = previous + intent;
                }
            }
        }
        if (MarketDayController.Instance != null)
        {
            foreach (var bot in MarketDayController.Instance.Bots)
            {
                if (bot == null || bot.Social == null)
                { continue; }
                var mind = bot.Social.Psychology;
                Snapshot.Relationships.Add((bot.Social.Id,mind.Room,mind.Trust,mind.Betrayal));
                mind.RefreshQuadrant();
                if (mind.Quadrant == InvestorQuadrant.Loyal) { Snapshot.Supporters++; }
                if (mind.Quadrant == InvestorQuadrant.Hostile) { Snapshot.Opponents++; }
            }
        }
        int positionCount = manager.stocks.Count + manager.UnlistedStocks.Count;
        while (Snapshot.Positions.Count < positionCount)
        { Snapshot.Positions.Add(new Position()); }
        if (Snapshot.Positions.Count > positionCount)
        { Snapshot.Positions.RemoveRange(positionCount, Snapshot.Positions.Count - positionCount); }
        Position strongest = null; decimal strongestPressure = 0m;
        for (int i = 0; i < positionCount; i++)
        {
            var stock = i < manager.stocks.Count ? manager.stocks[i] : manager.UnlistedStocks[i - manager.stocks.Count]; var position = Snapshot.Positions[i];
            position.Stock = stock; position.Owned = player.GetOwnedVolume(stock); position.Average = player.GetOwnedAverage(stock); position.Price = stock.currentPrice;
            if (secondary != null)
            {
                position.Owned += secondary.GetOwnedVolume(stock);
                position.Average = position.Owned > 0m ? (player.GetOwnedCost(stock) + secondary.GetOwnedCost(stock)) / position.Owned : 0m;
            }
            position.Change = previousHoldings.TryGetValue(stock, out decimal previous) ? position.Owned - previous : 0m;
            previousHoldings[stock] = position.Owned;
            position.Ownership = stock.issuedShares > 0m ? position.Owned / stock.issuedShares : 0m;
            if (position.Ownership > .5m) { Snapshot.ControlledCompanies++; }
            stock.orderBook.CopyOwnerOrders(player, orders); position.PendingBuy = position.PendingSell = 0m;
            if (secondary != null) { stock.orderBook.CopyOwnerOrders(secondary,secondaryOrders); orders.AddRange(secondaryOrders); }
            foreach (var order in orders)
            {
                Snapshot.OpenOrders.Add((stock.symbol,order.isBuy,order.price,order.volume,order.timestamp));
                if (order.isBuy) { position.PendingBuy += order.volume; }
                else { position.PendingSell += order.volume; }
            }
            decimal pressure = position.Change + (position.PendingBuy - position.PendingSell) * .25m;
            if (manual != null)
            {
                foreach (var request in manual.ActiveOrders)
                { if (request.Stock == stock) { pressure += request.Unsubmitted * (request.Buy ? .25m : -.25m); } }
            }
            if (autoManual != null)
            {
                foreach (var request in autoManual.ActiveOrders)
                { if (request.Stock == stock) { pressure += request.Unsubmitted * (request.Buy ? .25m : -.25m); } }
            }
            if (roomPressure.TryGetValue(stock,out decimal roomIntent))
            { pressure += roomIntent * .1m; }
            if (stock.AcceptsOrders && Math.Abs(pressure) > Math.Abs(strongestPressure))
            { strongest = position; strongestPressure = pressure; }
        }
        ObservationCount++;
        LastReason = "자산·지분·미체결·운용·리딩방·평판 관찰 " + ObservationCount + "회";
        if (strongest != null && now >= nextResponse && (ResponseStock == null || !ResponseStock.orderBook.IsActive(ResponseOrder)))
        { Respond(strongest.Stock, strongestPressure, now); }
        return true;
    }
```
