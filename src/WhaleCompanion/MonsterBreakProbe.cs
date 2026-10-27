using System.Text.Json;
namespace WhaleCompanion;

public sealed record MonsterBreakDefinition(int MonsterType,int Slot,int Part,int[] Thresholds);
public sealed record MonsterBreakSample(ulong Monster,ulong Table,int MonsterType,int Part,int Counter,int[] Thresholds,double Time);
public sealed record MonsterBreakChange(ulong Monster,int Part,int Previous,int Current,int Threshold,double Time);
// World MHWMonster.GetMonsterParts + MHWMonsterPartStructure; schema thresholds from HunterPie.
// A counter increment is often flinch, NOT necessarily a break. Severed parts are not handled here.
public static class MonsterBreakProbe
{
    static readonly Lazy<MonsterBreakDefinition[]> Definitions=new(()=>
    {
        using var stream=typeof(MonsterBreakProbe).Assembly.GetManifestResourceStream("WhaleCompanion.monster-break-thresholds.json")??throw new InvalidDataException("Missing monster break metadata");
        return JsonSerializer.Deserialize<MonsterBreakDefinition[]>(stream)??[];
    });
    public static int DefinitionCount=>Definitions.Value.Length;
    public static object Inspect(IReadOnlyLocalMemory m,ulong monster)
    {
        var rows=new List<object>();
        if(m.Pointer(monster+0x1D058,out var table)&&LocalProcessMemory.IsAddress(table,0x4000))
            for(uint slot=0;slot<12;slot++)
            {
                ulong at=table+0x40+slot*0x1F8;
                if(m.Int32(at+0x6C,out var index)&&m.Int32(at+0x18,out var counter)&&m.Int32(at+0xC,out var maximum)&&m.Int32(at+0x10,out var hp))
                {
                    float max=BitConverter.Int32BitsToSingle(maximum),current=BitConverter.Int32BitsToSingle(hp);
                    if(float.IsFinite(max)&&float.IsFinite(current))rows.Add(new{slot,index,counter,maximum=max,current});
                }
            }
        var tenderize=new List<object>();
        for(uint slot=0;slot<10;slot++)
        {
            ulong at=monster+0x1C458+slot*64;
            if(m.Int32(at+48,out var part)&&m.Int32(at+52,out var counter)&&m.Int32(at+8,out var duration)&&m.Int32(at+12,out var maximum))
            {
                float current=BitConverter.Int32BitsToSingle(duration),max=BitConverter.Int32BitsToSingle(maximum);
                if(float.IsFinite(current)&&float.IsFinite(max))tenderize.Add(new{slot,part,counter,current,maximum=max});
            }
        }
        return new{part_rows=rows,tenderize_rows=tenderize};
    }
    public static MonsterBreakSample[] Read(IReadOnlyLocalMemory m,ulong monster,string revision,double now)
    {
        if(revision!="421810"||!double.IsFinite(now)||!LocalProcessMemory.IsAddress(monster,0x1D060)||!m.Int32(monster+0x12280,out var type)||!m.Pointer(monster+0x1D058,out var table)||!LocalProcessMemory.IsAddress(table,0x4000))return [];
        var result=new List<MonsterBreakSample>();
        foreach(var definition in Definitions.Value.Where(d=>d.MonsterType==type&&d.Slot is >=0 and <32))
        {
            ulong at=table+0x40+(uint)definition.Slot*0x1F8;
            if(!m.Int32(at+0x6C,out var index)||index is <0 or >255||!m.Int32(at+0x18,out var counter)||counter is <0 or >10000||!m.Int32(at+0xC,out var maxBits)||!m.Int32(at+0x10,out var hpBits))continue;
            float maximum=BitConverter.Int32BitsToSingle(maxBits),hp=BitConverter.Int32BitsToSingle(hpBits);
            if(!float.IsFinite(maximum)||!float.IsFinite(hp)||maximum<=0||maximum>10000000||hp<0||hp>maximum+.5)continue;
            if(!m.Int32(at+0x6C,out var after)||after!=index)continue;
            result.Add(new(monster,table,type,definition.Part,counter,definition.Thresholds,now));
        }
        if(!m.Pointer(monster+0x1D058,out var afterTable)||afterTable!=table||!m.Int32(monster+0x12280,out var afterType)||afterType!=type)return [];
        return result.ToArray();
    }
}
public sealed class MonsterBreakTracker
{
    readonly Dictionary<(ulong,int),MonsterBreakSample> previous=new();
    readonly Dictionary<(ulong,int),(MonsterBreakSample Before,MonsterBreakSample Candidate,int Threshold)> pending=new();
    public void Reset(){previous.Clear();pending.Clear();}
    public MonsterBreakChange[] Observe(IEnumerable<MonsterBreakSample> samples,double now)
    {
        if(!double.IsFinite(now)){Reset();return [];}
        var next=samples.Where(v=>v.Time==now&&v.Counter is >=0 and <=10000).GroupBy(v=>(v.Monster,v.Part)).Where(g=>g.Count()==1).Select(g=>g.Single()).ToArray();
        var changes=new List<MonsterBreakChange>();var waiting=new Dictionary<(ulong,int),(MonsterBreakSample,MonsterBreakSample,int)>();
        foreach(var current in next)
        {
            var key=(current.Monster,current.Part);
            if(!previous.TryGetValue(key,out var before)||current.Table!=before.Table||current.MonsterType!=before.MonsterType||now<=before.Time||now-before.Time>.75||current.Counter<before.Counter)continue;
            if(pending.TryGetValue(key,out var candidate)&&current.Counter>=candidate.Candidate.Counter)
                changes.Add(new(current.Monster,current.Part,candidate.Before.Counter,current.Counter,candidate.Threshold,now));
            else
            {
                int threshold=current.Thresholds.Where(t=>t>before.Counter&&t<=current.Counter).DefaultIfEmpty(-1).Min();
                if(threshold>=0)waiting[key]=(before,current,threshold);
            }
        }
        previous.Clear();foreach(var current in next)previous[(current.Monster,current.Part)]=current;
        pending.Clear();foreach(var pair in waiting)pending[pair.Key]=pair.Value;
        return changes.ToArray();
    }
}
