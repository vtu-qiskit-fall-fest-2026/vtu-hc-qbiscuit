using TMPro;
using UnityEngine;

/// <summary>Shows the backend's live connection status, e.g. "QISKIT AER: ONLINE".</summary>
[RequireComponent(typeof(TMP_Text))]
public class BackendStatusLabel : MonoBehaviour
{
    [SerializeField] QuantumBackend backend;
    [Tooltip("{0} is replaced with the status, e.g. ONLINE.")]
    [SerializeField] string format = "QISKIT AER: {0}";
    [SerializeField] Color online = new Color32(0x8f, 0xbc, 0xa0, 0xff);
    [SerializeField] Color connecting = new Color32(0x71, 0x87, 0x80, 0xff);
    [SerializeField] Color offline = new Color32(0xc8, 0x79, 0x63, 0xff);

    TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        if (backend == null) return;
        backend.StatusChanged += OnChanged;
        Show();
    }

    void OnDisable()
    {
        if (backend != null) backend.StatusChanged -= OnChanged;
    }

    void OnChanged(QuantumBackend.Status previous, QuantumBackend.Status current) => Show();

    void Update()
    {
        // Animate the dots while connecting so a slow cold start doesn't look frozen.
        if (backend != null && backend.CurrentStatus == QuantumBackend.Status.Connecting) Show();
    }

    void Show()
    {
        var s = backend.CurrentStatus;
        string status = s == QuantumBackend.Status.Connecting
            ? "CONNECTING" + new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3)
            : backend.StatusText;
        text.text = string.Format(format, status);
        text.color = s == QuantumBackend.Status.Online ? online
                   : s == QuantumBackend.Status.Connecting ? connecting : offline;
    }
}
