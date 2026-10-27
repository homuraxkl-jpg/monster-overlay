using System.Runtime.InteropServices;

namespace WhaleCompanion;

// Explicit UTF-8 pointer ABI. SPL 2.0.0's string-typed calli is bypassed.
public sealed class NativeMessageInterop : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate nint DisplayDelegate(nint chat, nint utf8, float delay, uint flags, byte important);
    private readonly DisplayDelegate display;
    private readonly Queue<nint> buffers = new();
    private bool disposed;
    public NativeMessageInterop(nint function)
    {
        if (function == 0) throw new ArgumentException("No native display function");
        display = Marshal.GetDelegateForFunctionPointer<DisplayDelegate>(function);
    }
    public void Display(nint chat, string text)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (chat == 0) throw new ArgumentException("No chat singleton");
        var utf8 = Marshal.StringToCoTaskMemUTF8(text);
        // Same bounded retention strategy as SPL's popup API, rather than a
        // transient managed string. 64 paced messages span at least 96 seconds.
        buffers.Enqueue(utf8);
        while (buffers.Count > 64) Marshal.FreeCoTaskMem(buffers.Dequeue());
        display(chat, utf8, 0, 0, 0);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        while (buffers.TryDequeue(out var buffer)) Marshal.FreeCoTaskMem(buffer);
    }
}
