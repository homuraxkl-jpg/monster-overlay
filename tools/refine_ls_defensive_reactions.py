from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'src/WhaleCompanion/CombatDamageRules.cs';s=p.read_text(encoding='utf-8-sig')
def edit(a,b):
    global s
    assert s.count(a)==1,(a[:90],s.count(a))
    s=s.replace(a,b)
edit('''        var fields=state?.Fields??new Dictionary<string,double>();''','''        var fields=state?.Fields??new Dictionary<string,double>();
        // Same health component as SPL Player.Health, read through the guarded OS reader.
        if(m.Pointer(player+0x7630,out var health)&&LocalProcessMemory.IsAddress(health,0x68)&&m.Int32(health+0x64,out var hpBits)&&m.Int32(health+0x60,out var maxBits))
        {
            float hp=BitConverter.Int32BitsToSingle(hpBits),maximum=BitConverter.Int32BitsToSingle(maxBits);
            if(float.IsFinite(hp)&&float.IsFinite(maximum)&&maximum is >0 and <=100000&&hp>=0&&hp<=maximum&&m.Pointer(player+0x7630,out var afterHealth)&&afterHealth==health)fields["health"]=hp;
        }''')
edit('''            ("LongSword",101)=>P("ls_iai_small","居合拔刀斩","Long Sword",[25,26]),
''','')
edit('''string DamageSource="party");''','''string DamageSource="party",string Outcome="damage_verdict");''')
edit('''SaedResponseSettings? activeSettings;''','''SaedResponseSettings? activeSettings;double? minHealth;bool healthGap;''')
edit('''sealedCounter=null;activeSettings=settings;''','''sealedCounter=null;activeSettings=settings;minHealth=facts.Value("health");healthGap=minHealth==null;''')
edit('''        if(profile.Gate=="ammo_spent")''','''        if(profile.Gate=="iai_no_drop"&&sealedCounter==null)
        {
            if(facts.Value("health") is {} health)minHealth=Math.Min(minHealth??health,health);else healthGap=true;
        }
        if(profile.Gate=="ammo_spent")''')
edit('''        return new(before.Weapon,profile.Key,profile.Label,result.Damage,reference,result.Threshold,result.Good&&gate,detail,now,counter?.Source??"unknown");''','''        bool good=result.Good&&gate;string outcome="damage_verdict";
        if(profile.Key=="ls_foresight"&&!good)return null; // A defensive reposition is not a failure worth mocking.
        if(profile.Key=="ls_iai"&&!good)
        {
            if(result.Damage!=0||!gate||healthGap||before.Value("health") is not {} preHealth||minHealth<preHealth)return null;
            outcome="iai_safe_exit";
            detail+=$"；未观察到伤害，HP出招前{preHealth}、窗口最低{minHealth}。仅符合安全脱身启发式，没有确认敌方攻击或无敌/霸体。";
        }
        return new(before.Weapon,profile.Key,profile.Label,result.Damage,reference,result.Threshold,good,detail,now,counter?.Source??"unknown",outcome);''')
edit('''        string text=result.Good?''','''        if(result.Outcome=="iai_safe_exit")return new("iai_safe_exit","这一刀脱身啦，站稳再接下一招。","relaxed",.45f,
            $"大居合安全脱身候选：未观察到对怪伤害，刃色未降、HP采样未减少。{result.Mechanic} 按人设轻松陪伴一句，不贬低空刀，不说反击命中或大成功，也不声称确定躲过敌人攻击、无敌或霸体。空放也可能符合这个启发式。");
        string text=result.Good?''')
p.write_text(s,encoding='utf-8')
p=root/'src/WhaleCompanion/WeaponActions.cs';s=p.read_text(encoding='utf-8-sig')
edit('''            ("Hammer",94)=>''','''            ("Hammer",95)=>("hammer_spin","大锤转圈圈",true,3,0),
            ("Hammer",94)=>''')
p.write_text(s,encoding='utf-8')
p=root/'src/WhaleCompanion/ActionCompanion.cs';s=p.read_text(encoding='utf-8-sig')
edit('''"iai_slash" => alternate ? "出刀啦，接下来也别急。" : "我在旁边看着呢，照自己的节奏来。",''','''"iai_slash" => alternate ? "回气准备好，慢慢接下一刀。" : "先把节奏续上，本鱼陪你等下一刀。",''')
edit('''        var context =''','''        if(kind=="hammer_spin")text=alternate?"转圈圈——本鱼也要转晕啦！":"转圈圈，转圈圈！本鱼给你数着呢！";
        var context =''')
edit('''        return new(kind, text,''','''        if(kind=="iai_slash")context+=" 小居合的正式名称是居合拔刀斩，常用于回气准备；这不是大居合反击结果。只说衔接或回气准备，不说反击成功、规避成功、大成功，不因命中就夸耀，也不宣称已验证回气buff。";
        if(kind=="hammer_spin")context+=" 玩家正在大锤回转动作，请跟着可爱地说转圈圈；不评价命中或伤害。";
        return new(kind, text,''')
p.write_text(s,encoding='utf-8')
print('Small Iai is preparation chatter; defensive failures silent; large Iai safe-exit heuristic; hammer spin chatter.')
