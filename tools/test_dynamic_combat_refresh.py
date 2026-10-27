"""Exercise same-phase refresh and rollback only in an isolated fake game."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,uuid
root=Path(__file__).resolve().parents[1]
fixture=root/'research/chat'/('combat-refresh-check-'+uuid.uuid4().hex)
project=fixture/'project';game=fixture/'game';native=game/'nativePC';stage=project/'dist/DynamicCombatPreview'
shutil.copytree(root/'dist/DynamicCombatPreview',stage);(project/'tools').mkdir()
for name in ['Install-DialogueLanguages.ps1','Refresh-DynamicCombatPreview.ps1']:shutil.copyfile(root/'tools'/name,project/'tools'/name)
rows=json.loads((stage/'upgrade-manifest.json').read_text(encoding='utf-8-sig'))
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
for row in rows:
    if row['ExpectedHash']:
        source=root/'backups/dynamic-combat-preview'/row['Target'];assert digest(source)==row['ExpectedHash']
        target=native/row['Target'];target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
    (stage/row['Source']).write_bytes(b'prior-candidate-'+row['Source'].encode())
    row['NewHash']=digest(stage/row['Source'])
(stage/'upgrade-manifest.json').write_text(json.dumps(rows),encoding='utf-8')
canary=native/'plugins/CSharp/WhaleCompanion/config/preserve-canary.txt';canary.parent.mkdir(parents=True,exist_ok=True);canary.write_text('player settings',encoding='utf-8')
env=dict(os.environ,PSModulePath=os.environ['SystemRoot']+'/System32/WindowsPowerShell/v1.0/Modules')
def run(refresh=False,restore=False,running=False):
    script=project/'tools'/('Refresh-DynamicCombatPreview.ps1' if refresh else 'Install-DialogueLanguages.ps1')
    command="$ErrorActionPreference='Stop'; function Get-CimInstance { "+('@{ThreadCount=1}' if running else '@()')+" }; & '"+str(script)+"' -GameRoot '"+str(game)+"'"+('' if refresh else ' -Package DynamicCombatPreview')+(' -Restore' if restore else '')
    return subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-Command',command],capture_output=True,env=env)
r=run();assert r.returncode==0,r.stderr
stateFile=project/'backups/dynamic-combat-preview/manifest.json';original=json.loads(stateFile.read_text(encoding='utf-8-sig'))
shutil.copytree(root/'dist/DynamicCombatPreview',stage,dirs_exist_ok=True)
assert run(refresh=True,running=True).returncode!=0
r=run(refresh=True);assert r.returncode==0,r.stderr
updated=json.loads(stateFile.read_text(encoding='utf-8-sig'))
assert {r['Target']:r['OldHash'] for r in original}=={r['Target']:r['OldHash'] for r in updated}
for row in updated:assert digest(native/row['Target'])==row['NewHash']
assert canary.read_text()=='player settings'
r=run(refresh=True);assert r.returncode==0,r.stderr
edited=native/updated[0]['Target'];before=edited.read_bytes();edited.write_bytes(before+b'user-change')
assert run(refresh=True).returncode!=0 and edited.read_bytes()==before+b'user-change';edited.write_bytes(before)
r=run(restore=True);assert r.returncode==0,r.stderr
for row in updated:
    target=native/row['Target']
    assert digest(target)==row['OldHash'] if row['Existed'] else not target.exists()
assert canary.read_text()=='player settings'
report=dict(passed=True,game_modified=False,fixture=str(fixture),checked=['initial install','running-game block','refresh hashes','original baseline retained','idempotent refresh','external edits retained on refusal','rollback after refresh','player config retained'])
(root/'research/chat/dynamic-combat-refresh-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
