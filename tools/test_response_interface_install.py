"""Install/rollback rehearsal in an isolated fake game; no real game changes."""
from pathlib import Path
import hashlib,json,os,subprocess,uuid
ROOT=Path(__file__).resolve().parents[1]
fixture=ROOT/'research'/'response-install-fixtures'/uuid.uuid4().hex
native=fixture/'game/nativePC';backup=fixture/'backup';plugin=native/'plugins/CSharp/WhaleCompanion'
(plugin/'config/dialogue').mkdir(parents=True)
canaries={"config/settings.json":b"private settings canary","config/deepseek.dpapi":b"encrypted canary","config/saved-memories.json":b"memory canary","config/dialogue/ot_auto_chS.json":b"Chinese canary","config/dialogue/ot_auto_eng.json":b"custom English canary","Apply-PlayerDialogue.ps1":b"old script canary"}
for name,data in canaries.items():p=plugin/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
script=ROOT/'tools/Install-ResponseInterfacePreview.ps1'
env=dict(os.environ,PSModulePath=os.environ['SystemRoot']+'/System32/WindowsPowerShell/v1.0/Modules')
def run(restore=False,running=False):
 cmd="$ErrorActionPreference='Stop'; function Get-CimInstance { "+('@{ThreadCount=1}' if running else '@()')+" }; & '"+str(script)+"' -GameRoot '"+str(fixture/'game')+"' -BackupRoot '"+str(backup)+"'"+(' -Restore' if restore else '')
 return subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-Command',cmd],capture_output=True,env=env)
assert run(running=True).returncode!=0
r=run();assert r.returncode==0,r.stderr
for name,data in canaries.items():
 if name!='Apply-PlayerDialogue.ps1':assert (plugin/name).read_bytes()==data
rows=json.loads((backup/'manifest.json').read_text(encoding='utf-8-sig'))
for row in rows:assert hashlib.sha256((native/row['Target']).read_bytes()).hexdigest().upper()==row['InstalledHash']
assert run().returncode==0
edited=plugin/'Apply-PlayerDialogue.ps1';data=edited.read_bytes();edited.write_bytes(data+b'edit')
assert run(restore=True).returncode!=0
edited.write_bytes(data)
r=run(restore=True);assert r.returncode==0,r.stderr
for name,data in canaries.items():assert (plugin/name).read_bytes()==data
assert not (plugin/'OfflineTools/OfflineDialogueTool.exe').exists()
assert run(restore=True).returncode==0
print(json.dumps({"passed":True,"real_game_modified":False,"checks":["running-game block","install hashes","private configuration preserved","Chinese preserved","existing English preserved","repeat install","external edit blocks rollback","rollback","repeat rollback"]}))
