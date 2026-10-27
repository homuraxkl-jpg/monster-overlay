using WhaleCompanion;
using System.Text.Json;
int checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
var dir=Path.Combine(Path.GetTempPath(),"whale-response-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
try
{
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
    Console.WriteLine($"PASS {checks} response/config/language checks; no game or API requests");
}
finally{ResponseTuning.Current=new();Directory.Delete(dir,true);}
