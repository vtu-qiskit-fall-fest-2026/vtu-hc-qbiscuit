using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>A knob you drag (up/right to increase) that turns a needle between two angles.</summary>
public class RotaryDial : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    [SerializeField] RectTransform needle;
    [SerializeField] float minAngle = 135f;   // value 0
    [SerializeField] float maxAngle = -135f;  // value 100
    [SerializeField] float dragSensitivity = 0.6f;
    [Range(0f, 100f)] [SerializeField] float value;
    [Tooltip("The knob stops here even though the scale goes to 100.")]
    [SerializeField] float limit = 85f;

    [System.Serializable] public class ValueEvent : UnityEvent<float> { }
    public ValueEvent onChanged = new ValueEvent();
    public UnityEvent onHitLimit = new UnityEvent();

    public float Value => value;
    public float Limit => limit;

    void Start() => Apply();

    public void Nudge(float delta) => Set(value + delta);

    public void Set(float v)
    {
        float clamped = Mathf.Clamp(v, 0f, limit);
        if (v > limit && value >= limit - 0.01f) onHitLimit.Invoke();
        if (Mathf.Approximately(clamped, value)) return;
        value = clamped;
        Apply();
        onChanged.Invoke(value);
    }

    public void OnPointerDown(PointerEventData e) { }

    public void OnDrag(PointerEventData e) => Set(value + (e.delta.x + e.delta.y) * dragSensitivity);

    void Apply()
    {
        if (needle != null) needle.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(minAngle, maxAngle, value / 100f));
    }
}
