"""Blender resource parser; modifies material constants only, never image or geometry data."""
import bpy,sys,importlib,zlib,copy,struct,hashlib,json
from pathlib import Path
r=Path('D:/homura/codex/MHW');sys.path.insert(0,str(r/'tools/vendor'));import addon_utils
addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
m=importlib.import_module('MHW_Model_Editor-main.addons.MHW_Model_Editor.modules.mrl3.file_mrl3')
p=r/'assets/prototype/material-v1/otomo000.mrl3';original=p.read_bytes();assert hashlib.sha256(original).hexdigest().upper()=='E9716C92C557194F83D7309C9B0D9A08288EF3C9613D77F510CA4DA6468A64F9'
names=['body','clothing','hair','head','TiePen','expression','whale_tears','whale_outline'];hashes={zlib.crc32(n.encode())^0xffffffff:n for n in names}
base=m.readMrl3File(str(p));base.parser(hashes)
def patch(block,template,values):
 found=set()
 for resource in template.resourceDict.values():
  if not resource['name'].startswith('CB') or not resource.get('match'):continue
  offset=resource['value']
  for name,info in resource['prop'].items():
   kind=info[1];count=int(kind.split('[')[1][:-1]) if '[' in kind else 1
   if name in values:
    value=values[name];seq=value if isinstance(value,list) else [value];assert len(seq)==count
    struct.pack_into('<'+('I' if kind in ('bbool','uint') else 'f')*count,block,offset,*seq);found.add(name)
   offset+=4*count
 assert found==set(values),(found,set(values))
report={}
(r/'assets/prototype/appearance-v1').mkdir(parents=True,exist_ok=True)
for preset,tint in [('original',[1,1,1,1]),('soft-rosy',[1,.982,.978,1]),('rosy',[1,.965,.955,1])]:
 out=m.Mrl3File();out.fileHeader=copy.deepcopy(base.fileHeader);out.textureDict={n:i for i,n in enumerate(base.textureList)};out.materialList=[];blocks=[]
 for template in base.materialList:
  entry=copy.deepcopy(template);block=bytearray(original[template.matInfo.blockOffset:template.matInfo.blockOffset+template.matInfo.blockSize])
  if entry.matInfo.materialName=='head':patch(block,template,{'fBaseMapFactor__uiColor':tint})
  blocks.append(bytes(block));out.materialList.append(entry)
 template=next(x for x in base.materialList if x.matInfo.materialName=='body');entry=copy.deepcopy(template);entry.matInfo.materialName='whale_outline';entry.matInfo.materialNameHash=zlib.crc32(b'whale_outline')^0xffffffff
 # Single-sided opaque shell; original material blocks, including double-sided expression cards, stay unchanged.
 entry.matInfo.surfaceCoef=[0,17];block=bytearray(original[template.matInfo.blockOffset:template.matInfo.blockOffset+template.matInfo.blockSize])
 patch(block,template,{'fBaseMapFactor__uiColor':[.012,.009,.02,1],'fSpecular__uiUNorm':0.0,'fMetalic__uiUNorm':0.0,'fRoughness__uiUNorm':1.0})
 blocks.append(bytes(block));out.materialList.append(entry);out.resourceBuffer=b''.join(blocks)
 dest=r/f'assets/prototype/appearance-v1/{preset}.mrl3';m.writeMrl3File(out,str(dest));stable=bytearray(dest.read_bytes());stable[8:16]=original[8:16];dest.write_bytes(stable);data=dest.read_bytes();check=m.readMrl3File(str(dest));check.parser(hashes)
 for old in base.materialList:
  new=next(x for x in check.materialList if x.matInfo.materialName==old.matInfo.materialName)
  if old.matInfo.materialName!='head':assert original[old.matInfo.blockOffset:old.matInfo.blockOffset+old.matInfo.blockSize]==data[new.matInfo.blockOffset:new.matInfo.blockOffset+new.matInfo.blockSize]
  assert old.matInfo.surfaceCoef==new.matInfo.surfaceCoef and old.matInfo.alphaCoef==new.matInfo.alphaCoef and old.matInfo.shaderHash==new.matInfo.shaderHash
 assert check.textureList==base.textureList
 report[preset]={'sha256':hashlib.sha256(data).hexdigest().upper(),'head_base_color_factor':tint,'all_non_head_original_material_blocks_byte_identical':True,'texture_paths_unchanged':True}
(r/'research/face/appearance-v1-materials.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('APPEARANCE_MATERIALS_OK',json.dumps(report))
