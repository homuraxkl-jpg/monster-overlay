"""Exercise upgrade and rollback against a fake game tree, never the live game."""
import hashlib,json,os,shutil,subprocess,uuid,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
package=sys.argv[1] if len(sys.argv)>1 else 'DialogueLanguages'
assert package in ('DialogueLanguages','WeaponStateProbe','WeaponCombatPreview','NativeGuardPreview','ActionConfirmationPreview','SaedDamagePreview','DynamicCombatPreview')
fixture=ROOT/'research/chat'/('language-upgrade-check-'+uuid.uuid4().hex)
project=fixture/'project';stage=project/'dist'/package;game=fixture/'game';native=game/'nativePC'
shutil.copytree(ROOT/'dist'/package,stage)
(project/'tools').mkdir()
script=project/'tools/Install-DialogueLanguages.ps1'
shutil.copyfile(ROOT/'tools/Install-DialogueLanguages.ps1',script)
rows=json.loads((stage/'upgrade-manifest.json').read_text(encoding='utf-8-sig'))
live=Path('D:/steam/steamapps/common/Monster Hunter World/nativePC')
for row in rows:
    if row['ExpectedHash']:
        target=native/row['Target'];target.parent.mkdir(parents=True,exist_ok=True)
        candidates=[live/row['Target']]+[ROOT/'backups'/name/row['Target'] for name in ('dynamic-combat-preview','saed-damage-preview','action-confirmation-preview','native-guard-preview','weapon-combat-preview','weapon-state-probe','dialogue-languages')]
        source=next((p for p in candidates if p.is_file() and hashlib.sha256(p.read_bytes()).hexdigest().upper()==row['ExpectedHash']),None)
        assert source is not None,'Missing expected baseline fixture: '+row['Target']
        shutil.copyfile(source,target)
        assert hashlib.sha256(target.read_bytes()).hexdigest().upper()==row['ExpectedHash']
canary=native/'plugins/CSharp/WhaleCompanion/玩家自定义/随从台词.txt';canary.parent.mkdir(parents=True,exist_ok=True);canary.write_text('玩家的繁中修改，不得覆盖',encoding='utf-8')
def run(restore=False,running=False):
    cmd="$ErrorActionPreference='Stop'; function Get-CimInstance { "+('@{ThreadCount=1}' if running else '@()')+" }; & '"+str(script)+"' -GameRoot '"+str(game)+"' -Package "+package+(' -Restore' if restore else '')
    env=dict(os.environ,PSModulePath=os.environ['SystemRoot']+'/System32/WindowsPowerShell/v1.0/Modules')
    return subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-Command',cmd],capture_output=True,env=env)
assert run(running=True).returncode!=0
assert not (project/'backups'/('dynamic-combat-preview' if package=='DynamicCombatPreview' else 'saed-damage-preview' if package=='SaedDamagePreview' else 'action-confirmation-preview' if package=='ActionConfirmationPreview' else 'native-guard-preview' if package=='NativeGuardPreview' else 'weapon-combat-preview' if package=='WeaponCombatPreview' else 'weapon-state-probe' if package=='WeaponStateProbe' else 'dialogue-languages')/'manifest.json').exists()
r=run();assert r.returncode==0,r.stderr
for row in rows:assert hashlib.sha256((native/row['Target']).read_bytes()).hexdigest().upper()==row['NewHash']
assert canary.read_text(encoding='utf-8')=='玩家的繁中修改，不得覆盖'
r=run(restore=True);assert r.returncode==0,r.stderr
for row in rows:
    if row['Preserve']:continue
    p=native/row['Target']
    if row['ExpectedHash']:assert hashlib.sha256(p.read_bytes()).hexdigest().upper()==row['ExpectedHash']
    else:assert not p.exists()
assert canary.read_text(encoding='utf-8')=='玩家的繁中修改，不得覆盖'
report=dict(passed=True,package=package,staged_files=len(rows),fixture=str(fixture),checked=['running game blocks install','all staged hashes','existing player text retained','upgrade rollback'],game_modified=False)
(ROOT/'research/chat'/('dynamic-combat-upgrade-validation.json' if package=='DynamicCombatPreview' else 'saed-damage-upgrade-validation.json' if package=='SaedDamagePreview' else 'action-confirmation-upgrade-validation.json' if package=='ActionConfirmationPreview' else 'native-guard-upgrade-validation.json' if package=='NativeGuardPreview' else 'weapon-combat-upgrade-validation.json' if package=='WeaponCombatPreview' else 'weapon-state-upgrade-validation.json' if package=='WeaponStateProbe' else 'language-upgrade-validation.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
