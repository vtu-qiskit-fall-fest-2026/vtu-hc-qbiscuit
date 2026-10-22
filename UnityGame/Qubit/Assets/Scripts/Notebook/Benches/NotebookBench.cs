using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A small hands-on practice panel on the notebook's BENCH tab.
/// Benches illustrate ideas locally; they are never used to verify the mission.
/// </summary>
public abstract class NotebookBench : MonoBehaviour
{
    [SerializeField] NotebookSection.Bench kind;

    [Header("Colours")]
    [SerializeField] protected Color amber = new Color32(0xff, 0xb2, 0x3f, 0xff);
    [SerializeField] protected Color bright = new Color32(0xff, 0xe2, 0xa8, 0xff);
    [SerializeField] protected Color dim = new Color32(0xa8, 0x70, 0x1f, 0xff);
    [SerializeField] protected Color teal = new Color32(0x3f, 0xe0, 0xb5, 0xff);
    [SerializeField] protected Color alert = new Color32(0xff, 0x6a, 0x4a, 0xff);
    [SerializeField] protected Color off = new Color32(0x3a, 0x26, 0x0b, 0xff);

    bool initialised;

    public NotebookSection.Bench Kind => kind;

    public abstract void ResetBench();

    /// <summary>Keyboard shortcuts while this bench is on screen.</summary>
    public virtual void HandleKeys(Keyboard kb) { }

    protected virtual void OnEnable()
    {
        // Keep progress when the player switches tabs; only set up the first time.
        if (initialised) return;
        initialised = true;
        ResetBench();
    }

    protected static void Bind(Button button, UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    protected static void Paint(Button button, Color frame, Color label)
    {
        if (button == null) return;
        if (button.targetGraphic != null) button.targetGraphic.color = frame;
        var text = button.GetComponentInChildren<TMP_Text>();
        if (text != null) text.color = label;
    }

    protected static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    protected static string Ket(bool one) => one ? "|1⟩" : "|0⟩";
}
