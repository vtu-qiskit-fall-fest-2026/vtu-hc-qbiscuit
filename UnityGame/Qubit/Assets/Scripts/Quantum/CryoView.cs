using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// "Hold the Qubit": a three-stage remote experiment on the lunar vessel's cryogenic core.
/// 1. Measure: scan three qubits and find the one losing its state.
/// 2. Decay: hold the faulty qubit for longer and longer and chart its survival.
/// 3. Cool: trade cooling power against hold time to keep it alive for 40 µs.
/// Every scan is a Qiskit Aer simulation with a thermal-relaxation noise model.
/// </summary>
public class CryoView : MonoBehaviour
{
    [SerializeField] QuantumBackend backend;
    [SerializeField] int shots = 200;
    [SerializeField] float minRunSeconds = 0.7f;
    [SerializeField] TMP_Text stageStrip;
    [SerializeField] TMP_Text codeLine;
    [Tooltip("The console's bottom key bar; each stage shows only its own keys.")]
    [SerializeField] TMP_Text keysText;
    [SerializeField] string[] stageKeys =
    {
        "[1-3] SCAN, THEN SELECT THE FAULTY QUBIT   [ESC] BACK",
        "[←/→] HOLD TIME   [ENTER] FIRE 200 SHOTS   [ESC] BACK",
        "[←/→] COOLING DIAL   [↑/↓] HOLD TIME   [ENTER] FIRE   [E] ENCODE   [ESC] BACK",
    };
    [SerializeField] GameObject[] stagePanels = new GameObject[3];

    [Header("Stage 1: measure")]
    [SerializeField] Button[] cardButtons = new Button[3];
    [SerializeField] RectTransform[] keptBars = new RectTransform[3];
    [SerializeField] RectTransform[] lostBars = new RectTransform[3];
    [SerializeField] TMP_Text[] cardReadouts = new TMP_Text[3];
    [SerializeField] TMP_Text stage1Status;
    [SerializeField] float barWidth = 120f;
    [SerializeField] float stage1HoldUs = 20f;

    [Header("Stage 2: decay")]
    [SerializeField] RectTransform chartArea;
    [SerializeField] RectTransform pointPrefab;
    [SerializeField] Slider decayHold;
    [SerializeField] TMP_Text decayHoldText;
    [SerializeField] Button decayFire;
    [SerializeField] TMP_Text stage2Status;
    [SerializeField] float chartMaxUs = 100f;

    [Header("Stage 3: cool")]
    [SerializeField] RotaryDial dial;
    [SerializeField] RectTransform powerFill;
    [SerializeField] Image powerFillImage;
    [SerializeField] float powerBarWidth = 90f;
    [SerializeField] TMP_Text powerText;
    [SerializeField] TMP_Text tempText;
    [SerializeField] Slider coolHold;
    [SerializeField] TMP_Text coolHoldText;
    [SerializeField] Button coolFire;
    [SerializeField] Button encodeButton;
    [SerializeField] RectTransform coolKeptBar;
    [SerializeField] RectTransform coolLostBar;
    [SerializeField] TMP_Text coolReadout;
    [SerializeField] TMP_Text targetText;
    [Tooltip("Live T1 prediction for the dial setting, so cooling shows its effect before firing.")]
    [SerializeField] TMP_Text t1Text;
    [Tooltip("Matches TEMPERATURE_EXPONENT in the backend's noise.py: T1 = T1(80 mK) * (80 / T)^exponent.")]
    [SerializeField] float t1Exponent = 1.6f;
    [SerializeField] TMP_Text stage3Status;
    [SerializeField] float coolBarWidth = 110f;
    [SerializeField] float requiredHoldUs = 40f;
    [SerializeField] float targetSurvival = 0.9f;
    [SerializeField] int attemptsPerCycle = 5;

    [Header("Colours")]
    [SerializeField] Color amber = new Color32(0xd0, 0xa4, 0x6d, 0xff);
    [SerializeField] Color teal = new Color32(0x8f, 0xbc, 0xa0, 0xff);
    [SerializeField] Color alert = new Color32(0xc8, 0x79, 0x63, 0xff);
    [SerializeField] Color dim = new Color32(0x71, 0x87, 0x80, 0xff);

    public UnityEvent onClose = new UnityEvent();
    public UnityEvent onCompleted = new UnityEvent();

    const float WarmMk = 80f, ColdestMk = 10f;
    static readonly string[] Names = { "A", "B", "C" };

    int stage;
    bool busy;
    int openedFrame;
    readonly float[] scanned = { -1f, -1f, -1f };
    readonly List<Vector2> decayPoints = new List<Vector2>();
    float estimatedT1;
    int attempts;
    bool won, encodedDone;
    float lastSurvival = -1f;

    public bool Completed => won;
    public bool Busy => busy;
    public float EstimatedT1 => estimatedT1;
    public float WinningPower { get; private set; }

    void Awake()
    {
        for (int i = 0; i < 3; i++) { int k = i; cardButtons[i].onClick.AddListener(() => CardPressed(k)); }
        decayFire.onClick.AddListener(FireDecay);
        coolFire.onClick.AddListener(() => FireCool(false));
        encodeButton.onClick.AddListener(() => FireCool(true));
        decayHold.onValueChanged.AddListener(_ => RefreshDecayHold());
        coolHold.onValueChanged.AddListener(_ => RefreshCool());
        dial.onChanged.AddListener(_ => RefreshCool());
        dial.onHitLimit.AddListener(() => Status(stage3Status, "Power limit reached. The cryostat can't draw more than 85%."));
    }

    public void Open()
    {
        gameObject.SetActive(true);
        openedFrame = Time.frameCount;
        ShowStage(stage);
    }

    // ---- Stages ----

    void ShowStage(int s)
    {
        stage = s;
        for (int i = 0; i < stagePanels.Length; i++) stagePanels[i].SetActive(i == s);
        string[] labels = { "1 MEASURE", "2 DECAY", "3 COOL" };
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 3; i++)
        {
            string c = i == s ? Hex(amber) : i < s ? Hex(teal) : Hex(dim);
            sb.Append(i == s ? $"<color={c}>[{labels[i]}]</color>   " : $"<color={c}> {labels[i]} </color>   ");
        }
        stageStrip.text = sb.ToString();
        if (keysText != null && s < stageKeys.Length) keysText.text = stageKeys[s];
        if (s == 0) RefreshCards();
        if (s == 1) { RefreshDecayHold(); RefreshChart(); }
        if (s == 2) RefreshCool();
        UpdateCode();
    }

    // ---- Stage 1 ----

    void CardPressed(int i)
    {
        if (busy) return;
        if (scanned[i] < 0f) { StartCoroutine(Scan(i)); return; }
        // Selecting a qubit as the faulty one.
        if (i == 2)
        {
            Sfx.Play("lamp");
            ShowStage(1);
            Status(stage2Status, "Q-C selected. Hold it for different times and fire scans to chart how quickly it loses |1⟩.");
        }
        else
        {
            Sfx.Play("failed");
            Status(stage1Status, $"Q-{Names[i]} is within tolerance. Keep looking.");
        }
    }

    IEnumerator Scan(int i)
    {
        var req = new NoisyRequest { qubit = Names[i], temperature_mk = WarmMk, hold_us = stage1HoldUs, shots = shots };
        NoisyResponse r = null;
        yield return Run(req, x => r = x, stage1Status, $"SCANNING Q-{Names[i]}");
        if (r == null || !r.success) { Status(stage1Status, Err(r)); RefreshCards(); yield break; }
        scanned[i] = r.survival;
        Sfx.Play(r.survival < 0.85f ? "failed" : "lamp");
        int done = 0; foreach (var v in scanned) if (v >= 0f) done++;
        Status(stage1Status, done < 3
            ? $"Q-{Names[i]} kept |1⟩ in {Pct(r.survival)} of {shots} shots after {stage1HoldUs:0} µs.{Note(r)}"
            : $"All three scanned. One qubit should keep |1⟩ almost every time but doesn't. Select the faulty qubit.{Note(r)}");
        RefreshCards();
    }

    void RefreshCards()
    {
        for (int i = 0; i < 3; i++)
        {
            float v = scanned[i];
            bool has = v >= 0f;
            keptBars[i].sizeDelta = new Vector2(has ? Mathf.Max(1f, barWidth * v) : 1f, keptBars[i].sizeDelta.y);
            lostBars[i].sizeDelta = new Vector2(has ? Mathf.Max(1f, barWidth * (1f - v)) : 1f, lostBars[i].sizeDelta.y);
            cardReadouts[i].text = has ? $"|1⟩ {Pct(v)}   |0⟩ {Pct(1f - v)}" : "NOT SCANNED";
            var label = cardButtons[i].GetComponentInChildren<TMP_Text>();
            bool all = scanned[0] >= 0f && scanned[1] >= 0f && scanned[2] >= 0f;
            label.text = !has ? $"[{i + 1}] SCAN Q-{Names[i]}" : all ? $"[{i + 1}] SELECT AS FAULTY" : "SCANNED";
            cardButtons[i].interactable = !busy && (!has || all);
        }
    }

    // ---- Stage 2 ----

    void RefreshDecayHold() => decayHoldText.text = $"HOLD {decayHold.value:0} µs";

    void FireDecay()
    {
        if (busy || stage != 1) return;
        if (estimatedT1 > 0f) { ShowStage(2); Status(stage3Status, "The calculation needs Q-C to hold |1⟩ for 40 µs with at least 90% survival. Use cooling to buy time."); return; }
        StartCoroutine(FireDecayRoutine());
    }

    IEnumerator FireDecayRoutine()
    {
        float hold = decayHold.value;
        var req = new NoisyRequest { qubit = "C", temperature_mk = WarmMk, hold_us = hold, shots = shots };
        NoisyResponse r = null;
        yield return Run(req, x => r = x, stage2Status, $"HOLDING Q-C FOR {hold:0} µs");
        if (r == null || !r.success) { Status(stage2Status, Err(r)); yield break; }
        decayPoints.RemoveAll(p => Mathf.Approximately(p.x, hold));
        decayPoints.Add(new Vector2(hold, r.survival));
        Sfx.Play("lamp");
        RefreshChart();

        bool above = decayPoints.Exists(p => p.y > 0.5f), below = decayPoints.Exists(p => p.y <= 0.5f);
        if (decayPoints.Count >= 4 && above && below)
        {
            estimatedT1 = EstimateT1();
            Sfx.Play("verified", 0.7f);
            decayFire.GetComponentInChildren<TMP_Text>().text = "[ENTER] CONTINUE";
            Status(stage2Status, $"Kept {Pct(r.survival)} at {hold:0} µs. Survival halves at about {estimatedT1 * Mathf.Log(2f):0} µs.\n<color={Hex(teal)}>Q-C LIFETIME T1 ≈ {estimatedT1:0} µs.</color> Heat and time are draining the qubit.{Note(r)}");
        }
        else
        {
            string need = decayPoints.Count < 4 ? $"{4 - decayPoints.Count} more point(s) needed" : below ? "now try a shorter hold" : "try a longer hold, until it keeps |1⟩ less than half the time";
            Status(stage2Status, $"Kept {Pct(r.survival)} at {hold:0} µs. {need}.{Note(r)}");
        }
    }

    float EstimateT1()
    {
        // Each point gives T1 = -t / ln(survival); average the usable ones.
        float sum = 0f; int n = 0;
        foreach (var p in decayPoints)
            if (p.x > 0f && p.y > 0.02f && p.y < 0.98f) { sum += -p.x / Mathf.Log(p.y); n++; }
        return n > 0 ? sum / n : 0f;
    }

    void RefreshChart()
    {
        for (int i = chartArea.childCount - 1; i >= 0; i--)
        {
            var c = chartArea.GetChild(i);
            if (c.name.StartsWith("Point")) Destroy(c.gameObject);
        }
        var size = chartArea.rect.size;
        foreach (var p in decayPoints)
        {
            var dot = Instantiate(pointPrefab, chartArea);
            dot.name = "Point";
            dot.gameObject.SetActive(true);
            dot.anchoredPosition = new Vector2(p.x / chartMaxUs * size.x, p.y * size.y);
            dot.GetComponent<Image>().color = p.y > 0.5f ? amber : alert;
        }
    }

    // ---- Stage 3 ----

    float Temperature => Mathf.Lerp(WarmMk, ColdestMk, dial.Value / 100f);

    void RefreshCool()
    {
        float power = dial.Value;
        powerFill.sizeDelta = new Vector2(Mathf.Max(1f, powerBarWidth * power / 100f), powerFill.sizeDelta.y);
        powerFillImage.color = power >= dial.Limit - 0.5f ? alert : power > 60f ? amber : teal;
        powerText.text = $"POWER {power:0}%";
        int dots = Mathf.Clamp(Mathf.CeilToInt((Temperature - ColdestMk) / (WarmMk - ColdestMk) * 5f), 1, 5);
        tempText.text = $"CORE {Temperature:0.0} mK  <color={Hex(alert)}>{new string('●', dots)}</color><color={Hex(dim)}>{new string('○', 5 - dots)}</color>";
        if (t1Text != null && estimatedT1 > 0f)
        {
            // Survival after a hold is about e^(-hold / T1), so the target needs T1 >= -hold / ln(target).
            float predicted = estimatedT1 * Mathf.Pow(WarmMk / Temperature, t1Exponent);
            float needed = -Mathf.Max(coolHold.value, requiredHoldUs) / Mathf.Log(targetSurvival);
            string c = Hex(predicted >= needed ? teal : alert);
            t1Text.text = $"Q-C LIFETIME  <color={c}>T1 ≈ {predicted:0} µs</color>   NEED ≥ {needed:0} µs";
        }
        bool okHold = coolHold.value >= requiredHoldUs;
        coolHoldText.text = $"HOLD {coolHold.value:0} µs  " + (okHold ? $"<color={Hex(teal)}>OK</color>" : $"<color={Hex(alert)}>NEEDS ≥ {requiredHoldUs:0}</color>");
        targetText.text = won
            ? $"TARGET  |1⟩ ≥ {targetSurvival * 100f:0}%   <color={Hex(teal)}>✓ STABLE</color>"
            : $"TARGET  |1⟩ ≥ {targetSurvival * 100f:0}%   ATTEMPTS {attempts}/{attemptsPerCycle}";
        encodeButton.gameObject.SetActive(won && !encodedDone);
        UpdateCode();
    }

    void FireCool(bool encoded)
    {
        if (busy || stage != 2) return;
        if (encoded && !won) return;
        if (!encoded && won) { onClose.Invoke(); return; }
        if (!encoded && coolHold.value < requiredHoldUs)
        {
            Sfx.Play("failed");
            Status(stage3Status, $"The calculation needs the qubit held for at least {requiredHoldUs:0} µs. Move the HOLD slider up.");
            return;
        }
        StartCoroutine(FireCoolRoutine(encoded));
    }

    IEnumerator FireCoolRoutine(bool encoded)
    {
        var req = new NoisyRequest { qubit = "C", temperature_mk = Temperature, hold_us = coolHold.value, shots = shots, encoded = encoded };
        NoisyResponse r = null;
        yield return Run(req, x => r = x, stage3Status, encoded ? "RUNNING ENCODED ×3 (MAJORITY VOTE)" : $"FIRING {shots} SHOTS AT {Temperature:0.0} mK");
        if (r == null || !r.success) { Status(stage3Status, Err(r)); yield break; }

        coolKeptBar.sizeDelta = new Vector2(Mathf.Max(1f, coolBarWidth * r.survival), coolKeptBar.sizeDelta.y);
        coolLostBar.sizeDelta = new Vector2(Mathf.Max(1f, coolBarWidth * (1f - r.survival)), coolLostBar.sizeDelta.y);
        coolReadout.text = $"|1⟩ {r.Kept,3}  {Pct(r.survival)}\n|0⟩ {shots - r.Kept,3}  {Pct(1f - r.survival)}";

        if (encoded)
        {
            encodedDone = true;
            Sfx.Play("verified");
            Status(stage3Status, $"Encoded across three qubits with a majority vote: survival {Pct(lastSurvival)} → <color={Hex(teal)}>{Pct(r.survival)}</color>.\nNoise can't be removed completely, so we correct it. [ENTER] RETURN{Note(r)}");
            RefreshCool();
            yield break;
        }

        lastSurvival = r.survival;
        attempts++;
        if (r.survival >= targetSurvival)
        {
            won = true;
            WinningPower = dial.Value;
            Sfx.Play("verified");
            coolFire.GetComponentInChildren<TMP_Text>().text = "[ENTER] DONE";
            Status(stage3Status, $"<color={Hex(teal)}>Q-C STABLE: {Pct(r.survival)} survival at {coolHold.value:0} µs.</color> Cooling extended T1 to {r.t1_us:0} µs.\nTry [E] ENCODE ×3 to see error correction push it further.{Note(r)}");
            onCompleted.Invoke();
        }
        else
        {
            Sfx.Play("failed");
            string hint = coolHold.value > requiredHoldUs + 0.5f ? "Holding longer than needed costs survival. " : "";
            hint += dial.Value < dial.Limit - 1f ? "More cooling would extend T1." : "Cooling is maxed. Shorten the hold to the minimum.";
            if (attempts >= attemptsPerCycle)
            {
                attempts = 0;
                Status(stage3Status, $"Kept {Pct(r.survival)}. Attempts used: coolant cycle reset. {hint}{Note(r)}");
            }
            else Status(stage3Status, $"Kept {Pct(r.survival)}, below {targetSurvival * 100f:0}%. {hint}{Note(r)}");
        }
        RefreshCool();
    }

    // ---- Shared ----

    IEnumerator Run(NoisyRequest req, System.Action<NoisyResponse> done, TMP_Text status, string label)
    {
        busy = true;
        Sfx.Play("compute");
        Status(status, $"{label} ON {backend.Label}...");
        if (stage == 0) RefreshCards();
        float t0 = Time.unscaledTime;
        NoisyResponse r = null;
        yield return backend.SendNoisy(req, x => r = x);
        while (Time.unscaledTime - t0 < minRunSeconds) yield return null;
        busy = false;
        done(r);
    }

    void Update()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null);
        var kb = Keyboard.current;
        if (kb == null || busy || Time.frameCount == openedFrame) return;
        bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;

        if (kb.escapeKey.wasPressedThisFrame) { onClose.Invoke(); return; }
        if (stage == 0)
        {
            if (kb.digit1Key.wasPressedThisFrame) CardPressed(0);
            else if (kb.digit2Key.wasPressedThisFrame) CardPressed(1);
            else if (kb.digit3Key.wasPressedThisFrame) CardPressed(2);
        }
        else if (stage == 1)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) decayHold.value -= 5f;
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) decayHold.value += 5f;
            else if (enter) FireDecay();
        }
        else
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dial.Nudge(-5f);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dial.Nudge(5f);
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) coolHold.value -= 5f;
            else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) coolHold.value += 5f;
            else if (kb.eKey.wasPressedThisFrame) FireCool(true);
            else if (enter) FireCool(false);
        }
    }

    void UpdateCode()
    {
        if (codeLine == null) return;
        string c = Hex(dim);
        float temp = stage == 2 ? Temperature : WarmMk;
        float hold = stage == 0 ? stage1HoldUs : stage == 1 ? decayHold.value : coolHold.value;
        codeLine.text = $"qc.x(0)  <color={c}># prepare |1⟩</color>   " +
                        $"qc.append(thermal_relaxation_error(T1, T2, {hold:0}), [0])  <color={c}># hold at {temp:0.0} mK</color>   qc.measure_all()";
    }

    void Status(TMP_Text t, string s) { if (t != null) t.text = s; }
    string Note(NoisyResponse r) => r != null && r.source == QuantumBackend.StandInLabel ? $"\n<color={Hex(alert)}>(Offline stand-in, not Qiskit.)</color>" : "";
    string Err(NoisyResponse r) => $"<color={Hex(alert)}>{(r == null ? "No reply from the backend." : r.error)}</color>";
    static string Pct(float v) => $"{v * 100f:0}%";
    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}
