"""Create config defaults and exact-source headless boundaries; never rewrite game methods."""
import json, re, uuid, pathlib, hashlib
ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / 'Validation/ChatSocial'
OUT.mkdir(parents=True, exist_ok=True)

def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def prepare_defaults():
    values = dict(temperMean=50,temperSd=20,grudgeMean=45,grudgeSd=25,suggestibilityMean=50,suggestibilitySd=20,sociabilityMean=50,sociabilitySd=25,bravadoMean=40,bravadoSd=25,politenessMean=60,politenessSd=25,
        friendsMin=2,friendsMax=4,friendAffinityMin=30,friendAffinityMax=70,memoryCapacity=8,logCapacity=12000,
        speakCooldownSec=6,replyCooldownScale=.5,roomWindowSec=10,roomMaxMsgPer10Sec=5,
        replyExpiry=20,emotionExpiry=30,selfExpiry=60,witnessExpiry=45,idleChance=.12,idleInterval=2,
        botRecentTemplates=10,roomRecentTemplates=30,maxConsecutiveAct=2,memoryChance=.4,memoryMaxUses=2,
        styleFormalMin=67,styleCasualMin=34,intensityTwo=40,intensityThree=70,
        replyMin=.05,replyMax=.98,replySociability=.004,replyAnger=.003,replyChill=.003,replyDelayMin=2,replyDelayMax=7,
        chatTurnsMin=2,chatTurnsMax=4,questionTurnsMin=2,questionTurnsMax=3,argumentTurnsMin=4,argumentTurnsMax=8,
        maxThreads=2,maxArguments=1,maxParticipants=4,thirdPartyChance=.15,friendThreshold=40,mediatorSociability=60,mediatorTemper=50,spectatorBravado=60,spectatorAffinity=20,pairCooldownSec=60,interruptMovePct=5,
        angerStage1=30,angerStage2=55,angerStage3=75,angerStage4=90,angerMaximumPerMessage=25,angerDecayPerHour=6,
        angerHalfTemper=.5,angerDisciplineDivisor=200,attackDisciplineDivisor=150,witnessHalfSuggestibility=.5,witnessThreadCap=10,rumorDailyTrustCap=15,
        ignoredAnger=3,ignoredAffinity=-2,mediationBase=.3,mediationAffinityDivisor=200,mediationStage3Scale=.5,mediationSuccessAnger=-12,mediationSuccessAffinity=3,mediationFailAnger=5,mediationFailAffinity=-3,
        friendMockAnger=4,friendInsultAnger=8,insultWitnessAffinity=-4,bragGreed=4,losingBragAffinity=-3,lamentHeldFear=6,lamentOtherFear=2,praiseTrust=3,warnTrust=-4,
        stage1AttackWeight=2,stage2AttackWeight=3,stage3PeaceWeight=.3,callPlayerDelaySec=8,exitGraceSec=15,exitAngerMin=85,exitOtherAngerDrop=30,exitTrust=-10,exitGrudgeWarnChance=.6,exitRejoinMinutes=1440,abandonedFightTrust=-2,
        sympathyAffinity=60,sympathyTrustDivisor=150,sympathyDisciplineDivisor=200,sympathyDelayMin=5,sympathyDelayMax=25,sympathyCapFraction=.25,sympathyTrust=-5,sympathyRejoinMinutes=720,
        warningAnger=-15,warningTemperMin=70,warningTrust=-3,warningDiscipline=5,warningResolveChance=.6,warningRepeatMinutes=10,warningRepeatScale=.5,oneSidedTrust=-3,
        muteMinutes=30,muteTrust=-5,muteDiscipline=8,unmuteGrudge=70,
        kickFighterDiscipline=20,kickFighterTrust=2,kickBetrayalMin=60,kickBetrayalDiscipline=5,kickBetrayalTrust=1,kickArbitraryDiscipline=5,kickArbitraryChill=8,kickFriendTrust=-5,kickOneSidedFriendTrust=-8,
        kickBothWindowSec=20,kickBothDiscipline=10,kickRateWindowMinutes=60,kickRateLimit=3,kickRateChill=10,kickSelfTrust=-30,kickSelfBetrayal=15,kickWarnBase=.3,kickWarnGrudgeDivisor=200,
        disciplineDecayPerHour=3,chillDecayPerHour=4,disciplineReactThreshold=20,chillReactThreshold=30,chillSpeechFloor=.2,chillSpeechDivisor=125,
        quadrantThreshold=50,quadrantHysteresis=5,loyalAcceptanceBase=.5,loyalAcceptanceDivisor=200,loyalAmountBase=.6,loyalAmountDivisor=250,ambivalentAcceptanceBase=.35,ambivalentAcceptanceDivisor=300,ambivalentAmount=.5,
        ambivalentTargetMin=.6,ambivalentTargetMax=.8,ambivalentStopScale=.5,ambivalentGainTrustScale=.5,ambivalentProfitBetrayalRelief=1,ambivalentLossScale=1.5,hostileLeaveDivisor=200,indifferentAcceptanceDivisor=200,indifferentAmount=.4,indifferentDailyLeave=.1,
        fomoRisePct=8,fomoDurationMinutes=20,fomoCooldownMinutes=60,fomoFearDivisor=150,fomoAmountScale=1.5,fomoLossFear=15,fomoGainGreed=10,
        memoryLeadAssetPct=10,memorySelfAssetPct=15,memoryAngerScale=1.3,memoryLossSalience=90,memoryGainSalience=70,memoryKickedSalience=85,memoryFoughtSalience=60,memorySawKickSalience=45,memoryBragCrashSalience=40,memorySelfSalience=50,
        tradeFactMinutes=30,promiseWeight=2,
        hotWindowMinutes=5,hotBaselineMinutes=180,hotReturnDivisor=5,hotDwellStartMinutes=10,hotDwellPenaltyPerMinute=.05,hotDwellFloor=.3,hotReentryMinutes=15,hotReentryScale=.5,
        fatigueGain=8,fatigueDecayPerHour=15,fatigueDivisor=125,rotationMinutes=60,rotationStrength=1,rotationLookbackMinutes=120,rotationReturnDivisor=10,rotationMin=.3,rotationMax=2.5,
        overheatFloor=.2,overheatStartPct=15,overheatDivisor=30,overheatFear=2,overheatFearMinutes=10,
        R1Min=.55,R1Max=.8,R2Min=2.5,R2Max=4.5,R3Min=.3,R3Max=.5,R4Min=4,R4Max=15,R5Min=1,R5Max=4,R6Min=.3,R6Max=1.5,R7Min=.05,R7Max=.15,R8Max=.05,
        volumeShareMax=.45,hotLeaderIntervalMax=60,leaderTimeShareMax=.25,stockBreadthMin=.5)
    config=ROOT/'Assets/Resources/Chat/chat_social_config.json'
    if not config.exists(): write_json(config,dict(schemaVersion=1,values=[dict(key=k,value=v) for k,v in values.items()]))
    weights=ROOT/'Assets/Resources/Chat/chat_act_weights.json'
    motives={
        'IDLE':dict(SMALLTALK=8,ASK=15,REACT=20,BRAG=10,LAMENT=10,DOUBT=8,AGREE=8,MOCK=2),
        'SELF_EVENT':dict(BRAG=35,LAMENT=35,PRAISE_PLAYER=15,WARN_PLAYER=15,REACT=5),
        'WITNESSED_EVENT':dict(ASK=25,DOUBT=15,AGREE=20,REBUT=15,COMFORT=10,MOCK=8,REACT=15),
        'EMOTION_THRESHOLD':dict(MOCK=25,REBUT=25,DOUBT=15,INSULT=20,LAMENT=10,CALL_PLAYER=5),
        'REPLY_OBLIGATION':dict(ANSWER=50,AGREE=30,REBUT=20)}
    replies={
        'MOCK':dict(MOCK=35,REBUT=30,DOUBT=10,SMALLTALK=15,LAMENT=10),
        'INSULT':dict(INSULT=40,MOCK=30,REBUT=20,CALL_PLAYER=10),
        'REBUT':dict(REBUT=45,MOCK=20,AGREE=15,ASK=10,APOLOGIZE=10),
        'DOUBT':dict(ANSWER=45,REBUT=30,MOCK=15,BRAG=10),
        'ASK':dict(ANSWER=80,ASK=10,SMALLTALK=10),
        'BRAG':dict(AGREE=35,DOUBT=25,MOCK=15,ASK=25),
        'LAMENT':dict(COMFORT=50,MOCK=20,AGREE=30),
        'COMFORT':dict(AGREE=60,SMALLTALK=40),'AGREE':dict(AGREE=60,SMALLTALK=40),
        'APOLOGIZE':dict(AGREE=60,MOCK=25,APOLOGIZE=15),
        'MEDIATE':dict(AGREE=50,REBUT=30,SILENCE=20),
        'ANSWER':dict(AGREE=60,ASK=20,SMALLTALK=20),'SMALLTALK':dict(AGREE=60,SMALLTALK=40)}
    effects={'DOUBT':dict(anger=4,affinity=-2),'REBUT':dict(anger=6,affinity=-3),'MOCK':dict(anger=12,affinity=-8,fear=3),'INSULT':dict(anger=25,affinity=-20),'AGREE':dict(anger=-3,affinity=5),'COMFORT':dict(anger=-5,affinity=8,fear=-4),'APOLOGIZE':dict(anger=-20,affinity=5),'ANSWER':dict(affinity=2)}
    table=lambda d:[dict(key=k,weights=[dict(key=a,value=b) for a,b in v.items()]) for k,v in d.items()]
    if not weights.exists(): write_json(weights,dict(schemaVersion=1,motives=table(motives),replies=table(replies),effects=table(effects)))
    dest=ROOT/'Assets/Resources/Chat/chat_templates.json'
    if not dest.exists():
        original=json.loads((ROOT/'Assets/Resources/Chat/BotChatTemplates.ko.json').read_text(encoding='utf-8-sig'))
        mapping={'PriceRise':'REACT','PriceFall':'REACT','HoldingProfit':'BRAG','HoldingLoss':'LAMENT','BreakEvenRecovered':'REACT','ProfitRealized':'BRAG','LossRealized':'LAMENT','PriceRiseAfterSell':'LAMENT','BuyConsideration':'ASK','MarketOpen':'REACT','MarketClose':'REACT','DailyProfit':'BRAG','DailyLoss':'LAMENT','StressRelief':'REACT','SmallTalk':'SMALLTALK','Agreement':'AGREE'}
        write_json(dest,dict(schemaVersion=1,templates=[dict(key='legacy.'+e['id'],act=mapping[e['event']],style='casual',intensity=1,variant='plain',text=e['text'],requires=e.get('requirements',[])) for e in original['templates']],suffixes=[]))

def extract_member(source, name):
    match=re.search(r'^    (?:public |private |protected |internal |static |sealed |override )*[^\n=;]+\b'+re.escape(name)+r'\([^\n]*\)\s*\n    \{',source,re.M)
    if not match: raise ValueError(name)
    end=re.search(r'^    \}',source[match.end():],re.M)
    return source[match.start():match.end()+end.end()]

def legacy_host():
    source=(ROOT/'Assets/Scripts/MarketDay/MarketDayController.cs').read_text(encoding='utf-8-sig')
    prefix=source[:source.index('    void Awake()')].replace('public sealed class MarketDayController','public sealed partial class MarketDayController')
    names=['PickConversationEvent','EventAttention','RemoveEvent','Enqueue','RemoveOldestEvent','ProcessChat','PublishParticipant','Eligible','AddMessage']
    methods='\n'.join(extract_member(source,n) for n in names)
    methods+='\n    public BotChatMessage GetMessage(int index) { return messages[(messageStart + index) % messages.Length]; }\n'
    (OUT/'LegacyChatProduction.cs').write_text(prefix+methods+'\n}\n',encoding='utf-8')
    write_json(OUT/'legacy-source-provenance.json',{'source':'Assets/Scripts/MarketDay/MarketDayController.cs','sha256':hashlib.sha256(source.encode()).hexdigest(),'verbatimMethods':names})
    progress=(ROOT/'Assets/Scripts/NightShift/PlayerProgression.cs').read_text(encoding='utf-8-sig')
    progress_methods='\n'.join(extract_member(progress,n) for n in ['Capture','Restore'])
    (OUT/'ProgressionProduction.cs').write_text('using System; using System.Globalization; using UnityEngine;\npublic sealed class PlayerProgression\n{\n'
        +'[Serializable] public sealed class Snapshot { public int version=1,invitedRoomMask,journeyMilestones; public string worldId,turnover,realizedProfit; public bool roomsOpened,dreamt; public double newsCooldownRemaining; public long newsSavedUtc; public DailyIncomeController.SaveData income; }\n'
        +'public static PlayerProgression Instance; public string LockReason="locked"; public string WorldId="fixture-world"; public decimal Turnover,RealizedProfit; public bool CanOperateRooms,Dreamt; public int JourneyMilestones;\n'
        +progress_methods+'\n}\n'
        +'public sealed partial class InvestorSociety\n{\n'
        +'\n'.join(extract_member((ROOT/'Assets/Scripts/NightShift/InvestorSociety.cs').read_text(encoding='utf-8-sig'), n) for n in ['IsRoomOpen','ChooseRoom','Join','Invite'])
        +'\n}\n',encoding='utf-8')

def meta():
    for pattern in ['Assets/Scripts/NightShift/ChatSocial*.cs','Assets/Resources/Chat/chat_*.json','Assets/Editor/ChatSocialTools.cs']:
        for path in ROOT.glob(pattern):
            target=path.with_suffix(path.suffix+'.meta')
            if not target.exists():
                guid=uuid.uuid5(uuid.NAMESPACE_URL,'DonDon/'+path.relative_to(ROOT).as_posix()).hex
                target.write_text('fileFormatVersion: 2\nguid: '+guid+'\n',encoding='utf-8')

if __name__=='__main__':
    prepare_defaults(); legacy_host(); meta()
    print('Prepared config, migrated template data, and verbatim legacy chat methods.')
