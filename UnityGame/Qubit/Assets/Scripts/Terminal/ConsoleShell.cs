using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The flat operations console shown once the diagnostic session starts:
/// status bar, module list, main panel and key hints. Sections reveal one after another.
/// </summary>
public class ConsoleShell : MonoBehaviour
{
    [SerializeField] List<CanvasGroup> revealOrder = new List<CanvasGroup>();
    [SerializeField] float revealStep = 0.12f;
    [SerializeField] float revealFade = 0.18f;

    [Header("Status bar clock")]
    [SerializeField] TMP_Text clockText;
    [SerializeField] int startHour = 2;
    [SerializeField] int startMinute = 17;

    [Header("Main panel")]
    [SerializeField] TMP_Text statusLine;
    [SerializeField] TypewriterText mainPanelText;

    [Tooltip("Raised when the player opens the COMMS LINK module.")]
    public UnityEvent onOpenComms;

    float sessionStart;

    void OnEnable()
    {
        sessionStart = Time.unscaledTime;
        if (statusLine != null) statusLine.text = "";
        StartCoroutine(Reveal());
    }

    IEnumerator Reveal()
    {
        foreach (var g in revealOrder) g.alpha = 0f;
        foreach (var g in revealOrder)
        {
            StartCoroutine(Fade(g));
            yield return new WaitForSecondsRealtime(revealStep);
        }
        if (mainPanelText != null) mainPanelText.Play();
    }

    IEnumerator Fade(CanvasGroup g)
    {
        for (float t = 0f; t < revealFade; t += Time.unscaledDeltaTime)
        {
            // Stepped fade so panels "switch on" rather than glide in.
            g.alpha = Mathf.Floor(t / revealFade * 4f) / 4f;
            yield return null;
        }
        g.alpha = 1f;
    }

    void Update()
    {
        if (clockText == null) return;
        int seconds = Mathf.FloorToInt(Time.unscaledTime - sessionStart);
        int total = startHour * 3600 + startMinute * 60 + seconds;
        clockText.text = string.Format("{0:00}:{1:00}:{2:00} UTC", (total / 3600) % 24, (total / 60) % 60, total % 60);
    }

    public void OpenComms()
    {
        Status("OPENING CHANNEL TO LUNAR VESSEL ...");
        onOpenComms.Invoke();
    }

    public void NotebookUnavailable() => Status("RESEARCH NOTEBOOK: ANSWER THE TRANSMISSION FIRST.");
    public void DiagnosticsOffline() => Status("DIAGNOSTIC CONSOLE: MODULE OFFLINE.");
    public void ReportLocked() => Status("MISSION REPORT: NO VERIFIED RESULT TO TRANSMIT.");

    void Status(string message)
    {
        if (statusLine != null) statusLine.text = "> " + message;
    }
}
