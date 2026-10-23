using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Pops a short notice when the Qiskit backend comes online or drops out.</summary>
public class BackendToast : MonoBehaviour
{
    [SerializeField] QuantumBackend backend;
    [SerializeField] CanvasGroup group;
    [SerializeField] Image frame;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text body;
    [SerializeField] float showSeconds = 4f;
    [SerializeField] Color online = new Color32(0x8f, 0xbc, 0xa0, 0xff);
    [SerializeField] Color offline = new Color32(0xc8, 0x79, 0x63, 0xff);

    bool wasOnline;

    void Awake()
    {
        group.alpha = 0f;
        backend.StatusChanged += OnChanged;
    }

    void OnDestroy()
    {
        if (backend != null) backend.StatusChanged -= OnChanged;
    }

    void OnChanged(QuantumBackend.Status previous, QuantumBackend.Status current)
    {
        if (current == QuantumBackend.Status.Online && previous != QuantumBackend.Status.Online)
        {
            Show(online, "QISKIT AER ONLINE", wasOnline ? "Connection to the backend restored." : "Backend connected. Circuits will run on Qiskit Aer.");
            wasOnline = true;
        }
        else if (current == QuantumBackend.Status.Unreachable && previous == QuantumBackend.Status.Online)
        {
            Show(offline, "QISKIT AER UNREACHABLE", "Lost contact with the backend. Retrying...");
        }
    }

    void Show(Color colour, string heading, string message)
    {
        frame.color = colour;
        title.color = colour;
        title.text = heading;
        body.text = message;
        Sfx.Play(colour == online ? "toast" : "failed");
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // Stepped fade in and out, like the console panels switching on.
        for (int i = 1; i <= 4; i++) { group.alpha = i / 4f; yield return new WaitForSecondsRealtime(0.05f); }
        yield return new WaitForSecondsRealtime(showSeconds);
        for (int i = 3; i >= 0; i--) { group.alpha = i / 4f; yield return new WaitForSecondsRealtime(0.08f); }
    }
}
