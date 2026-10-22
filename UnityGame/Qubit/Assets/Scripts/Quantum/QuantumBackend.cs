using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Request sent to the Qiskit backend. Field names match the backend's JSON keys.</summary>
[Serializable]
public class QuantumRequest
{
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
    public int shots;

    [NonSerialized] public string source;
}

/// <summary>
/// Sends circuit requests to the Qiskit Aer backend over HTTP (e.g. hosted on Render).
/// With no server URL it can fall back to a clearly labelled offline stand-in, so the
/// game is playable during development. The stand-in is never presented as Qiskit.
/// </summary>
public class QuantumBackend : MonoBehaviour
{
    [Tooltip("POST endpoint of the Qiskit backend, e.g. https://andromeda-backend.onrender.com/run. Leave empty to use the offline stand-in.")]
    [SerializeField] string serverUrl = "";
    [Tooltip("A sleeping free-tier server can take up to a minute to wake.")]
    [SerializeField] int timeoutSeconds = 75;
    [SerializeField] bool allowOfflineStandIn = true;
    [SerializeField] float standInDelay = 0.9f;

    public const string StandInLabel = "OFFLINE STAND-IN (NOT QISKIT)";

    public bool UsesServer => !string.IsNullOrWhiteSpace(serverUrl);
    public bool IsStandIn => !UsesServer && allowOfflineStandIn;

    public string Label => UsesServer ? "QISKIT AER (REMOTE)" : allowOfflineStandIn ? StandInLabel : "NO BACKEND CONFIGURED";

    public IEnumerator Send(QuantumRequest request, Action<QuantumResponse> done)
    {
        if (!UsesServer)
        {
            if (!allowOfflineStandIn) { done(Fail(request, "No backend URL is configured.")); yield break; }
            yield return new WaitForSecondsRealtime(standInDelay);
            done(OfflineStandIn.Run(request));
            yield break;
        }

        using (var www = new UnityWebRequest(serverUrl, UnityWebRequest.kHttpVerbPOST))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = timeoutSeconds;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                done(Fail(request, $"Could not reach the Qiskit backend ({www.error})."));
                yield break;
            }

            QuantumResponse response = null;
            try { response = JsonUtility.FromJson<QuantumResponse>(www.downloadHandler.text); }
            catch (Exception e) { done(Fail(request, "The backend sent an unreadable reply: " + e.Message)); yield break; }

            if (response == null) { done(Fail(request, "The backend sent an empty reply.")); yield break; }
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
