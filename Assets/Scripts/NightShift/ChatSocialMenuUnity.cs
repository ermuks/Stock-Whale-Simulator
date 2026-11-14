using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A cached nickname menu inside the existing world-space chat canvas.</summary>
public sealed class ChatSocialMenuUnity : MonoBehaviour
{
    private TabletGroupChatUI chat;
    private RectTransform panel;
    private int botId, roomId;
    private TextMeshProUGUI title;
    public void Initialize(TabletGroupChatUI owner, TMP_FontAsset font)
    {
        chat = owner;
        var root = new GameObject("Panel :: Participant Actions", typeof(RectTransform), typeof(Image));
        root.layer = chat.chatPanel.layer;
        panel = root.GetComponent<RectTransform>(); panel.SetParent(chat.chatPanel.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f); panel.pivot = new Vector2(0, 1);
        panel.sizeDelta = new Vector2(330, 210) * chat.layoutScale;
        root.GetComponent<Image>().color = new Color(.06f, .09f, .11f, .98f);
        title = Label(panel, "참여자 관리", font, 0, 34);
        for (int i = 0; i < 3; i++)
        {
            var action = (SocialModeration)i;
            string caption = i == 0 ? "경고" : i == 1 ? "채팅 금지 · " + ChatSocialConfig.Current.Int("muteMinutes") + "분" : "추방";
            var label = Label(panel, caption, font, 40 + i * 54, 50);
            label.raycastTarget = true;
            var button = label.gameObject.AddComponent<Button>(); button.targetGraphic = label;
            button.onClick.AddListener(() => Execute(action));
        }
        panel.gameObject.SetActive(false);
    }
    private TextMeshProUGUI Label(RectTransform parent, string text, TMP_FontAsset font, float y, float height)
    {
        var go = new GameObject("Text :: " + text, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.offsetMin = new Vector2(14 * chat.layoutScale, -(y + height) * chat.layoutScale);
        rect.offsetMax = new Vector2(-14 * chat.layoutScale, -y * chat.layoutScale);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; label.text = text;
        label.fontSize = 25 * chat.layoutScale; label.enableAutoSizing = false; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = new Color(.90f, .92f, .94f); label.raycastTarget = false;
        return label;
    }
    public void Open(int id, RectTransform nickname)
    {
        var society = InvestorSociety.Instance;
        roomId = chat.SelectedRoom; botId = id;
        if (society == null || roomId < 1 || roomId > InvestorSociety.PlayerRoomCount || !society.IsRoomOpen(roomId) ||
            !society.SocialBots.TryGetValue(id, out var bot) || bot.Social.Psychology.Room != roomId)
        { Close(); return; }
        var parent = (RectTransform)panel.parent;
        Vector3 local = parent.InverseTransformPoint(nickname.position);
        panel.anchoredPosition = new Vector2(Mathf.Clamp(local.x, parent.rect.xMin, parent.rect.xMax - panel.rect.width),
            Mathf.Clamp(local.y, parent.rect.yMin + panel.rect.height, parent.rect.yMax));
        title.text = bot.Social.Nickname;
        panel.SetAsLastSibling(); panel.gameObject.SetActive(true);
    }
    private void Execute(SocialModeration action)
    {
        InvestorSociety.Instance?.Moderate(roomId, botId, action, out _);
        Close();
    }
    public void Close() { if (panel != null) { panel.gameObject.SetActive(false); } }
    private void OnDestroy() { if (panel != null) { Destroy(panel.gameObject); } }
}
