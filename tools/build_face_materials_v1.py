import bpy,sys,importlib,struct,copy,json,zlib,hashlib
from pathlib import Path
root=Path('D:/homura/codex/MHW');sys.path.insert(0,str(root/'tools/vendor'));import addon_utils
addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
mod=importlib.import_module('MHW_Model_Editor-main.addons.MHW_Model_Editor.modules.mrl3.file_mrl3')
path=root/'assets/prototype/otomo000.mrl3';original=path.read_bytes();base=mod.readMrl3File(str(path))
names=['body','clothing','hair','head','TiePen','expression','whale_tears'];hashes={(zlib.crc32(n.encode())^0xffffffff):n for n in names};base.parser(hashes)
assert hashlib.sha256(original).hexdigest()=='ba116c2263ec5b941b28d782477a053db697fd5a6b4243f5bbc29f6af49e39ed'
out=mod.Mrl3File();out.fileHeader=copy.deepcopy(base.fileHeader);out.textureDict={n:i for i,n in enumerate(base.textureList)};out.materialList=[];blocks=[];report=[]
expr=next(m for m in base.materialList if m.matInfo.materialName=='expression')
def patch(block,template,updates):
 found=set()
 for r in template.resourceDict.values():
  if not r['name'].startswith('CB') or not r.get('match'):continue
  offset=r['value']
  for name,info in r['prop'].items():
   kind=info[1];count=int(kind.split('[')[1][:-1]) if '[' in kind else 1
   if name in updates:
    value=updates[name];seq=value if isinstance(value,list) else [value]
    assert len(seq)==count
    fmt='I' if kind in ('bbool','uint') else 'f';struct.pack_into('<'+fmt*count,block,offset,*seq);found.add(name)
   offset+=4*count
 assert found==set(updates),(found,set(updates))
for name in names:
 template=expr if name=='whale_tears' else next(m for m in base.materialList if m.matInfo.materialName==name)
 entry=copy.deepcopy(template);entry.matInfo.materialName=name;entry.matInfo.materialNameHash=zlib.crc32(name.encode())^0xffffffff
 block=bytearray(original[template.matInfo.blockOffset:template.matInfo.blockOffset+template.matInfo.blockSize]);updates={}
 if name in ('expression','whale_tears'):
  entry.matInfo.surfaceCoef=[1,225] # retain alpha draw pass, restore double-sided expression cards
  updates={'fMetalic__uiUNorm':0.0,'fSpecular__uiUNorm':0.04,'fRoughness__uiUNorm':0.7,'bUseCMM':0,'bBackFaceNormalFilp':1}
  if name=='whale_tears':updates.update({'fSpecular__uiUNorm':0.55,'fRoughness__uiUNorm':0.18,'fBaseMapFactor__uiColor':[1,1,1,0.72]})
  patch(block,template,updates)
 else:assert bytes(block)==original[template.matInfo.blockOffset:template.matInfo.blockOffset+template.matInfo.blockSize]
 blocks.append(bytes(block));out.materialList.append(entry);report.append({'name':name,'updates':updates,'surface':entry.matInfo.surfaceCoef,'alpha':entry.matInfo.alphaCoef,'shader_hash_unchanged':entry.matInfo.shaderHash==template.matInfo.shaderHash})
out.resourceBuffer=b''.join(blocks);dest=root/'assets/prototype/material-v1/otomo000.mrl3';mod.writeMrl3File(out,str(dest))
check=mod.readMrl3File(str(dest));check.parser(hashes);data=dest.read_bytes()
for name in names[:5]:
 old=next(m for m in base.materialList if m.matInfo.materialName==name);new=next(m for m in check.materialList if m.matInfo.materialName==name)
 assert original[old.matInfo.blockOffset:old.matInfo.blockOffset+old.matInfo.blockSize]==data[new.matInfo.blockOffset:new.matInfo.blockOffset+new.matInfo.blockSize]
 assert old.matInfo.surfaceCoef==new.matInfo.surfaceCoef and old.matInfo.alphaCoef==new.matInfo.alphaCoef and old.matInfo.shaderHash==new.matInfo.shaderHash
for row in report:
 new=next(m for m in check.materialList if m.matInfo.materialName==row['name'])
 for name,value in row['updates'].items():
  prop=next(r['prop'][name][0] for r in new.resourceDict.values() if r.get('prop') and name in r['prop'])
  a=prop if isinstance(prop,list) else [prop];b=value if isinstance(value,list) else [value]
  assert all(abs(x-y)<1e-5 for x,y in zip(a,b)),(name,prop,value)
report={'mrl3_sha256':hashlib.sha256(data).hexdigest(),'unchanged_materials':names[:5],'same_texture_paths':base.textureList==check.textureList,'materials':report}
(root/'research/face/material-v1-build.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('MATERIAL_V1_ROUNDTRIP_OK',report['mrl3_sha256'])
