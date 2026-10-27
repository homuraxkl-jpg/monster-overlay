using System.Runtime.InteropServices;
using SharpPluginLoader.Core;
using SharpPluginLoader.Core.Memory;

namespace WhaleCompanion;

// Candidate route: SPL's existing local display entry, not a confirmed Palico UI.
// Created only when the user starts the battle capture. All arguments forwarded.
public sealed class PalicoMessageObserver : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void Display(nint chat,nint utf8,float delay,uint flags,byte important);
    private Hook<Display>? hook;
    private nint address;
    private NativeMessageInterop? checkDisplay;
    private readonly PalicoBattleCapture capture;
    private readonly IReadOnlyGmdMemory memory;
    private readonly Func<double> time;
    public PalicoMessageObserver(PalicoBattleCapture capture,IReadOnlyGmdMemory memory,Func<double> time)
    {this.capture=capture;this.memory=memory;this.time=time;}
    public void Initialize()
    {
        if(hook!=null)return;
        var field=typeof(Gui).GetField("DisplayMessageFunc",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)
            ?? throw new MissingFieldException("SPL display entry unavailable");
        var function=field.GetValue(null)??throw new InvalidOperationException("No SPL display entry");
        if(function.GetType().GetProperty("NativePointer")?.GetValue(function) is not nint resolved || resolved==0)
            throw new InvalidOperationException("No SPL display address");
        address=resolved;
        hook=Hook.Create<Display>((long)address,Detour);
    }
    public void CheckLocalPath()
    {
        Initialize();
        var chat=SingletonManager.GetSingleton("sChat")??throw new InvalidOperationException("sChat not ready");
        checkDisplay??=new(address);
        checkDisplay.Display(chat.Instance,"随从对白观察通路测试（仅本机，无API调用）");
    }
    private void Detour(nint chat,nint text,float delay,uint flags,byte important) =>
        PalicoBattleCapture.Forward(()=>capture.Observe((ulong)text,time(),memory),()=>hook!.Original(chat,text,delay,flags,important));
    public void Dispose(){capture.Stop();hook?.Dispose();checkDisplay?.Dispose();}
}
