"""Integration checks in a dedicated workspace fixture; no game writes or network."""
import hashlib
import json
import os
import shutil
import subprocess
import sys
import uuid
from pathlib import Path
from prepare_palico_lines import parse
from palico_dialogue import build

ROOT=Path(__file__).resolve().parents[1]
TOOL=ROOT/'tools/OfflineDialogueTool/bin/Release/net8.0/OfflineDialogueTool.dll'
fixture=ROOT/'research/chat'/('language-check-'+uuid.uuid4().hex)
plugin=fixture/'nativePC/plugins/CSharp/WhaleCompanion'
config=plugin/'config/dialogue'
player=plugin/'玩家自定义'
checks=0
def check(ok,label):
    global checks
    if not ok: raise AssertionError(label)
    checks+=1
def run(mode,good=True):
    result=subprocess.run(['dotnet',str(TOOL),mode,str(plugin)],capture_output=True)
    check((result.returncode==0)==good,'CLI '+mode+' '+result.stderr.decode('utf-8',errors='replace'))
def language(value):
    (player/'台词语言.txt').write_text(value,encoding='utf-8-sig')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def apply(lang,restore=False,good=True):
    # Stub process query ONLY for this fake GameRoot: the real running game is untouched.
    script=ROOT/'tools/Apply-OfflineDialogue.ps1'
    command="$ErrorActionPreference='Stop'; function Get-CimInstance { @() }; & '"+str(script)+"' -GameRoot '"+str(fixture)+"' -Language "+lang+(' -Restore' if restore else '')
    env=dict(os.environ,PSModulePath=os.environ['SystemRoot']+'/System32/WindowsPowerShell/v1.0/Modules')
    result=subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-Command',command],capture_output=True,env=env)
    check((result.returncode==0)==good,'apply '+lang+' '+result.stderr.decode(errors='replace'))

for d in (config,config/'source',config/'defaults',player):d.mkdir(parents=True,exist_ok=True)
for lang in ('chT','chS'):
    catalog=ROOT/('research/chat/offline-default-pack/ot_auto_chT.json' if lang=='chT' else 'dist/DialogueLanguages/config/dialogue/ot_auto_chS.json')
    shutil.copyfile(catalog,config/catalog.name)
    source=ROOT/f'research/chat/chunks/chunkG0/common/text/ot_auto_{lang}.gmd'
    shutil.copyfile(source,config/'source'/source.name)
    language(lang);run('export')
    text=player/('随从台词.txt' if lang=='chT' else '随从台词-简体中文.txt')
    shutil.copyfile(text,config/'defaults'/text.name)
    run('build')
    cat=json.loads((config/catalog.name).read_text(encoding='utf-8-sig'))
    expected,_=build(source.read_bytes(),cat,source.name)
    check((config/'generated'/source.name).read_bytes()==expected,'independent Python/C# build '+lang)
    _,_,values=parse(expected);_,_,original=parse(source.read_bytes())
    for row in cat['entries']:
        if row['replacement'] is None:check(values[row['index']]==original[row['index']],'retained original '+lang)

tpaths=[config/'ot_auto_chT.json',player/'随从台词.txt',config/'generated/ot_auto_chT.gmd']
tbefore=[sha(p) for p in tpaths]
language('简体中文');text=player/'随从台词-简体中文.txt'
data=text.read_text(encoding='utf-8');data=data.replace('【行动与恢复 · 第001句】\n','【行动与恢复 · 第001句】\n测试简中独立编辑。',1);text.write_text(data,encoding='utf-8-sig')
run('build');check(tbefore==[sha(p) for p in tpaths],'chS cannot change chT')
before=sha(config/'generated/ot_auto_chS.gmd');language('english');run('build',False);check(before==sha(config/'generated/ot_auto_chS.gmd'),'invalid language leaves output')
language('chS');run('restore');check(text.read_bytes()==(config/'defaults'/text.name).read_bytes(),'restore selected language')
check(tbefore==[sha(p) for p in tpaths],'restore cannot change chT')
apply('chS');target=fixture/'nativePC/common/text/ot_auto_chS.gmd';check(target.read_bytes()==(config/'generated'/target.name).read_bytes(),'apply chS')
apply('chS');apply('chS',True);check(not target.exists(),'restore removes only newly added override')
apply('chS');target.write_bytes(b'other mod');apply('chS',good=False);check(target.read_bytes()==b'other mod','foreign edit preserved')
# Fresh upgrade already carries a default chS override, which must be accepted and backed up.
target.write_bytes((ROOT/'dist/DialogueLanguages/config/dialogue/generated/ot_auto_chS.gmd').read_bytes())
state=config/'backups/applied-chS/manifest.json';state.unlink()
apply('chS');apply('chS',True);check(target.read_bytes()==(ROOT/'dist/DialogueLanguages/config/dialogue/generated/ot_auto_chS.gmd').read_bytes(),'upgrade default baseline accepted')
language('chT');t=fixture/'nativePC/common/text/ot_auto_chT.gmd';shutil.copyfile(ROOT/'research/chat/offline-default-pack/ot_auto_chT.gmd',t)
apply('chT');apply('chT',True);check(t.read_bytes()==(ROOT/'research/chat/offline-default-pack/ot_auto_chT.gmd').read_bytes(),'legacy chT backup path')
report=dict(checks=checks,passed=True,fixture=str(fixture),game_modified=False,api_calls=0)
(ROOT/'research/chat/language-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
