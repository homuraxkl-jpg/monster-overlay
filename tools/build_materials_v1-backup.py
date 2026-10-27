import bpy,sys,importlib,json,struct,copy,zlib
from pathlib import Path
root=Path('D:/homura/codex/MHW');p=root/'tools/vendor/MHW_Model_Editor-main';sys.path.insert(0,str(p.parent))
import addon_utils
addon_utils.enable(p.name,default_set=True,persistent=False)
mod=importlib.import_module(p.name+'.addons.MHW_Model_Editor.modules.mrl3.file_mrl3')
base=mod.readMrl3File(str(root/'assets/original/chunkG0/otomo/equip/ot001/body/mod/ot_body001.mrl3'));base.parser()
source=next(m for m in base.materialList if any(r['name'].startswith('tAlbedoMap') for r in m.resourceDict.values()))
b=(root/'蓝色小肥鱼.vrm').read_bytes();j=json.loads(b[20:20+struct.unpack_from('<I',b,12)[0]])
out=mod.Mrl3File();out.fileHeader=copy.deepcopy(base.fileHeader);out.materialList=[];out.textureDict={}
report=[]
for mat in j['materials']:
 name=mat['name'];entry=copy.deepcopy(source);entry.matInfo.materialName=name;entry.matInfo.materialNameHash=zlib.crc32(name.encode())^0xffffffff
 if mat.get('alphaMode')=='BLEND': entry.matInfo.surfaceCoef=[1,17];entry.matInfo.alphaCoef=[128,112,5,0]
 tex=mat.get('pbrMetallicRoughness',{}).get('baseColorTexture',{}).get('index')
 image=j['textures'][tex]['source'] if tex is not None else None
 custom=f'otomo\\ot000\\mod\\otomo000\\whale_image_{image}' if image is not None else None
 for code,r in entry.resourceDict.items():
  if r['name'].startswith('CB'): r['props']=copy.deepcopy(r.get('prop',mod.get_property_dict()[code]['fields']))
  if isinstance(r.get('value'),str):
   path=custom if custom and r['name'].startswith('tAlbedoMap') else r['value']
   if path not in out.textureDict:out.textureDict[path]=len(out.textureDict)
   r['value']=out.textureDict[path]+1
 out.materialList.append(entry);report.append({'name':name,'image':image,'alphaMode':mat.get('alphaMode','OPAQUE'),'template':source.matInfo.materialName,'custom_albedo':custom})
# Preserve original constant buffers verbatim; patch only texture resource indices.
original=(root/'assets/original/chunkG0/otomo/equip/ot001/body/mod/ot_body001.mrl3').read_bytes()
blocks=[]
for entry in out.materialList:
 block=bytearray(original[source.matInfo.blockOffset:source.matInfo.blockOffset+source.matInfo.blockSize])
 for offset in range(0,source.matInfo.resourceCount//2*16,16):
  code,resHash,value,unknown=struct.unpack_from('<4I',block,offset)
  resource=entry.resourceDict.get(str(resHash>>12))
  if resource and resource['name'].startswith('t'):struct.pack_into('<I',block,offset+8,resource['value'])
 blocks.append(block)
out.resourceBuffer=b''.join(blocks)
path=root/'assets/prototype/otomo000.mrl3';mod.writeMrl3File(out,str(path))
verify=mod.readMrl3File(str(path));verify.parser({e.matInfo.materialNameHash:e.matInfo.materialName for e in out.materialList})
assert len(verify.materialList)==len(j['materials'])
assert all(r['custom_albedo'] in verify.textureList for r in report if r['custom_albedo'])
(root/'research/model/material-build.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print('MRL3_ROUNDTRIP_OK',len(verify.materialList),report)



