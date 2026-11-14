using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 전화 컨트롤러를 표시하는 뷰. 받기·거절·닫기 이벤트와 3D 그립을 변경하지 않는다.</summary>
public sealed class PhoneScreenUI : MonoBehaviour
{
    public TextMeshProUGUI state, identity, countdown, closeHint, callHistory;
    public GameObject incoming, result, information;
    public Button informationButton;
    private int lastResolved;
    private readonly Queue<string> history = new(4);
    void Start()
    { informationButton.onClick.AddListener(ToggleInformation); }
    private void ToggleInformation()
    { information.SetActive(!information.activeSelf); }
    public void Refresh(PhoneCallController phone)
    {
        incoming.SetActive(phone.Ringing); result.SetActive(!phone.Ringing);
        DeviceTerminalUI.Set(state, phone.Ringing ? "수신 중" : "연락 기록");
        string caller = phone.CurrentCaller == PhoneCallController.Caller.Institution ? "기관 담당자 (자칭)" : phone.CurrentCaller == PhoneCallController.Caller.Research ? "리서치 데스크" : "알 수 없는 번호";
        DeviceTerminalUI.Set(phone.title, phone.Ringing ? caller : "최근 연락");
        DeviceTerminalUI.Set(identity, "신원 미확인");
        DeviceTerminalUI.Set(countdown, phone.RemainingSeconds + "초 후 종료");
        DeviceTerminalUI.Set(phone.body, phone.LastMessage);
        DeviceTerminalUI.Set(closeHint, phone.Ringing ? "화면만 닫기 · 전화 수신 유지\n3 / Esc" : "시장 화면으로 · 3 / Esc");
        if (lastResolved != phone.ResolvedCalls)
        {
            lastResolved = phone.ResolvedCalls;
            history.Enqueue(DeviceMarketJournal.Stamp() + "  " + caller);
            while (history.Count > 4)
            { history.Dequeue(); }
            DeviceTerminalUI.Set(callHistory, string.Join("\n", history));
        }
    }
    void OnDestroy()
    {
        if (informationButton != null)
        { informationButton.onClick.RemoveListener(ToggleInformation); }
    }
}
