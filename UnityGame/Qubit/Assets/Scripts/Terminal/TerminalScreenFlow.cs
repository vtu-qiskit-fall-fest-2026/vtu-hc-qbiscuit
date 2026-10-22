using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Switches between the panels shown on the terminal screen:
/// boot log, main terminal, system configuration, station personnel and session start.
/// </summary>
public class TerminalScreenFlow : MonoBehaviour
{
    [SerializeField] BootSequence startup;
    [SerializeField] GameObject bootPanel;
    [SerializeField] TypewriterText bootText;
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject configPanel;
    [SerializeField] GameObject personnelPanel;
    [SerializeField] GameObject sessionPanel;
    [SerializeField] TypewriterText sessionText;
    [SerializeField] float pauseAfterBoot = 0.8f;

    [Header("Operations console")]
    [SerializeField] CRTDisplay crtDisplay;
    [SerializeField] GameObject consoleShell;
    [SerializeField] float pauseBeforeConsole = 1.2f;
    [SerializeField] float displaySwitchTime = 1.4f;

    [Tooltip("Raised when the player chooses BEGIN DIAGNOSTIC SESSION.")]
    public UnityEvent onBeginSession;

    bool booting;

    void OnEnable()
    {
        ShowOnly(null);
        StartCoroutine(BootRoutine());
    }

    void Update()
    {
        // Esc skips the startup and goes straight to the main terminal.
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (booting && kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            StopAllCoroutines();
            if (startup != null) startup.StopAllCoroutines();
            booting = false;
            ShowMain();
        }
    }

    IEnumerator BootRoutine()
    {
        booting = true;
        if (startup != null) yield return startup.Run();
        ShowOnly(bootPanel);
        yield return null;
        while (bootText.IsTyping) yield return null;
        yield return new WaitForSecondsRealtime(pauseAfterBoot);
        booting = false;
        ShowMain();
    }

    public void ShowMain() => ShowOnly(mainPanel);
    public void ShowConfig() => ShowOnly(configPanel);
    public void ShowPersonnel() => ShowOnly(personnelPanel);

    public void BeginSession()
    {
        StopAllCoroutines();
        StartCoroutine(SessionRoutine());
    }

    IEnumerator SessionRoutine()
    {
        ShowOnly(sessionPanel);
        yield return null;
        while (sessionText != null && sessionText.IsTyping) yield return null;
        yield return new WaitForSecondsRealtime(pauseBeforeConsole);

        // The terminal re-syncs into its flat, readable display mode with the session log still
        // on screen, so the player sees the picture straighten out before the console appears.
        if (crtDisplay != null) yield return crtDisplay.TransitionToFlat(displaySwitchTime);
        ShowOnly(null);
        yield return new WaitForSecondsRealtime(0.15f);
        ShowOnly(consoleShell);
        onBeginSession.Invoke();
    }

    public void ReturnToMainTerminal()
    {
        StopAllCoroutines();
        StartCoroutine(ReturnRoutine());
    }

    IEnumerator ReturnRoutine()
    {
        ShowMain();
        if (crtDisplay != null && crtDisplay.IsFlat) yield return crtDisplay.TransitionToTube(displaySwitchTime);
    }

    public void PowerDown()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ShowOnly(GameObject panel)
    {
        if (panel != null && startup != null) startup.HideAll();
        foreach (var p in new[] { bootPanel, mainPanel, configPanel, personnelPanel, sessionPanel, consoleShell })
            if (p != null) p.SetActive(p == panel);
    }
}
