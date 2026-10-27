using System.Security.Cryptography;

namespace WhaleCompanion;

public interface IReadOnlyGmdMemory
{
    bool Read(ulong address, int count, out byte[] bytes);
}

public sealed class LocalGmdMemory : IReadOnlyGmdMemory
{
    private readonly SelfChatMemory memory = new();
    public bool Read(ulong address, int count, out byte[] bytes) => memory.Read(address, count, out bytes);
}

public sealed record GmdFingerprint(int Index, int Length, string Sha256);
public sealed record PalicoResourceResult(string Status, int Count, int Matched, int Mismatched, int Unreadable, string Language = "chS", string Phase = "callback");
public sealed record PalicoCaptureStats(long Callbacks, long Targets, string[] CandidatePaths);

// Only immediate, bounded OS reads during an existing resource-load callback.
// No resource retention, native method calls, pointer writes, or dialogue events.
public static class PalicoResourceProbe
{
    public static IReadOnlyList<GmdFingerprint> Fingerprints { get; } = Array.AsReadOnly<GmdFingerprint>([
        new(2,37,"63B048159E3E6DD678880790FBB6B4E4C4C4183779BC0DF8C99FB1DB6806F2BB"),
        new(3,37,"82A844D341B030E39E25A5FA99D65B7EB47E41C4F5EF9E4028EB54C41B0F3619"),
        new(6,34,"081A1D2F746735249AD562AC5B63CAA28379C890DFAB5ED019F3CD7B70692E05"),
        new(7,48,"1AC616F7092CC81076E6EC32107595FC906D7144CAE54070277181EDBEAF9310"),
        new(39,48,"AD4CD29ACBB8B1048AE3EE07844E858C68997E5ADF408FDBFB8CA24D95EEABCC"),
        new(40,51,"AEDAF12F855E34494C9AF1C5D7BF6D66014CC49545C9D18E54843B455F05E1AE"),
        new(41,39,"8287B2E8E50F0599D88230673EE9D43B6DE17894F3BC3520B8CAD7ADAFC77922"),
        new(42,33,"AAD9BF31CC297205E0F13E99CF7CCE8BBF7C91549E3E26B47AB4A21FC9B6ADEA")
    ]);

    public static IReadOnlyList<GmdFingerprint> TraditionalFingerprints { get; } = Array.AsReadOnly<GmdFingerprint>([
        new(2,37,"00F70F5972CE5C69DB67C90000DF56F0FA5A9A4E22850526335161757AF473C9"),
        new(3,37,"66CB0FF5C06F06911C7725F4EE8CB0C39E7CCAC0C6C20B19E14B4364D32C3DE9"),
        new(6,34,"CE8FB9BA7DA578F98DB293AA14DB1DB9BB2FE51DB8ECAFB845C4CE4612AB9162"),
        new(7,42,"9C0872BD77369059A536A9B0B05637995514DDECEF36EC27063728948FC23EF7"),
        new(39,48,"9C98884F9C12B28FD3E5FE6FC7AB218565DC0BABE5AAB6F48B3B96D5204E425D"),
        new(40,54,"B14DD9D490E831CFB7278D7CE4A5CC447F5901FCB6C8E4EDF323AF6FACF78331"),
        new(41,39,"7508C955C2120526A55A50BFA021FB01F2874372340CAB13B7CC30B9F6A7B3EE"),
        new(42,33,"B627D4A04DBA47CC791607F2ABED0D941C8FDAEC46A4588A801691ACB33D9468")
    ]);

    public static string? LanguageForPath(string? path)
    {
        var normalized=path?.Replace('\\','/');
        if(string.Equals(normalized,"common/text/ot_auto_chS",StringComparison.OrdinalIgnoreCase))return "chS";
        if(string.Equals(normalized,"common/text/ot_auto_chT",StringComparison.OrdinalIgnoreCase))return "chT";
        return null;
    }
    public static bool IsTarget(string? path) => LanguageForPath(path)!=null;

    public static PalicoResourceResult Inspect(IReadOnlyGmdMemory memory, ulong resource, string language="chS") =>
        language is "chS" or "chT"
        ? InspectCore(memory,resource,language=="chT"?TraditionalFingerprints:Fingerprints) with {Language=language}
        : new("unsupported_language",0,0,0,0,language);

    private static PalicoResourceResult InspectCore(IReadOnlyGmdMemory memory, ulong resource, IReadOnlyList<GmdFingerprint> fingerprints)
    {
        bool Read(ulong address, int count, out byte[] bytes)
        {
            bytes = [];
            return LocalProcessMemory.IsAddress(address, (uint)count) && memory.Read(address,count,out bytes) && bytes.Length == count;
        }
        if(!LocalProcessMemory.IsAddress(resource,0x100) || !Read(resource+0xD8,4,out var countBytes))
            return new("resource_unreadable",0,0,0,0);
        var count=BitConverter.ToInt32(countBytes);
        if(count!=240) return new("count_mismatch",count,0,0,0);
        if(!Read(resource+0xF0,8,out var blockBytes)||!Read(resource+0xF8,8,out var tableBytes))
            return new("header_unreadable",count,0,0,0);
        var block=BitConverter.ToUInt64(blockBytes);var table=BitConverter.ToUInt64(tableBytes);
        if(!LocalProcessMemory.IsAddress(block,1)||!LocalProcessMemory.IsAddress(table,(uint)(count*8)))
            return new("layout_invalid",count,0,0,0);
        int matched=0,mismatched=0,unreadable=0;
        foreach(var item in fingerprints)
        {
            if(!Read(table+(uint)(item.Index*8),8,out var pointerBytes)){unreadable++;continue;}
            var pointer=BitConverter.ToUInt64(pointerBytes);
            if(pointer<block || !Read(pointer,item.Length,out var textBytes)){unreadable++;continue;}
            if(Convert.ToHexString(SHA256.HashData(textBytes))==item.Sha256)matched++;else mismatched++;
        }
        return new(matched==8?"matched_8_of_8":"sample_mismatch",count,matched,mismatched,unreadable);
    }
}

public sealed class PalicoResourceCapture
{
    private sealed record Pending(ulong Resource,string Language,ulong Vtable,double Expires,double Next);
    private readonly object gate = new();
    private readonly Queue<PalicoResourceResult> results = new();
    private readonly HashSet<(ulong Resource,string Language)> seen = new();
    private readonly HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Pending> pending = new();
    private long callbacks, targets;
    private double deadline;
    public void Start(double now,double seconds=180){lock(gate){results.Clear();pending.Clear();seen.Clear();candidates.Clear();callbacks=targets=0;deadline=double.IsFinite(now)&&double.IsFinite(seconds)?now+Math.Clamp(seconds,10,3600):0;}}
    public void Stop(){lock(gate){deadline=0;pending.Clear();seen.Clear();}}
    public bool Active(double now){lock(gate){return double.IsFinite(now)&&now<deadline&&deadline>0;}}
    public PalicoCaptureStats Stats(){lock(gate){return new(callbacks,targets,candidates.Order(StringComparer.OrdinalIgnoreCase).ToArray());}}
    public void Observe(string? path, ulong resource, double now, IReadOnlyGmdMemory memory)
    {
        lock(gate)
        {
            if(!double.IsFinite(now)||now>=deadline||deadline<=0)return;
            callbacks++;
            // Only bounded resource paths, never dialogue text or arbitrary resource names.
            var normalized=path?.Replace('\\','/');
            if(normalized is {Length:<=100} && normalized.StartsWith("common/text/ot_",StringComparison.OrdinalIgnoreCase)
                && normalized.All(c=>char.IsAsciiLetterOrDigit(c)||c is '/' or '_' or '.') && candidates.Count<16)
                candidates.Add(new string(normalized.AsSpan()));
            var language=PalicoResourceProbe.LanguageForPath(path);
            if(language==null)return;
            targets++;
            if(seen.Count>=4||!seen.Add((resource,language)))return;
            try
            {
                var result=PalicoResourceProbe.Inspect(memory,resource,language);
                results.Enqueue(result);
                if(result is {Status:"count_mismatch",Count:0} && Identity(memory,resource,language,out var vtable))
                    pending.Add(new(resource,language,vtable,Math.Min(deadline,now+10),now+0.25));
            }
            catch{results.Enqueue(new("inspection_failed",0,0,0,0,language));}
        }
    }
    public PalicoResourceResult[] Drain(){lock(gate){var copy=results.ToArray();results.Clear();return copy;}}

    private static bool Identity(IReadOnlyGmdMemory memory,ulong resource,string language,out ulong vtable)
    {
        vtable=0;
        if(!LocalProcessMemory.IsAddress(resource,0x100)||!memory.Read(resource,8,out var first)||first.Length!=8)return false;
        vtable=BitConverter.ToUInt64(first);
        if(!LocalProcessMemory.IsAddress(vtable,8))return false;
        var expected="common/text/ot_auto_"+language;
        if(!memory.Read(resource+0xC,expected.Length+1,out var path)||path.Length!=expected.Length+1||path[^1]!=0)return false;
        return path.AsSpan(0,path.Length-1).IndexOf((byte)0)<0 &&
            System.Text.Encoding.ASCII.GetString(path,0,path.Length-1).Replace('\\','/').Equals(expected,StringComparison.OrdinalIgnoreCase);
    }

    // OS-only reads: no weak Resource wrapper is retained or dereferenced.
    // Recheck inline path and vtable each time; changed/released identity stops sampling.
    public void Tick(double now,IReadOnlyGmdMemory memory)
    {
        lock(gate)
        {
            if(!double.IsFinite(now))return;
            for(int i=pending.Count-1;i>=0;i--)
            {
                var item=pending[i];
                if(now>=item.Expires||deadline<=0||now>=deadline)
                {results.Enqueue(new("not_ready_timeout",0,0,0,0,item.Language,"deferred"));pending.RemoveAt(i);continue;}
                if(now<item.Next)continue;
                try
                {
                    if(!Identity(memory,item.Resource,item.Language,out var vtable)||vtable!=item.Vtable)
                    {results.Enqueue(new("identity_changed_or_unreadable",0,0,0,0,item.Language,"deferred"));pending.RemoveAt(i);continue;}
                    var result=PalicoResourceProbe.Inspect(memory,item.Resource,item.Language);
                    if(result is {Status:"count_mismatch",Count:0})pending[i]=item with {Next=now+0.25};
                    else {results.Enqueue(result with {Phase="deferred"});pending.RemoveAt(i);}
                }
                catch{results.Enqueue(new("inspection_failed",0,0,0,0,item.Language,"deferred"));pending.RemoveAt(i);}
            }
        }
    }
}
