from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'src/WhaleCompanion/CombatDamageRules.cs';s=p.read_text(encoding='utf-8-sig')
def edit(old,new):
    global s
    if new in s:return
    assert s.count(old)==1,(old[:70],s.count(old));s=s.replace(old,new)
edit('    public static CombatProfile? Identify(string weapon,int set,int id,CombatFacts facts)', '''    public static (int Type,int Level)? Shell(int special)=>special switch
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
    public static CombatProfile? Identify(string weapon,int set,int id,CombatFacts facts)''')
edit('        int gauge=(int)(facts.Value("spirit_level")??0);','''        int gauge=(int)(facts.Value("spirit_level")??0);
        if(weapon is "LightBowgun" or "HeavyBowgun"&&id is 68 or 70 or 72 or 73 or 74 or 75 or 76)return Ammo(facts);''')
edit('            ("GunLance",104 or 105 or 158)=>','''            ("GunLance",85 or 86)=>P("gl_sweep","横扫","Gunlance",[12]),
            ("GunLance",104 or 105 or 158)=>''')
edit('            ("HeavyBowgun",98 or 99)=>P("hbg_special","狙击龙弹","Bowgun Ammo",[47,49],5),\n            ("HeavyBowgun",119)=>P("hbg_finish","机关龙弹终结","Bowgun Ammo",[46],3),\n            ("LightBowgun",119)=>P("lbg_finish","射击终结","Bowgun Ammo",[8],3),','''            ("HeavyBowgun",98 or 99) when facts.Value("special_ammo_type_raw")==2=>P("hbg_special","狙击龙弹","Bowgun Ammo",[47,49],5),
            ("HeavyBowgun",88 or 89 or 90 or 91 or 92 or 93) when facts.Value("special_ammo_type_raw")==1=>P("hbg_heart","机关龙弹","Bowgun Ammo",[46],3),
            ("HeavyBowgun",106)=>P("hbg_melee","弩枪近战","Heavy Bowgun",[7]),
            ("LightBowgun",99)=>P("lbg_melee","弩枪近战","Light Bowgun",[7]),''')
edit('public static bool Handles(string weapon,int set,int id)=>Identify(weapon,set,id,new(1,weapon,null,new(),0))!=null;', 'public static bool Handles(string weapon,int set,int id)=>set==3&&WeaponActions.Name(weapon,set,id)!=null&&((weapon is "HeavyBowgun" or "LightBowgun"&&id is 68 or 70 or 72 or 73 or 74 or 75 or 76)||(weapon=="HeavyBowgun"&&id is 88 or 89 or 90 or 91 or 92 or 93 or 98 or 99)||Identify(weapon,set,id,new(1,weapon,null,new(),0))!=null);')
edit('        double expected=stats.Raw*raw/100*1.05*.45+(stats.ElementType is >=1 and <=5?stats.Element:0)*element*.15;', '''        double fixedRaw=profile.Rows.Sum(row=>CombatReference.Motion(profile.Sheet,row)?.Type=="None"?CombatReference.Raw(profile.Sheet,row):0)*profile.Repeats;
        double expected=stats.Raw*(raw-fixedRaw)/100*1.05*.45+stats.Raw*fixedRaw/100+(stats.ElementType is >=1 and <=5?stats.Element:0)*element*.15;
        if(facts.Weapon=="SwitchAxe"&&stats.Special==0)expected*=1.17; // Power phial, conservative sword estimate.''')
edit('''            // Special Ability 1 is a table index; row/type layout is not guessed.
            // Use conservative level inferred from weapon progression only until explicit shell metadata is mapped.
            int level=Math.Clamp((int)Math.Ceiling(stats.Raw/55),1,7);
            double shell=CombatReference.Fixed("Gunlance",profile.Key=="gl_burst"?20:23,level);
            expected=shell*(profile.Key=="gl_burst"?Math.Clamp(facts.Value("shells")??0,0,8):3);''','''            var shellType=Shell(stats.Special);if(shellType==null)return 0;
            var (type,level)=shellType.Value;
            double shell=CombatReference.Fixed("Gunlance",(profile.Key=="gl_burst"?20:23)+type,level);
            double multiplier=profile.Key=="gl_burst"?type switch{0=>1.2,2=>.9,_=>1}:1;
            expected=shell*multiplier*(profile.Key=="gl_burst"?Math.Clamp(facts.Value("shells")??0,0,8):3);''')
edit('double? minGauge,minPhials;bool mechanismGap;','double? minGauge,minPhials,minMagazine;bool mechanismGap;')
edit('if(rule.Gate=="iai_no_drop"&&facts.Value("spirit_level")==null)','if(rule.Gate=="iai_no_drop"&&facts.Value("spirit_level") is not >0)')
edit('"Iai pre-action gauge unknown"','"Iai pre-action gauge unknown/none; no false counter success"')
edit('minPhials=facts.Value("phials");mechanismGap=false;','minPhials=facts.Value("phials");minMagazine=facts.Value("magazine");mechanismGap=false;')
edit('''        if(now-start<=1)
        {''','''        if(profile.Gate=="ammo_spent")
        {
            if(facts.Value("ammo_index_raw")!=before.Value("ammo_index_raw")){Reset();return null;}
            if(facts.Value("magazine") is {} magazine)minMagazine=Math.Min(minMagazine??magazine,magazine);else mechanismGap=true;
        }
        if(profile.Gate=="phials"&&facts.Value("phials") is {} phials)minPhials=Math.Min(minPhials??phials,phials);
        if(now-start<=1)
        {''')
edit('''            case "phials":detail=''', '''            case "ammo_spent":
                if(mechanismGap)return null;
                gate=minMagazine<before.Value("magazine");detail=$"弹种索引{before.Value("ammo_index_raw")}，弹匣确有消耗={gate}，不把装填当命中";break;
            case "phials":detail=''')
p.write_text(s,encoding='utf-8')
print('Refined shell metadata, separate ammo motion values, and resource gates.')
