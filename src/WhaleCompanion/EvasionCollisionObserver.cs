using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SharpPluginLoader.Core.Memory;
namespace WhaleCompanion;

public sealed record EvasionCollisionCandidate(double Time,string Weapon,int Set,int Id,string? Scope,int? EventKind,string TypeGetterCode,uint? SourceFlags,int? ExclusionType,bool? SourceEligible=null);
// Experimental observation of the shared event entry called by the mantle-specific handler.
// Does not replace game collision decisions or set mantle timers. Disabled until explicitly selected.
public sealed class EvasionCollisionObserver:IDisposable
{
    private const string Signature="40 55 53 57 48 8B EC 48 83 EC 70 48 8B 02 48 8B F9 48 8B CA 48 8B DA FF 50 08 83 C0 FA 83 F8 38";
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Dispatch(nint self,nint message);
    private Hook<Dispatch>? hook; private int enabled; private long calls,localCalls,dropped,type25;
    private ulong module,end;
    private readonly LocalProcessMemory memory=new();private readonly SelfChatMemory codeMemory=new();
    private readonly Func<NativeActionSample?> current;private readonly Func<double> clock;
    private readonly ConcurrentQueue<EvasionCollisionCandidate> queue=new();
    public string Status{get;private set;}="Collision research OFF";
    public long Type25=>Interlocked.Read(ref type25);
    public long Calls=>Interlocked.Read(ref calls);public long LocalCalls=>Interlocked.Read(ref localCalls);public long Dropped=>Interlocked.Read(ref dropped);
    public EvasionCollisionObserver(Func<NativeActionSample?> current,Func<double> clock){this.current=current;this.clock=clock;}
    public bool Start(string revision)
    {
        if(revision!="421810"){Status="Blocked: unsupported revision";return false;}
        try
        {
            if(hook==null)
            {
                var processModule=System.Diagnostics.Process.GetCurrentProcess().MainModule!;module=(ulong)processModule.BaseAddress;end=module+(uint)processModule.ModuleMemorySize;
                var matches=PatternScanner.Scan(Signature);
                if(matches.Count!=1||(ulong)matches[0]-module!=0x2080810){Status="Blocked: entry signature/RVA mismatch";return false;}
                hook=Hook.Create<Dispatch>(matches[0],Detour);
            }
            Volatile.Write(ref enabled,1);Status="Base event observer active (2080810); success semantics unvalidated";return true;
        }
        catch(Exception ex){Status="Unavailable: "+ex.GetType().Name;return false;}
    }
    public void Stop(){Volatile.Write(ref enabled,0);while(queue.TryDequeue(out _)){}Status="Collision research OFF";}
    private nint Detour(nint self,nint message)
    {
        try
        {
            if(Volatile.Read(ref enabled)!=0)
            {
                Interlocked.Increment(ref calls);var state=current();double now=clock();
                if(state!=null&&now-state.Time is >=0 and <=.25&&memory.Pointer((ulong)self+0xB40,out var owner)&&owner==state.Owner)
                {
                    Interlocked.Increment(ref localCalls);
                    var scope=EvasionActionScope.Identify(state.Weapon,state.Set,state.Id);
                    if(scope!=null)
                    {
                        int? kind=null;string getterCode="";
                        if(memory.Pointer((ulong)message,out var table)&&memory.Pointer(table+8,out var getter)&&getter>=module&&getter+12<=end&&codeMemory.Read(getter,12,out var bytes))
                        {
                            getterCode=Convert.ToHexString(bytes);
                            if(bytes[0]==0xB8&&bytes[5]==0xC3)kind=BitConverter.ToInt32(bytes,1);
                        }
                        if(kind==25)Interlocked.Increment(ref type25);
                        uint? flags=null;int? excluded=null;bool? sourceEligible=null;
                        if(kind==8&&memory.Pointer((ulong)message+0x18,out var payload))
                        {
                            if(memory.Pointer(payload+0xB8,out var collision)&&memory.Int32(collision+0xA0,out var type))excluded=type;
                            if(EvasionEventSource.TryResolve(memory,payload,out var source))sourceEligible=EvasionEventSource.Eligible(memory,source,out flags);
                        }
                        // Source accessor is reproduced with bounded OS reads; no native accessor is invoked.
                        if(kind!=25){if(queue.Count<128)queue.Enqueue(new(now,state.Weapon,state.Set,state.Id,scope,kind,getterCode,flags,excluded,sourceEligible));else Interlocked.Increment(ref dropped);}
                    }
                }
            }
        }
        catch{ /* Always forward the original entry, including unknown/non-local events. */ }
        return hook!.Original(self,message);
    }
    public EvasionCollisionCandidate[] Drain(){var result=new List<EvasionCollisionCandidate>();while(result.Count<128&&queue.TryDequeue(out var value))result.Add(value);return result.ToArray();}
    public void Dispose(){Stop();hook?.Dispose();hook=null;}
}





