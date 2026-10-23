using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One line in a terminal menu. Shows a "> " cursor and an inverted bar when selected.
/// Option rows also have a value label that Left/Right change.
/// </summary>
public class TerminalMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] string label = "MENU ITEM";
    [SerializeField, TextArea] string description;
    [SerializeField] TMP_Text labelText;
    [SerializeField] TMP_Text valueText;
    [SerializeField] Image highlight;
    [SerializeField] Color normalColor = new Color(0.635f, 0.722f, 0.659f);
    [SerializeField] Color selectedTextColor = new Color(0.063f, 0.106f, 0.114f);
    [SerializeField, Range(50, 100)] int badgeScale = 75;

    public UnityEvent onSubmit;
    public UnityEvent onLeft;
    public UnityEvent onRight;

    TerminalMenu menu;
    string badge;
    bool selected;

    public bool IsOption => valueText != null;
    public string Description => description;

    public void SetDescription(string text) => description = text;

    /// <summary>Short status tag shown after the label, e.g. "INCOMING".</summary>
    public void SetBadge(string text)
    {
        badge = text;
        SetSelected(selected);
    }

    public void Bind(TerminalMenu owner) => menu = owner;

    public void SetValue(string value)
    {
        if (valueText != null) valueText.text = value;
    }

    public void SetSelected(bool selected)
    {
        this.selected = selected;
        // On the inverted bar a coloured badge loses contrast, so it takes the label's colour.
        string shownBadge = selected ? StripColor(badge) : badge;
        // Badges are set smaller so the longest ("[VERIFIED]") still fits the module column.
        labelText.text = (selected ? "> " : "  ") + label + (string.IsNullOrEmpty(shownBadge) ? "" : " <size=" + badgeScale + "%>" + shownBadge + "</size>");
        if (highlight != null) highlight.enabled = selected;
        Color c = selected ? selectedTextColor : normalColor;
        labelText.color = c;
        if (valueText != null) valueText.color = c;
    }

    static string StripColor(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = text.Replace("</color>", "");
        int start;
        while ((start = text.IndexOf("<color=")) >= 0)
        {
            int end = text.IndexOf('>', start);
            if (end < 0) break;
            text = text.Remove(start, end - start + 1);
        }
        return text;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (menu != null) menu.Select(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (menu == null) return;
        menu.Select(this);
        if (IsOption) onRight.Invoke(); else onSubmit.Invoke();
    }
}
