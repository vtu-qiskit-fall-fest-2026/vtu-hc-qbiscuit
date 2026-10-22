using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Title flow: the room idles with the CRT off until the player presses a key,
/// then the CRT powers on, the view zooms into the screen and the screen-only UI takes over.
/// </summary>
public class IntroController : MonoBehaviour
{
    [Header("Room view")]
    [SerializeField] RectTransform room;
    [SerializeField] CanvasGroup roomGroup;
    [SerializeField] RectTransform crtScreen;
    [SerializeField] Image crtPowerFlash;

    [Header("Prompt")]
    [SerializeField] CanvasGroup prompt;

    [Header("Screen UI")]
    [SerializeField] CanvasGroup screenUI;
    [SerializeField] GameObject screenContent;

    [Header("Look")]
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] Color screenOnColor = new Color(1f, 0.7f, 0.25f, 1f);

    [Header("Timing (seconds)")]
    [SerializeField] float lineTime = 0.12f;
    [SerializeField] float openTime = 0.22f;
    [SerializeField] float settleTime = 0.2f;
    [SerializeField] float holdTime = 0.25f;
    [SerializeField] float zoomTime = 1.2f;
    [SerializeField] float zoomOvershoot = 1.06f;

    [SerializeField] UnityEvent onScreenReady;

    bool started;

    void Start()
    {
        crtPowerFlash.gameObject.SetActive(false);
        screenUI.alpha = 0f;
        screenUI.interactable = false;
        screenUI.blocksRaycasts = false;
        screenContent.SetActive(false);
    }

    void Update()
    {
        if (started) return;
        bool key = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (key || click) StartCoroutine(PowerOnSequence());
    }

    IEnumerator PowerOnSequence()
    {
        started = true;

        // Hide the prompt.
        var blink = prompt.GetComponent<BlinkCanvasGroup>();
        if (blink != null) blink.enabled = false;
        prompt.alpha = 0f;

        // CRT power-on: a bright horizontal line, then the picture opens vertically.
        var flash = crtPowerFlash.rectTransform;
        crtPowerFlash.gameObject.SetActive(true);
        crtPowerFlash.color = flashColor;
        yield return Animate(lineTime, t => flash.localScale = new Vector3(t, 0.03f, 1f));
        yield return Animate(openTime, t =>
        {
            flash.localScale = new Vector3(1f, Mathf.Lerp(0.03f, 1f, t), 1f);
            crtPowerFlash.color = Color.Lerp(flashColor, screenOnColor, t * 0.5f);
        });
        yield return Animate(settleTime, t => crtPowerFlash.color = Color.Lerp(Color.Lerp(flashColor, screenOnColor, 0.5f), screenOnColor, t));
        yield return new WaitForSecondsRealtime(holdTime);

        // Zoom the whole room so the CRT screen fills the view.
        Vector2 roomSize = room.rect.size;
        Vector2 screenSize = crtScreen.rect.size;
        float scale = Mathf.Max(roomSize.x / screenSize.x, roomSize.y / screenSize.y) * zoomOvershoot;
        Vector2 screenCentre = ScreenCentreInRoom();

        yield return Animate(zoomTime, t =>
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            float s = Mathf.Lerp(1f, scale, e);
            room.localScale = new Vector3(s, s, 1f);
            room.anchoredPosition = -screenCentre * s;
            screenUI.alpha = Mathf.InverseLerp(0.55f, 1f, t);
        });

        // Hand over to the screen-only UI.
        roomGroup.alpha = 0f;
        room.gameObject.SetActive(false);
        screenUI.alpha = 1f;
        screenUI.interactable = true;
        screenUI.blocksRaycasts = true;
        screenContent.SetActive(true);
        onScreenReady.Invoke();
    }

    /// <summary>Centre of the CRT screen, relative to the room's centre, in room units.</summary>
    Vector2 ScreenCentreInRoom()
    {
        Vector3 worldCentre = crtScreen.TransformPoint(crtScreen.rect.center);
        Vector3 local = room.InverseTransformPoint(worldCentre);
        return (Vector2)local - room.rect.center;
    }

    static IEnumerator Animate(float duration, System.Action<float> step)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            step(t / duration);
            yield return null;
        }
        step(1f);
    }
}
