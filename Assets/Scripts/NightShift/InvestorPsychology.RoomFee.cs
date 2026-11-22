using System;
using System.Globalization;
using UnityEngine;

public sealed partial class InvestorPsychology
{
    private readonly InvestorEventMemory feeEvents = new();
    public void RecordRoomFee(long eventId,long amount,decimal assets,DailyIncomeLedger.Rules rules)
    {
        if (amount <= 0 || !feeEvents.Add(eventId))
        { return; }
        // 실제 매매 성과에서 아직 공제하지 않은 이용료만 평가. 소액 이용료를 투자금 전액 손실로 보지 않는다.
        float ratio = (float)(amount / Math.Max(1m,assets) * 100m);
        float impact = Mathf.Min(rules.idleFeeMaximum,ratio * rules.idleFeeImpact);
        Betrayal += impact; BetrayalMemory = Mathf.Clamp(BetrayalMemory + impact*.55f,0,100);
        Remember(new SocialMemory { eventId = eventId, type = SocialMemoryKind.ROOM_FEE, actorId = -1, targetId = SocialId, salience = 35,
            amount = amount.ToString(CultureInfo.InvariantCulture), at = ChatSocialAudit.GameMinute });
        LastCause = "이용료 " + amount.ToString("N0") + "돈 · 실현 성과 없음 · 배신감 +" + impact.ToString("0.00");
        Clamp(); AuditEmotion("room-fee",eventId);
    }
}
