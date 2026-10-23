using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the terminal camera's picture on screen through the UI/CRT shader.
/// The render texture matches the screen size so mouse input on the terminal canvas lines up.
/// Can blend between the full tube look and a flat, more readable look.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class CRTDisplay : MonoBehaviour
{
    [System.Serializable]
    public struct Look
    {
        public float curvature, cornerRadius, zoom;
        public float scanlines, mask, glow, glowRadius, chroma;
        public float flicker, rollingBand, grain, jitter, vignette, glare, brightness;
    }

    [SerializeField] Camera terminalCamera;

    [Tooltip("Readable look used once the operations console is up. The tube look is read from the material.")]
    [SerializeField] Look flatLook = new Look
    {
        curvature = 0f, cornerRadius = 0f, zoom = 1f,
        scanlines = 0.14f, mask = 0.05f, glow = 0.18f, glowRadius = 0.002f, chroma = 0f,
        flicker = 0.006f, rollingBand = 0.015f, grain = 0.012f, jitter = 0f, vignette = 0.08f, glare = 0f, brightness = 1.1f
    };

    [SerializeField] float degaussTime = 0.35f;

    static readonly int CurvatureId = Shader.PropertyToID("_Curvature");
    static readonly int CornerId = Shader.PropertyToID("_CornerRadius");
    static readonly int ZoomId = Shader.PropertyToID("_Zoom");
    static readonly int ScanId = Shader.PropertyToID("_ScanlineStrength");
    static readonly int MaskId = Shader.PropertyToID("_MaskStrength");
    static readonly int BloomId = Shader.PropertyToID("_Bloom");
    static readonly int BloomRadiusId = Shader.PropertyToID("_BloomRadius");
    static readonly int ChromaId = Shader.PropertyToID("_Chroma");
    static readonly int FlickerId = Shader.PropertyToID("_Flicker");
    static readonly int RollId = Shader.PropertyToID("_RollStrength");
    static readonly int NoiseId = Shader.PropertyToID("_Noise");
    static readonly int JitterId = Shader.PropertyToID("_Jitter");
    static readonly int VignetteId = Shader.PropertyToID("_Vignette");
    static readonly int GlareId = Shader.PropertyToID("_Glare");
    static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

    RawImage image;
    RenderTexture target;
    Material material;
    Look tubeLook;
    Look current;

    public bool IsFlat { get; private set; }

    void Awake()
    {
        image = GetComponent<RawImage>();
        // Work on a copy so tweaks at runtime never change the material asset.
        material = new Material(image.material);
        image.material = material;
        tubeLook = Read(material);
        current = tubeLook;
        Resize();
    }

    void OnEnable()
    {
        GameSettings.Changed += ApplySettings;
        ApplySettings();
    }

    void OnDisable()
    {
        GameSettings.Changed -= ApplySettings;
    }

    void Update()
    {
        if (target == null || target.width != Screen.width || target.height != Screen.height) Resize();
    }

    public IEnumerator TransitionToFlat(float duration)
    {
        IsFlat = true;
        yield return Blend(tubeLook, flatLook, duration);
    }

    public IEnumerator TransitionToTube(float duration)
    {
        IsFlat = false;
        yield return Blend(flatLook, tubeLook, duration);
    }

    IEnumerator Blend(Look from, Look to, float duration)
    {
        Sfx.Play("degauss");
        // Degauss: a short wobble and brightness surge, like the monitor re-syncing.
        for (float t = 0f; t < degaussTime; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / degaussTime;
            var wobble = from;
            wobble.jitter = Mathf.Max(from.jitter, 0.012f * k);
            wobble.chroma = from.chroma + 0.004f * k;
            wobble.brightness = from.brightness * (1f + 0.35f * k);
            Apply(wobble);
            yield return null;
        }
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            Apply(Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
            yield return null;
        }
        Apply(to);
    }

    void Resize()
    {
        if (target != null)
        {
            terminalCamera.targetTexture = null;
            target.Release();
            Destroy(target);
        }
        target = new RenderTexture(Mathf.Max(Screen.width, 2), Mathf.Max(Screen.height, 2), 24) { name = "TerminalScreen" };
        terminalCamera.targetTexture = target;
        image.texture = target;
    }

    void ApplySettings()
    {
        if (material != null) material.SetFloat(ScanId, GameSettings.Scanlines ? current.scanlines : 0f);
    }

    void Apply(Look l)
    {
        current = l;
        material.SetFloat(CurvatureId, l.curvature);
        material.SetFloat(CornerId, l.cornerRadius);
        material.SetFloat(ZoomId, l.zoom);
        material.SetFloat(MaskId, l.mask);
        material.SetFloat(BloomId, l.glow);
        material.SetFloat(BloomRadiusId, l.glowRadius);
        material.SetFloat(ChromaId, l.chroma);
        material.SetFloat(FlickerId, l.flicker);
        material.SetFloat(RollId, l.rollingBand);
        material.SetFloat(NoiseId, l.grain);
        material.SetFloat(JitterId, l.jitter);
        material.SetFloat(VignetteId, l.vignette);
        material.SetFloat(GlareId, l.glare);
        material.SetFloat(BrightnessId, l.brightness);
        ApplySettings();
    }

    static Look Read(Material m) => new Look
    {
        curvature = m.GetFloat(CurvatureId), cornerRadius = m.GetFloat(CornerId), zoom = m.GetFloat(ZoomId),
        scanlines = m.GetFloat(ScanId), mask = m.GetFloat(MaskId), glow = m.GetFloat(BloomId), glowRadius = m.GetFloat(BloomRadiusId),
        chroma = m.GetFloat(ChromaId), flicker = m.GetFloat(FlickerId), rollingBand = m.GetFloat(RollId), grain = m.GetFloat(NoiseId),
        jitter = m.GetFloat(JitterId), vignette = m.GetFloat(VignetteId), glare = m.GetFloat(GlareId), brightness = m.GetFloat(BrightnessId)
    };

    static Look Lerp(Look a, Look b, float t) => new Look
    {
        curvature = Mathf.Lerp(a.curvature, b.curvature, t), cornerRadius = Mathf.Lerp(a.cornerRadius, b.cornerRadius, t), zoom = Mathf.Lerp(a.zoom, b.zoom, t),
        scanlines = Mathf.Lerp(a.scanlines, b.scanlines, t), mask = Mathf.Lerp(a.mask, b.mask, t), glow = Mathf.Lerp(a.glow, b.glow, t),
        glowRadius = Mathf.Lerp(a.glowRadius, b.glowRadius, t), chroma = Mathf.Lerp(a.chroma, b.chroma, t), flicker = Mathf.Lerp(a.flicker, b.flicker, t),
        rollingBand = Mathf.Lerp(a.rollingBand, b.rollingBand, t), grain = Mathf.Lerp(a.grain, b.grain, t), jitter = Mathf.Lerp(a.jitter, b.jitter, t),
        vignette = Mathf.Lerp(a.vignette, b.vignette, t), glare = Mathf.Lerp(a.glare, b.glare, t), brightness = Mathf.Lerp(a.brightness, b.brightness, t)
    };

    void OnDestroy()
    {
        if (target != null) target.Release();
    }
}
