"""Build explicit public RC payload; never read game configuration or private exports."""
from pathlib import Path
import csv,hashlib,io,json,re,subprocess,zipfile,tempfile
from build_clean_plugin_preview import SOURCES
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'dist/WhaleCompanion-P11.5-RC1'
P='nativePC/plugins/CSharp/WhaleCompanion/'
payload={}; files=[]
def put(name,data,target=None,seed=False,keep=False):
    assert name not in payload,name
    payload[name]=data
    if target: files.append(dict(Source=name,Target=target,Hash=sha(data),Mode='seed' if seed else 'managed',KeepOnUninstall=keep))
def sha(data):return hashlib.sha256(data).hexdigest().upper()
def add(source,target,seed=False,keep=False):
    path=ROOT/source
    assert path.is_file() and not path.is_symlink(),source
    put(target,path.read_bytes(),target,seed,keep)
def run(args):subprocess.run(args,cwd=ROOT,check=True)
def build():
    defaults=Path(tempfile.mkdtemp(prefix='RC1-PublicDefaults-',dir=ROOT/'dist'))
    run(['dotnet','run','--project','tools/ResponseChecks','-c','Release','--no-restore','--','--export-public-defaults',str(defaults)])
    editor=ROOT/'dist/RC1-OfflineTools'
    run(['dotnet','publish','tools/OfflineDialogueTool','-c','Release','-r','win-x64','--self-contained','false','--no-restore','-o',str(editor)])
    for name,source in SOURCES.items():add(source,P+name)
    add('release/third-party/LICENSE.SharpPluginLoader.txt',P+'LICENSE.SharpPluginLoader.txt')
    for path in defaults.iterdir():add(str(path.relative_to(ROOT)),P+'config/'+path.name,True,True)
    add('assets/prototype/otomo000.mod3',P+'face-neutral.mod3')
    add('tools/Apply-OfflineDialogue.ps1',P+'Apply-OfflineDialogue.ps1')
    add('tools/Apply-PlayerDialogue.ps1',P+'玩家自定义/Apply-PlayerDialogue.ps1')
    for name in ['OfflineDialogueTool.exe','OfflineDialogueTool.dll','OfflineDialogueTool.deps.json','OfflineDialogueTool.runtimeconfig.json']:add(str((editor/name).relative_to(ROOT)),P+'OfflineTools/'+name)
    for name in ['应用修改.cmd','恢复默认台词.cmd']:add('dist/WhaleCompanion/PlayerDefaults/玩家自定义/'+name,P+'玩家自定义/'+name)
    for name,text in [('台词语言.txt','简体中文\n'),('离线人设.txt','你是陪伴猎人狩猎的大肥鱼。俏皮、关心同伴，台词简短，保留游戏参数标记。\n')]:put(P+'玩家自定义/'+name,text.encode('utf-8'),P+'玩家自定义/'+name,True,True)
    for lang,base,txt in [('chT','dist/WhaleCompanion/PlayerDefaults','随从台词.txt'),('chS','dist/DialogueLanguages','随从台词-简体中文.txt'),('eng','dist/ResponseInterfacePreview','Palico-dialogue-English.txt')]:
        f='ot_auto_'+lang
        for sub,suffix in [('', '.json'),('source/','.gmd'),('generated/','.gmd'),('generated/','.validation.json')]:add(base+'/config/dialogue/'+sub+f+suffix,P+'config/dialogue/'+sub+f+suffix,True,True)
        add(base+'/config/dialogue/generated/'+f+'.gmd','nativePC/common/text/'+f+'.gmd',True)
        add(base+'/config/dialogue/generated/'+f+'.gmd',P+'config/dialogue/defaults/'+f+'.gmd',True,True)
        add(base+'/玩家自定义/'+txt,P+'玩家自定义/'+txt,True,True)
        add(base+'/玩家自定义/'+txt,P+'config/dialogue/defaults/'+txt,True,True)
    model='nativePC/otomo/ot000/mod/otomo000/'
    add('assets/prototype/pot-v1/otomo000.mod3',model+'otomo000.mod3')
    add('assets/prototype/release-appearance/soft-rosy.mrl3',model+'otomo000.mrl3')
    add('assets/prototype/hide/otomo/ot000/mod/otomo000/otomo000_eyelens.mod3',model+'otomo000_eyelens.mod3')
    for part in ['body','helm']:add(f'assets/prototype/hide/otomo/equip/ot001/{part}/mod/ot_{part}001.mod3',f'nativePC/otomo/equip/ot001/{part}/mod/ot_{part}001.mod3')
    for name in ['image_0','image_1','image_2','image_3','image_5','image_6','black','flat_normal','flat_rmt']:add('assets/prototype/textures/'+name+'.tex',model+'whale_'+name+'.tex')
    tints={}
    for name in ['original','soft-rosy','rosy']:
        path='optional/face-tint/'+name+'.mrl3';data=(ROOT/'assets/prototype/release-appearance'/f'{name}.mrl3').read_bytes();put(path,data);tints[name]=dict(Source=path,Hash=sha(data))
    for name in ['Install.ps1','Install.cmd','Uninstall.cmd','README.md']:put(name,(ROOT/'release/package'/name).read_bytes())
    put('docs/FEEDBACK.md',(ROOT/'release/package/FEEDBACK.md').read_bytes())
    catalog=json.loads((ROOT/'src/WhaleCompanion/response-defaults.json').read_text(encoding='utf-8-sig'));stream=io.StringIO();writer=csv.writer(stream);writer.writerow(['Event','Category','Offline reference','AI instruction'])
    for key,value in catalog.items():writer.writerow([key,value['Category'],value['ReferenceText'],value['Prompt']])
    put('docs/trigger-catalog.csv',stream.getvalue().encode('utf-8-sig'))
    put('package-manifest.json',json.dumps(dict(Schema=1,PackageId='P11.5-RC1',Files=files,FaceTints=tints),ensure_ascii=False,indent=2).encode())
    for name,data in payload.items():
        assert not any(x in name.lower() for x in ['.dpapi','saved-memories','memory.md','chat-history','.vrm','.pdb','oo2core','/out/','/research/']),name
        if Path(name).suffix.lower() in ['.json','.md','.txt','.csv','.ps1','.cmd']:
            assert not re.search(rb'\bsk-[A-Za-z0-9_-]{24,}\b',data),name
            assert b'homuraxkl@gmail.com' not in data,name
    put('SHA256SUMS.json',json.dumps({name:sha(data) for name,data in sorted(payload.items())},ensure_ascii=False,indent=2).encode())
    if OUT.exists():assert {str(p.relative_to(OUT)).replace(chr(92),'/') for p in OUT.rglob('*') if p.is_file()} <= set(payload),'Unexpected staging contents'
    for name,data in payload.items():path=OUT/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    archive=OUT.parent/(OUT.name+'.zip')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for name,data in sorted(payload.items()):info=zipfile.ZipInfo(name,(2026,10,27,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,data)
    with zipfile.ZipFile(archive) as z:assert z.testzip() is None and set(z.namelist())==set(payload)
    archive.with_suffix('.zip.sha256').write_text(sha(archive.read_bytes())+'  '+archive.name+'\n')
    print(json.dumps(dict(archive=str(archive),files=len(payload),sha256=sha(archive.read_bytes()),private_configuration_read=False)))
if __name__=='__main__':build()
