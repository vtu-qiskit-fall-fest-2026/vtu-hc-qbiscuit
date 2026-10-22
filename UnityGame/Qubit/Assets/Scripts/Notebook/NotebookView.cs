using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Research Notebook reader on the operations console. Each entry has four tabs:
/// READ (the notebook text), BENCH (a hands-on practice panel), QISKIT (matching code)
/// and DOCS (IBM Quantum guides for the section and the full PDF).
/// </summary>
public class NotebookView : MonoBehaviour
{
    const int Read = 0, BenchTab = 1, Qiskit = 2, Docs = 3;

    [SerializeField] List<NotebookSection> sections = new List<NotebookSection>();

    [Header("Header")]
    [SerializeField] TMP_Text sectionStrip;
    [SerializeField] TMP_Text sectionTitle;
    [SerializeField] TMP_Text progressText;
    [SerializeField] TMP_Text entryIndex;
    [SerializeField] TMP_Text entryTitle;
    [SerializeField] TMP_Text entryTag;

    [Header("Tabs: READ, BENCH, QISKIT, DOCS")]
    [SerializeField] Button[] tabButtons = new Button[4];
    [SerializeField] GameObject[] tabPanels = new GameObject[4];
    [SerializeField] TMP_Text entryBody;
    [SerializeField] ScrollRect bodyScroll;
    [SerializeField] TMP_Text qiskitText;
    [SerializeField] TMP_Text docsText;
    [SerializeField] List<NotebookBench> benches = new List<NotebookBench>();

    [Header("Full notebook")]
    [SerializeField] string pdfFileName = "ResearchNotebook.pdf";

    [Header("Colours")]
    [SerializeField] string selectedInk = "#ffe2a8";
    [SerializeField] string normalInk = "#ffb23f";
    [SerializeField] string dim = "#a8701f";
    [SerializeField] string faint = "#5a3c12";
    [SerializeField] string linkColour = "#3fe0b5";
    [SerializeField] Color tabSelected = new Color32(0xff, 0xb2, 0x3f, 0xff);
    [SerializeField] Color tabIdle = new Color32(0x6a, 0x46, 0x14, 0xff);
    [SerializeField] Color tabDisabled = new Color32(0x24, 0x18, 0x07, 0xff);
    [SerializeField] Color inkOnTab = new Color32(0x0d, 0x08, 0x02, 0xff);

    [SerializeField] float scrollStep = 0.25f;

    [Tooltip("Raised when the player closes the notebook (Esc).")]
    public UnityEvent onClose = new UnityEvent();

    readonly HashSet<string> readEntries = new HashSet<string>();
    int sectionIndex;
    int entryIndexValue;
    int tab;
    int openedFrame;
    int totalEntries;

    NotebookSection Section => sections[sectionIndex];
    NotebookSection.Entry Entry => Section.unlocked && Section.entries.Count > 0 ? Section.entries[entryIndexValue] : null;

    void Awake()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int k = i;
            if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => SelectTab(k));
        }
        foreach (var s in sections) totalEntries += s.entries.Count;
    }

    public void Open(int section = -1)
    {
        if (section >= 0) sectionIndex = Mathf.Clamp(section, 0, sections.Count - 1);
        gameObject.SetActive(true);
        openedFrame = Time.frameCount;
        tab = Read;
        Refresh(true);
    }

    /// <summary>Lets the story unlock a section by its number, e.g. "04".</summary>
    public void UnlockSection(string number)
    {
        foreach (var s in sections) if (s.number == number) s.unlocked = true;
        if (isActiveAndEnabled) Refresh(false);
    }

    void Update()
    {
        // Buttons are driven by clicks and our own keys; don't let Enter/Space re-submit a clicked button.
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null);

        var kb = Keyboard.current;
        if (kb == null || Time.frameCount == openedFrame) return;

        if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) { onClose.Invoke(); return; }
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) SelectEntry(entryIndexValue - 1);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) SelectEntry(entryIndexValue + 1);
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) SelectSection(sectionIndex - 1);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) SelectSection(sectionIndex + 1);
        else if (kb.tabKey.wasPressedThisFrame) CycleTab(kb.shiftKey.isPressed ? -1 : 1);
        else if (kb.pKey.wasPressedThisFrame) OpenPdf();
        else if (tab == Read && kb.pageDownKey.wasPressedThisFrame) Scroll(-scrollStep);
        else if (tab == Read && kb.pageUpKey.wasPressedThisFrame) Scroll(scrollStep);
        else if (tab == Docs)
        {
            if (kb.digit1Key.wasPressedThisFrame) OpenGuide(0);
            else if (kb.digit2Key.wasPressedThisFrame) OpenGuide(1);
            else if (kb.digit3Key.wasPressedThisFrame) OpenGuide(2);
        }
        else if (tab == BenchTab)
        {
            var bench = ActiveBench();
            if (bench != null) bench.HandleKeys(kb);
        }
    }

    /// <summary>Handles clicks on links: "sec:2", "entry:1", "tab:1", "guide:0", "pdf".</summary>
    public void HandleLink(string id)
    {
        if (id.StartsWith("sec:")) SelectSection(int.Parse(id.Substring(4)));
        else if (id.StartsWith("entry:")) SelectEntry(int.Parse(id.Substring(6)));
        else if (id.StartsWith("tab:")) SelectTab(int.Parse(id.Substring(4)));
        else if (id.StartsWith("guide:")) OpenGuide(int.Parse(id.Substring(6)));
        else if (id == "pdf") OpenPdf();
    }

    void SelectSection(int i)
    {
        if (sections.Count == 0) return;
        sectionIndex = (i + sections.Count) % sections.Count;
        entryIndexValue = 0;
        tab = Read;
        Refresh(true);
    }

    void SelectEntry(int i)
    {
        if (Entry == null) return;
        int next = Mathf.Clamp(i, 0, Section.entries.Count - 1);
        if (next == entryIndexValue) return;
        entryIndexValue = next;
        tab = Read;
        Refresh(true);
    }

    public void SelectTab(int i)
    {
        if (!TabAvailable(i)) return;
        tab = i;
        ShowTab();
    }

    void CycleTab(int dir)
    {
        for (int step = 1; step <= 4; step++)
        {
            int i = ((tab + dir * step) % 4 + 4) % 4;
            if (TabAvailable(i)) { SelectTab(i); return; }
        }
    }

    bool TabAvailable(int i)
    {
        var e = Entry;
        switch (i)
        {
            case Read: return true;
            case BenchTab: return e != null && FindBench(e.bench) != null;
            case Qiskit: return e != null && !string.IsNullOrEmpty(e.qiskit);
            default: return true;
        }
    }

    NotebookBench FindBench(NotebookSection.Bench kind)
    {
        if (kind == NotebookSection.Bench.None) return null;
        foreach (var b in benches) if (b != null && b.Kind == kind) return b;
        return null;
    }

    NotebookBench ActiveBench() => Entry == null ? null : FindBench(Entry.bench);

    void Scroll(float delta)
    {
        if (bodyScroll != null) bodyScroll.verticalNormalizedPosition = Mathf.Clamp01(bodyScroll.verticalNormalizedPosition + delta);
    }

    void OpenGuide(int i)
    {
        var guides = Section.guides;
        if (i >= 0 && i < guides.Count && !string.IsNullOrEmpty(guides[i].url)) Application.OpenURL(guides[i].url);
    }

    void OpenPdf()
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, pdfFileName);
        // Desktop builds get a file path; WebGL builds already get a URL.
        if (!path.Contains("://")) path = "file:///" + path.Replace('\\', '/');
        Application.OpenURL(path);
    }

    void Refresh(bool resetScroll)
    {
        if (sections.Count == 0) return;
        var s = Section;
        var e = Entry;
        if (e != null) readEntries.Add(s.number + ":" + entryIndexValue);

        // Section strip: 01 [02] 03 ...
        var strip = new StringBuilder();
        for (int i = 0; i < sections.Count; i++)
        {
            string n = sections[i].number;
            if (i == sectionIndex) strip.Append($"<link=\"sec:{i}\"><color={selectedInk}>[{n}]</color></link> ");
            else strip.Append($"<link=\"sec:{i}\"><color={(sections[i].unlocked ? dim : faint)}> {n} </color></link> ");
        }
        sectionStrip.text = strip.ToString();
        sectionTitle.text = s.number + "  " + s.title;
        progressText.text = $"PAGES READ {readEntries.Count}/{totalEntries}";

        // Entry index: > selected, • unread.
        var index = new StringBuilder();
        if (e == null) index.Append($"<color={dim}>SECTION SEALED</color>");
        else
        {
            for (int i = 0; i < s.entries.Count; i++)
            {
                string title = s.entries[i].title;
                bool unread = !readEntries.Contains(s.number + ":" + i);
                if (i == entryIndexValue) index.Append($"<link=\"entry:{i}\"><color={selectedInk}><b>></b><indent=1.1em><b>{title}</b></indent></color></link>\n");
                else index.Append($"<link=\"entry:{i}\"><color={linkColour}>{(unread ? "•" : " ")}</color><indent=1.1em><color={normalInk}>{title}</color></indent></link>\n");
            }
        }
        entryIndex.text = index.ToString();

        // Reading pane.
        if (e == null)
        {
            entryTitle.text = "SECTION SEALED";
            entryTag.text = "";
            entryBody.text = $"<color={dim}>This part of the notebook unlocks as the diagnostic session progresses.</color>";
            qiskitText.text = "";
        }
        else
        {
            entryTitle.text = e.title;
            entryTag.text = e.tag == NotebookSection.Tag.Core ? $"<color={linkColour}>[ CORE ]</color>"
                          : e.tag == NotebookSection.Tag.GoDeeper ? $"<color={dim}>[ GO DEEPER ]</color>" : "";
            string hint = FindBench(e.bench) != null
                ? $"<color={linkColour}>→ Hands-on: try this on the <link=\"tab:1\"><u>BENCH</u></link> tab  [TAB]</color>\n\n" : "";
            entryBody.text = hint + e.body;
            qiskitText.text = string.IsNullOrEmpty(e.qiskit) ? "" : FormatCode(e.qiskit);
        }
        if (resetScroll && bodyScroll != null) bodyScroll.verticalNormalizedPosition = 1f;

        // Docs.
        var docs = new StringBuilder();
        docs.Append($"<color={dim}>FURTHER READING FOR SECTION {s.number}: IBM QUANTUM DOCUMENTATION</color>\n\n");
        for (int i = 0; i < s.guides.Count && i < 3; i++)
        {
            string path = s.guides[i].url.Replace("https://", "");
            docs.Append($"<color={dim}>[{i + 1}]</color>  <link=\"guide:{i}\"><color={linkColour}><u>{s.guides[i].label}</u></color></link>\n");
            docs.Append($"      <color={faint}>{path}</color>\n\n");
        }
        docs.Append($"<color={dim}>[P]</color>  <link=\"pdf\"><color={linkColour}><u>Full Research Notebook (PDF)</u></color></link>\n");
        docs.Append($"      <color={faint}>Every entry in one printable document.</color>");
        docsText.text = docs.ToString();

        if (!TabAvailable(tab)) tab = Read;
        ShowTab();
    }

    void ShowTab()
    {
        for (int i = 0; i < tabPanels.Length; i++) tabPanels[i].SetActive(i == tab);

        var active = tab == BenchTab ? ActiveBench() : null;
        foreach (var b in benches) if (b != null) b.gameObject.SetActive(b == active);

        for (int i = 0; i < tabButtons.Length; i++)
        {
            bool available = TabAvailable(i);
            bool selected = i == tab;
            tabButtons[i].interactable = available;
            tabButtons[i].targetGraphic.color = selected ? tabSelected : available ? tabIdle : tabDisabled;
            var label = tabButtons[i].GetComponentInChildren<TMP_Text>();
            Color ink;
            if (selected) ink = inkOnTab;
            else if (!available) ColorUtility.TryParseHtmlString(faint, out ink);
            else if (i == BenchTab) ColorUtility.TryParseHtmlString(linkColour, out ink);
            else ColorUtility.TryParseHtmlString(normalInk, out ink);
            label.color = ink;
        }
    }

    string FormatCode(string code)
    {
        var sb = new StringBuilder();
        sb.Append($"<color={dim}>QISKIT  //  HOW THIS LOOKS IN CODE</color>\n\n");
        foreach (var line in code.Split('\n'))
        {
            int hash = line.IndexOf('#');
            if (hash < 0) sb.Append($"<color={normalInk}>{line}</color>\n");
            else sb.Append($"<color={normalInk}>{line.Substring(0, hash)}</color><color={dim}>{line.Substring(hash)}</color>\n");
        }
        sb.Append($"\n<color={faint}>Python, using qiskit and qiskit-aer.</color>");
        return sb.ToString();
    }
}
