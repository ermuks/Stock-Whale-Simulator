# 위험 알림·단일 리딩방·가격 표시 코드 기록

2026-11-15. 이번 작업 시작 시 저장한 파일을 기준으로 비교했다. 이전 작업의 변경을 HEAD로 되돌리지 않았다. 임시 화면 촬영기는 검증 뒤 제거하며 원본을 Validation에 보관한다.

## 신규 C# 전문

### Assets/Scripts/NightShift/ChatSocialMarketRisk.cs

```csharp
using System;
using System.Collections.Generic;

public sealed partial class InvestorPsychology
{
    private sealed class MarketRiskMemory
    {
        public int version, grade, peak;
        public long eventId;
        public double buyScale = 1, pauseUntil, at;
    }
    private readonly Dictionary<string, MarketRiskMemory> marketRisk = new();
    internal long LastRiskBulletin;

    // This is the bot's observed risk, not an omniscient read of the current exchange grade.
    public double MarketRiskBuyScale(string symbol, double seconds)
    {
        if (symbol == null || !marketRisk.TryGetValue(symbol, out var memory) || seconds < memory.at || seconds - memory.at >= ChatSocialConfig.Current.Get("riskAttentionSeconds"))
        { return 1; }
        return seconds < memory.pauseUntil ? 0 : memory.buyScale;
    }
    public bool ObserveMarketRisk(string symbol, int version, int grade, long eventId, double seconds, double preference, bool held)
    {
        if (string.IsNullOrEmpty(symbol) || grade < 0 || grade > 4)
        { return false; }
        marketRisk.TryGetValue(symbol, out var memory);
        if (memory != null && version <= memory.version)
        { return false; }
        if (memory == null)
        {
            if (marketRisk.Count >= 16)
            {
                string oldest = null; double at = double.MaxValue;
                foreach (var row in marketRisk) { if (row.Value.at < at) { at = row.Value.at; oldest = row.Key; } }
                if (oldest != null) { marketRisk.Remove(oldest); }
            }
            memory = new MarketRiskMemory(); marketRisk[symbol] = memory;
        }
        var config = ChatSocialConfig.Current;
        double caution = Math.Max(.05, Math.Min(1, (1 - Math.Max(0, Math.Min(1, preference))) * .65 + Fear / 100d * .25 + (100 - Greed) / 100d * .2 + (held ? .15 : 0)));
        int severity = Math.Min(3, grade);
        // A fluctuating warning must not pump the same fear repeatedly.
        double fear = Math.Max(0, severity - memory.peak) * config.Get("riskFearPerGrade") * caution;
        memory.peak = Math.Max(memory.peak, severity);
        memory.version = version; memory.grade = grade; memory.eventId = eventId; memory.at = seconds;
        bool dismissive = caution < .35 && Greed >= 60;
        memory.buyScale = dismissive ? 1 : Math.Max(.1, 1 - severity / 3d * caution * config.Get("riskBuyReduction"));
        memory.pauseUntil = grade >= 3 && caution >= config.Get("riskPauseConcern") ? seconds + config.Get("riskPauseSeconds") : 0;
        if (grade == 0) { memory.buyScale = 1; memory.pauseUntil = 0; }
        SocialDelta("fear", fear, "market-risk-observed", eventId);
        LastCause = "위험 알림 확인: " + symbol + " · " + InvestorSociety.MarketRiskLabel(grade);
        ChatSocialAudit.Journal.Event(new SocialEvent { id = eventId, time = ChatSocialAudit.GameMinute, actor = SocialId,
            kind = "MARKET_RISK_OBSERVED", reason = symbol + " " + InvestorSociety.MarketRiskLabel(grade), value = MarketRiskBuyScale(symbol, seconds) });
        return true;
    }
}

public sealed partial class InvestorSociety
{
    private sealed class MarketRiskBulletin
    {
        public Stock stock;
        public int version, grade, offered;
        public long id;
        public double expires;
    }
    private readonly List<MarketRiskBulletin> riskBulletins = new();
    private readonly Dictionary<int, double> nextRiskSpeech = new();
    public static string MarketRiskLabel(int grade) => grade switch { 0 => "안전", 1 => "경고", 2 => "위험", 3 => "고위험", _ => "거래중지" };
    public static bool IsMarketRiskNotice(string notice) => notice != null && notice.StartsWith("MARKET_RISK_", StringComparison.Ordinal);

    public long NotifyMarketRisk(Stock stock)
    {
        if (stock == null || stock.RiskPending || stock.IsDelisted)
        { return 0; }
        foreach (var prior in riskBulletins)
        { if (prior.stock == stock && prior.version == stock.RiskVersion) { return prior.id; } }
        double now = UnityEngine.Time.time;
        riskBulletins.RemoveAll(row => row.stock == stock || row.expires < now || row.stock.IsDelisted);
        if (riskBulletins.Count >= 16) { riskBulletins.RemoveAt(0); }
        var bulletin = new MarketRiskBulletin { stock = stock, version = stock.RiskVersion, grade = (int)stock.RiskGrade,
            id = ChatSocialIds.Next(), expires = now + Config.Get("riskBulletinSeconds") };
        riskBulletins.Add(bulletin);
        ChatSocialAudit.Journal.Event(new SocialEvent { id = bulletin.id, time = SocialTime.GameMinute, kind = "MARKET_RISK_NOTICE",
            reason = stock.symbol + " " + MarketRiskLabel(bulletin.grade), value = bulletin.grade });
        return bulletin.id;
    }
    private void TickMarketRisk(TradeBot bot)
    {
        var mind = bot.Social.Psychology;
        if (!Present(bot, 0) || !mind.ActiveAt((float)SocialTime.Seconds))
        { return; }
        foreach (var bulletin in riskBulletins)
        {
            if (bulletin.id <= mind.LastRiskBulletin) { continue; }
            mind.LastRiskBulletin = bulletin.id;
            var stock = bulletin.stock;
            if (bulletin.expires < SocialTime.Seconds || stock.IsDelisted || stock.RiskVersion != bulletin.version)
            { continue; }
            bool held = bot.GetOwnedVolume(stock) > 0;
            bool interested = held || mind.WatchedStock == stock;
            if (!interested || mind.SocialRng.Next() >= Config.Get("riskWitnessChance") * (.5 + mind.Activity * .5))
            { continue; }
            if (!mind.ObserveMarketRisk(stock.symbol, bulletin.version, bulletin.grade, bulletin.id, SocialTime.Seconds, bot.riskPreference, held))
            { continue; }
            int room = mind.Room;
            bool roomBusy = nextRiskSpeech.TryGetValue(room, out double next) && SocialTime.Seconds < next;
            if (roomBusy || bulletin.offered >= Config.Int("riskVoicesPerNotice") || !Present(bot, room) ||
                mind.SocialRng.Next() >= Config.Get("riskSpeechChance") * (.5 + mind.Traits.Sociability / 100d))
            { continue; }
            var facts = CaptureFacts(bot, stock);
            facts.eventId = bulletin.id; facts.riskVersion = bulletin.version; facts.riskGrade = bulletin.grade;
            facts.riskObserver = mind.SocialId;
            SetRiskStance(mind, facts);
            Offer(bot, room, SocialMotive.SELF_EVENT, facts, SocialAct.REACT, cause: bulletin.id);
            bulletin.offered++; nextRiskSpeech[room] = SocialTime.Seconds + Config.Get("riskSpeechGapSeconds");
        }
    }
    private void SetRiskStance(InvestorPsychology mind, SocialFacts facts)
    {
        double scale = mind.MarketRiskBuyScale(facts.symbol, SocialTime.Seconds);
        facts.notice = "MARKET_RISK_" + (facts.riskGrade == 0 ? "EASED" : facts.riskGrade == 4 ? "HALTED" : scale == 0 ? "PAUSE" : scale < .8 ? "CAUTIOUS" : scale < 1 ? "WATCH" : "DISMISS");
    }
    private bool RefreshMarketRiskFacts(TradeBot bot, SocialIntent intent, SocialFacts current)
    {
        var offered = intent.facts;
        if (!IsMarketRiskNotice(offered?.notice)) { return true; }
        // Replies may discuss a warning, but cannot borrow another bot's observation or trading stance.
        if (intent.replyToId != 0 || offered.riskObserver != bot.Social.Id)
        { current.notice = null; return true; }
        var stock = FindSocialStock(offered.symbol);
        if (stock == null || stock.IsDelisted || stock.RiskVersion != offered.riskVersion)
        { return false; }
        current.riskVersion = offered.riskVersion; current.riskGrade = offered.riskGrade; current.riskObserver = offered.riskObserver;
        SetRiskStance(bot.Social.Psychology, current);
        return true;
    }
}
```

### Assets/Scripts/NightShift/ChatSocialRecruitment.cs

```csharp
using System;
using System.Collections.Generic;

public sealed partial class InvestorPsychology
{
    public int InvitedPlayerRooms;
    public readonly Dictionary<int, double> KnownPlayerRooms = new();
}

public sealed partial class InvestorSociety
{
    public bool CanDiscoverPlayerRoom(TradeBot bot, int roomId)
    {
        var room = GetRoom(roomId);
        if (bot?.Social == null || room == null || !room.PlayerOwned || roomId > AvailablePlayerRoomCount)
        { return false; }
        var mind = bot.Social.Psychology;
        if (!room.Social.firstMemberInvited)
        { return false; }
        return (mind.InvitedPlayerRooms & (1 << (roomId - 1))) != 0 ||
            mind.KnownPlayerRooms.TryGetValue(roomId, out double until) && SocialTime.GameMinute < until;
    }
    public bool ObserveRoomReferral(TradeBot witness, TradeBot author, SocialMessage message)
    {
        if (witness?.Social == null || author?.Social == null || witness == author || message == null || message.roomId != 0 ||
            message.senderId != author.Social.Id || message.act != SocialAct.PRAISE_PLAYER || message.facts == null || !message.facts.directLead || !message.facts.profitable)
        { return false; }
        int roomId = author.Social.Psychology.Room;
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned || roomId > AvailablePlayerRoomCount || !room.Social.firstMemberInvited || !room.Members.Contains(author.Social.Id))
        { return false; }
        bool actualProfit = false;
        foreach (var experience in author.Social.Psychology.Experiences)
        {
            if (experience.EventId == message.facts.eventId && experience.Room == roomId && experience.CommandId > 0 && experience.ReturnPercent > 0)
            { actualProfit = true; break; }
        }
        if (!actualProfit) { return false; }
        witness.Social.Psychology.KnownPlayerRooms[roomId] = SocialTime.GameMinute + Config.Get("roomReferralMinutes");
        ChatSocialAudit.Journal.Event(new SocialEvent { id = message.id, time = SocialTime.GameMinute, actor = witness.Social.Id, target = author.Social.Id,
            room = roomId, kind = "ROOM_REFERRAL_HEARD", reason = "회원의 실제 수익 후기 목격" });
        return true;
    }
    public int InvitedRoomMask
    {
        get
        {
            int mask = 0;
            for (int i = 1; i <= AvailablePlayerRoomCount; i++) { if (GetRoom(i).Social.firstMemberInvited) { mask |= 1 << (i - 1); } }
            return mask;
        }
    }
    public void RestoreInvitedRooms(int mask)
    {
        for (int i = 1; i <= PlayerRoomCount; i++) { GetRoom(i).Social.firstMemberInvited = i <= AvailablePlayerRoomCount && (mask & (1 << (i - 1))) != 0; }
    }
}
```

### Tests/ChatSocial/TestsMarketRiskRecruitment.cs

```csharp
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static void MarketRiskRecruitmentScenarios()
    {
        ChatSocialAssets.Reload();
        var cautious = new InvestorPsychology(4101,73129);
        var speculative = new InvestorPsychology(4102,73129);
        var saved = cautious.CaptureSocial(); saved.greed=65; saved.fear=10; cautious.RestoreSocial(saved);
        saved=speculative.CaptureSocial(); saved.greed=85; saved.fear=0; speculative.RestoreSocial(saved);
        Check(cautious.MarketRiskBuyScale("NM",0)==1,"unseen bulletin cannot change a bot's order size");
        Check(cautious.ObserveMarketRisk("NM",1,1,9001,0,.05,true) && cautious.Fear>10 && cautious.MarketRiskBuyScale("NM",0)<1,"cautious holder acknowledges warning with fear and a smaller future buy");
        float fear=cautious.Fear;
        Check(!cautious.ObserveMarketRisk("NM",1,1,9001,1,.05,true) && cautious.Fear==fear,"duplicate warning version cannot accumulate fear");
        Check(cautious.ObserveMarketRisk("NM",2,3,9002,2,.05,true) && cautious.MarketRiskBuyScale("NM",3)==0,"high risk pauses cautious holder's additional buys");
        Check(cautious.MarketRiskBuyScale("NM",25)>0 && cautious.MarketRiskBuyScale("NM",25)<.5 && cautious.MarketRiskBuyScale("OTHER",3)==1,"pause expires to reduced sizing and cannot affect unrelated symbols");
        Check(cautious.MarketRiskBuyScale("NM",125)==1,"missing a later recovery bulletin cannot suppress new buy sizes forever");
        speculative.ObserveMarketRisk("NM",2,3,9002,2,.95,false);
        Check(speculative.MarketRiskBuyScale("NM",3)==1,"speculative greedy bot can knowingly dismiss the same notice");
        cautious.ObserveMarketRisk("NM",3,0,9003,30,.05,true);
        Check(cautious.MarketRiskBuyScale("NM",30)==1,"observed safe downgrade removes the additional buy restriction");
        fear=cautious.Fear; cautious.ObserveMarketRisk("NM",4,3,9004,31,.05,true);
        Check(cautious.Fear==fear,"repeated grade oscillation does not pump already-experienced peak fear");

        var fixture=SocialReplay(73129,8,steps:1); var society=fixture.Society;
        var stock=StockManager.Instance.stocks[0]; var bot=society.SocialBots[1];
        stock.RiskGrade=StockRiskGrade.HighRisk; stock.RiskVersion=1; stock.RiskPending=false;
        Time.time=10; society.SocialTime.Set(1,550,10);
        long notice=society.NotifyMarketRisk(stock);
        Check(notice>0 && society.NotifyMarketRisk(stock)==notice,"production bulletin registration deduplicates stock and version");
        bot.Social.Psychology.ObserveMarketRisk(stock.symbol,1,3,notice,10,.05,true);
        var intent=new SocialIntent {roomId=1,motive=SocialMotive.SELF_EVENT,forcedAct=SocialAct.REACT,
            facts=new SocialFacts {symbol=stock.symbol,notice="MARKET_RISK_PAUSE",riskGrade=3,riskVersion=1,riskObserver=bot.Social.Id,eventId=notice}};
        var generated=typeof(InvestorSociety).GetMethod("GenerateSocialMessage",BindingFlags.Instance|BindingFlags.NonPublic);
        SocialMessage message=null; society.SocialMessagePublished+=row=>message=row;
        generated.Invoke(society,new object[]{bot,intent});
        Check(message!=null && message.templateKey.StartsWith("market-risk.") && message.text.Contains("고위험") && !message.text.Contains("{"),"warning speech uses live grade, bot stance and guarded Korean text");
        message=null; stock.RiskVersion=2; stock.RiskGrade=StockRiskGrade.Safe;
        generated.Invoke(society,new object[]{bot,intent});
        Check(message==null,"typing an obsolete warning is discarded after an exchange-grade change");
        Check(ChatSocialAssets.Templates.templates.Count(t=>t.key.StartsWith("market-risk."))==216,"risk templates cover six stances without leaking into ordinary chatter");

        var day=new MarketDayController(); day.InitializeReplay(73129);
        society=new InvestorSociety(); society.BindSocialBots(day.Bots); InvestorSociety.Instance=society;
        PlayerProgression.Instance=null; Time.time=100; society.SocialTime.Set(1,560,100);
        var first=day.Bots[0]; var unaware=day.Bots[1]; var witness=day.Bots[2]; var banned=day.Bots[3];
        var choose=typeof(InvestorSociety).GetMethod("ChooseRoom",BindingFlags.NonPublic|BindingFlags.Instance);
        Check((int)choose.Invoke(society,new object[]{first})==0 && !society.Join(first,1),"opening player rooms creates no random entry before an invitation");
        society.GetRoom(4).OwnerBotId=99;
        Check((int)choose.Invoke(society,new object[]{first})==4,"existing NPC room remains discoverable without a player invitation");
        society.GetRoom(1).Social.banned.Add(banned.Social.Id);
        Check(!society.Invite(banned.Social.Id,1) && society.InvitedRoomMask==0,"first-invite guarantee cannot bypass a ban or consume its entitlement on failure");
        Check(society.Invite(first.Social.Id,1) && first.Social.Psychology.Room==1 && society.GetRoom(1).Members.Contains(first.Social.Id),"first eligible bot selected by player joins regardless of initial trust probability");
        Check(!society.Join(unaware,1) && !society.CanDiscoverPlayerRoom(unaware,1),"first member does not magically reveal the room to the rest of the population");
        Check(society.IsRoomOpen(1) && !society.IsRoomOpen(2) && !society.IsRoomOpen(3) && !society.Invite(unaware.Social.Id,2) && !society.Join(unaware,3,true),"one player room only: legacy second/third rooms cannot open or accept even a direct invite");
        var profit=new InvestorExperience(990001,51,1,stock.symbol,1000,1300,100,"fixture actual fill");
        first.Social.Psychology.Record(profit,new InvestorSocietySettings());
        var testimony=new SocialMessage {id=990002,senderId=first.Social.Id,roomId=0,act=SocialAct.PRAISE_PLAYER,facts=new SocialFacts{eventId=profit.EventId,directLead=true,profitable=true,symbol=stock.symbol}};
        Check(society.ObserveRoomReferral(witness,first,testimony) && society.CanDiscoverPlayerRoom(witness,1) && !society.CanDiscoverPlayerRoom(unaware,1),"only a witness to a member's actual profitable instruction learns that room");
        testimony.facts.eventId=990099;
        Check(!society.ObserveRoomReferral(unaware,first,testimony),"made-up profitable testimony cannot recruit investors");
        testimony.facts.eventId=profit.EventId; testimony.roomId=1;
        Check(!society.ObserveRoomReferral(unaware,first,testimony),"private testimonials cannot be overheard outside the room");
        saved=witness.Social.Psychology.CaptureSocial();
        var restored=new InvestorPsychology(witness.Social.Id,73129);
        Check(restored.RestoreSocial(saved) && restored.KnownPlayerRooms.ContainsKey(1),"heard-room knowledge survives social snapshot round trip");
        saved.room=2; restored.RestoreSocial(saved);
        Check(restored.Room==0,"old second-room membership restores to public lounge without exposing an extra player room");
        society.SocialTime.Set(1,690,230);
        Check(!society.CanDiscoverPlayerRoom(witness,1) && society.CanDiscoverPlayerRoom(first,1),"stale rumor expires while a personal invitation remains known");
        var progression=new PlayerProgression {CanOperateRooms=true}; string json=progression.Capture();
        society.RestoreInvitedRooms(0);
        Check(progression.Restore(json,progression.WorldId) && society.InvitedRoomMask==1,"progression snapshot preserves used first-invite entitlement independently per room");
        Time.time=1000; first.Social.Psychology.Room=0; society.GetRoom(1).Members.Clear();
        Check(society.GetRoom(1).Social.firstMemberInvited,"emptying a room cannot regenerate the guaranteed first-member invitation");
        InvestorSociety.Instance=null;
    }
}
```

## 함수 단위 원본·수정본

필드 추가: SocialFacts에는 위험 등급/버전/관찰자 ID, SocialRoomState에는 첫 초대 사용 여부, SocialSnapshot에는 직접 초대 방 비트와 목격한 방의 유효기한을 추가했다. 기존 2·3번 방 식별자는 호환용으로 유지하되 플레이 가능한 방 수는 1이다.

### Assets/Scripts/NightShift/InvestorSociety.cs · IsRoomOpen

원본:

```csharp
public bool IsRoomOpen(int id)
    {
        var room = GetRoom(id);
        return room != null && (room.PlayerOwned ? PlayerProgression.Instance == null || PlayerProgression.Instance.CanOperateRooms : room.OwnerBotId > 0);
    }
```

수정본:

```csharp
public bool IsRoomOpen(int id)
    {
        var room = GetRoom(id);
        return room != null && (room.PlayerOwned ? id <= AvailablePlayerRoomCount && (PlayerProgression.Instance == null || PlayerProgression.Instance.CanOperateRooms) : room.OwnerBotId > 0);
    }
```

### Assets/Scripts/NightShift/InvestorSociety.cs · ChooseRoom

원본:

```csharp
private int ChooseRoom(TradeBot bot)
    {
        if (suggestions.TryGetValue(bot.Social.Id,out int suggested) && IsRoomOpen(suggested))
        { return suggested; }
        int first = random.Next(Rooms.Length);
        for (int i = 0; i < Rooms.Length; i++)
        {
            int candidate = Rooms[(first + i) % Rooms.Length].Id;
            if (IsRoomOpen(candidate))
            { return candidate; }
        }
        return 0;
    }
```

수정본:

```csharp
private int ChooseRoom(TradeBot bot)
    {
        if (suggestions.TryGetValue(bot.Social.Id,out int suggested) && IsRoomOpen(suggested) && CanDiscoverPlayerRoom(bot, suggested))
        { return suggested; }
        int first = random.Next(Rooms.Length);
        for (int i = 0; i < Rooms.Length; i++)
        {
            int candidate = Rooms[(first + i) % Rooms.Length].Id;
            if (IsRoomOpen(candidate) && (!Rooms[(first + i) % Rooms.Length].PlayerOwned || CanDiscoverPlayerRoom(bot, candidate)))
            { return candidate; }
        }
        return 0;
    }
```

### Assets/Scripts/NightShift/InvestorSociety.cs · TickBot

원본:

```csharp
public void TickBot(TradeBot bot, float now)
    {
        var mind = bot.Social.Psychology;
        mind.Decay(Mathf.Max(0f, now - mind.UpdatedAt), settings); mind.UpdatedAt = now;
        if (!bot.isActiveAndEnabled)
        { if (mind.Room > 0) { Leave(bot, "거래 활동 중단"); } return; }
        foreach (var position in bot.Social.Positions)
        {
            decimal basis = bot.GetOwnedAverage(position.Key);
            if (bot.GetOwnedVolume(position.Key) > 0m && basis > 0m)
            { mind.ObserveRisk((float)((position.Key.currentPrice / basis - 1m) * 100m)); }
            else if (position.Value.SellPrice > 0m && now - position.Value.SellTime < day.recentTradeLifetime)
            {
                float recency = Mathf.Clamp01(1f - (now - position.Value.SellTime) / Mathf.Max(1f, day.recentTradeLifetime));
                mind.ObserveMissedGain((float)((position.Key.currentPrice / position.Value.SellPrice - 1m) * 100m) * recency);
            }
        }
        Orders?.Tick(bot, now, random.NextDouble());
        if (!mind.ActiveAt(now))
        { return; }
        if (!SocialReady)
        {
            ReadRecent(bot, 0, now);
            if (mind.Room > 0) { ReadRecent(bot, mind.Room, now); }
        }
        if (now >= mind.NextMembership)
        {
            mind.NextMembership = now + 20f + (float)random.NextDouble() * 45f;
            if (mind.Room == 0)
            {
                int target = ChooseRoom(bot);
                float interest = target == NpcLeadingRoom.RoomId ? .3f + .4f * mind.Activity : mind.JoinChance;
                if (target > 0 && random.NextDouble() < settings.joinProbability * interest)
                { Join(bot,target); }
            }
            else if (mind.Room > 0 && !SocialReady)
            {
                var currentRoom = GetRoom(mind.Room);
                var command = currentRoom.Command;
                bool idle = currentRoom.PlayerOwned ? command == null || command.Canceled : NpcLeadingRoom.Instance == null || !NpcLeadingRoom.Instance.HasSignal;
                float boredom = idle ? Mathf.Clamp01((now - mind.JoinedRoomAt - 90f) / 180f) * (1f - Mathf.Clamp01(mind.Trust / 100f)) : 0f;
                if (currentRoom.OwnerBotId != bot.Social.Id && random.NextDouble() < settings.leaveProbability * mind.LeaveChance + settings.idleRoomLeaveProbability * boredom)
                { Leave(bot, idle ? "다른 종목과 대화를 보러 이동" : "관찰한 위험과 자신의 경험을 재평가"); }
            }
        }
        if (mind.Room > 0)
        { Orders?.Consider(bot, GetRoom(mind.Room).Command, random.NextDouble(), now); }
        if (SocialReady)
        { return; }
        if (now < mind.NextSpeech || now - bot.Social.LastSpeech < settings.speechCooldown)
        { return; }
        if (mind.Latest.HasValue && mind.LastSharedExperience != mind.Latest.Value.EventId && random.NextDouble() < .25f + mind.Activity * .35f)
        {
            var experience = mind.Latest.Value;
            int room = random.NextDouble() < .45 ? 0 : mind.Room;
            Speak(bot, room, InvestorConversation.Testimony(bot, experience, false), Context(bot, experience, room));
            mind.LastSharedExperience = experience.EventId;
        }
        else if (mind.Room > 0 && now >= GetRoom(mind.Room).NextConversation && random.NextDouble() < .25f * mind.Activity)
        {
            string text = InvestorConversation.Waiting(bot, Orders?.Get(bot), random.Next());
            if (mind.LastWitnessRoom == mind.Room && now - mind.LastRoomWitnessAt < 45f && random.NextDouble() < .3f)
            { text = mind.LastRoomWitness.Nickname + "님, " + text; }
            Speak(bot, mind.Room, text, default, ChatEvent.SmallTalk);
        }
        else if (mind.LatestObservation.HasValue && mind.LastSharedObservation != mind.LatestObservation.Value.EventId && random.NextDouble() < .2f + mind.Activity * .2f)
        {
            var observation = mind.LatestObservation.Value;
            var context = new InvestorMessageContext(observation.EventId, mind.Room, 1f, .75f, mind.Trust, false);
            Speak(bot, mind.Room, InvestorConversation.MissedForecast(bot, observation), context);
            mind.LastSharedObservation = observation.EventId;
        }
    }
```

수정본:

```csharp
public void TickBot(TradeBot bot, float now)
    {
        var mind = bot.Social.Psychology;
        mind.Decay(Mathf.Max(0f, now - mind.UpdatedAt), settings); mind.UpdatedAt = now;
        if (!bot.isActiveAndEnabled)
        { if (mind.Room > 0) { Leave(bot, "거래 활동 중단"); } return; }
        if (mind.Room > AvailablePlayerRoomCount && mind.Room <= PlayerRoomCount)
        { Leave(bot, "비활성화된 이전 리딩방에서 공용 라운지로 이동", ChatRoomNotice.None); }
        foreach (var position in bot.Social.Positions)
        {
            decimal basis = bot.GetOwnedAverage(position.Key);
            if (bot.GetOwnedVolume(position.Key) > 0m && basis > 0m)
            { mind.ObserveRisk((float)((position.Key.currentPrice / basis - 1m) * 100m)); }
            else if (position.Value.SellPrice > 0m && now - position.Value.SellTime < day.recentTradeLifetime)
            {
                float recency = Mathf.Clamp01(1f - (now - position.Value.SellTime) / Mathf.Max(1f, day.recentTradeLifetime));
                mind.ObserveMissedGain((float)((position.Key.currentPrice / position.Value.SellPrice - 1m) * 100m) * recency);
            }
        }
        Orders?.Tick(bot, now, random.NextDouble());
        if (!mind.ActiveAt(now))
        { return; }
        if (!SocialReady)
        {
            ReadRecent(bot, 0, now);
            if (mind.Room > 0) { ReadRecent(bot, mind.Room, now); }
        }
        if (now >= mind.NextMembership)
        {
            mind.NextMembership = now + 20f + (float)random.NextDouble() * 45f;
            if (mind.Room == 0)
            {
                int target = ChooseRoom(bot);
                float interest = target == NpcLeadingRoom.RoomId ? .3f + .4f * mind.Activity : mind.JoinChance;
                if (target > 0 && random.NextDouble() < settings.joinProbability * interest)
                { Join(bot,target); }
            }
            else if (mind.Room > 0 && !SocialReady)
            {
                var currentRoom = GetRoom(mind.Room);
                var command = currentRoom.Command;
                bool idle = currentRoom.PlayerOwned ? command == null || command.Canceled : NpcLeadingRoom.Instance == null || !NpcLeadingRoom.Instance.HasSignal;
                float boredom = idle ? Mathf.Clamp01((now - mind.JoinedRoomAt - 90f) / 180f) * (1f - Mathf.Clamp01(mind.Trust / 100f)) : 0f;
                if (currentRoom.OwnerBotId != bot.Social.Id && random.NextDouble() < settings.leaveProbability * mind.LeaveChance + settings.idleRoomLeaveProbability * boredom)
                { Leave(bot, idle ? "다른 종목과 대화를 보러 이동" : "관찰한 위험과 자신의 경험을 재평가"); }
            }
        }
        if (mind.Room > 0)
        { Orders?.Consider(bot, GetRoom(mind.Room).Command, random.NextDouble(), now); }
        if (SocialReady)
        { return; }
        if (now < mind.NextSpeech || now - bot.Social.LastSpeech < settings.speechCooldown)
        { return; }
        if (mind.Latest.HasValue && mind.LastSharedExperience != mind.Latest.Value.EventId && random.NextDouble() < .25f + mind.Activity * .35f)
        {
            var experience = mind.Latest.Value;
            int room = random.NextDouble() < .45 ? 0 : mind.Room;
            Speak(bot, room, InvestorConversation.Testimony(bot, experience, false), Context(bot, experience, room));
            mind.LastSharedExperience = experience.EventId;
        }
        else if (mind.Room > 0 && now >= GetRoom(mind.Room).NextConversation && random.NextDouble() < .25f * mind.Activity)
        {
            string text = InvestorConversation.Waiting(bot, Orders?.Get(bot), random.Next());
            if (mind.LastWitnessRoom == mind.Room && now - mind.LastRoomWitnessAt < 45f && random.NextDouble() < .3f)
            { text = mind.LastRoomWitness.Nickname + "님, " + text; }
            Speak(bot, mind.Room, text, default, ChatEvent.SmallTalk);
        }
        else if (mind.LatestObservation.HasValue && mind.LastSharedObservation != mind.LatestObservation.Value.EventId && random.NextDouble() < .2f + mind.Activity * .2f)
        {
            var observation = mind.LatestObservation.Value;
            var context = new InvestorMessageContext(observation.EventId, mind.Room, 1f, .75f, mind.Trust, false);
            Speak(bot, mind.Room, InvestorConversation.MissedForecast(bot, observation), context);
            mind.LastSharedObservation = observation.EventId;
        }
    }
```

### Assets/Scripts/NightShift/InvestorSociety.cs · Join

원본:

```csharp
public bool Join(TradeBot bot, int roomId)
    {
        var room = GetRoom(roomId); var mind = bot.Social.Psychology;
        if (room != null && (room.Social.banned.Contains(bot.Social.Id) || mind.RejoinAt.TryGetValue(roomId, out double allowedAt) && SocialTime.GameMinute < allowedAt))
        { return false; }
        if (room == null || !IsRoomOpen(roomId) || mind.Room != 0 || room.Members.Count >= settings.roomCapacity || !bot.isActiveAndEnabled || bot.IsExchangeParticipant)
        { return false; }
        mind.Room = roomId; mind.JoinedRoomAt = Time.time; room.Members.Add(bot.Social.Id); Joins++;
        mind.LastWitnessRoom = 0;
        mind.LastRead[roomId] = room.Version; // Pre-join private messages were not witnessed.
        PostRoomNotice(roomId, ChatRoomNotice.Join, bot.Social.Nickname + " 님이 입장했습니다.");
        if (Time.time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
        { Speak(bot, roomId, InvestorConversation.Greeting(bot), default, ChatEvent.SmallTalk); }
        Trace(bot, roomId + "번 방 참여"); return true;
    }
```

수정본:

```csharp
public bool Join(TradeBot bot, int roomId, bool playerInvited = false)
    {
        if (bot?.Social == null) { return false; }
        var room = GetRoom(roomId); var mind = bot.Social.Psychology;
        if (room != null && (room.Social.banned.Contains(bot.Social.Id) || mind.RejoinAt.TryGetValue(roomId, out double allowedAt) && SocialTime.GameMinute < allowedAt))
        { return false; }
        if (room == null || !IsRoomOpen(roomId) || mind.Room != 0 || room.Members.Count >= settings.roomCapacity || !bot.isActiveAndEnabled || bot.IsExchangeParticipant)
        { return false; }
        if (room.PlayerOwned && !playerInvited && !CanDiscoverPlayerRoom(bot, roomId))
        { return false; }
        if (room.PlayerOwned && playerInvited)
        { room.Social.firstMemberInvited = true; mind.InvitedPlayerRooms |= 1 << (roomId - 1); }
        mind.Room = roomId; mind.JoinedRoomAt = Time.time; room.Members.Add(bot.Social.Id); Joins++;
        mind.LastWitnessRoom = 0;
        mind.LastRead[roomId] = room.Version; // Pre-join private messages were not witnessed.
        PostRoomNotice(roomId, ChatRoomNotice.Join, bot.Social.Nickname + " 님이 입장했습니다.");
        if (Time.time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
        { Speak(bot, roomId, InvestorConversation.Greeting(bot), default, ChatEvent.SmallTalk); }
        Trace(bot, roomId + "번 방 참여"); return true;
    }
```

### Assets/Scripts/NightShift/InvestorSociety.cs · Invite

원본:

```csharp
public bool Invite(int botId, int roomId)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        day ??= MarketDayController.Instance;
        var bot = day == null ? null : day.FindBot(botId);
        if (bot == null || bot.Social == null || !MarketDayController.CanTrade || GetRoom(roomId) == null || !GetRoom(roomId).PlayerOwned)
        { LastAction = "참여자와 리딩방을 선택하세요."; return false; }
        var mind = bot.Social.Psychology;
        if (Time.time < nextContact || Time.time - mind.LastContact < settings.repeatContactCooldown)
        { LastAction = "아직 다시 연락할 수 없습니다."; return false; }
        if (mind.Room != 0)
        { LastAction = "이미 " + mind.Room + "번 방에 참여 중입니다."; return false; }
        if (GetRoom(roomId).Members.Count >= settings.roomCapacity)
        { LastAction = "방의 정원이 가득 찼습니다."; return false; }
        nextContact = Time.time + settings.contactCooldown; mind.LastContact = Time.time;
        mind.RecallBetrayal(); Trace(bot, "회유: " + mind.LastCause);
        bool accepted = random.NextDouble() < mind.JoinChance && Join(bot, roomId);
        LastAction = accepted ? bot.Social.Nickname + ": " + roomId + "번 방에 참여했습니다. 명령 수락은 별도 판단합니다." : bot.Social.Nickname + ": 참여 제안을 거절했습니다.";
        return accepted;
    }
```

수정본:

```csharp
public bool Invite(int botId, int roomId)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        day ??= MarketDayController.Instance;
        var bot = day == null ? null : day.FindBot(botId);
        if (bot == null || bot.Social == null || !MarketDayController.CanTrade || GetRoom(roomId) == null || !GetRoom(roomId).PlayerOwned || !IsRoomOpen(roomId))
        { LastAction = "참여자와 리딩방을 선택하세요."; return false; }
        var mind = bot.Social.Psychology;
        bool firstMember = !GetRoom(roomId).Social.firstMemberInvited;
        if (!firstMember && (Time.time < nextContact || Time.time - mind.LastContact < settings.repeatContactCooldown))
        { LastAction = "아직 다시 연락할 수 없습니다."; return false; }
        if (mind.Room != 0)
        { LastAction = "이미 " + GetRoom(mind.Room).DisplayName + "에 참여 중입니다."; return false; }
        if (GetRoom(roomId).Members.Count >= settings.roomCapacity)
        { LastAction = "방의 정원이 가득 찼습니다."; return false; }
        if (!bot.isActiveAndEnabled || bot.IsExchangeParticipant || GetRoom(roomId).Social.banned.Contains(botId) ||
            mind.RejoinAt.TryGetValue(roomId, out double returnAt) && SocialTime.GameMinute < returnAt)
        { LastAction = "지금은 이 참여자를 초대할 수 없습니다."; return false; }
        nextContact = Time.time + settings.contactCooldown; mind.LastContact = Time.time;
        mind.RecallBetrayal(); Trace(bot, "회유: " + mind.LastCause);
        bool accepted = (firstMember || random.NextDouble() < mind.JoinChance) && Join(bot, roomId, playerInvited: true);
        LastAction = accepted ? bot.Social.Nickname + ": 내 리딩방에 참여했습니다. 명령 수락은 별도 판단합니다." : bot.Social.Nickname + ": 참여 제안을 거절했습니다.";
        return accepted;
    }
```

### Assets/Scripts/NightShift/InvestorSociety.cs · Issue

원본:

```csharp
public bool Issue(int roomId, Stock stock, decimal buy, decimal sell)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned || !MarketDayController.CanTrade || stock == null || !stock.AcceptsOrders || buy < 1m || sell <= buy || buy != PriceTickPolicy.RoundToTick(buy) || sell != PriceTickPolicy.RoundToTick(sell))
        { LastAction = "거래 가능한 종목과 올바른 원화 호가를 입력하세요. 매도가는 매수가보다 커야 합니다."; return false; }
        CancelRoom(roomId);
        room.Command = new LeadingRoomOrders.Command { Id = ++nextCommand, Room = roomId, Stock = stock, Buy = buy, Sell = sell, IssuedAt = Time.time, IssuedDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day };
        LastAction = $"{roomId}번 방: {stock.stockName} 매수 ≤ {buy:N0} / 매도 ≥ {sell:N0}. 참가자가 개별 판단합니다.";
        Speak(null, roomId, LastAction, default); return true;
    }
```

수정본:

```csharp
public bool Issue(int roomId, Stock stock, decimal buy, decimal sell)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned || !IsRoomOpen(roomId) || !MarketDayController.CanTrade || stock == null || !stock.AcceptsOrders || buy < 1m || sell <= buy || buy != PriceTickPolicy.RoundToTick(buy) || sell != PriceTickPolicy.RoundToTick(sell))
        { LastAction = "거래 가능한 종목과 올바른 원화 호가를 입력하세요. 매도가는 매수가보다 커야 합니다."; return false; }
        CancelRoom(roomId);
        room.Command = new LeadingRoomOrders.Command { Id = ++nextCommand, Room = roomId, Stock = stock, Buy = buy, Sell = sell, IssuedAt = Time.time, IssuedDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day };
        LastAction = $"{room.DisplayName}: {stock.stockName} 매수 ≤ {buy:N0} / 매도 ≥ {sell:N0}. 참가자가 개별 판단합니다.";
        Speak(null, roomId, LastAction, default); return true;
    }
```

### Assets/Scripts/NightShift/InvestorSociety.cs · CancelRoom

원본:

```csharp
public void CancelRoom(int roomId)
    {
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned)
        { return; }
        if (room.Command != null) { room.Command.Canceled = true; }
        Orders?.CancelRoom(roomId);
        LastAction = roomId + "번 방 지시 취소. 체결된 주식은 각 참가자에게 남습니다.";
    }
```

수정본:

```csharp
public void CancelRoom(int roomId)
    {
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned)
        { return; }
        if (room.Command != null) { room.Command.Canceled = true; }
        Orders?.CancelRoom(roomId);
        LastAction = room.DisplayName + " 지시 취소. 체결된 주식은 각 참가자에게 남습니다.";
    }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs · Start

원본:

```csharp
void Start()
    {
        publicRoom.onClick.AddListener(Public); roomOne.onClick.AddListener(One);
        roomTwo.onClick.AddListener(Two); roomThree.onClick.AddListener(Three);
        invite.onClick.AddListener(Invite); issue.onClick.AddListener(Issue); cancel.onClick.AddListener(Cancel);
        if (growth != null) { growth.onClick.AddListener(ShowGrowth); }
        if (npcRoom != null)
        {
            npcRoom.onClick.AddListener(ShowNpc); npcMembership.onClick.AddListener(Membership);
            npcBuy.onClick.AddListener(NpcBuy); npcSell.onClick.AddListener(NpcSell);
        }
    }
```

수정본:

```csharp
void Start()
    {
        ConfigureRoomTabs();
        publicRoom.onClick.AddListener(Public); roomOne.onClick.AddListener(One);
        roomTwo.onClick.AddListener(Two); roomThree.onClick.AddListener(Three);
        invite.onClick.AddListener(Invite); issue.onClick.AddListener(Issue); cancel.onClick.AddListener(Cancel);
        inviteLabel = invite.GetComponentInChildren<TextMeshProUGUI>();
        if (growth != null) { growth.onClick.AddListener(ShowGrowth); }
        if (npcRoom != null)
        {
            npcRoom.onClick.AddListener(ShowNpc); npcMembership.onClick.AddListener(Membership);
            npcBuy.onClick.AddListener(NpcBuy); npcSell.onClick.AddListener(NpcSell);
        }
    }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs · One

원본:

```csharp
private void One() { chat.SelectRoom(1); }
```

수정본:

```csharp
private void One() { ShowPlayerRoom(1); }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs · Two

원본:

```csharp
private void Two() { chat.SelectRoom(2); }
```

수정본:

```csharp
private void Two() { ShowPlayerRoom(2); }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs · Three

원본:

```csharp
private void Three() { chat.SelectRoom(3); }
```

수정본:

```csharp
private void Three() { ShowPlayerRoom(3); }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs · Invite

원본:

```csharp
private void Invite() { SocialContactController.Instance?.Contact(chat.SelectedBotId, chat.SelectedRoom > 0 ? chat.SelectedRoom : 1); }
```

수정본:

```csharp
private void Invite()
    {
        int roomId = chat.SelectedRoom > 0 ? chat.SelectedRoom : 1;
        int candidate = chat.SelectedBotId > 0 ? chat.SelectedBotId : invitationBot;
        bool joined = SocialContactController.Instance != null && SocialContactController.Instance.Contact(candidate, roomId);
        if (joined) { invitationBot = 0; chat.SelectRoom(roomId); }
        inviteFeedbackUntil = Time.unscaledTime + 6f;
        nextUpdate = 0f;
    }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs · Update

원본:

```csharp
void Update()
    {
        if (Time.unscaledTime < nextUpdate || StockManager.Instance == null || InvestorSociety.Instance == null)
        { return; }
        nextUpdate = Time.unscaledTime + .5f;
        var stock = StockManager.Instance.CurrentStock;
        if (selected != stock && stock != null && !buyPrice.isFocused && !sellPrice.isFocused)
        {
            selected = stock;
            buyPrice.SetTextWithoutNotify(stock.currentPrice.ToString("0", CultureInfo.InvariantCulture));
            sellPrice.SetTextWithoutNotify(System.Math.Max(PriceTickPolicy.OneTickAbove(stock.currentPrice), PriceTickPolicy.RoundToTick(stock.currentPrice * 1.08m)).ToString("0", CultureInfo.InvariantCulture));
        }
        var room = InvestorSociety.Instance.GetRoom(chat.SelectedRoom);
        bool operate = PlayerProgression.Instance == null || PlayerProgression.Instance.CanOperateRooms;
        roomOne.interactable = roomTwo.interactable = roomThree.interactable = operate;
        bool npcSelected = room != null && !room.PlayerOwned;
        bool ownSelected = room != null && room.PlayerOwned;
        if (playerControls != null)
        { playerControls.SetActive(!npcSelected && (lobbyControls == null || ownSelected && operate)); npcControls.SetActive(npcSelected); }
        if (lobbyControls != null)
        {
            lobbyControls.SetActive(!npcSelected && (!ownSelected || !operate));
            Set(lobbyStatus,operate ? InvestorSociety.Instance.LastAction : "내 리딩방 · 개설 전");
        }
        if (npcSelected && npcControls != null)
        {
            NpcDetails(); invite.interactable = false; return;
        }
        string title = room == null ? "공용 라운지 · 메시지를 눌러 참여자 선택" : $"리딩방 {room.Id} · 참여 {room.Members.Count}명";
        if (room != null && room.PlayerOwned) { title += $" · 경각심 {room.Social.discipline:0} · 위축 {room.Social.chill:0}"; }
        int waiting = 0, holding = 0, deferred = 0, rejected = 0, completed = 0, canceled = 0;
        if (InvestorSociety.Instance.Orders != null && room != null)
        {
            foreach (var plan in InvestorSociety.Instance.Orders.Plans)
            {
                if (plan.Command != room.Command)
                { continue; }
                if (plan.State == LeadingRoomOrders.Stage.Completed) { completed++; }
                else if (plan.State == LeadingRoomOrders.Stage.Canceled) { canceled++; }
                else if (plan.State == LeadingRoomOrders.Stage.Deferred) { deferred++; }
                else if (plan.State == LeadingRoomOrders.Stage.Rejected) { rejected++; }
                else if (plan.Remaining > 0m) { holding++; }
                else if (plan.State == LeadingRoomOrders.Stage.AwaitBuy || plan.State == LeadingRoomOrders.Stage.Buying) { waiting++; }
            }
        }
        string active = room?.Command == null || room.Command.Canceled ? "활성 지시 없음" : $"{room.Command.Stock.stockName}  매수 ≤ {room.Command.Buy:N0} / 매도 ≥ {room.Command.Sell:N0}";
        Set(commandStatus, title + "\n" + active + $"\n대기 {waiting} · 보유 {holding} · 보류 {deferred} · 거부 {rejected}\n완료 {completed} · 취소 {canceled}");
        Set(feedback, operate ? InvestorSociety.Instance.LastAction : "관전 가능 · 태블릿 2의 성장·권한에서 리딩방을 개설하세요.\n" + PlayerProgression.Instance.LockReason);
        issue.interactable = operate && room != null && MarketDayController.CanTrade;
        cancel.interactable = room?.Command != null && !room.Command.Canceled;
        invite.interactable = operate && chat.SelectedBotId > 0 && MarketDayController.CanTrade;
    }
```

수정본:

```csharp
void Update()
    {
        if (Time.unscaledTime < nextUpdate || StockManager.Instance == null || InvestorSociety.Instance == null)
        { return; }
        nextUpdate = Time.unscaledTime + .5f;
        if (chat.SelectedRoom == 0 && chat.SelectedBotId > 0) { invitationBot = chat.SelectedBotId; }
        var stock = StockManager.Instance.CurrentStock;
        if (selected != stock && stock != null && !buyPrice.isFocused && !sellPrice.isFocused)
        {
            selected = stock;
            buyPrice.SetTextWithoutNotify(stock.currentPrice.ToString("0", CultureInfo.InvariantCulture));
            sellPrice.SetTextWithoutNotify(System.Math.Max(PriceTickPolicy.OneTickAbove(stock.currentPrice), PriceTickPolicy.RoundToTick(stock.currentPrice * 1.08m)).ToString("0", CultureInfo.InvariantCulture));
        }
        var room = InvestorSociety.Instance.GetRoom(chat.SelectedRoom);
        bool operate = PlayerProgression.Instance == null || PlayerProgression.Instance.CanOperateRooms;
        roomOne.interactable = operate;
        roomTwo.interactable = roomThree.interactable = false;
        bool npcSelected = room != null && !room.PlayerOwned;
        bool ownSelected = room != null && room.PlayerOwned;
        if (playerControls != null)
        { playerControls.SetActive(!npcSelected && (lobbyControls == null || ownSelected && operate)); npcControls.SetActive(npcSelected); }
        if (lobbyControls != null)
        {
            lobbyControls.SetActive(!npcSelected && (!ownSelected || !operate));
            Set(lobbyStatus,operate ? InvestorSociety.Instance.InvitedRoomMask == 0 && Time.unscaledTime >= inviteFeedbackUntil ? "첫 회원 초대 · 닉네임 선택 → 내 리딩방" : InvestorSociety.Instance.LastAction : "내 리딩방 · 개설 전");
        }
        if (npcSelected && npcControls != null)
        {
            NpcDetails(); invite.interactable = false; return;
        }
        string title = room == null ? "공용 라운지 · 메시지를 눌러 참여자 선택" : $"내 리딩방 · 참여 {room.Members.Count}명";
        if (room != null && room.PlayerOwned) { title += $" · 경각심 {room.Social.discipline:0} · 위축 {room.Social.chill:0}"; }
        int waiting = 0, holding = 0, deferred = 0, rejected = 0, completed = 0, canceled = 0;
        if (InvestorSociety.Instance.Orders != null && room != null)
        {
            foreach (var plan in InvestorSociety.Instance.Orders.Plans)
            {
                if (plan.Command != room.Command)
                { continue; }
                if (plan.State == LeadingRoomOrders.Stage.Completed) { completed++; }
                else if (plan.State == LeadingRoomOrders.Stage.Canceled) { canceled++; }
                else if (plan.State == LeadingRoomOrders.Stage.Deferred) { deferred++; }
                else if (plan.State == LeadingRoomOrders.Stage.Rejected) { rejected++; }
                else if (plan.Remaining > 0m) { holding++; }
                else if (plan.State == LeadingRoomOrders.Stage.AwaitBuy || plan.State == LeadingRoomOrders.Stage.Buying) { waiting++; }
            }
        }
        string active = room?.Command == null || room.Command.Canceled ? "활성 지시 없음" : $"{room.Command.Stock.stockName}  매수 ≤ {room.Command.Buy:N0} / 매도 ≥ {room.Command.Sell:N0}";
        Set(commandStatus, title + "\n" + active + $"\n대기 {waiting} · 보유 {holding} · 보류 {deferred} · 거부 {rejected}\n완료 {completed} · 취소 {canceled}");
        Set(feedback, operate ? InvestorSociety.Instance.LastAction : "관전 가능 · 태블릿 2의 성장·권한에서 리딩방을 개설하세요.\n" + PlayerProgression.Instance.LockReason);
        var invitationRoom = room ?? InvestorSociety.Instance.GetRoom(1);
        bool firstInvite = invitationRoom.PlayerOwned && !invitationRoom.Social.firstMemberInvited;
        if (inviteLabel != null) { Set(inviteLabel, firstInvite ? "첫 회원 초대" : "초대"); }
        if (operate && firstInvite && Time.unscaledTime >= inviteFeedbackUntil)
        {
            var candidate = MarketDayController.Instance?.FindBot(invitationBot);
            Set(feedback, candidate == null ? "공용 라운지에서 닉네임 선택 → 내 리딩방 → 첫 회원 초대" : candidate.Social.Nickname + "님을 직접 초대하세요.\n첫 초대는 확정 가입 · 투자 지시는 각자 판단");
        }
        issue.interactable = operate && room != null && MarketDayController.CanTrade;
        cancel.interactable = room?.Command != null && !room.Command.Canceled;
        invite.interactable = operate && (chat.SelectedBotId > 0 || invitationBot > 0) && MarketDayController.CanTrade;
    }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs · ConfigureRoomTabs

원본:

```csharp
// 새 함수
```

수정본:

```csharp
private void ConfigureRoomTabs()
    {
        if (roomTwo != null) { roomTwo.gameObject.SetActive(false); }
        if (roomThree != null) { roomThree.gameObject.SetActive(false); }
        var tabs = npcRoom == null ? new[] { publicRoom, roomOne } : new[] { publicRoom, npcRoom, roomOne };
        float width = (.95f - .025f * (tabs.Length - 1)) / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            var rect = (RectTransform)tabs[i].transform;
            float x = .025f + i * (width + .025f);
            rect.anchorMin = new Vector2(x, rect.anchorMin.y); rect.anchorMax = new Vector2(x + width, rect.anchorMax.y);
            rect.offsetMin = new Vector2(0, rect.offsetMin.y); rect.offsetMax = new Vector2(0, rect.offsetMax.y);
        }
        var label = roomOne.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) { label.text = "내 리딩방"; }
    }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs · ShowPlayerRoom

원본:

```csharp
// 새 함수
```

수정본:

```csharp
private void ShowPlayerRoom(int room)
    {
        if (chat.SelectedRoom == 0 && chat.SelectedBotId > 0) { invitationBot = chat.SelectedBotId; }
        chat.SelectRoom(room); nextUpdate = 0f;
    }
```

### Assets/Scripts/NightShift/ChatSocialData.cs · CaptureSocial

원본:

```csharp
public SocialSnapshot CaptureSocial()
    {
        var relations = new List<SocialRelation>(); foreach (var row in Relations.Values) { relations.Add(CopyRelation(row)); }
        relations.Sort((a, b) => a.to.CompareTo(b.to));
        var memories = new List<SocialMemory>(); foreach (var row in Memories) { memories.Add(CopyMemory(row)); }
        var lots = new List<FomoLotSnapshot>();
        foreach (var row in FomoLots.Values) { lots.Add(new FomoLotSnapshot { symbol = row.symbol, quantity = row.quantity.ToString(CultureInfo.InvariantCulture), cost = row.cost.ToString(CultureInfo.InvariantCulture), realized = row.realized.ToString(CultureInfo.InvariantCulture) }); }
        lots.Sort((a, b) => string.CompareOrdinal(a.symbol, b.symbol));
        return new SocialSnapshot { id = SocialId, room = Room, greed = Greed, fear = Fear, trust = Trust, betrayal = Betrayal,
            fomo = 0, fomoSymbol = FomoSymbol, fomoUntil = FomoUntil, fomoCooldownUntil = FomoCooldownUntil, quadrant = Quadrant, randomState = SocialRng.State,
            traits = Traits, relations = relations.ToArray(), memories = memories.ToArray(), fomoLots = lots.ToArray(), rejoin = SaveCooldowns(RejoinAt), mutes = SaveCooldowns(MutedUntil),
            promisedAction = PromisedAction, promisedSymbol = PromisedSymbol, promiseMessage = PromiseMessage,
            lastQuadrantReview = LastQuadrantReview, lastFomoObservation = LastFomoObservation, lastRelationshipSpeech = LastRelationshipSpeech };
    }
```

수정본:

```csharp
public SocialSnapshot CaptureSocial()
    {
        var relations = new List<SocialRelation>(); foreach (var row in Relations.Values) { relations.Add(CopyRelation(row)); }
        relations.Sort((a, b) => a.to.CompareTo(b.to));
        var memories = new List<SocialMemory>(); foreach (var row in Memories) { memories.Add(CopyMemory(row)); }
        var lots = new List<FomoLotSnapshot>();
        foreach (var row in FomoLots.Values) { lots.Add(new FomoLotSnapshot { symbol = row.symbol, quantity = row.quantity.ToString(CultureInfo.InvariantCulture), cost = row.cost.ToString(CultureInfo.InvariantCulture), realized = row.realized.ToString(CultureInfo.InvariantCulture) }); }
        lots.Sort((a, b) => string.CompareOrdinal(a.symbol, b.symbol));
        return new SocialSnapshot { id = SocialId, room = Room, greed = Greed, fear = Fear, trust = Trust, betrayal = Betrayal,
            fomo = 0, fomoSymbol = FomoSymbol, fomoUntil = FomoUntil, fomoCooldownUntil = FomoCooldownUntil, quadrant = Quadrant, randomState = SocialRng.State,
            traits = Traits, relations = relations.ToArray(), memories = memories.ToArray(), fomoLots = lots.ToArray(), rejoin = SaveCooldowns(RejoinAt), mutes = SaveCooldowns(MutedUntil),
            knownPlayerRooms = SaveCooldowns(KnownPlayerRooms), invitedPlayerRooms = InvitedPlayerRooms,
            promisedAction = PromisedAction, promisedSymbol = PromisedSymbol, promiseMessage = PromiseMessage,
            lastQuadrantReview = LastQuadrantReview, lastFomoObservation = LastFomoObservation, lastRelationshipSpeech = LastRelationshipSpeech };
    }
```

### Assets/Scripts/NightShift/ChatSocialData.cs · RestoreSocial

원본:

```csharp
public bool RestoreSocial(SocialSnapshot saved)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.id != SocialId ||
            !Finite(saved.trust) || !Finite(saved.betrayal) || !Finite(saved.greed) || !Finite(saved.fear) || !Finite(saved.fomoUntil) || !Finite(saved.fomoCooldownUntil) ||
            !Finite(saved.lastQuadrantReview) || !Finite(saved.lastFomoObservation) || !Finite(saved.lastRelationshipSpeech) || saved.traits != null && !saved.traits.Valid)
        { return false; }
        var lots = new Dictionary<string, FomoLot>();
        if (saved.fomoLots != null)
        {
            foreach (var row in saved.fomoLots)
            {
                if (row == null || string.IsNullOrEmpty(row.symbol) || lots.ContainsKey(row.symbol) ||
                    !decimal.TryParse(row.quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal quantity) || quantity < 0 ||
                    !decimal.TryParse(row.cost, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal cost) || cost < 0 ||
                    !decimal.TryParse(row.realized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal realized)) { return false; }
                lots[row.symbol] = new FomoLot { symbol = row.symbol, quantity = quantity, cost = cost, realized = realized };
            }
        }
        if (saved.relations != null) { foreach (var row in saved.relations) { if (row != null && (!Finite(row.anger) || !Finite(row.affinity) || !Finite(row.lastInteractionAt) || !Finite(row.lastWarningAt))) { return false; } } }
        if (saved.memories != null) { foreach (var row in saved.memories) { if (row != null && (!Finite(row.at) || row.usedCount < 0)) { return false; } } }
        foreach (var group in new[] { saved.rejoin, saved.mutes }) { if (group != null) { foreach (var row in group) { if (row == null || row.room < 1 || row.room > InvestorSociety.MaximumRoomId || !Finite(row.until)) { return false; } } } }
        if (!Enum.IsDefined(typeof(SocialPromisedAction), saved.promisedAction) || saved.promisedAction != SocialPromisedAction.None && string.IsNullOrEmpty(saved.promisedSymbol)) { return false; }
        Greed = saved.greed; Fear = saved.fear; Trust = saved.trust; Betrayal = saved.betrayal;
        PromisedAction = saved.promisedAction; PromisedSymbol = saved.promisedSymbol; PromiseMessage = saved.promiseMessage;
        Quadrant = saved.quadrant; FomoSymbol = saved.version >= 2 ? saved.fomoSymbol : null;
        Room = Math.Max(0, Math.Min(InvestorSociety.MaximumRoomId, saved.room));
        FomoUntil = saved.version >= 2 ? Math.Max(0, saved.fomoUntil) : 0;
        FomoCooldownUntil = saved.version >= 2 ? Math.Max(0, saved.fomoCooldownUntil) : 0;
        LastQuadrantReview = saved.lastQuadrantReview; LastFomoObservation = saved.lastFomoObservation; LastRelationshipSpeech = saved.lastRelationshipSpeech;
        FomoLots.Clear(); foreach (var row in lots) { FomoLots[row.Key] = row.Value; }
        RejoinAt.Clear(); MutedUntil.Clear();
        if (saved.rejoin != null) { foreach (var row in saved.rejoin) { RejoinAt[row.room] = row.until; } }
        if (saved.mutes != null) { foreach (var row in saved.mutes) { MutedUntil[row.room] = row.until; } }
        if (saved.traits != null) { saved.traits.MigrateSpeech(Traits); Traits = saved.traits; }
        if (saved.randomState != 0) { SocialRng.Restore(saved.randomState); }
        Relations.Clear(); Memories.Clear();
        if (saved.relations != null)
        {
            foreach (var row in saved.relations)
            {
                if (row == null || row.to == SocialId) { continue; }
                Relations[row.to] = CopyRelation(row);
            }
        }
        if (saved.memories != null) { foreach (var memory in saved.memories) { if (memory != null) { Remember(CopyMemory(memory)); } } }
        Clamp(); AuditEmotion("restore"); return true;
    }
```

수정본:

```csharp
public bool RestoreSocial(SocialSnapshot saved)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.id != SocialId ||
            !Finite(saved.trust) || !Finite(saved.betrayal) || !Finite(saved.greed) || !Finite(saved.fear) || !Finite(saved.fomoUntil) || !Finite(saved.fomoCooldownUntil) ||
            !Finite(saved.lastQuadrantReview) || !Finite(saved.lastFomoObservation) || !Finite(saved.lastRelationshipSpeech) || saved.traits != null && !saved.traits.Valid)
        { return false; }
        var lots = new Dictionary<string, FomoLot>();
        if (saved.fomoLots != null)
        {
            foreach (var row in saved.fomoLots)
            {
                if (row == null || string.IsNullOrEmpty(row.symbol) || lots.ContainsKey(row.symbol) ||
                    !decimal.TryParse(row.quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal quantity) || quantity < 0 ||
                    !decimal.TryParse(row.cost, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal cost) || cost < 0 ||
                    !decimal.TryParse(row.realized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal realized)) { return false; }
                lots[row.symbol] = new FomoLot { symbol = row.symbol, quantity = quantity, cost = cost, realized = realized };
            }
        }
        if (saved.relations != null) { foreach (var row in saved.relations) { if (row != null && (!Finite(row.anger) || !Finite(row.affinity) || !Finite(row.lastInteractionAt) || !Finite(row.lastWarningAt))) { return false; } } }
        if (saved.memories != null) { foreach (var row in saved.memories) { if (row != null && (!Finite(row.at) || row.usedCount < 0)) { return false; } } }
        foreach (var group in new[] { saved.rejoin, saved.mutes }) { if (group != null) { foreach (var row in group) { if (row == null || row.room < 1 || row.room > InvestorSociety.MaximumRoomId || !Finite(row.until)) { return false; } } } }
        if (saved.invitedPlayerRooms < 0 || saved.invitedPlayerRooms >= 1 << InvestorSociety.PlayerRoomCount) { return false; }
        if (saved.knownPlayerRooms != null) { foreach (var row in saved.knownPlayerRooms) { if (row == null || row.room < 1 || row.room > InvestorSociety.PlayerRoomCount || !Finite(row.until)) { return false; } } }
        if (!Enum.IsDefined(typeof(SocialPromisedAction), saved.promisedAction) || saved.promisedAction != SocialPromisedAction.None && string.IsNullOrEmpty(saved.promisedSymbol)) { return false; }
        Greed = saved.greed; Fear = saved.fear; Trust = saved.trust; Betrayal = saved.betrayal;
        PromisedAction = saved.promisedAction; PromisedSymbol = saved.promisedSymbol; PromiseMessage = saved.promiseMessage;
        Quadrant = saved.quadrant; FomoSymbol = saved.version >= 2 ? saved.fomoSymbol : null;
        Room = Math.Max(0, Math.Min(InvestorSociety.MaximumRoomId, saved.room));
        if (Room > InvestorSociety.AvailablePlayerRoomCount && Room <= InvestorSociety.PlayerRoomCount) { Room = 0; }
        FomoUntil = saved.version >= 2 ? Math.Max(0, saved.fomoUntil) : 0;
        FomoCooldownUntil = saved.version >= 2 ? Math.Max(0, saved.fomoCooldownUntil) : 0;
        LastQuadrantReview = saved.lastQuadrantReview; LastFomoObservation = saved.lastFomoObservation; LastRelationshipSpeech = saved.lastRelationshipSpeech;
        FomoLots.Clear(); foreach (var row in lots) { FomoLots[row.Key] = row.Value; }
        RejoinAt.Clear(); MutedUntil.Clear();
        KnownPlayerRooms.Clear(); InvitedPlayerRooms = saved.invitedPlayerRooms;
        if (saved.knownPlayerRooms != null) { foreach (var row in saved.knownPlayerRooms) { KnownPlayerRooms[row.room] = row.until; } }
        if (saved.rejoin != null) { foreach (var row in saved.rejoin) { RejoinAt[row.room] = row.until; } }
        if (saved.mutes != null) { foreach (var row in saved.mutes) { MutedUntil[row.room] = row.until; } }
        if (saved.traits != null) { saved.traits.MigrateSpeech(Traits); Traits = saved.traits; }
        if (saved.randomState != 0) { SocialRng.Restore(saved.randomState); }
        Relations.Clear(); Memories.Clear();
        if (saved.relations != null)
        {
            foreach (var row in saved.relations)
            {
                if (row == null || row.to == SocialId) { continue; }
                Relations[row.to] = CopyRelation(row);
            }
        }
        if (saved.memories != null) { foreach (var memory in saved.memories) { if (memory != null) { Remember(CopyMemory(memory)); } } }
        Clamp(); AuditEmotion("restore"); return true;
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · OpenLeadingRooms

원본:

```csharp
public bool OpenLeadingRooms()
    {
        if (CanOperateRooms)
        { return true; }
        if (!Eligible)
        { return false; }
        CanOperateRooms = true;
        MarketEventFeed.Instance?.Notify("리딩방 운영을 시작했습니다. 참가자는 실제 수익과 경험으로 당신을 판단합니다.");
        return true;
    }
```

수정본:

```csharp
public bool OpenLeadingRooms()
    {
        if (CanOperateRooms)
        { return true; }
        if (!Eligible)
        { return false; }
        CanOperateRooms = true;
        MarketEventFeed.Instance?.Notify("리딩방 개설 완료 · 공용 라운지에서 첫 회원을 직접 초대하세요.");
        return true;
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · Capture

원본:

```csharp
public string Capture()
    {
        return JsonUtility.ToJson(new Snapshot { worldId = WorldId, turnover = Turnover.ToString(CultureInfo.InvariantCulture), realizedProfit = RealizedProfit.ToString(CultureInfo.InvariantCulture), roomsOpened = CanOperateRooms });
    }
```

수정본:

```csharp
public string Capture()
    {
        return JsonUtility.ToJson(new Snapshot { worldId = WorldId, turnover = Turnover.ToString(CultureInfo.InvariantCulture), realizedProfit = RealizedProfit.ToString(CultureInfo.InvariantCulture), roomsOpened = CanOperateRooms,
            invitedRoomMask = InvestorSociety.Instance?.InvitedRoomMask ?? 0 });
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · Restore

원본:

```csharp
public bool Restore(string json, string restoredWorldId)
    {
        // A progress fragment is not a world save. Never grant a new economy the previous world's rights.
        Snapshot saved;
        try { saved = JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return false; }
        if (saved == null || saved.version != 1 || saved.worldId != WorldId || restoredWorldId != WorldId ||
            !decimal.TryParse(saved.turnover, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal turnover) || turnover < 0m ||
            !decimal.TryParse(saved.realizedProfit, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal profit))
        { return false; }
        Turnover = turnover; RealizedProfit = profit; CanOperateRooms = saved.roomsOpened;
        return true;
    }
```

수정본:

```csharp
public bool Restore(string json, string restoredWorldId)
    {
        // A progress fragment is not a world save. Never grant a new economy the previous world's rights.
        Snapshot saved;
        try { saved = JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return false; }
        if (saved == null || saved.version != 1 || saved.worldId != WorldId || restoredWorldId != WorldId || saved.invitedRoomMask < 0 || saved.invitedRoomMask >= 1 << InvestorSociety.PlayerRoomCount ||
            !decimal.TryParse(saved.turnover, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal turnover) || turnover < 0m ||
            !decimal.TryParse(saved.realizedProfit, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal profit))
        { return false; }
        Turnover = turnover; RealizedProfit = profit; CanOperateRooms = saved.roomsOpened;
        InvestorSociety.Instance?.RestoreInvitedRooms(saved.invitedRoomMask);
        return true;
    }
```

### Assets/Scripts/ChartManager.cs · ConfigureHeaderLayout

원본:

```csharp
public void ConfigureHeaderLayout()
    {
        if (textName == null || textName.transform.parent is not RectTransform header)
        { return; }
        PlaceHeaderText(textName,header,.025f,.055f,.30f,.255f,36f);
        PlaceHeaderText(textSymbol,header,.025f,.32f,.30f,.18f,22f,true);
        PlaceHeaderText(textRiskBadge,header,.35f,.075f,.15f,.215f,22f,true);
        PlaceHeaderText(textLastPrice,header,.025f,.53f,.30f,.42f,62f);
        PlaceHeaderText(textChangePct,header,.35f,.53f,.22f,.26f,36f);
        PlaceHeaderText(textChangeAmount,header,.35f,.80f,.22f,.18f,26f);
        // Both statistics columns share caption/value baselines and right edges.
        PlaceHeaderText(textRealValue,header,.63f,.27f,.14f,.23f,25f,true,true);
        PlaceHeaderText(textFakeValue,header,.63f,.72f,.14f,.23f,25f,true,true);
        PlaceHeaderText(textVolume,header,.80f,.27f,.175f,.23f,25f,true,true);
        PlaceHeaderText(textTurnover,header,.80f,.72f,.175f,.23f,25f,true,true);
        PlaceHeaderCaption(header,"Real Caption",.63f,.08f,.14f);
        PlaceHeaderCaption(header,"Fake Caption",.63f,.53f,.14f);
        PlaceHeaderCaption(header,"Volume Caption",.80f,.08f,.175f);
        PlaceHeaderCaption(header,"Turnover Caption",.80f,.53f,.175f);
    }
```

수정본:

```csharp
public void ConfigureHeaderLayout()
    {
        if (textName == null || textName.transform.parent is not RectTransform header)
        { return; }
        PlaceHeaderText(textName,header,.025f,.035f,.30f,.28f,36f);
        PlaceHeaderText(textSymbol,header,.025f,.31f,.30f,.20f,22f,true);
        PlaceHeaderText(textRiskBadge,header,.35f,.075f,.15f,.215f,22f,true);
        PlaceHeaderText(textLastPrice,header,.025f,.50f,.30f,.48f,58f);
        PlaceHeaderText(textChangePct,header,.35f,.48f,.22f,.28f,36f);
        PlaceHeaderText(textChangeAmount,header,.35f,.76f,.22f,.23f,26f);
        // Both statistics columns share caption/value baselines and right edges.
        PlaceHeaderText(textRealValue,header,.63f,.27f,.14f,.23f,25f,true,true);
        PlaceHeaderText(textFakeValue,header,.63f,.72f,.14f,.23f,25f,true,true);
        PlaceHeaderText(textVolume,header,.80f,.27f,.175f,.23f,25f,true,true);
        PlaceHeaderText(textTurnover,header,.80f,.72f,.175f,.23f,25f,true,true);
        PlaceHeaderCaption(header,"Real Caption",.63f,.08f,.14f);
        PlaceHeaderCaption(header,"Fake Caption",.63f,.53f,.14f);
        PlaceHeaderCaption(header,"Volume Caption",.80f,.08f,.175f);
        PlaceHeaderCaption(header,"Turnover Caption",.80f,.53f,.175f);
    }
```

### Assets/Scripts/ChartManager.cs · PlaceHeaderText

원본:

```csharp
private static void PlaceHeaderText(TextMeshProUGUI text,RectTransform parent,float x,float y,float width,float height,float size,bool muted = false,bool right = false)
    {
        if (text == null)
        { return; }
        var rect = text.rectTransform;
        if (rect.parent != parent) { rect.SetParent(parent,false); }
        rect.anchorMin = new Vector2(x,1-y-height); rect.anchorMax = new Vector2(x+width,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        text.fontSize = size; text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.color = muted ? new Color32(138,150,163,255) : new Color32(230,235,239,255);
    }
```

수정본:

```csharp
private static void PlaceHeaderText(TextMeshProUGUI text,RectTransform parent,float x,float y,float width,float height,float size,bool muted = false,bool right = false)
    {
        if (text == null)
        { return; }
        var rect = text.rectTransform;
        if (rect.parent != parent) { rect.SetParent(parent,false); }
        rect.anchorMin = new Vector2(x,1-y-height); rect.anchorMax = new Vector2(x+width,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        text.margin = Vector4.zero;
        text.fontSize = size; text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.color = muted ? new Color32(138,150,163,255) : new Color32(230,235,239,255);
    }
```

### Assets/Scripts/MarketDay/TabletGroupChatUI.cs · SelectRoom

원본:

```csharp
public void SelectRoom(int room)
    {
        moderationMenu?.Close();
        SelectedRoom = Mathf.Clamp(room, 0, InvestorSociety.MaximumRoomId);
        selectedBot = 0;
        if (profile != null)
        { profile.text = "메시지를 눌러 참여자의 경험과 심리를 확인하세요."; }
        if (visualProfile != null) { visualProfile.Clear(); }
        measuredHeights.Clear(); displayedMessages.Clear(); retainedSequences.Clear(); rowOffsets.Clear(); rowHeights.Clear();
        foreach (var row in rows) { row.Sequence = -1; }
        version = -1; readSequence = oldestSequence = 0; lastUnread = -1; lastParticipantCount = -1;
        firstVisible = -1; followNewest = true; dirty = true;
        scroll.StopMovement(); scrollInput?.StopFollowingGesture();
        ShowChat();
    }
```

수정본:

```csharp
public void SelectRoom(int room)
    {
        moderationMenu?.Close();
        SelectedRoom = Mathf.Clamp(room, 0, InvestorSociety.MaximumRoomId);
        if (SelectedRoom > InvestorSociety.AvailablePlayerRoomCount && SelectedRoom <= InvestorSociety.PlayerRoomCount) { SelectedRoom = 0; }
        selectedBot = 0;
        if (profile != null)
        { profile.text = "메시지를 눌러 참여자의 경험과 심리를 확인하세요."; }
        if (visualProfile != null) { visualProfile.Clear(); }
        measuredHeights.Clear(); displayedMessages.Clear(); retainedSequences.Clear(); rowOffsets.Clear(); rowHeights.Clear();
        foreach (var row in rows) { row.Sequence = -1; }
        version = -1; readSequence = oldestSequence = 0; lastUnread = -1; lastParticipantCount = -1;
        firstVisible = -1; followNewest = true; dirty = true;
        scroll.StopMovement(); scrollInput?.StopFollowingGesture();
        ShowChat();
    }
```

### Assets/Scripts/NightShift/MarketEventFeed.cs · Awake

원본:

```csharp
void Awake() { Instance = this; }
```

수정본:

```csharp
void Awake() { Instance = this; visibleUntil = Time.unscaledTime + displaySeconds; }
```

### Assets/Scripts/NightShift/MarketEventFeed.cs · Notify

원본:

```csharp
public void Notify(string message) { LatestEvent = message; dirty = true; }
```

수정본:

```csharp
public void Notify(string message) { LatestEvent = message; dirty = true; visibleUntil = Time.unscaledTime + displaySeconds; }
```

### Assets/Scripts/NightShift/MarketEventFeed.cs · Update

원본:

```csharp
void Update()
    {
        if (!dirty || eventText == null)
        { return; }
        eventText.text = LatestEvent; dirty = false;
    }
```

수정본:

```csharp
void Update()
    {
        if (eventText == null)
        { return; }
        bool visible = Time.unscaledTime < visibleUntil;
        if (eventText.enabled != visible) { eventText.enabled = visible; }
        if (dirty) { eventText.text = LatestEvent; dirty = false; }
    }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · OpenRooms

원본:

```csharp
private void OpenRooms()
    {
        var career = PlayerProgression.Instance;
        if (career == null)
        { visualGrowth?.ShowFeedback("성장 기록을 불러오는 중입니다."); return; }
        if (!career.CanOperateRooms && !career.Eligible)
        {
            if (orderScreen == null)
            { visualGrowth?.ShowFeedback("주문 화면 연결을 확인해 주세요."); return; }
            HideGrowth(); orderScreen.FocusScreen(); return;
        }
        if (!career.CanOperateRooms)
        {
            if (career.OpenLeadingRooms()) { visualGrowth?.Refresh(career); }
            else { visualGrowth?.ShowFeedback("개설 조건이 바뀌었습니다. 현재 성과를 확인하세요."); }
            next = 0f; return;
        }
        if (leadingRooms == null) { leadingRooms = FindAnyObjectByType<LeadingRoomUI>(); }
        if (leadingRooms == null || leadingRooms.chat == null)
        { visualGrowth?.ShowFeedback("라운지를 준비하는 중입니다. 잠시 후 다시 눌러 주세요."); return; }
        leadingRooms.chat.SelectRoom(1); HideGrowth(); thisTablet.ReturnTablet();
        MarketEventFeed.Instance?.Notify("내 리딩방 1을 열었습니다. 오른쪽 모니터에서 참가자와 대화를 확인하세요.");
    }
```

수정본:

```csharp
private void OpenRooms()
    {
        var career = PlayerProgression.Instance;
        if (career == null)
        { visualGrowth?.ShowFeedback("성장 기록을 불러오는 중입니다."); return; }
        if (!career.CanOperateRooms && !career.Eligible)
        {
            if (orderScreen == null)
            { visualGrowth?.ShowFeedback("주문 화면 연결을 확인해 주세요."); return; }
            HideGrowth(); orderScreen.FocusScreen(); return;
        }
        if (!career.CanOperateRooms)
        {
            if (career.OpenLeadingRooms()) { visualGrowth?.Refresh(career); }
            else { visualGrowth?.ShowFeedback("개설 조건이 바뀌었습니다. 현재 성과를 확인하세요."); }
            next = 0f; return;
        }
        if (leadingRooms == null) { leadingRooms = FindAnyObjectByType<LeadingRoomUI>(); }
        if (leadingRooms == null || leadingRooms.chat == null)
        { visualGrowth?.ShowFeedback("라운지를 준비하는 중입니다. 잠시 후 다시 눌러 주세요."); return; }
        leadingRooms.chat.SelectRoom(1); HideGrowth(); thisTablet.ReturnTablet();
        MarketEventFeed.Instance?.Notify("내 리딩방을 열었습니다. 공용 라운지에서 첫 회원을 직접 초대하세요.");
    }
```

## 기타 연결 변경

- MarketDayController.RiskChanged → NotifyMarketRisk: 실제 거래소 위험 사건 전달.
- ChatSocialDecisions.TickSocialAutonomy → TickMarketRisk: 기존 24명 갱신 예산 안에서 처리.
- TradeBot.BuyLimit / CanBuyStock: 실제 인지한 위험에 따른 추가 매수 크기와 잠시 보류. 기존 호가·체결·예약금 계산 유지.
- ChatSocialPipeline.GenerateSocialMessage / Slot: 게시 직전 등급 버전·자기 경험 검증과 위험 등급 문구.
- ChatSocialSpeech.RenderSocialText: 진지한 봇은 차분한 위험 반응 문구 사용.
- ChatSocialEmotions.ApplySocialEffectsCore: 실제 수익 후기를 읽은 목격자에게만 방 인지 전달.
- ChatSocialTopicFocus.HesitateOnNewTopic: 실제 위험 경고를 일반 새 주제 망설임으로 미루지 않음.
- ChatSocialConfig.Validate: 새 확률·시간·상한 검증.
- Compile.ps1: 삭제된 임시 촬영기 등 Unity rsp의 오래된 Assets 소스 경로 제거 후 실제 파일로 컴파일.
- Tests/ChatSocial/Prepare.py: 실제 IsRoomOpen/ChooseRoom/Join/Invite를 그대로 추출해 멤버십 경로 검증.
