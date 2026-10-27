"""Exercise English TXT -> tool -> GMD -> apply/restore in a fake game only."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,uuid
r=Path(__file__).resolve().parents[1];fixture=r/'research/english-dialogue-fixtures'/uuid.uuid4().hex;game=fixture/'game';plugin=game/'nativePC/plugins/CSharp/WhaleCompanion';stage=r/'dist/ResponseInterfacePreview'
shutil.copytree(stage/'config',plugin/'config');shutil.copytree(stage/'OfflineTools',plugin/'OfflineTools');(plugin/'玩家自定义').mkdir()
shutil.copyfile(stage/'玩家自定义/Palico-dialogue-English.txt',plugin/'玩家自定义/Palico-dialogue-English.txt')
(plugin/'玩家自定义/台词语言.txt').write_text('English',encoding='utf-8-sig')
chinese=game/'nativePC/common/text/ot_auto_chS.gmd';chinese.parent.mkdir(parents=True);chinese.write_bytes(b'Chinese untouched canary')
text=plugin/'玩家自定义/Palico-dialogue-English.txt';value=text.read_text(encoding='utf-8-sig');heading='【Entry 001】';value=value.replace(heading+'\n',heading+"\nI'm here. ",1);text.write_text(value,encoding='utf-8-sig')
p=subprocess.run([str(plugin/'OfflineTools/OfflineDialogueTool.exe'),'build',str(plugin)],capture_output=True);assert p.returncode==0,p.stderr
source=plugin/'config/dialogue/generated/ot_auto_eng.gmd';original=plugin/'config/dialogue/source/ot_auto_eng.gmd';assert source.read_bytes()!=original.read_bytes()
env=dict(os.environ,PSModulePath=os.environ['SystemRoot']+'/System32/WindowsPowerShell/v1.0/Modules')
def apply(restore=False):
 command="$ErrorActionPreference='Stop'; function Get-CimInstance { @() }; & '"+str(r/'tools/Apply-OfflineDialogue.ps1')+"' -GameRoot '"+str(game)+"' -Language eng"+(' -Restore' if restore else '')
 return subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-Command',command],capture_output=True,env=env)
p=apply();assert p.returncode==0,p.stderr
installed=game/'nativePC/common/text/ot_auto_eng.gmd';assert installed.read_bytes()==source.read_bytes();assert chinese.read_bytes()==b'Chinese untouched canary'
p=apply(True);assert p.returncode==0,p.stderr;assert not installed.exists();assert chinese.read_bytes()==b'Chinese untouched canary'
print(json.dumps({"passed":True,"real_game_modified":False,"checks":["English TXT edit","CLI build","format validation","English source hash","GMD install hash","Chinese retained","GMD restore"]}))
