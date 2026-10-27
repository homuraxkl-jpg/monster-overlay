from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'src/WhaleCompanion/CombatDamageRules.cs';s=p.read_text(encoding='utf-8-sig')
def edit(old,new):
    global s
    if new in s:return
    assert s.count(old)==1,(old[:60],s.count(old));s=s.replace(old,new)
edit('''        if(!m.Pointer(player+0x76B0,out var after)||after!=current||!m.Int32(root+0x1D0C,out var afterRow)||afterRow!=row)return false;
        sample=new''','''        if(!m.Pointer(player+0x76B0,out var after)||after!=current||!m.Int32(root+0x1D0C,out var afterRow)||afterRow!=row)return false;
        ulong finalRoot=player;foreach(uint offset in new uint[]{0x98,0x10,0x70,0x18,0x550})
        {if(!LocalProcessMemory.IsAddress(finalRoot,offset+8)||!m.Pointer(finalRoot+offset,out finalRoot))return false;}
        if(finalRoot!=root||!m.Int32(current+0x9F8,out var finalType)||finalType!=type)return false;
        sample=new''')
edit('''        if(facts.Weapon=="SwitchAxe"&&stats.Special==0)expected*=1.17; // Power phial, conservative sword estimate.
''','')
edit('''        if(profile.Key=="gl_burst"||profile.Key=="gl_wyvern")''','''        if(profile.Key is "sa_zsd" or "sa_ed"&&stats.Special==0)expected*=1.17;
        if(profile.Key=="gl_burst"||profile.Key=="gl_wyvern")''')
edit('public string Status=>window.Status;','public string Status=>profile==null?window.Status:$"{profile.Label}: ref {reference:0.#}, gate {profile.Gate}; {window.Status}";')
edit('double? minGauge,minPhials,minMagazine;','double? minGauge,minPhials,minMagazine,minShells;')
edit('finished|=current.Id is 125 or 135 or 136 or 137||profile.Key=="ls_helm"&&current.Id is 91 or 92;','finished|=profile.Key=="sa_zsd"&&current.Id==125||profile.Key=="gs_tcs"&&current.Id is 135 or 136 or 137||profile.Key=="ls_helm"&&current.Id is 91 or 92||profile.Key=="sns_rush"&&current.Id is 133 or 134;')
edit('if(facts.Owner!=current.Owner||facts.Weapon!=current.Weapon||current.Time-facts.Time is <0 or >.75)return false;','if(facts.Owner!=current.Owner||facts.Weapon!=current.Weapon||!double.IsFinite(facts.Time)||current.Time-facts.Time is <0 or >.75)return false;')
edit('minMagazine=facts.Value("magazine");mechanismGap=false;','minMagazine=facts.Value("magazine");minShells=facts.Value("shells");mechanismGap=false;')
edit('finished=current.Id is 125 or 135 or 136 or 137||rule.Key=="ls_helm"&&current.Id is 91 or 92;','finished=rule.Key=="sa_zsd"&&current.Id==125||rule.Key=="gs_tcs"&&current.Id is 135 or 136 or 137||rule.Key=="ls_helm"&&current.Id is 91 or 92||rule.Key=="sns_rush"&&current.Id is 133 or 134;')
edit('''        if(facts.Owner!=before.Owner||facts.Weapon!=before.Weapon||facts.Equipment?.Stats.Row!=before.Equipment?.Stats.Row''','''        if(!double.IsFinite(facts.Time)||now<facts.Time||now-facts.Time>.35||facts.Owner!=before.Owner||facts.Weapon!=before.Weapon||facts.Equipment?.Stats.Row!=before.Equipment?.Stats.Row''')
edit('''        if(profile.Gate=="phials"&&facts.Value("phials") is {} phials)minPhials=Math.Min(minPhials??phials,phials);''','''        if(profile.Gate=="phials"&&facts.Value("phials") is {} phials)minPhials=Math.Min(minPhials??phials,phials);
        if(profile.Gate=="shells"&&facts.Value("shells") is {} shells)minShells=Math.Min(minShells??shells,shells);''')
edit('gate=just&&lastJust;detail=','gate=just&&lastJust&&finished;detail=')
edit('case "phials":detail=$"出招前装瓶','case "phials":gate=before.Value("phials")==0||minPhials<before.Value("phials");detail=$"出招前装瓶')
edit('case "shells":detail=$"出招前炮弹','case "shells":gate=before.Value("shells")>0&&minShells<before.Value("shells");detail=$"出招前炮弹')
p.write_text(s,encoding='utf-8')
# More state events use actual timers, never just the performance/reload animation.
p=root/'src/WhaleCompanion/WeaponStateCompanion.cs';s=p.read_text(encoding='utf-8-sig')
old='''        var rules=new (string Field,string Label)[]{'''
new='''        var derived=new Dictionary<string,double>(sample.Fields);
        if(new[]{"red_extract_timer_raw","white_extract_timer_raw","orange_extract_timer_raw"}.All(derived.ContainsKey))
            derived["three_extracts"]=new[]{"red_extract_timer_raw","white_extract_timer_raw","orange_extract_timer_raw"}.All(key=>derived[key]>0)?1:0;
        var rules=new (string Field,string Label)[]{'''
assert old in s;s=s.replace(old,new)
s=s.replace('("arch_demon_mode","鬼人强化")};','("arch_demon_mode","鬼人强化"),("three_extracts","三灯")}.Concat(derived.Keys.Where(key=>key.StartsWith("song_effect_",StringComparison.Ordinal)).Select(key=>(key,"旋律增益"))).ToArray();')
s=s.replace('sample.Fields.TryGetValue(field,out var raw)','derived.TryGetValue(field,out var raw)')
p.write_text(s,encoding='utf-8')
print('Finalized counter, completion, resource and state-effect gates.')
