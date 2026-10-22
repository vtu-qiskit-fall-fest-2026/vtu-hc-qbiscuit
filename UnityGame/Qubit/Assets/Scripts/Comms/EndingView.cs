using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>Closing mission log: types out the entry, then waits for Enter.</summary>
public class EndingView : MonoBehaviour
{
    [SerializeField] TypewriterText logText;
    [SerializeField] TMP_Text closingText;
    [SerializeField] TMP_Text promptText;
    [SerializeField] float closingDelay = 1.2f;

    [Tooltip("Raised when the player presses Enter after the log.")]
    public UnityEvent onFinished = new UnityEvent();

    bool ready;

    public void Show(string log)
    {
        gameObject.SetActive(true);
        ready = false;
        closingText.alpha = 0f;
        promptText.text = "";
        logText.GetComponent<TMP_Text>().text = log;
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        logText.Play();
        yield return null;
        while (logText.IsTyping) yield return null;
        yield return new WaitForSecondsRealtime(closingDelay);
        // The closing line fades up in steps, like a phosphor warming.
        for (int i = 1; i <= 6; i++)
        {
            closingText.alpha = i / 6f;
            yield return new WaitForSecondsRealtime(0.12f);
        }
        yield return new WaitForSecondsRealtime(0.8f);
        promptText.text = "[ENTER] RETURN TO MAIN TERMINAL";
        ready = true;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        if (!enter) return;

        if (!ready)
        {
            // Enter finishes the typing instead of waiting.
            if (logText.IsTyping) logText.MarkDone();
            return;
        }
        gameObject.SetActive(false);
        onFinished.Invoke();
    }
}
