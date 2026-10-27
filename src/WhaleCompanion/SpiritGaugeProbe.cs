using System.Runtime.InteropServices;

namespace WhaleCompanion;

public static class SpiritDiagnosticGate
{
    public static string? Blocked(bool enabled, bool scenePaused) =>
        !enabled ? "Probe OFF: start/resume in loaded save" :
        scenePaused ? "Scene paused: start/resume after loading save" : null;
}

public interface IReadOnlyLocalMemory
{
    bool Pointer(ulong address, out ulong value);
    bool Int32(ulong address, out int value);
}

// Reads only this process through the OS: no direct dereferences, patching or new game hooks.
public sealed class LocalProcessMemory : IReadOnlyLocalMemory
{
    public static bool IsAddress(ulong address, uint size) => address >= 0x10000 && address <= 0x00007FFFFFFFFFFFUL - size;
    [DllImport("kernel32.dll", EntryPoint = "ReadProcessMemory", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadPointer(nint process, nint address, out ulong value, nuint size, out nuint read);
    [DllImport("kernel32.dll", EntryPoint = "ReadProcessMemory", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadInteger(nint process, nint address, out int value, nuint size, out nuint read);
    public bool Pointer(ulong address, out ulong value)
    {
        value = 0;
        return IsAddress(address, 8) && ReadPointer(-1, (nint)address, out value, 8, out var size) && size == 8;
    }
    public bool Int32(ulong address, out int value)
    {
        value = 0;
        return IsAddress(address, 4) && ReadInteger(-1, (nint)address, out value, 4, out var size) && size == 4;
    }
}

public static class SpiritGaugeProbe
{
    // Field-chain reference: eigeen/more-auto-shoutouts e433fcac, src/game.rs.
    // Its PLAYER_BASE -> [0x50] yields the same player instance supplied by SPL.
    // Start at that instance; dereference four fields, then read an int32 at +0x2370.
    private static readonly uint[] PointerOffsets = [0x468, 0x70, 0x10, 0x18];
    public static bool TryRead(IReadOnlyLocalMemory memory, ulong player, out int value, out string status)
    {
        value = -1; status = "Unknown: no valid player";
        if (!LocalProcessMemory.IsAddress(player, 8)) return false;
        var current = player;
        for (int i = 0; i < PointerOffsets.Length; i++)
        {
            var offset = PointerOffsets[i];
            if (!LocalProcessMemory.IsAddress(current, offset + 8) || !memory.Pointer(current + offset, out var next) || !LocalProcessMemory.IsAddress(next, 8))
            { status = $"Unknown: field-chain step {i + 1} unreadable"; return false; }
            current = next;
        }
        if (!LocalProcessMemory.IsAddress(current, 0x2374) || !memory.Int32(current + 0x2370, out value))
        { status = "Unknown: level field unreadable"; return false; }
        if (value is < 0 or > 3) { status = $"Unknown: out-of-range raw value {value}"; return false; }
        status = "Gauge level read (training-field verified)"; return true;
    }
}

public sealed record SpiritChange(int? Previous, int Current);
public sealed class SpiritCompanion
{
    private double nextAllowed;
    private int variant;
    public void Reset() { nextAllowed = 0; variant = 0; }
    public static string LevelName(int? level) => level switch { 0 => "无刃", 1 => "白刃", 2 => "黄刃", 3 => "红刃", _ => "未知" };
    public ActionReaction? Observe(SpiritChange change, double now)
    {
        if (!double.IsFinite(now) || now < nextAllowed || change.Previous is not (>= 0 and <= 3) || change.Current is < 1 or > 3 || change.Current <= change.Previous) return null;
        nextAllowed = now + 18;
        var alternate = variant++ % 2 != 0;
        var text = change.Current switch
        {
            1 => alternate ? "白刃亮啦，哼，我有在看。" : "升到白刃了，慢慢来，本鱼陪你找节奏。",
            2 => alternate ? "黄刃了诶，再接再厉……我才没看入迷。" : "黄刃亮起来了，打得不错嘛。",
            _ => alternate ? "红刃！这下本鱼可要认真看你表演了。" : "红刃啦！哼，打得这么好，回去记得请本鱼吃饭。"
        };
        return new("spirit_level_up", text, "happy", change.Current == 3 ? .75f : .5f,
            $"已验证的太刀气刃等级从{LevelName(change.Previous)}升至{LevelName(change.Current)}。只确认升刃，不推断具体招式、反击、登龙命中或伤害。用人设说一句简短夸奖，避免照抄：{text}");
    }
}
public sealed class SpiritGaugeTracker
{
    private ulong owner;
    private double lastTime = double.NaN;
    private int? candidate;
    private int count;
    public int? Raw { get; private set; }
    public int? Stable { get; private set; }
    public double SampleTime => lastTime;
    public void Reset() { owner = 0; lastTime = double.NaN; candidate = Raw = Stable = null; count = 0; }
    public SpiritChange? Observe(ulong player, int? value, double now)
    {
        if (player == 0 || value is null or < 0 or > 3 || !double.IsFinite(now)) { Reset(); return null; }
        if (owner != player || !double.IsFinite(lastTime) || now < lastTime || now - lastTime > 1) Reset();
        if (now == lastTime) return null; // Repeated callback at the same time is not another sample.
        owner = player; lastTime = now; Raw = value;
        if (candidate != value) { candidate = value; count = 1; } else count++;
        if (count < 3 || Stable == value) return null;
        var old = Stable; Stable = value; return new(old, value.Value);
    }
}
