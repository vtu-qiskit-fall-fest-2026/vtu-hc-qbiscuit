using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Set a control and a target, apply CNOT, and see the matching truth-table row.</summary>
public class CnotBench : NotebookBench
{
    [SerializeField] TMP_Text controlKet;
    [SerializeField] TMP_Text targetKet;
    [SerializeField] TMP_Text tableText;
    [SerializeField] TMP_Text resultText;
    [SerializeField] Button controlButton;
    [SerializeField] Button targetButton;
    [SerializeField] Button applyButton;
    [SerializeField] Button resetButton;

    bool control, target;
    int row;

    void Awake()
    {
        Bind(controlButton, ToggleControl);
        Bind(targetButton, ToggleTarget);
        Bind(applyButton, Apply);
        Bind(resetButton, ResetBench);
    }

    public override void ResetBench()
    {
        control = target = false;
        resultText.text = "Set the inputs, then apply CNOT.";
        Refresh();
    }

    public void ToggleControl()
    {
        control = !control;
        resultText.text = "Set the inputs, then apply CNOT.";
        Refresh();
    }

    public void ToggleTarget()
    {
        target = !target;
        resultText.text = "Set the inputs, then apply CNOT.";
        Refresh();
    }

    public void Apply()
    {
        bool before = target;
        if (control) target = !target;
        resultText.text = control
            ? $"Control is 1, so the target flipped: {Ket(before)} → {Ket(target)}."
            : "Control is 0, so nothing happened. The control itself never changes.";
        Refresh(before);
    }

    void Refresh() => Refresh(target);

    void Refresh(bool targetIn)
    {
        controlKet.text = Ket(control);
        targetKet.text = Ket(target);
        row = (control ? 2 : 0) + (targetIn ? 1 : 0);

        var sb = new StringBuilder();
        sb.Append($"<color={Hex(dim)}>CTRL TARG → CTRL TARG</color>\n");
        for (int i = 0; i < 4; i++)
        {
            int c = i / 2, t = i % 2, tOut = t ^ c;
            string line = $"  {c}    {t}       {c}    {tOut}";
            sb.Append(i == row ? $"<color={Hex(teal)}><b>{line}  ◂</b></color>\n" : $"<color={Hex(amber)}>{line}</color>\n");
        }
        tableText.text = sb.ToString();
    }

    public override void HandleKeys(Keyboard kb)
    {
        if (kb.digit1Key.wasPressedThisFrame) ToggleControl();
        else if (kb.digit2Key.wasPressedThisFrame) ToggleTarget();
        else if (kb.cKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Apply();
        else if (kb.rKey.wasPressedThisFrame) ResetBench();
    }
}
