using WhaleCompanion;
int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
var reply = CompanionReply.Parse("{\"text\":\"你好\",\"emotion\":\"happy\",\"intensity\":9}");
Check(reply.Text == "你好" && reply.Intensity == 1, "structured reply clamps intensity");
Check(CompanionReply.Parse("{\"text\":\"hi\",\"emotion\":\"arbitrary_code\"}").Emotion == "neutral", "emotion allowlist");
Check(CompanionReply.Parse("plain reply").Text == "plain reply", "plain fallback");
Check(CompanionReply.Parse("{broken").Text == "", "malformed JSON discarded");
var director = new CompanionExpression();
director.Offer("concerned", .9f, 3, 0, 5);
director.Offer("happy", 1, 1, 0);
for(int n=1;n<=10;n++) director.Update(n*.1);
Check(director.Emotion == "concerned" && director.Weight > .8f, "urgent preempts AI");
for(int n=11;n<=60;n++) director.Update(n*.1);
Check(director.Emotion == "neutral" && director.Weight == 0, "expiry neutral");
for(int n=61;n<=1000;n++) { director.Update(n*.01+6); Check(director.Blink >= 0 && director.Blink <= 1, "blink bounded"); }
director.Reset(20);
Check(director.Emotion == "neutral" && director.Blink == 0, "reset");
for (int selected=56000; selected<FaceSelection.FirstGroup+FaceSelection.StateCount; selected++)
{
    int visible=0;
    for (int group=56000; group<FaceSelection.FirstGroup+FaceSelection.StateCount; group++) { Check(FaceSelection.TryEvaluate(group, selected, out var show), "owned group handled"); if(show) visible++; }
    Check(visible==1, "exactly one face visible");
}
foreach(int group in new[]{0,1,30,63,55999,56041,65535}) Check(!FaceSelection.TryEvaluate(group,56000,out _), "unrelated group forwarded");
Check(FaceSelection.Select("happy",1,0)==56016,"happy routing");
Check(FaceSelection.Select("neutral",0,1)==56008,"blink routing");
Check(FaceSelection.Select("concerned",1,0)==56021,"concern maps to mild sad");
Check(FaceSelection.Select("happy",float.NaN,float.PositiveInfinity)==56000,"non-finite safe");
foreach(int fps in new[]{15,30,60,144})
{
    var timer = new CompanionExpression(); var playback = new FacePlayback();
    var middle = new HashSet<int>();
    for(int frame=0;frame<=fps*14;frame++)
    {
        double now=frame/(double)fps; timer.Update(now);
        playback.Update(now,true,timer.Emotion,timer.Weight,timer.Blink);
        if(playback.Group>56000 && playback.Group<56008) middle.Add(playback.Group);
    }
    Check(playback.CompletedBlinks>=2,$"natural blinks run at {fps}fps");
    Check(playback.LastBlinkPeak==1 && playback.LastClosedFrames>=2,$"full closure persists at {fps}fps");
    Check(middle.Count>=3,$"visible intermediate eyelids at {fps}fps");
    foreach(string kind in new[]{"happy","blink"})
    {
        var manual=new FacePlayback(); manual.Preview(kind,0,slow:true);
        var levels=new HashSet<int>(); float previous=0; float maxStep=0;
        for(int frame=0;frame<=fps*5;frame++)
        {
            manual.Update(frame/(double)fps,false,"neutral",0,0);
            var value=kind=="happy"?manual.Happy:manual.Blink;
            maxStep=Math.Max(maxStep,Math.Abs(value-previous)); previous=value;
            levels.Add(manual.Group);
        }
        Check(levels.Count==9,$"all manual intermediate levels {kind} {fps}fps");
        Check(maxStep<.15f,$"no instant full weight jump {kind} {fps}fps");
        Check(manual.Group==56000 && !manual.ManualActive,$"manual returns to neutral {kind}");
    }
    var smiling=new FacePlayback();
    for(int frame=0;frame<=fps*2;frame++) smiling.Update(frame/(double)fps,true,"happy",1,1);
    Check(smiling.Group==56016,"smiling mouth not replaced by blink");
}
// A delayed frame used to skip a wall-clock-only blink; bounded phase must still close.
var stalled=new CompanionExpression();var stalledPlayback=new FacePlayback();
for(int n=0;n<100;n++) { stalled.Update(n*.25);stalledPlayback.Update(n*.25,true,stalled.Emotion,stalled.Weight,stalled.Blink); }
Check(stalledPlayback.CompletedBlinks>0 && stalledPlayback.LastClosedFrames>=2,"delayed updates retain full closure");
// New authored expressions, safe intensity mapping, and normal-speed playback.
foreach(var (emotion,fullGroup) in new[]{("happy",56016),("sad",56024),("angry",56032),("relaxed",56040)})
{
    Check(FaceSelection.Select(emotion,1,0)==fullGroup,$"{emotion} full group");
    var play=new FacePlayback();play.Preview(emotion,0);
    var levels=new HashSet<int>();float previousWeight=0;float largestJump=0;
    for(int frame=0;frame<=120;frame++)
    {
        play.Update(frame/60.0,false,"neutral",0,0);levels.Add(play.Group);
        largestJump=Math.Max(largestJump,Math.Abs(play.Weight-previousWeight));previousWeight=play.Weight;
        Check(float.IsFinite(play.Weight) && play.Weight>=0 && play.Weight<=1,"finite normal-speed output");
    }
    Check(levels.Count==(emotion=="relaxed"?2:9),$"{emotion} visits safe samples at 60fps");
    Check(largestJump<.15f,$"{emotion} gradual normal-speed playback");
    Check(play.Group==56000 && !play.ManualActive,$"{emotion} normal preview completes in 2s");
}
Check(FaceSelection.Select("concerned",1,0)==56021,"concern strength reduced once");
Check(FaceSelection.Select("unknown",1,0)==56000,"unknown face neutral");
for(int group=56001;group<=56040;group++) Check(FaceSelection.IsIntermediate(group)==((group-56000)%8!=0),"intermediate query classification");
var changing=new FacePlayback();
for(int frame=0;frame<=60;frame++) changing.Update(frame/60.0,true,"happy",1,0);
changing.Preview("angry",1);
bool neutralSeen=false;
for(int frame=61;frame<=90;frame++)
{
    changing.Update(frame/60.0,false,"neutral",0,0);
    if(changing.Group==56000)neutralSeen=true;
    if(changing.Emotion=="angry" && changing.Weight>.2f)Check(neutralSeen,"emotion identity passes through neutral");
}
Check(changing.Emotion=="angry" && changing.Weight==1,"interrupted preview reaches new emotion");
var normalBlinkTimer=new CompanionExpression();normalBlinkTimer.PostponeBlink(0);var normalBlinkPlay=new FacePlayback();
int firstActive=-1,completedAt=-1;
for(int frame=0;frame<60;frame++)
{
    double now=frame/60.0;normalBlinkTimer.Update(now);normalBlinkPlay.Update(now,true,normalBlinkTimer.Emotion,normalBlinkTimer.Weight,normalBlinkTimer.Blink);
    if(normalBlinkPlay.Group>56000 && firstActive<0)firstActive=frame;
    if(normalBlinkPlay.CompletedBlinks>0){completedAt=frame;break;}
}
Check(firstActive>=0 && completedAt>firstActive && (completedAt-firstActive)/60.0<.28,"normal blink shortened at 60fps");
Check(normalBlinkPlay.LastClosedFrames>=2 && normalBlinkPlay.LastBlinkPeak==1,"shortened blink preserves full closure");
var actions = new ActionCompanion();
foreach(var sample in new[] {
    new ActionObservation("GreatSword",3,98,0), new ActionObservation("LongSword",1,98,0),
    new ActionObservation("LongSword",3,99,0), new ActionObservation("LongSword",3,92,0),
    new ActionObservation("LongSword",3,98,-2), new ActionObservation("LongSword",3,98,2),
    new ActionObservation("LongSword",3,98,double.NaN) })
    Check(actions.Observe(sample,0)==null,"reject wrong weapon/set, loop/landing, stale/future/invalid samples");
foreach(var id in new[]{98,101,102,86}) {
    actions.Reset();var a=actions.Observe(new("LongSword",3,id,0),0);
    Check(a!=null && a.Context.Contains("不知道是否命中") && a.Intensity>0,"candidate action grounded in limited evidence");
    Check(actions.Observe(new("LongSword",3,86,1),1)==null,"global cooldown suppresses different action spam");
    Check(actions.Observe(new("LongSword",3,id,20),20)==null,"same action extra suppression");
    Check(actions.Observe(new("LongSword",3,id,31),31)!=null,"same action eventually recovers");
}
actions.Reset();actions.Observe(new("LongSword",3,98,0),0);
Check(actions.Observe(new("LongSword",3,101,18),18)!=null,"different action resumes at cooldown boundary");
actions.Reset();Check(actions.Observe(new("LongSword",3,98,0),0)!=null,"session reset clears cooldown");
actions.Reset();actions.CooldownSeconds=double.NaN;actions.Observe(new("LongSword",3,98,0),0);
Check(actions.Observe(new("LongSword",3,101,1),1)==null,"invalid cooldown retains safe default");
for(int n=1;n<=8;n++) Check(FaceSelection.Select("relaxed",n/8f,0)==56040,"all relaxed intensities use safe terminal");
Check(FaceSelection.Select("relaxed",0,0)==56000,"zero relaxed remains neutral");
var config = ChatConfiguration.Load("D:/homura/codex/MHW/dist/WhaleCompanion/config");
Check(config.Persona.Contains("嘴硬") && config.Memory.Contains("homura"),"external Chinese persona and memory load");
Check(config.Settings.ContextTurns==12 && config.Settings.Model=="deepseek-flash" && !config.Settings.UseJsonMode,"external defaults");
var chatHistory=new List<ChatMessage>();
for(int n=0;n<20;n++){chatHistory.Add(new("user",$"question{n}"));chatHistory.Add(new("assistant",$"answer{n}"));}
var built=config.BuildMessages(chatHistory,"current","HP=100/200");
Check(built.Count==26 && built[1].content=="question8" && built[^2].content=="answer19" && built[^1].content=="current","keep 12 complete most recent turns in order");
Check(built[0].content.Contains("HP=100/200") && built[0].content.Contains("只输出JSON对象"),"state and JSON contract accompany persona");
var zero=config with {Settings=config.Settings with {ContextTurns=0}};
Check(zero.BuildMessages(chatHistory,"current","").Count==2,"zero context excludes all history");
var limited=config with {Settings=config.Settings with {MaxContextCharacters=2048}};
var big=new List<ChatMessage>{new("user",new string('a',1000)),new("assistant",new string('b',1000)),new("user","recent"),new("assistant","answer")};
var cut=limited.BuildMessages(big,new string('c',100),"");
Check(cut.Count==4 && cut[1].content=="recent","character budget drops old pairs intact");
Check(config.BuildMessages(Array.Empty<ChatMessage>(),"new","").Count==2,"new conversation carries no episodic history");
Check(config.BuildMessages(chatHistory,"a","")[0].content.Contains(config.Memory),"manual memory independent of conversation history");
foreach(var invalid in new[]{new ChatSettings{ContextTurns=-1},new ChatSettings{ContextTurns=51},new ChatSettings{MaxContextCharacters=0},new ChatSettings{MaxReplyTokens=5000},new ChatSettings{Model="bad\nmodel"}}) {
 bool rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid settings rejected");
}
var fixture="D:/homura/codex/MHW/research/chat/config-check-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(fixture);
Check(ChatConfiguration.Load(fixture).Persona.Length>0,"missing files use defaults");
File.WriteAllText(Path.Combine(fixture,"settings.json"),"{broken");
bool badJson=false;try{ChatConfiguration.Load(fixture);}catch(System.Text.Json.JsonException){badJson=true;}Check(badJson,"broken JSON rejected before applying");
File.WriteAllText(Path.Combine(fixture,"settings.json"),"{}");File.WriteAllText(Path.Combine(fixture,"persona.md"),new string('x',16001));
bool oversized=false;try{ChatConfiguration.Load(fixture);}catch(InvalidDataException){oversized=true;}Check(oversized,"oversized persona rejected");
File.WriteAllText(Path.Combine(fixture,"persona.md"),"鲸鱼娘");File.WriteAllText(Path.Combine(fixture,"memory.md"),"白米饭");
Check(ChatConfiguration.Load(fixture).Memory=="白米饭","reload reads changed UTF8 file");
foreach(bool on in new[]{false,true}) {
 Check(AccessorySelection.TryEvaluate(55000,on,out var shown)&&shown==on,"pot follows independent visibility flag");
 foreach(int g in new[]{0,1,54999,55001,56000,56008,56036,56040,65535}) Check(!AccessorySelection.TryEvaluate(g,on,out _),"accessory director does not override body or face");
}
var appearancePath=Path.Combine(fixture,"appearance.json");
Check(!AppearanceSettings.Load(appearancePath).ShowHeadPot,"missing appearance preserves original no-pot look");
new AppearanceSettings{ShowHeadPot=false}.Save(appearancePath);
Check(!AppearanceSettings.Load(appearancePath).ShowHeadPot,"hidden pot persists across reload");
new AppearanceSettings{ShowHeadPot=true}.Save(appearancePath);
Check(AppearanceSettings.Load(appearancePath).ShowHeadPot,"visible pot persists across reload");
var memoryPath=Path.Combine(fixture,"saved-memories.json");
var store=LongTermMemory.Load(memoryPath);
Check(!store.Enabled && store.Entries.Count==0 && !File.Exists(memoryPath),"memory opt-in default without eager file write");
store.SetEnabled(true);store.Save("长期暗号：蓝鲸白饭四十二","请记住长期暗号：蓝鲸白饭四十二");
var restarted=LongTermMemory.Load(memoryPath);var remembered=restarted.Entries.Single();
Check(restarted.Enabled && remembered.UserConfirmed && remembered.Source=="user_chat" && remembered.SourceText.StartsWith("请记住"),"restart restores confirmed memory and provenance");
Check(config.BuildMessages(Array.Empty<ChatMessage>(),"暗号是什么", "",restarted.Context())[0].content.Contains("蓝鲸白饭四十二"),"new conversation receives persisted memory");
Check(zero.BuildMessages(Array.Empty<ChatMessage>(),"暗号是什么","",restarted.Context())[0].content.Contains("蓝鲸白饭四十二"),"zero conversational context does not disable saved memory");
restarted.SetEnabled(false);Check(!restarted.Context().Contains("蓝鲸白饭四十二"),"disable excludes stored facts");
restarted.SetEnabled(true);restarted.Save("长期暗号：白饭七十三",editId:remembered.Id);
var edited=LongTermMemory.Load(memoryPath).Entries.Single();
Check(edited.Text.Contains("白饭七十三") && edited.CreatedUtc==remembered.CreatedUtc && edited.SourceText==remembered.SourceText,"edit persists with creation/source preserved");
restarted.Delete(edited.Id);Check(LongTermMemory.Load(memoryPath).Entries.Count==0,"delete persists across restart");
restarted.Save("喜欢白米饭");restarted.Clear();Check(LongTermMemory.Load(memoryPath).Entries.Count==0 && restarted.Enabled,"clear persists and retains enable preference");
string diskBefore=File.ReadAllText(memoryPath);
foreach(var bad in new[]{"",new string('长',281),"sk-"+new string('z',20)}) {
 bool rejected=false;try{restarted.Save(bad);}catch(InvalidDataException){rejected=true;}Check(rejected && File.ReadAllText(memoryPath)==diskBefore && restarted.Entries.Count==0,"invalid facts do not mutate disk or state");
}
restarted.Save("喜欢白米饭");bool duplicate=false;try{restarted.Save("喜欢白米饭");}catch(InvalidDataException){duplicate=true;}Check(duplicate && restarted.Entries.Count==1,"duplicate add rejected");
File.WriteAllText(memoryPath,"{}");bool conflict=false;try{restarted.Save("外部编辑之后的新内容");}catch(IOException){conflict=true;}Check(conflict && File.ReadAllText(memoryPath)=="{}","external edits not overwritten");
File.WriteAllText(memoryPath,"{broken");bool corrupt=false;try{LongTermMemory.Load(memoryPath);}catch(System.Text.Json.JsonException){corrupt=true;}Check(corrupt && File.ReadAllText(memoryPath)=="{broken","corrupt memory preserved");
var capPath=Path.Combine(fixture,"memory-cap.json");var cap=LongTermMemory.Load(capPath);cap.SetEnabled(true);
for(int n=0;n<50;n++)cap.Save($"fact{n:D2}:"+new string('鲸',200));
Check(cap.Context().Length<=4000 && cap.Context().Contains("fact49") && !cap.Context().Contains("fact00"),"bounded recent memory context");
bool full=false;try{cap.Save("第51条");}catch(InvalidDataException){full=true;}Check(full && cap.Entries.Count==50,"memory count capped without mutation");
var pendingPath=Path.Combine(fixture,"unconfirmed.json");
File.WriteAllText(pendingPath,System.Text.Json.JsonSerializer.Serialize(new MemoryDocument{Enabled=true,Entries=new(){new SavedMemory(Guid.NewGuid().ToString(),"未确认猜测","manual","",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,false)}}));
Check(!LongTermMemory.Load(pendingPath).Context().Contains("未确认猜测"),"unconfirmed fact never injected");
var bomPath=Path.Combine(fixture,"bom-memory.json");File.WriteAllText(bomPath,"{}",new System.Text.UTF8Encoding(true));
Check(LongTermMemory.Load(bomPath).Entries.Count==0,"UTF8 BOM external edits accepted");
ProbeMemoryFixture GaugeFixture(int level) {
 var m=new ProbeMemoryFixture();m.Pointers[0x100468]=0x200000;m.Pointers[0x200070]=0x300000;m.Pointers[0x300010]=0x400000;m.Pointers[0x400018]=0x500000;m.Integers[0x502370]=level;return m;
}
foreach(int level in new[]{0,1,2,3}) {
 var m=GaugeFixture(level);Check(SpiritGaugeProbe.TryRead(m,0x100000,out var raw,out _) && raw==level,"read exact int32 spirit level via relative field chain");
 Check(m.Reads.SequenceEqual(new ulong[]{0x100468,0x200070,0x300010,0x400018,0x502370}),"correct dereference order without extra player dereference");
}
foreach(int invalid in new[]{-1,4,99,int.MaxValue}) Check(!SpiritGaugeProbe.TryRead(GaugeFixture(invalid),0x100000,out _,out _),"invalid level and community sentinel rejected");
var missing=GaugeFixture(2);missing.Pointers.Remove(0x300010);Check(!SpiritGaugeProbe.TryRead(missing,0x100000,out _,out var missStatus)&&missStatus.Contains("step 3"),"unreadable intermediate pointer stops safely");
var nullPtr=GaugeFixture(2);nullPtr.Pointers[0x100468]=0;Check(!SpiritGaugeProbe.TryRead(nullPtr,0x100000,out _,out _)&&nullPtr.Reads.Count==1,"null target stops before another read");
foreach(ulong invalid in new ulong[]{0,1,ulong.MaxValue,0x0000800000000000}) {var m=GaugeFixture(0);Check(!SpiritGaugeProbe.TryRead(m,invalid,out _,out _)&&m.Reads.Count==0,"invalid player address never read");}
var tracker=new SpiritGaugeTracker();
Check(tracker.Observe(1,0,0)==null && tracker.Observe(1,0,.1)==null,"three samples required");
var initial=tracker.Observe(1,0,.2);Check(initial is {Previous:null,Current:0},"first stable value is baseline only");
Check(tracker.Observe(1,3,.3)==null && tracker.Observe(1,0,.4)==null && tracker.Stable==0,"one-sample red glitch ignored");
tracker.Observe(1,3,.5);tracker.Observe(1,3,.6);var red=tracker.Observe(1,3,.7);
Check(red is {Previous:0,Current:3},"stable change detected");
Check(tracker.Observe(1,3,.8)==null,"holding red does not repeat transitions");
tracker.Observe(1,null,.9);Check(tracker.Stable==null,"read failure clears baseline");
tracker.Observe(1,3,1);tracker.Observe(1,3,1.1);Check(tracker.Observe(1,3,1.2) is {Previous:null},"read recovery rebuilds baseline rather than praising");
tracker.Observe(2,2,1.3);Check(tracker.Stable==null,"changed player identity invalidates baseline");
tracker.Observe(2,2,1.4);tracker.Observe(2,2,1.5);tracker.Observe(2,3,5);Check(tracker.Stable==null,"scene-sized time gap clears stale baseline");
tracker.Reset();tracker.Observe(1,0,0);tracker.Observe(1,0,0);Check(tracker.Observe(1,0,.1)==null,"duplicate timestamps are not independent observations");
tracker.Reset();Check(tracker.Raw==null&&tracker.Stable==null,"explicit reset clears gauge state");
var local=new LocalProcessMemory();
Check(!local.Pointer(1,out _) && !local.Int32(ulong.MaxValue,out _),"native reader rejects invalid addresses");
var scratch=System.Runtime.InteropServices.Marshal.AllocHGlobal(16);
try {
 System.Runtime.InteropServices.Marshal.WriteInt64(scratch,(long)scratch+8);System.Runtime.InteropServices.Marshal.WriteInt32(scratch,8,3);
 Check(local.Pointer((ulong)scratch,out var ptr)&&ptr==(ulong)scratch+8,"OS reader copies self-process pointer");
 Check(local.Int32((ulong)scratch+8,out var lv)&&lv==3,"OS reader copies self-process int32");
} finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(scratch); }
Check(SpiritDiagnosticGate.Blocked(false,false) is not null,"disabled probe explains missing samples");
Check(SpiritDiagnosticGate.Blocked(false,true) is not null,"disabled paused probe is blocked");
Check(SpiritDiagnosticGate.Blocked(true,true)!.Contains("Scene paused"),"scene guard requires explicit resume");
Check(SpiritDiagnosticGate.Blocked(true,false) is null,"active loaded probe can sample");
var spiritReplies = new SpiritCompanion();
Check(spiritReplies.Observe(new(null,3),0)==null,"red baseline does not praise");
Check(spiritReplies.Observe(new(3,2),0)==null,"level consumption does not claim helmbreaker hit");
Check(spiritReplies.Observe(new(2,2),0)==null,"holding gauge is silent");
Check(spiritReplies.Observe(new(0,1),0) is {Emotion:"happy"},"verified white level-up reacts");
Check(spiritReplies.Observe(new(1,2),1) is {Emotion:"happy"},"rapid verified level-up has no cooldown");
Check(spiritReplies.Observe(new(2,3),18)!.Text.Contains("红刃"),"red level-up gets matching line");
spiritReplies.Reset();Check(spiritReplies.Observe(new(null,3),20)==null,"scene reset baseline silent");
foreach(var level in new[]{-1,0,4,99}) Check(spiritReplies.Observe(new(0,level),30)==null,"invalid or nonincreasing level silent");
Check(spiritReplies.Observe(new(0,3),double.NaN)==null,"invalid time silent");
Check(SpiritCompanion.LevelName(null)=="未知" && SpiritCompanion.LevelName(3)=="红刃","context level names");
Check(NativeDialogue.Format("你好\0\r\n<MSTR>\t世界")=="鲸鱼娘：你好 ＜MSTR＞ 世界","native text strips controls and neutralizes game tags");
Check(NativeDialogue.Format(" \r\n\0")=="","blank native message ignored");
Check(NativeDialogue.Format(string.Concat(Enumerable.Repeat("🐋",181))).EnumerateRunes().Count()==185,"native truncation preserves Unicode scalars");
var nativeLines=new NativeDialogue();
nativeLines.Enqueue("first",0);nativeLines.Enqueue("second",0);
Check(nativeLines.Take(0,true)!.Text.EndsWith("first"),"native queue ordered");
Check(nativeLines.Take(.1,true)==null,"native messages paced");
Check(nativeLines.Take(1.5,true)!.Text.EndsWith("second"),"second message after spacing");
nativeLines.Enqueue("old",2);Check(nativeLines.Take(11,true)==null,"expired native message discarded");
nativeLines.Enqueue("scene",12);Check(nativeLines.Take(12,false)==null&&nativeLines.Take(13,true)!.Text.EndsWith("scene"),"transient HUD absence retains fresh output");
nativeLines.Enqueue("reset",14);nativeLines.Clear();Check(nativeLines.Take(15,true)==null,"scene reset clears queue");
nativeLines.Enqueue("future",20);Check(nativeLines.Take(19,true)==null,"backward time discards queued line");
for(int i=0;i<6;i++)nativeLines.Enqueue(i.ToString(),30);
Check(nativeLines.Take(30,true)!.Text.EndsWith("2"),"bounded native queue drops oldest");
nativeLines.Clear();nativeLines.Enqueue("bad",double.NaN);Check(nativeLines.Take(0,true)==null,"invalid timestamp never queued");
string? nativeDecoded = null; nint retainedUtf8 = 0; int nativeInvocations = 0;
NativeMessageInterop.DisplayDelegate nativeStub = (chatPtr, utf8, delay, flags, important) => {
    Check(chatPtr == 123 && delay == 0 && flags == 0 && important == 0,"native display ABI exact arguments");
    nativeDecoded = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(utf8);
    retainedUtf8 = utf8; nativeInvocations++; return 0;
};
var stubAddress = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(nativeStub);
using(var utf8Sink = new NativeMessageInterop(stubAddress)) {
    utf8Sink.Display(123,"鲸鱼娘：中文 ABC 123");
    Check(nativeDecoded=="鲸鱼娘：中文 ABC 123","explicit native pointer carries exact UTF8 Chinese");
    GC.Collect(); GC.WaitForPendingFinalizers();
    Check(System.Runtime.InteropServices.Marshal.PtrToStringUTF8(retainedUtf8)==nativeDecoded,"native UTF8 allocation survives GC and call return");
    for(int i=0;i<70;i++)utf8Sink.Display(123,"中文"+i);
    Check(nativeDecoded=="中文69" && nativeInvocations==71,"bounded buffer rollover retains current message");
    try{utf8Sink.Display(0,"bad");Check(false,"zero singleton rejected");}catch(ArgumentException){Check(nativeInvocations==71,"zero singleton never invokes native callback");}
    utf8Sink.Dispose();
    try{utf8Sink.Display(123,"bad");Check(false,"disposed sink rejected");}catch(ObjectDisposedException){Check(true,"disposed sink cannot reuse freed buffers");}
}
GC.KeepAlive(nativeStub);
var nativeChat = new NativeChatInput();
Check(nativeChat.Receive("大肥鱼，今天吃什么？",0),"local message accepted");
Check(!nativeChat.Receive("大肥鱼，今天吃什么？",1),"native duplicate filtered");
Check(nativeChat.Take(0,true,true)==null,"busy manual conversation not interrupted");
Check(nativeChat.Take(0,true,false)=="大肥鱼，今天吃什么？","local input dispatched intact");
nativeChat.Receive("下一句",1);Check(nativeChat.Take(7,true,false)==null && nativeChat.Take(8,true,false)=="下一句","paid native dispatch rate limited");
Check(!nativeChat.Receive("/other command",9),"other mod command ignored");
Check(nativeChat.Receive("/whale 你好",9),"optional addressing prefix supported");
Check(!nativeChat.Receive("/whale 你好",10),"normalized prefixed duplicate filtered");
Check(!nativeChat.Receive("x\0y",11) && !nativeChat.Receive(new string('x',513),11),"invalid native chat filtered");
nativeChat.Clear();nativeChat.Receive("旧场景",0);Check(nativeChat.Take(61,true,false)==null,"expired input discarded");
nativeChat.Receive("切场景",17);Check(nativeChat.Take(17,false,false)==null && nativeChat.Take(18,true,false)==null,"scene pause cannot replay native input");
nativeChat.Clear();for(int i=0;i<6;i++)nativeChat.Receive("m"+i,0);Check(nativeChat.Take(0,true,false)=="m2","input queue bounded to four");
Check(NativeChatDecision.Parse("{\"should_reply\":false,\"text\":\"must not show\"}") is {Valid:true,Speak:false,Reply:null},"silent decision ignores extra text");
Check(NativeChatDecision.Parse("{\"should_reply\":true,\"text\":\"吃饭！\",\"emotion\":\"happy\"}") is {Valid:true,Speak:true,Reply.Text:"吃饭！"},"reply decision includes expression");
foreach(var invalid in new[]{"hi","{}","{\"should_reply\":\"false\"}","{\"should_reply\":true}","null"})Check(!NativeChatDecision.Parse(invalid).Valid,"malformed decision fails quietly");
try{new ChatSettings{NativeChatModel="bad\nmodel"}.Validate();Check(false,"invalid native model rejected");}catch(InvalidDataException){Check(true,"invalid native model rejected");}
var nativePrompt=NativeChatDecision.ApplyContract(ChatConfiguration.Default.SystemPrompt("状态未知"));
Check(nativePrompt.Contains("should_reply")&&!nativePrompt.Contains(ChatConfiguration.ReplyFormatInstruction),"native prompt replaces conflicting generic JSON contract");
Check(!ChatConfiguration.Default.SystemPrompt("状态未知").Contains("should_reply"),"ordinary F9 chat retains original output contract");
Check(NativeChatDecision.Parse("{\"text\":\"本鱼在呢\",\"emotion\":\"happy\"}") is {Valid:true,Speak:true,Reply.Emotion:"happy"},"complete legacy reply survives missing decision field");
Check(NativeChatDecision.Parse("```json\n{\"should_reply\":true,\"text\":\"在呢\"}\n```") is {Valid:true,Speak:true},"JSON fence normalized");
Check(!NativeChatDecision.Parse("你好，我会回复").Valid,"unstructured prose is not guessed");
Check(NativeChatDecision.Shape("{\"text\":\"private text\"}")=="decision=missing,text=String","telemetry does not contain private text");
var visibleCd=new NativeChatInput();visibleCd.Receive("第一句",0);visibleCd.Take(0,true,false);
Check(visibleCd.Summary(2,true,false).Contains("6.0"),"visible cooldown exact remaining time");
visibleCd.Receive("第二句",1);Check(visibleCd.Take(20,true,true)==null&&visibleCd.Take(21,true,false)=="第二句","queue survives normal request latency");
visibleCd.Receive("same",22);visibleCd.Receive("same",23);Check(visibleCd.Feedback.Contains("重复"),"duplicate rejection visible");
var readableHistory=new List<ChatMessage>{new("user","吃什么"),new("assistant","本鱼想吃米饭")};
var structuredHistory=ChatConfiguration.Default.BuildMessages(readableHistory,"配什么菜","状态未知");
NativeChatDecision.PrepareMessages(structuredHistory);
Check(structuredHistory[2].content.Contains("should_reply")&&NativeChatDecision.Parse(structuredHistory[2].content).Speak,"native history provides consistent JSON assistant examples");
Check(readableHistory[1].content=="本鱼想吃米饭","native payload conversion leaves local history unchanged");
var transientOutput=new NativeDialogue();transientOutput.Enqueue("保留",0);transientOutput.Take(1,false);
Check(transientOutput.Take(2,true)!=null,"temporary HUD absence does not discard new reply");
transientOutput.Enqueue("旧场景",3);transientOutput.Clear();Check(transientOutput.Take(4,true)==null,"explicit scene clear still prevents old output replay");
transientOutput.Enqueue("过期",5);transientOutput.Take(14,false);Check(transientOutput.ExpiredCount==1,"missing HUD expiry tracked for visible feedback");
var teamProbe=new TeamChatProbe();
var markerBytes=System.Text.Encoding.UTF8.GetBytes("WHALE-PEER-B1");
teamProbe.Receive(1,1,0,markerBytes,0);Check(teamProbe.Drain().Length==0,"team diagnostic default off");
teamProbe.Start(0);
var markedPacket=Enumerable.Repeat((byte)0xAA,80).Concat(markerBytes).Concat(new byte[]{0,1}).ToArray();
var untouched=markedPacket.ToArray();teamProbe.Receive(123,3,0,markedPacket,1);
var markedSample=teamProbe.Drain().Single();
Check(markedSample.Route=="received_unverified" && markedSample.PacketId==123 && markedSample.Session==0,"received metadata does not claim owner identity");
Check(markedSample.Offset==80 && markedSample.PrefixHex.Length==128 && markedSample.Encoding=="UTF8","marked header sample bounded to 64 bytes");
Check(markedPacket.SequenceEqual(untouched),"network diagnostic does not mutate or consume packet bytes");
Check(!teamProbe.Local("ordinary private chat",2)&&teamProbe.Drain().Length==0,"ordinary text never captured");
Check(teamProbe.Local("WHALE-OWNER-A1",2)&&teamProbe.Drain().Single().Route=="local_outbound","local marker uses trusted local route");
teamProbe.Receive(2,1,1,System.Text.Encoding.Unicode.GetBytes("WHALE-OWNER-A1"),3);
Check(teamProbe.Drain().Single() is {Route:"received_unverified",Encoding:"UTF16LE"},"remote owner-worded marker remains unverified");
teamProbe.Receive(3,1,0,new byte[4097],4);Check(teamProbe.Drain().Length==0,"large packets skipped");
for(int i=0;i<100;i++)teamProbe.Receive(1,1,0,markerBytes,5);
Check(teamProbe.Drain().Length==32,"network diagnostic queue bounded under bursts");
Check(!teamProbe.Active(180),"probe automatically ends at three minutes");
teamProbe.Receive(1,1,0,markerBytes,181);Check(teamProbe.Drain().Length==0,"no samples after time window");
teamProbe.Start(200);teamProbe.Stop();Check(!teamProbe.Active(201),"stop and scene reset disable collection");
Check(ChatAuthorityPolicy.Classify(true,false)==ChatAuthority.Owner,"owner attribution comes from local route");
Check(ChatAuthorityPolicy.Classify(false,false)==ChatAuthority.Unknown,"unverified network source cannot be owner");
Check(ChatAuthorityPolicy.Classify(false,true)==ChatAuthority.PeerContext,"verified remote source is peer context only");
Check(ChatAuthorityPolicy.MayRequestReply(ChatAuthority.Owner)&&!ChatAuthorityPolicy.MayRequestReply(ChatAuthority.PeerContext)&&!ChatAuthorityPolicy.MayRequestReply(ChatAuthority.Unknown),"default owner priority denies remote-triggered paid replies");
var borrowedMarker=new string("WHALE-OWNER-A1".ToCharArray());
var markerHandle=System.Runtime.InteropServices.GCHandle.Alloc(borrowedMarker,System.Runtime.InteropServices.GCHandleType.Pinned);
try{
 teamProbe.Start(0);teamProbe.Local(borrowedMarker,1);
 System.Runtime.InteropServices.Marshal.WriteInt16(markerHandle.AddrOfPinnedObject(),0);
 Check(teamProbe.Drain().Single().Marker=="WHALE-OWNER-A1","marker queue owns snapshot when native sender clears original first character");
}finally{markerHandle.Free();}
var borrowedChat=new string("你好大肥鱼".ToCharArray());
var chatHandle=System.Runtime.InteropServices.GCHandle.Alloc(borrowedChat,System.Runtime.InteropServices.GCHandleType.Pinned);
try{
 var capturedChat=new NativeChatInput();capturedChat.Receive(borrowedChat,0);
 System.Runtime.InteropServices.Marshal.WriteInt16(chatHandle.AddrOfPinnedObject(),0);
 Check(capturedChat.Take(0,true,false)=="你好大肥鱼","model input owns complete snapshot after callback mutation");
}finally{chatHandle.Free();}
byte[] PeerFixture(string text="队友说想吃饭",string name="队友甲") {
 var bytes=new byte[180];Convert.FromHexString("00000801FF000003003F0000").CopyTo(bytes,0);
 System.Text.Encoding.UTF8.GetBytes(name).CopyTo(bytes,12);System.Text.Encoding.UTF8.GetBytes(text).CopyTo(bytes,44);return bytes;
}
var peerFixture=PeerFixture();var peerCopy=peerFixture.ToArray();
var peerDecoded=PeerChatPacket.Parse(PeerChatPacket.Id,1,0,peerFixture,0);
Check(peerDecoded is {DisplayName:"队友甲",Text:"队友说想吃饭"},"candidate hub chat fields decode exact UTF8");
Check(peerCopy.SequenceEqual(peerFixture),"peer decoder reads managed bytes without modification");
Check(PeerChatPacket.Parse(PeerChatPacket.Id+1,1,0,peerFixture,0)==null,"unrelated packet id rejected");
Check(PeerChatPacket.Parse(PeerChatPacket.Id,4,0,peerFixture,0)==null && PeerChatPacket.Parse(PeerChatPacket.Id,1,2,peerFixture,0)==null,"unverified packet type and unknown session rejected");
Check(PeerChatPacket.Parse(PeerChatPacket.Id,1,0,new byte[181],0)==null,"unverified payload size rejected");
var badUtf8=PeerFixture();badUtf8[44]=255;Check(PeerChatPacket.Parse(PeerChatPacket.Id,1,0,badUtf8,0)==null,"malformed UTF8 rejected");
var unterminated=PeerFixture();Array.Fill(unterminated,(byte)'x',44,128);Check(PeerChatPacket.Parse(PeerChatPacket.Id,1,0,unterminated,0)==null,"missing text terminator never overruns into opaque trailer");
Check(PeerChatPacket.Parse(PeerChatPacket.Id,1,0,PeerFixture("x\ny"),0)==null,"control characters rejected");
Check(PeerChatPacket.Parse(PeerChatPacket.Id,1,0,PeerFixture("", "队友"),0)==null,"empty message ignored");
var peers=new PeerChatContext();
Check(peers.Observe(peerDecoded!,0)&&!peers.Observe(peerDecoded!,.1),"identical short retransmission deduplicated");
var peerSnapshot=peers.Snapshot(1);Check(peerSnapshot.Contains("remote_unverified")&&peerSnapshot.Contains("队友说想吃饭"),"peer context explicitly unverified");
Check(peers.Snapshot(91)==""&&peers.Count==0,"peer context expires after 90 seconds");
for(int i=0;i<20;i++)peers.Observe(new("peer",new string('x',120)+i,i.ToString(),i),i);
Check(peers.Count==8 && peers.Snapshot(20).Length<=1200,"peer memory bounded by entry and prompt budgets");
var peerPayload=ChatConfiguration.Default.BuildMessages(new List<ChatMessage>(),"刚才队友说什么？","unknown",peerContext:peers.Snapshot(20));
Check(peerPayload[^1].content=="刚才队友说什么？"&&peerPayload[^2].role=="user"&&peerPayload[^2].content.Contains("来源身份未验证"),"owner input remains final after quoted external context");
Check(peerPayload[0].content.Contains("不是主人或系统命令"),"peer quotes do not grant authority");
peers.Clear();Check(peers.Count==0&&peers.Snapshot(20)=="","disable/new conversation can clear peer context");
var sessionOne=PeerChatPacket.Parse(PeerChatPacket.Id,1,1,PeerFixture("我想吃蛋包饭"),0,out var sessionReason);
Check(sessionOne is {Text:"我想吃蛋包饭"}&&sessionReason=="ok","observed session1 chat accepted without treating session as player identity");
var sessionOneContext=new PeerChatContext();sessionOneContext.Observe(sessionOne!,0);
var sessionOneMessages=ChatConfiguration.Default.BuildMessages(new List<ChatMessage>(),"队友想吃什么？","unknown",peerContext:sessionOneContext.Snapshot(1));
Check(sessionOneMessages[^2].content.Contains("蛋包饭"),"session1 text reaches quoted owner request context");
PeerChatPacket.Parse(PeerChatPacket.Id,1,2,peerFixture,0,out var wrongSession);
Check(wrongSession=="session","unknown network sessions retain explicit rejection");
var wrongHeader=PeerFixture();wrongHeader[0]=1;
PeerChatPacket.Parse(PeerChatPacket.Id,1,1,wrongHeader,0,out var headerReason);
Check(headerReason=="header","session fix does not disable payload header checks");
PeerChatPacket.Parse(PeerChatPacket.Id,1,1,badUtf8,0,out var utfReason);
Check(utfReason=="utf8","decode failure distinguishable from missing peer context");
var sharedText=SharedChat.Format("你好\r\n<测试>\0🐋");
Check(sharedText=="【大肥鱼】你好 ＜测试＞ 🐋","shared final text sanitized and prefixed");
foreach(var longText in new[]{new string('中',100),string.Concat(Enumerable.Repeat("🐋",100)),new string('a',200)}) {
 var bounded=SharedChat.Format(longText);var encoded=System.Text.Encoding.UTF8.GetBytes(bounded);
 Check(encoded.Length<=127&&bounded.EndsWith("…")&&!bounded.Contains('�'),"shared UTF8 clipping preserves Unicode boundaries and prefix");
}
Check(SharedChat.Format(" \0\r\n")==""&&SharedChat.IsCompanion("【大肥鱼】你好"),"empty output ignored and echo prefix recognized");
var shareQueue=new SharedChat();shareQueue.Enqueue("one",0);Check(shareQueue.Peek(0)!=null,"final conversation reply queued");shareQueue.Submitted(0);
shareQueue.Enqueue("two",1);Check(shareQueue.Peek(7)==null&&shareQueue.Peek(8)!=null,"shared messages paced at 8s");
shareQueue.Clear();shareQueue.Enqueue("old",0);Check(shareQueue.Peek(21)==null,"old unsent reply expires rather than later broadcast");
ChatMemoryFixture SenderFixture(){var m=new ChatMemoryFixture();m.Data[0x113FD0]=BitConverter.GetBytes((ulong)0x200000);m.Data[0x114748]=BitConverter.GetBytes(4);m.Data[0x202A58]=BitConverter.GetBytes(0);m.Data[0x20325E]=[0];m.Data[0x202A5D]=new byte[128];return m;}
var senderMemory=SenderFixture();
Check(GameChatSender.TrySend(senderMemory,0x100000,0x200000,sharedText,out _,true)==ChatSendResult.Submitted&&senderMemory.Writes.Count==0,"read-only sender check never writes");
Check(GameChatSender.TrySend(senderMemory,0x100000,0x200000,sharedText,out _)==ChatSendResult.Submitted,"verified idle GUI submits to normal game input path");
Check(senderMemory.Writes.Select(x=>x.Address).SequenceEqual(new ulong[]{0x202A5D,0x20325E})&&senderMemory.Data[0x20325E][0]==1,"only text buffer then send flag are written");
Check(System.Text.Encoding.UTF8.GetString(senderMemory.Data[0x202A5D]).TrimEnd('\0')==sharedText,"native game send buffer contains exact UTF8 with zero termination");
foreach(var occupied in new[]{"typing","draft","pending"}){
 var m=SenderFixture();if(occupied=="typing")m.Data[0x202A58]=BitConverter.GetBytes(1);if(occupied=="draft")m.Data[0x202A5D][0]=65;if(occupied=="pending")m.Data[0x20325E][0]=1;
 Check(GameChatSender.TrySend(m,0x100000,0x200000,sharedText,out _)==ChatSendResult.Busy&&m.Writes.Count==0,"player input and pending send are never overwritten");
}
var whisper=SenderFixture();whisper.Data[0x114748]=BitConverter.GetBytes(2);Check(GameChatSender.TrySend(whisper,0x100000,0x200000,sharedText,out _)==ChatSendResult.Unavailable&&whisper.Writes.Count==0,"private whisper target not shared");
var mismatchedGui=SenderFixture();Check(GameChatSender.TrySend(mismatchedGui,0x100000,0x300000,sharedText,out _)==ChatSendResult.Unavailable&&mismatchedGui.Writes.Count==0,"GUI object mismatch prevents all writes");
var failedFlag=SenderFixture();failedFlag.FailFlag=true;failedFlag.Data[0x202A5D][3]=7;var savedDraft=failedFlag.Data[0x202A5D].ToArray();
Check(GameChatSender.TrySend(failedFlag,0x100000,0x200000,sharedText,out _)==ChatSendResult.Unavailable&&failedFlag.Data[0x202A5D].SequenceEqual(savedDraft),"send flag failure restores original input bytes");
Check(GameChatSender.TrySend(SenderFixture(),0x100000,0x200000,"not prefixed",out _)==ChatSendResult.Unavailable,"only formatted companion text can be submitted");
var ownChatBuffer=System.Runtime.InteropServices.Marshal.AllocHGlobal(128);
try{
 var nativeChatMemory=new SelfChatMemory();var bytes=new byte[128];System.Text.Encoding.UTF8.GetBytes(sharedText).CopyTo(bytes,0);
 Check(nativeChatMemory.Write((ulong)ownChatBuffer,bytes)&&nativeChatMemory.Read((ulong)ownChatBuffer,128,out var copiedChat)&&copiedChat.SequenceEqual(bytes),"OS chat memory adapter roundtrip on test-owned allocation only");
 Check(!nativeChatMemory.Write(1,[1])&&!nativeChatMemory.Read(ulong.MaxValue,128,out _),"OS chat adapter rejects invalid address ranges");
}finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(ownChatBuffer);}
Check(PalicoResourceProbe.IsTarget(@"common\text\ot_auto_chS")&&PalicoResourceProbe.IsTarget("COMMON/TEXT/OT_AUTO_CHS"),"Palico target accepts game separators and casing");
foreach(var path in new string?[]{null,"common/text/ot_auto_eng","common/text/ot_auto_chS.gmd","other/ot_auto_chS","common/text/ot_s_auto_action_chS"})
 Check(!PalicoResourceProbe.IsTarget(path),"neighbor resources not probed");
using var palicoInventory=System.Text.Json.JsonDocument.Parse(File.ReadAllText("D:/homura/codex/MHW/research/chat/palico-gmd-inventory.json"));
var traditionalGmdData=File.ReadAllBytes("D:/homura/codex/MHW/research/chat/chunks/chunkG0/common/text/ot_auto_chT.gmd");
var traditionalStringSize=BitConverter.ToInt32(traditionalGmdData,32);
var traditionalStrings=System.Text.Encoding.UTF8.GetString(traditionalGmdData,traditionalGmdData.Length-traditionalStringSize,traditionalStringSize).Split('\0');
GmdMemoryFixture GmdFixture(string language="chS"){
 var m=new GmdMemoryFixture();m.Data[0x1000D8]=BitConverter.GetBytes(240);m.Data[0x1000F0]=BitConverter.GetBytes((ulong)0x300000);m.Data[0x1000F8]=BitConverter.GetBytes((ulong)0x200000);
 foreach(var f in language=="chT"?PalicoResourceProbe.TraditionalFingerprints:PalicoResourceProbe.Fingerprints){var pointer=(ulong)(0x300000+f.Index*128);m.Data[0x200000+(ulong)f.Index*8]=BitConverter.GetBytes(pointer);var text=language=="chT"?traditionalStrings[f.Index]:palicoInventory.RootElement[0].GetProperty("entries")[f.Index].GetProperty("text").GetString();m.Data[pointer]=System.Text.Encoding.UTF8.GetBytes(text+"\0");}
 return m;
}
var gmdMatch=GmdFixture();var gmdResult=PalicoResourceProbe.Inspect(gmdMatch,0x100000);
Check(gmdResult is {Status:"matched_8_of_8",Matched:8,Count:240},"8 original Palico fingerprints match extracted inventory");
Check(gmdMatch.Reads.Count==19&&gmdMatch.Reads.All(x=>x.Count<=128),"bounded 3 field and 8 pointer/text reads");
var changedGmd=GmdFixture();changedGmd.Data[0x300000+2*128][0]^=1;
Check(PalicoResourceProbe.Inspect(changedGmd,0x100000) is {Matched:7,Mismatched:1},"external edits detected");
var badCountGmd=GmdFixture();badCountGmd.Data[0x1000D8]=BitConverter.GetBytes(999999);
Check(PalicoResourceProbe.Inspect(badCountGmd,0x100000).Status=="count_mismatch"&&badCountGmd.Reads.Count==1,"invalid count stops table reads");
var badTableGmd=GmdFixture();badTableGmd.Data[0x1000F8]=BitConverter.GetBytes(ulong.MaxValue);
Check(PalicoResourceProbe.Inspect(badTableGmd,0x100000).Status=="layout_invalid","overflowing pointer table rejected");
var badStringGmd=GmdFixture();badStringGmd.Data[0x200000+2*8]=BitConverter.GetBytes((ulong)1);
Check(PalicoResourceProbe.Inspect(badStringGmd,0x100000) is {Matched:7,Unreadable:1},"invalid string pointer rejected");
var shortReadGmd=GmdFixture();shortReadGmd.Data[0x1000D8]=[1];
Check(PalicoResourceProbe.Inspect(shortReadGmd,0x100000).Status=="resource_unreadable","short field read rejected");
var noReadsGmd=GmdFixture();Check(PalicoResourceProbe.Inspect(noReadsGmd,ulong.MaxValue).Status=="resource_unreadable"&&noReadsGmd.Reads.Count==0,"invalid resource never read");
var gmdCapture=new PalicoResourceCapture();var capturedGmd=GmdFixture();
gmdCapture.Observe("common/text/ot_auto_chS",0x100000,0,capturedGmd);
Check(capturedGmd.Reads.Count==0,"resource capture default off");
gmdCapture.Start(10);gmdCapture.Observe("unrelated",0x100000,11,capturedGmd);
Check(capturedGmd.Reads.Count==0,"whitelist before resource read");
gmdCapture.Observe("common/text/ot_auto_chS",0x100000,11,capturedGmd);gmdCapture.Observe("common/text/ot_auto_chS",0x100000,12,capturedGmd);
Check(gmdCapture.Drain().Length==1&&capturedGmd.Reads.Count==19,"duplicate resource observed once");
gmdCapture.Observe("common/text/ot_auto_chS",0x400000,190,capturedGmd);
Check(gmdCapture.Drain().Length==0&&!gmdCapture.Active(190),"capture expires at 180 seconds");
gmdCapture.Start(200);gmdCapture.Stop();gmdCapture.Observe("common/text/ot_auto_chS",0x100000,201,capturedGmd);
Check(gmdCapture.Drain().Length==0,"stopped capture cannot read");
Check(gmdCapture.Stats() is {Callbacks:0,Targets:0},"capture restart clears telemetry and stop adds none");
gmdCapture.Start(300);
for(int i=0;i<40;i++)gmdCapture.Observe($"common/text/ot_candidate_{i}",0,301,capturedGmd);
gmdCapture.Observe("common/text/ot_bad\nprivate",0,301,capturedGmd);
gmdCapture.Observe("common/text/ot_auto_chS",0x100000,302,capturedGmd);
var boundedGmdStats=gmdCapture.Stats();
Check(boundedGmdStats.Callbacks==42&&boundedGmdStats.Targets==1&&boundedGmdStats.CandidatePaths.Length==16,"bounded path telemetry distinguishes callbacks from target loads");
Check(boundedGmdStats.CandidatePaths.All(x=>!x.Contains('\n')),"diagnostic path telemetry rejects control characters");
gmdCapture.Observe("common/text/ot_auto_chS",0x100000,480,capturedGmd);
Check(gmdCapture.Stats().Callbacks==42,"expired window cannot collect callback telemetry");
Check(PalicoResourceProbe.LanguageForPath(@"common\text\ot_auto_chT")=="chT"&&PalicoResourceProbe.LanguageForPath("COMMON/TEXT/OT_AUTO_CHT")=="chT","actual traditional resource path accepted");
Check(PalicoResourceProbe.Inspect(GmdFixture("chT"),0x100000,"chT") is {Language:"chT",Matched:8,Status:"matched_8_of_8"},"all traditional fingerprints match actual extracted game bytes");
Check(PalicoResourceProbe.Inspect(GmdFixture(),0x100000,"chT").Matched==0,"simplified strings cannot validate traditional resource");
var unsupportedGmd=GmdFixture();Check(PalicoResourceProbe.Inspect(unsupportedGmd,0x100000,"eng").Status=="unsupported_language"&&unsupportedGmd.Reads.Count==0,"unsupported language never read");
var traditionalCapture=new PalicoResourceCapture();traditionalCapture.Start(500);traditionalCapture.Observe("common/text/ot_auto_chT",0x100000,501,GmdFixture("chT"));
Check(traditionalCapture.Drain() is [{Language:"chT",Matched:8}],"capture selects traditional fingerprints from actual path");
GmdMemoryFixture DeferredGmdFixture(){var m=GmdFixture("chT");m.Data[0x100000]=BitConverter.GetBytes((ulong)0x900000);m.Data[0x10000C]=System.Text.Encoding.ASCII.GetBytes("common\\text\\ot_auto_chT\0");m.Data[0x1000D8]=BitConverter.GetBytes(0);return m;}
var deferredCapture=new PalicoResourceCapture();var deferredMemory=DeferredGmdFixture();deferredCapture.Start(600);
deferredCapture.Observe("common/text/ot_auto_chT",0x100000,601,deferredMemory);
Check(deferredCapture.Drain() is [{Count:0,Phase:"callback"}],"initial zero count recorded as unready, never accepted");
var beforeDeferredReads=deferredMemory.Reads.Count;deferredCapture.Tick(601.1,deferredMemory);
Check(deferredMemory.Reads.Count==beforeDeferredReads,"deferred sampling has no busy polling before 250ms");
deferredMemory.Data[0x1000D8]=BitConverter.GetBytes(240);deferredCapture.Tick(601.3,deferredMemory);
Check(deferredCapture.Drain() is [{Matched:8,Phase:"deferred",Language:"chT"}],"async resource readiness can validate after initial count zero");
var completeDeferredReads=deferredMemory.Reads.Count;deferredCapture.Tick(602,deferredMemory);
Check(deferredMemory.Reads.Count==completeDeferredReads,"completed deferred sampling releases descriptor");
var changedIdentityCapture=new PalicoResourceCapture();var changedIdentityMemory=DeferredGmdFixture();changedIdentityCapture.Start(700);
changedIdentityCapture.Observe("common/text/ot_auto_chT",0x100000,701,changedIdentityMemory);changedIdentityCapture.Drain();
changedIdentityMemory.Data[0x100000]=BitConverter.GetBytes((ulong)0x910000);changedIdentityCapture.Tick(702,changedIdentityMemory);
Check(changedIdentityCapture.Drain() is [{Status:"identity_changed_or_unreadable"}],"changed vtable halts deferred probe");
var freedIdentityCapture=new PalicoResourceCapture();var freedIdentityMemory=DeferredGmdFixture();freedIdentityCapture.Start(800);
freedIdentityCapture.Observe("common/text/ot_auto_chT",0x100000,801,freedIdentityMemory);freedIdentityCapture.Drain();
freedIdentityMemory.Data.Remove(0x10000C);freedIdentityCapture.Tick(802,freedIdentityMemory);
Check(freedIdentityCapture.Drain() is [{Status:"identity_changed_or_unreadable"}],"unreadable inline path halts deferred probe without native calls");
var timeoutCapture=new PalicoResourceCapture();var timeoutMemory=DeferredGmdFixture();timeoutCapture.Start(900);
timeoutCapture.Observe("common/text/ot_auto_chT",0x100000,901,timeoutMemory);timeoutCapture.Drain();var timeoutReads=timeoutMemory.Reads.Count;timeoutCapture.Tick(911,timeoutMemory);
Check(timeoutCapture.Drain() is [{Status:"not_ready_timeout"}]&&timeoutMemory.Reads.Count==timeoutReads,"10 second expiry stops sampling without final memory read");
var stoppedDeferredCapture=new PalicoResourceCapture();var stoppedDeferredMemory=DeferredGmdFixture();stoppedDeferredCapture.Start(1000);
stoppedDeferredCapture.Observe("common/text/ot_auto_chT",0x100000,1001,stoppedDeferredMemory);stoppedDeferredCapture.Drain();stoppedDeferredCapture.Stop();var stoppedReads=stoppedDeferredMemory.Reads.Count;stoppedDeferredCapture.Tick(1002,stoppedDeferredMemory);
Check(stoppedDeferredMemory.Reads.Count==stoppedReads&&stoppedDeferredCapture.Drain().Length==0,"stop cancels pending sampling");
var battleProbe=new PalicoBattleCapture();var battleMemory=new BattleMemoryFixture();battleMemory.SetText(traditionalStrings[2]);
battleProbe.Observe(0x400000,0,battleMemory);Check(battleMemory.Reads==0,"battle observer default off never reads message body");
battleProbe.Start(0);
foreach(var f in PalicoResourceProbe.TraditionalFingerprints){battleMemory.SetText(traditionalStrings[f.Index]);battleProbe.Observe(0x400000,1,battleMemory);var matched=battleProbe.Drain();Check(matched is [{Language:"chT"}]&&matched[0].Index==f.Index,"native display exact traditional bytes classify only expected resource key");}
foreach(var f in PalicoResourceProbe.Fingerprints){battleMemory.SetText(palicoInventory.RootElement[0].GetProperty("entries")[f.Index].GetProperty("text").GetString()!);battleProbe.Observe(0x400000,2,battleMemory);Check(battleProbe.Drain() is [{Language:"chS"}],"native display exact simplified bytes classified independently");}
battleMemory.SetText("ordinary player text, never logged");battleProbe.Observe(0x400000,3,battleMemory);
Check(battleProbe.Drain().Length==0&&battleProbe.Stats(3).Unmatched==1,"unmatched message body not retained as diagnostic output");
var beforeInactiveBattle=battleMemory.Reads;battleProbe.Observe(0x400000,3600,battleMemory);
Check(!battleProbe.Stats(3600).Active&&battleMemory.Reads==beforeInactiveBattle,"battle observer lasts one hour then reads nothing");
battleProbe.Start(4000);battleMemory.SetText(traditionalStrings[2]);for(int i=0;i<40;i++)battleProbe.Observe(0x400000,4001,battleMemory);
Check(battleProbe.Drain().Length==32&&battleProbe.Stats(4001).Dropped==8,"battle match output queue bounded to32");
battleProbe.Stop();beforeInactiveBattle=battleMemory.Reads;battleProbe.Observe(0x400000,4002,battleMemory);
Check(battleMemory.Reads==beforeInactiveBattle,"stopped battle capture performs no reads");
battleProbe.Start(5000);battleMemory.Data=Enumerable.Repeat((byte)65,512).ToArray();battleProbe.Observe(0x400000,5001,battleMemory);
Check(battleProbe.Stats(5001).ReadFailures==1&&battleProbe.Drain().Length==0,"unterminated text bounded to512bytes");
var forwarded=0;PalicoBattleCapture.Forward(()=>throw new Exception("observer failure"),()=>forwarded++);
Check(forwarded==1,"observer failure still forwards original display exactly once");
bool originalFailurePropagated=false;try{PalicoBattleCapture.Forward(()=>{},()=>throw new InvalidOperationException("native original"));}catch(InvalidOperationException){originalFailurePropagated=true;}
Check(originalFailurePropagated,"original display errors are not swallowed by diagnostics");
var hourResourceCapture=new PalicoResourceCapture();hourResourceCapture.Start(0,3600);
Check(hourResourceCapture.Active(3599)&&!hourResourceCapture.Active(3600),"resource observation can cover battle window without short startup limit");
var offlineCatalog=System.Text.Json.JsonSerializer.Deserialize<OfflineCatalog>(File.ReadAllText("D:/homura/codex/MHW/config/dialogue/ot_auto_chT.json"))!;
var offlineBuilt=OfflineDialogue.Build(traditionalGmdData,offlineCatalog);
Check(offlineBuilt.SequenceEqual(File.ReadAllBytes("D:/homura/codex/MHW/research/chat/editable-persona-candidate/ot_auto_chT.gmd")),"C# game editor produces byte-identical output to independently tested Python compiler");
OfflineCatalog CopyOffline()=>System.Text.Json.JsonSerializer.Deserialize<OfflineCatalog>(System.Text.Json.JsonSerializer.Serialize(offlineCatalog))!;
var unchangedOffline=CopyOffline();foreach(var row in unchangedOffline.Entries)row.Replacement=null;
Check(OfflineDialogue.Build(traditionalGmdData,unchangedOffline).SequenceEqual(traditionalGmdData),"C# unmodified GMD roundtrip is byte-identical");
foreach(var invalidOffline in new[]{"original","hash","index","duplicate","text","tokens"}){
 var copy=CopyOffline();if(invalidOffline=="original")copy.Entries[0].Original="tampered";if(invalidOffline=="hash")copy.SourceHash="bad";if(invalidOffline=="index")copy.Entries[0].Index=999;if(invalidOffline=="duplicate")copy.Entries.Add(copy.Entries[0]);if(invalidOffline=="text")copy.Entries[0].Replacement="bad\0text";if(invalidOffline=="tokens")copy.Entries.First(x=>x.Original.Contains("%s")).Replacement="lost parameter";
 bool rejected=false;try{OfflineDialogue.Build(traditionalGmdData,copy);}catch(InvalidDataException){rejected=true;}Check(rejected,"C# offline compiler rejects source/parameter tampering: "+invalidOffline);
}
var generationLines=new[]{new OfflineLine{Key="heal",Original="先休息一下"},new OfflineLine{Key="support",Original="放好%s"}};
var goodGeneration=OfflineDialogue.ParseReply("{\"lines\":[{\"key\":\"heal\",\"text\":\"本鱼缓一缓。\"},{\"key\":\"support\",\"text\":\"本鱼放好%s啦。\"}]}",generationLines);
Check(goodGeneration.Count==2&&goodGeneration["support"].Contains("%s"),"offline batch accepts complete structured replies with preserved parameters");
foreach(var badGeneration in new[]{"{\"lines\":[]}","{\"lines\":[{\"key\":\"unknown\",\"text\":\"鱼\"}]}","{\"lines\":[{\"key\":\"heal\",\"text\":\"鱼\"},{\"key\":\"heal\",\"text\":\"鱼\"}]}","{\"lines\":[{\"key\":\"heal\",\"text\":\"鱼\"},{\"key\":\"support\",\"text\":\"参数丢失\"}]}"}){
 bool rejected=false;try{OfflineDialogue.ParseReply(badGeneration,generationLines);}catch(InvalidDataException){rejected=true;}Check(rejected,"offline AI batch missing/unknown/duplicate/parameter edits rejected before save");
}
Check(OfflineDialogue.Prompt("test persona").Contains("准备/进行/完成")&&!OfflineDialogue.Prompt("test persona").Contains("长期记忆"),"offline generation contract maintains event phase and excludes conversation memory");
var defaultOffline=System.Text.Json.JsonSerializer.Deserialize<OfflineCatalog>(File.ReadAllText("D:/homura/codex/MHW/research/chat/offline-default-pack/ot_auto_chT.json"))!;
var playerText=OfflinePlainText.Export(defaultOffline);
Check(!playerText.Contains("%s")&&!playerText.Contains("<ICON NOTE>")&&playerText.Contains("〔名字〕")&&playerText.Contains("〔音符〕"),"player TXT hides technical placeholders behind Chinese markers");
var importedDefault=OfflinePlainText.Import(defaultOffline,playerText);
Check(OfflineDialogue.Build(traditionalGmdData,importedDefault).SequenceEqual(File.ReadAllBytes("D:/homura/codex/MHW/research/chat/offline-default-pack/ot_auto_chT.gmd")),"human text roundtrip preserves independently generated full default GMD");
Check(OfflineDialogue.Build(traditionalGmdData,OfflinePlainText.Import(unchangedOffline,OfflinePlainText.Export(unchangedOffline))).SequenceEqual(traditionalGmdData),"human TXT roundtrip preserves special original whitespace and source metadata");
var changedPlayerText=playerText.Replace("听到啦，马上过来！别趁我不在逞强。","收到，来陪你啦！");
var changedPlayerCatalog=OfflinePlainText.Import(defaultOffline,changedPlayerText);
Check(changedPlayerCatalog.Entries[0].Replacement=="收到，来陪你啦！"&&changedPlayerCatalog.Entries.Skip(1).Select(x=>x.Replacement).SequenceEqual(defaultOffline.Entries.Skip(1).Select(x=>x.Replacement)),"ordinary text edit changes only chosen line");
foreach(var badPlayerText in new[]{playerText.Replace(OfflinePlainText.Heading(defaultOffline.Entries[0]),"【乱改标题】"),playerText+OfflinePlainText.Heading(defaultOffline.Entries[0])+"\n重复",playerText.Replace("〔名字〕",""),playerText.Replace("听到啦，马上过来！别趁我不在逞强。","")}){
 bool rejected=false;try{OfflinePlainText.Import(defaultOffline,badPlayerText);}catch(InvalidDataException){rejected=true;}Check(rejected,"malformed player text rejected without touching live files");
}
Check(OfflinePlainText.Import(defaultOffline,"\uFEFF"+playerText.Replace("\n","\r\n")).Entries.Count==240,"Notepad BOM/Windows line endings accepted");
var originalPlayer=OfflinePlainText.Import(defaultOffline,playerText.Replace("听到啦，马上过来！别趁我不在逞强。","（保留原句）"));
Check(originalPlayer.Entries[0].Replacement==null,"Chinese restore-original phrase requires no JSON null syntax");
Console.WriteLine($"PASS {checks} assertions");

sealed class ProbeMemoryFixture : IReadOnlyLocalMemory {
 public Dictionary<ulong,ulong> Pointers {get;}=new(); public Dictionary<ulong,int> Integers {get;}=new();public List<ulong> Reads {get;}=new();
 public bool Pointer(ulong address,out ulong value){Reads.Add(address);return Pointers.TryGetValue(address,out value);}
 public bool Int32(ulong address,out int value){Reads.Add(address);return Integers.TryGetValue(address,out value);}
}

sealed class ChatMemoryFixture : IChatMemory {
 public Dictionary<ulong,byte[]> Data {get;}=new(); public List<(ulong Address,byte[] Bytes)> Writes {get;}=new();public bool FailFlag;
 public bool Read(ulong address,int count,out byte[] bytes){if(Data.TryGetValue(address,out var found)&&found.Length==count){bytes=found.ToArray();return true;}bytes=[];return false;}
 public bool Write(ulong address,byte[] bytes){if(FailFlag&&address==0x20325E)return false;if(!Data.TryGetValue(address,out var previous)||previous.Length!=bytes.Length)return false;Data[address]=bytes.ToArray();Writes.Add((address,bytes.ToArray()));return true;}
}

sealed class GmdMemoryFixture : IReadOnlyGmdMemory {
 public Dictionary<ulong,byte[]> Data {get;}=new();public List<(ulong Address,int Count)> Reads {get;}=new();
 public bool Read(ulong address,int count,out byte[] bytes){Reads.Add((address,count));if(Data.TryGetValue(address,out var found)){bytes=found.ToArray();return true;}bytes=[];return false;}
}
sealed class BattleMemoryFixture : IReadOnlyGmdMemory {
 public byte[] Data=new byte[128];public int Reads;
 public void SetText(string text){var bytes=System.Text.Encoding.UTF8.GetBytes(text+"\0");Data=new byte[Math.Max(128,bytes.Length)];bytes.CopyTo(Data,0);}
 public bool Read(ulong address,int count,out byte[] bytes){Reads++;if(address<0x400000||address-0x400000+(ulong)count>(ulong)Data.Length){bytes=[];return false;}bytes=Data.AsSpan((int)(address-0x400000),count).ToArray();return true;}
}
