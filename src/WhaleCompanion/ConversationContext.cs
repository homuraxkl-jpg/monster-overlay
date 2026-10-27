using System.Text.Json;
namespace WhaleCompanion;

public sealed class ConversationOptions
{
    public string OutputMode {get;set;}="native";
    public bool Multiplayer {get;set;}=false;
    public bool NativeChat {get;set;}=true;
    public bool ShowStatusOverlay {get;set;}=false;
    public bool ReadPeers {get;set;}=true;
    public bool ReplyToPeers {get;set;}=true;
    public bool RetainHuntContext {get;set;}=true;
    public bool ProactiveHuntComments {get;set;}=false;
    public double ProactiveIntervalSeconds {get;set;}=45;
    public double NativeChatCooldownSeconds {get;set;}=8;
    public static ConversationOptions Load(string path)
    {
        if(!File.Exists(path))return new();
        if(new FileInfo(path).Length>16000)throw new InvalidDataException();
        var value=JsonSerializer.Deserialize<ConversationOptions>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException();
        if(value.OutputMode is not ("native" or "subtitle")||!double.IsFinite(value.ProactiveIntervalSeconds)||value.ProactiveIntervalSeconds<10||value.ProactiveIntervalSeconds>3600||!double.IsFinite(value.NativeChatCooldownSeconds)||value.NativeChatCooldownSeconds<0||value.NativeChatCooldownSeconds>300)throw new InvalidDataException();return value;
    }
    public void Save(string path)=>OfflineDialogue.AtomicWrite(path,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
}
public sealed record ObservedContextLine(string Source,string Event,string Text,double Time);
// In-memory observations only. Silence does not erase chat. No automatic permanent memories.
public sealed class ObservedContext
{
    readonly Queue<ObservedContextLine> lines=new();
    readonly object gate=new();long version;
    public long Version {get{lock(gate)return version;}}
    public int Count{get{lock(gate)return lines.Count;}}
    public int Capacity {get;init;}=64;
    public double RetentionSeconds {get;init;}=1200;
    public void Clear(){lock(gate){lines.Clear();version++;}}
    public void Add(string source,string kind,string text,double now)
    {
        lock(gate)
        {
        if(Capacity<1||!double.IsFinite(now)||string.IsNullOrWhiteSpace(text))return;
        Prune(now);text=text.Length>600?text[..600]:text;
        if(lines.TryPeek(out _)&&lines.Last() is {} last&&last.Source==source&&last.Event==kind&&last.Text==text&&now-last.Time<2)return;
        while(lines.Count>=Capacity)lines.Dequeue();lines.Enqueue(new(source,kind,text,now));version++;
        }
    }
    public bool Recent(string source,string text,double now,double seconds){lock(gate)return lines.Any(x=>x.Source==source&&x.Text==text&&now>=x.Time&&now-x.Time<=seconds);}
    void Prune(double now){while(lines.TryPeek(out var first)&&(now<first.Time||now-first.Time>RetentionSeconds))lines.Dequeue();}
    public string Snapshot(double now,int maximum=4000)
    {
        lock(gate)
        {
        Prune(now);var selected=new List<object>();string result="[]";
        foreach(var line in lines.Reverse())
        {
            selected.Insert(0,new{source=line.Source,@event=line.Event,text=line.Text,age_seconds=Math.Round(now-line.Time,1)});
            var candidate=JsonSerializer.Serialize(selected,new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
            if(candidate.Length>maximum)break;result=candidate;
        }
        return result;
        }
    }
}

// Compact counts survive recent-line eviction. They count observations, not exact gameplay actions.
public sealed class HuntLedger
{
    readonly object gate=new();readonly Dictionary<string,int> counts=new();
    Dictionary<string,int> previous=new();int? quest,previousQuest;bool closed;
    public void Observe(string kind){lock(gate){if(counts.Count>=64&&!counts.ContainsKey(kind))kind="other_observations";counts[kind]=Math.Min(100000,counts.GetValueOrDefault(kind)+1);}}
    public void NewEncounter(int id){lock(gate){if(counts.Count>0){previous=new(counts);previousQuest=quest;}counts.Clear();quest=id;closed=false;}}
    public void EndEncounter(){lock(gate)closed=true;}
    public void Clear(){lock(gate){counts.Clear();previous.Clear();quest=previousQuest=null;closed=true;}}
    public object Snapshot(){lock(gate)return new{quest_id=quest,encounter_closed=closed,observation_counts=new Dictionary<string,int>(counts),previous_quest_id=previousQuest,previous_observation_counts=new Dictionary<string,int>(previous),semantics="record counts only, not exact hit/action totals, player attribution, or DPS"};}
}
