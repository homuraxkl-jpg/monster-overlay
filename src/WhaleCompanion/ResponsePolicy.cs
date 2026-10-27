using System.Text.Json;
namespace WhaleCompanion;

public sealed class ResponseCategory
{
    public bool Enabled {get;set;}=true;
    public double CooldownSeconds {get;set;}=0;
}
public sealed class ResponseRule
{
    public string Category {get;set;}="weapon";
    public bool Enabled {get;set;}=true;
    public string OfflineText {get;set;}="";
    public string ReferenceText {get;set;}="";
    public string Prompt {get;set;}="";
}
public sealed class ResponseSettings
{
    public bool Enabled {get;set;}=true;
    public bool DynamicAi {get;set;}=false;
    public Dictionary<string,ResponseCategory> Categories {get;set;}=new(){["health"]=new(){CooldownSeconds=12},["evasion"]=new(),["guard"]=new(){CooldownSeconds=15},["spirit"]=new(),["monster"]=new(){CooldownSeconds=12},["damage"]=new(),["weapon"]=new(){CooldownSeconds=18},["buff"]=new(){CooldownSeconds=18},["quest"]=new()};
    public ResponseThresholds Thresholds {get;set;}=new();
    public Dictionary<string,ResponseRule> Events {get;set;}=new();
}
// Only runtime state lines are stored here. No conversations, credentials or memories.
public sealed class ResponsePolicy
{
    public ResponseSettings Settings {get;private set;}=new();
    public Dictionary<string,string> LastLines {get;}=new();
    readonly Dictionary<string,double> next=new();
    public string Status {get;private set;}="Ready";
    string? path;
    bool validFile=true;
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
    public void Load(string file)
    {
        path=file;
        try
        {
            if(File.Exists(file)&&new FileInfo(file).Length>4000000)throw new InvalidDataException();
            var candidate=File.Exists(file)?JsonSerializer.Deserialize<ResponseSettings>(File.ReadAllText(file),Json)??throw new InvalidDataException():new();
            if(candidate.Categories==null||candidate.Events==null||candidate.Categories.Any(x=>x.Value==null||!double.IsFinite(x.Value.CooldownSeconds)||x.Value.CooldownSeconds<0||x.Value.CooldownSeconds>3600)||candidate.Events.Any(x=>x.Value==null||x.Value.OfflineText.Length>2000||x.Value.Prompt.Length>8000||string.IsNullOrWhiteSpace(x.Value.Category)||!candidate.Categories.ContainsKey(x.Value.Category)))throw new InvalidDataException();
            if(candidate.Thresholds==null||!candidate.Thresholds.Valid())throw new InvalidDataException();
            validFile=true;Settings=candidate;ResponseTuning.Current=candidate;next.Clear();Status="Response settings loaded";
        }
        catch {validFile=false;Status="Invalid response settings; previous settings retained; saving blocked until repaired/reloaded";}
    }
    public void Save()
    {
        if(path==null||!validFile)return;
        try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(Settings,Json));File.Move(path+".tmp",path,true);Status="Response settings saved";}
        catch {Status="Response settings could not be saved";}
    }
    public ResponseRule Register(string id,string category,string fallback,string prompt)
    {
        if(!Settings.Categories.ContainsKey(category))Settings.Categories[category]=new();
        if(!Settings.Events.TryGetValue(id,out var rule))Settings.Events[id]=rule=new(){Category=category,ReferenceText=fallback,Prompt=prompt};
        return rule;
    }
    public bool Accept(string id,string category,string fallback,string prompt,double now,out ResponseRule rule)
    {
        rule=Register(id,category,fallback,prompt);
        if(!Settings.Enabled||!rule.Enabled||!Settings.Categories.TryGetValue(rule.Category,out var group)||!group.Enabled||!double.IsFinite(now))return false;
        if(!id.StartsWith("spirit_level_up",StringComparison.Ordinal)&&next.TryGetValue(rule.Category,out var until)&&now<until)return false;
        if(!id.StartsWith("spirit_level_up",StringComparison.Ordinal))next[rule.Category]=now+group.CooldownSeconds;
        return true;
    }
    public void Record(string id,string line){if(!string.IsNullOrWhiteSpace(line)&&line.Length<=2000)LastLines[id]=line;}
    public void FreezeLast(){foreach(var (id,line) in LastLines)if(Settings.Events.TryGetValue(id,out var rule))rule.OfflineText=line;Save();}
    public bool IsEnabled(string id)=>Settings.Enabled&&Settings.Events.TryGetValue(id,out var rule)&&rule.Enabled&&Settings.Categories.TryGetValue(rule.Category,out var group)&&group.Enabled;
    public void ResetScene()=>next.Clear();
}

public sealed class ResponseThresholds
{
    public double EventFreshnessSeconds {get;set;}=3;
    public double DynamicEventTimeoutSeconds {get;set;}=2.5;
    public double NativeOutputIntervalSeconds {get;set;}=1;
    public double SharedOutputIntervalSeconds {get;set;}=2;
    public double AiReplyQuietSeconds {get;set;}=1.5;
    public double CombatResultMaxAgeSeconds {get;set;}=2;
    public double LowHealthRatio {get;set;}=.25;
    public double LowHealthResetRatio {get;set;}=.4;
    public double HealthLossRatio {get;set;}=.15;
    public double HealthLossWindow {get;set;}=2;
    public double PlayerHealRatio {get;set;}=.2;
    public double PlayerHealMinimum {get;set;}=20;
    public double PlayerHealWindow {get;set;}=6;
    public double PlayerHealCooldown {get;set;}=30;
    public double SameActionExtraCooldown {get;set;}=12;
    public double MantleCooldown {get;set;}=18;
    public double PalicoArmRatio {get;set;}=.75;
    public double PalicoHealRatio {get;set;}=.25;
    public double PalicoHealWindow {get;set;}=10;
    public double PalicoHealCooldown {get;set;}=30;
    public int EvasionCount {get;set;}=4;
    public int EvasionWindow {get;set;}=60;
    public int BowDualEvasionWindow {get;set;}=30;
    public int BowPowerCount {get;set;}=3;
    public double BowPowerWindow {get;set;}=8;
    public bool Valid()
    {
        bool Ratio(double v)=>double.IsFinite(v)&&v>0&&v<=1;
        bool Time(double v)=>double.IsFinite(v)&&v>=0&&v<=3600;
        return new[]{EventFreshnessSeconds,DynamicEventTimeoutSeconds,CombatResultMaxAgeSeconds}.All(v=>Time(v)&&v>=.1)&&new[]{NativeOutputIntervalSeconds,SharedOutputIntervalSeconds,AiReplyQuietSeconds}.All(Time)&&new[]{LowHealthRatio,LowHealthResetRatio,HealthLossRatio,PlayerHealRatio,PalicoArmRatio,PalicoHealRatio}.All(Ratio)&&LowHealthResetRatio>LowHealthRatio&&new[]{SameActionExtraCooldown,MantleCooldown,HealthLossWindow,PlayerHealWindow,PlayerHealCooldown,PalicoHealWindow,PalicoHealCooldown,BowPowerWindow}.All(Time)&&double.IsFinite(PlayerHealMinimum)&&PlayerHealMinimum>=0&&PlayerHealMinimum<=10000&&EvasionCount is >=1 and <=100&&EvasionWindow is >=1 and <=3600&&BowDualEvasionWindow is >=1 and <=3600&&BowPowerCount is >=1 and <=100;
    }
}
public static class ResponseTuning
{
    public static ResponseSettings Current {get;set;}=new();
    public static ResponseThresholds Thresholds=>Current.Thresholds;
    public static double Cooldown(string category,double fallback)=>Current.Categories.TryGetValue(category,out var group)?group.CooldownSeconds:fallback;
}

public static class RuntimeResponseText
{
    public static string ApplyEventBoundaries(string id,string weapon,int? level,string proposed,string fallback)
    {
        if(weapon!="LongSword"||id.StartsWith("spirit_level_up_",StringComparison.Ordinal))return proposed;
        // A confirmed hit/counter is not evidence of another gauge promotion; an AI paraphrase cannot override that boundary.
        if(new[]{"升刃","开刃","開刃","升到","升成","升上"}.Any(proposed.Contains)||level==3&&(proposed.Contains("红刃")||proposed.Contains("紅刃")))return fallback;
        return proposed;
    }
}
