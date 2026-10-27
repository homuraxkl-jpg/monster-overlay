using System.Collections.Concurrent;

namespace WhaleCompanion;

public sealed record DamageObservation(int Epoch,double Time,ulong Target,int Amount,int AttackId,int Unknown0=0,int Unknown1=0,int Unknown2=0,ulong VerifiedSourceOwner=0);
public sealed record AttackObservation(int Epoch,ulong Owner,ActionObservation Action);
public sealed record AttackDamageResult(long Instance,int Epoch,string Weapon,int ActionId,string Label,string Verdict,int ObservedCallbacks,long ObservedDamage,int ConfirmedCallbacks,long ConfirmedDamage,bool CoverageComplete,int[] Stages);

// Game callback only enqueues bounded facts. Attribution runs on update, not on a damage hook.
public sealed class DamageCaptureBuffer
{
    private readonly ConcurrentQueue<object> queue=new();
    private int count;
    private long dropped;
    public long Dropped=>Interlocked.Read(ref dropped);
    public void Add(object value)
    {
        if(Interlocked.Increment(ref count)>512){Interlocked.Decrement(ref count);Interlocked.Increment(ref dropped);return;}
        queue.Enqueue(value);
    }
    public object[] Drain()
    {
        var result=new List<object>();
        while(result.Count<512&&queue.TryDequeue(out var value)){Interlocked.Decrement(ref count);result.Add(value);}
        return result.OrderBy(x=>x is DamageObservation d?d.Time:((AttackObservation)x).Action.Time).ThenBy(x=>x is AttackObservation?0:1).ToArray();
    }
    public void Clear(){while(queue.TryDequeue(out _))Interlocked.Decrement(ref count);}
}

public sealed class DamageAttribution
{
    private sealed class Attempt
    {
        public required AttackObservation Observation;
        public required WeaponActionTag Tag;
        public long Id;public double Deadline;public int Count,Confirmed;public long Damage,ConfirmedDamage;public bool Gap,Ambiguous;
        public int LastStage;public double LastStageTime;public HashSet<int> Stages=new();
    }
    private readonly List<Attempt> attempts=new();
    private readonly HashSet<(string Weapon,int Action,int Damage)> bindings=new();
    private long sequence;
    public long Unattributed {get;private set;}
    public long Ambiguous {get;private set;}
    public void Reset(){attempts.Clear();Unattributed=Ambiguous=0;}
    // Only a separately validated provider may supply a source owner and validated attack binding.
    // Native DrawDamage has neither verified today. No settings switch upgrades confidence.
    public void BindValidatedAttack(string weapon,int action,int damage)=>bindings.Add((weapon,action,damage));
    public void MarkGap(){foreach(var attempt in attempts)attempt.Gap=true;}
    public void Action(AttackObservation value)
    {
        if(value.Owner==0||!double.IsFinite(value.Action.Time))return;
        foreach(var old in attempts.Where(a=>a.Observation.Epoch==value.Epoch&&a.Observation.Owner==value.Owner&&a.Tag.Delay<=0))
            old.Deadline=Math.Min(old.Deadline,value.Action.Time+.15);
        var tag=WeaponActions.Identify(value.Action.Weapon,value.Action.Set,value.Action.Id);
        if(tag is not {Attack:true})return;
        var parent=attempts.LastOrDefault(a=>a.Observation.Epoch==value.Epoch&&a.Observation.Owner==value.Owner&&a.Observation.Action.Weapon==value.Action.Weapon&&value.Action.Time>=a.LastStageTime&&value.Action.Time<=a.Deadline&&WeaponActions.Continues(value.Action.Weapon,a.LastStage,value.Action.Id));
        if(parent!=null)
        {parent.LastStage=value.Action.Id;parent.LastStageTime=value.Action.Time;parent.Stages.Add(value.Action.Id);parent.Tag=tag;parent.Deadline=Math.Max(parent.Deadline,value.Action.Time+tag.Window+tag.Delay);return;}
        if(attempts.Count>=64){MarkGap();attempts.RemoveAt(0);}
        attempts.Add(new(){Observation=value,Tag=tag,Id=++sequence,Deadline=value.Action.Time+tag.Window+tag.Delay,LastStage=value.Action.Id,LastStageTime=value.Action.Time,Stages=[value.Action.Id]});
    }
    public void Damage(DamageObservation value)
    {
        if(!double.IsFinite(value.Time)||value.Target==0||value.Amount<=0||value.Amount>10000000){Unattributed++;return;}
        var eligible=attempts.Where(a=>a.Observation.Epoch==value.Epoch&&value.Time>=a.Observation.Action.Time&&value.Time<=a.Deadline&&(value.VerifiedSourceOwner==0||value.VerifiedSourceOwner==a.Observation.Owner)).ToArray();
        if(eligible.Length!=1)
        {
            if(eligible.Length>1){Ambiguous++;foreach(var a in eligible)a.Ambiguous=true;}else Unattributed++;
            return;
        }
        var attempt=eligible[0];attempt.Count++;attempt.Damage+=value.Amount;
        if(value.VerifiedSourceOwner!=0&&!attempt.Gap&&attempt.Stages.Any(id=>bindings.Contains((attempt.Observation.Action.Weapon,id,value.AttackId))))
        {attempt.Confirmed++;attempt.ConfirmedDamage+=value.Amount;}
    }
    public AttackDamageResult[] Finish(int epoch,double now)
    {
        if(!double.IsFinite(now))return [];
        var completed=attempts.Where(a=>a.Observation.Epoch!=epoch||now>a.Deadline).ToArray();
        var result=completed.Select(a=>new AttackDamageResult(a.Id,a.Observation.Epoch,a.Observation.Action.Weapon,a.LastStage,a.Tag.Label,
            a.Observation.Epoch!=epoch?"scene_changed":a.Gap?"coverage_gap":a.Ambiguous?"ambiguous":a.Confirmed>0?"confirmed_hit":a.Count>0?"temporal_candidate":"unknown_no_damage",
            a.Count,a.Damage,a.Confirmed,a.ConfirmedDamage,!a.Gap,a.Stages.Order().ToArray())).ToArray();
        foreach(var a in completed)attempts.Remove(a);
        return result;
    }
}
