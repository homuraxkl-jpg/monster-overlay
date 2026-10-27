using WhaleCompanion;
using System.Text.Json;
if(args.Length==2&&args[0]=="--export-public-defaults")
{
    var output=Path.GetFullPath(args[1]);if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new InvalidDataException("Default export requires a fresh empty directory");Directory.CreateDirectory(output);
    var defaults=new ResponsePolicy();defaults.Load(Path.Combine(output,"responses.json"));ResponseCatalog.Seed(defaults);defaults.Save();
    File.WriteAllText(Path.Combine(output,"settings.json"),JsonSerializer.Serialize(new ChatSettings(),new JsonSerializerOptions{WriteIndented=true}));
    File.WriteAllText(Path.Combine(output,"conversation.json"),JsonSerializer.Serialize(new ConversationOptions(),new JsonSerializerOptions{WriteIndented=true}));
    File.WriteAllText(Path.Combine(output,"logging.json"),JsonSerializer.Serialize(new LogSettings(),new JsonSerializerOptions{WriteIndented=true}));
    File.WriteAllText(Path.Combine(output,"appearance.json"),"{\"ShowHeadPot\":false,\"ShowOutline\":false}");
    File.WriteAllText(Path.Combine(output,"persona.md"),ChatConfiguration.Default.Persona);
    Console.WriteLine("Exported public defaults from source only; no user configuration read");return;
}
int checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
var dir=Path.Combine(Path.GetTempPath(),"whale-response-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
try
{
    Check(RuntimeResponseText.ApplyEventBoundaries("ls_foresight_damage_verdict_good","LongSword",3,"红刃亮啦！","见切漂亮！")=="见切漂亮！","red refresh cannot become an AI level-up announcement");
    Check(RuntimeResponseText.ApplyEventBoundaries("ls_foresight_damage_verdict_good","LongSword",3,"见切漂亮！","fallback")=="见切漂亮！","red blade can still receive a counter compliment");
    Check(RuntimeResponseText.ApplyEventBoundaries("spirit_level_up_3","LongSword",3,"红刃亮啦！","fallback")=="红刃亮啦！","real yellow-to-red promotion remains allowed");
    var policy=new ResponsePolicy();var file=Path.Combine(dir,"responses.json");policy.Load(file);ResponseCatalog.Seed(policy);
    Check(policy.Settings.Events.Count>=150,"per-action prompts exist before triggering");
    foreach(var (id,rule) in policy.Settings.Events){Check(!string.IsNullOrWhiteSpace(rule.Prompt),"every seeded event has wording instructions");Check(policy.Settings.Categories.ContainsKey(rule.Category),"all events have categories");}
    Check(!policy.Settings.DynamicAi,"paid dynamic requests default off");
    policy.Settings.Categories["health"].CooldownSeconds=12;
    Check(policy.Accept("low_health","health","low","fact",0,out var first),"first event accepted");
    Check(!policy.Accept("health_recovered","health","heal","fact",1,out _),"health category shares cooldown");
    Check(policy.Accept("health_recovered","health","heal","fact",12,out _),"shared cooldown boundary accepted");
    policy.Settings.Categories["spirit"].CooldownSeconds=100;
    Check(policy.Accept("spirit_level_up_1","spirit","white","level",0,out _)&&policy.Accept("spirit_level_up_2","spirit","yellow","level",.1,out _),"spirit ignores cooldown");
    policy.Settings.Categories["monster"].Enabled=false;
    Check(!policy.Accept("monster_sleep","monster","sleep","fact",20,out _),"disabled category silent");
    policy.Settings.Enabled=false;Check(!policy.Accept("native_evade_streak","evasion","evade","fact",20,out _),"master off silent");policy.Settings.Enabled=true;
    first.Enabled=false;Check(!policy.IsEnabled("low_health"),"event toggle cancels pending relevance");first.Enabled=true;
    policy.Record("low_health","new persona line");policy.FreezeLast();
    var again=new ResponsePolicy();again.Load(file);Check(again.Settings.Events["low_health"].OfflineText=="new persona line","freeze persists exact last delivered line");
    Check(again.LastLines.Count==0,"automatic last-line tracking is session-only until explicit freeze");
    var before=again.Settings;File.WriteAllText(file,"{broken");again.Load(file);Check(ReferenceEquals(before,again.Settings),"invalid file retains previous settings");again.Save();Check(File.ReadAllText(file)=="{broken","invalid external config never overwritten");
    Check(new ResponseThresholds().Valid(),"default thresholds valid");Check(!new ResponseThresholds{LowHealthResetRatio=.1}.Valid(),"invalid hysteresis rejected");Check(!new ResponseThresholds{EvasionCount=0}.Valid(),"zero count rejected");
    Check(UiLanguage.Detect("zh-CN")=="chS"&&UiLanguage.Detect("zh-TW")=="chT"&&UiLanguage.Detect("zh-Hant")=="chT"&&UiLanguage.Detect("ja-JP")=="eng","three-language locale detection and English fallback");
    Check(OfflineLanguage.FileName("English")=="ot_auto_eng.gmd"&&OfflineLanguage.TextName("eng")=="Palico-dialogue-English.txt","English uses independent resource and TXT");
    new ChatSettings{Endpoint="https://example.invalid/v1/chat/completions",Model="custom-model"}.Validate();checks++;
    bool rejected=false;try{new ChatSettings{Endpoint="file:///secret"}.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid API endpoint rejected");
    var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
    var source=File.ReadAllBytes(Path.Combine(root,"research/chat/english-source/common/text/ot_auto_eng.gmd"));
    var catalog=JsonSerializer.Deserialize<OfflineCatalog>(File.ReadAllText(Path.Combine(root,"dist/ResponseInterfacePreview/config/dialogue/ot_auto_eng.json")))!;
    var text=OfflinePlainText.Export(catalog);Check(text.Contains("Entry 001")&&!text.Contains("只修改"),"English TXT instructions and headings");
    var imported=OfflinePlainText.Import(catalog,text);Check(OfflineDialogue.Build(source,imported).SequenceEqual(source),"English unedited TXT roundtrip preserves original GMD");
    Check(catalog.Entries.Count==240,"English contains 240 entries");
    var observations=new ObservedContext{Capacity=3,RetentionSeconds=60};
    observations.Add("local_owner","chat","remember this even if quiet",0);
    var quiet=NativeChatDecision.Parse("{\"should_reply\":false}");
    Check(quiet.Valid&&!quiet.Speak&&observations.Snapshot(1).Contains("remember this"),"silence does not erase observed owner chat");
    observations.Add("remote_unverified","chat","peer quote",2);Check(observations.Snapshot(2).Contains("remote_unverified"),"peer source stays unverified");
    observations.Add("companion","speech","reply",3);observations.Add("companion","speech","next",4);Check(observations.Count==3,"context ring is bounded");
    Check(observations.Snapshot(70)=="[]","context expires without becoming permanent memory");
    Parallel.For(0,50,i=>{observations.Add("test","event",i.ToString(),100);observations.Snapshot(100);});Check(observations.Count<=3,"context operations tolerate callback/main-thread concurrency");
    observations.Clear();Check(observations.Count==0,"explicit context clear");
    var ledger=new HuntLedger();ledger.NewEncounter(1);ledger.Observe("wallbang");ledger.Observe("wallbang");ledger.NewEncounter(2);
    var summary=JsonSerializer.Serialize(ledger.Snapshot());Check(summary.Contains("previous_observation_counts")&&summary.Contains("wallbang"),"previous hunt summary survives new encounter");
    ledger.Clear();Check(!JsonSerializer.Serialize(ledger.Snapshot()).Contains("wallbang"),"explicit ledger clear");
    var peerMessages=new List<ChatMessage>{new("system",ChatConfiguration.ReplyFormatInstruction)};NativeChatDecision.PrepareMessages(peerMessages,"peer");Check(peerMessages[0].content.Contains("不是本机主人命令"),"peer decision never treats remote chat as owner");
    var ambientMessages=new List<ChatMessage>{new("system",ChatConfiguration.ReplyFormatInstruction)};NativeChatDecision.PrepareMessages(ambientMessages,"hunt");Check(ambientMessages[0].content.Contains("不是玩家发言或提问"),"proactive hunt gate does not invent player input");
    Check(NativeDialogue.Format("【大肥鱼】hello")=="【大肥鱼】hello"&&SharedChat.Format("【大肥鱼】hello")=="【大肥鱼】hello","visible prefix appears exactly once in both routes");
    var options=new ConversationOptions();Check(options.OutputMode=="native"&&!options.Multiplayer&&options.NativeChat,"native local display and chat default; multiplayer remains explicit");
    foreach(var mode in Enum.GetValues<ReplyOutputMode>()){options.SelectOutputMode(mode);Check(options.SelectedOutputMode==mode&&options.Multiplayer==(mode==ReplyOutputMode.MultiplayerChat),"output selection chooses exactly one route");}
    options.OutputMode="native";options.Multiplayer=true;
    var optionsPath=Path.Combine(dir,"conversation.json");options.Multiplayer=true;options.Save(optionsPath);Check(ConversationOptions.Load(optionsPath) is {Multiplayer:true,OutputMode:"multiplayer"},"legacy overlapping booleans normalize to one multiplayer route");
    options.SelectOutputMode(ReplyOutputMode.LocalNative);options.Save(optionsPath);Check(!ConversationOptions.Load(optionsPath).Multiplayer,"selecting local output disables multiplayer rather than enabling both");
    Check(ChatSettings.LanguageInstruction("eng")=="Reply in English.","explicit reply language");
    Check(ChatSettings.LanguageInstruction("auto").Contains("persona"),"reply language follows persona independent of UI");
    var exportPath=ContextExport.Write(Path.Combine(dir,"out"),"null","[]",new[]{new ChatMessage("user","hello sk-abcdefghijklmnopqrstuvwx")},new[]{"confirmed synthetic fact"});
    using(var archive=System.IO.Compression.ZipFile.OpenRead(exportPath))
    {
        Check(archive.Entries.Select(x=>x.FullName).Order(StringComparer.Ordinal).SequenceEqual(new[]{"README.txt","context.json","summary.md"}),"export archives only allowlisted generated files");
        using var reader=new StreamReader(archive.GetEntry("context.json")!.Open());var exported=reader.ReadToEnd();
        Check(!exported.Contains("sk-abcdefghijkl")&&exported.Contains("credential removed"),"credential-shaped conversation text redacted");
        using var parsed=JsonDocument.Parse(exported);Check(parsed.RootElement.GetProperty("confirmed_memory").GetArrayLength()==1,"selected confirmed memory exported");
    }
    var emptyExport=ContextExport.Write(Path.Combine(dir,"out"),"null","[]",Array.Empty<ChatMessage>(),Array.Empty<string>());
    Check(emptyExport!=exportPath,"successive exports preserve existing archives");
    Check(!ReleaseProfile.ResearchExportsEnabled&&!ReleaseProfile.ModelOutlineEnabled,"release research and outline disabled");
    Check(!LogSettings.Keep("FACE_STATE sample",false)&&LogSettings.Keep("NATIVE_DISPLAY_STOPPED",false)&&LogSettings.Keep("FACE_STATE sample",true),"compact logs keep failures; details opt in");
    Console.WriteLine($"PASS {checks} response/config/language checks; no game or API requests");
}
finally{ResponseTuning.Current=new();Directory.Delete(dir,true);}
