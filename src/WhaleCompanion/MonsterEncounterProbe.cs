namespace WhaleCompanion;

public sealed record MonsterAilmentSample(int Id,bool Active,float Duration,float Maximum,int Counter);
public sealed record MonsterEncounterSample(ulong Monster,int Type,int ActionSet,int ActionId,string ActionType,float? Health,float? Maximum,MonsterAilmentSample[] Ailments,double Time);
// HunterPie World GetMonsterAilments and MHWMonsterAilmentStructure; bounded read-only adaptation.
public static class MonsterEncounterProbe
{
    public static MonsterEncounterSample? Read(IReadOnlyLocalMemory m,ulong monster,ulong module,string revision,double now)
    {
        if(revision!="421810"||!double.IsFinite(now)||!LocalProcessMemory.IsAddress(monster,0x1BD40)||!m.Int32(monster+0x12280,out var type)||!m.Int32(monster+0x6274,out var set)||!m.Int32(monster+0x6278,out var action)||set is <0 or >3||action is <0 or >8191)return null;
        var ailments=new List<MonsterAilmentSample>();
        for(uint i=0;i<32;i++)
        {
            ulong slot=monster+0x1BC40+i*8;
            if(!m.Pointer(slot,out var pointer)||pointer<=1)break;
            if(!LocalProcessMemory.IsAddress(pointer,0x1D0))continue;
            ulong at=pointer+0x148;
            if(!m.Int32(at+0x10,out var id)||id is <0 or >24||!m.Int32(at+8,out var active)||active is <0 or >1||!m.Int32(at+0x14,out var maxBits)||!m.Int32(at+0x70,out var timerBits)||!m.Int32(at+0x78,out var counter)||counter is <0 or >10000)continue;
            float maximum=BitConverter.Int32BitsToSingle(maxBits),duration=BitConverter.Int32BitsToSingle(timerBits);
            if(!float.IsFinite(maximum)||!float.IsFinite(duration)||maximum<0||maximum>3600||duration<0||duration>maximum+.5||!m.Pointer(slot,out var after)||after!=pointer||!m.Int32(at+0x10,out var afterId)||afterId!=id)continue;
            ailments.Add(new(id,active!=0,duration,maximum,counter));
        }
        float? hp=null,maxHp=null;
        if(m.Pointer(monster+0x7670,out var health)&&LocalProcessMemory.IsAddress(health,0x68)&&m.Int32(health+0x60,out var hmax)&&m.Int32(health+0x64,out var hcur))
        {
            float maximum=BitConverter.Int32BitsToSingle(hmax),current=BitConverter.Int32BitsToSingle(hcur);
            if(float.IsFinite(maximum)&&float.IsFinite(current)&&maximum>0&&maximum<=10000000&&current>=0&&current<=maximum&&m.Pointer(monster+0x7670,out var after)&&after==health){hp=current;maxHp=maximum;}
        }
        string name=ActionType(m,monster,module,set,action);
        if(!m.Int32(monster+0x12280,out var afterType)||afterType!=type||!m.Int32(monster+0x6274,out var afterSet)||afterSet!=set||!m.Int32(monster+0x6278,out var afterAction)||afterAction!=action)return null;
        return new(monster,type,set,action,name,hp,maxHp,ailments.GroupBy(a=>a.Id).Where(g=>g.Count()==1).Select(g=>g.Single()).ToArray(),now);
    }
    static string ActionType(IReadOnlyLocalMemory m,ulong monster,ulong module,int set,int action)
    {
        bool Image(ulong p)=>module>=0x10000&&p>=module&&p-module<0x6000000;
        ulong list=monster+0x61C8+0x68+(uint)set*0x10;
        if(!m.Pointer(list,out var array)||!m.Int32(list+8,out var count)||count is <1 or >8192||action>=count||!LocalProcessMemory.IsAddress(array,(uint)count*8)||!m.Pointer(array+(uint)action*8,out var instance)||!m.Pointer(instance,out var table)||!Image(table)||!m.Pointer(table+32,out var getter)||!Image(getter)||!m.Pointer(getter,out var code))return "unknown";
        var b=BitConverter.GetBytes(code);
        if(b[0]!=0x48||b[1]!=0x8D||b[2]!=5||b[7]!=0xC3)return "unknown";
        long dti=(long)getter+7+BitConverter.ToInt32(b,3);
        if(dti<0||!Image((ulong)dti)||!m.Pointer((ulong)dti+8,out var text)||!Image(text))return "unknown";
        var bytes=new List<byte>();
        for(uint i=0;i<96;i+=8)
        {
            if(!m.Pointer(text+i,out var word))return "unknown";
            foreach(var c in BitConverter.GetBytes(word))
            {
                if(c==0)return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
                if(c<32||c>126)return "unknown";bytes.Add(c);
            }
        }
        return "unknown";
    }
}
public sealed class MonsterEncounterTrace
{
    readonly Dictionary<ulong,MonsterEncounterSample> previous=new();
    public void Reset()=>previous.Clear();
    public object? Observe(MonsterEncounterSample sample)
    {
        if(previous.Count>=32&&!previous.ContainsKey(sample.Monster))previous.Clear();
        bool known=previous.TryGetValue(sample.Monster,out var old);previous[sample.Monster]=sample;
        bool action=!known||old!.ActionSet!=sample.ActionSet||old.ActionId!=sample.ActionId;
        bool health=known&&old!.Health!=sample.Health;
        var changes=sample.Ailments.Where(a=>!known||!old!.Ailments.Any(b=>b.Id==a.Id&&b.Active==a.Active&&b.Counter==a.Counter&&(b.Duration>0)==(a.Duration>0))).ToArray();
        if(!action&&!health&&changes.Length==0)return null;
        return new{sample.Monster,sample.Type,sample.Time,initial=!known,action=new{sample.ActionSet,sample.ActionId,sample.ActionType},sample.Health,sample.Maximum,ailment_changes=changes,owner="unknown",wallbang="unconfirmed",ai_response=false};
    }
}

public sealed record MonsterStateResult(string Kind,double Time);
public sealed class MonsterStateDirector
{
    public event Action<string,double>? Observed;
    readonly Dictionary<ulong,MonsterEncounterSample> last=new();
    readonly Dictionary<(ulong,int),double> pending=new();
    readonly Queue<MonsterStateResult> ready=new();
    readonly Dictionary<string,double> cooldown=new();
    readonly Dictionary<ulong,double> mounting=new();
    static readonly Dictionary<int,string> Labels=new(){[2]="paralysis",[3]="sleep",[7]="stun",[14]="shock_trap",[15]="pitfall_trap"};
    public void Reset(){last.Clear();pending.Clear();ready.Clear();cooldown.Clear();mounting.Clear();}
    public void Add(string kind,double now)
    {
        Observed?.Invoke(kind,now);
        if(cooldown.TryGetValue(kind,out var until)&&now<until)return;
        cooldown[kind]=now+ResponseTuning.Cooldown("monster",12);if(ready.Count>=8)ready.Dequeue();ready.Enqueue(new(kind,now));
    }
    public void Observe(MonsterEncounterSample sample)
    {
        if(!double.IsFinite(sample.Time))return;
        if(last.Count>=32&&!last.ContainsKey(sample.Monster)){last.Clear();pending.Clear();mounting.Clear();}
        bool known=last.TryGetValue(sample.Monster,out var old);last[sample.Monster]=sample;
        if(!known||sample.Time<=old!.Time||sample.Time-old.Time>.75)
        {foreach(var key in pending.Keys.Where(k=>k.Item1==sample.Monster).ToArray())pending.Remove(key);return;}
        foreach(var (id,kind) in Labels)
        {
            var a=sample.Ailments.SingleOrDefault(a=>a.Id==id);var b=old.Ailments.SingleOrDefault(a=>a.Id==id);var key=(sample.Monster,id);
            if(a==null||b==null||!a.Active){pending.Remove(key);continue;}
            if(!b.Active){pending[key]=sample.Time;continue;}
            if(pending.Remove(key,out var started)&&sample.Time-started<=.75)Add(kind,sample.Time);
        }
        var mount=sample.Ailments.SingleOrDefault(a=>a.Id==5);var previousMount=old.Ailments.SingleOrDefault(a=>a.Id==5);
        if(mount!=null&&previousMount!=null&&mount.Counter>previousMount.Counter&&sample.ActionType.EndsWith("::DamageRideAccept",StringComparison.Ordinal))
        {
            mounting[sample.Monster]=sample.Time;Add("mount_started",sample.Time);
        }
        var down=sample.Ailments.SingleOrDefault(a=>a.Id==11);var previousDown=old.Ailments.SingleOrDefault(a=>a.Id==11);
        if(sample.ActionType.EndsWith("::DamageRideDown",StringComparison.Ordinal)&&down!=null&&previousDown!=null&&down.Counter>previousDown.Counter&&mounting.TryGetValue(sample.Monster,out var mountTime)&&sample.Time-mountTime is >=0 and <=90)
        {mounting.Remove(sample.Monster);Add("mount_knockdown",sample.Time);}
        if(sample.ActionType.EndsWith("::DamageClawGrabHitWall",StringComparison.Ordinal)&&old.ActionType.EndsWith("::DamageClawGrabCarry",StringComparison.Ordinal))Add("wallbang",sample.Time);
    }
    public MonsterStateResult? Take(double now,bool canSpeak)
    {
        while(ready.TryPeek(out var head)&&(now<head.Time||now-head.Time>10))ready.Dequeue();
        return canSpeak&&ready.TryDequeue(out var value)?value:null;
    }
    public static string Text(string kind)=>kind switch
    {"mount_knockdown"=>"骑乘倒地啦，机会来了！","sleep"=>"睡眠生效啦！","mount_started"=>"骑乘开始啦！","tenderize"=>"软化了，机会来了！","part_break"=>"部位破坏啦！","paralysis"=>"麻痹生效啦！","stun"=>"击晕啦，机会来了！","shock_trap"=>"麻痹陷阱生效啦！","pitfall_trap"=>"落穴生效啦！","wallbang"=>"撞墙成功啦！",_=>"状态变化啦。"};
}
