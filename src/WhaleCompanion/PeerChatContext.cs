using System.Text;
using System.Text.Json;

namespace WhaleCompanion;

public sealed record PeerChatLine(string DisplayName,string Text,string Fingerprint,double Time);

// Narrow experimental decoder based on three user-marked hub packets. Neither
// the display name nor opaque header/trailer is treated as verified identity.
public static class PeerChatPacket
{
    public const uint Id=0x104E34CF;
    private static readonly UTF8Encoding Utf8=new(false,true);
    public static PeerChatLine? Parse(uint id,int type,int session,ReadOnlySpan<byte> bytes,double now) => Parse(id,type,session,bytes,now,out _);
    public static PeerChatLine? Parse(uint id,int type,int session,ReadOnlySpan<byte> bytes,double now,out string reason)
    {
        reason="ok";
        if(id!=Id){reason="packet_id";return null;}
        if(type!=1){reason="packet_type";return null;}
        if(session is not (0 or 1)){reason="session";return null;}
        if(bytes.Length!=180){reason="payload_size";return null;}
        if(!double.IsFinite(now)){reason="time";return null;}
        if(!bytes[..4].SequenceEqual(new byte[]{0,0,8,1})){reason="header";return null;}
        try
        {
            string Field(ReadOnlySpan<byte> field)
            {
                var end=field.IndexOf((byte)0);
                if(end<=0)return "";
                var text=Utf8.GetString(field[..end]);
                return text.Any(char.IsControl)?"":text.Trim();
            }
            var name=Field(bytes.Slice(12,32));
            // 128 bytes is a conservative candidate window; do not interpret the
            // final 8 opaque bytes as text, SteamID or account authority.
            var text=Field(bytes.Slice(44,128));
            if(name.Length==0){reason="name_field";return null;}
            if(text.Length==0){reason="text_field";return null;}
            var fingerprint=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            return new(name,text,fingerprint,now);
        }
        catch(DecoderFallbackException){reason="utf8";return null;}
    }
}

public sealed class PeerChatContext
{
    private readonly object gate=new();
    private readonly Queue<PeerChatLine> lines=new();
    public int Count { get {lock(gate)return lines.Count;} }
    public void Clear(){lock(gate)lines.Clear();}
    public bool Observe(PeerChatLine line,double now)
    {
        lock(gate)
        {
            Prune(now);
            if(!double.IsFinite(now)||!double.IsFinite(line.Time)||now<line.Time||now-line.Time>90)return false;
            if(lines.Any(x=>x.Fingerprint==line.Fingerprint&&now-x.Time<2))return false;
            while(lines.Count>=8)lines.Dequeue();
            lines.Enqueue(line);return true;
        }
    }
    private void Prune(double now){while(lines.TryPeek(out var first)&&(!double.IsFinite(now)||now<first.Time||now-first.Time>90))lines.Dequeue();}
    public string Snapshot(double now)
    {
        lock(gate)
        {
            Prune(now);
            var selected=new List<object>();string result="";
            foreach(var line in lines.Reverse())
            {
                selected.Insert(0,new{source="remote_unverified",display_name=line.DisplayName,text=line.Text});
                var candidate=JsonSerializer.Serialize(selected,new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
                if(candidate.Length>1200)break;
                result=candidate;
            }
            return result;
        }
    }
}
