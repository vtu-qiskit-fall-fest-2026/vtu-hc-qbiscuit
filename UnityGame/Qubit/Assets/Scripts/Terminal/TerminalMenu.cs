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

    public UnityEvent onBack;

    int index;
    int enabledFrame;

    void Awake()
    {
        foreach (var item in items) item.Bind(this);
    }

    void OnEnable()
    {
        // The key press that opened this panel must not also trigger it.
        enabledFrame = Time.frameCount;
        index = 0;
        Refresh();
    }

    public void Select(TerminalMenuItem item)
    {
        int i = items.IndexOf(item);
        if (i < 0 || i == index) return;
        index = i;
        Refresh();
        if (blip != null) blip.Play();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || items.Count == 0 || Time.frameCount == enabledFrame) return;

        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) items[index].onLeft.Invoke();
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) items[index].onRight.Invoke();
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            var item = items[index];
            if (item.IsOption) item.onRight.Invoke(); else item.onSubmit.Invoke();
        }
        else if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) onBack.Invoke();
    }

    void Move(int delta)
    {
        index = (index + delta + items.Count) % items.Count;
        Refresh();
        if (blip != null) blip.Play();
    }

    void Refresh()
    {
        for (int i = 0; i < items.Count; i++) items[i].SetSelected(i == index);
        if (descriptionText != null && items.Count > 0) descriptionText.text = items[index].Description;
    }
}
