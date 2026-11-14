using UnityEngine;

/// <summary>기존 연락 UI의 진입점. 방 참여와 재회유 기억 처리는 투자자 사회에 위임한다.</summary>
public sealed class SocialContactController : MonoBehaviour
{
    public static SocialContactController Instance { get; private set; }
    public string LastResult => InvestorSociety.Instance == null ? "참여자 준비 중" : InvestorSociety.Instance.LastAction;
    void Awake() { Instance = this; }
    public bool Contact(int id, int room)
    { return InvestorSociety.Instance != null && InvestorSociety.Instance.Invite(id, room); }
    void OnDestroy() { if (Instance == this) { Instance = null; } }
}
