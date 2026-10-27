from pathlib import Path

root = Path(__file__).resolve().parents[1]
p = root / 'src/WhaleCompanion/CombatDamageRules.cs'
s = p.read_text(encoding='utf-8-sig')
def replace(a, b):
    global s
    assert s.count(a) == 1, (a[:100], s.count(a))
    s = s.replace(a, b)

replace('("Hammer",94)=>', '("Hammer",94 or 131)=>')
replace('bool mechanismGap;', 'bool mechanismGap;PlayerDamageCounterSample? sealedCounter;double sealedTime;SaedResponseSettings? activeSettings;')
replace('profile=null;mechanismGap=false;', 'profile=null;mechanismGap=false;sealedCounter=null;activeSettings=null;')
replace('''            var next=CombatProfiles.Identify(current.Weapon,current.Set,current.Id,before);
            if(next?.Key==profile.Key)''', '''            if(sealedCounter!=null)return false;
            var next=CombatProfiles.Identify(current.Weapon,current.Set,current.Id,before);
            if(current.Weapon=="ChargeBlade"&&current.Set==3&&current.Id==148){Reset();return false;}
            // SUPER_FINISH can be cancelled into AED MAIN. The eventual branch determines the label.
            if(profile.Key=="cb_saed"&&next?.Key=="cb_aed"&&activeSettings!=null)
            {
                profile=next;reference=activeSettings.DynamicThreshold?CombatProfiles.Estimate(next,before,current.Id):activeSettings.ReferenceDamage;
                window.Reset();window.Start(request?.DamageBaseline??counter,current.Time,new SaedResponseSettings{ReferenceDamage=Math.Clamp(reference,1,100000),PraiseRatio=activeSettings.PraiseRatio,ManualThreshold=activeSettings.ManualThreshold,WindowSeconds=next.Seconds});
                finished=true;return false;
            }
            // Measure the descending slash only: the connecting thrust is not a successful helm breaker.
            if(profile.Key=="ls_helm"&&current.Set==3&&current.Id==91&&!finished&&activeSettings!=null)
            {
                window.Reset();window.Start(request?.Action.Id==91?request.DamageBaseline??counter:counter,current.Time,new SaedResponseSettings{ReferenceDamage=Math.Clamp(reference,1,100000),PraiseRatio=activeSettings.PraiseRatio,ManualThreshold=activeSettings.ManualThreshold,WindowSeconds=3});
                finished=true;
            }
            if(next?.Key==profile.Key)''')
replace('''            if(current.Weapon=="ChargeBlade"&&current.Id==148&&profile.Key=="cb_saed")Reset();
            return false;''', '''            if(profile.Key=="cb_saed"&&current.Set==3&&current.Id is 120 or 122)finished=true;
            if(next?.Key!=profile.Key)
            {
                if(profile.Key=="cb_saed"&&!finished){Reset();return false;}
                // Freeze before the following action's hits; do not award a previous move its combo's damage.
                sealedCounter=counter;sealedTime=current.Time;
                if(counter==null)Reset();
            }
            return false;''')
replace('''        before=facts;profile=rule;start=current.Time;''', '''        sealedCounter=null;activeSettings=settings;
        before=facts;profile=rule;start=current.Time;''')
replace('''        weak=rule.Key=="sa_zsd"''', '''        finished|=rule.Key=="cb_saed"&&current.Id is 120 or 122;
        weak=rule.Key=="sa_zsd"''')
replace('''        var result=window.Observe(counter,now);if(result==null)return null;''', '''        var result=window.Observe(sealedCounter??counter,sealedCounter!=null?sealedTime:now,sealedCounter!=null);if(result==null)return null;''')
replace('''case "phials":gate=before.Value("phials")==0||minPhials<before.Value("phials");''', '''case "phials":gate=(profile.Key!="cb_saed"||finished)&&(before.Value("phials")==0||minPhials<before.Value("phials"));''')
p.write_text(s,encoding='utf-8')
p=root/'src/WhaleCompanion/PlayerDamageCounter.cs'
s=p.read_text(encoding='utf-8-sig')
replace('''Observe(PlayerDamageCounterSample? sample,double now)''','''Observe(PlayerDamageCounterSample? sample,double now,bool complete=false)''')
replace('''        if(now<finish)return null;''','''        if(now<finish&&!complete)return null;''')
p.write_text(s,encoding='utf-8')
print('Applied phase-specific damage windows, CB branch resolution and powered hammer branch.')
