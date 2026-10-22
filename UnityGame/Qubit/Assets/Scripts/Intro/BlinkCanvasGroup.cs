using UnityEngine;

/// <summary>Blinks a CanvasGroup on and off, like a terminal prompt.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class BlinkCanvasGroup : MonoBehaviour
{
    [SerializeField] float onSeconds = 0.6f;
    [SerializeField] float offSeconds = 0.4f;

    CanvasGroup group;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        float period = onSeconds + offSeconds;
        group.alpha = Time.unscaledTime % period < onSeconds ? 1f : 0f;
    }
}
