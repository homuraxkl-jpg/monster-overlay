"""Explicit, repeatable staging for P11.4; excludes all private player files."""
from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parents[1]
def update(path,old,new):
    content=path.read_text(encoding='utf-8-sig')
    if new in content:return
    assert old in content,(path,old)
    path.write_text(content.replace(old,new),encoding='utf-8-sig' if path.suffix=='.ps1' else 'utf-8')
installer=root/'tools/Install-DialogueLanguages.ps1'
update(installer,"'NativeGuardPreview','ActionConfirmationPreview'","'NativeGuardPreview','ActionConfirmationPreview','SaedDamagePreview'")
update(installer,"$(if($Package -eq 'ActionConfirmationPreview')","$(if($Package -eq 'SaedDamagePreview'){'backups\\saed-damage-preview'}elseif($Package -eq 'ActionConfirmationPreview')")
update(installer,"if($Package -eq 'ActionConfirmationPreview'){'P11.3","if($Package -eq 'SaedDamagePreview'){'P11.4主人累计伤害与超解双结果评价已安装；默认关闭，阈值可调，面板伤害自动估算尚未接入。'}elseif($Package -eq 'ActionConfirmationPreview'){'P11.3")
test=root/'tools/test_dialogue_language_upgrade.py'
update(test,"'NativeGuardPreview','ActionConfirmationPreview'","'NativeGuardPreview','ActionConfirmationPreview','SaedDamagePreview'")
update(test,"('action-confirmation-preview','native-guard-preview'","('saed-damage-preview','action-confirmation-preview','native-guard-preview'")
update(test,"('action-confirmation-preview' if package==","('saed-damage-preview' if package=='SaedDamagePreview' else 'action-confirmation-preview' if package==")
update(test,"('action-confirmation-upgrade-validation.json' if package==","('saed-damage-upgrade-validation.json' if package=='SaedDamagePreview' else 'action-confirmation-upgrade-validation.json' if package==")
stage=root/'dist/SaedDamagePreview';stage.mkdir(exist_ok=True)
files=[root/'src/WhaleCompanion/bin/Release/net8.0/WhaleCompanion.dll',root/'P11-4-SAED-DAMAGE.md',root/'THIRD-PARTY.md']
for src,name in [('LICENSE','LICENSE.MonsterOverlay.txt'),('NOTICE','NOTICE.MonsterOverlay.txt')]:
    shutil.copyfile(root/'tools/vendor/monster-overlay-research'/src,root/name);files.append(root/name)
game=Path('D:/steam/steamapps/common/Monster Hunter World/nativePC')
rows=[]
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
for source in files:
    dest=stage/source.name;shutil.copyfile(source,dest)
    relative='plugins/CSharp/WhaleCompanion/'+source.name;target=game/relative
    rows.append(dict(Source=source.name,Target=relative,NewHash=digest(dest),ExpectedHash=digest(target) if target.exists() else None,Preserve=False))
(stage/'upgrade-manifest.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Staged',len(rows),'explicit application/doc/license files; no configuration or model files.')
