"""Normalize only public dialogue defaults; compile an independent GMD fixture. No game/user configuration read."""
from pathlib import Path
import ctypes,json,re,shutil,struct,subprocess,tempfile
R=Path(__file__).resolve().parents[1]
api=ctypes.windll.kernel32.LCMapStringEx
api.argtypes=[ctypes.c_wchar_p,ctypes.c_uint,ctypes.c_wchar_p,ctypes.c_int,ctypes.c_void_p,ctypes.c_int,ctypes.c_void_p,ctypes.c_void_p,ctypes.c_longlong];api.restype=ctypes.c_int
pattern=re.compile(r'<[^<>]*>|\{[^{}]*\}|%(?:%|(?:\d+\$)?[-+ #0]*\d*(?:\.\d+)?(?:hh|ll|[hljztL])?[diuoxXfFeEgGaAcspn])')
def normalize(text):
 def convert(part):
  if not part:return part
  size=api('zh-CN',0x04000000,part,len(part),None,0,None,None,0);assert size>0
  out=ctypes.create_unicode_buffer(size+1);assert api('zh-CN',0x04000000,part,len(part),out,size,None,None,0)>0
  return out.value
 at=0;out=''
 for token in pattern.finditer(text):out+=convert(text[at:token.start()])+token.group();at=token.end()
 return out+convert(text[at:])
def fixture(catalog_path="config/dialogue/ot_auto_chT.json",name="ot_auto_chT.gmd"):
 source=(R/'research/chat/chunks/chunkG0/common/text/ot_auto_chT.gmd').read_bytes();catalog=json.loads((R/catalog_path).read_text(encoding='utf-8-sig'))
 keys,count,keysize,_,namesize=struct.unpack_from('<5I',source,20)
 start=41+namesize+keys*32+(2048 if keys else 0)+keysize
 values=source[start:].split(b'\0');assert len(values)==count+1 and values[-1]==b''
 for row in catalog['entries']:
  assert values[row['index']].decode()==row['original']
  if row['replacement'] is not None:values[row['index']]=normalize(row['replacement']).encode()
 data=bytearray(source[:start]);data.extend(b'\0'.join(values));struct.pack_into('<I',data,32,len(data)-start)
 out=R/'research/chat/script-normalized-candidate';out.mkdir(exist_ok=True);(out/name).write_bytes(data)
def defaults():
 tool=R/'dist/RC1-OfflineTools/OfflineDialogueTool.exe'
 for lang,base,text in [('chT','dist/WhaleCompanion/PlayerDefaults','随从台词.txt'),('chS','dist/DialogueLanguages','随从台词-简体中文.txt')]:
  with tempfile.TemporaryDirectory(prefix='public-script-',dir=R/'dist') as tmp:
   root=Path(tmp);cfg=root/'config/dialogue';player=root/'玩家自定义';(cfg/'source').mkdir(parents=True);player.mkdir()
   file='ot_auto_'+lang;original=R/base
   shutil.copyfile(original/'config/dialogue'/f'{file}.json',cfg/f'{file}.json');shutil.copyfile(original/'config/dialogue/source'/f'{file}.gmd',cfg/'source'/f'{file}.gmd');shutil.copyfile(original/'玩家自定义'/text,player/text);(player/'台词语言.txt').write_text(lang,encoding='utf-8')
   subprocess.run([str(tool),'build',str(root)],check=True)
   assert (player/text).resolve().is_relative_to(root.resolve());(player/text).unlink()
   subprocess.run([str(tool),'export',str(root)],check=True)
   for relative in [f'config/dialogue/{file}.json',f'config/dialogue/generated/{file}.gmd',f'config/dialogue/generated/{file}.validation.json','玩家自定义/'+text]:shutil.copyfile(root/relative,original/relative)
   print('Public defaults normalized:',lang)
if __name__=='__main__':fixture();fixture('research/chat/offline-default-pack/ot_auto_chT.json','default_chT.gmd');defaults()
