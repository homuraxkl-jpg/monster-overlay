using System.Runtime.InteropServices;
using SharpPluginLoader.Core.Memory;

namespace WhaleCompanion;

// MAS e433fcac / mhw-toolkit 0a593484, Apache-2.0. Nine-argument x64 ABI.
// This is an observation of the damage DISPLAY route, not proof of damage ownership.
public sealed class DamageObserver:IDisposable
{
    private const string Signature="8B 84 ?? ?? ?? ?? ?? C6 44 ?? ?? ?? C7 44 ?? ?? ?? ?? ?? ?? 89 44 ?? ?? 0F B6 ?? ?? ?? ?? ?? ?? 88 44 ?? ?? 8B 84";
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void Draw(nint target,int damage,nint position,int tenderized,int crit,int unk0,int unk1,sbyte unk2,int attackId);
    private Hook<Draw>? hook;
    private readonly DamageCaptureBuffer buffer;
    private readonly Func<double> time;
    private readonly Func<int> epoch;
    private int enabled;
    public long Callbacks=>Interlocked.Read(ref calls);
    private long calls;
    public string Status {get;private set;}="Damage observer OFF";
    public DamageObserver(DamageCaptureBuffer buffer,Func<double> time,Func<int> epoch){this.buffer=buffer;this.time=time;this.epoch=epoch;}
    public bool Start(string revision)
    {
        if(!CombatHookIsolation.Allowed){Status="Disabled for stability isolation";return false;}
        if(revision!="421810"){Status="Damage observer blocked: unsupported revision";return false;}
        try
        {
            if(hook==null)
            {
                var matches=PatternScanner.Scan(Signature);
                if(matches.Count!=1){Status=$"Damage observer blocked: signature matches={matches.Count}";return false;}
                long address=matches[0]-13;
                long module=System.Diagnostics.Process.GetCurrentProcess().MainModule!.BaseAddress.ToInt64();
                if(address-module!=0x1CC5F80){Status=$"Damage observer blocked: signature RVA=0x{address-module:X} differs from 421810 map 0x1CC5F80";return false;}
                hook=Hook.Create<Draw>(address,Detour);
            }
            Volatile.Write(ref enabled,1);Status="Damage display observation active; source owner UNKNOWN; observation only";return true;
        }
        catch(Exception ex){Status="Damage observer unavailable: "+ex.GetType().Name;return false;}
    }
    public void Stop(){Volatile.Write(ref enabled,0);Status="Damage observer OFF";}
    private void Detour(nint target,int amount,nint position,int tenderized,int crit,int unk0,int unk1,sbyte unk2,int attackId)
    {
        try
        {
            if(Volatile.Read(ref enabled)!=0)
            {Interlocked.Increment(ref calls);buffer.Add(new DamageObservation(epoch(),time(),(ulong)target,amount,attackId,unk0,unk1,unk2));}
        }
        catch{ /* Observation must never suppress the original game function. */ }
        hook!.Original(target,amount,position,tenderized,crit,unk0,unk1,unk2,attackId);
    }
    public void Dispose(){Stop();hook?.Dispose();hook=null;}
}
