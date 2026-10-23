using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>Keyboard and mouse navigation for a list of TerminalMenuItems on one panel.</summary>
public class TerminalMenu : MonoBehaviour
{
    [SerializeField] List<TerminalMenuItem> items = new List<TerminalMenuItem>();
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] AudioSource blip;
    [Tooltip("Start at the first item each time the menu opens. Turn off to keep the last selection.")]
    [SerializeField] bool resetOnEnable = true;

    public UnityEvent onBack;

    int index;
    int enabledFrame;

    void Awake()
    {
        // Items switched off for this platform (see HideOnWebGL) drop out of navigation.
        items.RemoveAll(item => item == null || !item.gameObject.activeSelf);
        foreach (var item in items) item.Bind(this);
    }

    void OnEnable()
    {
        // The key press that opened this panel must not also trigger it.
        enabledFrame = Time.frameCount;
        if (resetOnEnable) index = 0;
        Refresh();
    }

    public void Select(TerminalMenuItem item)
    {
        int i = items.IndexOf(item);
        if (i < 0 || i == index) return;
        index = i;
        Refresh();
        Sfx.Play("move");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || items.Count == 0 || Time.frameCount == enabledFrame) return;

        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { Sfx.Play("move"); items[index].onLeft.Invoke(); }
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { Sfx.Play("move"); items[index].onRight.Invoke(); }
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            var item = items[index];
            Sfx.Play("select");
            if (item.IsOption) item.onRight.Invoke(); else item.onSubmit.Invoke();
        }
        else if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) { Sfx.Play("back"); onBack.Invoke(); }
    }

    void Move(int delta)
    {
        index = (index + delta + items.Count) % items.Count;
        Refresh();
        Sfx.Play("move");
    }

    public void Refresh()
    {
        for (int i = 0; i < items.Count; i++) items[i].SetSelected(i == index);
        if (descriptionText != null && items.Count > 0) descriptionText.text = items[index].Description;
    }
}
