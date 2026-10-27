from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'src/WhaleCompanion/CompanionChat.cs';s=p.read_text(encoding='utf-8-sig')
lines=s.splitlines();old=next(line for line in lines if 'public static readonly string SecretPath' in line)
s=s.replace(old,'    public static string SecretPath => Path.Combine(ConfigurationDirectory, "deepseek.dpapi");')
p.write_text(s,encoding='utf-8-sig')
p=root/'src/WhaleCompanion/PlayerDamageCounter.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('    public double Threshold=>','    [System.Text.Json.Serialization.JsonIgnore]\n    public double Threshold=>').replace('    public bool Valid=>','    [System.Text.Json.Serialization.JsonIgnore]\n    public bool Valid=>')
p.write_text(s,encoding='utf-8-sig')
p=root/'src/WhaleCompanion/CombatDamageRules.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('"Switch Axe",[13,14,15]','"Switch Axe",[13,14]')
needle='''        EquipmentDamageProbe.TryRead(m,player,weapon,revision,now,out var equipment);'''
assert s.count(needle)==1;s=s.replace(needle,'''        if(revision!="421810")return new(player,weapon,null,new(),now);
'''+needle)
p.write_text(s,encoding='utf-8')
p=root/'src/WhaleCompanion/Class1.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('actionQuietUntil=now+(saedResponses&&weapon=="ChargeBlade"?1.5:8)','actionQuietUntil=now+(saedResponses?1.5:8)')
old='''LocalLine(reaction.Text);expression.Offer(reaction.Emotion,reaction.Intensity,1,now,3);actionQuietUntil=now+8;'''
assert s.count(old)==1;s=s.replace(old,'''LocalLine(reaction.Text);expression.Offer(reaction.Emotion,reaction.Intensity,1,now,3);actionQuietUntil=now+(saedResponses?2:8);''')
p.write_text(s,encoding='utf-8-sig')
print('Portable DPAPI path and release config cleanup complete; no secret read/decryption.')
