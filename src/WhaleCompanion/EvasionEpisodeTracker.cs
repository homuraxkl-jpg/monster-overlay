namespace WhaleCompanion;

public sealed record EvasionEpisode(NativeActionSample Action,long Id);
// cActionBase.ActiveTime(+8) restarts on re-execution, including consecutive identical action IDs.
// No requests/timing guesses/native writes. A missing or unstable action never gains a token.
public sealed class EvasionEpisodeTracker
{
    private readonly object gate=new();
    private ulong owner,instance;private string weapon="";private int set=-1,id=-1;private float elapsed;private long serial;
    public string Status {get;private set;}="waiting";
    public float? ActiveTime {get;private set;}
    public void Reset(){lock(gate)ResetCore();}
    private void ResetCore(){Status="waiting";ActiveTime=null;owner=instance=0;weapon="";set=id=-1;elapsed=0;}
    public EvasionEpisode Track(IReadOnlyLocalMemory memory,NativeActionSample sample)
    {lock(gate)return TrackCore(memory,sample);}
    private EvasionEpisode TrackCore(IReadOnlyLocalMemory memory,NativeActionSample sample)
    {
        if(!double.IsFinite(sample.Time)||!memory.Int32(sample.Owner+0x6274,out var liveSet)||liveSet!=sample.Set||!memory.Int32(sample.Owner+0x6278,out var liveId)||liveId!=sample.Id){Status="stale_action";return new(sample,0);}
        if(EvasionActionScope.Identify(sample.Weapon,sample.Set,sample.Id)==null){ResetCore();Status="outside_scope";return new(sample,0);}
        if(!ActionCodeProbe.TryLocate(memory,sample.Owner,sample.Set,sample.Id,out var action,out _,out var parentMatches,out var reason)){Status=reason;return new(sample,0);}
        if(!parentMatches){Status="parent_mismatch";return new(sample,0);}
        if(!memory.Int32(action+8,out var raw)){Status="active_time_unreadable";return new(sample,0);}
        float active=BitConverter.Int32BitsToSingle(raw);
        if(!float.IsFinite(active)||active<0||active>86400){Status="active_time_invalid";return new(sample,0);}
        if(!memory.Int32(sample.Owner+0x6274,out var actualSet)||actualSet!=sample.Set||!memory.Int32(sample.Owner+0x6278,out var actualId)||actualId!=sample.Id||!ActionCodeProbe.TryLocate(memory,sample.Owner,sample.Set,sample.Id,out var after,out _,out var parentAfter,out _)||after!=action||!parentAfter){Status="identity_changed";return new(sample,0);}
        if(owner!=sample.Owner||weapon!=sample.Weapon||instance!=action||set!=sample.Set||id!=sample.Id||active+.0001f<elapsed)serial++;
        owner=sample.Owner;weapon=sample.Weapon;instance=action;set=sample.Set;id=sample.Id;elapsed=active;
        Status="ready";ActiveTime=active;return new(sample,serial);
    }
}
