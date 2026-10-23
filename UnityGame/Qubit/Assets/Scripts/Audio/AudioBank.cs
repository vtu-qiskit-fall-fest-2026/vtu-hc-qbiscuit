using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's sound effects and ambient loops, edited in the Inspector.
/// Other scripts play sounds by name through <see cref="Sfx"/>.
/// </summary>
public class AudioBank : MonoBehaviour
{
    [Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("Random pitch variation, so repeated sounds (keys, blips) don't feel mechanical.")]
        [Range(0f, 0.3f)] public float pitchJitter;
    }

    [SerializeField] List<Sound> sounds = new List<Sound>();

    [Header("Ambient loops")]
    [SerializeField] AudioSource windLoop;
    [SerializeField] AudioSource humLoop;
    [SerializeField] float windVolume = 0.35f;
    [SerializeField] float humVolume = 0.12f;
    [SerializeField] float fadeSpeed = 0.6f;

    [SerializeField] int oneShotVoices = 6;

    [Header("Background music (looping, crossfaded)")]
    [SerializeField] AudioClip calmTrack;
    [SerializeField] AudioClip tenseTrack;
    [SerializeField] float musicVolume = 0.22f;
    [SerializeField] float musicFadeSeconds = 2f;

    AudioSource musicA, musicB;
    bool aIsActive = true;

    public static AudioBank Instance { get; private set; }

    readonly Dictionary<string, Sound> lookup = new Dictionary<string, Sound>();
    AudioSource[] voices;
    int nextVoice;
    float windTarget, humTarget;

    void Awake()
    {
        Instance = this;
        foreach (var s in sounds) if (s.clip != null) lookup[s.name] = s;
        voices = new AudioSource[oneShotVoices];
        for (int i = 0; i < voices.Length; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }
        windTarget = windVolume;
        if (windLoop != null) { windLoop.volume = windVolume; windLoop.Play(); }
        if (humLoop != null) { humLoop.volume = 0f; humLoop.Play(); }

        musicA = gameObject.AddComponent<AudioSource>();
        musicB = gameObject.AddComponent<AudioSource>();
        foreach (var m in new[] { musicA, musicB }) { m.loop = true; m.playOnAwake = false; m.volume = 0f; }
        PlayMusic(false);
    }

    /// <summary>Crossfades to the calm station theme, or the tense theme for the cryo puzzle.</summary>
    public void PlayMusic(bool tense)
    {
        var clip = tense ? tenseTrack : calmTrack;
        if (clip == null) return;
        var current = aIsActive ? musicA : musicB;
        if (current.clip == clip && current.isPlaying) return;
        aIsActive = !aIsActive;
        var next = aIsActive ? musicA : musicB;
        next.clip = clip;
        next.volume = 0f;
        next.Play();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        float step = fadeSpeed * Time.unscaledDeltaTime;
        if (windLoop != null) windLoop.volume = Mathf.MoveTowards(windLoop.volume, windTarget, step);
        if (humLoop != null) humLoop.volume = Mathf.MoveTowards(humLoop.volume, humTarget, step);

        if (musicA == null) return;
        float musicStep = musicVolume / Mathf.Max(0.01f, musicFadeSeconds) * Time.unscaledDeltaTime;
        var active = aIsActive ? musicA : musicB;
        var idle = aIsActive ? musicB : musicA;
        active.volume = Mathf.MoveTowards(active.volume, musicVolume, musicStep);
        idle.volume = Mathf.MoveTowards(idle.volume, 0f, musicStep);
        if (idle.isPlaying && idle.volume <= 0f) idle.Stop();
    }

    public void Play(string name, float volumeScale = 1f)
    {
        if (!lookup.TryGetValue(name, out var s)) return;
        var v = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        v.pitch = 1f + UnityEngine.Random.Range(-s.pitchJitter, s.pitchJitter);
        v.PlayOneShot(s.clip, s.volume * volumeScale);
    }

    /// <summary>Room view: wind outside. Terminal: equipment hum, wind muffled.</summary>
    public void SetAmbience(bool terminalOn)
    {
        windTarget = terminalOn ? windVolume * 0.35f : windVolume;
        humTarget = terminalOn ? humVolume : 0f;
    }
}

/// <summary>Plays a named sound from the scene's AudioBank, if there is one.</summary>
public static class Sfx
{
    public static void Play(string name, float volumeScale = 1f)
    {
        if (AudioBank.Instance != null) AudioBank.Instance.Play(name, volumeScale);
    }

    public static void Ambience(bool terminalOn)
    {
        if (AudioBank.Instance != null) AudioBank.Instance.SetAmbience(terminalOn);
    }

    public static void Music(bool tense)
    {
        if (AudioBank.Instance != null) AudioBank.Instance.PlayMusic(tense);
    }
}
