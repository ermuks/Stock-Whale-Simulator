using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Only real drag/wheel input changes chat follow intent; content resizing does not.</summary>
[DefaultExecutionOrder(200)]
public sealed class ChatScrollInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    public TabletGroupChatUI chat;
    public ScrollRect scroll;
    public bool Dragging { get; private set; }
    private bool userMotion;
    public void OnBeginDrag(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left)
        { return; }
        Dragging = userMotion = true; chat.BeginManualScroll();
    }
    public void OnDrag(PointerEventData data)
    {
        if (Dragging) { chat.ReadUserScrollPosition(); }
    }
    public void OnEndDrag(PointerEventData data)
    {
        if (!Dragging)
        { return; }
        Dragging = false; chat.ReadUserScrollPosition();
    }
    public void OnScroll(PointerEventData data)
    { userMotion = true; chat.BeginManualScroll(); chat.ReadUserScrollPosition(); }
    public void StopFollowingGesture()
    { Dragging = userMotion = false; }
    void LateUpdate()
    {
        if (!userMotion)
        { return; }
        if (!chat.ReadUserScrollPosition())
        { return; }
        if (!Dragging && scroll.velocity.sqrMagnitude < 1f) { userMotion = false; }
    }
    void OnDisable() { StopFollowingGesture(); }
}
