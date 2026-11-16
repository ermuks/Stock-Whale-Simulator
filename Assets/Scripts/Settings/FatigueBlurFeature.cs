using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>Player-camera optics only. Pixel limits refer to final output, not render scale.</summary>
public sealed class FatigueBlurFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    [Range(0, 2)] public float maximumGaussianRadius = 2f;
    [Range(0, 4)] public float maximumMotionPixels = 4f;
    private Material material;
    private OpticsPass pass;
    private readonly MotionHistory history = new();
    public float LastGaussianRadius { get; private set; }
    public Vector2 LastMotionPixels { get; private set; }
    public int RecordedPasses { get; private set; }

    public override void Create()
    {
        CoreUtils.Destroy(material);
        if (shader == null) { shader = Resources.Load<Shader>("UI/SystemMenu/FatigueBlur"); }
        if (shader != null) { material = CoreUtils.CreateEngineMaterial(shader); }
        pass = new OpticsPass(this) { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
        history.Reset();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var camera = renderingData.cameraData.camera;
        var rest = PlayerRestController.Instance;
        if (!Application.isPlaying || camera.cameraType != CameraType.Game || rest == null || camera != rest.ViewCamera)
        { return; }
        LastGaussianRadius = 0; LastMotionPixels = Vector2.zero;
        if (PauseMenuController.IsOpen || material == null)
        { history.Reset(); return; }
        float fatigue = (float)rest.State.fatigue;
        LastGaussianRadius = GaussianRadius(fatigue, maximumGaussianRadius);
        if (DeskViewController.Instance != null && DeskViewController.Instance.IsTransitioning) { history.Reset(); }
        else { LastMotionPixels = history.Sample(camera, fatigue, maximumMotionPixels, Time.unscaledTimeAsDouble, Time.frameCount); }
        if (LastGaussianRadius <= .001f) { return; }
        pass.Setup(material, LastGaussianRadius, LastMotionPixels, camera.pixelWidth, camera.pixelHeight);
        renderer.EnqueuePass(pass);
    }

    public static float GaussianRadius(float fatigue, float maximum = 2f)
    {
        if (float.IsNaN(fatigue) || float.IsInfinity(fatigue)) { return 0; }
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(25, 100, fatigue)) * Mathf.Clamp(maximum, 0, 2);
    }

    /// <summary>Rotational shutter blur suits the seated camera; no motion-vector buffer or history texture.</summary>
    public static Vector2 MotionPixels(Quaternion previous, Quaternion current, float fov, float aspect,
        int height, double elapsed, float fatigue, float maximum = 4f)
    {
        if (fatigue <= 50 || float.IsNaN(fatigue) || float.IsInfinity(fatigue) || elapsed < .001 || elapsed > .2 || height <= 0 || aspect <= 0)
        { return Vector2.zero; }
        if (Quaternion.Angle(previous, current) > 12f) { return Vector2.zero; }
        var direction = Quaternion.Inverse(current) * previous * Vector3.forward;
        float projection = height / (2f * Mathf.Tan(Mathf.Clamp(fov, 1, 179) * .5f * Mathf.Deg2Rad));
        var pixels = new Vector2(direction.x, direction.y) * (projection / Mathf.Max(.1f, direction.z));
        pixels *= .025f / (float)elapsed;
        float strength = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(50, 100, fatigue));
        return Vector2.ClampMagnitude(pixels * strength, Mathf.Clamp(maximum, 0, 4) * strength);
    }

    protected override void Dispose(bool disposing)
    { CoreUtils.Destroy(material); material = null; history.Reset(); }

    public sealed class MotionHistory
    {
        private bool valid;
        private int frame, width, height;
        private double time;
        private float fov;
        private Vector3 position;
        private Quaternion rotation;
        private Vector2 cached;
        public void Reset() { valid = false; cached = Vector2.zero; }
        public Vector2 Sample(Camera camera, float fatigue, float maximum, double now, int currentFrame)
        {
            if (valid && frame == currentFrame) { return cached; }
            var transform = camera.transform;
            bool continuous = valid && camera.pixelWidth == width && camera.pixelHeight == height &&
                Mathf.Abs(camera.fieldOfView - fov) < .5f && (transform.position - position).sqrMagnitude < .0225f;
            cached = continuous ? MotionPixels(rotation, transform.rotation, camera.fieldOfView, camera.aspect,
                camera.pixelHeight, now - time, fatigue, maximum) : Vector2.zero;
            valid = true; frame = currentFrame; time = now; fov = camera.fieldOfView;
            width = camera.pixelWidth; height = camera.pixelHeight; position = transform.position; rotation = transform.rotation;
            return cached;
        }
    }

    private sealed class OpticsPass : ScriptableRenderPass
    {
        private static readonly int OpticsId = Shader.PropertyToID("_FatigueOptics");
        private readonly FatigueBlurFeature owner;
        private readonly MaterialPropertyBlock properties = new();
        private Material material;
        private bool moving;
        public OpticsPass(FatigueBlurFeature owner) { this.owner = owner; requiresIntermediateTexture = true; }
        public void Setup(Material value, float radius, Vector2 motion, int width, int height)
        {
            material = value; moving = motion.sqrMagnitude > .0025f;
            properties.SetVector(OpticsId, new Vector4(radius / Mathf.Max(1, width), radius / Mathf.Max(1, height),
                motion.x / Mathf.Max(1, width), motion.y / Mathf.Max(1, height)));
        }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) { return; }
            var source = resources.activeColorTexture;
            var descriptor = graph.GetTextureDesc(source);
            descriptor.name = "Fatigue Gaussian 2px"; descriptor.clearBuffer = false; descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            var blurred = graph.CreateTexture(descriptor);
            var gaussian = new RenderGraphUtils.BlitMaterialParameters(source, blurred, material, 0) { propertyBlock = properties };
            graph.AddBlitPass(gaussian, passName: "Fatigue / Gaussian (max 2px)");
            if (moving)
            {
                descriptor.name = "Fatigue Camera Motion";
                var motion = graph.CreateTexture(descriptor);
                var shutter = new RenderGraphUtils.BlitMaterialParameters(blurred, motion, material, 1) { propertyBlock = properties };
                graph.AddBlitPass(shutter, passName: "Fatigue / Camera motion (max 4px)");
                resources.cameraColor = motion;
            }
            else { resources.cameraColor = blurred; }
            owner.RecordedPasses++;
        }
    }
}
