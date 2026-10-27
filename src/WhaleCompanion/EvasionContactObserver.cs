using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SharpPluginLoader.Core.Memory;
namespace WhaleCompanion;

// Revision-locked diagnostic contact callback. Always forwards all nine integer/pointer arguments.
// No action requests, timer writes, callback replacement or success responses.
public sealed class EvasionContactObserver:IDisposable
{
    private const string Signature="4C 8B DC 41 57 48 81 EC 80 00 00 00 48 8B 81 A8 01 00 00 44 8B D2 4C 8B F9 48 85 C0";
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Contact(nint self,nint result,nint first,nint second,nint contact,nint sixth,nint seventh,nint firstIndex,nint secondIndex);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint Category(nint self,nint parameter,nint owner);
    private Reloaded.Hooks.Definitions.IHook<Category>? categoryHook;private Category? originalCategory;
    [ThreadStatic] private static Frame? activeFrame;
    [ThreadStatic] private static int categoryDepth;
    private sealed class Frame(EvasionContactCandidate candidate,nint self,ulong attackDefinition,ulong targetDefinition,ulong attackOwner,ulong targetOwner)
    {
        public EvasionContactCandidate Candidate=candidate;public nint Self=self;
        public ulong AttackDefinition=attackDefinition,TargetDefinition=targetDefinition,AttackOwner=attackOwner,TargetOwner=targetOwner;
        public uint? AttackKind,TargetKind;
    }
    private long categoryCalls,localCategoryReturns;
    public long CategoryCalls=>Interlocked.Read(ref categoryCalls);public long LocalCategoryReturns=>Interlocked.Read(ref localCategoryReturns);
    private Reloaded.Hooks.Definitions.IHook<Contact>? hook;private Contact? original;private EvasionEpisode? state;private readonly EvasionEpisodeTracker episodes=new();private readonly Func<double> clock;private readonly LocalProcessMemory memory=new();
    private readonly ConcurrentQueue<EvasionContactCandidate> queue=new();private int queued;private long calls,local,dropped,throttled,nextWindowTick,nextOtherTick,lastPublishedSuccessId;private int attempted;
    public string Status{get;private set;}="Not started";
    public string EpisodeStatus=>episodes.Status;
    public float? ActiveTime=>episodes.ActiveTime;
    public long Calls=>Interlocked.Read(ref calls);public long Local=>Interlocked.Read(ref local);public long Dropped=>Interlocked.Read(ref dropped);public long Throttled=>Interlocked.Read(ref throttled);
    public EvasionContactObserver(Func<double> clock){this.clock=clock;}
    public void SetContext(NativeActionSample? value)
    {
        if(value==null){Volatile.Write(ref state,null);episodes.Reset();while(queue.TryDequeue(out _))Interlocked.Decrement(ref queued);return;}
        Volatile.Write(ref state,episodes.Track(memory,value));
    }
    public void Start(string revision)
    {
        if(!CombatHookIsolation.Allowed){Status="Disabled for stability isolation";return;}
        if(Interlocked.Exchange(ref attempted,1)!=0)return;
        if(revision!="421810"){Status="Blocked: unsupported revision";return;}
        try
        {
            var module=System.Diagnostics.Process.GetCurrentProcess().MainModule!;
            if(!Path.GetFileName(module.FileName).Equals("MonsterHunterWorld.exe",StringComparison.OrdinalIgnoreCase)){Status="Blocked: not game";return;}
            var matches=PatternScanner.Scan(Signature);
            if(matches.Count!=1||(ulong)matches[0]-(ulong)module.BaseAddress!=0x12D6D20){Status="Blocked: signature/RVA mismatch";return;}
            var categories=PatternScanner.Scan("48 83 EC 48 48 8D 05 ?? ?? ?? ?? 48 89 54 24 38 48 89 44 24 20 49 8B C8 8B 42 50");
            if(categories.Count!=1||(ulong)categories[0]-(ulong)module.BaseAddress!=0x12DDE80){Status="Blocked: category signature/RVA mismatch";return;}
            var pending=Reloaded.Hooks.ReloadedHooks.Instance.CreateHook<Contact>(Detour,matches[0]);
            var categoryPending=Reloaded.Hooks.ReloadedHooks.Instance.CreateHook<Category>(CategoryDetour,categories[0]);
            hook=pending;original=pending.OriginalFunction;categoryHook=categoryPending;originalCategory=categoryPending.OriginalFunction;
            categoryPending.Activate();pending.Activate();Status="Diagnostic contact + native categories; success unknown";
        }
        catch(Exception error){hook?.Disable();categoryHook?.Disable();Status="Unavailable: "+error.GetType().Name;}
    }
    private void Detour(nint self,nint result,nint first,nint second,nint contact,nint sixth,nint seventh,nint firstIndex,nint secondIndex)
    {
        var previous=activeFrame;Frame? frame=null;
        try
        {
            Interlocked.Increment(ref calls);var current=Volatile.Read(ref state);
            double now=clock();
            if(current!=null&&now-current.Action.Time is >=0 and <=.25&&memory.Pointer(current.Action.Owner+0x6A8,out var localBody)&&memory.Pointer((ulong)second+0x80,out var targetBody)&&targetBody==localBody&&NativeActionSignal.TryRead(memory,current.Action.Owner,current.Action.Weapon,"421810",now,out var actual)&&actual!=null&&EvasionContactProbe.TryRead(memory,actual,now,result,(ulong)first,(ulong)second,out var value)&&value!=null)
            {
                Interlocked.Increment(ref local);
                EvasionContactProbe.TryDefinition(memory,(ulong)first,out var attackDefinition,out var attackOwner);
                EvasionContactProbe.TryDefinition(memory,(ulong)second,out var targetDefinition,out var targetOwner);
                value=value with{EvasionId=episodes.Track(memory,actual).Id};
                frame=new(value,self,attackDefinition,targetDefinition,attackOwner,targetOwner);
            }
        }
        catch{ /* Observation must never prevent original collision processing. */ }
        activeFrame=frame;
        try{original!(self,result,first,second,contact,sixth,seventh,firstIndex,secondIndex);}
        finally
        {
            activeFrame=previous;
            if(frame!=null&&Volatile.Read(ref state) is {} fresh&&fresh.Action.Owner==frame.Candidate.Owner)try{var value=frame.Candidate;Publish(value with{AttackKind=frame.AttackKind,TargetKind=frame.TargetKind,InvulnerabilityFilterCandidate=EvasionContactProbe.FilterCandidate(value.CollisionFlags,value.SourceCollisionFlags,frame.AttackKind,frame.TargetKind)});}catch{}
        }
    }
    private uint CategoryDetour(nint self,nint parameter,nint owner)
    {
        Interlocked.Increment(ref categoryCalls);var frame=activeFrame;int depth=categoryDepth++;uint result;
        try{result=originalCategory!(self,parameter,owner);}finally{categoryDepth--;}
        try
        {
            if(depth==0&&frame!=null&&self==frame.Self&&activeFrame==frame)
            {
                bool attack=frame.AttackDefinition!=0&&(ulong)parameter==frame.AttackDefinition&&(ulong)owner==frame.AttackOwner;
                bool target=frame.TargetDefinition!=0&&(ulong)parameter==frame.TargetDefinition&&(ulong)owner==frame.TargetOwner;
                if(attack!=target){if(attack){frame.AttackKind??=result;}else{frame.TargetKind??=result;}Interlocked.Increment(ref localCategoryReturns);}
            }
        }
        catch{ /* Return exactly the native category result even if observation fails. */ }
        return result;
    }
    private void Publish(EvasionContactCandidate value)
    {
        // Separate sampling budgets preserve window contacts while bounding idle/control logs.
        ref long next=ref (value.InvulnerabilityFilterCandidate==true&&value.Scope!=null&&value.Result==0?ref nextWindowTick:ref nextOtherTick);
        long tick=(long)(value.Time*1000),previous=Interlocked.Read(ref next);
        // A first qualifying contact for a new actual evade must never be lost to log throttling.
        long published=Interlocked.Read(ref lastPublishedSuccessId);
        bool firstSuccess=EvasionContactDirector.Qualifies(value)&&value.EvasionId>published&&Interlocked.CompareExchange(ref lastPublishedSuccessId,value.EvasionId,published)==published;
        if(!firstSuccess&&(tick<previous||Interlocked.CompareExchange(ref next,tick+50,previous)!=previous)){Interlocked.Increment(ref throttled);return;}
        if(Interlocked.Increment(ref queued)<=128)queue.Enqueue(value);
        else{Interlocked.Decrement(ref queued);Interlocked.Increment(ref dropped);}
    }
    public EvasionContactCandidate[] Drain()
    {
        var result=new List<EvasionContactCandidate>();
        while(result.Count<128&&queue.TryDequeue(out var value)){Interlocked.Decrement(ref queued);result.Add(value);}return result.ToArray();
    }
    public void Dispose(){SetContext(null);hook?.Disable();categoryHook?.Disable();Drain();}
}
