using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Choose |α|², fire shots, and watch the 0/1 histogram build up.</summary>
public class ShotsBench : NotebookBench
{
    [SerializeField] TMP_Text stateText;
    [SerializeField] TMP_Text lastText;
    [SerializeField] TMP_Text countsText;
    [SerializeField] RectTransform barZero;
    [SerializeField] RectTransform barOne;
    [SerializeField] float barMax = 56f;
    [SerializeField] Button minusButton;
    [SerializeField] Button plusButton;
    [SerializeField] Button fireOneButton;
    [SerializeField] Button fireManyButton;
    [SerializeField] Button clearButton;
    [SerializeField] int burstShots = 100;
    [SerializeField] float burstSeconds = 1.2f;

    int pZero = 50;
    int zeros, ones;
    bool firing;

    void Awake()
    {
        Bind(minusButton, () => Adjust(-10));
        Bind(plusButton, () => Adjust(10));
        Bind(fireOneButton, FireOne);
        Bind(fireManyButton, FireMany);
        Bind(clearButton, Clear);
    }

    void OnDisable()
    {
        firing = false;
    }

    public override void ResetBench()
    {
        pZero = 50;
        Clear();
    }

    public void Clear()
    {
        StopAllCoroutines();
        firing = false;
        zeros = ones = 0;
        lastText.text = "Fire a shot.";
        Refresh();
    }

    public void Adjust(int delta)
    {
        pZero = Mathf.Clamp(pZero + delta, 0, 100);
        Clear();
    }

    public void FireOne()
    {
        if (firing) return;
        Shot();
        Refresh();
    }

    public void FireMany()
    {
        if (!firing) StartCoroutine(Burst());
    }

    IEnumerator Burst()
    {
        firing = true;
        for (int i = 0; i < burstShots; i++)
        {
            Shot();
            Refresh();
            yield return new WaitForSecondsRealtime(burstSeconds / burstShots);
        }
        firing = false;
    }

    void Shot()
    {
        bool zero = Random.value * 100f < pZero;
        if (zero) zeros++; else ones++;
        lastText.text = $"LAST SHOT  <color={Hex(bright)}>{(zero ? 0 : 1)}</color>";
    }

    void Refresh()
    {
        float a = Mathf.Sqrt(pZero / 100f), b = Mathf.Sqrt(1f - pZero / 100f);
        stateText.text = $"|ψ⟩ = {a:0.00}|0⟩ + {b:0.00}|1⟩\nP(0) = |α|² = {pZero}%     P(1) = |β|² = {100 - pZero}%";

        int total = zeros + ones;
        int peak = Mathf.Max(1, Mathf.Max(zeros, ones));
        barZero.sizeDelta = new Vector2(barZero.sizeDelta.x, Mathf.Max(1f, barMax * zeros / peak));
        barOne.sizeDelta = new Vector2(barOne.sizeDelta.x, Mathf.Max(1f, barMax * ones / peak));

        string share = total == 0 ? "" : $"  ({Mathf.RoundToInt(100f * zeros / total)}% 0s)";
        countsText.text = $"SHOTS {total}{share}\n0: {zeros}    1: {ones}";
    }

    public override void HandleKeys(Keyboard kb)
    {
        if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) Adjust(-10);
        else if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) Adjust(10);
        else if (kb.digit1Key.wasPressedThisFrame) FireOne();
        else if (kb.digit2Key.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) FireMany();
        else if (kb.rKey.wasPressedThisFrame) Clear();
    }
}
