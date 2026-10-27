using System.Text;

namespace WhaleCompanion;

public sealed record SharedChatLine(long Id,string Text,double Time,bool Transient=true);
public sealed class SharedChat
{
    public const string Prefix="【大肥鱼】";
    private readonly Queue<SharedChatLine> queue=new();
    private double nextAllowed;
    private long sequence;
    private readonly object gate=new();
    public int Count{get{lock(gate)return queue.Count;}}
    public void Clear(){lock(gate){queue.Clear();nextAllowed=0;}}
    public static bool IsCompanion(string text)=>text.TrimStart().StartsWith(Prefix,StringComparison.Ordinal);
    public static string Format(string text)
    {
        var body=new StringBuilder();
        foreach(var rune in text.EnumerateRunes())
        {
            if(Rune.IsControl(rune)||Rune.IsWhiteSpace(rune)){if(body.Length>0&&body[^1]!=' ')body.Append(' ');continue;}
            body.Append(rune.Value switch {'<'=>"＜",'>'=>"＞",_=>rune.ToString()});
        }
        var clean=body.ToString().Trim();if(clean.StartsWith(Prefix,StringComparison.Ordinal))clean=clean[Prefix.Length..].TrimStart();if(clean.Length==0)return "";
        var output=new StringBuilder(Prefix);var bytes=Encoding.UTF8.GetByteCount(Prefix);
        var truncated=Encoding.UTF8.GetByteCount(Prefix+clean)>127;
        foreach(var rune in clean.EnumerateRunes())
        {
            if(bytes+rune.Utf8SequenceLength+(truncated?3:0)>127)break;
            output.Append(rune);bytes+=rune.Utf8SequenceLength;
        }
        if(truncated)output.Append('…');
        return output.ToString();
    }
    public void Enqueue(string text,double now,bool transient=true)
    {
        lock(gate){
        if(!double.IsFinite(now))return;
        var formatted=Format(text);if(formatted.Length==0)return;
        if(queue.TryPeek(out var prior)&&!prior.Transient&&transient&&now>=prior.Time&&now-prior.Time<=ResponseTuning.Thresholds.ConversationDeliveryMaxAgeSeconds)return;
        queue.Clear();queue.Enqueue(new(++sequence,formatted,now,transient));

        }
    }
    public SharedChatLine? Peek(double now)
    {
        lock(gate){
        while(queue.TryPeek(out var item)&&(now<item.Time||now-item.Time>(item.Transient?ResponseTuning.Thresholds.EventFreshnessSeconds:ResponseTuning.Thresholds.ConversationDeliveryMaxAgeSeconds)))queue.Dequeue();
        return now>=nextAllowed&&queue.TryPeek(out var next)?next:null;

        }
    }
    public void Discard(long? id=null){lock(gate){if(queue.TryPeek(out var head)&&(id==null||head.Id==id))queue.Dequeue();}}
    public void Submitted(double now,long? id=null){lock(gate){if(queue.TryPeek(out var head)&&(id==null||head.Id==id))queue.Dequeue();nextAllowed=now+ResponseTuning.Thresholds.SharedOutputIntervalSeconds;}}
}
