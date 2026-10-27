using System.Text;

namespace WhaleCompanion;

public sealed record TeamProbeSample(string Route, uint PacketId, int Type, int Session, int Bytes,
    string Marker, string Encoding, int Offset, string PrefixHex, string TrailerHex="");

// Observe only specifically marked test messages. Never consume game buffers or
// infer authority from display names, marker wording or the session enum.
public sealed class TeamChatProbe
{
    private readonly object gate=new();
    private readonly Queue<TeamProbeSample> pending=new();
    private double until;
    private long packets, matches;
    public static readonly string[] Markers=["WHALE-OWNER-A1","WHALE-PEER-B1","WHALE-PEER-B2"];
    private static readonly (string Marker,string Encoding,byte[] Bytes)[] Patterns=Markers
        .SelectMany(m=>new[]{(m,"UTF8",Encoding.UTF8.GetBytes(m)),(m,"UTF16LE",Encoding.Unicode.GetBytes(m))}).ToArray();
    public void Start(double now) { lock(gate){until=double.IsFinite(now)?now+180:0;packets=matches=0;pending.Clear();} }
    public void Stop() { lock(gate){until=0;pending.Clear();} }
    public bool Active(double now) { lock(gate)return double.IsFinite(now)&&now>=0&&now<until; }
    public string Summary(double now) { lock(gate)return $"Probe remaining {Math.Max(0,until-now):0}s | received {packets} | marked {matches} (identity unverified)"; }
    public bool Local(string text,double now)
    {
        lock(gate)
        {
            if(!Active(now)||!Markers.Contains(text.Trim(),StringComparer.Ordinal))return false;
            pending.Enqueue(new("local_outbound",0,0,0,Encoding.UTF8.GetByteCount(text),new string(text.AsSpan()).Trim(),"managed",0,""));
            while(pending.Count>32)pending.Dequeue();return true;
        }
    }
    public void Receive(uint id,int type,int session,ReadOnlySpan<byte> bytes,double now)
    {
        lock(gate)
        {
            if(!Active(now))return;
            packets++;
            if(bytes.Length is 0 or >4096 || pending.Count>=32)return;
            foreach(var pattern in Patterns)
            {
                var offset=bytes.IndexOf(pattern.Bytes);
                if(offset<0)continue;
                // Fixed bounded prefix for offline field analysis of marked test
                // packets only. No complete packet or ordinary chat is retained.
                var start=Math.Max(0,offset-64);
                pending.Enqueue(new("received_unverified",id,type,session,bytes.Length,pattern.Marker,
                    pattern.Encoding,offset,Convert.ToHexString(bytes.Slice(start,offset-start)),bytes.Length==180?Convert.ToHexString(bytes[^8..]):""));
                matches++;break;
            }
        }
    }
    public TeamProbeSample[] Drain() { lock(gate){var result=pending.ToArray();pending.Clear();return result;} }
}

public enum ChatAuthority { Owner, PeerContext, Unknown }
public static class ChatAuthorityPolicy
{
    // A local callback is already attributed by the game path. Remote packets
    // need separately verified identity; session numbers and text grant nothing.
    public static ChatAuthority Classify(bool localOutbound,bool verifiedRemoteIdentity) =>
        localOutbound ? ChatAuthority.Owner : verifiedRemoteIdentity ? ChatAuthority.PeerContext : ChatAuthority.Unknown;
    public static bool MayRequestReply(ChatAuthority authority) => authority==ChatAuthority.Owner;
}


