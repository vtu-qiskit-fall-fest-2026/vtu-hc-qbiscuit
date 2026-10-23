using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Request sent to the Qiskit backend. Field names match the backend's JSON keys.</summary>
[Serializable]
public class QuantumRequest
{
    public int version = 1;            // backend API version
    public string action;              // "diagnose" or "correct"
    public int error_qubit = -1;       // 0, 1, 2, or -1 for no error
    public int correction_qubit = -1;  // 0, 1, 2, or -1 for no correction
}

/// <summary>Reply from the Qiskit backend. Field names match the backend's JSON keys.</summary>
[Serializable]
public class QuantumResponse
{
    public bool success;
    public string action;
    public string syndrome;
    public string final_data;
    public int correction_qubit;
    public bool verified;
    public string message;
    public string error;    // backend's reason when success is false
    public string detail;   // FastAPI's reason on an HTTP error
    public int shots;

    [NonSerialized] public string source;
}

/// <summary>Cryogenic noise experiment: prepare |1⟩, hold it, measure.</summary>
[Serializable]
public class NoisyRequest
{
    public int version = 1;
    public string action = "noisy";
    public string qubit = "C";
    public float temperature_mk = 80f;
    public float hold_us = 20f;
    public int shots = 200;
    public bool encoded;
}

[Serializable]
public class NoisyResponse
{
    public bool success;
    public float survival;
    public int shots;
    public float t1_us;
    public string error;
    public string detail;

    [NonSerialized] public string source;
    public int Kept => Mathf.RoundToInt(survival * shots);
}

/// <summary>
/// Sends circuit requests to the Qiskit Aer backend over HTTP (e.g. hosted on Render).
/// With no server URL it can fall back to a clearly labelled offline stand-in, so the
/// game is playable during development. The stand-in is never presented as Qiskit.
/// </summary>
public class QuantumBackend : MonoBehaviour
{
    [Tooltip("POST endpoint used in the editor, e.g. a local server at http://127.0.0.1:8000/run. Leave empty to use the offline stand-in.")]
    [SerializeField] string serverUrl = "";
    [Tooltip("POST endpoint used by builds (WebGL on itch.io, desktop). Must be HTTPS for itch.io, e.g. the Render service URL + /run.")]
    [SerializeField] string buildServerUrl = "https://andromeda-quantum-backend.onrender.com/run";
    [Tooltip("A sleeping free-tier server can take up to a minute to wake.")]
    [SerializeField] int timeoutSeconds = 75;
    [SerializeField] bool allowOfflineStandIn = true;
    [Tooltip("If the hosted server cannot be reached (asleep past the timeout, or down), answer with the labelled stand-in so the game stays playable.")]
    [SerializeField] bool fallBackWhenUnreachable = true;
    [SerializeField] float standInDelay = 0.9f;

    public const string StandInLabel = "OFFLINE STAND-IN (NOT QISKIT)";

    public bool UsesServer => !string.IsNullOrWhiteSpace(serverUrl);
    public bool IsStandIn => !UsesServer && allowOfflineStandIn;

    public string Label => UsesServer ? "QISKIT AER (REMOTE)" : allowOfflineStandIn ? StandInLabel : "NO BACKEND CONFIGURED";

    // ---- Live connection status ----

    public enum Status { StandIn, Connecting, Online, Unreachable }

    [Header("Health check")]
    [Tooltip("Seconds between /health checks once the server has answered.")]
    [SerializeField] float healthInterval = 15f;
    [Tooltip("Seconds between retries while the server is unreachable.")]
    [SerializeField] float retryInterval = 8f;

    /// <summary>Raised with (previous, current) whenever the connection status changes.</summary>
    public event Action<Status, Status> StatusChanged;

    public Status CurrentStatus { get; private set; } = Status.Connecting;

    /// <summary>Short status text, e.g. "ONLINE", for labels such as "QISKIT AER: ONLINE".</summary>
    public string StatusText => CurrentStatus switch
    {
        Status.StandIn => "OFFLINE STAND-IN",
        Status.Online => "ONLINE",
        Status.Unreachable => "UNREACHABLE",
        _ => "CONNECTING..."
    };

    string HealthUrl
    {
        get
        {
            string url = serverUrl.Trim().TrimEnd('/');
            if (url.EndsWith("/run")) url = url.Substring(0, url.Length - 4);
            return url + "/health";
        }
    }

    void Awake()
    {
        // Builds talk to the hosted backend; the editor keeps its own (usually local) URL.
        if (!Application.isEditor) serverUrl = buildServerUrl;
    }

    void Start()
    {
        // Checking at launch also wakes a sleeping free-tier server before the player needs it.
        if (UsesServer) StartCoroutine(HealthLoop());
        else SetStatus(allowOfflineStandIn ? Status.StandIn : Status.Unreachable);
    }

    IEnumerator HealthLoop()
    {
        SetStatus(Status.Connecting);
        while (true)
        {
            // Allow a long first wait for a cold start, shorter ones afterwards.
            int timeout = CurrentStatus == Status.Online ? 10 : timeoutSeconds;
            using (var www = UnityWebRequest.Get(HealthUrl))
            {
                www.timeout = timeout;
                float started = Time.realtimeSinceStartup;
                yield return www.SendWebRequest();
                bool ok = www.result == UnityWebRequest.Result.Success || www.responseCode == 200;
                Debug.Log($"[QuantumBackend] health {(ok ? "OK" : "FAILED")}: {www.result} code={www.responseCode} error={www.error} after {Time.realtimeSinceStartup - started:0.0}s url={HealthUrl}");
                SetStatus(ok ? Status.Online : Status.Unreachable);
            }
            yield return new WaitForSecondsRealtime(CurrentStatus == Status.Online ? healthInterval : retryInterval);
        }
    }

    void SetStatus(Status next)
    {
        if (next == CurrentStatus) return;
        var previous = CurrentStatus;
        CurrentStatus = next;
        Debug.Log($"[QuantumBackend] status {previous} -> {next}");
        StatusChanged?.Invoke(previous, next);
    }

    public IEnumerator Send(QuantumRequest request, Action<QuantumResponse> done)
    {
        if (!UsesServer)
        {
            if (!allowOfflineStandIn) { done(Fail(request, "No backend URL is configured.")); yield break; }
            yield return new WaitForSecondsRealtime(standInDelay);
            done(OfflineStandIn.Run(request));
            yield break;
        }

        // The backend has one puzzle action, "correct", which measures the syndrome and verifies.
        // A diagnosis is the same circuit with no correction; only its syndrome is used.
        var wire = new QuantumRequest
        {
            version = request.version,
            action = "correct",
            error_qubit = request.error_qubit,
            correction_qubit = request.action == "diagnose" ? -1 : request.correction_qubit
        };

        using (var www = new UnityWebRequest(serverUrl, UnityWebRequest.kHttpVerbPOST))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(wire)));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = timeoutSeconds;
            yield return www.SendWebRequest();

            string body = www.downloadHandler != null ? www.downloadHandler.text : "";
            QuantumResponse response = null;
            if (!string.IsNullOrEmpty(body))
            {
                try { response = JsonUtility.FromJson<QuantumResponse>(body); }
                catch (Exception) { response = null; }
            }

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.DataProcessingError)
            {
                SetStatus(Status.Unreachable);
                if (allowOfflineStandIn && fallBackWhenUnreachable) done(OfflineStandIn.Run(request));
                else done(Fail(request, $"Could not reach the Qiskit backend ({www.error})."));
                yield break;
            }
            SetStatus(Status.Online);
            if (www.result == UnityWebRequest.Result.ProtocolError || response == null || !response.success)
            {
                string reason = response != null ? (response.error ?? response.detail ?? response.message) : null;
                if (string.IsNullOrEmpty(reason)) reason = www.error ?? "The backend sent an unreadable reply.";
                done(Fail(request, "Qiskit backend error: " + reason));
                yield break;
            }

            response.action = request.action;
            response.source = "QISKIT AER";
            done(response);
        }
    }

    /// <summary>Runs the cryogenic noise experiment on the backend (or the labelled stand-in).</summary>
    public IEnumerator SendNoisy(NoisyRequest request, Action<NoisyResponse> done)
    {
        if (!UsesServer)
        {
            if (!allowOfflineStandIn) { done(new NoisyResponse { success = false, error = "No backend URL is configured." }); yield break; }
            yield return new WaitForSecondsRealtime(standInDelay);
            done(OfflineStandIn.RunNoisy(request));
            yield break;
        }

        using (var www = new UnityWebRequest(serverUrl, UnityWebRequest.kHttpVerbPOST))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = timeoutSeconds;
            yield return www.SendWebRequest();

            string body = www.downloadHandler != null ? www.downloadHandler.text : "";
            NoisyResponse response = null;
            if (!string.IsNullOrEmpty(body))
            {
                try { response = JsonUtility.FromJson<NoisyResponse>(body); }
                catch (Exception) { response = null; }
            }
            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.DataProcessingError)
            {
                SetStatus(Status.Unreachable);
                if (allowOfflineStandIn && fallBackWhenUnreachable) done(OfflineStandIn.RunNoisy(request));
                else done(new NoisyResponse { success = false, error = $"Could not reach the Qiskit backend ({www.error})." });
                yield break;
            }
            SetStatus(Status.Online);
            if (response == null || !response.success)
            {
                string reason = response != null ? (response.error ?? response.detail) : null;
                done(new NoisyResponse { success = false, error = "Qiskit backend error: " + (reason ?? www.error ?? "unreadable reply") });
                yield break;
            }
            response.source = "QISKIT AER";
            done(response);
        }
    }

    static QuantumResponse Fail(QuantumRequest request, string message) => new QuantumResponse
    {
        success = false, action = request.action, syndrome = "", final_data = "",
        correction_qubit = -1, verified = false, message = message, source = "ERROR"
    };
}

/// <summary>
/// Development stand-in: runs the same five-qubit circuit gate by gate on basis states
/// (exact for X and CNOT starting from |00000⟩). It is not Qiskit and is labelled as such.
/// </summary>
public static class OfflineStandIn
{
    // Same simplified cryogenic model as Andromeda_Backend/quantum_backend/noise.py.
    static float T1(string qubit, float temperatureMk)
    {
        float t1Ref = qubit == "A" ? 990f : qubit == "B" ? 657f : 58f;
        return t1Ref * Mathf.Pow(80f / temperatureMk, 1.6f);
    }

    public static NoisyResponse RunNoisy(NoisyRequest r)
    {
        float t1 = T1(r.qubit, Mathf.Clamp(r.temperature_mk, 10f, 80f));
        float p = Mathf.Exp(-r.hold_us / t1); // chance one qubit is still |1⟩
        int kept = 0;
        for (int s = 0; s < r.shots; s++)
        {
            int ones = 0, width = r.encoded ? 3 : 1;
            for (int q = 0; q < width; q++) if (UnityEngine.Random.value < p) ones++;
            if (r.encoded ? ones >= 2 : ones == 1) kept++;
        }
        return new NoisyResponse
        {
            success = true, survival = kept / (float)r.shots, shots = r.shots, t1_us = t1,
            source = QuantumBackend.StandInLabel
        };
    }

    public static QuantumResponse Run(QuantumRequest request)
    {
        var d = new bool[3];
        if (request.error_qubit >= 0 && request.error_qubit < 3) d[request.error_qubit] = !d[request.error_qubit];

        bool a = d[0] ^ d[1];   // cx(d0, a0); cx(d1, a0)
        bool b = d[1] ^ d[2];   // cx(d1, a1); cx(d2, a1)
        string syndrome = $"{(a ? 1 : 0)}{(b ? 1 : 0)}";

        bool correct = request.action == "correct";
        if (correct && request.correction_qubit >= 0 && request.correction_qubit < 3)
            d[request.correction_qubit] = !d[request.correction_qubit];
        string data = $"{(d[0] ? 1 : 0)}{(d[1] ? 1 : 0)}{(d[2] ? 1 : 0)}";
        bool verified = correct && data == "000";

        return new QuantumResponse
        {
            success = true,
            action = request.action,
            syndrome = syndrome,
            final_data = correct ? data : "",
            correction_qubit = correct ? request.correction_qubit : -1,
            verified = verified,
            message = "Offline stand-in result, not Qiskit.",
            shots = 1,
            source = QuantumBackend.StandInLabel
        };
    }
}
