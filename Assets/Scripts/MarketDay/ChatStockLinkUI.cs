using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Uses the existing World Space event camera and lets parent ScrollRect handle dragging.</summary>
public sealed class ChatStockLinkUI : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    private TMP_Text label;
    private Action<string> activate;
    private Action fallback;
    public void Bind(TMP_Text text, Action<string> onLink, Action onText)
    { label = text; activate = onLink; fallback = onText; }
    // Claim the press so the ancestor message Button cannot steal it before this link's click handler.
    public void OnPointerDown(PointerEventData eventData) { }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging || !eventData.eligibleForClick || label == null)
        { return; }
        int index = TMP_TextUtilities.FindIntersectingLink(label, eventData.position, eventData.pressEventCamera);
        if (index >= 0) { activate?.Invoke(label.textInfo.linkInfo[index].GetLinkID()); }
        else { fallback?.Invoke(); }
    }
}
