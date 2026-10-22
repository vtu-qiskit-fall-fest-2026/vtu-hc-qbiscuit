using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The three-qubit parity checks as a toy. EXPLORE: flip data qubits and watch lamps A and B.
/// CHALLENGE: a hidden single flip (or none) is injected; read the lamps and pick the fix.
/// </summary>
public class ParityBench : NotebookBench
{
    [SerializeField] Button[] qubitButtons = new Button[3];
    [SerializeField] TMP_Text[] qubitValues = new TMP_Text[3];
    [SerializeField] Image lampA;
    [SerializeField] Image lampB;
    [SerializeField] TMP_Text syndromeText;
    [SerializeField] TMP_Text diagnosisText;
    [SerializeField] TMP_Text streakText;
    [SerializeField] Button modeButton;
    [SerializeField] GameObject fixRow;
    [Tooltip("X on q0, X on q1, X on q2, no correction.")]
    [SerializeField] Button[] fixButtons = new Button[4];
    [SerializeField] Button nextButton;

    readonly bool[] q = new bool[3];
    bool challenge;
    bool answered;
    int hidden = -1;
    int streak, best;

    void Awake()
    {
        for (int i = 0; i < 3; i++)
        {
            int k = i;
            Bind(qubitButtons[i], () => Toggle(k));
        }
        for (int i = 0; i < 4; i++)
        {
            int k = i == 3 ? -1 : i;
            Bind(fixButtons[i], () => Fix(k));
        }
        Bind(modeButton, ToggleMode);
        Bind(nextButton, NextRound);
    }

    public override void ResetBench()
    {
        challenge = false;
        Clear();
        Refresh();
    }

    void Clear()
    {
        for (int i = 0; i < 3; i++) q[i] = false;
    }

    public void Toggle(int i)
    {
        if (challenge) return;
        q[i] = !q[i];
        Refresh();
    }

    public void ToggleMode()
    {
        challenge = !challenge;
        if (challenge) NextRound();
        else { Clear(); Refresh(); }
    }

    public void NextRound()
    {
        if (!challenge) return;
        Clear();
        // One in four rounds has no error, so the player learns syndrome 00 too.
        hidden = Random.Range(-1, 3);
        if (hidden >= 0) q[hidden] = true;
        answered = false;
        Refresh();
    }

    public void Fix(int i)
    {
        if (!challenge || answered) return;
        answered = true;
        if (i >= 0) q[i] = !q[i];
        bool ok = !q[0] && !q[1] && !q[2];
        streak = ok ? streak + 1 : 0;
        best = Mathf.Max(best, streak);

        string data = Bits();
        if (ok)
        {
            string where = hidden < 0 ? "There was no error, and you left it alone." : $"The flip was on q{hidden}.";
            diagnosisText.text = $"<color={Hex(teal)}>VERIFIED. Data reads {data}.</color> {where}";
        }
        else
        {
            string why = hidden < 0 ? "There was no error, so your X created one."
                       : i < 0 ? "The error is still there."
                       : "Now two bits are wrong.";
            diagnosisText.text = $"<color={Hex(alert)}>FAILED. Data reads {data}.</color> {why} The error was on {(hidden < 0 ? "no qubit" : "q" + hidden)}.";
        }
        Refresh();
    }

    string Bits() => $"{(q[0] ? 1 : 0)}{(q[1] ? 1 : 0)}{(q[2] ? 1 : 0)}";

    void Refresh()
    {
        bool a = q[0] ^ q[1];
        bool b = q[1] ^ q[2];
        string s = $"{(a ? 1 : 0)}{(b ? 1 : 0)}";
        bool hideData = challenge && !answered;

        for (int i = 0; i < 3; i++)
        {
            qubitValues[i].text = hideData ? "?" : (q[i] ? "1" : "0");
            qubitButtons[i].interactable = !challenge;
            Paint(qubitButtons[i], !hideData && q[i] ? teal : amber, !hideData && q[i] ? teal : amber);
        }
        lampA.color = a ? teal : off;
        lampB.color = b ? teal : off;
        syndromeText.text = $"SYNDROME  <color={Hex(bright)}>{s}</color>";

        var modeLabel = modeButton.GetComponentInChildren<TMP_Text>();
        modeLabel.text = challenge ? "MODE: CHALLENGE" : "MODE: EXPLORE";
        streakText.text = challenge ? $"STREAK {streak}   BEST {best}" : "CLICK A QUBIT TO FLIP IT";
        fixRow.SetActive(challenge);
        nextButton.gameObject.SetActive(challenge && answered);
        foreach (var f in fixButtons) f.interactable = challenge && !answered;

        if (!challenge) diagnosisText.text = Explain(s);
        else if (!answered) diagnosisText.text = "A fault was injected. Read lamps A and B, then choose a correction.";
    }

    string Explain(string s)
    {
        int flipped = (q[0] ? 1 : 0) + (q[1] ? 1 : 0) + (q[2] ? 1 : 0);
        int pointed = s == "10" ? 0 : s == "11" ? 1 : s == "01" ? 2 : -1;
        switch (flipped)
        {
            case 0: return "All three agree. Both lamps are off: syndrome 00, no correction needed.";
            case 1: return $"Only q{pointed} flipped. It is the qubit in every pair that differs and in no pair that agrees, so syndrome {s} points to it. Fix: X on q{pointed}.";
            case 2: return $"<color={Hex(alert)}>Two flips!</color> Syndrome {s} points to q{pointed}, so the \"fix\" would give 111. Two errors fool this code (entry 20).";
            default: return "111 is the other code word, |1_L⟩. Every pair agrees, so the syndrome is 00. The checks never reveal the logical value.";
        }
    }

    public override void HandleKeys(Keyboard kb)
    {
        if (kb.mKey.wasPressedThisFrame) { ToggleMode(); return; }
        if ((kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) && answered) { NextRound(); return; }
        for (int i = 0; i < 3; i++)
        {
            var key = i == 0 ? kb.digit1Key : i == 1 ? kb.digit2Key : kb.digit3Key;
            if (!key.wasPressedThisFrame) continue;
            if (challenge) Fix(i); else Toggle(i);
        }
        if (kb.digit0Key.wasPressedThisFrame || kb.nKey.wasPressedThisFrame) Fix(-1);
        if (kb.rKey.wasPressedThisFrame && !challenge) { Clear(); Refresh(); }
    }
}
