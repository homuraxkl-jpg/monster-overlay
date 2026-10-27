using System.Security.Cryptography;

namespace WhaleCompanion;

public sealed record BattleMessageSample(double Time,string Language,int Index,int Bytes);
public sealed record BattleCaptureStats(bool Active,double Remaining,long Calls,long ReadFailures,long Unmatched,long Dropped);

// No message bodies retained. Exact original UTF8 fingerprints only; never infer
// actor identity or action success from a matching shared resource string.
public sealed class PalicoBattleCapture
{
    private readonly object gate=new();
    private readonly Queue<BattleMessageSample> matches=new();
    private double deadline;
    private long calls,failures,unmatched,dropped;
    public void Start(double now){lock(gate){deadline=double.IsFinite(now)?now+3600:0;calls=failures=unmatched=dropped=0;matches.Clear();}}
    public void Stop(){lock(gate){deadline=0;}}
    public BattleCaptureStats Stats(double now){lock(gate){var active=double.IsFinite(now)&&deadline>0&&now<deadline;return new(active,active?deadline-now:0,calls,failures,unmatched,dropped);}}
    public void Observe(ulong text,double now,IReadOnlyGmdMemory memory)
    {
        lock(gate)
        {
            if(!double.IsFinite(now)||deadline<=0||now>=deadline)return;
            calls++;
            var buffer=new List<byte>(128);
            for(int offset=0;offset<512;)
            {
                int size=Math.Min(16,512-offset);
                if(!LocalProcessMemory.IsAddress(text,(uint)(offset+size))){failures++;return;}
                if(!memory.Read(text+(uint)offset,size,out var bytes)||bytes.Length!=size)
                {
                    size=1;
                    if(!memory.Read(text+(uint)offset,1,out bytes)||bytes.Length!=1){failures++;return;}
                }
                var end=Array.IndexOf(bytes,(byte)0);
                buffer.AddRange(end>=0?bytes.Take(end+1):bytes);
                if(end>=0)
                {
                    var hash=Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
                    foreach(var (language,items) in new[]{("chS",PalicoResourceProbe.Fingerprints),("chT",PalicoResourceProbe.TraditionalFingerprints)})
                    {
                        var match=items.FirstOrDefault(x=>x.Length==buffer.Count&&x.Sha256==hash);
                        if(match==null)continue;
                        if(matches.Count>=32){dropped++;return;}
                        matches.Enqueue(new(now,language,match.Index,buffer.Count));return;
                    }
                    unmatched++;return;
                }
                offset+=size;
            }
            failures++;
        }
    }
    public BattleMessageSample[] Drain(){lock(gate){var result=matches.ToArray();matches.Clear();return result;}}
    public static void Forward(Action observe,Action original)
    {
        try{observe();}catch{ /* Diagnostics must not suppress normal display. */ }
        original();
    }
}
