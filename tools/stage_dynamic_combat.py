"""Stage only application/docs; never stage credentials, player config or logs."""
from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parents[1]
def update(path,old,new):
    content=path.read_text(encoding='utf-8-sig')
    if new in content:return
    assert old in content,(path,old)
    path.write_text(content.replace(old,new),encoding='utf-8-sig' if path.suffix=='.ps1' else 'utf-8')
installer=root/'tools/Install-DialogueLanguages.ps1'
update(installer,"'ActionConfirmationPreview','SaedDamagePreview'","'ActionConfirmationPreview','SaedDamagePreview','DynamicCombatPreview'")
update(installer,"$(if($Package -eq 'SaedDamagePreview')","$(if($Package -eq 'DynamicCombatPreview'){'backups\\dynamic-combat-preview'}elseif($Package -eq 'SaedDamagePreview')")
update(installer,"if($Package -eq 'SaedDamagePreview'){'P11.4","if($Package -eq 'DynamicCombatPreview'){'P11.5动态装备/招式门槛与关键资源判据已安装；默认关闭，待集中实机验证。'}elseif($Package -eq 'SaedDamagePreview'){'P11.4")
test=root/'tools/test_dialogue_language_upgrade.py'
update(test,"'ActionConfirmationPreview','SaedDamagePreview'","'ActionConfirmationPreview','SaedDamagePreview','DynamicCombatPreview'")
update(test,"('saed-damage-preview','action-confirmation-preview'","('dynamic-combat-preview','saed-damage-preview','action-confirmation-preview'")
update(test,"('saed-damage-preview' if package==","('dynamic-combat-preview' if package=='DynamicCombatPreview' else 'saed-damage-preview' if package==")
update(test,"('saed-damage-upgrade-validation.json' if package==","('dynamic-combat-upgrade-validation.json' if package=='DynamicCombatPreview' else 'saed-damage-upgrade-validation.json' if package==")
stage=root/'dist/DynamicCombatPreview';stage.mkdir(exist_ok=True)
files=[root/'src/WhaleCompanion/bin/Release/net8.0/WhaleCompanion.dll',root/'P11-5-DYNAMIC-COMBAT.md',root/'THIRD-PARTY.md']
game=Path('D:/steam/steamapps/common/Monster Hunter World/nativePC');rows=[]
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
for source in files:
    dest=stage/source.name;shutil.copyfile(source,dest)
    relative='plugins/CSharp/WhaleCompanion/'+source.name;target=game/relative
    rows.append(dict(Source=source.name,Target=relative,NewHash=digest(dest),ExpectedHash=digest(target) if target.exists() else None,Preserve=False))
(stage/'upgrade-manifest.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Staged',len(rows),'explicit application/doc files; no configuration/model/key files.')
