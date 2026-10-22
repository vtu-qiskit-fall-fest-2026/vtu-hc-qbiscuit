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
            bool newLine = text.textInfo.characterInfo[i].character == '\n';
            yield return new WaitForSecondsRealtime(newLine ? lineBreakPause : delay);
        }
        IsTyping = false;
    }
}
