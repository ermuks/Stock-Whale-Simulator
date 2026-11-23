using UnityEngine;

// 모니터와 태블릿 화면에서 나오는 빛.
// 화면마다 그 앞에 사방으로 퍼지는 조명을 하나씩 둔다. 플레이어 쪽의 책상과 방은 물론이고, 화면 둘레의 벽에도 빛이 번진다.
// 씬 파일은 건드리지 않고 실행 중에 만든다. 화면의 Transform과 카메라는 바꾸지 않는다.
public sealed class ScreenGlow : MonoBehaviour
{
    /// <summary>플레이어 자리(화면 너비의 이 배수만큼 떨어진 곳)에서 받는 빛의 세기.</summary>
    [Range(0f, 6f)] public float monitorStrength = .38f, tabletStrength = .14f;
    // 빛이 닿는 거리를 짧게 잡는다. 화면 바로 둘레만 희미하게 드러나고 그 너머는 어둠에 잠겨서, 좁은 방에 갇힌 느낌이 난다.
    private const float PlayerDistance = 1.4f, RangeWidths = 2.6f;
    private static readonly Color Cool = new(.55f, .66f, .78f), Rising = new(.86f, .50f, .46f), Falling = new(.46f, .58f, .82f);
    private readonly Light[] lights = new Light[5];
    private readonly float[] strength = new float[5];
    private DeskViewController views;

    void Update()
    {
        if (views == null)
        {
            views = DeskViewController.Instance;
            if (views == null || !views.Ready) { views = null; return; }
        }
        for (int i = 0; i < lights.Length; i++)
        {
            var screen = views.ScreenFor((DeskViewTarget)i);
            if (screen == null) { if (lights[i] != null) { lights[i].enabled = false; } continue; }
            if (lights[i] == null) { lights[i] = Create(i, screen); }
            var light = lights[i]; light.enabled = screen.gameObject.activeInHierarchy;
            // 화면은 숫자가 계속 바뀐다. 아주 약하게 일렁이게 해서 켜져 있는 화면처럼 보이게 한다.
            float flicker = .94f + .06f * Mathf.PerlinNoise(Time.unscaledTime * .9f, i * 3.7f);
            light.intensity = strength[i] * flicker;
            // 가운데 모니터(차트)는 지금 보는 종목이 오르는지 내리는지에 따라 빛깔이 살짝 기운다.
            if (i == (int)DeskViewTarget.Chart) { light.color = Color.Lerp(light.color, ChartTint(), Time.unscaledDeltaTime * 1.5f); }
        }
    }

    void OnDisable()
    {
        foreach (var light in lights) { if (light != null) { light.enabled = false; } }
    }

    private Light Create(int index, RectTransform screen)
    {
        var holder = new GameObject("Light :: Screen Glow " + (DeskViewTarget)index);
        holder.transform.SetParent(transform, false);
        // 화면의 앞면은 -forward 쪽이다(캔버스는 등을 보인다). 그쪽으로 조금 나온 자리에 둔다.
        Vector3 center = screen.TransformPoint(screen.rect.center), toward = -screen.forward;
        float width = Mathf.Max(.01f, screen.rect.width * screen.lossyScale.x);
        holder.transform.SetPositionAndRotation(center + toward * width * .18f, Quaternion.LookRotation(toward, screen.up));
        var light = holder.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = width * RangeWidths; light.color = Cool;
        // 모니터 세 대의 빛은 그림자를 드리운다. 책상 위 물건들이 플레이어 쪽으로 그림자를 끌어 책상에 놓인 것처럼 보인다.
        // 태블릿 빛은 약하고 책상에 붙어 있어 켜지 않는다(빛 하나마다 그림자를 여섯 방향으로 그려야 한다).
        if (index < 3) { light.shadows = LightShadows.Soft; light.shadowStrength = 1f; light.shadowBias = .02f; light.shadowNormalBias = .12f; light.shadowNearPlane = .1f; }
        else { light.shadows = LightShadows.None; }
        // 빛은 거리의 제곱으로 약해진다. 플레이어 자리에서 원하는 밝기가 되도록 화면 크기에 맞춰 세기를 정한다.
        float distance = width * PlayerDistance;
        strength[index] = (index < 3 ? monitorStrength : tabletStrength) * distance * distance;
        light.intensity = strength[index];
        return light;
    }

    private static Color ChartTint()
    {
        var stock = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        if (stock == null || StockStatistics.Instance == null || !StockStatistics.Instance.TryGetDailyChange(stock, out decimal change)) { return Cool; }
        float lean = Mathf.Clamp01(Mathf.Abs((float)change) / 10f) * .6f;
        return Color.Lerp(Cool, change > 0m ? Rising : Falling, lean);
    }
}
