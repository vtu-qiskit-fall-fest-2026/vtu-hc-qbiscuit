using UnityEngine;

/// <summary>Player settings stored in PlayerPrefs and applied on load.</summary>
public static class GameSettings
{
    const string VolumeKey = "settings.volume";
    const string TextSpeedKey = "settings.textSpeed";
    const string ScanlinesKey = "settings.scanlines";
    const string FullscreenKey = "settings.fullscreen";

    public static readonly string[] TextSpeedNames = { "SLOW", "STANDARD", "RAPID" };
    static readonly float[] TextSpeedMultipliers = { 0.6f, 1f, 1.8f };

    public static event System.Action Changed;

    /// <summary>0..10 steps.</summary>
    public static int VolumeSteps
    {
        get => PlayerPrefs.GetInt(VolumeKey, 8);
        set { PlayerPrefs.SetInt(VolumeKey, Mathf.Clamp(value, 0, 10)); Apply(); }
    }

    public static int TextSpeed
    {
        get => PlayerPrefs.GetInt(TextSpeedKey, 1);
        set { PlayerPrefs.SetInt(TextSpeedKey, (value + TextSpeedNames.Length) % TextSpeedNames.Length); Apply(); }
    }

    public static float TextSpeedMultiplier => TextSpeedMultipliers[TextSpeed];

    public static bool Scanlines
    {
        get => PlayerPrefs.GetInt(ScanlinesKey, 1) == 1;
        set { PlayerPrefs.SetInt(ScanlinesKey, value ? 1 : 0); Apply(); }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        set { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); Apply(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnLoad() => Apply();

    public static void Apply()
    {
        AudioListener.volume = VolumeSteps / 10f;
        if (!Application.isEditor) Screen.fullScreen = Fullscreen;
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
