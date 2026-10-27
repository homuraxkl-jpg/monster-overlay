namespace WhaleCompanion;

public sealed record TenderizeSample(ulong Monster,int Part,double Duration,double Maximum,double Time,int Slot=-1);
public sealed record TenderizeChange(ulong Monster,int Part,double Previous,double Current,double Time);

// HunterPie MHWMonster.GetMonsterPartTenderizes / MHWTenderizeInfoStructure (Apache-2.0).
// Read-only result observation. Neither the soft-hit flag nor this timer identifies its author.
public static class TenderizeProbe
{
    public static TenderizeSample[] Read(IReadOnlyLocalMemory m,ulong monster,string revision,double now)
    {
        if(revision!="421810"||!double.IsFinite(now)||!LocalProcessMemory.IsAddress(monster,0x1C6D8))return [];
        var result=new List<TenderizeSample>();
        for(uint i=0;i<10;i++)
        {
            ulong at=monster+0x1C458+i*64;
            if(!m.Int32(at+48,out var part)||part is <-1 or >255||!m.Int32(at+8,out var bits)||!m.Int32(at+12,out var maxBits))continue;
            float duration=BitConverter.Int32BitsToSingle(bits),maximum=BitConverter.Int32BitsToSingle(maxBits);
            if(!float.IsFinite(duration)||!float.IsFinite(maximum)||maximum is <0 or >3600||duration<0||duration>3600)continue;
            if(!m.Int32(at+48,out var after)||after!=part)continue;
            result.Add(new(monster,part,duration,maximum,now,(int)i));
        }
        return result.ToArray();
    }
}

// Current 421810 live evidence: duration is elapsed, Part=-1 means inactive.
public sealed class TenderizeTracker
{
    readonly Dictionary<(ulong,int),TenderizeSample> previous=new();
    readonly Dictionary<(ulong,int),(TenderizeSample Before,TenderizeSample Candidate)> pending=new();
    public void Reset(){previous.Clear();pending.Clear();}
    static (ulong,int) Key(TenderizeSample s)=>(s.Monster,s.Slot>=0?s.Slot:s.Part);
    public TenderizeChange[] Observe(IEnumerable<TenderizeSample> samples,double now)
    {
        if(!double.IsFinite(now)){Reset();return [];}
        var next=samples.Where(v=>v.Monster!=0&&v.Part is >=-1 and <=255&&v.Slot is >=-1 and <10&&double.IsFinite(v.Time)&&v.Time==now&&double.IsFinite(v.Duration)&&double.IsFinite(v.Maximum)&&v.Duration is >=0 and <=3600&&v.Maximum is >=0 and <=3600)
            .GroupBy(Key).Where(g=>g.Count()==1).Select(g=>g.Single()).ToArray();
        var changes=new List<TenderizeChange>();var nextPending=new Dictionary<(ulong,int),(TenderizeSample Before,TenderizeSample Candidate)>();
        foreach(var value in next)
        {
            var key=Key(value);
            if(!previous.TryGetValue(key,out var old)||now<=old.Time||now-old.Time>.75||value.Part<0)continue;
            if(pending.TryGetValue(key,out var waiting)&&value.Part==waiting.Candidate.Part&&value.Duration>=waiting.Candidate.Duration-.5&&value.Duration-waiting.Candidate.Duration<=2)
                changes.Add(new(value.Monster,value.Part,waiting.Before.Duration,value.Duration,now));
            else if(old.Part<0||value.Part!=old.Part||value.Duration<old.Duration-1)nextPending[key]=(old,value);
        }
        previous.Clear();foreach(var value in next)previous[Key(value)]=value;
        pending.Clear();foreach(var pair in nextPending)pending[pair.Key]=pair.Value;
        return changes.ToArray();
    }
}
