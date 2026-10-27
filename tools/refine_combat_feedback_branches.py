from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'src/WhaleCompanion/CombatDamageRules.cs'
s=p.read_text(encoding='utf-8')
def edit(a,b):
    global s
    assert s.count(a)==1,(a[:80],s.count(a))
    s=s.replace(a,b)
edit('("LongSword",102)=>','("LongSword",86)=>P("ls_foresight","见切反击","Long Sword",[20],3,"foresight_branch"),\n            ("LongSword",102)=>')
edit('current.Id==148){Reset();return false;}','current.Id is 109 or 148){Reset();return false;}')
edit('''            if(next?.Key!=profile.Key)
            {''','''            if(profile.Key=="ls_foresight"&&current.Set==3&&current.Id==69&&transition.Previous.Id==86)finished=true;
            if(next?.Key!=profile.Key)
            {''')
edit('''            case "helm_release":''','''            case "foresight_branch":gate=finished;detail=$"见切直接派生气刃大回旋={finished}，且见切自身伤害达到门槛；不声称已读取精确回避帧";break;
            case "helm_release":''')
p.write_text(s,encoding='utf-8')
# Existing success tests must simulate the committed branch rather than a cancellable preparation.
p=root/'tools/WeaponStateChecks/CombatRulesChecks.cs'
s=p.read_text(encoding='utf-8-sig').replace('Setup("ChargeBlade",119,cb)','Setup("ChargeBlade",120,cb)')
p.write_text(s,encoding='utf-8')
print('Added practical foresight branch gate and native SHIELD_BUFF cancellation.')
