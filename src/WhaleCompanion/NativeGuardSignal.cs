namespace WhaleCompanion;

public sealed record NativeGuardSample(ulong Owner,ulong Data,string Weapon,float Timer);

// HunterPie ef654889, World/AbnormalityData.xml: SKILLS +79C, float Timer.
// Reads a native SKILL RESULT, not a public GP callback or a recreation of guard timing.
public static class NativeGuardSignal
{
    public static bool CanGuard(string weapon)=>weapon is "GreatSword" or "SwordAndShield" or "Lance" or "GunLance" or "ChargeBlade" or "HeavyBowgun";
    public static bool TryRead(IReadOnlyLocalMemory memory,ulong player,string weapon,string revision,out NativeGuardSample? sample)
    {
        sample=null;
        if(revision!="421810"||!CanGuard(weapon)||!LocalProcessMemory.IsAddress(player,0x7D28)||!memory.Pointer(player+0x7D20,out var data)||!LocalProcessMemory.IsAddress(data,0x7A0)||!memory.Int32(data+0x79C,out var bits))return false;
        var timer=BitConverter.Int32BitsToSingle(bits);
        if(!float.IsFinite(timer)||timer<0||timer>1000000||!memory.Pointer(player+0x7D20,out var after)||after!=data)return false;
        sample=new(player,data,weapon,timer);return true;
    }
}

public sealed class NativeGuardCompanion
{
    private ulong owner,data;private string weapon="";
    private double last=double.NaN,nextAllowed;
    private float previous;
    private int count,pending;
    private bool? stable;
    private bool candidate,refresh;
    public void Reset(){owner=data=0;weapon="";last=double.NaN;previous=0;count=pending=0;stable=null;nextAllowed=0;candidate=refresh=false;}
    public ActionReaction? Observe(NativeGuardSample? sample,double now)
    {
        if(sample==null||!double.IsFinite(now)||!float.IsFinite(sample.Timer)||sample.Timer<0){Reset();return null;}
        if(owner!=sample.Owner||data!=sample.Data||weapon!=sample.Weapon||!double.IsFinite(last)||now<last||now-last>1)Reset();
        if(last==now)return null;
        bool active=sample.Timer>0;
        bool jump=stable==true&&previous>0&&sample.Timer-previous>Math.Max(.25f,previous*.1f);
        bool changed=active!=candidate;
        if(changed){candidate=active;count=1;}else count++;
        if(jump){pending=1;refresh=true;}
        else if(pending>0&&active)pending++;
        if(!active){pending=0;refresh=false;}
        owner=sample.Owner;data=sample.Data;weapon=sample.Weapon;last=now;previous=sample.Timer;
        bool trigger=false;
        if(count>=3&&stable!=active){trigger=stable==false&&active;stable=active;}
        if(pending>=3){trigger=true;pending=0;}
        if(!trigger||now<nextAllowed)return null;
        var wasRefresh=refresh;refresh=false;nextAllowed=now+ResponseTuning.Cooldown("guard",15);
        var text=wasRefresh?"起势续上了，本鱼陪你准备下一招。":"准备接下一招，本鱼看着呢！";
        return new(wasRefresh?"offensive_guard_refresh":"offensive_guard_activated",text,"happy",.6f,
            $"原生技能结果的实验读取：{sample.Weapon}攻击守势增益{(wasRefresh?"计时出现明显回升并保持":"从未激活变为激活并保持")}。社区字段尚待本机实测。只作为后续动作准备陪伴，不将GP当作本次已确认结果；可提及已观察的攻击守势触发/续上，不知道具体防御动作，不能说确定盾斧GP、完美反击、无伤、反击命中或精确帧数。未读到守势不代表防御失败，未装备技能也可能成功防御。按当前人设简短回应。");
    }
}

// HunterPie World 421810 ABNORMALITY_GEAR_OFFSETS 50/12608/0.
// Legacy IsGearBuff=true selects this component, not ordinary abnormality +7D20.
// FC4 remains a mantle-specific timer candidate until game validation.
public static class NativeEvasionSignal
{
    public static bool TryRead(IReadOnlyLocalMemory memory,ulong player,string revision,out float timer)
    {
        timer=0;
        if(revision!="421810"||!LocalProcessMemory.IsAddress(player,0x12610)||!memory.Pointer(player+0x12608,out var data)||!LocalProcessMemory.IsAddress(data,0xFC8)||!memory.Int32(data+0xFC4,out var bits))return false;
        var value=BitConverter.Int32BitsToSingle(bits);
        if(!float.IsFinite(value)||float.IsSubnormal(value)||value<0||value>1000000||!memory.Pointer(player+0x12608,out var after)||after!=data)return false;
        timer=value;return true;
    }
}

// Reacts only to the observed mantle-specific result; never infers an unmantled evade.
public sealed class NativeEvasionCompanion
{
    private ulong owner; private string weapon=""; private float? previous;
    private double last=double.NaN,nextAllowed; private int pending; private bool refresh;
    public void Reset(){owner=0;weapon="";previous=null;last=double.NaN;nextAllowed=0;pending=0;refresh=false;}
    public ActionReaction? Observe(ulong player,string currentWeapon,float? timer,double now,bool eligible=true)
    {
        if(player==0||timer is not {} value||!float.IsFinite(value)||float.IsSubnormal(value)||value<0||value>60||!double.IsFinite(now)){Reset();return null;}
        if(owner!=player||weapon!=currentWeapon||!double.IsFinite(last)||now<last||now-last>.75)Reset();
        if(now==last)return null;
        if(previous is {} old)
        {
            bool activated=old==0&&value>0, renewed=old>0&&value-old>.5f;
            if(activated||renewed){pending=1;refresh=renewed;}
            else if(pending>0&&value>0)pending++;
            if(value==0)pending=0;
        }
        owner=player;weapon=currentWeapon;previous=value;last=now;
        if(pending<3)return null;
        pending=0;
        if(now<nextAllowed)return null;
        nextAllowed=now+ResponseTuning.Thresholds.MantleCooldown;
        return new(refresh?"evasion_mantle_refresh":"evasion_mantle_active",refresh?"回避衣装的劲又续上啦，本鱼给你加油！":"回避衣装亮起来啦，这下本鱼也跟着来劲了！","happy",.6f,
            $"已观察回避衣装专用增伤计时{(refresh?"刷新":"从零激活")}，当前约{value:0.##}秒，连续采样稳定。此字段已在本机太刀翻滚测试看到约20秒刷新及归零。只评价衣装增益触发，不声称特定武器动作已实机验收、无伤、反击命中或衣装外成功闪避。按人设简短回应一次。");
    }
}

public static class EvasionActionScope
{
    public static string? Identify(string weapon,int set,int id)
    {
        if(!WeaponStateProbe.Supports(weapon))return null;
        if(set==1&&id==53)return "common_roll"; // Observed in LongSword mantle and HuntingHorn unmantled tests; other weapons not runtime-certified.
        if(set!=3)return null;
        if(weapon=="Bow"&&id is >=85 and <=88)return "bow_charge_step";
        if(weapon=="DualBlades"&&id==94)return "db_demon_rush";
        if(weapon=="DualBlades"&&id is >=105 and <=109)return "db_demon_step";
        if(id is >=18 and <=22)return "common_roll";
        return null;
    }
}

// Upstream flag fields observed in the loaded activation caller. Semantics unvalidated.
public static class EvasionFlagProbe
{
    public static bool TryRead(IReadOnlyLocalMemory memory,ulong player,string revision,out uint a,out uint b)
    {
        a=b=0;
        if(revision!="421810"||!LocalProcessMemory.IsAddress(player,0x12610)||!memory.Pointer(player+0x12608,out var gear)||!LocalProcessMemory.IsAddress(gear,0x59B4)||!memory.Int32(gear+0x5998,out var first)||!memory.Int32(gear+0x59B0,out var second)||!memory.Pointer(player+0x12608,out var after)||gear!=after)return false;
        a=unchecked((uint)first);b=unchecked((uint)second);return true;
    }
}


// Read-only equivalent of the captured 12DBE50 source accessor and 2077A30 source checks.
// Source eligibility is NOT an evade-success verdict.
public static class EvasionEventSource
{
    private static bool Alive(IReadOnlyLocalMemory m,ulong at)=>LocalProcessMemory.IsAddress(at,0x10)&&m.Int32(at+0xC,out var flags)&&(flags&0xE)!=0;
    public static bool TryResolve(IReadOnlyLocalMemory m,ulong payload,out ulong source)
    {
        source=0;
        if(!LocalProcessMemory.IsAddress(payload,0x40)||!m.Pointer(payload+8,out var backing)||!Alive(m,backing))return false;
        if(m.Pointer(payload+0x38,out var reference)&&LocalProcessMemory.IsAddress(reference,0x80)&&m.Pointer(reference+0x78,out var link)&&LocalProcessMemory.IsAddress(link,0x1E0)&&m.Pointer(link+0x1D8,out var preferred)&&Alive(m,preferred))
        {source=preferred;return true;}
        if(!m.Int32(backing+0x60,out var flags))return false;
        if((flags&0x400)!=0)return m.Pointer(backing+0xB40,out source)&&Alive(m,source);
        if((flags&0x02000000)!=0)return m.Pointer(backing+0x9E0,out source)&&Alive(m,source);
        source=backing;return true;
    }
    public static bool? Eligible(IReadOnlyLocalMemory m,ulong source,out uint? sourceFlags)
    {
        sourceFlags=null;if(!Alive(m,source)||!m.Int32(source+0x60,out var raw))return null;
        uint flags=unchecked((uint)raw);sourceFlags=flags;
        if((flags&0x10)!=0)return m.Int32(source+0x8D8,out var subtype)?subtype==0x45:null;
        if((flags&2)!=0)return true;
        if((flags&0x400)==0)return false;
        if(!m.Pointer(source+0xB40,out var parent)||!Alive(m,parent)||!m.Int32(parent+0x60,out var parentFlags))return null;
        return (parentFlags&2)!=0;
    }
}
