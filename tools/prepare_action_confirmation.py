"""Stage only the explicit application files; never copy user configuration."""
from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parents[1]
def edit(path,old,new):
    text=path.read_text(encoding='utf-8-sig')
    assert old in text,(path,old)
    path.write_text(text.replace(old,new),encoding='utf-8-sig' if path.suffix=='.ps1' else 'utf-8')
installer=root/'tools/Install-DialogueLanguages.ps1'
edit(installer,"'WeaponCombatPreview','NativeGuardPreview'","'WeaponCombatPreview','NativeGuardPreview','ActionConfirmationPreview'")
edit(installer,"$(if($Package -eq 'NativeGuardPreview')","$(if($Package -eq 'ActionConfirmationPreview'){'backups\\action-confirmation-preview'}elseif($Package -eq 'NativeGuardPreview')")
edit(installer,"if($Package -eq 'NativeGuardPreview'){'P11.2","if($Package -eq 'ActionConfirmationPreview'){'P11.3实际动作复核与防御反作用观察已安装；默认关闭，精确GP与单次伤害归属仍未知。'}elseif($Package -eq 'NativeGuardPreview'){'P11.2")
test=root/'tools/test_dialogue_language_upgrade.py'
edit(test,"'WeaponCombatPreview','NativeGuardPreview'","'WeaponCombatPreview','NativeGuardPreview','ActionConfirmationPreview'")
edit(test,"('native-guard-preview','weapon-combat-preview'","('action-confirmation-preview','native-guard-preview','weapon-combat-preview'")
edit(test,"('native-guard-preview' if package==","('action-confirmation-preview' if package=='ActionConfirmationPreview' else 'native-guard-preview' if package==")
edit(test,"('native-guard-upgrade-validation.json' if package==","('action-confirmation-upgrade-validation.json' if package=='ActionConfirmationPreview' else 'native-guard-upgrade-validation.json' if package==")
edit(root/'src/WhaleCompanion/Class1.cs','P11.2 experimental native Offensive Guard result; GP unknown; damage observation OFF by default','P11.3 actual-action confirmation and guard recoil candidates; precise GP/source unknown; experimental features OFF by default')
stage=root/'dist/ActionConfirmationPreview';stage.mkdir(exist_ok=True)
game=Path('D:/steam/steamapps/common/Monster Hunter World/nativePC')
rows=[]
for source in [root/'P11-3-ACTION-CONFIRMATION.md',root/'src/WhaleCompanion/bin/Release/net8.0/WhaleCompanion.dll']:
    dest=stage/source.name;shutil.copyfile(source,dest)
    relative='plugins/CSharp/WhaleCompanion/'+source.name
    target=game/relative
    digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
    rows.append(dict(Source=source.name,Target=relative,NewHash=digest(dest),ExpectedHash=digest(target) if target.exists() else None,Preserve=False))
(stage/'upgrade-manifest.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Prepared installer, tests and explicit stage; rebuild DLL then refresh its staged hash.')
