"""Appearance installation rehearsal in an isolated game fixture; no private/game data used."""
from pathlib import Path
import hashlib,json,os,subprocess,uuid,shutil
r=Path(__file__).resolve().parents[1];f=r/'research/appearance-install-fixtures'/uuid.uuid4().hex
native=f/'game/nativePC';model=native/'otomo/ot000/mod/otomo000/otomo000.mod3';material=model.with_suffix('.mrl3');plugin=native/'plugins/CSharp/WhaleCompanion';plugin.mkdir(parents=True);model.parent.mkdir(parents=True)
shutil.copyfile(r/'assets/prototype/pot-v1/otomo000.mod3',model);shutil.copyfile(r/'assets/prototype/material-v1/otomo000.mrl3',material);shutil.copyfile(r/'src/WhaleCompanion/bin/Release/net8.0/WhaleCompanion.dll',plugin/'WhaleCompanion.dll')
(plugin/'config').mkdir();(plugin/'config/appearance.json').write_text('{"ShowHeadPot":true}',encoding='utf8');private=(plugin/'config/appearance.json').read_bytes()
env=dict(os.environ,PSModulePath=os.environ['SystemRoot']+'/System32/WindowsPowerShell/v1.0/Modules')
def run(tint='soft-rosy',restore=False,running=False):
 cmd="$ErrorActionPreference='Stop'; function Get-CimInstance { "+('@{ThreadCount=1}' if running else '@()')+" }; & '"+str(r/'tools/Install-OptionalAppearance.ps1')+"' -GameRoot '"+str(f/'game')+"' -BackupRoot '"+str(f/'backup')+"' -Tint '"+tint+"'"+(' -Restore' if restore else '')
 return subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-Command',cmd],capture_output=True,env=env)
assert run(running=True).returncode!=0
for tint in ['soft-rosy','soft-rosy','original','rosy']:
 result=run(tint);assert result.returncode==0,result.stderr
 assert material.read_bytes()==(r/f'dist/OptionalAppearancePreview/{tint}.mrl3').read_bytes()
 assert model.read_bytes()==(r/'dist/OptionalAppearancePreview/otomo000.mod3').read_bytes()
 assert (plugin/'config/appearance.json').read_bytes()==private
known=material.read_bytes();material.write_bytes(known+b'edit');assert run(restore=True).returncode!=0;assert material.read_bytes()==known+b'edit';material.write_bytes(known)
for i in range(2):
 result=run(restore=True);assert result.returncode==0,result.stderr
 assert model.read_bytes()==(r/'assets/prototype/pot-v1/otomo000.mod3').read_bytes()
 assert material.read_bytes()==(r/'assets/prototype/material-v1/otomo000.mrl3').read_bytes()
assert (plugin/'config/appearance.json').read_bytes()==private
print('PASS appearance isolated install: game-running guard, all 3 tints, repeat install, external-edit refusal, rollback twice, private settings preserved')
