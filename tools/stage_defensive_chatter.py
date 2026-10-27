from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parents[1]
p=root/'tools/WeaponStateChecks/DefensiveReactionChecks.cs';s=p.read_text(encoding='utf-8')
needle='''        var memory=new MemoryFixture();'''
assert s.count(needle)==1
s=s.replace(needle,'''        chatter=new(){IncludeAllWeapons=true};chatter.Observe(new("Hammer",3,126,0,owner,true),0);
        check(chatter.Observe(new("Hammer",3,95,1,owner,true),1)?.Kind=="hammer_spin","recent power-charge chatter does not swallow spin companion line");
'''+needle)
p.write_text(s,encoding='utf-8')
for name in ['P11-5-DYNAMIC-COMBAT.md','README.md','PROJECT-STATUS.md']:
    p=root/name;s=p.read_text(encoding='utf-8-sig').replace('241项','242项');p.write_text(s,encoding='utf-8')
stage=root/'dist/DynamicCombatPreview'
for name,source in [('WhaleCompanion.dll',root/'src/WhaleCompanion/bin/Release/net8.0/WhaleCompanion.dll'),('P11-5-DYNAMIC-COMBAT.md',root/'P11-5-DYNAMIC-COMBAT.md')]:shutil.copyfile(source,stage/name)
p=stage/'upgrade-manifest.json';rows=json.loads(p.read_text(encoding='utf-8-sig'))
for row in rows:row['NewHash']=hashlib.sha256((stage/row['Source']).read_bytes()).hexdigest().upper()
p.write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print('Staged defensive chatter revision 3, preserving rollback hashes.')
