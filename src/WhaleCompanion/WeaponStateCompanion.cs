namespace WhaleCompanion;

// Opt-in experimental state responses. No attack/hit/guard-point inference.
public sealed class WeaponStateCompanion
{
    readonly HornEffectCompanion horn=new();
    private ulong owner,data;private string weapon="";
    private readonly Dictionary<string,(bool Value,int Count,bool? Stable)> flags=new();
    private double last=double.NaN,nextAllowed;
    public void Reset(){horn.Reset();owner=data=0;weapon="";last=double.NaN;nextAllowed=0;flags.Clear();}
    public ActionReaction? Observe(WeaponStateSnapshot sample,double now)
    {
        if(!double.IsFinite(now)){Reset();return null;}
        if(owner!=sample.Owner||data!=sample.Data||weapon!=sample.Weapon||!double.IsFinite(last)||now<last||now-last>1)Reset();
        if(last==now)return null;
        owner=sample.Owner;data=sample.Data;weapon=sample.Weapon;last=now;
        var derived=new Dictionary<string,double>(sample.Fields);
        if(new[]{"red_extract_timer_raw","white_extract_timer_raw","orange_extract_timer_raw"}.All(derived.ContainsKey))
            derived["three_extracts"]=new[]{"red_extract_timer_raw","white_extract_timer_raw","orange_extract_timer_raw"}.All(key=>derived[key]>0)?1:0;
        var rules=new (string Field,string Label)[]{("shield_timer_raw","红盾"),("sword_timer_raw","红剑"),("axe_timer_raw","斧强化"),("full_phials","满瓶"),("power_charge","力量蓄力"),("power_axe","斧强化"),("amp_timer_raw","高出力"),("demon_mode","鬼人化"),("arch_demon_mode","鬼人强化"),("three_extracts","三灯")}.Concat(ItemEffectProbe.Labels.Select(pair=>(pair.Key,pair.Value))).ToArray();
        ActionReaction? result=horn.Observe(sample,now);
        foreach(var (field,label) in rules)
        {
            if(!derived.TryGetValue(field,out var raw)||!double.IsFinite(raw)){flags.Remove(field);continue;}
            bool value=raw>0;
            var previous=flags.TryGetValue(field,out var old)?old:(value,0,(bool?)null);
            var count=previous.Item1==value?previous.Item2+1:1;var stable=previous.Item3;
            if(count>=3&&stable!=value)
            {
                if(stable==false&&value&&now>=nextAllowed&&result==null)
                {
                    bool item=field.StartsWith("item_effect_",StringComparison.Ordinal);
                    var text=field switch { "demon_mode"=>"准备好了呦，要上了呦！", "power_axe"=>"斧头充满能量了呦！", _=>$"{label}准备起来啦，本鱼有在看着你的节奏。" };
                    if(item)text=$"{label}生效啦，本鱼陪你接着打。";
                    result=new("weapon_state_"+field,text,"happy",.5f,$"实验武器状态读取：{sample.Weapon}的{label}由未激活变为激活，连续采样稳定，社区字段语义尚待实机核对。仅评价这项状态，不推断命中、GP、反击、全段伤害或怪物状态。按人设说一句简短回应。"+(item?" 只确认玩家身上的道具相关增益，不确认本人使用、消耗道具或使用动作完成，可能由队友广域或粉尘提供；不带演奏者或使用者主语，不说主人吃药成功。":"")){RequiredStateFields=[field]};
                }
                stable=value;
            }
            flags[field]=(value,count,stable);
        }
        if(result!=null)nextAllowed=now+ResponseTuning.Cooldown("buff",18);
        return result;
    }
}


// World song timer meanings: HunterPie World AbnormalityData.xml (Apache-2.0).
// Timers describe effects on this hunter, not the musician who supplied them.
public sealed class HornEffectCompanion
{
    static readonly Dictionary<string,string> Names=new()
    {
        ["38"]="自我强化",["3C"]="攻击力提升",["40"]="攻击力提升",["44"]="体力上限提升",["48"]="体力上限提升",
        ["4C"]="耐力消耗减少",["50"]="耐力消耗减少",["54"]="风压耐性",["58"]="风压耐性",["5C"]="防御力提升",["60"]="防御力提升",
        ["80"]="体力回复速度提升",["84"]="体力回复速度提升",["88"]="听觉保护",["8C"]="听觉保护",["9C"]="昏厥无效",["A4"]="耐震",
        ["64"]="特殊装备消耗减少",["68"]="特殊装备消耗减少",["90"]="精灵王加护",["94"]="导虫追踪强化",["98"]="环境伤害无效",["A0"]="麻痹无效",["A8"]="泥地行动强化",
        ["AC"]="火耐性提升",["B0"]="火耐性提升",["B4"]="水耐性提升",["B8"]="水耐性提升",["BC"]="雷耐性提升",["C0"]="雷耐性提升",["C4"]="冰耐性提升",["C8"]="冰耐性提升",["CC"]="龙耐性提升",["D0"]="龙耐性提升",
        ["D4"]="属性攻击力提升",["D8"]="属性异常无效",["E4"]="击退无效",["EC"]="全属性耐性提升",["F0"]="会心率提升",["F4"]="状态异常无效",["F8"]="听觉保护",["FC"]="异常状态攻击强化",
        ["10C"]="耐力旋律效果",["110"]="体力回复旋律效果",["114"]="移动回避强化",["118"]="旋律增益"
    };
    readonly Dictionary<string,(double Timer,int Samples,bool? Stable,int Pending,bool Refresh)> states=new();
    ulong owner,data;double last=double.NaN,nextAllowed;
    public void Reset(){states.Clear();owner=data=0;last=double.NaN;nextAllowed=0;}
    public ActionReaction? Observe(WeaponStateSnapshot sample,double now)
    {
        if(sample.Weapon!="HuntingHorn"||!double.IsFinite(now)||sample.Owner==0){Reset();return null;}
        if(owner!=sample.Owner||data!=sample.Data||!double.IsFinite(last)||now<last||now-last>1)Reset();
        if(now==last)return null;
        owner=sample.Owner;data=sample.Data;last=now;
        var changes=new List<string>();var changedFields=new List<string>();bool anyRefresh=false;
        foreach(var (suffix,label) in Names)
        {
            string key="song_effect_"+suffix+"_raw";
            if(!sample.Fields.TryGetValue(key,out var timer)||!double.IsFinite(timer)||timer<0){states.Remove(key);continue;}
            bool active=timer>0;
            if(!states.TryGetValue(key,out var old)){states[key]=(timer,1,null,0,false);continue;}
            int samples=(old.Timer>0)==active?old.Samples+1:1;
            bool? stable=old.Stable;int pending=old.Pending;bool refresh=old.Refresh;
            if(stable==true&&active&&timer-old.Timer>Math.Max(1,old.Timer*.1)){pending=1;refresh=true;}
            else if(pending>0&&active)pending++;
            if(!active){pending=0;refresh=false;}
            bool changed=false;
            if(samples>=3&&stable!=active){changed=stable==false&&active;stable=active;}
            if(pending>=3){changed=true;pending=0;}
            if(changed){changes.Add(label);changedFields.Add(key);anyRefresh|=refresh;refresh=false;}
            states[key]=(timer,samples,stable,pending,refresh);
        }
        if(changes.Count==0||now<nextAllowed)return null;
        nextAllowed=now+ResponseTuning.Cooldown("buff",18);string labels=string.Join("、",changes.Distinct());
        return new(anyRefresh?"horn_effect_refreshed":"horn_effect_active",$"{labels}{(anyRefresh?"续上啦":"生效啦")}，本鱼陪你稳住节奏。","happy",.5f,
            $"玩家身上的旋律效果{labels}计时出现稳定激活或明显续时，已连续三次采样确认。只确认实际增益状态，不从演奏动作或音符队列推断生效；多人时可能来自队友，不宣称主人演奏、指定曲目音符、全队受益或伤害命中。字段映射仍待实机核对。"){RequiredStateFields=changedFields.ToArray()};
    }
}

// Buff facts remain valid only for the same observed weapon/entity and active fields.
public sealed class WeaponStateResponseQueue
{
    readonly Queue<(ActionReaction Reaction,ulong Owner,ulong Data,string Weapon,double Time)> pending=new();
    public void Reset()=>pending.Clear();
    public void Offer(ActionReaction reaction,WeaponStateSnapshot sample,double now)
    {
        if(!double.IsFinite(now))return;
        while(pending.Count>=4)pending.Dequeue();
        pending.Enqueue((reaction,sample.Owner,sample.Data,sample.Weapon,now));
    }
    static bool Active(WeaponStateSnapshot sample,string key)
    {
        if(key=="three_extracts")return new[]{"red_extract_timer_raw","white_extract_timer_raw","orange_extract_timer_raw"}.All(k=>sample.Fields.TryGetValue(k,out var v)&&double.IsFinite(v)&&v>0);
        return sample.Fields.TryGetValue(key,out var value)&&double.IsFinite(value)&&value>0;
    }
    public ActionReaction? Take(WeaponStateSnapshot? sample,double now,bool ready)
    {
        if(sample==null||!double.IsFinite(now)){Reset();return null;}
        while(pending.TryPeek(out var head))
        {
            if(now<head.Time||now-head.Time>12||sample.Owner!=head.Owner||sample.Data!=head.Data||sample.Weapon!=head.Weapon||!head.Reaction.RequiredStateFields.All(key=>Active(sample,key))){pending.Dequeue();continue;}
            if(!ready)return null;
            pending.Dequeue();return head.Reaction;
        }
        return null;
    }
}
