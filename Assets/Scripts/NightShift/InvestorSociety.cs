using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

/// <summary>주기적 예산 안에서 각 투자자의 관찰·감정·가입·대화를 처리한다. 전역 감정 방송은 없다.</summary>
public sealed partial class InvestorSociety : MonoBehaviour
{
    public const int PlayerRoomCount = 3, MaximumRoomId = 4;
    // Keep legacy room IDs for saved references; only one player room is available in gameplay.
    public const int AvailablePlayerRoomCount = 1;
    public sealed class Room
    {
        public readonly int Id;
        public int OwnerBotId;
        public string Title;
        public bool PlayerOwned => Id <= PlayerRoomCount;
        public string DisplayName => PlayerOwned ? "내 리딩방" : Title;
        public readonly HashSet<int> Members = new();
        private readonly BotChatMessage[] messages = new BotChatMessage[128];
        private int start;
        public int Count { get; private set; }
        public long Version { get; private set; }
        public LeadingRoomOrders.Command Command;
        public float NextConversation;
        public readonly SocialRoomState Social = new();
        private readonly Dictionary<string, float> recentText = new();
        private readonly Queue<(string text, float time)> recentTextOrder = new();
        public bool RegisterText(string text, float now)
        {
            while (recentTextOrder.Count > 0 && (now - recentTextOrder.Peek().time > 120f || recentTextOrder.Count >= 128))
            { recentText.Remove(recentTextOrder.Dequeue().text); }
            if (recentText.ContainsKey(text))
            { return false; }
            recentText[text] = now; recentTextOrder.Enqueue((text, now)); return true;
        }
        public Room(int id) { Id = id; }
        public BotChatMessage Get(int index) => messages[(start + index) % messages.Length];
        public void Add(BotChatMessage message)
        {
            if (Count == messages.Length) { start = (start + 1) % messages.Length; Count--; }
            messages[(start + Count++) % messages.Length] = message; Version = message.Sequence;
        }
    }
    public static InvestorSociety Instance { get; private set; }
    public InvestorSocietySettings settings = new();
    public readonly Room[] Rooms = { new Room(1), new Room(2), new Room(3), new Room(NpcLeadingRoom.RoomId) { Title = "새벽 관찰실" } };
    public LeadingRoomOrders Orders { get; private set; }
    public string LastAction { get; private set; } = "공용 라운지 또는 리딩방을 선택하세요.";
    public int Witnesses { get; private set; }
    public int Arguments { get; private set; }
    public int Joins { get; private set; }
    public int Leaves { get; private set; }
    public int Outcomes { get; private set; }
    public int ConversationReads { get; private set; }
    public int MaxBotsProcessedPerTick { get; private set; }
    public readonly Queue<string> DebugTrace = new(64);
    private System.Random random;
    private long nextEvent, nextCommand;
    private int cursor;
    private float nextTick, nextContact;
    private MarketDayController day;
    private readonly Dictionary<int, int> suggestions = new();
    private static readonly ProfilerMarker TickMarker = new("DonDon.InvestorSociety");
    void Awake() { Instance = this; random = new System.Random(settings.seed); ChatSocialIds.Reset(); }
    void Update()
    {
        if (Time.timeScale > 0f) { Orders?.FlushObservedResults(settings.botsPerTick); }
        // 장이 닫혀 있는 동안에는 매매와 심리는 멈추고 장외 채팅만 잇는다.
        if (!MarketDayController.CanTrade)
        { TickAfterHours(); return; }
        if (SessionClock.Time < nextTick)
        { return; }
        EndAfterHours();
        day = MarketDayController.Instance;
        if (day != null) { ChatSocialAudit.GameMinute = (day.Day - 1) * 1440d + day.GameMinute; }
        if (day == null || !day.Ready || StockManager.Instance == null)
        { return; }
        if (Orders == null) { Orders = new LeadingRoomOrders(this); }
        PrepareSocialRuntime();
        nextTick = SessionClock.Time + Mathf.Max(.05f, settings.updateInterval);
        using (TickMarker.Auto())
        {
            int count = Math.Min(Math.Max(1, settings.botsPerTick), day.Bots.Count);
            MaxBotsProcessedPerTick = Math.Max(MaxBotsProcessedPerTick, count);
            for (int i = 0; i < count; i++)
            {
                if (cursor >= day.Bots.Count) { cursor = 0; }
                var bot = day.Bots[cursor++];
                if (bot == null || bot.Social == null || bot.IsExchangeParticipant)
                { continue; }
                TickBot(bot, SessionClock.Time);
            }
            SocialAdvance(day.Day, day.GameMinute, SessionClock.Time, settings.botsPerTick);
        }
    }
    public long NextEventId() => ChatSocialIds.Next();
    public LeadingRoomOrders EnsureSessionOrders()
    { return Orders ??= new LeadingRoomOrders(this); }
    public void ReconnectSession()
    {
        day = MarketDayController.Instance;
        EnsureSessionOrders().ReconnectSession();
        PrepareSocialRuntime();
    }
    public Room GetRoom(int id) => id >= 1 && id <= Rooms.Length ? Rooms[id - 1] : null;
    public bool IsRoomOpen(int id)
    {
        var room = GetRoom(id);
        return room != null && (room.PlayerOwned ? id <= AvailablePlayerRoomCount && (PlayerProgression.Instance == null || PlayerProgression.Instance.CanOperateRooms) : room.OwnerBotId > 0);
    }
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
                // 플레이어의 방은 잘 알고 믿을수록 들어갈 마음이 커진다.
                if (target > 0 && GetRoom(target).PlayerOwned) { interest *= RoomInterest(mind); }
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
    private void ReadRecent(TradeBot bot, int roomId, float now)
    {
        var room = GetRoom(roomId);
        int count = room == null ? day.MessageCount : room.Count;
        var mind = bot.Social.Psychology;
        long latest = mind.LastRead[roomId];
        for (int i = Math.Max(0, count - Math.Max(1, settings.recentMessages)); i < count; i++)
        {
            var message = room == null ? day.GetMessage(i) : room.Get(i);
            if (message.Sequence <= mind.LastRead[roomId])
            { continue; }
            latest = Math.Max(latest, message.Sequence);
            if (message.BotId <= 0 || message.BotId == bot.Social.Id)
            { continue; }
            if (random.NextDouble() >= settings.witnessProbability * (.35f + mind.Activity * .65f))
            { continue; }
            if (message.Context.EventId > 0 && message.Context.TargetId == -1)
            { Observe(bot, message, roomId, now, random.NextDouble()); }
            else if (roomId > 0)
            {
                mind.LastRoomWitness = message; mind.LastRoomWitnessAt = now; mind.LastWitnessRoom = roomId;
                mind.RememberSpeaker(message.BotId, 0, 0, message.Context.Credibility); ConversationReads++;
            }
        }
        mind.LastRead[roomId] = latest;
    }
    public void ReadBeforeSpeech(TradeBot bot)
    {
        day ??= MarketDayController.Instance;
        if (day == null || bot.Social == null || !bot.Social.Psychology.ActiveAt(SessionClock.Time))
        { return; }
        ReadRecent(bot, 0, SessionClock.Time);
        if (bot.Social.Psychology.Room > 0) { ReadRecent(bot, bot.Social.Psychology.Room, SessionClock.Time); }
    }
    public bool Observe(TradeBot bot, BotChatMessage message, int roomId, float now, double replyRoll)
    {
        if (roomId != 0 && bot.Social.Psychology.Room != roomId || message.Context.TargetId != -1 || message.BotId == bot.Social.Id)
        { return false; }
        var mind = bot.Social.Psychology; var context = message.Context;
        if (!mind.Witness(context.EventId, context.Sentiment, context.Credibility, context.Hops, settings))
        { return false; }
        mind.RememberSpeaker(message.BotId, context.EventId, context.Sentiment, context.Credibility);
        Witnesses++; Trace(bot, mind.LastCause);
        if (context.Sentiment > 0f && context.Room > 0) { suggestions[bot.Social.Id] = context.Room; }
        if (now >= mind.NextSpeech && now - bot.Social.LastSpeech >= settings.speechCooldown && InvestorConversation.ShouldDisagree(mind, context, replyRoll))
        {
            var experience = mind.Latest.Value;
            Speak(bot, roomId, message.Nickname + "님, " + InvestorConversation.Testimony(bot, experience, true), Context(bot, experience, roomId, message.Sequence));
            Arguments++;
        }
        else if (context.Hops < 2 && now >= mind.NextSpeech && now - bot.Social.LastSpeech >= settings.speechCooldown && replyRoll < .04f * mind.Credulity)
        {
            string report = message.Nickname + "님은 " + (context.Sentiment > 0f ? "지시 따라 수익을 봤다" : "지시 따라 손실을 봤다") + "고 하네요. 직접 확인한 얘기는 아닙니다.";
            var rumor = new InvestorMessageContext(context.EventId, context.Room, context.Sentiment, context.Credibility * settings.rumorAttenuation, mind.Trust, false, message.Sequence, context.Hops + 1);
            Speak(bot, roomId, report, rumor);
        }
        return true;
    }
    private InvestorMessageContext Context(TradeBot bot, InvestorExperience experience, int room, long reply = 0)
    {
        return new InvestorMessageContext(experience.EventId, experience.Room, Math.Sign(experience.ReturnPercent), .6f + bot.Social.Confidence * .25f, bot.Social.Psychology.Trust, true, reply);
    }
    public void Speak(TradeBot bot, int room, string text, InvestorMessageContext context, ChatEvent kind = ChatEvent.Agreement)
    {
        if (bot != null && SocialReady)
        { OfferExistingSpeech(bot, room, context, kind); return; }
        day ??= MarketDayController.Instance;
        if (day == null)
        { return; }
        if (room == 0) { day.PublishParticipant(bot, text, context); }
        else
        {
            var channel = GetRoom(room);
            if (channel == null)
            { return; }
            if (bot != null && !channel.RegisterText(text, SessionClock.Time))
            { bot.Social.Psychology.NextSpeech = SessionClock.Time + 8f; return; }
            channel.Add(new BotChatMessage(channel.Version + 1, bot, day.Day, day.GameMinute, text, "society", kind, context));
            if (bot != null) { channel.NextConversation = SessionClock.Time + Mathf.Max(1f, settings.roomMessageInterval); }
        }
        if (bot != null)
        { bot.Social.LastSpeech = SessionClock.Time; bot.Social.Psychology.NextSpeech = SessionClock.Time + settings.speechCooldown + (float)random.NextDouble() * 30f; }
    }
    public void RecordOutcome(TradeBot bot, InvestorExperience experience)
    {
        if (DailyIncomeController.Instance != null) { experience = DailyIncomeController.Instance.NetExperience(bot,experience); }
        if (!bot.Social.Psychology.Record(experience, settings))
        { return; }
        Outcomes++; Trace(bot, bot.Social.Psychology.LastCause);
        if (SocialReady) { OfferExistingSpeech(bot, bot.Social.Psychology.Room, Context(bot, experience, bot.Social.Psychology.Room), ChatEvent.Agreement); }
    }
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
        if (room.PlayerOwned && DailyIncomeController.Instance != null && !DailyIncomeController.Instance.Enter(bot,roomId,out string feeReason))
        { LastAction = bot.Social.Nickname + ": " + feeReason; Trace(bot,"이용료 거절: " + feeReason); return false; }
        // 초대로 들어왔든 홍보를 보고 들어왔든, 첫 회원이 생기면 방은 알려진 것으로 본다.
        if (room.PlayerOwned)
        {
            room.Social.firstMemberInvited = true;
            if (playerInvited) { mind.InvitedPlayerRooms |= 1 << (roomId - 1); }
            RememberRoomJoin(mind, roomId);
        }
        mind.Room = roomId; mind.JoinedRoomAt = SessionClock.Time; room.Members.Add(bot.Social.Id); Joins++;
        mind.LastWitnessRoom = 0;
        mind.LastRead[roomId] = room.Version; // Pre-join private messages were not witnessed.
        PostRoomNotice(roomId, ChatRoomNotice.Join, bot.Social.Nickname + " 님이 입장했습니다.");
        if (SessionClock.Time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
        { Speak(bot, roomId, InvestorConversation.Greeting(bot), default, ChatEvent.SmallTalk); }
        Trace(bot, roomId + "번 방 참여"); return true;
    }
    public void Leave(TradeBot bot, string reason, ChatRoomNotice notice = ChatRoomNotice.Leave)
    {
        int room = bot.Social.Psychology.Room;
        if (room == 0)
        { return; }
        GetRoom(room).Members.Remove(bot.Social.Id); bot.Social.Psychology.Room = 0;
        Orders?.Cancel(Orders.Get(bot)); Leaves++;
        bot.Social.Psychology.MediationExitAt = -1;
        if (notice != ChatRoomNotice.None) { PostRoomNotice(room, notice, bot.Social.Nickname + " 님이 퇴장했습니다."); }
        Trace(bot, room + "번 방 이탈: " + reason);
    }
    public void PostRoomNotice(int roomId, ChatRoomNotice notice, string text)
    {
        day ??= MarketDayController.Instance;
        if (day == null || notice == ChatRoomNotice.None)
        { return; }
        DeliverSocialSystemNotice(roomId, text, NextEventId(), notice);
    }
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
        if (!firstMember && (SessionClock.Time < nextContact || SessionClock.Time - mind.LastContact < settings.repeatContactCooldown))
        { LastAction = "아직 다시 연락할 수 없습니다."; return false; }
        if (mind.Room != 0)
        { LastAction = "이미 " + GetRoom(mind.Room).DisplayName + "에 참여 중입니다."; return false; }
        if (GetRoom(roomId).Members.Count >= settings.roomCapacity)
        { LastAction = "방의 정원이 가득 찼습니다."; return false; }
        if (!bot.isActiveAndEnabled || bot.IsExchangeParticipant || GetRoom(roomId).Social.banned.Contains(botId) ||
            mind.RejoinAt.TryGetValue(roomId, out double returnAt) && SocialTime.GameMinute < returnAt)
        { LastAction = "지금은 이 참여자를 초대할 수 없습니다."; return false; }
        nextContact = SessionClock.Time + settings.contactCooldown; mind.LastContact = SessionClock.Time;
        mind.RecallBetrayal(); Trace(bot, "회유: " + mind.LastCause);
        bool willing = firstMember || random.NextDouble() < mind.JoinChance;
        LastAction = bot.Social.Nickname + ": 참여 제안을 거절했습니다.";
        bool accepted = willing && Join(bot, roomId, playerInvited: true);
        if (accepted) { LastAction = bot.Social.Nickname + ": 내 리딩방에 참여했습니다. 명령 수락은 별도 판단합니다."; }
        return accepted;
    }
    public bool Issue(int roomId, Stock stock, decimal buy, decimal sell)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned || !IsRoomOpen(roomId) || !MarketDayController.CanTrade || stock == null || !stock.AcceptsOrders || buy < 1m || sell <= buy || buy != PriceTickPolicy.RoundToTick(buy) || sell != PriceTickPolicy.RoundToTick(sell))
        { LastAction = "거래 가능한 종목과 올바른 원화 호가를 입력하세요. 매도가는 매수가보다 커야 합니다."; return false; }
        CancelRoom(roomId);
        room.Command = new LeadingRoomOrders.Command { Id = ++nextCommand, Room = roomId, Stock = stock, Buy = buy, Sell = sell, IssuedAt = SessionClock.Time, IssuedDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day };
        LastAction = $"{room.DisplayName}: {stock.stockName} 매수 ≤ {buy:N0} / 매도 ≥ {sell:N0}. 참가자가 개별 판단합니다.";
        Speak(null, roomId, LastAction, default); return true;
    }
    public void CancelRoom(int roomId)
    {
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned)
        { return; }
        if (room.Command != null) { room.Command.Canceled = true; }
        Orders?.CancelRoom(roomId);
        LastAction = room.DisplayName + " 지시 취소. 체결된 주식은 각 참가자에게 남습니다.";
    }
    public void Trace(TradeBot bot, string cause)
    {
        if (DebugTrace.Count >= 64) { DebugTrace.Dequeue(); }
        string entry = $"{SessionClock.Time:0.0}s #{bot.Social.Id} {cause}";
        DebugTrace.Enqueue(entry);
        if (settings.trace) { Debug.Log("[InvestorSociety] " + entry); }
    }
    public void Forget(TradeBot bot)
    {
        if (bot.Social == null)
        { return; }
        if (bot.Social.Psychology.Room > 0) { Leave(bot, "시장 퇴장"); }
        suggestions.Remove(bot.Social.Id); Orders?.Forget(bot);
    }
    void OnDestroy()
    {
        DisposeSocialRuntime();
        Orders?.Dispose();
        if (Instance == this) { Instance = null; }
    }
}
