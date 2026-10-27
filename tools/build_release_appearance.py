"""Release face tint: patch only head RGB constants, no outline material, geometry or texture edits."""
import bpy,sys,importlib,zlib,struct,json,hashlib
from pathlib import Path
r=Path('D:/homura/codex/MHW');sys.path.insert(0,str(r/'tools/vendor'));import addon_utils
addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
m=importlib.import_module('MHW_Model_Editor-main.addons.MHW_Model_Editor.modules.mrl3.file_mrl3')
p=r/'assets/prototype/material-v1/otomo000.mrl3';original=p.read_bytes();assert hashlib.sha256(original).hexdigest().upper()=='E9716C92C557194F83D7309C9B0D9A08288EF3C9613D77F510CA4DA6468A64F9'
names=['body','clothing','hair','head','TiePen','expression','whale_tears'];parsed=m.readMrl3File(str(p));parsed.parser({zlib.crc32(n.encode())^0xffffffff:n for n in names})
head=next(x for x in parsed.materialList if x.matInfo.materialName=='head');offset=None
for resource in head.resourceDict.values():
 if not resource['name'].startswith('CB') or not resource.get('match'):continue
 at=head.matInfo.blockOffset+resource['value']
 for name,info in resource['prop'].items():
  kind=info[1];count=int(kind.split('[')[1][:-1]) if '[' in kind else 1
  if name=='fBaseMapFactor__uiColor':assert count==4;offset=at
  at+=4*count
assert offset is not None
out=r/'assets/prototype/release-appearance';out.mkdir(parents=True,exist_ok=True);report={}
for preset,tint in [('original',[1,1,1,1]),('soft-rosy',[1,.982,.978,1]),('rosy',[1,.965,.955,1])]:
 data=bytearray(original);struct.pack_into('<4f',data,offset,*tint)
 assert all(i in range(offset,offset+16) for i,(a,b) in enumerate(zip(original,data)) if a!=b)
 target=out/(preset+'.mrl3');target.write_bytes(data)
 report[preset]={'sha256':hashlib.sha256(data).hexdigest().upper(),'only_head_base_color_factor_changed':True,'color_factor':tint,'outline_material_count':0,'original_expression_blocks_unchanged':True}
(r/'research/face/release-appearance.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report))
