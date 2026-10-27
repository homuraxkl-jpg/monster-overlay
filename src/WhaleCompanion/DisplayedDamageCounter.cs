namespace WhaleCompanion;

// Fallback mirrors the local damage-display route used by HunterPie World.
// This is explicitly a candidate source, not verified per-hit ownership.
public sealed class DisplayedDamageCounter
{
    int epoch=-1,total;ulong owner;double since;bool healthy;
    public void Reset(int scene,ulong player,double now){epoch=scene;owner=player;since=now;total=0;healthy=player!=0&&double.IsFinite(now);}
    public bool Matches(int scene,ulong player)=>epoch==scene&&owner==player;
    public void Gap(){healthy=false;}
    public void Add(DamageObservation observation)
    {
        if(!healthy||observation.Epoch!=epoch||!double.IsFinite(observation.Time)||observation.Time<since||observation.Target==0||observation.Amount is <=0 or >1000000)return;
        if(total>1000000000-observation.Amount){healthy=false;return;}total+=observation.Amount;
    }
    public PlayerDamageCounterSample? Sample(double now)=>healthy&&double.IsFinite(now)&&now>=since
        ?new(0,"local display candidate",owner,owner,total,now,"display_local_candidate"):null;
}
