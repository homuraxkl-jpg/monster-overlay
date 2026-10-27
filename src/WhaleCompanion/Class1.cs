using SharpPluginLoader.Core.Networking;
using ImGuiNET;
using SharpPluginLoader.Core.Components;
using SharpPluginLoader.Core;
using SharpPluginLoader.Core.Actions;
using SharpPluginLoader.Core.Entities;
using SharpPluginLoader.Core.Rendering;
using SharpPluginLoader.Core.Resources;
using SharpPluginLoader.Core.MtTypes;

namespace WhaleCompanion;

public sealed partial class ProbePlugin : IPlugin
{
    public string Name => "Whale Companion";
    public string Author => "Homura";
    private readonly CompanionChat chat = new();
    private readonly ResponsePolicy responses=new();
    private readonly OfflineDialogueEditor offlineEditor = new();
    private readonly NativeDialogue nativeDialogue = new();
    private NativeMessageInterop? nativeInterop;
    private readonly NativeChatInput nativeInput = new();
    private readonly SharedChat sharedChat = new();
    private readonly SelfChatMemory chatMemory = new();
    private bool shareEnabled,shareSceneReady;
    private string shareStatus="联机共享关闭，回复仅自己可见";
    private readonly TeamChatProbe teamProbe = new();
    private readonly PalicoHealthProbe palicoHealthProbe=new();
    private readonly WeaponStateResponseQueue weaponStateResponses=new();
    private readonly MonsterBreakTracker monsterBreakTracker=new();
    private readonly MonsterEncounterTrace monsterEncounterTrace=new();
    private readonly MonsterStateDirector monsterStateDirector=new();
    private double nextMonsterStateStats;
    private readonly PalicoRecoveryDirector palicoRecovery=new();
    private readonly PendingRecoveryFeedback pendingCatRecovery=new();
    private double nextCatHealthStats;
    private readonly PalicoResourceCapture palicoCapture = new();
    private readonly LocalGmdMemory gmdMemory = new();
    private string palicoResourceStatus = "随从台词资源诊断关闭";
    private double nextPalicoStats;
    private long loggedPalicoCallbacks = -1;
    private readonly PalicoBattleCapture battleCapture = new();
    private PalicoMessageObserver? battleObserver;
    private string battleStatus="战斗对白观察关闭";
    private double nextBattleStats;
    private readonly PeerChatContext peerContext = new();
    private readonly object peerGate = new();
    private volatile bool readPeerChat, peerSceneReady;
    private long peerCandidates, peerAccepted, loggedPeerCandidates;
    private string peerLastReject="none";
    private int peerLastType,peerLastSession,peerLastBytes;
    private double nextPeerLog;
    private int nativeChatEpoch;
    private bool showChatStatus = true;
    private string deliveryStatus = "等待原生回复";
    private volatile bool nativeChatEnabled, nativeChatSceneReady;
    private bool nativeOutput, nativeSceneReady;
    private long nativeDeliveredId = -1;
    private string nativeStatus = "Native output disabled";
    private readonly CompanionExpression expression = new();
    private readonly FaceMeshExecutor face = new();
    private bool showHeadPot = false;
    private string appearanceStatus = "";
    private static string AppearancePath => Path.Combine(CompanionChat.ConfigurationDirectory, "appearance.json");
    private bool animatedFace = true, slowFacePreview;
    private readonly FacePlayback facePlayback = new();
    private int selectedFaceGroup = FaceSelection.FirstGroup;
    private int loggedBlink;
    private long blinkQueryBaseline, blinkClosedQueryBaseline;
    private double nextFaceLog;
    private bool enabled, spiritDiagnostic;
    private bool spiritCompanion;
    private readonly SpiritCompanion spiritDirector = new();
    private readonly LocalProcessMemory localMemory = new();
    private readonly SpiritGaugeTracker spirit = new();
    private readonly WeaponStateTracker weaponStates=new();
    private readonly WeaponStateCompanion weaponStateDirector=new();
    private readonly NativeGuardCompanion guardDirector=new();
    private NativeGuardSample? guardSample;
    private readonly NativeEvasionCompanion evasionDirector=new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,bool> evasionActionExports=new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,int> evasionActionAttempts=new(); private double nextEvasionActionAttempt; private int evasionActionBusy;
    private int evasionResearchQueued; private string? evasionResearchOutcome;


    private uint? lastEvasionFlagA,lastEvasionFlagB; private string? recentEvasionScope; private string recentEvasionWeapon=""; private double recentEvasionScopeTime; private ulong recentEvasionScopeOwner;
    private float? previousEvasionTimer; private double previousEvasionTime; private ulong previousEvasionOwner;
    private readonly EvasionContactDirector contactEvasionDirector=new();
    private EvasionContactObserver? evasionContacts;
    private double nextEvasionContactLog;
    private readonly NativeActionTracker nativeActions=new();
    private ulong sampledPlayer;
    private string nativeActionStatus="Current-action confirmation OFF";
    private GuardRecoilCandidate? pendingGuardRecoil;
    private double nextRecoilResponse;
    private bool saedResponses;
    private ulong gameModule;
    private SaedResponseSettings saedSettings=new();
    private readonly CombatDamageDirector combatVerdicts=new();
    private readonly TenderizeTracker tenderizeTracker=new();
    private double nextTenderizePoll;
    private readonly CombatFeedback combatFeedback=new();
    private readonly DisplayedDamageCounter displayedDamage=new();
    private bool combatFallbackObserverOn;
    private double nextFallbackAttempt;
    private CombatActionRequest? combatRequest;
    private CombatFacts? combatFacts;
    private double nextCombatStart;
    private readonly RollingOutputDirector rollingOutput=new();
    private PlayerDamageCounterSample? ownerCounter;
    private CombatDamageResult? pendingSaedResult;
    private string counterStatus="Owner counter OFF";
    private double nextCounterPoll,nextCounterLog;
    private static string SaedSettingsPath=>Path.Combine(CompanionChat.ConfigurationDirectory,"combat-response.json");
    private double nextGuardLog;
    private readonly DamageCaptureBuffer damageBuffer=new();
    private readonly DamageAttribution damageAttribution=new();
    private DamageObserver? damageObserver;
    private bool allWeaponCompanion,damageDiagnostic;
    private long lastDamageDropped;
    private double nextDamageStats;
    private double nextDamageLogBudget;
    private int damageLogBudget=32;
    private long damageLogSuppressed;
    private string damageStatus="Damage observer OFF";
    private bool weaponStateDiagnostic;
    private bool monsterResearchLogging;
    private string weaponStateStatus="Weapon state diagnostic OFF",weaponStateRevision="unknown";
    private double nextWeaponStateLog;
    private string spiritStatus = "Diagnostic disabled", lastSpiritStatus = "", spiritMarkFeedback = "";
    private double nextSpiritLog;
    private bool autoCompanion, actionCompanion;
    private readonly ActionCompanion actionDirector = new();
    private ActionObservation? pendingAction;
    private double actionQuietUntil;
    private readonly CompanionEvents companionEvents = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private double lineUntil;
    private int lossLine, lowLine;
    private static readonly string[] LossLines = ["先稳住节奏，我陪着你。", "刚才掉了些血，找个空隙调整一下吧。", "别着急，慢慢来就好。", "疼不疼？我们先缓口气。", "我在呢，照顾好自己。", "先站稳，再找下一次机会吧。"];
    private static readonly string[] LowLines = ["血量不多了，找个空隙缓一缓。", "先顾好自己，找机会恢复吧。", "别勉强撑着，我还想陪你回营呢。", "先拉开一点距离，喘口气吧。"];
    private int session;
    private float elapsed;
    private float hp, maxHp;
    private string status = "Waiting for player";
    private string weapon = "-", action = "-";
    private readonly Queue<string> events = new();
    private bool baseline;
    private bool fontReady;
    // SPL can load assemblies from bytes; Assembly.Location is then empty.
    private readonly string logPath = Path.Combine(AppContext.BaseDirectory, "WhaleCompanion-p0-diagnostics.log");
    private int samples;
    private bool scenePaused;

    public void OnLoad()
    {
        WriteLog("P11.5 response interface + native output, multiplayer and observed context 2026-10-27");
        try{gameModule=(ulong)(System.Diagnostics.Process.GetCurrentProcess().MainModule?.BaseAddress??0);}catch{gameModule=0;}
        UiLanguage.Load(Path.Combine(CompanionChat.ConfigurationDirectory,"ui-language.txt"));
        responses.Load(Path.Combine(CompanionChat.ConfigurationDirectory,"responses.json"));
        ResponseCatalog.Seed(responses);responses.Save();
        LoadConversationOptions();
        LoadSaedSettings();
        try { WriteLog($"COMBAT_CATALOG equipment={CombatReference.EquipmentCount} motion={CombatReference.MotionCount}"); } catch(Exception ex) { WriteLog("COMBAT_CATALOG_FAILED "+ex.GetType().Name); }
        try{weaponStateRevision=typeof(IPlugin).Assembly.GetType("SharpPluginLoader.Core.InternalCalls")?.GetMethod("GetGameRevision",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)?.Invoke(null,null) as string??"unknown";}catch{weaponStateRevision="unknown";}
        try
        {
            var armPath=Path.Combine(CompanionChat.ConfigurationDirectory,"palico-resource-probe.once");
            if(File.Exists(armPath)&&File.ReadAllText(armPath).Trim()=="read-only-180s-v1")
            {
                File.Delete(armPath);palicoCapture.Start(clock.Elapsed.TotalSeconds);
                palicoResourceStatus="本次启动已自动开启180秒只读资源诊断";
                WriteLog("PALICO_RESOURCE_PROBE started seconds=180 read_only=true startup_once=true");
            }
        }
        catch(Exception ex){WriteLog("PALICO_RESOURCE_ARM_FAILED "+ex.GetType().Name);}
        monsterStateDirector.Observed+=(kind,time)=>RecordHunt("monster_"+kind,"只读怪物状态确认："+kind+"；归属未知。",time);
        chat.NativeDecisionMade += (valid, speak) => { if(!valid)deliveryStatus="本次无可显示回复：格式错误";else if(!speak)deliveryStatus="本次无需显示：模型选择沉默";WriteLog($"NATIVE_CHAT_DECISION valid={valid} speak={speak}"); };
        // One output subscription covers both conversation and battle/state lines.
        chat.LineReady += text =>
        {
            observedConversation.Add("companion","speech",text,clock.Elapsed.TotalSeconds);
            nativeDeliveredId=-1;
            if(shareEnabled){sharedChat.Enqueue(text,clock.Elapsed.TotalSeconds);deliveryStatus="回复排队等待游戏聊天发送";}
            else if(nativeOutput){nativeDialogue.Enqueue(text,clock.Elapsed.TotalSeconds);deliveryStatus="回复排队等待原生显示";}
        };
        chat.NativeTrace += detail => WriteLog("NATIVE_CHAT_RESPONSE "+detail);

        try { showHeadPot = AppearanceSettings.Load(AppearancePath).ShowHeadPot; }
        catch { appearanceStatus = "Invalid appearance.json; default no-pot appearance retained"; }
        face.SetPotVisible(showHeadPot);
        face.Initialize(WriteLog);
        chat.ReplyReceived += reply => { expression.Offer(reply.Emotion, reply.Intensity, 1, clock.Elapsed.TotalSeconds); WriteLog($"FACE_AI emotion={reply.Emotion} intensity={reply.Intensity:0.00}"); lineUntil = clock.Elapsed.TotalSeconds + 8; actionQuietUntil = lineUntil; };
        chat.ConversationReset += () => { observedConversation.Clear();chat.UpdateObservationContext("");sharedChat.Clear();ClearPeerChat(false); nativeChatEpoch++; nativeInput.Clear(); nativeDialogue.Clear(); nativeDeliveredId=-1; expression.Reset(clock.Elapsed.TotalSeconds); };
        var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "msyh.ttc");
        if (File.Exists(font))
        {
            Renderer.RegisterFont("WhaleChinese", font, 20, Renderer.GetGlyphRanges(GlyphRanges.ChineseFull));
            fontReady = true;
        }
    }

    public void OnUpdate(float deltaTime)
    {
        if(enabled&&!scenePaused&&weaponStateDiagnostic)
        {
            while(palicoHealthProbe.TryTypeDetail(out var typeDetail))WriteLog("PALICO_TYPE_DIAGNOSTIC "+typeDetail);
            var catSample=palicoHealthProbe.Poll(localMemory,gameModule,clock.Elapsed.TotalSeconds,weaponStateRevision);
            if(catSample!=null)
            {
                WriteLog("PALICO_HEALTH_OBSERVED "+catSample);
                if(palicoRecovery.Observe(palicoHealthProbe.LatestHealth,true))
                {
                    RecordHunt("palico_recovery","Observed Palico HP recovery; healer and item unknown",clock.Elapsed.TotalSeconds);
                    pendingCatRecovery.Offer(clock.Elapsed.TotalSeconds);
                    WriteLog("PALICO_RECOVERY_DETECTED source=unknown item=unknown attribution=none");
                }
            }
            if(clock.Elapsed.TotalSeconds>=nextCatHealthStats){nextCatHealthStats=clock.Elapsed.TotalSeconds+5;WriteLog("PALICO_HEALTH_STATS "+palicoHealthProbe.Status);}
        }
        ProcessDamageObservations();
        PollOwnerCounter();
        PollNativeAction();
        PollTenderize();
        DeliverSaedResult();
        offlineEditor.Poll();
        palicoCapture.Tick(clock.Elapsed.TotalSeconds,gmdMemory);
        foreach(var match in battleCapture.Drain())WriteLog("PALICO_BATTLE_MATCH "+System.Text.Json.JsonSerializer.Serialize(match));
        if(battleCapture.Stats(clock.Elapsed.TotalSeconds).Active&&clock.Elapsed.TotalSeconds>=nextBattleStats)
        {nextBattleStats=clock.Elapsed.TotalSeconds+10;WriteLog("PALICO_BATTLE_STATS "+System.Text.Json.JsonSerializer.Serialize(battleCapture.Stats(clock.Elapsed.TotalSeconds)));}
        if(clock.Elapsed.TotalSeconds>=nextPalicoStats)
        {
            nextPalicoStats=clock.Elapsed.TotalSeconds+5;
            var stats=palicoCapture.Stats();
            if(stats.Callbacks!=loggedPalicoCallbacks)
            {loggedPalicoCallbacks=stats.Callbacks;WriteLog("PALICO_RESOURCE_STATS "+System.Text.Json.JsonSerializer.Serialize(stats));}
        }
        foreach(var result in palicoCapture.Drain())
        {
            palicoResourceStatus=$"随从资源（{result.Language}）：{result.Status}，条数={result.Count}，原文匹配={result.Matched}/8";
            WriteLog("PALICO_RESOURCE "+System.Text.Json.JsonSerializer.Serialize(result));
        }
        foreach(var sample in teamProbe.Drain()) WriteLog("TEAM_CHAT_MARK "+System.Text.Json.JsonSerializer.Serialize(sample));
        if(readPeerChat && clock.Elapsed.TotalSeconds>=nextPeerLog && Interlocked.Read(ref peerCandidates)!=loggedPeerCandidates)
        {
            nextPeerLog=clock.Elapsed.TotalSeconds+5;loggedPeerCandidates=Interlocked.Read(ref peerCandidates);
            WriteLog($"PEER_CONTEXT_STATS packets={loggedPeerCandidates} accepted={Interlocked.Read(ref peerAccepted)} entries={peerContext.Count} ready={peerSceneReady} last_type={peerLastType} last_session={peerLastSession} last_bytes={peerLastBytes} reject={peerLastReject}");
        }
        chat.UpdatePeerContext(readPeerChat && peerSceneReady ? peerContext.Snapshot(clock.Elapsed.TotalSeconds) : "");
        chat.Poll();
        try { ProcessObservedConversation();ProcessNativeChat();ProcessPeerAndProactiveChat(); }
        catch(Exception ex) { nativeChatEnabled=false;nativeInput.Clear();chat.CancelNative();WriteLog($"NATIVE_CHAT_STOPPED exception={ex.GetType().Name}"); }
        FlushNativeDialogue();
        ProcessSharedChat();
        expression.Update(clock.Elapsed.TotalSeconds);
        facePlayback.Update(clock.Elapsed.TotalSeconds, animatedFace, expression.Emotion, expression.Weight, expression.Blink);
        selectedFaceGroup = facePlayback.Group;
        if (facePlayback.CompletedBlinks != loggedBlink)
        {
            loggedBlink = facePlayback.CompletedBlinks;
            WriteLog($"FACE_BLINK cycle={loggedBlink} peak={facePlayback.LastBlinkPeak:0.000} closed_updates={facePlayback.LastClosedFrames} queries_since_previous={face.Queries-blinkQueryBaseline} visible_closed_queries={face.ClosedEyeQueries-blinkClosedQueryBaseline} auto={animatedFace}");
            blinkQueryBaseline = face.Queries; blinkClosedQueryBaseline = face.ClosedEyeQueries;
        }
        face.Set(selectedFaceGroup);
        if (face.Ready && clock.Elapsed.TotalSeconds >= nextFaceLog)
        { nextFaceLog = clock.Elapsed.TotalSeconds + 5; WriteLog($"FACE_STATE group={selectedFaceGroup} queries={face.Queries} emotion={expression.Emotion} weight={expression.Weight:0.00} blink={facePlayback.Blink:0.000} face_emotion={facePlayback.Emotion} face_weight={facePlayback.Weight:0.000} manual={facePlayback.ManualActive}"); }
        ProcessAction();
        if (!enabled || scenePaused) { contactEvasionDirector.Reset();spirit.Reset(); weaponStates.Reset();evasionDirector.Reset();guardDirector.Reset();guardSample=null;return; }
        elapsed += deltaTime;
        if (elapsed < 0.1f) return;
        elapsed = 0;
        try
        {
            if (SingletonManager.GetSingleton("sPlayer") is null) { Reset(); return; }
            var player = Player.MainPlayer;
            if (player is null || player.Get<nint>(0x7630) == 0) { Reset(); return; }
            var current = player.Health;
            var maximum = player.MaxHealth;
            if (!float.IsFinite(current) || !float.IsFinite(maximum) || maximum <= 0 || current < 0 || current > maximum) { Reset(); return; }
            if (baseline && current < hp) Add($"HP decreased {hp - current:0.0} (cause unknown)");
            hp = current; maxHp = maximum; baseline = true;
            sampledPlayer=(ulong)player.Instance;
            nativeSceneReady=nativeOutput;nativeChatSceneReady=nativeChatEnabled;shareSceneReady=shareEnabled;peerSceneReady=readPeerChat;
            if (autoCompanion) HandleEvent(companionEvents.Observe(hp, maxHp, clock.Elapsed.TotalSeconds));
            weapon = player.CurrentWeaponType.ToString();
            ObserveSpirit(player.Instance);
            ObserveNativeGuard(player.Instance);
            ProcessGuardRecoil();
            ObserveWeaponState(player.Instance);
            var experimentalContext=allWeaponCompanion&&weaponStates.Ready&&weaponStates.Current?.Weapon==weapon?"；实验武器字段（社区映射待实机确认，不据此判断命中或反击；raw不保证计时单位）："+System.Text.Json.JsonSerializer.Serialize(weaponStates.Current.Fields):"";
            if(allWeaponCompanion&&guardSample!=null)experimentalContext+=$"；原生攻击守势计时原始值：{guardSample.Timer}（实验读取；非GP/反击命中证据）。";
            chat.UpdateGameContext($"生命值 {hp:0.#}/{maxHp:0.#}；武器 {weapon}；最近动作编号 {action}（不代表命中）；太刀气刃等级：{(weapon == "LongSword" ? SpiritCompanion.LevelName(spirit.Stable) : "不适用")}（只说明等级，不证明具体招式或命中）。"+experimentalContext);
            if(!autoCompanion)pendingCatRecovery.Reset();
            if(pendingCatRecovery.TryTake(clock.Elapsed.TotalSeconds,autoCompanion&&!chat.Busy&&clock.Elapsed.TotalSeconds>=actionQuietUntil))
            {
                EventLine("palico_recovered","health","又充满活力啦！",$"猫的HP低于{ResponseTuning.Thresholds.PalicoArmRatio:P0}后恢复至少最大HP{ResponseTuning.Thresholds.PalicoHealRatio:P0}。只说恢复状态，不指定治疗来源、道具或主语。");expression.Offer("happy",.6f,1,clock.Elapsed.TotalSeconds,3);actionQuietUntil=clock.Elapsed.TotalSeconds+3;
                WriteLog("PALICO_RECOVERED source=unknown item=unknown attribution=none");
            }
            var monsterState=monsterStateDirector.Take(clock.Elapsed.TotalSeconds,saedResponses&&autoCompanion&&!chat.Busy&&clock.Elapsed.TotalSeconds>=actionQuietUntil);
            if(monsterState!=null)
            {
                EventLine("monster_"+monsterState.Kind,"monster",MonsterStateDirector.Text(monsterState.Kind),"已确认怪物状态："+monsterState.Kind+"。只说该状态，不指定部位，不宣称主人造成，不编造伤害或贡献。");expression.Offer("happy",.5f,1,clock.Elapsed.TotalSeconds,3);actionQuietUntil=clock.Elapsed.TotalSeconds+3;
                WriteLog("MONSTER_STATE_RESPONSE kind="+monsterState.Kind+" owner=unknown source=native_state");
            }
            status = "Player available";
            if (++samples % 50 == 0) WriteLog($"sample HP={hp}/{maxHp} weapon={weapon} action={action}");
        }
        catch (Exception ex)
        {
            Reset(); enabled = false;
            status = $"Probe stopped: {ex.GetType().Name}";
            WriteLog($"PROBE_STOPPED exception={ex.GetType().Name}");
        }
    }

    public void OnEntityAction(Entity entity,ref ActionInfo next)
    {
        if(!enabled||scenePaused||!weaponStateDiagnostic||weaponStateRevision!="421810")return;
        // Property Instance only. No entity.Is, GetDti, native calls or full snapshots here.
        try{var actor=(ulong)entity.Instance;palicoHealthProbe.Inspect(localMemory,actor,gameModule,clock.Elapsed.TotalSeconds);}catch{}
    }

    public void OnPlayerAction(Player player, ref ActionInfo next)
    {
        if (!enabled || scenePaused) return;
        var main = Player.MainPlayer;
        if (main is null || main.Instance != player.Instance) return;
        action = $"{next.ActionSet}:{next.ActionId}";
        WriteLog($"action {action}");
        if (autoCompanion && actionCompanion)
            Interlocked.Exchange(ref pendingAction, new ActionObservation(player.CurrentWeaponType.ToString(), next.ActionSet, next.ActionId, clock.Elapsed.TotalSeconds,(ulong)player.Instance));
        if(saedResponses&&autoCompanion&&actionCompanion)
        {
            var actualWeapon=player.CurrentWeaponType.ToString();
            if(CombatProfiles.Handles(actualWeapon,next.ActionSet,next.ActionId))
            {
                var now=clock.Elapsed.TotalSeconds;
                var before=CombatFacts.Read(localMemory,(ulong)player.Instance,actualWeapon,weaponStateRevision,now);
                Interlocked.Exchange(ref combatRequest,new CombatActionRequest(new(actualWeapon,next.ActionSet,next.ActionId,now,(ulong)player.Instance),before,ownerCounter));
            }
        }
        // Observe only: never change the ref parameter.
    }

    public void OnChatMessageSent(string message)
    {
        if(!string.IsNullOrWhiteSpace(message)&&SharedChat.IsCompanion(message)){WriteLog("SHARED_CHAT_ECHO ignored local=true");return;}
        if(!string.IsNullOrWhiteSpace(message) && teamProbe.Local(message,clock.Elapsed.TotalSeconds))return;
        if(!nativeChatEnabled || !nativeChatSceneReady || string.IsNullOrWhiteSpace(message))return;
        // Whisper text is not public companion input and must not later become a shared reply.
        var chatGui=SingletonManager.GetSingleton("sMhGUI");
        if(chatGui!=null&&localMemory.Int32((ulong)chatGui.Instance+0x14748,out var channel)&&channel==2){WriteLog("NATIVE_CHAT_PRIVATE_SKIPPED");return;}
        var accepted=nativeInput.Receive(message,clock.Elapsed.TotalSeconds);
        if(accepted)observedConversation.Add("local_owner","chat",message.Trim(),clock.Elapsed.TotalSeconds);
        WriteLog($"NATIVE_CHAT_LOCAL accepted={accepted} chars={Math.Min(message.Length,512)}");
    }

    public void OnReceivePacket(uint id, PacketType type, SessionIndex sourceSession, NetBuffer data)
    {
        var now=clock.Elapsed.TotalSeconds;
        if(!teamProbe.Active(now) && !(readPeerChat&&peerSceneReady))return;
        try
        {
            if(teamProbe.Active(now))teamProbe.Receive(id,(int)type,(int)sourceSession,data.Buffer,now);
            if(!readPeerChat||!peerSceneReady||id!=PeerChatPacket.Id)return;
            peerLastType=(int)type;peerLastSession=(int)sourceSession;peerLastBytes=data.Size;
            var line=PeerChatPacket.Parse(id,(int)type,(int)sourceSession,data.Buffer,now,out var reject);
            if(line==null){peerLastReject=reject;Interlocked.Increment(ref peerCandidates);return;}
            if(SharedChat.IsCompanion(line.Text)){peerLastReject="companion_echo";Interlocked.Increment(ref peerCandidates);return;}
            peerLastReject="none";
            lock(peerGate)
                if(readPeerChat&&peerSceneReady&&peerContext.Observe(line,now)){Interlocked.Increment(ref peerAccepted);while(pendingPeerObservations.Count>=8)pendingPeerObservations.TryDequeue(out _);pendingPeerObservations.Enqueue(line);}
            Interlocked.Increment(ref peerCandidates);
        }
        catch { /* Bad/unrecognized packets must not disrupt the game's receive path. */ }
    }

    private void ClearPeerChat(bool pause)
    {
        lock(peerGate){if(pause)peerSceneReady=false;peerContext.Clear();while(pendingPeerObservations.TryDequeue(out _)){}peerInput.Clear();}
        chat.UpdatePeerContext("");
    }

    private void ProcessNativeChat()
    {
        if(!nativeChatEnabled||!nativeInput.HasQueued)return;
        var ready=nativeChatSceneReady && HasLoadedSave();
        var text=nativeInput.Take(clock.Elapsed.TotalSeconds,ready,!chat.CanAcceptNative);
        if(text==null)return;
        var epoch=nativeChatEpoch; var expires=clock.Elapsed.TotalSeconds+30;
        if(nativeOutput)nativeSceneReady=true;
        if(chat.StartNative(text,()=>nativeChatEnabled && nativeChatSceneReady && epoch==nativeChatEpoch && clock.Elapsed.TotalSeconds<=expires))
        { deliveryStatus="等待本次模型结果"; actionQuietUntil=clock.Elapsed.TotalSeconds+8; WriteLog("NATIVE_CHAT_REQUEST source=local_outbound decision_and_reply=one_request"); }
    }

    public void OnQuestEnter(int questId) { huntLedger.NewEncounter(questId);huntContext.Clear(); scenePaused = false; shareSceneReady=shareEnabled; peerSceneReady=readPeerChat; nativeChatSceneReady = nativeChatEnabled; nativeSceneReady = nativeOutput; Reset(true); Add($"Quest enter {questId}"); }
    public void OnQuestLeave(int questId) { huntLedger.EndEncounter(); scenePaused = true; shareSceneReady=false; sharedChat.Clear(); ClearPeerChat(true); nativeChatSceneReady = false; nativeSceneReady = false; chat.Reset(); Reset(true); Add($"Quest leave {questId}"); }
    public void OnSelectSaveSlot(int slot) { huntContext.Clear();huntLedger.Clear();observedConversation.Clear(); scenePaused = true; shareSceneReady=false; sharedChat.Clear(); ClearPeerChat(true); nativeChatSceneReady = false; nativeSceneReady = false; chat.Reset(); Reset(true); }
    public void OnQuestComplete(int questId) { huntLedger.EndEncounter(); if (autoCompanion) { actionQuietUntil = clock.Elapsed.TotalSeconds + 8; EventLine("quest_complete","quest","辛苦啦，任务完成！这次也平安归来了。","任务已完成，简短陪伴，不编造贡献、奖励或无伤。"); expression.Offer("happy", 0.75f, 2, clock.Elapsed.TotalSeconds); } Add($"Quest complete {questId}"); }
    public void OnQuestFail(int questId) { huntLedger.EndEncounter(); if (autoCompanion) { actionQuietUntil = clock.Elapsed.TotalSeconds + 8; EventLine("quest_fail","quest","先歇一歇吧，我陪你准备下一次出发。","任务失败，温和陪伴准备下次，不责备或编造失败原因。"); expression.Offer("concerned", 0.7f, 2, clock.Elapsed.TotalSeconds); } Add($"Quest fail {questId}"); }
    public void OnUnload() { WriteLog("PLUGIN_UNLOAD_BEGIN");evasionContacts?.Dispose();damageObserver?.Dispose();offlineEditor.Dispose();battleObserver?.Dispose();palicoCapture.Stop();teamProbe.Stop();chat.Dispose(); face.Dispose(); nativeInterop?.Dispose(); }

    public void OnResourceLoad(Resource? resource, MtDti dti, string path, LoadFlags flags)
    {
        // The callback resource is weak: inspect immediately and retain no object or pointer.
        palicoCapture.Observe(path,resource is null?0:(ulong)resource.Instance,clock.Elapsed.TotalSeconds,gmdMemory);
    }

    public unsafe void OnImGuiRender()
    {
        var uiFont=fontReady?Renderer.GetFont("WhaleChinese"):null;
        bool uiPushed=uiFont.NativePtr!=null;
        if(uiPushed)ImGui.PushFont(uiFont);
        try
        {
        RenderResponseControls();
        RenderConversationControls();
        chat.Render();
        offlineEditor.Render();
        if(!ImGui.CollapsingHeader(UiLanguage.Text("Advanced diagnostics (technical English)","高级诊断（英文技术字段）","進階診斷（英文技術欄位）")))return;
        ImGui.TextWrapped("Monster encounter sampling: soften / part breaks / actions / ailments / traps; read only, no AI");
        foreach(var marker in new[]{"soften","part break","wallbang","stun","sleep","mount started","trap placed","monster trapped"})
        {
            if(ImGui.Button("Mark "+marker+" just seen"))WriteLog("MONSTER_EVENT_USER_MARK kind="+marker+" time="+clock.Elapsed.TotalSeconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
        }
        ImGui.TextUnformatted(palicoHealthProbe.Status);
        ImGui.TextWrapped("Palico recovery: arm below 75% HP; recover 25% max HP within one 10s recovery period; 30s cooldown");
        if(ImGui.Button("Reset Palico HP monitor (read only)"))palicoHealthProbe.Reset();
        if(ImGui.Button("Start Palico battle capture (60 min, no API)"))StartBattleCapture();
        ImGui.SameLine();
        if(ImGui.Button("Stop battle capture")){battleCapture.Stop();palicoCapture.Stop();battleStatus="战斗采样已停止";WriteLog("PALICO_BATTLE_STOP "+System.Text.Json.JsonSerializer.Serialize(battleCapture.Stats(clock.Elapsed.TotalSeconds)));}
        ChineseText(battleStatus);
        var battleStats=battleCapture.Stats(clock.Elapsed.TotalSeconds);
        ImGui.TextUnformatted($"Battle capture: {(battleStats.Active?"ON":"OFF")} | remaining {battleStats.Remaining/60:0.0} min | display calls {battleStats.Calls}");
        if(battleStats.Active)
        {
            if(ImGui.Button("Check observer path (local, no API)"))
            {
                try{var before=battleCapture.Stats(clock.Elapsed.TotalSeconds).Calls;battleObserver!.CheckLocalPath();WriteLog($"PALICO_BATTLE_SELF_CHECK before={before} after={battleCapture.Stats(clock.Elapsed.TotalSeconds).Calls}");}
                catch(Exception ex){battleStatus="通路自检失败："+ex.GetType().Name;WriteLog("PALICO_BATTLE_SELF_CHECK_FAILED "+ex.GetType().Name);}
            }
            if(ImGui.Button("Mark self-heal line just seen"))MarkBattleLine("self_heal");
            ImGui.SameLine();if(ImGui.Button("Mark support line just seen"))MarkBattleLine("support");
            if(ImGui.Button("Mark other Palico line just seen"))MarkBattleLine("other");
        }
        if(ImGui.Button("Start team-chat marker probe (180s, no API)")){teamProbe.Start(clock.Elapsed.TotalSeconds);WriteLog("TEAM_CHAT_PROBE started seconds=180");}
        if(ImGui.Button("Start Palico resource probe (180s, read only)"))
        {palicoCapture.Start(clock.Elapsed.TotalSeconds);palicoResourceStatus="等待ot_auto_chS/chT资源自然加载；不会主动加载或修改资源";WriteLog("PALICO_RESOURCE_PROBE started seconds=180 read_only=true");}
        ImGui.SameLine();
        if(ImGui.Button("Stop Palico resource probe")){palicoCapture.Stop();palicoResourceStatus="随从台词资源诊断已停止";}
        ChineseText(palicoResourceStatus);
        var palicoStats=palicoCapture.Stats();
        ImGui.TextUnformatted($"Resource callbacks: {palicoStats.Callbacks}; exact targets: {palicoStats.Targets}");
        ImGui.TextUnformatted(palicoCapture.Active(clock.Elapsed.TotalSeconds)?"Palico resource probe: active (no API, no replacement)":"Palico resource probe: inactive");
        ImGui.SameLine();
        if(ImGui.Button("Stop team-chat probe")){WriteLog("TEAM_CHAT_PROBE stopped "+teamProbe.Summary(clock.Elapsed.TotalSeconds));teamProbe.Stop();}
        ImGui.TextWrapped(teamProbe.Summary(clock.Elapsed.TotalSeconds));
        ImGui.TextWrapped("DIAGNOSTIC ONLY: no AI replies. Send the exact WHALE marker strings below, not ordinary chat. Native AI input is a separate paid checkbox.");
        ImGui.TextWrapped("Owner: WHALE-OWNER-A1; friend: WHALE-PEER-B1; friend: WHALE-OWNER-A1; owner: WHALE-PEER-B1; friend: WHALE-PEER-B2.");
        ImGui.TextUnformatted("Whale Companion P10 - companion and state diagnostics");
        ImGui.TextUnformatted($"Expression intent: {expression.Emotion} {expression.Weight:0.00} | blink {expression.Blink:0.00}");
        ImGui.TextWrapped(face.Status);
        ImGui.TextUnformatted($"Face group: {selectedFaceGroup} | {facePlayback.Emotion} {facePlayback.Weight:0.000} | eyelids {facePlayback.Blink:0.000} | native queries: {face.Queries}");
        ImGui.TextUnformatted($"Blink cycles: {facePlayback.CompletedBlinks} | last closed updates: {facePlayback.LastClosedFrames}");
        ImGui.TextUnformatted($"Visible queries: closed {face.ClosedEyeQueries} | intermediate {face.IntermediateQueries}");
        ImGui.TextUnformatted("8-step fades; relaxed uses verified terminal only (intermediate eyes unsafe).");
        if (ImGui.Checkbox("Show head pot", ref showHeadPot))
        {
            face.SetPotVisible(showHeadPot);
            try { new AppearanceSettings { ShowHeadPot = showHeadPot }.Save(AppearancePath); appearanceStatus = "Appearance saved"; }
            catch { appearanceStatus = "Applied for this session; could not save appearance"; }
            WriteLog($"HEAD_POT visible={showHeadPot} ready={face.Ready}");
        }
        if (!string.IsNullOrEmpty(appearanceStatus)) ImGui.TextUnformatted(appearanceStatus);
        ImGui.Checkbox("Automatic face animation (experimental)", ref animatedFace);
        ImGui.Checkbox("Slow preview (diagnostic)", ref slowFacePreview);
        if (ImGui.Button("Test neutral")) TestFace("neutral");
        ImGui.SameLine();
        if (ImGui.Button("Test eyelids")) TestFace("blink");
        if (ImGui.Button("Test happy")) TestFace("happy");
        ImGui.SameLine();
        if (ImGui.Button("Test sad")) TestFace("sad");
        if (ImGui.Button("Test angry")) TestFace("angry");
        ImGui.SameLine();
        if (ImGui.Button("Test relaxed")) TestFace("relaxed");
        ImGui.TextWrapped("Concerned uses a mild sad face. Normal transitions 0.22s; slow preview is optional. Restart game to replace plugin/model.");
        if (ImGui.Checkbox("Enable read-only probe (after loading save)", ref enabled))
        {
            scenePaused = false; Reset(); WriteLog($"probe enabled={enabled}");
        }
        ImGui.TextWrapped(nativeStatus);
        ChineseText(shareStatus);
        if(shareEnabled)
        {
            ImGui.TextWrapped("All companion lines. Current game channel; no private whisper. 127 UTF8 bytes including prefix, one message per 8s. Local full replies remain available.");
            if(ImGui.Button("Check shared sender (no send)"))CheckSharedSender();
            if(ImGui.Button("Send shared test (no API)"))sharedChat.Enqueue("大家好，本鱼来陪你们狩猎啦。",clock.Elapsed.TotalSeconds);
            if(ImGui.Button("Resume sharing (in loaded save)")){shareSceneReady=true;sharedChat.Clear();}
        }
        if(readPeerChat)
        {
            ImGui.TextWrapped($"Peer packets: {Interlocked.Read(ref peerCandidates)} | accepted: {Interlocked.Read(ref peerAccepted)} | recent context: {peerContext.Count}/8 | rejected: {peerLastReject}");
            ImGui.TextWrapped("Experimental session 0/1 text. Display names are unverified. Peers cannot trigger requests; ask the companion yourself to discuss recent peer chat.");
            if(ImGui.Button("Resume peer reading (in loaded save)")){lock(peerGate)peerSceneReady=true;}
        }
        if(nativeChatEnabled)
        {
            ImGui.TextWrapped(nativeInput.Summary(clock.Elapsed.TotalSeconds,nativeChatSceneReady,chat.Busy));
            ImGui.TextWrapped(nativeInput.Status);
            ImGui.Checkbox("Show chat cooldown/status overlay",ref showChatStatus);
            ImGui.TextWrapped("Local sent messages only; normal chat is still sent. Peer context is controlled in the main conversation panel. Up to one paid request per 8s.");
            if(ImGui.Button("Resume native chat input (in loaded save)")){nativeChatSceneReady=true;nativeOutput=true;nativeSceneReady=true;nativeInput.Clear();}
        }
        if (nativeOutput)
        {
            if (ImGui.Button("Resume native output (in loaded save)")) { nativeSceneReady = true; nativeDialogue.Clear(); nativeDeliveredId = -1; }
            if (ImGui.Button("Test native Chinese (no API)")) nativeDialogue.Enqueue("本鱼在这里，准备好就出发吧！", clock.Elapsed.TotalSeconds);
            if (ImGui.Button("Test native ASCII (no API)")) nativeDialogue.Enqueue("Whale test: ABC 123", clock.Elapsed.TotalSeconds);
        }
        if (ImGui.Checkbox("Long sword spirit diagnostic (start after loading save)", ref spiritDiagnostic))
        {
            if (spiritDiagnostic) { enabled = true; scenePaused = false; Reset(); }
            else spirit.Reset();
            spiritMarkFeedback = "";
            spiritStatus = spiritDiagnostic ? "Waiting for long sword sample" : "Diagnostic disabled";
            WriteLog($"LS_GAUGE_DIAGNOSTIC enabled={spiritDiagnostic} probe={enabled} scene_paused={scenePaused}");
        }
        if (spiritDiagnostic)
        {
            ImGui.TextUnformatted($"Spirit raw: {spirit.Raw?.ToString() ?? "unknown"} | stable: {spirit.Stable?.ToString() ?? "unknown"}");
            ImGui.TextWrapped(SpiritDiagnosticGate.Blocked(enabled, scenePaused) ?? $"{spiritStatus}; {status}");
            if ((!enabled || scenePaused) && ImGui.Button("Start/resume probe (only in loaded save)"))
            { enabled = true; scenePaused = false; Reset(); spiritMarkFeedback = ""; WriteLog("LS_GAUGE_RESUME probe=True scene_paused=False"); }
            if (spiritMarkFeedback.Length > 0) ImGui.TextWrapped(spiritMarkFeedback);
            ImGui.TextWrapped("Verified 0/1/2/3 = none/white/yellow/red. Diagnostic alone does not enable praise.");
            if (ImGui.Button("Mark no aura")) MarkSpirit("none"); ImGui.SameLine();
            if (ImGui.Button("Mark white")) MarkSpirit("white"); ImGui.SameLine();
            if (ImGui.Button("Mark yellow")) MarkSpirit("yellow"); ImGui.SameLine();
            if (ImGui.Button("Mark red")) MarkSpirit("red");
        }
        if(ImGui.Checkbox("Experimental weapon state diagnostics (read-only)",ref weaponStateDiagnostic))
        {weaponStates.Reset();nextWeaponStateLog=0;if(weaponStateDiagnostic){enabled=true;scenePaused=false;}}
        if(ImGui.Checkbox("Detailed monster research logging (read-only)",ref monsterResearchLogging))
        {monsterEncounterTrace.Reset();nextMonsterStateStats=0;}
        if(weaponStateDiagnostic)
        {
            if(NativeGuardSignal.CanGuard(weapon))ImGui.TextWrapped($"Native Offensive Guard timer: {guardSample?.Timer.ToString("0.###")??"unknown"}; GP success is not inferred.");
            ImGui.TextWrapped($"{weaponStateStatus} | revision={weaponStateRevision} | fresh={weaponStates.Ready}");
                        ImGui.TextWrapped("Shared-event hook suspended: no confirmed local ownership. Action code research is read-only and automatic.");
            if(ImGui.Button("Export mantle code research (local code candidates only)"))
            {try{counterStatus="Saved: "+EvasionCodeResearch.Export(Path.Combine(CompanionChat.ConfigurationDirectory,"combat-feedback"),weaponStateRevision);}catch(Exception ex){counterStatus="Code research export failed: "+ex.GetType().Name;}}
            if(weaponStates.Current is {} snapshot)
                foreach(var field in snapshot.Fields.Where(x=>!x.Key.StartsWith("song_effect_",StringComparison.Ordinal)||x.Value>0))ImGui.TextUnformatted($"{field.Key}: {field.Value:0.###}");
            ImGui.TextWrapped("14 weapon entries. Community fields remain experimental; missing fields stay unknown. Raw values are not confirmed hits.");
        }
        if(ImGui.Checkbox("Experimental all-weapon action and state responses",ref allWeaponCompanion))
        {contactEvasionDirector.Reset();actionDirector.IncludeAllWeapons=allWeaponCompanion;actionDirector.Reset();nativeActions.Reset();pendingGuardRecoil=null;weaponStateDirector.Reset();guardDirector.Reset();guardSample=null;chat.CancelAutomatic();if(allWeaponCompanion){weaponStateDiagnostic=true;enabled=true;scenePaused=false;}}
        ImGui.TextWrapped("Responses require Automatic companion + action companion below. AI requests use the existing paid actions switch. Native evade contact responses are experimental; other action samples do not prove hits or counters.");
        if(allWeaponCompanion||damageDiagnostic||saedResponses)ImGui.TextWrapped(nativeActionStatus);
        if(weaponStateDiagnostic&&evasionContacts!=null)ImGui.TextWrapped($"Contact diagnostic: {evasionContacts.Status}; local {evasionContacts.Local}; evades {contactEvasionDirector.Count}/{ResponseTuning.Thresholds.EvasionCount} in {EvasionContactDirector.WindowSeconds(weapon)}s; responses {contactEvasionDirector.Responses}; no fixed cooldown");
        ImGui.TextUnformatted("Native contact/damage observers enabled for controlled retest");
        if(ImGui.Checkbox("Experimental dynamic combat verdicts (14 weapons)",ref saedResponses))
        {
            rollingOutput.Reset();combatVerdicts.Reset();ownerCounter=null;pendingSaedResult=null;nextCombatStart=0;
            if(saedResponses){enabled=true;scenePaused=false;allWeaponCompanion=true;weaponStateDiagnostic=true;autoCompanion=true;actionCompanion=true;actionDirector.IncludeAllWeapons=true;}
            chat.CancelAutomatic();
        }
        if(saedResponses)
        {
            ImGui.TextWrapped(counterStatus);ImGui.TextWrapped(combatVerdicts.Status);
            ImGui.TextWrapped($"Threshold mode: {(saedSettings.DynamicThreshold?"dynamic equipment + move":"manual reference")}. Owner seat: {(saedSettings.OwnerSlot<0?"auto":(saedSettings.OwnerSlot+1).ToString())}. Config: combat-response.json");
            bool dynamicThreshold=saedSettings.DynamicThreshold;if(ImGui.Checkbox("Dynamic equipment and move threshold",ref dynamicThreshold)){saedSettings.DynamicThreshold=dynamicThreshold;SaveSaedSettings();}
            bool rollingEnabled=saedSettings.RollingOutputEnabled;
            if(ImGui.Checkbox("Rolling output praise (owner counter only)",ref rollingEnabled)){saedSettings.RollingOutputEnabled=rollingEnabled;SaveSaedSettings();}
            if(rollingEnabled)
            {
                float rollingAttack=(float)saedSettings.RollingOutputAttack,rollingSeconds=(float)saedSettings.RollingOutputSeconds,rollingMultiplier=(float)saedSettings.RollingOutputMultiplier,rollingCooldown=(float)saedSettings.RollingOutputCooldown;
                if(ImGui.InputFloat("Panel attack reference (0 = disabled; manual)",ref rollingAttack))saedSettings.RollingOutputAttack=float.IsFinite(rollingAttack)?Math.Clamp(rollingAttack,0,100000):0;
                if(ImGui.SliderFloat("Output window seconds",ref rollingSeconds,10,300))saedSettings.RollingOutputSeconds=rollingSeconds;
                if(ImGui.SliderFloat("Output attack multiplier",ref rollingMultiplier,1,100))saedSettings.RollingOutputMultiplier=rollingMultiplier;
                if(ImGui.SliderFloat("Output cooldown seconds",ref rollingCooldown,10,3600))saedSettings.RollingOutputCooldown=rollingCooldown;
                ImGui.TextWrapped("Requires a manual attack reference and an owner-attributed party counter. Not a live panel read; solo display candidates are excluded. Save combat settings to apply/reset the window.");
            }
            if(ImGui.Button("Reload combat settings"))LoadSaedSettings();
            if(ImGui.Button("Export combat feedback (no chat / key)")){try{counterStatus="Saved: "+combatFeedback.Export(Path.Combine(CompanionChat.ConfigurationDirectory,"combat-feedback"),weaponStateRevision);}catch(Exception ex){counterStatus="Export failed: "+ex.GetType().Name;}}
            float reference=(float)saedSettings.ReferenceDamage,ratio=(float)saedSettings.PraiseRatio,manual=(float)saedSettings.ManualThreshold;
            if(ImGui.InputFloat("Manual reference damage (used only with dynamic OFF)",ref reference))saedSettings.ReferenceDamage=float.IsFinite(reference)?Math.Clamp(reference,1,100000):1200;
            if(ImGui.SliderFloat("Praise fraction",ref ratio,.1f,1,"%.2f"))saedSettings.PraiseRatio=ratio;
            if(ImGui.InputFloat("Force threshold (0 = automatic)",ref manual))saedSettings.ManualThreshold=float.IsFinite(manual)?Math.Clamp(manual,0,100000):0;
            if(ImGui.Button("Save combat settings"))SaveSaedSettings();
            if(ImGui.Button("Auto owner seat")){saedSettings.OwnerSlot=-1;SaveSaedSettings();}
            for(int i=0;i<4;i++){ImGui.SameLine();if(ImGui.Button($"My seat {i+1}")){saedSettings.OwnerSlot=i;SaveSaedSettings();}}
            ImGui.TextWrapped("Owner damage over move-specific windows. Iai also checks gauge loss. Equipment base stats scale with progression; current attack buffs/augments are not fully modeled. Solo fallback can enable the guarded display hook; multiplayer uses owner totals.");
        }
        if(ImGui.Checkbox("Experimental damage observation (no hit praise / no API)",ref damageDiagnostic))
        {
            damageBuffer.Clear();damageAttribution.Reset();
            if(damageDiagnostic)
            {
                damageObserver??=new(damageBuffer,()=>clock.Elapsed.TotalSeconds,()=>Volatile.Read(ref session));
                damageDiagnostic=damageObserver.Start(weaponStateRevision);damageStatus=damageObserver.Status;
                if(damageDiagnostic){enabled=true;scenePaused=false;}
            }
            else {if(!combatFallbackObserverOn)damageObserver?.Stop();damageStatus=combatFallbackObserverOn?"Solo display fallback active":"Damage observer OFF";}
            WriteLog("DAMAGE_OBSERVER "+damageStatus);
        }
        ImGui.TextWrapped(damageStatus);
        if(damageDiagnostic)ImGui.TextUnformatted($"Observed callbacks: {damageObserver?.Callbacks??0}; dropped: {damageBuffer.Dropped}. Owner UNKNOWN; temporal match is not hit attribution.");
        if (ImGui.Checkbox("Automatic companion (requires probe)", ref autoCompanion)) { companionEvents.Reset(); chat.CancelAutomatic(); }
        if (ImGui.Checkbox("Spirit level-up companion (requires probe + automatic companion)", ref spiritCompanion))
        { spirit.Reset(); spiritDirector.Reset(); chat.CancelAutomatic(); }
        ImGui.TextWrapped("Level-up praise only; first sample and level loss are silent. Paid follow-up is optional.");
        ImGui.SliderFloat("Companion cooldown (seconds)", ref companionEvents.CooldownSeconds, 6, 30, "%.0f");
        if (ImGui.Checkbox("Weapon action companion (requires auto companion)", ref actionCompanion)) { actionDirector.Reset(); Interlocked.Exchange(ref pendingAction, null); chat.CancelAutomatic(); }
        ImGui.TextWrapped("Action candidates only: no hit/counter-success detection. Local lines need no API.");
        ImGui.TextUnformatted(status);
        ImGui.TextUnformatted($"HP: {hp:0.0}/{maxHp:0.0} | Weapon: {weapon} | Action: {action}");
        ImGui.TextWrapped("HP decrease is not confirmed attack damage. Action ID is not confirmed hit.");
        ImGui.TextWrapped($"Log: {logPath}");
        foreach (var item in events) ImGui.TextUnformatted(item);
        var font = fontReady ? Renderer.GetFont("WhaleChinese") : null;
        if (font.NativePtr != null)
        {
            ImGui.PushFont(font);
            try { ImGui.TextUnformatted("鲸鱼娘：你好，猎人！");  }
            finally { ImGui.PopFont(); }
        }
        else { ImGui.TextUnformatted("Chinese font unavailable.");  }
        }
        finally {if(uiPushed)ImGui.PopFont();}
    }

    private void ObserveSpirit(nint player)
    {
        if (!spiritDiagnostic && !spiritCompanion) { spirit.Reset(); return; }
        var now = clock.Elapsed.TotalSeconds;
        if (weapon != "LongSword") { spirit.Reset(); spiritStatus = "Unknown: not using long sword"; }
        else if (!SpiritGaugeProbe.TryRead(localMemory, (ulong)player, out var raw, out var readStatus))
        { spirit.Reset(); spiritStatus = readStatus; }
        else
        {
            var change = spirit.Observe((ulong)player, raw, now); spiritStatus = readStatus;
            if (change != null) WriteLog(change.Previous == null ? $"LS_GAUGE_BASELINE candidate={change.Current}" : $"LS_GAUGE_CHANGED old={change.Previous} new={change.Current} candidate=true");
            if (change != null && autoCompanion && spiritCompanion)
            {
                var reaction = spiritDirector.Observe(change, now);
                if (reaction != null)
                {
                    EventLine("spirit_level_up_"+change.Current,"spirit",reaction.Text,reaction.Context); actionQuietUntil = now;
                    expression.Offer(reaction.Emotion, reaction.Intensity, 1, now, 3);
                    WriteLog($"SPIRIT_COMPANION old={change.Previous} new={change.Current}");
                    
                }
            }
        }
        if (spiritStatus != lastSpiritStatus || now >= nextSpiritLog)
        {
            WriteLog($"LS_GAUGE_SAMPLE raw={spirit.Raw?.ToString() ?? "unknown"} stable={spirit.Stable?.ToString() ?? "unknown"} weapon={weapon} status={spiritStatus}");
            lastSpiritStatus = spiritStatus; nextSpiritLog = now + 5;
        }
    }
    private void ObserveWeaponState(nint player)
    {
        if(!weaponStateDiagnostic){weaponStates.Reset();weaponStateDirector.Reset();weaponStateResponses.Reset();return;}
        var now=clock.Elapsed.TotalSeconds;
        WeaponStateProbe.TryRead(localMemory,(ulong)player,weapon,weaponStateRevision,out var snapshot,out weaponStateStatus);
        weaponStates.Observe(snapshot,now);
        if(allWeaponCompanion&&autoCompanion&&actionCompanion&&weaponStates.Ready&&snapshot!=null)
        {
            var observed=weaponStateDirector.Observe(snapshot,now);
            if(observed!=null)weaponStateResponses.Offer(observed,snapshot,now);
            var reaction=weaponStateResponses.Take(snapshot,now,now>=actionQuietUntil&&!chat.Busy);
            if(reaction!=null)
            {
                EventLine(ResponseCategoryOf(reaction.Kind)=="weapon"?weapon+"."+reaction.Kind:reaction.Kind,ResponseCategoryOf(reaction.Kind),reaction.Text,reaction.Context);expression.Offer(reaction.Emotion,reaction.Intensity,1,now,3);actionQuietUntil=now+(saedResponses?2:8);
                WriteLog($"WEAPON_STATE_RESPONSE kind={reaction.Kind} weapon={weapon} experimental=true");
                
            }
        }
        else weaponStateResponses.Reset();
        if(now<nextWeaponStateLog)return;
        nextWeaponStateLog=now+5;
        WriteLog("WEAPON_STATE_CANDIDATE "+System.Text.Json.JsonSerializer.Serialize(new{revision=weaponStateRevision,weapon,status=weaponStateStatus,ready=weaponStates.Ready,fields=snapshot?.Fields,validated=false,hit="unknown"}));
    }
    private void ObserveNativeGuard(nint player)
    {
        if(evasionContacts!=null)
        {
            var responseNow=clock.Elapsed.TotalSeconds;
            bool countEvasions=weaponStateDiagnostic&&allWeaponCompanion&&autoCompanion&&actionCompanion&&enabled&&!scenePaused;
            if(countEvasions)contactEvasionDirector.Synchronize((ulong)player,weapon,responseNow);else contactEvasionDirector.Reset();
            foreach(var candidate in evasionContacts.Drain())
            {
                WriteLog("EVASION_CONTACT_CANDIDATE "+System.Text.Json.JsonSerializer.Serialize(new{candidate,general_evade_success="unknown",ai_response=false}));
                if(!countEvasions)continue;
                long before=contactEvasionDirector.LastCountedId;
                contactEvasionDirector.Observe((ulong)player,weapon,candidate,clock.Elapsed.TotalSeconds,readyToSpeak:false);
                if(contactEvasionDirector.LastCountedId!=before){RecordHunt("effective_evade",$"weapon={weapon}; scope={candidate.Scope}; one distinct evade with native contact/filter evidence",responseNow);}
                if(contactEvasionDirector.LastCountedId!=before)WriteLog("EVASION_NATIVE_COUNT "+System.Text.Json.JsonSerializer.Serialize(new{weapon,candidate.EvasionId,candidate.Scope,count=contactEvasionDirector.Count,window_seconds=EvasionContactDirector.WindowSeconds(weapon),required=ResponseTuning.Thresholds.EvasionCount}));
            }
            // Collect while another line is playing; reset the batch only upon actual delivery.
            responseNow=clock.Elapsed.TotalSeconds;
            if(countEvasions&&responseNow>=actionQuietUntil&&!chat.Busy)
            {
                var contactReaction=contactEvasionDirector.TryRespond((ulong)player,weapon,responseNow);
                if(contactReaction!=null)
                {
                    EventLine(contactReaction.Kind+"_"+(weapon is "Bow" or "DualBlades"?weapon:"common"),"evasion",contactReaction.Text,contactReaction.Context);expression.Offer(contactReaction.Emotion,contactReaction.Intensity,1,responseNow,3);
                    WriteLog("EVASION_NATIVE_RESPONSE "+System.Text.Json.JsonSerializer.Serialize(new{kind=contactReaction.Kind,weapon,window_seconds=EvasionContactDirector.WindowSeconds(weapon),successful_actions=4,cooldown_seconds=0,experimental=true}));
                    
                }
            }
            if(clock.Elapsed.TotalSeconds>=nextEvasionContactLog){nextEvasionContactLog=clock.Elapsed.TotalSeconds+5;WriteLog("EVASION_CONTACT_STATS "+System.Text.Json.JsonSerializer.Serialize(new{evasionContacts.Status,evasionContacts.EpisodeStatus,evasionContacts.ActiveTime,evasionContacts.Calls,evasionContacts.Local,evasionContacts.CategoryCalls,evasionContacts.LocalCategoryReturns,evasionContacts.Dropped,evasionContacts.Throttled,responses=contactEvasionDirector.Responses,count=contactEvasionDirector.Count,window_seconds=EvasionContactDirector.WindowSeconds(weapon),required=ResponseTuning.Thresholds.EvasionCount,response_gates=new{allWeaponCompanion,autoCompanion,actionCompanion,enabled,scenePaused,chatBusy=chat.Busy},ai_response=false}));}
        }
        var researchOutcome=Interlocked.Exchange(ref evasionResearchOutcome,null);if(researchOutcome!=null)WriteLog("EVASION_CODE_RESEARCH "+researchOutcome);
        if(!weaponStateDiagnostic){lastEvasionFlagA=lastEvasionFlagB=null;recentEvasionScope=null;evasionDirector.Reset();previousEvasionTimer=null;guardSample=null;guardDirector.Reset();return;}
        var now=clock.Elapsed.TotalSeconds;
        if(!evasionActionExports.ContainsKey(weapon)&&evasionActionAttempts.GetValueOrDefault(weapon)<3&&now>=nextEvasionActionAttempt&&Interlocked.CompareExchange(ref evasionActionBusy,1,0)==0)
        {
            nextEvasionActionAttempt=now+5;evasionActionAttempts.AddOrUpdate(weapon,1,(_,count)=>count+1);
            var directory=Path.Combine(CompanionChat.ConfigurationDirectory,"combat-feedback");var revision=weaponStateRevision;var actionOwner=(ulong)player;var actionWeapon=weapon;
            _=System.Threading.Tasks.Task.Run(()=>
            {
                try{var report=EvasionCodeResearch.ExportActions(directory,revision,actionOwner,actionWeapon);if(report.Methods>0&&report.CorpusReady)evasionActionExports.TryAdd(actionWeapon,true);Interlocked.Exchange(ref evasionResearchOutcome,"action_code_"+actionWeapon+"_methods_"+report.Methods+"_corpus_"+report.CorpusReady);}
                catch(Exception ex){Interlocked.Exchange(ref evasionResearchOutcome,"action_export_failed_"+ex.GetType().Name);}
                finally{Volatile.Write(ref evasionActionBusy,0);}
            });
        }        if(Interlocked.CompareExchange(ref evasionResearchQueued,1,0)==0)
        {
            var directory=Path.Combine(CompanionChat.ConfigurationDirectory,"combat-feedback");var revision=weaponStateRevision;
            _=System.Threading.Tasks.Task.Run(()=>{try{EvasionCodeResearch.Export(directory,revision);Interlocked.Exchange(ref evasionResearchOutcome,"saved_dispatch_context_candidates");}catch(Exception ex){Interlocked.Exchange(ref evasionResearchOutcome,"failed_"+ex.GetType().Name);}});
        }
        var evasionReadable=NativeEvasionSignal.TryRead(localMemory,(ulong)player,weaponStateRevision,out var evasionTimer);
        var currentEvasionAction=nativeActions.Current;
        var scope=currentEvasionAction!=null&&currentEvasionAction.Owner==(ulong)player&&currentEvasionAction.Weapon==weapon&&now-currentEvasionAction.Time is >=0 and <=.35?EvasionActionScope.Identify(weapon,currentEvasionAction.Set,currentEvasionAction.Id):null;
        if(scope!=null){recentEvasionScope=scope;recentEvasionWeapon=weapon;recentEvasionScopeTime=now;recentEvasionScopeOwner=(ulong)player;}
        bool allowedEvasion=recentEvasionScope!=null&&recentEvasionWeapon==weapon&&recentEvasionScopeOwner==(ulong)player&&now-recentEvasionScopeTime is >=0 and <=.75;
        if(EvasionFlagProbe.TryRead(localMemory,(ulong)player,weaponStateRevision,out var flagA,out var flagB))
        {
            uint maskA=flagA&0x02000000,maskB=flagB&0x02000000;
            if(allowedEvasion&&(lastEvasionFlagA!=maskA||lastEvasionFlagB!=maskB))WriteLog("EVASION_FLAG_CANDIDATE "+System.Text.Json.JsonSerializer.Serialize(new{a=maskA,b=maskB,scope=recentEvasionScope,weapon,action,timer=evasionReadable?(float?)evasionTimer:null,semantics="unvalidated_upstream_flag",general_evade_success="unknown",ai_response=false}));
            lastEvasionFlagA=maskA;lastEvasionFlagB=maskB;
        }
        else {lastEvasionFlagA=null;lastEvasionFlagB=null;}
        if(evasionReadable&&previousEvasionTimer is {} oldTimer&&previousEvasionOwner==(ulong)player&&now>previousEvasionTime&&now-previousEvasionTime<=.75)
        {
            string? change=oldTimer==0&&evasionTimer>0?"activated":oldTimer>0&&evasionTimer==0?"ended":evasionTimer-oldTimer>.5f?"refreshed":null;
            if(change!=null)WriteLog("EVASION_MANTLE_TRANSITION "+System.Text.Json.JsonSerializer.Serialize(new{change,before=oldTimer,after=evasionTimer,weapon,action,component="gear_12608",validated=false,ai_response=false}));
        }
        var evasionReaction=evasionDirector.Observe((ulong)player,weapon,evasionReadable?evasionTimer:null,now,allowedEvasion);
        if(evasionReaction!=null&&allowedEvasion&&allWeaponCompanion&&autoCompanion&&actionCompanion&&now>=actionQuietUntil&&!chat.Busy)
        {
            EventLine(evasionReaction.Kind,"evasion",evasionReaction.Text,evasionReaction.Context);expression.Offer(evasionReaction.Emotion,evasionReaction.Intensity,1,now,3);actionQuietUntil=now+3;
            WriteLog("EVASION_MANTLE_RESPONSE "+System.Text.Json.JsonSerializer.Serialize(new{kind=evasionReaction.Kind,weapon,action,timer=evasionTimer,scope="mantle_buff_only",action_scope=recentEvasionScope}));
            
        }
        previousEvasionTimer=evasionReadable?evasionTimer:null;previousEvasionTime=now;previousEvasionOwner=(ulong)player;
        NativeGuardSignal.TryRead(localMemory,(ulong)player,weapon,weaponStateRevision,out guardSample);
        var reaction=guardDirector.Observe(guardSample,now);
        if(now>=nextGuardLog)
        {WriteLog("EVASION_MANTLE_CANDIDATE "+System.Text.Json.JsonSerializer.Serialize(new{readable=evasionReadable,timer_raw=evasionReadable?(float?)evasionTimer:null,weapon,action,component="gear_12608",flag_a=lastEvasionFlagA,flag_b=lastEvasionFlagB,validated=false,general_evade_success="unknown",ai_response=false})); nextGuardLog=now+5;WriteLog($"NATIVE_GUARD_SAMPLE weapon={weapon} timer_raw={guardSample?.Timer.ToString(System.Globalization.CultureInfo.InvariantCulture)??"unknown"} gp=unknown validated=false");}
        if(reaction==null||!allWeaponCompanion||!autoCompanion||!actionCompanion||now<actionQuietUntil||chat.Busy)return;
        pendingGuardRecoil=null;
        if(saedResponses&&weapon=="ChargeBlade")reaction=reaction with{Text="挡住了，要上了！",Context=reaction.Context+" 这是超解前的起势陪伴，可以按人设说要上了，不断言精确GP。"};
        EventLine(ResponseCategoryOf(reaction.Kind)=="weapon"?weapon+"."+reaction.Kind:reaction.Kind,ResponseCategoryOf(reaction.Kind),reaction.Text,reaction.Context);expression.Offer(reaction.Emotion,reaction.Intensity,1,now,3);actionQuietUntil=now+(saedResponses?1.5:8);
        WriteLog($"NATIVE_GUARD_RESPONSE kind={reaction.Kind} weapon={weapon} action={action} gp=unknown hit=unknown");
        
    }
    private void LoadSaedSettings()
    {
        try
        {
            if(File.Exists(SaedSettingsPath))
            {
                var settings=System.Text.Json.JsonSerializer.Deserialize<SaedResponseSettings>(File.ReadAllText(SaedSettingsPath));
                if(settings==null||!settings.Valid)throw new InvalidDataException("SAED settings out of range");
                saedSettings=settings;
            }
            else {Directory.CreateDirectory(Path.GetDirectoryName(SaedSettingsPath)!);File.WriteAllText(SaedSettingsPath,System.Text.Json.JsonSerializer.Serialize(saedSettings,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
            rollingOutput.Reset();combatVerdicts.Reset();ownerCounter=null;pendingSaedResult=null;counterStatus="Settings loaded; waiting for fresh owner counter";
        }
        catch(Exception ex){counterStatus="Settings failed; previous settings retained: "+ex.GetType().Name;WriteLog("SAED_CONFIG_FAILED "+ex.GetType().Name);}
    }
    private void SaveSaedSettings()
    {
        rollingOutput.Reset();combatVerdicts.Reset();ownerCounter=null;pendingSaedResult=null;
        try{Directory.CreateDirectory(Path.GetDirectoryName(SaedSettingsPath)!);File.WriteAllText(SaedSettingsPath,System.Text.Json.JsonSerializer.Serialize(saedSettings,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
        catch(Exception ex){WriteLog("SAED_CONFIG_SAVE_FAILED "+ex.GetType().Name);}
    }
    private void PollOwnerCounter()
    {
        if(!saedResponses||!enabled||scenePaused||sampledPlayer==0||!WeaponStateProbe.Supports(weapon)||!autoCompanion||!actionCompanion)
        {rollingOutput.Reset();StopCombatFallback();ownerCounter=null;combatFacts=null;combatVerdicts.Reset();pendingSaedResult=null;Interlocked.Exchange(ref combatRequest,null);return;}
        var now=clock.Elapsed.TotalSeconds;if(now<nextCounterPoll)return;nextCounterPoll=now+.1;
        PlayerDamageCounter.TryRead(localMemory,gameModule,weaponStateRevision,saedSettings.OwnerSlot,now,out ownerCounter,out counterStatus);
        bool solo=PlayerDamageCounter.TryPartySize(localMemory,gameModule,weaponStateRevision,out var partySize)&&partySize==0;
        if(saedSettings.SoloDisplayFallback&&solo)
        {
            if(!combatFallbackObserverOn&&now>=nextFallbackAttempt)
            {
                nextFallbackAttempt=now+30;damageObserver??=new(damageBuffer,()=>clock.Elapsed.TotalSeconds,()=>Volatile.Read(ref session));
                combatFallbackObserverOn=damageObserver.Start(weaponStateRevision);
                if(combatFallbackObserverOn){displayedDamage.Reset(session,sampledPlayer,now);lastDamageDropped=damageBuffer.Dropped;damageBuffer.Clear();}
                WriteLog("SOLO_DAMAGE_FALLBACK "+damageObserver.Status);
            }
            if(combatFallbackObserverOn)
            {
                if(!displayedDamage.Matches(session,sampledPlayer)){displayedDamage.Reset(session,sampledPlayer,now);damageBuffer.Clear();lastDamageDropped=damageBuffer.Dropped;}
                ownerCounter=displayedDamage.Sample(now);counterStatus=ownerCounter==null?"Display fallback capture gap; restart probe":"Solo displayed damage candidate: "+ownerCounter.Total;
            }
            else {ownerCounter=null;counterStatus="Solo fallback unavailable: "+(damageObserver?.Status??"waiting");}
        }
        else {StopCombatFallback();if(solo){ownerCounter=null;counterStatus="Solo fallback disabled; party counter is not trusted for solo";}}
        combatFacts=CombatFacts.Read(localMemory,sampledPlayer,weapon,weaponStateRevision,now);
        var rolling=rollingOutput.Observe(ownerCounter,combatFacts,saedSettings,now);
        if(rolling!=null)
        {
            RecordHunt("rolling_output",rolling.Mechanic,now);
            combatFeedback.Record(rolling);WriteLog("ROLLING_OUTPUT_VERDICT "+System.Text.Json.JsonSerializer.Serialize(rolling));
            if(!chat.Busy&&now>=actionQuietUntil){EventLine("rolling_output","damage","这一阵打得真起劲，本鱼给你加饭呦！",rolling.Mechanic+"。持续输出达到人工参考，不说真实DPS或完整面板。");expression.Offer("happy",.6f,1,now,3);actionQuietUntil=now+5;}
        }
        var result=combatVerdicts.Observe(ownerCounter,combatFacts,now);
        if(result!=null){RecordHunt("combat_"+result.Key,$"{result.Label}: damage estimate={result.Damage}; threshold={result.Threshold}; positive_verdict={result.Good}; source={result.DamageSource}; {result.Mechanic}",now);combatFeedback.Record(result);pendingSaedResult=result;WriteLog("COMBAT_DAMAGE_VERDICT "+System.Text.Json.JsonSerializer.Serialize(result));}
        if(now>=nextCounterLog){nextCounterLog=now+5;WriteLog("OWNER_DAMAGE_COUNTER "+System.Text.Json.JsonSerializer.Serialize(new{sample=ownerCounter,status=counterStatus,window=combatVerdicts.Status,equipment=combatFacts.Equipment,fields=combatFacts.Fields}));}
    }
    private void StopCombatFallback()
    {
        if(!combatFallbackObserverOn)return;
        combatFallbackObserverOn=false;nextFallbackAttempt=0;displayedDamage.Reset(-1,0,clock.Elapsed.TotalSeconds);
        if(!damageDiagnostic)damageObserver?.Stop();
    }
    private void DeliverSaedResult()
    {
        var result=pendingSaedResult;if(result==null)return;
        var now=clock.Elapsed.TotalSeconds;
        if(!saedResponses||!enabled||scenePaused||!autoCompanion||!actionCompanion||weapon!=result.Weapon||now-result.Time>5)
        {pendingSaedResult=null;return;}
        if(chat.Busy||now<actionQuietUntil)return;
        pendingSaedResult=null;var reaction=CombatDamageDirector.Reaction(result);
        EventLine(result.Key+(result.Label.Contains("强击")?"_strong":"")+"_"+result.Outcome+"_"+(result.Good?"good":"bad"),"damage",reaction.Text,reaction.Context);expression.Offer(reaction.Emotion,reaction.Intensity,1,now,3);actionQuietUntil=now+5;
        WriteLog($"COMBAT_VERDICT_RESPONSE kind={reaction.Kind} move={result.Key} damage={result.Damage} threshold={result.Threshold:0.#} estimated=true");
        
    }
    private void PollNativeAction()
    {
        if(!enabled||scenePaused||sampledPlayer==0||!(allWeaponCompanion||damageDiagnostic||saedResponses||weaponStateDiagnostic))
        {evasionContacts?.SetContext(null);nativeActions.Reset();pendingGuardRecoil=null;nativeActionStatus="Current-action confirmation OFF / waiting for loaded player";return;}
        var now=clock.Elapsed.TotalSeconds;
        NativeActionSignal.TryRead(localMemory,sampledPlayer,weapon,weaponStateRevision,now,out var current);
        if(weaponStateDiagnostic&&current!=null)
        {
            evasionContacts??=new EvasionContactObserver(()=>clock.Elapsed.TotalSeconds);
            evasionContacts.SetContext(current);evasionContacts.Start(weaponStateRevision);
        }
        else evasionContacts?.SetContext(null);
        var transition=nativeActions.Observe(current);
        nativeActionStatus=current==null?"Current action unknown; experimental action responses wait":$"Current action confirmed: {current.Set}:{current.Id}; not a hit result";
        if(transition==null)return;
        var value=transition.Current;
        if(saedResponses&&autoCompanion&&actionCompanion)
        {
            var request=Volatile.Read(ref combatRequest);
            if(combatVerdicts.Active||CombatDamageDirector.NeedsEveryEntry(value.Weapon,value.Set,value.Id)||now>=nextCombatStart)
            {
                if(combatVerdicts.Enter(transition,request,ownerCounter,saedSettings))
                {
                    nextCombatStart=now+8;
                    WriteLog("COMBAT_WINDOW_START "+System.Text.Json.JsonSerializer.Serialize(new{action=value,before=request?.Before,dynamic=saedSettings.DynamicThreshold,ratio=saedSettings.PraiseRatio,manual=saedSettings.ManualThreshold}));
                    if(value.Weapon=="ChargeBlade"&&!chat.Busy&&now>=actionQuietUntil){EventLine("cb_prepare","weapon","要上了，本鱼看着呢！","盾斧准备阶段，只说准备与起势，不声称命中或GP成功。");expression.Offer("relaxed",.4f,1,now,2);actionQuietUntil=now+1;}
                }
            }
        }
        WriteLog($"NATIVE_ACTION_ENTER weapon={value.Weapon} set={value.Set} id={value.Id} previous={transition.Previous.Set}:{transition.Previous.Id} bracket={transition.Previous.Time:0.000}..{value.Time:0.000} hit=unknown");
        if(damageDiagnostic)
            damageBuffer.Add(new AttackObservation(session,value.Owner,new(value.Weapon,value.Set,value.Id,transition.Previous.Time,value.Owner,true)));
        var recoil=NativeGuardRecoil.Identify(transition);
        if(recoil!=null)
        {pendingGuardRecoil=recoil;WriteLog("NATIVE_GUARD_RECOIL "+System.Text.Json.JsonSerializer.Serialize(new{candidate=recoil,gp="unknown",validated=false}));}
    }
    private void PollTenderize()
    {
        if(!enabled||scenePaused||sampledPlayer==0||!saedResponses){tenderizeTracker.Reset();monsterBreakTracker.Reset();monsterEncounterTrace.Reset();monsterStateDirector.Reset();return;}
        double now=clock.Elapsed.TotalSeconds;if(now<nextTenderizePoll)return;nextTenderizePoll=now+.25;
        try
        {
            var monsters=Monster.GetAllMonsters().Take(16).Select(monster=>(ulong)monster.Instance).ToArray();
            int encounterRecords=0,ailmentRecords=0;
            foreach(var monster in monsters)
            {
                var encounter=MonsterEncounterProbe.Read(localMemory,monster,gameModule,weaponStateRevision,now);
                if(encounter!=null){encounterRecords++;ailmentRecords+=encounter.Ailments.Length;monsterStateDirector.Observe(encounter);}
                if(monsterResearchLogging&&encounter!=null&&monsterEncounterTrace.Observe(encounter) is {} trace)WriteLog("MONSTER_ENCOUNTER_TRACE "+System.Text.Json.JsonSerializer.Serialize(trace));
            }
            var parts=monsters.SelectMany(monster=>MonsterBreakProbe.Read(localMemory,monster,weaponStateRevision,now)).ToArray();
            foreach(var change in monsterBreakTracker.Observe(parts,now))
            {
                monsterStateDirector.Add("part_break",now);
                WriteLog("MONSTER_BREAK_CANDIDATE "+System.Text.Json.JsonSerializer.Serialize(new{change,owner="unknown",validated=false,response_queued=true,severed_parts=false}));
            }
            var samples=monsters.SelectMany(monster=>TenderizeProbe.Read(localMemory,monster,weaponStateRevision,now)).ToArray();
            if(monsterResearchLogging&&now>=nextMonsterStateStats){nextMonsterStateStats=now+5;foreach(var monster in monsters)WriteLog("MONSTER_PART_LAYOUT "+System.Text.Json.JsonSerializer.Serialize(MonsterBreakProbe.Inspect(localMemory,monster)));WriteLog("MONSTER_STATE_STATS "+System.Text.Json.JsonSerializer.Serialize(new{monsters=monsters.Length,encounter_records=encounterRecords,ailment_records=ailmentRecords,tenderize_records=samples.Length,breakable_records=parts.Length,part_counters=parts.Select(p=>new{p.MonsterType,p.Part,p.Counter,p.Thresholds}),owner="unknown",ai_response=false}));}
            foreach(var change in tenderizeTracker.Observe(samples,now))
            {
                monsterStateDirector.Add("tenderize",now);
                WriteLog("TENDERIZE_TRANSITION "+System.Text.Json.JsonSerializer.Serialize(new{change,owner="unknown",validated=false,response_queued=true}));
            }
        }
        catch(Exception ex){tenderizeTracker.Reset();monsterBreakTracker.Reset();nextTenderizePoll=now+10;WriteLog("TENDERIZE_READ_FAILED "+ex.GetType().Name);}
    }
    private void ProcessGuardRecoil()
    {
        if(!allWeaponCompanion||!autoCompanion||!actionCompanion){pendingGuardRecoil=null;return;}
        var value=pendingGuardRecoil;if(value==null)return;
        var now=clock.Elapsed.TotalSeconds;var age=now-value.Time;
        if(age<0||age>2||value.Owner!=sampledPlayer||value.Weapon!=weapon){pendingGuardRecoil=null;return;}
        // Give the more specific native skill result a chance to arrive first.
        if(age<.4||now<nextRecoilResponse||now<actionQuietUntil||chat.Busy)return;
        pendingGuardRecoil=null;nextRecoilResponse=now+ResponseTuning.Cooldown("guard",15);
        var reaction=NativeGuardRecoil.Reaction(value);
        if(saedResponses&&weapon=="ChargeBlade")reaction=reaction with{Text="接住了，要上了！",Context=reaction.Context+" 可以说要上了，作为防御后起势陪伴；不强调精确GP。"};
        EventLine(ResponseCategoryOf(reaction.Kind)=="weapon"?weapon+"."+reaction.Kind:reaction.Kind,ResponseCategoryOf(reaction.Kind),reaction.Text,reaction.Context);expression.Offer(reaction.Emotion,reaction.Intensity,1,now,3);actionQuietUntil=now+(saedResponses?1.5:8);
        WriteLog($"GUARD_RECOIL_RESPONSE weapon={weapon} id={value.RecoilId} gp=unknown");
        
    }
    private void ProcessDamageObservations()
    {
        if(!damageDiagnostic&&!combatFallbackObserverOn){damageBuffer.Clear();return;}
        var now=clock.Elapsed.TotalSeconds;
        if(now>=nextDamageLogBudget){nextDamageLogBudget=now+1;damageLogBudget=32;}
        var captureGap=damageBuffer.Dropped!=lastDamageDropped;
        if(captureGap){lastDamageDropped=damageBuffer.Dropped;damageAttribution.MarkGap();displayedDamage.Gap();}
        foreach(var fact in damageBuffer.Drain())
        {
            if(fact is AttackObservation a){if(damageDiagnostic&&a.Epoch==session&&enabled&&!scenePaused)damageAttribution.Action(a);}
            else if(fact is DamageObservation d&&d.Epoch==session&&enabled&&!scenePaused)
            {
                if(combatFallbackObserverOn)displayedDamage.Add(d);
                if(damageDiagnostic){damageAttribution.Damage(d);if(damageLogBudget-->0)WriteLog("DAMAGE_RAW "+System.Text.Json.JsonSerializer.Serialize(d));else damageLogSuppressed++;}
            }
        }
        if(captureGap||damageBuffer.Dropped!=lastDamageDropped){lastDamageDropped=damageBuffer.Dropped;damageAttribution.MarkGap();displayedDamage.Gap();}
        if(!damageDiagnostic)return;
        foreach(var result in damageAttribution.Finish(session,now))WriteLog("DAMAGE_ATTRIBUTION "+System.Text.Json.JsonSerializer.Serialize(result));
        if(now>=nextDamageStats)
        {nextDamageStats=now+5;WriteLog($"DAMAGE_STATS callbacks={damageObserver?.Callbacks??0} dropped={damageBuffer.Dropped} logs_suppressed={damageLogSuppressed} unmatched={damageAttribution.Unattributed} ambiguous={damageAttribution.Ambiguous} source_owner=unknown");}
    }
    private void MarkSpirit(string expected)
    {
        var age = clock.Elapsed.TotalSeconds - spirit.SampleTime;
        if (!enabled || scenePaused || !spiritDiagnostic || weapon != "LongSword" || spirit.Raw == null || !double.IsFinite(age) || age < 0 || age > .5)
        {
            var reason = SpiritDiagnosticGate.Blocked(enabled, scenePaused) ?? (!spiritDiagnostic ? "Diagnostic disabled" : weapon != "LongSword" ? $"Not using long sword: {weapon}" : $"No fresh sample: {spiritStatus}; {status}");
            spiritMarkFeedback = $"Mark {expected} NOT saved: {reason}";
            WriteLog($"LS_GAUGE_MARK_REJECTED expected={expected} reason={reason}"); return;
        }
        spiritMarkFeedback = $"Mark {expected} saved: raw={spirit.Raw}, stable={spirit.Stable?.ToString() ?? "pending"}";
        WriteLog($"LS_GAUGE_MARK expected={expected} raw={spirit.Raw} stable={spirit.Stable} age={age:0.000} session={session}");
    }

    private void TestFace(string kind) { var now = clock.Elapsed.TotalSeconds; facePlayback.Preview(kind, now, slowFacePreview); expression.PostponeBlink(now + (slowFacePreview ? 7 : 5)); WriteLog($"FACE_MANUAL_TRANSITION kind={kind} slow={slowFacePreview}"); }

    private static string ResponseCategoryOf(string id)=>id.StartsWith("spirit_")?"spirit":id.StartsWith("horn_effect")||id.StartsWith("weapon_state")?"buff":id.Contains("guard")?"guard":id.Contains("evade")||id.Contains("evasion")?"evasion":"weapon";
    private void EventLine(string id,string category,string fallback,string facts)
    {
        double now=clock.Elapsed.TotalSeconds;
        if(category is not ("monster" or "damage")&&id!="palico_recovered")RecordHunt(id,facts,now);
        if(!responses.Accept(id,category,fallback,"用当前外置人设说一句简短陪伴。仅评价给定事实，不编造主语、命中、来源或额外成功。",now,out var rule))return;
        string fixedText=string.IsNullOrWhiteSpace(rule.OfflineText)?fallback:rule.OfflineText;
        void Deliver(string line){responses.Record(id,line);LocalLine(line);}
        if(!responses.Settings.DynamicAi){Deliver(fixedText);return;}
        int epoch=session;string currentWeapon=weapon;
        var prompt=$"状态事件键：{id}。外置事件话术要求：{rule.Prompt}\n只读事实与不可跨越的边界：{facts}\n离线措辞参考（可随人设改写）：{fixedText}\n只回复一句角色对白，保持指定JSON回复格式。";
        if(!chat.StartDynamicEvent(prompt,()=>responses.IsEnabled(id)&&responses.Settings.DynamicAi&&enabled&&!scenePaused&&autoCompanion&&epoch==session&&clock.Elapsed.TotalSeconds-now<=12&&(category!="weapon"&&category!="damage"||weapon==currentWeapon),line=>Deliver(string.IsNullOrWhiteSpace(line)?fixedText:line)))Deliver(fixedText);
    }
    private void LocalLine(string text)
    {
        chat.CancelAutomatic(); chat.SayLocal(text); lineUntil = clock.Elapsed.TotalSeconds + 8;
    }
    private void HandleEvent(string? kind)
    {
        if (kind == null) return;
        actionQuietUntil = clock.Elapsed.TotalSeconds + 8;
        Add($"Companion event {kind}");
        if(kind=="health_recovered")
        {
            expression.Offer("happy",.5f,1,clock.Elapsed.TotalSeconds,3);
            EventLine("health_recovered","health","得救啦，状态大健康！","玩家HP明显恢复，只说恢复状态，不说是谁用了什么道具。");
            WriteLog("HEALTH_RECOVERED source=unknown item=unknown use_completed=unknown");
            return;
        }
        expression.Offer("concerned", kind == "low_health" ? 0.9f : 0.6f, kind == "low_health" ? 3 : 2, clock.Elapsed.TotalSeconds, 5);
        if (kind == "low_health") { EventLine("low_health","health",LowLines[lowLine++ % LowLines.Length], $"玩家HP比例{hp/maxHp:P0}，低于外置准备阈值{ResponseTuning.Thresholds.LowHealthRatio:P0}，简短提醒照顾状态，不编造原因。"); return; }
        EventLine("health_loss","health",LossLines[lossLine++ % LossLines.Length],$"玩家在{ResponseTuning.Thresholds.HealthLossWindow}秒内掉血至少{ResponseTuning.Thresholds.HealthLossRatio:P0}，一句关心，不编造来源或被何招击中。");
    }
    private void ProcessAction()
    {
        var sample = Volatile.Read(ref pendingAction);
        var now = clock.Elapsed.TotalSeconds;
        if(sample!=null&&(!double.IsFinite(sample.Time)||now<sample.Time||now-sample.Time>.75))
        {Interlocked.CompareExchange(ref pendingAction,null,sample);return;}
        if (sample == null || !enabled || scenePaused || !autoCompanion || !actionCompanion || now < actionQuietUntil || chat.Busy) return;
        if(saedResponses&&CombatProfiles.Handles(sample.Weapon,sample.Set,sample.Id)&&!WeaponActions.AllowsAlongsideDamage(sample.Weapon,sample.Set,sample.Id)){Interlocked.CompareExchange(ref pendingAction,null,sample);return;}
        if(allWeaponCompanion&&!NativeActionSignal.Confirms(sample,nativeActions.LatestTransition,now))return;
        if(Interlocked.CompareExchange(ref pendingAction,null,sample)!=sample)return;
        if(allWeaponCompanion)sample=sample with{Entered=true};
        var reaction = actionDirector.Observe(sample, now);
        if (reaction == null) return;
        EventLine(ResponseCategoryOf(reaction.Kind)=="weapon"?weapon+"."+reaction.Kind:reaction.Kind,ResponseCategoryOf(reaction.Kind),reaction.Text,reaction.Context);
        expression.Offer(reaction.Emotion, reaction.Intensity, 1, now, 2.5);
        WriteLog($"ACTION_COMPANION kind={reaction.Kind} set={sample.Set} id={sample.Id} hit=unknown");
    }

    public unsafe void OnImGuiFreeRender()
    {
        RenderChatStatus();
        if (nativeOutput) return; // Native mode owns reply display, including queued delivery.
        if (clock.Elapsed.TotalSeconds > lineUntil && !chat.Busy) return;
        if (string.IsNullOrEmpty(chat.LastLine)) return;
        var font = fontReady ? Renderer.GetFont("WhaleChinese") : null;
        ImGui.SetNextWindowBgAlpha(0.8f);
        ImGui.Begin("Whale companion subtitle", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing);
        if (font.NativePtr != null) ImGui.PushFont(font);
        try { ImGui.TextWrapped(NativeDialogue.Format(chat.LastLine)); }
        finally { if (font.NativePtr != null) ImGui.PopFont(); ImGui.End(); }
    }

    private unsafe void ChineseText(string text)
    {
        var font=fontReady?Renderer.GetFont("WhaleChinese"):null;
        if(font.NativePtr!=null)ImGui.PushFont(font);
        try{ImGui.TextWrapped(text);}finally{if(font.NativePtr!=null)ImGui.PopFont();}
    }
    private ChatSendResult SharedSender(string text,bool checkOnly,out string status)
    {
        var gui=SingletonManager.GetSingleton("sMhGUI");
        var widget=Gui.FindObject("uGUIChat");
        return GameChatSender.TrySend(chatMemory,(ulong)(gui?.Instance??0),(ulong)(widget?.Instance??0),text,out status,checkOnly);
    }
    private void CheckSharedSender()
    {
        try {var result=SharedSender("",true,out shareStatus);WriteLog($"SHARED_CHAT_CHECK result={result} status={shareStatus}");}
        catch(Exception ex){shareStatus="发送器检查失败："+ex.GetType().Name;WriteLog("SHARED_CHAT_CHECK_FAILED "+ex.GetType().Name);}
    }
    private void ProcessSharedChat()
    {
        if(!shareEnabled||!shareSceneReady)return;
        var now=clock.Elapsed.TotalSeconds;var before=sharedChat.Count;var line=sharedChat.Peek(now);
        if(sharedChat.Count<before)shareStatus="共享消息等待超过20秒，已取消；本机回复保留";
        if(line==null)return;
        try
        {
            if(!HasLoadedSave())return;
            var result=SharedSender(line.Text,false,out shareStatus);
            if(result==ChatSendResult.Submitted){sharedChat.Submitted(now);WriteLog($"SHARED_CHAT_SUBMITTED id={line.Id} utf8_bytes={System.Text.Encoding.UTF8.GetByteCount(line.Text)}");}
            else if(result==ChatSendResult.Unavailable){nativeDialogue.Enqueue(line.Text,now);shareEnabled=false;sharedChat.Clear();WriteLog("SHARED_CHAT_STOPPED status="+shareStatus);}
        }
        catch(Exception ex){nativeDialogue.Enqueue(line.Text,now);shareEnabled=false;sharedChat.Clear();shareStatus="共享已停止："+ex.GetType().Name;WriteLog("SHARED_CHAT_STOPPED "+ex.GetType().Name);}
    }

    private unsafe void RenderChatStatus()
    {
        if(!nativeChatEnabled || !showChatStatus)return;
        var font=fontReady ? Renderer.GetFont("WhaleChinese") : null;
        ImGui.SetNextWindowBgAlpha(.7f);
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(20,150),ImGuiCond.FirstUseEver);
        ImGui.Begin("Whale chat status",ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing);
        if(font.NativePtr!=null)ImGui.PushFont(font);
        try
        {
            ImGui.TextUnformatted(nativeInput.Summary(clock.Elapsed.TotalSeconds,nativeChatSceneReady,chat.Busy));
            if(nativeInput.Feedback.Length>0)ImGui.TextUnformatted(nativeInput.Feedback);
            if(readPeerChat)ImGui.TextUnformatted($"队友引用：{peerContext.Count} 条 | 接受 {Interlocked.Read(ref peerAccepted)}/{Interlocked.Read(ref peerCandidates)} | 拒绝原因：{peerLastReject}");
            ImGui.TextUnformatted(chat.NativeStatus);
            ImGui.TextUnformatted(deliveryStatus);
            if(shareEnabled)ImGui.TextUnformatted(shareStatus);
        }
        finally { if(font.NativePtr!=null)ImGui.PopFont();ImGui.End(); }
    }

    private void FlushNativeDialogue()
    {
        if (!nativeOutput||shareEnabled||nativeDialogue.Count==0||clock.Elapsed.TotalSeconds<nextNativeDeliveryPoll) return;
        nextNativeDeliveryPoll=clock.Elapsed.TotalSeconds+.1;
        try
        {
            var ready = nativeSceneReady && HasLoadedSave() && SingletonManager.GetSingleton("sChat") is not null;
            if (!ready) nativeStatus = "Waiting for loaded save; resume native output after loading";
            var oldExpired=nativeDialogue.ExpiredCount;
            var line = nativeDialogue.Take(clock.Elapsed.TotalSeconds, ready);
            if(nativeDialogue.ExpiredCount!=oldExpired){deliveryStatus="原生界面未就绪：旧回复已过期";WriteLog("NATIVE_DISPLAY_EXPIRED hud_ready="+ready);}
            if (line == null) return;
            // SPL's local sChat display API, not the network send/chat-input path.
            if (nativeInterop == null)
            {
                // Reuse the address SPL already resolved; no hardcoded address or new hook.
                // Reflection only obtains NativePointer, never invokes its string-typed calli.
                var field = typeof(Gui).GetField("DisplayMessageFunc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?? throw new MissingFieldException("SPL display function unavailable");
                var function = field.GetValue(null) ?? throw new InvalidOperationException("No SPL display function");
                var pointer = function.GetType().GetProperty("NativePointer")?.GetValue(function);
                if (pointer is not nint address || address == 0) throw new InvalidOperationException("No SPL native display address");
                nativeInterop = new(address);
                WriteLog("NATIVE_UTF8_READY source=SPL.DisplayMessageFunc.NativePointer encoding=UTF-8 retained_buffers=64");
            }
            nativeInterop.Display(SingletonManager.GetSingleton("sChat")!.Instance, line.Text);
            nativeDeliveredId = line.Id;
            nativeStatus = "Local native message displayed"; deliveryStatus="已提交到游戏原生消息区域";
            WriteLog($"NATIVE_DISPLAY id={line.Id} chars={line.Text.Length} route=explicit_utf8 local=true");
        }
        catch (Exception ex)
        {
            nativeOutput = false; nativeChatEnabled=false;nativeInput.Clear();chat.CancelNative();nativeDialogue.Clear(); nativeDeliveredId = -1; deliveryStatus="原生显示失败，已停止接收聊天";
            nativeStatus = $"Native output stopped: {ex.GetType().Name}; subtitle fallback active";
            WriteLog($"NATIVE_DISPLAY_STOPPED exception={ex.GetType().Name}");
        }
    }

    private void StartBattleCapture()
    {
        try
        {
            battleObserver??=new(battleCapture,gmdMemory,()=>clock.Elapsed.TotalSeconds);
            battleObserver.Initialize();battleCapture.Start(clock.Elapsed.TotalSeconds);palicoCapture.Start(clock.Elapsed.TotalSeconds,3600);
            nextBattleStats=0;battleStatus="已开启60分钟观察；正常战斗即可，看到原生台词时可标记";
            WriteLog("PALICO_BATTLE_START seconds=3600 route=SPL_local_display palico_route=unverified no_api=true");
        }
        catch(Exception ex){battleCapture.Stop();battleStatus="观察入口未启用："+ex.GetType().Name;WriteLog("PALICO_BATTLE_START_FAILED "+ex.GetType().Name);}
    }
    private void MarkBattleLine(string kind)
    {
        WriteLog($"PALICO_BATTLE_MARK kind={kind} observation=user_report time={clock.Elapsed.TotalSeconds:0.000} "+System.Text.Json.JsonSerializer.Serialize(battleCapture.Stats(clock.Elapsed.TotalSeconds)));
        battleStatus="已记录刚看到的台词类别："+kind+"（这是手动标记，不代表自动识别成功）";
    }
    private void WriteLog(string message)
    {
        try { File.AppendAllText(logPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private void Reset(bool clearNative=false) { responses.ResetScene(); palicoHealthProbe.Reset();palicoRecovery.Reset();pendingCatRecovery.Reset();monsterBreakTracker.Reset();tenderizeTracker.Reset();monsterEncounterTrace.Reset();monsterStateDirector.Reset(); if(clearNative){sharedChat.Clear();ClearPeerChat(false);teamProbe.Stop();nativeChatEpoch++;nativeInput.Clear();chat.CancelNative();nativeDialogue.Clear();nativeDeliveredId=-1;} sampledPlayer=0;contactEvasionDirector.Reset();evasionContacts?.SetContext(null);nativeActions.Reset();pendingGuardRecoil=null;nextRecoilResponse=0;guardDirector.Reset();guardSample=null;weaponStateDirector.Reset();weaponStateResponses.Reset(); damageAttribution.Reset(); damageBuffer.Clear(); weaponStates.Reset(); spiritDirector.Reset(); spirit.Reset(); spiritStatus = spiritDiagnostic ? "Waiting for fresh scene/player sample" : "Diagnostic disabled"; facePlayback.Reset(clock.Elapsed.TotalSeconds); expression.Reset(clock.Elapsed.TotalSeconds); chat.UpdateGameContext("未提供实时状态，可能正在切换场景或探针已关闭。"); session++; actionDirector.Reset(); Interlocked.Exchange(ref pendingAction, null); actionQuietUntil = 0; companionEvents.Reset(); chat.CancelAutomatic(); lineUntil = 0; baseline = false; hp = maxHp = 0; action = weapon = "-"; status = "Waiting for player"; }
    private void Add(string text) { WriteLog(text); if (events.Count == 12) events.Dequeue(); events.Enqueue($"{DateTime.Now:HH:mm:ss} {text}"); }
}












































