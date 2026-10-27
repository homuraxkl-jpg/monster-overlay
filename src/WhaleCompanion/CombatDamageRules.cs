using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WhaleCompanion;

public sealed record WeaponDamageData(string Weapon,int Row,double Raw,int ElementType,double Element,int Special);
public sealed record MotionValueData(string Sheet,int Row,string Name,string Type,string RawText,string FixedText,string ElementText,string Gid);
public static class CombatReference
{
    static T[] Load<T>(string name)
    {using var stream=typeof(CombatReference).Assembly.GetManifestResourceStream("WhaleCompanion."+name)??throw new InvalidDataException(name);return JsonSerializer.Deserialize<T[]>(stream)??throw new InvalidDataException(name);}
    static readonly Lazy<Dictionary<(string,int),WeaponDamageData>> Equipment=new(()=>Load<WeaponDamageData>("equipment-damage.json").ToDictionary(x=>(x.Weapon,x.Row)));
    static readonly Lazy<Dictionary<(string,int),MotionValueData>> Motions=new(()=>Load<MotionValueData>("motion-values.json").ToDictionary(x=>(x.Sheet,x.Row)));
    public static int EquipmentCount=>Equipment.Value.Count;
    public static int MotionCount=>Motions.Value.Count;
    public static WeaponDamageData? Weapon(string weapon,int row)=>Equipment.Value.GetValueOrDefault((weapon,row));
    public static MotionValueData? Motion(string sheet,int row)=>Motions.Value.GetValueOrDefault((sheet,row));
    public static double Number(string? value)=>double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var result)&&double.IsFinite(result)&&result>=0?result:0;
    public static double Raw(string sheet,params int[] rows)=>rows.Sum(row=>Number(Motion(sheet,row)?.RawText));
    public static double Element(string sheet,params int[] rows)=>rows.Sum(row=>Number(Motion(sheet,row)?.ElementText));
    public static double Fixed(string sheet,int row,int level)
    {var text=Motion(sheet,row)?.FixedText??"";var match=Regex.Match(text,@"\[Lv"+Math.Clamp(level,1,7)+@"\]\s*(\d+(?:\.\d+)?)");return match.Success?Number(match.Groups[1].Value):0;}
}

public sealed record CombatEquipment(ulong Owner,ulong Object,WeaponDamageData Stats,int Sharpness,double Time);
public static class EquipmentDamageProbe
{
    public static bool TryRead(IReadOnlyLocalMemory m,ulong player,string weapon,string revision,double now,out CombatEquipment? sample)
    {
        sample=null;if(revision!="421810"||!WeaponStateProbe.Supports(weapon)||!double.IsFinite(now)||!LocalProcessMemory.IsAddress(player,0x76B8))return false;
        if(!m.Pointer(player+0x76B0,out var current)||!LocalProcessMemory.IsAddress(current,0x9FC)||!m.Int32(current+0x9F8,out var type)||type!=Array.IndexOf(WeaponStateProbe.Weapons,weapon))return false;
        ulong root=player;
        foreach(uint offset in new uint[]{0x98,0x10,0x70,0x18,0x550})
        {if(!LocalProcessMemory.IsAddress(root,offset+8)||!m.Pointer(root+offset,out root)||!LocalProcessMemory.IsAddress(root,8))return false;}
        if(!LocalProcessMemory.IsAddress(root,0x2100)||!m.Int32(root+0x1D0C,out var row)||row is <0 or >8191)return false;
        var stats=CombatReference.Weapon(weapon,row);if(stats==null)return false;
        int sharp=-1;if(m.Int32(root+0x20FC,out var s)&&s is >=0 and <=7)sharp=s;
        if(!m.Pointer(player+0x76B0,out var after)||after!=current||!m.Int32(root+0x1D0C,out var afterRow)||afterRow!=row)return false;
        ulong finalRoot=player;foreach(uint offset in new uint[]{0x98,0x10,0x70,0x18,0x550})
        {if(!LocalProcessMemory.IsAddress(finalRoot,offset+8)||!m.Pointer(finalRoot+offset,out finalRoot))return false;}
        if(finalRoot!=root||!m.Int32(current+0x9F8,out var finalType)||finalType!=type)return false;
        sample=new(player,current,stats,sharp,now);return true;
    }
}

public sealed record CombatFacts(ulong Owner,string Weapon,CombatEquipment? Equipment,Dictionary<string,double> Fields,double Time)
{
    public double? Value(string key)=>Fields.TryGetValue(key,out var value)&&double.IsFinite(value)?value:null;
    public static CombatFacts Read(IReadOnlyLocalMemory m,ulong player,string weapon,string revision,double now)
    {
        if(revision!="421810")return new(player,weapon,null,new(),now);
        EquipmentDamageProbe.TryRead(m,player,weapon,revision,now,out var equipment);
        WeaponStateProbe.TryRead(m,player,weapon,revision,out var state,out _);
        var fields=state?.Fields??new Dictionary<string,double>();
        // Same health component as SPL Player.Health, read through the guarded OS reader.
        if(m.Pointer(player+0x7630,out var health)&&LocalProcessMemory.IsAddress(health,0x68)&&m.Int32(health+0x64,out var hpBits)&&m.Int32(health+0x60,out var maxBits))
        {
            float hp=BitConverter.Int32BitsToSingle(hpBits),maximum=BitConverter.Int32BitsToSingle(maxBits);
            if(float.IsFinite(hp)&&float.IsFinite(maximum)&&maximum is >0 and <=100000&&hp>=0&&hp<=maximum&&m.Pointer(player+0x7630,out var afterHealth)&&afterHealth==health)fields["health"]=hp;
        }
        if(weapon=="LongSword"&&SpiritGaugeProbe.TryRead(m,player,out var level,out _))fields["spirit_level"]=level;
        return new(player,weapon,equipment,fields,now);
    }
}

public sealed record CombatProfile(string Key,string Label,string Sheet,int[] Rows,double Seconds,string Gate="damage",double Repeats=1);
public static class CombatProfiles
{
    static CombatProfile P(string key,string label,string sheet,int[] rows,double seconds=3,string gate="damage",double repeats=1)=>new(key,label,sheet,rows,seconds,gate,repeats);
    public static (int Type,int Level)? Shell(int special)=>special switch
    {
        >=0 and <=4=>(0,special+1), >=5 and <=9=>(2,special-4), >=10 and <=14=>(1,special-9),
        15=>(0,6),16=>(2,6),17=>(1,6),18=>(0,7),19=>(2,7),_=>null
    };
    static CombatProfile? Ammo(CombatFacts facts)
    {
        if(facts.Value("ammo_index_raw") is not {} raw||facts.Value("magazine") is not >0)return null;
        int index=(int)raw;
        int[]? rows=index switch
        {
            >=0 and <=2=>[7+index], >=3 and <=5=>[10+index-3], >=6 and <=8=>[13+index-6],
            >=9 and <=11=>[21+index-9],12=>[41,42],>=13 and <=15=>[17+index-13],16=>[40],>=17 and <=21=>[34+index-17],_=>null
        };
        if(rows==null)return null;
        double repeats=index switch{>=3 and <=5=>3,6=>3,7=>5,8=>7,>=9 and <=11=>3,16=>5,_=>1};
        var label=CombatReference.Motion("Bowgun Ammo",rows[0])?.Name??"射击";
        return P("ammo_"+index,label,"Bowgun Ammo",rows,index is >=9 and <=16?6:3,"ammo_spent",repeats);
    }
    public static CombatProfile? Identify(string weapon,int set,int id,CombatFacts facts)
    {
        if(set!=3||WeaponActions.Name(weapon,set,id)==null)return null;
        int gauge=(int)(facts.Value("spirit_level")??0);
        if(weapon is "LightBowgun" or "HeavyBowgun"&&id is 68 or 70 or 72 or 73 or 74 or 75 or 76)return null;
        return (weapon,id) switch
        {
            ("ChargeBlade",119 or 120 or 122)=>P("cb_saed","超解","Charge Blade",facts.Value("phials")>0?[26,27,28]:[29,30],5,"phials"),
            ("ChargeBlade",117 or 118 or 121)=>P("cb_aed","高解","Charge Blade",[25],4,"phials"),
            ("Hammer",81)=>P("hammer_big_bang","敲打终结","Hammer",[25,25,29]),
            ("Hammer",94 or 131)=>P("hammer_charge3","蓄力大地一击","Hammer",facts.Value("power_charge")>0?[19,19,20]:[16,17]),
            ("Hammer",130)=>P("hammer_charge2","蓄力上捞","Hammer",[18]),
            ("SwitchAxe",124 or 134)=>P("sa_zsd","零距离解放","Switch Axe",[43,44,45],8,"zsd_finish"),
            ("SwitchAxe",125 or 126)=>P("sa_zsd","零距离解放终结","Switch Axe",[45],3,"zsd_finish"),
            ("SwitchAxe",121 or 122)=>P("sa_ed","属性解放终结","Switch Axe",[45]),
            ("SwitchAxe",135 or 136)=>null, // Preparation is handled by the observed power_axe state.
            ("LongSword",86)=>P("ls_foresight","见切反击","Long Sword",[20],3,"foresight_branch"),
            ("LongSword",102)=>P("ls_iai","居合拔刀气刃斩","Long Sword",gauge switch{1=>[28,29],2=>[30,31],3=>[32,33],_=>[27]},3,"iai_no_drop"),
            ("LongSword",87 or 89 or 90 or 91 or 92)=>P("ls_helm","登龙","Long Sword",[gauge switch{3=>24,2=>23,_=>22}],5,"helm_release",7),
            ("GreatSword",78)=>P("gs_tcs","真蓄力斩","Great Sword",[23],5,"tcs_finish"),
            ("GreatSword",135 or 136 or 137)=>P("gs_tcs","真蓄力斩（强击）","Great Sword",[id-111],3,"tcs_finish"),
            ("SwordAndShield",125 or 126 or 127 or 128 or 129 or 130 or 131 or 132 or 133 or 134)=>P("sns_rush","精准突击","Sword & Shield",[33,34,35,36,37,38],6,"rush_timing"),
            ("DualBlades",92)=>P("db_dance","鬼人乱舞","Dual Blades",Enumerable.Range(59,16).ToArray(),5,"demon"),
            ("HuntingHorn",74)=>P("hh_echo","响音攻击","Hunting Horn",[15,16],3),
            ("Lance",78 or 79 or 80 or 81)=>P("lance_counter","反击突刺","Lance",[15]),
            ("Lance",163)=>P("lance_claw","反击飞翔爪","Lance",[25,26],4),
            ("GunLance",85 or 86)=>null,
            ("GunLance",104 or 105 or 158)=>P("gl_burst","全弹发射","Gunlance",[],4,"shells"),
            ("GunLance",106 or 107)=>P("gl_wyvern","龙击炮","Gunlance",[],4),
            ("GunLance",110 or 111 or 112 or 113 or 114 or 115)=>P("gl_stake","龙杭炮","Gunlance",[26,27],5),
            ("InsectGlaive",78)=>P("ig_tornado","飞圆斩","Insect Glaive",[27,28],3,"red_extract"),
            ("InsectGlaive",115)=>P("ig_dive","急袭突刺","Insect Glaive",[40,41,42,43],4),
            ("Bow",75)=>P("bow_power","连续刚射阶段","Bow",[(facts.Value("charge_level_raw")??1)>=3?14:13],3,"bow_charge",5),
            ("Bow",78)=>P("bow_piercer","龙之一矢","Bow",[21],5,"damage",3),
            ("Bow",112)=>P("bow_thousand","龙之千矢","Bow",[27],4,"damage",6),
            ("HeavyBowgun",98 or 99) when facts.Value("special_ammo_type_raw")==2=>P("hbg_special","狙击龙弹","Bowgun Ammo",[47,49],5),
            ("HeavyBowgun",88 or 89 or 90 or 91 or 92 or 93) when facts.Value("special_ammo_type_raw")==1=>P("hbg_heart","机关龙弹","Bowgun Ammo",[46],3),
            ("HeavyBowgun",106)=>P("hbg_melee","弩枪近战","Heavy Bowgun",[7]),
            ("LightBowgun",99)=>P("lbg_melee","弩枪近战","Light Bowgun",[7]),
            _=>null
        };
    }
    public static bool Handles(string weapon,int set,int id)=>weapon=="DualBlades"&&set==3&&id==126||set==3&&WeaponActions.Name(weapon,set,id)!=null&&((weapon=="HeavyBowgun"&&id is 88 or 89 or 90 or 91 or 92 or 93 or 98 or 99)||Identify(weapon,set,id,new(1,weapon,null,new(),0))!=null);
    public static double Estimate(CombatProfile profile,CombatFacts facts,int actionId)
    {
        var equipment=facts.Equipment;if(equipment==null)return 0;
        var stats=equipment.Stats;
        double raw=CombatReference.Raw(profile.Sheet,profile.Rows)*profile.Repeats;
        if(profile.Key=="ls_foresight")raw=12; // Source row 20: 12 or 26; use the conservative numeric branch.
        double element=CombatReference.Element(profile.Sheet,profile.Rows)*profile.Repeats;
        // Conservative reference target, not a claim about the actual hitzone or critical damage.
        double fixedRaw=profile.Rows.Sum(row=>CombatReference.Motion(profile.Sheet,row)?.Type=="None"?CombatReference.Raw(profile.Sheet,row):0)*profile.Repeats;
        double expected=stats.Raw*(raw-fixedRaw)/100*1.05*.45+stats.Raw*fixedRaw/100+(stats.ElementType is >=1 and <=5?stats.Element:0)*element*.15;
        if(facts.Weapon=="SwitchAxe"&&stats.Special==0)expected*=1.17; // Power phial, conservative sword estimate.
        if(profile.Key.StartsWith("cb_",StringComparison.Ordinal))
        {
            int phials=(int)Math.Clamp(facts.Value("phials")??0,0,6);int blasts=profile.Key=="cb_saed"?phials:phials>0?3:0;
            if(facts.Value("shield_timer_raw")>0)expected*=1.1;
            expected+=stats.Special==0?stats.Raw*(profile.Key=="cb_saed"?.25:.09)*blasts:stats.Element*(profile.Key=="cb_saed"?9:5)*.15*blasts;
        }
        if(profile.Key=="sa_zsd")
        {expected=stats.Raw*(profile.Rows.Length==3?18+13*12+85*1.4:85*1.4)/100*1.05*.45;if(actionId==126)expected*=.7;}
        if(profile.Key is "sa_zsd" or "sa_ed"&&stats.Special==0)expected*=1.17;
        if(profile.Key=="gl_burst"||profile.Key=="gl_wyvern")
        {
            var shellType=Shell(stats.Special);if(shellType==null)return 0;
            var (type,level)=shellType.Value;
            double shell=CombatReference.Fixed("Gunlance",(profile.Key=="gl_burst"?20:23)+type,level);
            double multiplier=profile.Key=="gl_burst"?type switch{0=>1.2,2=>.9,_=>1}:1;
            expected=shell*multiplier*(profile.Key=="gl_burst"?Math.Clamp(facts.Value("shells")??0,0,8):3);
        }
        return double.IsFinite(expected)&&expected>0?expected:0;
    }
}

public sealed record CombatActionRequest(ActionObservation Action,CombatFacts Before,PlayerDamageCounterSample? DamageBaseline=null);
public sealed record CombatDamageResult(string Weapon,string Key,string Label,int Damage,double Reference,double Threshold,bool Good,string Mechanic,double Time,string DamageSource="party",string Outcome="damage_verdict");
public sealed class CombatDamageDirector
{
    readonly SaedDamageWindow window=new();
    CombatFacts? spineBefore,spineCompletedFacts; double spineStart; bool spineFinal,spineLanded; CombatDamageResult? spinePending;
    bool EnterSpine(NativeActionTransition transition,CombatActionRequest? request)
    {
        var c=transition.Current;
        if(spineBefore!=null)
        {
            if(!double.IsFinite(c.Time)||c.Owner!=spineBefore.Owner||c.Weapon!="DualBlades"||c.Time<spineStart||c.Time-spineStart>15){spineBefore=null;return false;}
            if(c.Set==3&&c.Id is 126 or 127 or 128 or 129)
            {
                if(c.Id==127)spineFinal=true;
                if(c.Id is 128 or 129&&spineFinal)spineLanded=true;
                return true;
            }
            if(spineFinal&&spineLanded){spineCompletedFacts=spineBefore;spinePending=new("DualBlades","db_heavenly","空中回转乱舞连段",0,0,0,true,"已观察到126起始、127终结及128/129落地；只确认动作序列，不评价伤害、全段命中或命中率",c.Time,"action_only","spine_complete");}
            spineBefore=null;return true;
        }
        if(c.Weapon!="DualBlades"||c.Set!=3||c.Id!=126||request==null||!NativeActionSignal.Confirms(request.Action,transition,c.Time)||request.Before.Owner!=c.Owner||request.Before.Weapon!=c.Weapon||!double.IsFinite(request.Before.Time)||!double.IsFinite(c.Time)||c.Time-request.Before.Time is <0 or >.75)return false;
        spineBefore=request.Before;spineStart=c.Time;spineFinal=spineLanded=false;return true;
    }
    private ulong bowOwner,bowEquipment; private double lastPower=double.NaN; private readonly Queue<double> powerShots=new();
    CombatFacts? before;CombatProfile? profile;double start,reference;int actionId;bool finished,weak,just,lastJust,diveFollow;double? minGauge,minPhials,minMagazine,minShells;bool mechanismGap;PlayerDamageCounterSample? sealedCounter;double sealedTime;SaedResponseSettings? activeSettings;double? minHealth;bool healthGap;
    public string Status=>profile==null?window.Status:$"{profile.Label}: ref {reference:0.#}, gate {profile.Gate}; {window.Status}";
    public bool Active=>window.Active||spineBefore!=null;
    public static bool NeedsEveryEntry(string weapon,int set,int id)=>set==3&&(weapon=="Bow"&&id==75||weapon=="DualBlades"&&id==126);
    void ClearPowerShots(){bowOwner=bowEquipment=0;lastPower=double.NaN;powerShots.Clear();}
    public void Reset(){spineBefore=spineCompletedFacts=null;spinePending=null;spineFinal=spineLanded=false;bowOwner=bowEquipment=0;lastPower=double.NaN;powerShots.Clear();window.Reset();before=null;profile=null;diveFollow=false;mechanismGap=false;sealedCounter=null;activeSettings=null;}
    public bool Enter(NativeActionTransition transition,CombatActionRequest? request,PlayerDamageCounterSample? counter,SaedResponseSettings settings)
    {
        var current=transition.Current;
        if(EnterSpine(transition,request))return false;
        if(before!=null&&profile!=null&&window.Active)
        {
            if(sealedCounter!=null)return false;
            var next=CombatProfiles.Identify(current.Weapon,current.Set,current.Id,before);
            if(current.Weapon=="ChargeBlade"&&current.Set==3&&current.Id is 109 or 148){Reset();return false;}
            // SUPER_FINISH can be cancelled into AED MAIN. The eventual branch determines the label.
            if(profile.Key=="cb_saed"&&next?.Key=="cb_aed"&&activeSettings!=null)
            {
                profile=next;reference=activeSettings.DynamicThreshold?CombatProfiles.Estimate(next,before,current.Id):activeSettings.ReferenceDamage;
                window.Reset();window.Start(request?.DamageBaseline??counter,current.Time,new SaedResponseSettings{ReferenceDamage=Math.Clamp(reference,1,100000),PraiseRatio=activeSettings.PraiseRatio,ManualThreshold=activeSettings.ManualThreshold,WindowSeconds=next.Seconds});
                finished=true;return false;
            }
            // The powered TCS branch has its own motion value and player-facing label.
            // Start at its actual entry so the preceding connecting hit is not counted twice.
            if(profile.Key=="gs_tcs"&&next?.Key=="gs_tcs"&&current.Set==3&&current.Id is 135 or 136 or 137&&activeSettings!=null)
            {
                profile=next;actionId=current.Id;reference=activeSettings.DynamicThreshold?CombatProfiles.Estimate(next,before,current.Id):activeSettings.ReferenceDamage;
                window.Reset();window.Start(request?.Action.Id==current.Id?request.DamageBaseline??counter:counter,current.Time,new SaedResponseSettings{ReferenceDamage=Math.Clamp(reference,1,100000),PraiseRatio=activeSettings.PraiseRatio,ManualThreshold=activeSettings.ManualThreshold,WindowSeconds=next.Seconds});
                finished=true;return false;
            }
            // Measure the descending slash only: the connecting thrust is not a successful helm breaker.
            if(profile.Key=="ls_helm"&&current.Set==3&&current.Id==91&&!finished&&activeSettings!=null)
            {
                window.Reset();window.Start(request?.Action.Id==91?request.DamageBaseline??counter:counter,current.Time,new SaedResponseSettings{ReferenceDamage=Math.Clamp(reference,1,100000),PraiseRatio=activeSettings.PraiseRatio,ManualThreshold=activeSettings.ManualThreshold,WindowSeconds=3});
                finished=true;
            }
            if(next?.Key==profile.Key)
            {
                finished|=profile.Key=="sa_zsd"&&current.Id==125||profile.Key=="gs_tcs"&&current.Id is 135 or 136 or 137||profile.Key=="ls_helm"&&current.Id is 91 or 92||profile.Key=="sns_rush"&&current.Id is 133 or 134;
                weak|=profile.Key=="sa_zsd"&&current.Id==126;
                if(profile.Key=="sns_rush"){lastJust=current.Id is 126 or 128 or 130 or 132 or 134;just|=lastJust;}
            }
            if(profile.Key=="cb_saed"&&current.Set==3&&current.Id is 120 or 122)finished=true;
            if(profile.Key=="ls_foresight"&&current.Set==3&&current.Id==69&&transition.Previous.Id==86)finished=true;
            if(next?.Key!=profile.Key)
            {
                if(profile.Key=="ig_dive"&&next?.Key=="ig_tornado"&&current.Time-start<=3)diveFollow=true;
                if(profile.Key=="cb_saed"&&!finished){Reset();return false;}
                // Freeze before the following action's hits; do not award a previous move its combo's damage.
                sealedCounter=counter;sealedTime=current.Time;
                if(counter==null)Reset();
            }
            return false;
        }
        if(request==null||!NativeActionSignal.Confirms(request.Action,transition,current.Time))return false;
        var facts=request.Before;
        if(facts.Owner!=current.Owner||facts.Weapon!=current.Weapon||!double.IsFinite(facts.Time)||current.Time-facts.Time is <0 or >.75)return false;
        if(current.Weapon=="Bow"&&current.Set==3&&current.Id==75)
        {
            if(bowOwner!=current.Owner||bowEquipment!=(facts.Equipment?.Object??0))ClearPowerShots();
            // Duplicate confirmations must not create another shot or erase valid earlier entries.
            if(double.IsFinite(lastPower)&&current.Time<=lastPower)return false;
            bowOwner=current.Owner;bowEquipment=facts.Equipment?.Object??0;lastPower=current.Time;
            while(powerShots.Count>0&&current.Time-powerShots.Peek()>ResponseTuning.Thresholds.BowPowerWindow)powerShots.Dequeue();
            powerShots.Enqueue(current.Time);
            if(powerShots.Count<ResponseTuning.Thresholds.BowPowerCount)return false;
            powerShots.Clear();
        }
        if(facts.Owner!=current.Owner||facts.Weapon!=current.Weapon||!double.IsFinite(facts.Time)||current.Time-facts.Time is <0 or >.75)return false;
        var rule=CombatProfiles.Identify(current.Weapon,current.Set,current.Id,facts);if(rule==null)return false;
        // Never infer an entire spine run from its finisher or landing alone.
        if(rule.Key=="db_heavenly"&&current.Id!=126)return false;
        var estimate=settings.DynamicThreshold?CombatProfiles.Estimate(rule,facts,current.Id):settings.ReferenceDamage;
        if(!double.IsFinite(estimate)||estimate<=0){window.Reset("Dynamic estimate unavailable; no fixed endgame fallback");return false;}
        if(rule.Gate=="iai_no_drop"&&facts.Value("spirit_level") is not >0){window.Reset("Iai pre-action gauge unknown/none; no false counter success");return false;}
        var effective=new SaedResponseSettings{ReferenceDamage=Math.Clamp(estimate,1,100000),PraiseRatio=settings.PraiseRatio,ManualThreshold=settings.ManualThreshold,WindowSeconds=rule.Seconds,OwnerSlot=settings.OwnerSlot};
        counter=request.DamageBaseline??counter;
        if(!window.Start(counter,current.Time,effective))return false;
        sealedCounter=null;diveFollow=false;activeSettings=settings;minHealth=facts.Value("health");healthGap=minHealth==null;
        before=facts;profile=rule;start=current.Time;reference=estimate;actionId=current.Id;minGauge=facts.Value("spirit_level");minPhials=facts.Value("phials");minMagazine=facts.Value("magazine");minShells=facts.Value("shells");mechanismGap=false;
        finished=rule.Key=="sa_zsd"&&current.Id==125||rule.Key=="gs_tcs"&&current.Id is 135 or 136 or 137||rule.Key=="ls_helm"&&current.Id is 91 or 92||rule.Key=="sns_rush"&&current.Id is 133 or 134;
        finished|=rule.Key=="cb_saed"&&current.Id is 120 or 122;
        weak=rule.Key=="sa_zsd"&&current.Id==126;just=lastJust=current.Id is 126 or 128 or 130 or 132 or 134;
        return true;
    }
    public CombatDamageResult? Observe(PlayerDamageCounterSample? counter,CombatFacts facts,double now)
    {
        if(bowOwner!=0&&(facts.Owner!=bowOwner||facts.Weapon!="Bow"||(facts.Equipment?.Object??0)!=bowEquipment||!double.IsFinite(now)||double.IsFinite(lastPower)&&now<lastPower))ClearPowerShots();
        if(spineBefore!=null&&(facts.Owner!=spineBefore.Owner||facts.Weapon!=spineBefore.Weapon||facts.Equipment?.Object!=spineBefore.Equipment?.Object||!double.IsFinite(facts.Time)||now-facts.Time is <0 or >.35||now-spineStart>15))spineBefore=null;
        if(spinePending is {} completed)
        {
            spinePending=null;
            var completedFacts=spineCompletedFacts;spineCompletedFacts=null;
            if(completedFacts!=null&&facts.Owner==completedFacts.Owner&&facts.Equipment?.Object==completedFacts.Equipment?.Object&&facts.Weapon==completed.Weapon&&double.IsFinite(now)&&now-completed.Time is >=0 and <=.35)return completed;
        }
        if(!window.Active||before==null||profile==null)return null;
        if(!double.IsFinite(facts.Time)||now<facts.Time||now-facts.Time>.35||facts.Owner!=before.Owner||facts.Weapon!=before.Weapon||facts.Equipment?.Stats.Row!=before.Equipment?.Stats.Row||facts.Equipment?.Object!=before.Equipment?.Object)
        {Reset();return null;}
        if(profile.Gate=="iai_no_drop"&&sealedCounter==null)
        {
            if(facts.Value("health") is {} health)minHealth=Math.Min(minHealth??health,health);else healthGap=true;
        }
        if(profile.Gate=="ammo_spent")
        {
            if(facts.Value("ammo_index_raw")!=before.Value("ammo_index_raw")){Reset();return null;}
            if(facts.Value("magazine") is {} magazine)minMagazine=Math.Min(minMagazine??magazine,magazine);else mechanismGap=true;
        }
        if(profile.Gate=="phials"&&facts.Value("phials") is {} phials)minPhials=Math.Min(minPhials??phials,phials);
        if(profile.Gate=="shells"&&facts.Value("shells") is {} shells)minShells=Math.Min(minShells??shells,shells);
        if(now-start<=1)
        {
            if(profile.Gate=="iai_no_drop")
            {var value=facts.Value("spirit_level");if(value==null)mechanismGap=true;else minGauge=Math.Min(minGauge??value.Value,value.Value);}
            if(profile.Gate=="phials")
            {var value=facts.Value("phials");if(value!=null)minPhials=Math.Min(minPhials??value.Value,value.Value);}
        }
        // Demon dance is one action, not a succession of timed damage windows.
        // Keep validating fresh counters while waiting; an absent exit cancels silently.
        if(profile.Key=="db_dance"&&sealedCounter==null&&now-start>15){Reset();return null;}
        var result=window.Observe(sealedCounter??counter,sealedCounter!=null?sealedTime:now,sealedCounter!=null,profile.Key=="db_dance");if(result==null)return null;
        bool gate=true;string detail="伤害增量估计";
        switch(profile.Gate)
        {
            case "iai_no_drop":
                if(mechanismGap)return null;
                var pre=before.Value("spirit_level");
                gate=pre>0&&minGauge>=pre;detail=pre>0?$"居合前刃色{pre}，出刀后首秒最低刃色{minGauge}，不降级={gate}":"无刃无法用不降级证明反击";break;
            case "foresight_branch":gate=finished;detail=$"见切直接派生气刃大回旋={finished}，且见切自身伤害达到门槛；不声称已读取精确回避帧";break;
            case "helm_release":gate=finished;detail=$"登龙实际下劈/落地阶段={finished}；只起手刺中不算完整登龙";break;
            case "zsd_finish":gate=finished&&!weak;detail=$"零解完整终结={finished}，提前收尾={weak}";break;
            case "rush_timing":gate=just&&lastJust&&finished;detail=$"精准突击节奏分支={just}，末个观察分支为JUST={lastJust}";break;
            case "demon":gate=before.Value("demon_mode")>0;detail=$"出招前鬼人化={gate}；已观察到乱舞动作退出后封存，一套只评价一次；不将动作退出当作全段命中或完整演出证明";break;
            case "red_extract":gate=before.Value("red_extract_timer_raw")>0;detail=$"出招前红精华={gate}";break;
            case "ammo_spent":
                if(mechanismGap)return null;
                gate=minMagazine<before.Value("magazine");detail=$"弹种索引{before.Value("ammo_index_raw")}，弹匣确有消耗={gate}，不把装填当命中";break;
            case "phials":gate=(profile.Key!="cb_saed"||finished)&&(before.Value("phials")==0||minPhials<before.Value("phials"));detail=$"出招前装瓶{before.Value("phials")}，早期最低装瓶{minPhials}；仅辅助记录，不按瓶数倒推命中";break;
            case "tcs_finish":detail=$"真蓄强化终结候选分支={finished}；不凭总伤害声称第一击打中弱点";break;
            case "bow_charge":detail=$"出招前蓄力原始等级{before.Value("charge_level_raw")}，不声称所有箭命中";break;
            case "shells":gate=before.Value("shells")>0&&minShells<before.Value("shells");detail=$"出招前炮弹{before.Value("shells")}，门槛是保守估计，不断言每发炮弹命中";break;
        }
        bool good=result.Good&&gate;string outcome=profile.Key=="ig_dive"?(diveFollow?"ig_dive_follow":"ig_dive_damage"):"damage_verdict";
        if(diveFollow)detail+="；急袭后已进入飞圆动作，只评价封存的急袭伤害，不确认飞圆命中。";
        // A defensive counter branch is not evidence that an enemy attack was blocked.
        if(profile.Key=="lance_counter")
        {
            if(result.Damage<=0)return null;
            outcome="lance_counter_damage";
            detail+="；只确认反击突刺动作分支及伤害增量，未确认敌方攻击触发、防御成功或无伤。";
        }
        if(profile.Key=="ls_foresight"&&!good)return null; // A defensive reposition is not a failure worth mocking.
        if(profile.Key=="ls_iai"&&!good)
        {
            if(result.Damage!=0||!gate||healthGap||before.Value("health") is not {} preHealth||minHealth<preHealth)return null;
            outcome="iai_safe_exit";
            detail+=$"；未观察到伤害，HP出招前{preHealth}、窗口最低{minHealth}。仅符合安全脱身启发式，没有确认敌方攻击或无敌/霸体。";
        }
        return new(before.Weapon,profile.Key,profile.Label,result.Damage,reference,result.Threshold,good,detail,now,counter?.Source??"unknown",outcome);
    }
    public static ActionReaction Reaction(CombatDamageResult result)
    {
        if(result.Outcome=="spine_complete")return new("spine_complete","这一套搓背打出来啦，本鱼给你鼓掌！","happy",.6f,
            $"{result.Mechanic} 按用户要求完整动作序列就夸一次，不设置伤害门槛，不产生低伤害或中间评价；动作编号映射仍待实机确认。按人设简短夸奖。");
        if(result.Outcome=="lance_counter_damage")return new("lance_counter_damage",result.Good?"这一刺打得起劲，本鱼跟上啦！":"这一刺蹭到啦，稳住再找下一次机会。",result.Good?"happy":"sad",.5f,
            $"进入长枪反击突刺动作后伤害增量估计{result.Damage}，达到门槛={result.Good}。{result.Mechanic} 来源={result.DamageSource}，不能保证排除随从或环境。不说挡住攻击、反击成功、无伤或完美防御，只轻松回应这次出刺的伤害估计。");
        if(result.Outcome is "ig_dive_follow" or "ig_dive_damage")
        {
            bool follow=result.Outcome=="ig_dive_follow";
            string diveText=result.Good
                ?(follow?"下戳打得起劲，接着飞圆，本鱼跟上啦！":"这下下戳打得起劲，本鱼看到了！")
                :(follow?"这次下戳没打够劲，接着飞圆再找机会呦。":"这次下戳没打够劲，站稳再找下一次机会呦。");
            return new(result.Outcome,diveText.TrimEnd(),result.Good?"happy":"sad",.5f,
                $"急袭动作阶段伤害增量估计={result.Damage}，门槛={result.Threshold}。{result.Mechanic} 伤害来源={result.DamageSource}，仍可能混入随从或环境。只按是否达到伤害门槛作正面或负面两档评价，不添加中间评价，也不把低伤害或零增量说成已经证明全空。{(follow?"只确认短时间进入飞圆动作，不确认飞圆命中或整套成功，只回应一次衔接。":"只评价急袭阶段，不编造后续连招。")}");
        }
        if(result.Outcome=="iai_safe_exit")return new("iai_safe_exit","这一刀脱身啦，站稳再接下一招。","relaxed",.45f,
            $"大居合安全脱身候选：未观察到对怪伤害，刃色未降、HP采样未减少。{result.Mechanic} 按人设轻松陪伴一句，不贬低空刀，不说反击命中或大成功，也不声称确定躲过敌人攻击、无敌或霸体。空放也可能符合这个启发式。");
        string text=result.Good?$"这发{result.Label}打得好！本鱼看到了！":result.Key is "ls_iai" or "sns_rush" or "sa_zsd"?$"这次{result.Label}没对上，下一次本鱼陪你找准点。":$"这次{result.Label}没打够劲，下次本鱼陪你找准机会。";
        return new(result.Good?"combat_good":"combat_mostly_missed",text,result.Good?"happy":"sad",.65f,
            $"本地宽松估计：{result.Label}，主人伤害增量{result.Damage}，动态参考伤害{result.Reference:0.#}，门槛{result.Threshold:0.#}，评价{(result.Good?"打得好":"没有打好")}；机制信息：{result.Mechanic}；伤害来源={result.DamageSource}（display_local_candidate为单人显示伤害候选，不能保证排除随从或环境）。这是当前装备基础数据与动作倍率的估计，非精确命中率。按人设简短{(result.Good?"夸奖":"轻轻调侃")}，不编造命中瓶数、弱点或队友归属。");
    }
}







// Conservative optional rolling output: requires an explicit panel reference and owner-attributed source.
public sealed class RollingOutputDirector
{
    private readonly Queue<(double Time,int Total)> samples=new();
    private PlayerDamageCounterSample? last;
    private ulong equipment; private string weapon=""; private double nextAllowed,started=double.NaN;
    public void Reset(){samples.Clear();last=null;equipment=0;weapon="";nextAllowed=0;started=double.NaN;}
    public CombatDamageResult? Observe(PlayerDamageCounterSample? counter,CombatFacts? facts,SaedResponseSettings settings,double now)
    {
        if(!settings.Valid||!settings.RollingOutputEnabled||settings.RollingOutputAttack<=0||counter==null||facts?.Equipment==null||counter.Source!="party"||!double.IsFinite(now)||now<counter.Time||now-counter.Time>.35||now<facts.Time||now-facts.Time>.35)
        {Reset();return null;}
        if(last!=null&&(counter.Slot!=last.Slot||counter.Member!=last.Member||counter.Counter!=last.Counter||counter.Name!=last.Name||counter.Source!=last.Source||counter.Total<last.Total||counter.Time<=last.Time||counter.Time-last.Time>.75||equipment!=facts.Equipment.Object||weapon!=facts.Weapon))Reset();
        if(!double.IsFinite(started))started=now;
        last=counter;equipment=facts.Equipment.Object;weapon=facts.Weapon;
        samples.Enqueue((counter.Time,counter.Total));
        // Keep the oldest sample at/after cutoff; do not include damage older than the window.
        while(samples.Count>1&&now-samples.Peek().Time>settings.RollingOutputSeconds)samples.Dequeue();
        var first=samples.Peek();
        if(now-started<settings.RollingOutputSeconds||now-first.Time<settings.RollingOutputSeconds-.35||now<nextAllowed)return null;
        int damage=counter.Total-first.Total;double threshold=settings.RollingOutputAttack*settings.RollingOutputMultiplier;
        if(damage<threshold)return null;
        nextAllowed=now+settings.RollingOutputCooldown;
        return new(facts.Weapon,"rolling_output","持续输出",damage,settings.RollingOutputAttack,threshold,true,$"滚动窗口{settings.RollingOutputSeconds}秒；用户配置攻击参考{settings.RollingOutputAttack}乘{settings.RollingOutputMultiplier}；不是实时面板读取",now,counter.Source);
    }
}

