using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The diagnostic console: the three-qubit bit-flip circuit drawn as a board, with the
/// player's correction placed in the FIX column and the matching Qiskit code generated live.
/// Only the backend's "verified" field can mark the repair as successful.
/// </summary>
public class DiagnosticsView : MonoBehaviour
{
    [SerializeField] QuantumBackend backend;
    [Tooltip("Data qubit the scenario flips (0, 1 or 2). -1 picks one at random each session. 3 means no error.")]
    [SerializeField] int scenarioErrorQubit = -1;

    [Header("Circuit board")]
    [SerializeField] Button[] fixSlots = new Button[3];
    [SerializeField] TMP_Text[] faultMarks = new TMP_Text[3];
    [SerializeField] RectTransform scanBar;
    [SerializeField] Vector2 scanRange = new Vector2(48f, 288f);
    [SerializeField] TMP_Text codeText;

    [Header("Readout")]
    [SerializeField] Image lampA;
    [SerializeField] Image lampB;
    [SerializeField] TMP_Text syndromeText;
    [SerializeField] TMP_Text dataText;
    [SerializeField] TMP_Text resultText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_Text backendText;
    [SerializeField] Button runButton;
    [SerializeField] Button verifyButton;
    [SerializeField] Button notebookButton;

    [Header("Colours")]
    [SerializeField] Color amber = new Color32(0xff, 0xb2, 0x3f, 0xff);
    [SerializeField] Color bright = new Color32(0xff, 0xe2, 0xa8, 0xff);
    [SerializeField] Color dim = new Color32(0xa8, 0x70, 0x1f, 0xff);
    [SerializeField] Color faint = new Color32(0x5a, 0x3c, 0x12, 0xff);
    [SerializeField] Color teal = new Color32(0x3f, 0xe0, 0xb5, 0xff);
    [SerializeField] Color alert = new Color32(0xff, 0x6a, 0x4a, 0xff);
    [SerializeField] Color lampOff = new Color32(0x3a, 0x26, 0x0b, 0xff);

    public UnityEvent onClose = new UnityEvent();
    public UnityEvent onVerified = new UnityEvent();
    public UnityEvent onOpenNotebook = new UnityEvent();

    int errorQubit = -2;
    string syndrome;
    string finalData;
    int correction = -1;
    bool busy, verified;
    int openedFrame;

    public bool Verified => verified;
    /// <summary>Which backend produced the verified result, for the mission log.</summary>
    public string VerifiedBy { get; private set; } = "";
    public string Syndrome => syndrome;
    public int Correction => correction;
    public int ErrorQubit => errorQubit;

    void Awake()
    {
        for (int i = 0; i < fixSlots.Length; i++)
        {
            int k = i;
            fixSlots[i].onClick.AddListener(() => PlaceFix(k));
        }
        runButton.onClick.AddListener(RunDiagnosis);
        verifyButton.onClick.AddListener(Verify);
        notebookButton.onClick.AddListener(() => { if (!busy) onOpenNotebook.Invoke(); });
    }

    public void Open()
    {
        if (errorQubit == -2)
            errorQubit = scenarioErrorQubit < 0 ? Random.Range(0, 3) : scenarioErrorQubit > 2 ? -1 : scenarioErrorQubit;
        gameObject.SetActive(true);
        openedFrame = Time.frameCount;
        if (scanBar != null) scanBar.gameObject.SetActive(false);
        if (syndrome == null)
            Status("Run the diagnostic circuit. The parity checks will show which pairs of data qubits disagree.");
        Refresh();
    }

    void Update()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null);

        var kb = Keyboard.current;
        if (kb == null || busy || Time.frameCount == openedFrame) return;

        if (kb.escapeKey.wasPressedThisFrame) onClose.Invoke();
        else if (kb.rKey.wasPressedThisFrame) RunDiagnosis();
        else if (kb.vKey.wasPressedThisFrame) Verify();
        else if (kb.nKey.wasPressedThisFrame) onOpenNotebook.Invoke();
        else if (kb.digit1Key.wasPressedThisFrame) PlaceFix(0);
        else if (kb.digit2Key.wasPressedThisFrame) PlaceFix(1);
        else if (kb.digit3Key.wasPressedThisFrame) PlaceFix(2);
        else if (kb.digit0Key.wasPressedThisFrame) ClearFix();
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            if (syndrome == null) RunDiagnosis(); else Verify();
        }
    }

    // ---- Player actions ----

    public void RunDiagnosis()
    {
        if (busy || verified) return;
        var request = new QuantumRequest { action = "diagnose", error_qubit = errorQubit };
        StartCoroutine(Run(request, "RUNNING DIAGNOSTIC CIRCUIT", OnDiagnosis));
    }

    public void PlaceFix(int qubit)
    {
        if (busy || verified) return;
        if (syndrome == null) { Status("Run the diagnosis first. The FIX column unlocks once the syndrome is known."); return; }
        correction = correction == qubit ? -1 : qubit;
        resultText.text = "";
        Refresh();
    }

    public void ClearFix()
    {
        if (busy || verified || syndrome == null) return;
        correction = -1;
        Refresh();
    }

    public void Verify()
    {
        if (busy || verified) return;
        if (syndrome == null) { Status("Run the diagnosis first."); return; }
        var request = new QuantumRequest { action = "correct", error_qubit = errorQubit, correction_qubit = correction };
        StartCoroutine(Run(request, "RUNNING CORRECTED CIRCUIT", OnVerify));
    }

    // ---- Backend round trip ----

    IEnumerator Run(QuantumRequest request, string label, System.Action<QuantumResponse> handle)
    {
        busy = true;
        SetButtons(false);
        resultText.text = "";
        var scan = StartCoroutine(Scan(label));
        QuantumResponse response = null;
        yield return backend.Send(request, r => response = r);
        StopCoroutine(scan);
        if (scanBar != null) scanBar.gameObject.SetActive(false);
        busy = false;
        handle(response);
        Refresh();
    }

    IEnumerator Scan(string label)
    {
        if (scanBar != null) scanBar.gameObject.SetActive(true);
        float start = Time.unscaledTime;
        while (true)
        {
            float t = Time.unscaledTime - start;
            if (scanBar != null)
                scanBar.anchoredPosition = new Vector2(Mathf.Lerp(scanRange.x, scanRange.y, Mathf.PingPong(t * 0.9f, 1f)), scanBar.anchoredPosition.y);
            string wait = t > 6f ? "\nWaiting for the server. A sleeping server can take up to a minute to wake." : "";
            Status($"{label} ON {backend.Label}{new string('.', 1 + (int)(t * 3f) % 3)}{wait}");
            yield return null;
        }
    }

    void OnDiagnosis(QuantumResponse r)
    {
        if (!Valid(r) || !IsBits(r.syndrome, 2)) { BackendError(r, "an invalid syndrome"); return; }
        syndrome = r.syndrome;
        finalData = null;
        Status($"SYNDROME {syndrome} MEASURED.\nPlace an X in the FIX column on the qubit it points to (none for 00), then run the corrected circuit.{SourceNote(r)}");
    }

    void OnVerify(QuantumResponse r)
    {
        if (!Valid(r) || !IsBits(r.final_data, 3)) { BackendError(r, "invalid data bits"); return; }
        if (IsBits(r.syndrome, 2)) syndrome = r.syndrome;
        finalData = r.final_data;

        if (r.verified)
        {
            verified = true;
            VerifiedBy = r.source == QuantumBackend.StandInLabel ? "OFFLINE STAND-IN" : "QISKIT AER";
            resultText.text = $"<color={Hex(teal)}>VERIFIED</color>";
            Status($"Encoded state restored: the data reads {finalData}. MISSION REPORT is ready to transmit.{SourceNote(r)}");
            onVerified.Invoke();
        }
        else
        {
            int wrong = 0;
            foreach (char c in finalData) if (c == '1') wrong++;
            string how = wrong == 1 ? "One data bit is still flipped." : $"{wrong} data bits are flipped.";
            resultText.text = $"<color={Hex(alert)}>VERIFICATION FAILED</color>";
            Status($"Data reads {finalData}, not 000. {how}\nRe-read the syndrome table [N], move the X and run again.{SourceNote(r)}");
        }
    }

    bool Valid(QuantumResponse r) => r != null && r.success;

    void BackendError(QuantumResponse r, string what)
    {
        resultText.text = $"<color={Hex(alert)}>BACKEND ERROR</color>";
        string reason = r == null ? "No reply." : !r.success ? r.message : $"The backend returned {what}.";
        Status($"{reason}\nNothing was verified. Check the backend connection and try again.");
    }

    string SourceNote(QuantumResponse r) =>
        r.source == QuantumBackend.StandInLabel ? $"\n<color={Hex(alert)}>(Offline stand-in, not Qiskit.)</color>" : "";

    static bool IsBits(string s, int n)
    {
        if (s == null || s.Length != n) return false;
        foreach (char c in s) if (c != '0' && c != '1') return false;
        return true;
    }

    // ---- Display ----

    void Refresh()
    {
        bool a = syndrome != null && syndrome[0] == '1';
        bool b = syndrome != null && syndrome[1] == '1';
        lampA.color = a ? teal : lampOff;
        lampB.color = b ? teal : lampOff;
        syndromeText.text = syndrome == null ? $"<color={Hex(faint)}>SYNDROME --</color>" : $"SYNDROME <color={Hex(bright)}>{syndrome}</color>";
        dataText.text = finalData == null ? $"<color={Hex(faint)}>DATA OUT ---</color>" : $"DATA OUT <color={Hex(bright)}>{finalData}</color>";
        backendText.text = backend.IsStandIn ? $"<color={Hex(alert)}>{backend.Label}</color>" : backend.Label;

        for (int i = 0; i < fixSlots.Length; i++)
        {
            bool placed = correction == i;
            bool open = syndrome != null && !verified;
            var label = fixSlots[i].GetComponentInChildren<TMP_Text>();
            label.text = placed ? "X" : "·";
            label.color = placed ? teal : open ? dim : faint;
            fixSlots[i].targetGraphic.color = placed ? teal : open ? dim : faint;
            fixSlots[i].interactable = open && !busy;
        }

        for (int i = 0; i < faultMarks.Length; i++)
        {
            bool reveal = verified && errorQubit == i;
            faultMarks[i].text = reveal ? "X" : verified ? "" : "?";
            faultMarks[i].color = reveal ? teal : dim;
        }

        SetButtons(!busy);
        codeText.text = Code();
    }

    void SetButtons(bool on)
    {
        runButton.interactable = on && !verified;
        verifyButton.interactable = on && !verified && syndrome != null;
        notebookButton.interactable = on;
        foreach (var s in fixSlots) s.interactable = on && syndrome != null && !verified;
    }

    string Code()
    {
        string c = Hex(dim), t = Hex(teal), f = Hex(faint);
        var sb = new StringBuilder();
        sb.Append($"data, anc = QuantumRegister(3, \"d\"), QuantumRegister(2, \"a\")\n");
        sb.Append($"syn, out  = ClassicalRegister(2, \"syn\"), ClassicalRegister(3, \"out\")\n");
        sb.Append($"qc = QuantumCircuit(data, anc, syn, out)   <color={c}># all start in |0⟩</color>\n");
        if (verified && errorQubit >= 0) sb.Append($"qc.x(data[{errorQubit}])                          <color={c}># fault (revealed)</color>\n");
        else sb.Append($"<color={c}># qc.x(data[?])                        # fault injected by scenario</color>\n");
        sb.Append($"qc.cx(data[0], anc[0]); qc.cx(data[1], anc[0])  <color={c}># A = q0 ⊕ q1</color>\n");
        sb.Append($"qc.cx(data[1], anc[1]); qc.cx(data[2], anc[1])  <color={c}># B = q1 ⊕ q2</color>\n");
        sb.Append($"qc.measure(anc, syn)                       <color={c}># syndrome {(syndrome ?? "")}</color>\n");
        if (syndrome == null) sb.Append($"<color={f}># FIX: run the diagnosis first</color>\n");
        else if (correction < 0) sb.Append($"<color={t}># no correction</color>                       <color={c}># your fix</color>\n");
        else sb.Append($"<color={t}>qc.x(data[{correction}])</color>                          <color={c}># your fix</color>\n");
        sb.Append($"qc.measure(data, out)                      <color={c}># verify: expect 000</color>\n");
        sb.Append($"<color={f}>AerSimulator().run(transpile(qc, sim), shots=256)</color>");
        return sb.ToString();
    }

    void Status(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}
