using UnityEngine;

/// <summary>Drives the SYSTEM CONFIGURATION panel rows from GameSettings.</summary>
public class SystemConfigMenu : MonoBehaviour
{
    [SerializeField] TerminalMenuItem volumeRow;
    [SerializeField] TerminalMenuItem textSpeedRow;
    [SerializeField] TerminalMenuItem scanlinesRow;
    [SerializeField] TerminalMenuItem displayRow;
    [SerializeField] GameObject scanlineOverlay;

    void OnEnable()
    {
        GameSettings.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        GameSettings.Changed -= Refresh;
    }

    public void VolumeDown() => GameSettings.VolumeSteps--;
    public void VolumeUp() => GameSettings.VolumeSteps++;
    public void TextSpeedDown() => GameSettings.TextSpeed--;
    public void TextSpeedUp() => GameSettings.TextSpeed++;
    public void ToggleScanlines() => GameSettings.Scanlines = !GameSettings.Scanlines;
    public void ToggleDisplay() => GameSettings.Fullscreen = !GameSettings.Fullscreen;

    void Refresh()
    {
        int v = GameSettings.VolumeSteps;
        volumeRow.SetValue("[" + new string('#', v) + new string('-', 10 - v) + "] " + (v * 10) + "%");
        textSpeedRow.SetValue("< " + GameSettings.TextSpeedNames[GameSettings.TextSpeed] + " >");
        scanlinesRow.SetValue(GameSettings.Scanlines ? "< ENGAGED >" : "< DISENGAGED >");
        displayRow.SetValue(GameSettings.Fullscreen ? "< FULL SCREEN >" : "< WINDOWED >");
        if (scanlineOverlay != null) scanlineOverlay.SetActive(GameSettings.Scanlines);
    }
}
