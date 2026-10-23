using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The terminal's startup before the boot log: tube warm-up, the manufacturer's
/// power-on self test (POST), then the operating system splash with a loading bar.
/// </summary>
public class BootSequence : MonoBehaviour
{
    [Header("Warm-up")]
    [SerializeField] GameObject warmupPanel;
    [SerializeField] Image warmupDot;
    [SerializeField] float warmupTime = 1.6f;

    [Header("Power-on self test")]
    [SerializeField] GameObject postPanel;
    [SerializeField] TMP_Text postText;
    [SerializeField] TMP_Text memoryText;
    [SerializeField, TextArea(8, 20)] string[] postLines;
    [SerializeField] float postLineDelay = 0.32f;
    [SerializeField] int memoryKilobytes = 65536;
    [SerializeField] float memoryCountTime = 1.4f;
    [SerializeField] float postHoldTime = 1.0f;

    [Header("Operating system splash")]
    [SerializeField] GameObject splashPanel;
    [SerializeField] List<Image> loadingSegments = new List<Image>();
    [SerializeField] TMP_Text splashStatus;
    [SerializeField] string[] splashMessages;
    [SerializeField] float splashTime = 4.5f;
    [SerializeField] Color segmentOff = new Color(0.816f, 0.643f, 0.427f, 0.12f);
    [SerializeField] Color segmentOn = new Color(0.816f, 0.643f, 0.427f, 1f);

    public IEnumerator Run()
    {
        yield return Warmup();
        yield return Post();
        yield return Splash();
        warmupPanel.SetActive(false);
        postPanel.SetActive(false);
        splashPanel.SetActive(false);
    }

    IEnumerator Warmup()
    {
        Show(warmupPanel);
        var rt = warmupDot.rectTransform;
        // A faint dot of light swells in the middle of the tube, flares, then settles to black.
        for (float t = 0f; t < warmupTime; t += Time.unscaledDeltaTime)
        {
            float k = t / warmupTime;
            float size = Mathf.Lerp(2f, 14f, Mathf.Sin(k * Mathf.PI));
            rt.sizeDelta = new Vector2(size, size);
            float flicker = Random.value < 0.08f ? 0.4f : 1f;
            warmupDot.color = new Color(1f, 0.85f, 0.6f, Mathf.Sin(k * Mathf.PI) * flicker);
            yield return null;
        }
        warmupDot.color = Color.clear;
        yield return new WaitForSecondsRealtime(0.35f);
    }

    IEnumerator Post()
    {
        Show(postPanel);
        var sb = new StringBuilder();
        postText.text = "";
        memoryText.text = "";

        // Memory test counts up first, like an old BIOS.
        for (float t = 0f; t < memoryCountTime; t += Time.unscaledDeltaTime)
        {
            int kb = Mathf.RoundToInt(Mathf.Lerp(0, memoryKilobytes, t / memoryCountTime) / 64f) * 64;
            memoryText.text = "MEMORY TEST : " + kb + "K";
            yield return null;
        }
        memoryText.text = "MEMORY TEST : " + memoryKilobytes + "K OK";
        Sfx.Play("post_beep");
        yield return new WaitForSecondsRealtime(0.3f);

        foreach (var line in postLines)
        {
            sb.AppendLine(line);
            postText.text = sb.ToString();
            if (line.Contains("DRIVES")) Sfx.Play("disk");
            yield return new WaitForSecondsRealtime(string.IsNullOrWhiteSpace(line) ? postLineDelay * 0.5f : postLineDelay);
        }
        yield return new WaitForSecondsRealtime(postHoldTime);

        // Brief blank between the BIOS and the OS, as the video mode switches.
        postPanel.SetActive(false);
        yield return new WaitForSecondsRealtime(0.5f);
    }

    IEnumerator Splash()
    {
        Show(splashPanel);
        foreach (var s in loadingSegments) s.color = segmentOff;
        Sfx.Play("disk");

        int lit = 0;
        for (float t = 0f; t < splashTime; t += Time.unscaledDeltaTime)
        {
            float k = t / splashTime;
            // Uneven progress so the bar stalls and jumps like a real load.
            float eased = Mathf.Clamp01(k + 0.06f * Mathf.Sin(k * 23f));
            int target = Mathf.FloorToInt(eased * loadingSegments.Count);
            while (lit < target && lit < loadingSegments.Count) loadingSegments[lit++].color = segmentOn;
            if (splashMessages.Length > 0)
                splashStatus.text = splashMessages[Mathf.Min(splashMessages.Length - 1, Mathf.FloorToInt(k * splashMessages.Length))];
            yield return null;
        }
        foreach (var s in loadingSegments) s.color = segmentOn;
        Sfx.Play("select");
        yield return new WaitForSecondsRealtime(0.6f);
        splashPanel.SetActive(false);
        yield return new WaitForSecondsRealtime(0.3f);
    }

    public void HideAll() => Show(null);

    void Show(GameObject panel)
    {
        warmupPanel.SetActive(panel == warmupPanel);
        postPanel.SetActive(panel == postPanel);
        splashPanel.SetActive(panel == splashPanel);
    }
}
