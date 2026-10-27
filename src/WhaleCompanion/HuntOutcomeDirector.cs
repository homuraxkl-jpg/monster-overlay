namespace WhaleCompanion;
public sealed record HuntOutcome(string Kind,double Time);
// Native callbacks offer immutable signals only. Presentation is drained on the game update thread.
public sealed class HuntOutcomeDirector
{
    readonly object gate=new();readonly HashSet<ulong> dead=new();
    HuntOutcome? pending;double readyAt;bool complete;
    // World/Iceborne monster type IDs from SPL's MonsterType enum; exclude small monsters, props and training objects.
    public static bool IsLarge(int type)=>type is 0 or 1 or 4 or 7 or >=0x09 and <=0x27 or 0x33 or >=0x3D and <=0x51 or >=0x57 and <=0x61 or >=0x63 and <=0x65;
    public void Reset(){lock(gate){dead.Clear();pending=null;readyAt=0;complete=false;}}
    public void MonsterDied(ulong identity,int type,double now)
    {
        if(identity==0||!IsLarge(type)||!double.IsFinite(now))return;
        lock(gate){if(complete||dead.Count>=32||!dead.Add(identity))return;pending=new("monster_defeated",now);readyAt=now+.75;}
    }
    public void QuestCompleted(double now)
    {
        if(!double.IsFinite(now))return;
        lock(gate){if(complete)return;complete=true;pending=new("quest_complete",now);readyAt=now;}
    }
    public HuntOutcome? Take(double now,bool enabled)
    {
        lock(gate)
        {
            if(!enabled||!double.IsFinite(now)){pending=null;return null;}
            if(pending is not {} next)return null;
            if(now<next.Time||now-next.Time>3){pending=null;return null;}
            if(now<readyAt)return null;pending=null;return next;
        }
    }
}
