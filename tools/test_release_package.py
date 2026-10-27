from pathlib import Path
import subprocess,json,hashlib,tempfile,shutil,os
ROOT=Path(__file__).resolve().parents[1];PKG=ROOT/'dist/WhaleCompanion-P11.5-RC1';P='nativePC/plugins/CSharp/WhaleCompanion/'
manifest=json.loads((PKG/'package-manifest.json').read_text(encoding='utf-8'))
assert len([f for f in manifest['Files'] if f['Target'].startswith('nativePC/otomo/accessory/ot_acc000/mod/')])==8,'Rice replacement incomplete'
assert (PKG/'nativePC/otomo/ot000/mod/otomo000/otomo000.mod3').stat().st_size==13103784,'Expected approved replacement model'
def ps(game,*args,ok=True):
    script="function Get-CimInstance { @() }; & '"+str(PKG/'Install.ps1')+"' -GameRoot '"+str(game)+"' "+' '.join(args)
    result=subprocess.run(['powershell','-NoProfile','-ExecutionPolicy','Bypass','-Command',script],capture_output=True,text=True,encoding='utf-8',errors='replace')
    if (result.returncode==0)!=ok:raise AssertionError(result.stdout+result.stderr)
def read(game,path):return (game/path).read_bytes()
def put(game,path,data):p=game/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
with tempfile.TemporaryDirectory(prefix='whale-release-',dir=ROOT/'dist') as tmp:
    game=Path(tmp)
    for name in ['MonsterHunterWorld.exe','winmm.dll','nativePC/plugins/CSharp/Loader/SharpPluginLoader.Core.dll']:put(game,name,b'fixture')
    original=b'previous-model';model='nativePC/otomo/ot000/mod/otomo000/otomo000.mod3';put(game,model,original)
    private={P+'config/settings.json':b'{"private-canary":true}',P+'config/memory.md':b'private-canary',P+'out/private.txt':b'private-canary','nativePC/common/text/ot_auto_chS.gmd':b'custom-dialogue'}
    for path,data in private.items():put(game,path,data)
    ps(game)
    assert read(game,model)==read(PKG,model)
    for path,data in private.items():assert read(game,path)==data,path
    ps(game);ps(game,'-FaceTint','rosy')
    assert read(game,'nativePC/otomo/ot000/mod/otomo000/otomo000.mrl3')==read(PKG,'optional/face-tint/rosy.mrl3')
    dll=P+'WhaleCompanion.dll';expected=read(game,dll);put(game,dll,b'external-edit');ps(game,ok=False);ps(game,'-Uninstall',ok=False);assert read(game,dll)==b'external-edit';put(game,dll,expected)
    ps(game,'-Uninstall');assert read(game,model)==original
    for path,data in private.items():assert read(game,path)==data,path
    ps(game,'-Uninstall')
print('PASS release install/reinstall/tint/modified-file refusal/uninstall; private configuration and edited dialogue preserved; isolated fixture only')


with tempfile.TemporaryDirectory(prefix='whale-fresh-',dir=ROOT/'dist') as tmp:
    game=Path(tmp)
    for name in ['MonsterHunterWorld.exe','winmm.dll','nativePC/plugins/CSharp/Loader/SharpPluginLoader.Core.dll']:put(game,name,b'fixture')
    ps(game)
    plugin=game/P
    env=dict(os.environ,PSModulePath=os.environ['SystemRoot']+'/System32/WindowsPowerShell/v1.0/Modules')
    for lang,label in [('chS','简体中文'),('chT','繁體中文'),('eng','English')]:
        put(game,P+'玩家自定义/台词语言.txt',label.encode('utf-8-sig'))
        command="function Get-CimInstance { @() }; & '"+str(plugin/'玩家自定义/Apply-PlayerDialogue.ps1')+"'"
        result=subprocess.run(['powershell','-NoProfile','-ExecutionPolicy','Bypass','-Command',command],env=env,capture_output=True)
        assert result.returncode==0,result.stderr
        assert read(game,'nativePC/common/text/ot_auto_'+lang+'.gmd')==read(plugin,'config/dialogue/generated/ot_auto_'+lang+'.gmd')
    ps(game,'-Uninstall')
print('PASS fresh defaults + all three packaged TXT/editor/apply entrypoints + uninstall; no real game modified')
