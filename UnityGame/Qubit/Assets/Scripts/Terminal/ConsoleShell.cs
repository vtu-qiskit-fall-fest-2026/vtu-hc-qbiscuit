using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The flat operations console shown once the diagnostic session starts:
/// status bar, module list, main panel and key hints. Sections reveal one after another.
/// Also tracks Act I progress: answer the transmission, review the notebook, bring diagnostics online.
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

    [Header("Research notebook")]
    [SerializeField] NotebookView notebook;
    [SerializeField] TerminalMenu modulesMenu;
    [SerializeField] GameObject[] hiddenWhileReading;
    [SerializeField] TMP_Text keysText;
    [SerializeField] string consoleKeys = "[UP/DOWN] MODULE    [ENTER] OPEN    [ESC] MAIN TERMINAL";
    [SerializeField] string notebookKeys = "[W/S] ENTRY  [A/D] SECTION  [PGUP/PGDN] SCROLL  [1-3] GUIDE  [P] PDF  [ESC] BACK";

    [Header("Comms link")]
    [SerializeField] CommsView comms;
    [SerializeField] DialogueScript openingTransmission;
    [SerializeField] GameObject[] hiddenDuringComms;
    [SerializeField] string commsKeys = "[ENTER] CONTINUE / TRANSMIT    [ESC] LEAVE CHANNEL";

    [Header("Diagnostic console")]
    [SerializeField] DiagnosticsView diagnostics;
    [SerializeField] GameObject[] hiddenDuringDiagnostics;
    [SerializeField] string diagnosticsKeys = "[R] RUN   [1-3] PLACE X   [0] CLEAR   [V] VERIFY   [N] NOTEBOOK   [ESC] BACK";
    [Tooltip("Notebook section opened from the diagnostic console (05 = reading the console).")]
    [SerializeField] int notebookSectionFromDiagnostics = 4;

    [Header("Cryo control (cause of the error)")]
    [SerializeField] CryoView cryo;
    [SerializeField] TerminalMenuItem cryoItem;
    [SerializeField] GameObject[] hiddenDuringCryo;
    [SerializeField] string cryoKeys = "[1-3] SCAN  [←/→] DIAL / HOLD  [↑/↓] HOLD  [ENTER] FIRE  [E] ENCODE  [ESC] BACK";

    [Header("Act III: report and ending")]
    [SerializeField] EndingView ending;
    [SerializeField] GameObject[] hiddenDuringEnding;
    [SerializeField] string endingKeys = "[ENTER] SKIP / CONTINUE";
    [Tooltip("Raised after the ending, to return to the main terminal.")]
    public UnityEvent onSessionComplete = new UnityEvent();

    [Header("Modules")]
    [SerializeField] TerminalMenuItem commsItem;
    [SerializeField] TerminalMenuItem notebookItem;
    [SerializeField] TerminalMenuItem diagnosticsItem;
    [SerializeField] TerminalMenuItem reportItem;

    [Header("Main panel cards")]
    [SerializeField, TextArea(4, 12)] string objectiveCard =
        "<color=#E3C58F>02:18 UTC   CHANNEL CLOSED</color>\n\n" +
        "OBJECTIVE ... REVIEW THE RESEARCH NOTEBOOK\n" +
        "THEN ........ BRING THE DIAGNOSTIC MODULE ONLINE\n\n" +
        "<color=#718780>Start with the CORE entries: the X gate, bit-flip errors and parity checks.</color>";
    [SerializeField, TextArea(4, 12)] string diagnosticsCard =
        "<color=#E3C58F>DIAGNOSTIC MODULE ONLINE</color>\n\n" +
        "CIRCUIT ..... 3 DATA QUBITS + 2 ANCILLAS\n" +
        "BACKEND ..... QISKIT AER (LOCAL)\n" +
        "TASK ........ FIND AND CORRECT THE BIT-FLIP ERROR\n\n" +
        "<color=#718780>Select DIAGNOSTICS and press [ENTER].</color>";

    [SerializeField, TextArea(4, 12)] string verifiedCard =
        "<color=#8FBCA0>REPAIR VERIFIED</color>   <color=#718780>INCOMING // LUNAR RELAY</color>\n\n" +
        "\"Good work, Andromeda. But why did it flip? We're seeing fluctuations around the cryogenic system. Find the cause before the transmission window closes.\"\n\n" +
        "<color=#718780>Select CRYO CONTROL and press [ENTER].</color>";
    [SerializeField, TextArea(4, 12)] string causeCard =
        "<color=#8FBCA0>CAUSE FOUND</color>\n\n" +
        "QUBIT ....... Q-C, NEXT TO THE CRYOGENIC FAULT\n" +
        "CAUSE ....... THERMAL RELAXATION (HEAT + TIME)\n" +
        "REPORT ...... READY FOR TRANSMISSION\n\n" +
        "<color=#718780>Select MISSION REPORT and press [ENTER] to transmit.</color>";

    [Tooltip("Raised when the player opens the COMMS LINK module.")]
    public UnityEvent onOpenComms;
    [Tooltip("Raised when the player opens DIAGNOSTICS once it is online.")]
    public UnityEvent onOpenDiagnostics;

    // Badges are bracketed so they read apart from the label: amber needs attention, mint is done.
    const string Incoming = "<color=#D0A46D>[INCOMING]</color>";
    const string New = "<color=#D0A46D>[NEW]</color>";
    const string Online = "<color=#D0A46D>[ONLINE]</color>";

    const string Ready = "<color=#D0A46D>[READY]</color>";
    const string Verified = "<color=#8FBCA0>[VERIFIED]</color>";
    const string Sent = "<color=#8FBCA0>[SENT]</color>";
    const string Done = "<color=#8FBCA0>[DONE]</color>";

    float sessionStart;
    bool transmissionAnswered;
    bool notebookReviewed;
    bool repairVerified;
    bool causeFound;
    bool reportSent;
    bool notebookFromDiagnostics;
    DialogueScript reportDialogue;

    public bool DiagnosticsOnline => notebookReviewed;

    void Awake()
    {
        if (comms != null)
        {
            comms.clock = () => StationTime(false);
            comms.onClose.AddListener(CloseComms);
        }
        if (diagnostics != null)
        {
            diagnostics.onClose.AddListener(CloseDiagnostics);
            diagnostics.onVerified.AddListener(OnRepairVerified);
            diagnostics.onOpenNotebook.AddListener(OpenNotebookFromDiagnostics);
        }
        if (ending != null) ending.onFinished.AddListener(FinishSession);
        if (cryo != null)
        {
            cryo.onClose.AddListener(CloseCryo);
            cryo.onCompleted.AddListener(OnCauseFound);
        }
    }

    void OnEnable()
    {
        sessionStart = Time.unscaledTime;
        if (statusLine != null) statusLine.text = "";
        ApplyProgress();
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
        if (clockText != null) clockText.text = StationTime(true) + " UTC";
    }

    public string StationTime(bool seconds)
    {
        int elapsed = Mathf.FloorToInt(Time.unscaledTime - sessionStart);
        int total = startHour * 3600 + startMinute * 60 + elapsed;
        string hm = string.Format("{0:00}:{1:00}", (total / 3600) % 24, (total / 60) % 60);
        return seconds ? hm + string.Format(":{0:00}", total % 60) : hm;
    }

    // ---- Comms link ----

    public void OpenComms()
    {
        onOpenComms.Invoke();
        if (comms == null) { Status("OPENING CHANNEL TO LUNAR VESSEL ..."); return; }

        foreach (var g in hiddenDuringComms) g.SetActive(false);
        modulesMenu.enabled = false;
        if (keysText != null) keysText.text = commsKeys;
        Status("CHANNEL OPEN: LUNAR RELAY");

        if (transmissionAnswered) comms.ShowLog(openingTransmission);
        else comms.Play(openingTransmission, OnTransmissionAnswered);
    }

    void OnTransmissionAnswered()
    {
        transmissionAnswered = true;
        CloseComms();
        ApplyProgress();
        ShowCard(objectiveCard);
        Status("NOTEBOOK FLAGGED: REVIEW THE CORE ENTRIES.");
    }

    public void CloseComms()
    {
        comms.gameObject.SetActive(false);
        foreach (var g in hiddenDuringComms) g.SetActive(true);
        modulesMenu.enabled = true;
        if (keysText != null) keysText.text = consoleKeys;
        if (!transmissionAnswered) Status("CHANNEL LEFT OPEN. THE TRANSMISSION IS STILL WAITING.");
    }

    // ---- Research notebook ----

    public void OpenNotebook()
    {
        foreach (var g in hiddenWhileReading) g.SetActive(false);
        modulesMenu.enabled = false;
        if (keysText != null) keysText.text = notebookKeys;
        notebook.Open();
    }

    void OpenNotebookFromDiagnostics()
    {
        notebookFromDiagnostics = true;
        diagnostics.gameObject.SetActive(false);
        foreach (var g in hiddenWhileReading) g.SetActive(false);
        if (keysText != null) keysText.text = notebookKeys;
        notebook.Open(notebookSectionFromDiagnostics);
    }

    public void CloseNotebook()
    {
        notebook.gameObject.SetActive(false);
        foreach (var g in hiddenWhileReading) g.SetActive(true);

        if (notebookFromDiagnostics)
        {
            // Back to the circuit board the player came from.
            notebookFromDiagnostics = false;
            foreach (var g in hiddenDuringDiagnostics) g.SetActive(false);
            if (keysText != null) keysText.text = diagnosticsKeys;
            diagnostics.Open();
            return;
        }

        modulesMenu.enabled = true;
        if (keysText != null) keysText.text = consoleKeys;

        if (transmissionAnswered && !notebookReviewed)
        {
            notebookReviewed = true;
            ApplyProgress();
            ShowCard(diagnosticsCard);
            Status("DIAGNOSTIC MODULE ONLINE.");
        }
        else Status("RESEARCH NOTEBOOK CLOSED.");
    }

    // ---- Diagnostics and report ----

    public void OpenDiagnostics()
    {
        if (!notebookReviewed)
        {
            Status(transmissionAnswered ? "DIAGNOSTIC MODULE OFFLINE. REVIEW THE NOTEBOOK FIRST."
                                        : "DIAGNOSTIC MODULE OFFLINE. ANSWER THE TRANSMISSION FIRST.");
            return;
        }
        onOpenDiagnostics.Invoke();
        if (diagnostics == null) { Status("DIAGNOSTIC CONSOLE: LOADING CIRCUIT INTERFACE ..."); return; }
        foreach (var g in hiddenDuringDiagnostics) g.SetActive(false);
        modulesMenu.enabled = false;
        if (keysText != null) keysText.text = diagnosticsKeys;
        diagnostics.Open();
    }

    public void CloseDiagnostics()
    {
        diagnostics.gameObject.SetActive(false);
        foreach (var g in hiddenDuringDiagnostics) g.SetActive(true);
        modulesMenu.enabled = true;
        if (keysText != null) keysText.text = consoleKeys;
        Status(repairVerified ? "REPAIR VERIFIED. REPORT READY TO SEND." : "CONSOLE CLOSED. FAULT STILL OPEN.");
    }

    void OnRepairVerified()
    {
        if (repairVerified) return;
        repairVerified = true;
        ApplyProgress();
        ShowCard(verifiedCard);
    }

    // ---- Cryo control: the cause of the error ----

    public void OpenCryo()
    {
        if (!repairVerified) { Status("CRYO CONTROL LOCKED. REPAIR THE FAULT FIRST."); return; }
        if (cryo == null) return;
        foreach (var g in hiddenDuringCryo) g.SetActive(false);
        modulesMenu.enabled = false;
        if (keysText != null) keysText.text = cryoKeys;
        Sfx.Music(true);
        cryo.Open();
    }

    public void CloseCryo()
    {
        cryo.gameObject.SetActive(false);
        foreach (var g in hiddenDuringCryo) g.SetActive(true);
        modulesMenu.enabled = true;
        if (keysText != null) keysText.text = consoleKeys;
        Sfx.Music(false);
        Status(causeFound ? "CAUSE FOUND. MISSION REPORT READY." : "CRYO CONTROL CLOSED. CAUSE STILL UNKNOWN.");
        if (causeFound) ShowCard(causeCard);
    }

    void OnCauseFound()
    {
        if (causeFound) return;
        causeFound = true;
        ApplyProgress();
    }

    // ---- Act III: mission report and ending ----

    /// <summary>MISSION REPORT module: transmit the verified result, or explain why it's locked.</summary>
    public void ReportLocked()
    {
        if (!repairVerified) { Status("NO VERIFIED RESULT TO TRANSMIT YET."); return; }
        if (!causeFound) { Status("FIND THE CAUSE FIRST: OPEN CRYO CONTROL."); return; }
        if (comms == null) return;

        foreach (var g in hiddenDuringComms) g.SetActive(false);
        modulesMenu.enabled = false;
        if (keysText != null) keysText.text = commsKeys;

        if (reportSent) { comms.ShowLog(reportDialogue); Status("REPORT ARCHIVE: LUNAR RELAY"); return; }
        reportDialogue = BuildReport();
        Status("TRANSMITTING MISSION REPORT ...");
        comms.Play(reportDialogue, OnReportSent);
    }

    DialogueScript BuildReport()
    {
        string syn = diagnostics.Syndrome;
        int fix = diagnostics.Correction;
        string[] ordinal = { "first", "second", "third" };
        string you = fix >= 0
            ? $"The syndrome reads {syn}, which indicates a bit-flip error on the {ordinal[fix]} data qubit. I've applied the corresponding correction, and the verification run passed."
            : $"The syndrome reads {syn}: no bit-flip detected, so no correction was needed. The verification run passed.";

        var d = ScriptableObject.CreateInstance<DialogueScript>();
        d.name = "Act3_Report";
        d.channel = "CH 04";
        d.lines = new List<DialogueScript.Line>
        {
            new DialogueScript.Line { speaker = DialogueScript.Speaker.System, text = $"Mission report compiled. Syndrome {syn}, correction {(fix >= 0 ? "X on q" + fix : "none")}, data 000, verified. Opening uplink." },
            new DialogueScript.Line { speaker = DialogueScript.Speaker.You, text = you },
            new DialogueScript.Line { speaker = DialogueScript.Speaker.MissionSpecialist, text = "Received. I'll compare the result with the instrument's next measurement." },
            new DialogueScript.Line { speaker = DialogueScript.Speaker.System, text = "Diagnostic complete. Research report transmitted." },
        };
        return d;
    }

    void OnReportSent()
    {
        reportSent = true;
        comms.gameObject.SetActive(false);
        foreach (var g in hiddenDuringComms) g.SetActive(true);
        ApplyProgress();
        if (ending == null) { CloseComms(); return; }

        foreach (var g in hiddenDuringEnding) g.SetActive(false);
        if (keysText != null) keysText.text = endingKeys;
        ending.Show(MissionLog());
    }

    string MissionLog()
    {
        int fix = diagnostics.Correction;
        string syn = diagnostics.Syndrome;
        string fault = fix >= 0 ? $"SINGLE BIT-FLIP ON q{fix}" : "NONE DETECTED";
        string source = diagnostics.VerifiedBy;
        return
            $"<color=#E3C58F>MISSION LOG  //  ANDROMEDA RESEARCH STATION</color>\n" +
            $"<color=#718780>{StationTime(false)} UTC   SESSION 0217-A</color>\n\n" +
            $"FAULT .......... {fault}\n" +
            $"SYNDROME ....... {syn}\n" +
            $"CORRECTION ..... {(fix >= 0 ? "X ON q" + fix : "NONE")}\n" +
            $"VERIFICATION ... PASSED  (DATA 000, {source})\n" +
            (cryo != null && cryo.Completed
                ? $"CAUSE .......... THERMAL RELAXATION ON Q-C  (T1 ≈ {cryo.EstimatedT1:0} µs AT 80 mK)\n" +
                  $"MITIGATION ..... CORE COOLED, POWER {cryo.WinningPower:0}%\n\n"
                : "\n") +
            $"The control calculation can be trusted again.\n\n" +
            $"<color=#8FBCA0>INCOMING  //  LUNAR RELAY</color>\n" +
            $"\"The instrument's next measurement matches your result. One more thing: the latest lunar survey shows a signal we can't explain yet. Sending the data now.\"";
    }

    void FinishSession()
    {
        foreach (var g in hiddenDuringEnding) g.SetActive(true);
        modulesMenu.enabled = true;
        if (keysText != null) keysText.text = consoleKeys;
        Status("SESSION 0217-A COMPLETE.");
        onSessionComplete.Invoke();
    }

    // ---- Shared ----

    void ApplyProgress()
    {
        if (commsItem != null)
        {
            commsItem.SetBadge(transmissionAnswered ? "" : Incoming);
            commsItem.SetDescription(transmissionAnswered
                ? "STATUS: CHANNEL IDLE\nRe-read the 02:17 transmission from the lunar vessel."
                : "STATUS: INCOMING\nA transmission from the lunar vessel is waiting on the relay.");
        }
        if (notebookItem != null)
        {
            notebookItem.SetBadge(transmissionAnswered && !notebookReviewed ? New : "");
            notebookItem.SetDescription(transmissionAnswered && !notebookReviewed
                ? "STATUS: RECOMMENDED\nReview the CORE entries before running the diagnostic."
                : "STATUS: AVAILABLE\nThe station's beginner guide, with hands-on benches and IBM Quantum docs.");
        }
        if (diagnosticsItem != null)
        {
            diagnosticsItem.SetBadge(repairVerified ? Verified : notebookReviewed ? Online : "");
            diagnosticsItem.SetDescription(repairVerified
                ? "STATUS: VERIFIED\nThe encoded state was restored and verified."
                : notebookReviewed
                ? "STATUS: ONLINE\nThe three-qubit diagnostic circuit, run on Qiskit Aer."
                : "STATUS: OFFLINE\nComes online after you answer the transmission and review the notebook.");
        }
        if (reportItem != null)
        {
            bool ready = repairVerified && causeFound;
            reportItem.SetBadge(reportSent ? Sent : ready ? Ready : "");
            reportItem.SetDescription(reportSent
                ? "STATUS: SENT\nRe-read the report transmitted to the lunar vessel."
                : ready
                ? "STATUS: READY\nPress [ENTER] to transmit the verified result and its cause."
                : repairVerified
                ? "STATUS: LOCKED\nFind the cause of the bit-flip in CRYO CONTROL first."
                : "STATUS: LOCKED\nNeeds a verified result before anything can be transmitted.");
        }
        if (cryoItem != null)
        {
            cryoItem.SetBadge(causeFound ? Done : repairVerified ? New : "");
            cryoItem.SetDescription(causeFound
                ? "STATUS: STABLE\nQ-C cooled. Revisit the experiments any time."
                : repairVerified
                ? "STATUS: ALERT\nFluctuations near the cryogenic system. Find out why the qubit flipped."
                : "STATUS: LOCKED\nRemote access to the vessel's cryogenic core opens after the repair.");
        }
        if (modulesMenu != null && modulesMenu.isActiveAndEnabled) modulesMenu.Refresh();
    }

    void ShowCard(string text)
    {
        if (mainPanelText == null) return;
        mainPanelText.GetComponent<TMP_Text>().text = text;
        mainPanelText.Play();
    }

    void Status(string message)
    {
        if (statusLine != null) statusLine.text = "> " + message;
    }
}
