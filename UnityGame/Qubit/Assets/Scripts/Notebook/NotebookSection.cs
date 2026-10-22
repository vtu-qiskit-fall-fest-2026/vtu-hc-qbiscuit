using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One chapter of the Research Notebook: its entries and the IBM Quantum guides that go with it.</summary>
[CreateAssetMenu(menuName = "Andromeda/Notebook Section", fileName = "NotebookSection")]
public class NotebookSection : ScriptableObject
{
    public enum Tag { None, Core, GoDeeper }
    public enum Bench { None, XGate, Cnot, Parity, Shots, Quiz }

    [Serializable]
    public class Entry
    {
        public string title;
        public Tag tag;
        [TextArea(8, 40)] public string body;
        [Tooltip("Hands-on bench shown on the BENCH tab for this entry.")]
        public Bench bench;
        [Tooltip("Qiskit code shown on the QISKIT tab. Text after # is shown as a comment.")]
        [TextArea(4, 20)] public string qiskit;
    }

    [Serializable]
    public class GuideLink
    {
        public string label;
        public string url;
    }

    public string number = "01";
    public string title = "SECTION";
    [Tooltip("Locked sections show as sealed until the story unlocks them.")]
    public bool unlocked = true;
    public List<Entry> entries = new List<Entry>();
    public List<GuideLink> guides = new List<GuideLink>();
}
