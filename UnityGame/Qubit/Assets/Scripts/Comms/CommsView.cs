using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Radio transcript for the COMMS LINK. Incoming lines are "received" then typed out;
/// the player's own lines wait for them to press Enter to transmit.
/// </summary>
public class CommsView : MonoBehaviour
{
    [SerializeField] TMP_Text transcript;
    [SerializeField] ScrollRect scroll;
    [SerializeField] TMP_Text signalText;
    [SerializeField] TMP_Text promptText;

    [Header("Timing")]
    [SerializeField] float charactersPerSecond = 45f;
    [SerializeField] float systemSpeedMultiplier = 2f;
    [SerializeField] float receiveTime = 0.9f;
    [SerializeField] float transmitTime = 0.4f;

    [Header("Colours")]
    [SerializeField] string specialistColour = "#3fe0b5";
    [SerializeField] string youColour = "#ffe2a8";
    [SerializeField] string systemColour = "#a8701f";
    [SerializeField] string textColour = "#ffb23f";
    [SerializeField] string faint = "#5a3c12";

    [Tooltip("Raised when the player presses Esc to leave the channel.")]
    public UnityEvent onClose = new UnityEvent();

    /// <summary>Supplies the station clock for message timestamps.</summary>
    public Func<string> clock;

    enum State { Idle, Receiving, Typing, Waiting, Finished }

    readonly StringBuilder log = new StringBuilder();
    DialogueScript script;
    bool viewingLog;
    State state;
    bool advance, skip;
    int openedFrame;
    string status = "";
    float signalTimer;
    int signalBars = 4;

    public void Play(DialogueScript dialogue, Action finished)
    {
        script = dialogue;
        log.Clear();
        viewingLog = false;
        Open();
        StopAllCoroutines();
        StartCoroutine(Run(finished));
    }

    /// <summary>Shows a finished conversation again, for re-reading.</summary>
    public void ShowLog(DialogueScript dialogue)
    {
        script = dialogue;
        log.Clear();
        foreach (var line in dialogue.lines) log.Append(Header(line.speaker, "")).Append(Body(line, line.text)).Append("\n\n");
        Open();
        StopAllCoroutines();
        state = State.Finished;
        viewingLog = true;
        status = "";
        promptText.text = "CHANNEL IDLE.  NO NEW MESSAGES.    [ENTER] CLOSE";
        Render("");
    }

    void Open()
    {
        gameObject.SetActive(true);
        openedFrame = Time.frameCount;
        advance = skip = false;
        transcript.text = "";
    }

    IEnumerator Run(Action finished)
    {
        var lines = script.lines;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (line.speaker == DialogueScript.Speaker.You)
            {
                yield return WaitForEnter("[ENTER] TRANSMIT REPLY");
                yield return Signal("TRANSMITTING", transmitTime);
            }
            else if (line.speaker == DialogueScript.Speaker.MissionSpecialist)
            {
                yield return Signal("RECEIVING", receiveTime);
            }

            string header = Header(line.speaker, clock != null ? clock() : "");
            float speed = charactersPerSecond * GameSettings.TextSpeedMultiplier;
            if (line.speaker == DialogueScript.Speaker.System) speed *= systemSpeedMultiplier;

            state = State.Typing;
            promptText.text = $"<color={faint}>[ENTER] SKIP</color>";
            for (int shown = 0; shown <= line.text.Length; shown++)
            {
                if (skip) { shown = line.text.Length; skip = false; }
                Render(header + Body(line, line.text.Substring(0, shown)) + "<color=" + textColour + ">_</color>");
                if (shown < line.text.Length) yield return new WaitForSecondsRealtime(1f / speed);
            }
            log.Append(header).Append(Body(line, line.text)).Append("\n\n");
            Render("");

            bool nextIsReply = i + 1 < lines.Count && lines[i + 1].speaker == DialogueScript.Speaker.You;
            if (i + 1 < lines.Count && !nextIsReply) yield return WaitForEnter("[ENTER] CONTINUE");
        }

        state = State.Finished;
        yield return WaitForEnter("[ENTER] CLOSE CHANNEL");
        finished?.Invoke();
    }

    IEnumerator WaitForEnter(string prompt)
    {
        state = State.Waiting;
        promptText.text = prompt;
        advance = false;
        while (!advance) yield return null;
        advance = false;
        promptText.text = "";
    }

    IEnumerator Signal(string label, float seconds)
    {
        state = State.Receiving;
        promptText.text = "";
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            status = label + new string('.', 1 + (int)(t * 6f) % 3);
            yield return null;
        }
        status = "";
    }

    string Header(DialogueScript.Speaker speaker, string time)
    {
        string name = speaker == DialogueScript.Speaker.MissionSpecialist ? "MISSION SPECIALIST"
                    : speaker == DialogueScript.Speaker.You ? "YOU" : "SYSTEM";
        string colour = speaker == DialogueScript.Speaker.MissionSpecialist ? specialistColour
                      : speaker == DialogueScript.Speaker.You ? youColour : systemColour;
        string stamp = string.IsNullOrEmpty(time) ? "" : $"   <color={faint}>{time}</color>";
        return $"<color={colour}><b>{name}</b></color>{stamp}\n";
    }

    string Body(DialogueScript.Line line, string text)
    {
        string colour = line.speaker == DialogueScript.Speaker.System ? systemColour
                      : line.speaker == DialogueScript.Speaker.You ? youColour : textColour;
        return $"<color={colour}>{text}</color>";
    }

    void Render(string live)
    {
        transcript.text = log + live;
        // Keep the newest line in view.
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.verticalNormalizedPosition = 0f;
    }

    void Update()
    {
        // Flickering signal meter, or the receive/transmit status.
        signalTimer -= Time.unscaledDeltaTime;
        if (signalTimer <= 0f)
        {
            signalTimer = 0.45f;
            signalBars = Mathf.Clamp(signalBars + UnityEngine.Random.Range(-1, 2), 3, 5);
        }
        string bars = new string('|', signalBars) + new string('.', 5 - signalBars);
        signalText.text = string.IsNullOrEmpty(status)
            ? $"{(script != null ? script.channel : "")}   SIGNAL [{bars}]"
            : $"<color={specialistColour}>{status}</color>";

        var kb = Keyboard.current;
        if (kb == null || Time.frameCount == openedFrame) return;
        if (kb.escapeKey.wasPressedThisFrame) { onClose.Invoke(); return; }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Advance();
    }

    /// <summary>Same as pressing Enter: finish typing, continue, transmit or close.</summary>
    public void Advance()
    {
        if (viewingLog) onClose.Invoke();
        else if (state == State.Typing) skip = true;
        else if (state == State.Waiting || state == State.Finished) advance = true;
    }
}
