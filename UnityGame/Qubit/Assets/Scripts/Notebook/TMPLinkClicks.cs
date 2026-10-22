using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>Forwards clicks on &lt;link="id"&gt; tags in a TextMeshPro text as the link id.</summary>
[RequireComponent(typeof(TMP_Text))]
public class TMPLinkClicks : MonoBehaviour, IPointerClickHandler
{
    [System.Serializable] public class LinkEvent : UnityEvent<string> { }

    public LinkEvent onLinkClicked = new LinkEvent();

    TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        int index = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
        if (index < 0) return;
        onLinkClicked.Invoke(text.textInfo.linkInfo[index].GetLinkID());
    }
}
