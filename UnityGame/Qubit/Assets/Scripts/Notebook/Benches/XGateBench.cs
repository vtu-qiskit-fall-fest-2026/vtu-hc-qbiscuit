using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Flip one qubit with X, measure it, and see that two flips cancel.</summary>
public class XGateBench : NotebookBench
{
    [SerializeField] Image qubitBox;
    [SerializeField] TMP_Text ketText;
    [SerializeField] TMP_Text historyText;
    [SerializeField] TMP_Text measureText;
    [SerializeField] Button applyButton;
    [SerializeField] Button measureButton;
    [SerializeField] Button resetButton;

    bool one;
    int flips;

    void Awake()
    {
        Bind(applyButton, ApplyX);
        Bind(measureButton, Measure);
        Bind(resetButton, ResetBench);
    }

    public override void ResetBench()
    {
        one = false;
        flips = 0;
        measureText.text = "Press MEASURE to read the qubit.";
        Refresh();
    }

    public void ApplyX()
    {
        one = !one;
        flips++;
        measureText.text = "Press MEASURE to read the qubit.";
        Refresh();
        StopAllCoroutines();
        StartCoroutine(Pulse());
    }

    public void Measure()
    {
        int bit = one ? 1 : 0;
        measureText.text = $"MEASURED: <color={Hex(bright)}>{bit}</color>   {Ket(one)} is a basis state, so every measurement gives {bit}.";
    }

    void Refresh()
    {
        qubitBox.color = amber;
        ketText.text = Ket(one);

        var gates = new StringBuilder();
        if (flips == 0) gates.Append("none");
        for (int i = 0; i < Mathf.Min(flips, 8); i++) gates.Append("X ");
        if (flips > 8) gates.Append("...");

        string note;
        if (flips == 0) note = "Apply X and watch |0⟩ and |1⟩ swap.";
        else if (flips % 2 == 0) note = $"<color={Hex(teal)}>X·X = I. Two flips cancel out. That is how a correction undoes a bit-flip error.</color>";
        else note = "One X swapped the state, like a classical NOT.";

        historyText.text = $"GATES  {gates}\nSTATE  {Ket(one)}\n\n{note}";
    }

    IEnumerator Pulse()
    {
        qubitBox.color = teal;
        yield return new WaitForSecondsRealtime(0.15f);
        qubitBox.color = amber;
    }

    public override void HandleKeys(Keyboard kb)
    {
        if (kb.xKey.wasPressedThisFrame || kb.digit1Key.wasPressedThisFrame) ApplyX();
        else if (kb.mKey.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame) Measure();
        else if (kb.rKey.wasPressedThisFrame) ResetBench();
    }
}
