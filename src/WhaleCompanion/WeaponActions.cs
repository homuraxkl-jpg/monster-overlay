namespace WhaleCompanion;

public sealed record WeaponActionTag(string Key,string Label,string RawName,bool Attack,double Window=2,double Delay=0);
public static class WeaponActions
{
    public static bool Continues(string weapon,int previous,int next)=>(weapon,previous,next) switch
    {
        ("ChargeBlade",119,120) or ("ChargeBlade",117,118)=>true,
        ("LongSword",87,89) or ("LongSword",89,90) or ("LongSword",90,91) or ("LongSword",91,92)=>true,
        ("SwitchAxe",124 or 134,125 or 126)=>true,
        _=>false
    };

    private static readonly Lazy<Dictionary<(string,int),string>> Names=new(()=>
    {
        using var stream=typeof(WeaponActions).Assembly.GetManifestResourceStream("WhaleCompanion.weapon-actions.tsv")??throw new InvalidDataException("Missing action catalog");
        using var reader=new StreamReader(stream);var map=new Dictionary<(string,int),string>();
        while(reader.ReadLine() is {} line){var parts=line.Split('\t');if(parts.Length==3)map.Add((parts[0],int.Parse(parts[1])),parts[2]);}
        return map;
    });
    public static string? Name(string weapon,int set,int id)=>set==3&&Names.Value.TryGetValue((weapon,id),out var value)?value:null;
    public static bool AllowsAlongsideDamage(string weapon,int set,int id)=>weapon=="HuntingHorn"&&set==3&&id==74;
    public static WeaponActionTag? Identify(string weapon,int set,int id)
    {
        var raw=Name(weapon,set,id);if(raw==null)return null;
        // Ordinary ammo firing is intentionally silent, including generic SHOT/FINISH fallbacks.
        if(weapon is "LightBowgun" or "HeavyBowgun"&&id is 68 or 70 or 72 or 73 or 74 or 75 or 76)return null;
        (string Key,string Label,bool Attack,double Window,double Delay)? rule=(weapon,id) switch
        {
            ("ChargeBlade",119 or 120 or 122)=>("cb_super_discharge","超高出力属性解放斩",true,3,1.5),
            ("ChargeBlade",117 or 118 or 121)=>("cb_discharge","高出力属性解放斩",true,2,1),
            ("ChargeBlade",109)=>("cb_shield_charge","盾强化动作",false,1,0),
            ("ChargeBlade",107)=>("cb_phial_reload","装瓶动作",false,1,0),
            ("ChargeBlade",148)=>("cb_axe_charge","斧强化衍生动作",true,2,0),
            ("Hammer",81)=>("hammer_big_bang_finish","敲打连段终结",true,2,0),
            ("Hammer",95)=>("hammer_spin","大锤转圈圈",true,3,0),
            ("Hammer",94)=>("hammer_charge_3","蓄力攻击",true,2,0),
            ("Hammer",126)=>("hammer_power_charge","力量蓄力动作",false,1,0),
            ("Hammer",115)=>("hammer_slide_spin","滑坡回转攻击",true,3,0),
            ("SwitchAxe",124 or 134)=>("sa_zero_discharge","零距离解放动作",true,5,1),
            ("SwitchAxe",125 or 126)=>("sa_zero_finish","零距离解放终结",true,2,1),
            ("SwitchAxe",121 or 122)=>("sa_discharge_finish","属性解放终结",true,2,1),
            ("SwitchAxe",135 or 136)=>("sa_axe_prepare","斧强化准备",false,2,0),
            ("HeavyBowgun",118)=>("scope_prepare","会心镜瞄准准备",false,2,0),
            ("Lance",76)=>("lance_counter_prepare","反击架势准备",false,2,0),
            ("HuntingHorn",74)=>("horn_spin","响音攻击",true,3,0),
            ("LongSword",101)=>("iai_slash","居合拔刀斩",true,1.5,0),
            ("LongSword",102)=>("iai_spirit_slash","居合拔刀气刃斩",true,2,1),
            ("LongSword",87 or 89 or 90 or 91 or 92)=>("helm_breaker","气刃兜割阶段",true,2,1.5),
            _=>null
        };
        if(rule is {} r)return new(r.Key,r.Label,raw,r.Attack,r.Window,r.Delay);
        // Other weapons get named action coverage, not guessed persistent gauges or success.
        if(weapon=="LongSword")return null; // Preserve existing LS chatter policy.
        if(raw.Contains("RELOAD",StringComparison.Ordinal)&&!raw.Contains("FAIL",StringComparison.Ordinal))return new("reload","装填动作",raw,false);
        if(raw.Contains("GUARD",StringComparison.Ordinal)&&raw.Contains("START",StringComparison.Ordinal))return new("guard_start","防御架势",raw,false);
        if(raw.Contains("JUST",StringComparison.Ordinal))return new("timed_combo","节奏连段动作",raw,true);
        if(raw.Contains("FINISH",StringComparison.Ordinal)&&!raw.Contains("RIDE",StringComparison.Ordinal))return new("combo_finish","连段终结动作",raw,true,3,weapon is "LightBowgun" or "HeavyBowgun"?3:0);
        if(raw.Contains("SHOT",StringComparison.Ordinal)&&!raw.Contains("SLINGER",StringComparison.Ordinal)&&!raw.Contains("CLAW",StringComparison.Ordinal))return new("weapon_shot","射击动作",raw,true,2,3);
        if(raw.Contains("CHARGE",StringComparison.Ordinal)&&!raw.Contains("SLINGER",StringComparison.Ordinal))return new("charge_action","蓄力相关动作",raw,false);
        return null;
    }
}

