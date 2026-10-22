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
    [SerializeField] Color normalColor = new Color(1f, 0.7f, 0.25f);
    [SerializeField] Color selectedTextColor = new Color(0.05f, 0.03f, 0.01f);

    public UnityEvent onSubmit;
    public UnityEvent onLeft;
    public UnityEvent onRight;

    TerminalMenu menu;

    public bool IsOption => valueText != null;
    public string Description => description;

    public void Bind(TerminalMenu owner) => menu = owner;

    public void SetValue(string value)
    {
        if (valueText != null) valueText.text = value;
    }

    public void SetSelected(bool selected)
    {
        labelText.text = (selected ? "> " : "  ") + label;
        if (highlight != null) highlight.enabled = selected;
        Color c = selected ? selectedTextColor : normalColor;
        labelText.color = c;
        if (valueText != null) valueText.color = c;
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
