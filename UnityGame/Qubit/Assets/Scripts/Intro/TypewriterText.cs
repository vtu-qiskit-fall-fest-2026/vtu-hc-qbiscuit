using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>Reveals a TextMeshPro text one character at a time.</summary>
[RequireComponent(typeof(TMP_Text))]
public class TypewriterText : MonoBehaviour
{
    [SerializeField] float charactersPerSecond = 40f;
    [SerializeField] float lineBreakPause = 0.25f;
    [SerializeField] bool playOnEnable = true;
    [Tooltip("Play a soft key click every few characters.")]
    [SerializeField] bool keyClicks = true;
    [SerializeField] int clickEvery = 3;

    TMP_Text text;

    public bool IsTyping { get; private set; }

    void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    void OnDisable()
    {
        // If the panel is hidden mid-line, show the whole text rather than freezing on a partial line.
        if (IsTyping) MarkDone();
    }

    public void Play()
    {
        StopAllCoroutines();
        StartCoroutine(TypeRoutine());
    }

    /// <summary>Shows all text at once and ends typing.</summary>
    public void MarkDone()
    {
        StopAllCoroutines();
        text.maxVisibleCharacters = int.MaxValue;
        IsTyping = false;
    }

    IEnumerator TypeRoutine()
    {
        IsTyping = true;
        text.ForceMeshUpdate();
        int total = text.textInfo.characterCount;
        text.maxVisibleCharacters = 0;

        float delay = 1f / (charactersPerSecond * GameSettings.TextSpeedMultiplier);
        for (int i = 0; i < total; i++)
        {
            text.maxVisibleCharacters = i + 1;
            char ch = text.textInfo.characterInfo[i].character;
            bool newLine = ch == '\n';
            if (keyClicks && !char.IsWhiteSpace(ch) && i % clickEvery == 0) Sfx.Play("key", 0.6f);
            yield return new WaitForSecondsRealtime(newLine ? lineBreakPause : delay);
        }
        IsTyping = false;
    }
}
