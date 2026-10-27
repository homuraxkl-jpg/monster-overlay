using System.Text;

namespace WhaleCompanion;

public sealed record PlayerDamageCounterSample(int Slot,string Name,ulong Member,ulong Counter,int Total,double Time,string Source="party");
public static class PlayerDamageCounter
{
    // World 421810 map; dereference THEN add each offset (HunterPie/monster-overlay).
    static ulong Chain(IReadOnlyLocalMemory m,ulong at,params uint[] offsets)
    {
        foreach(var offset in offsets)
        {if(!LocalProcessMemory.IsAddress(at,8)||!m.Pointer(at,out var next)||!LocalProcessMemory.IsAddress(next,offset+8))return 0;at=next+offset;}
        return at;
    }
    public static bool TryPartySize(IReadOnlyLocalMemory m,ulong module,string revision,out int size)
    {
        size=-1;if(revision!="421810"||!LocalProcessMemory.IsAddress(module,0x51C46C0))return false;
        var address=Chain(m,module+0x51C46B8,0x258,0x10,0x6574);
        return address!=0&&m.Int32(address,out size)&&size is >=0 and <=4;
    }
    static string Name(IReadOnlyLocalMemory m,ulong at)
    {
        var bytes=new byte[32];
        for(int i=0;i<8;i++){if(!m.Int32(at+(uint)(i*4),out var value))return "";System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i*4,4),value);}
        int end=Array.IndexOf(bytes,(byte)0);if(end<=0)return "";
        try{var value=new UTF8Encoding(false,true).GetString(bytes,0,end);return value.Any(char.IsControl)?"":value;}catch{return "";}
    }
    public static bool TryRead(IReadOnlyLocalMemory m,ulong module,string revision,int selectedSlot,double now,out PlayerDamageCounterSample? sample,out string status)
    {
        sample=null;status="Counter unknown";
        if(revision!="421810"||selectedSlot is < -1 or >3||!double.IsFinite(now)||!LocalProcessMemory.IsAddress(module,0x51C46C0))return false;
        var damage=Chain(m,module+0x51C46B8,0x258,0x38,0x450,0x8,0x48);
        if(damage==0)return false;
        var party=Chain(m,module+0x5013530,0x1AB0);
        var members=new List<(int Slot,ulong Member,string Name)>();
        if(party!=0)for(int i=0;i<4;i++)
            if(m.Pointer(party+(uint)i*0x58,out var member)&&LocalProcessMemory.IsAddress(member,0x80))
            {var name=Name(m,member+0x49);if(name.Length>0)members.Add((i,member,name));}
        int slot=selectedSlot;ulong identity=0;string label="manual seat";
        if(slot<0)
        {
            var save=Chain(m,module+0x5013950,0xA8);
            if(save==0||!m.Int32(save+0x44,out var index)||index is <0 or >2||!m.Pointer(save,out var first)||!LocalProcessMemory.IsAddress(first,0x4D9900))
            {status="Cannot identify owner; select your seat manually";return false;}
            var localName=Name(m,first+(uint)index*0x26CC00+0x50);
            var matches=members.Where(x=>localName.Length>0&&x.Name==localName).ToArray();
            if(matches.Length!=1){status="Owner missing/ambiguous; select your seat manually";return false;}
            slot=matches[0].Slot;identity=matches[0].Member;label=matches[0].Name;
        }
        else
        {var entry=members.FirstOrDefault(x=>x.Slot==slot);identity=entry.Member;label=entry.Name??"manual seat (no roster)";}
        var counter=damage+(uint)slot*0x2A0;
        if(!m.Int32(counter,out var total)||total is <0 or >1000000000)return false;
        if(Chain(m,module+0x51C46B8,0x258,0x38,0x450,0x8,0x48)!=damage)return false;
        if(identity!=0&&(!m.Pointer(party+(uint)slot*0x58,out var after)||after!=identity))return false;
        sample=new(slot,label,identity,counter,total,now);status=$"Seat {slot+1}: {label}, total {total} (experimental)";return true;
    }
}

public sealed class SaedResponseSettings
{
    public bool DynamicThreshold {get;set;}=true;
    public bool RollingOutputEnabled {get;set;}=false;
    public double RollingOutputSeconds {get;set;}=60;
    public double RollingOutputMultiplier {get;set;}=4.5;
    public double RollingOutputCooldown {get;set;}=120;
    public double RollingOutputAttack {get;set;}=0; // Explicit panel attack reference; zero disables rather than substituting base raw.
    public bool SoloDisplayFallback {get;set;}=true;
    public int OwnerSlot {get;set;}=-1; // -1 auto; 0..3 explicitly selected by player
    public double ReferenceDamage {get;set;}=1200; // user's estimate, NOT panel attack
    public double PraiseRatio {get;set;}=1d/3;
    public double ManualThreshold {get;set;}=0; // positive overrides estimate*ratio
    public double WindowSeconds {get;set;}=5;
    [System.Text.Json.Serialization.JsonIgnore]
    public double Threshold=>ManualThreshold>0?ManualThreshold:ReferenceDamage*PraiseRatio;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Valid=>double.IsFinite(RollingOutputSeconds)&&RollingOutputSeconds is >=10 and <=300&&double.IsFinite(RollingOutputMultiplier)&&RollingOutputMultiplier is >=1 and <=100&&double.IsFinite(RollingOutputCooldown)&&RollingOutputCooldown is >=10 and <=3600&&double.IsFinite(RollingOutputAttack)&&RollingOutputAttack is >=0 and <=100000&&OwnerSlot is >=-1 and <=3&&double.IsFinite(ReferenceDamage)&&ReferenceDamage is >=1 and <=100000&&double.IsFinite(PraiseRatio)&&PraiseRatio is >0 and <=1&&double.IsFinite(ManualThreshold)&&ManualThreshold is >=0 and <=100000&&double.IsFinite(WindowSeconds)&&WindowSeconds is >=2 and <=10;
}
public sealed record SaedDamageResult(int Damage,double Threshold,bool Good,double Time);
public sealed class SaedDamageWindow
{
    PlayerDamageCounterSample? baseline,last;
    double start,finish;double threshold;bool pending;
    public string Status {get;private set;}="Waiting for SAED";
    public bool Active=>pending;
    public void Reset(string reason="Waiting for SAED"){baseline=last=null;pending=false;Status=reason;}
    public bool Start(PlayerDamageCounterSample? sample,double now,SaedResponseSettings settings)
    {
        if(pending)return false;
        if(!settings.Valid||sample==null||!double.IsFinite(now)||now<sample.Time||now-sample.Time>.35){Status="SAED skipped: no fresh owner counter";return false;}
        baseline=last=sample;start=now;finish=now+settings.WindowSeconds;threshold=settings.Threshold;pending=true;Status="Measuring SAED damage";return true;
    }
    public SaedDamageResult? Observe(PlayerDamageCounterSample? sample,double now,bool complete=false,bool waitForCompletion=false)
    {
        if(!pending)return null;
        if(sample==null||baseline==null||last==null||!double.IsFinite(now)||now<start||sample.Time<last.Time||now<sample.Time||now-sample.Time>.35||sample.Time-last.Time>.75||sample.Slot!=baseline.Slot||sample.Member!=baseline.Member||sample.Counter!=baseline.Counter||sample.Name!=baseline.Name||sample.Source!=baseline.Source||sample.Total<last.Total)
        {Reset("SAED cancelled: counter gap, reset or owner change");return null;}
        last=sample;
        if(!complete&&(waitForCompletion||now<finish))return null;
        var result=new SaedDamageResult(sample.Total-baseline.Total,threshold,sample.Total-baseline.Total>=threshold,now);
        pending=false;
        if(sample.Total==0){Status="SAED silent: zero-only counter may be inactive in this area";return null;}
        Status=$"SAED delta {result.Damage}, threshold {threshold:0.#}: {(result.Good?"good":"mostly missed")} (estimate)";return result;
    }
    public static ActionReaction Reaction(SaedDamageResult result)=>result.Good
        ?new("saed_good","帅气的一击！回去本鱼给你加饭！","happy",.75f,$"超解时间窗内主人累计伤害增加{result.Damage}，达到宽松阈值{result.Threshold:0.#}，按规则评价打得好。这是估计，不编造瓶数或精确命中率。按人设简短夸奖。")
        :new("saed_mostly_missed","废了老铁，废了……下一发本鱼陪你找准点！","relaxed",.65f,$"超解时间窗内主人累计伤害增加{result.Damage}，低于宽松阈值{result.Threshold:0.#}，按规则轻轻调侃基本空完了。这是估计，不说精确命中瓶数，不侮辱玩家。按人设说一句。");
}

