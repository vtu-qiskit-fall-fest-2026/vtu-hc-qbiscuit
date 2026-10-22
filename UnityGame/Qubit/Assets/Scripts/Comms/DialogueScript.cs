using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One radio exchange on the COMMS LINK, edited in the Inspector.</summary>
[CreateAssetMenu(menuName = "Andromeda/Dialogue Script", fileName = "Dialogue")]
public class DialogueScript : ScriptableObject
{
    public enum Speaker { MissionSpecialist, You, System }

    [Serializable]
    public class Line
    {
        public Speaker speaker;
        [TextArea(2, 6)] public string text;
    }

    public string channel = "LUNAR RELAY  //  CH 04";
    public List<Line> lines = new List<Line>();
}
