import bpy,sys,importlib,struct,copy,json,zlib,hashlib,shutil
from pathlib import Path
r=Path('D:/homura/codex/MHW');sys.path.insert(0,str(r/'tools/vendor'));import addon_utils
addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
mod=importlib.import_module('MHW_Model_Editor-main.addons.MHW_Model_Editor.modules.mrl3.file_mrl3');texmod=importlib.import_module('MHW_Model_Editor-main.addons.MHW_Model_Editor.modules.tex.file_tex')
source=r/'assets/prototype/material-v1/otomo000.mrl3';raw=source.read_bytes();base=mod.readMrl3File(str(source));base.parser({zlib.crc32(n.encode())^0xffffffff:n for n in ['body','clothing','hair','head','TiePen','expression','whale_tears']})
template=next(m for m in base.materialList if m.matInfo.materialName=='TiePen')
out=mod.Mrl3File();out.fileHeader=copy.deepcopy(base.fileHeader);out.materialList=[];out.textureDict={};blocks=[]
dest=r/'assets/prototype/rice-v1/otomo/accessory/ot_acc000/mod';dest.mkdir(parents=True,exist_ok=True)
prefix='otomo\\accessory\\ot_acc000\\mod\\whale_rice_'
exec((r/'tools/build_face_materials_v1.py').read_text(encoding='utf8').split('def patch(')[1].split('for name in names:')[0].join(['def patch(','']))
for name,albedo,rough,metal,spec in [('rice_pot','pot_color',.3,.65,.45),('rice_white','white',.65,0,.18)]:
 entry=copy.deepcopy(template);entry.matInfo.materialName=name;entry.matInfo.materialNameHash=zlib.crc32(name.encode())^0xffffffff
 block=bytearray(raw[template.matInfo.blockOffset:template.matInfo.blockOffset+template.matInfo.blockSize])
 for offset in range(0,template.matInfo.resourceCount//2*16,16):
  code,resHash,value,unknown=struct.unpack_from('<4I',block,offset);resource=template.resourceDict.get(str(resHash>>12))
  if not resource or not resource['name'].startswith('t') or not isinstance(resource.get('value'),str) or not resource.get('value'):continue
  n=resource['name'];filename=None
  for key,target in [('tAlbedoMap',albedo),('tNormalMap','normal'),('tDetailNormalMap','normal'),('tRMTMap','pot_rmt' if name=='rice_pot' else 'rice_rmt'),('tEmissiveMap','black'),('tColorMaskMap','black'),('tAlbedoBlendMap','black')]:
   if n.startswith(key):filename=target;break
  if filename is None:filename='normal' if resource['value'].lower().endswith('null_nm') else 'black' if resource['value'].lower().endswith('null_black') else None
  assert filename or resource['value'].startswith('Assets'),(n,resource)
  path=prefix+filename if filename else resource['value']
  if path not in out.textureDict:out.textureDict[path]=len(out.textureDict)
  struct.pack_into('<I',block,offset+8,out.textureDict[path]+1)
 patch(block,template,{'fMetalic__uiUNorm':metal,'fSpecular__uiUNorm':spec,'fRoughness__uiUNorm':rough,'bUseCMM':0})
 blocks.append(bytes(block));out.materialList.append(entry)
out.resourceBuffer=b''.join(blocks);mrl=dest/'ot_acc000.mrl3';mod.writeMrl3File(out,str(mrl));check=mod.readMrl3File(str(mrl));check.parser({zlib.crc32(n.encode())^0xffffffff:n for n in ['rice_pot','rice_white']});assert len(check.materialList)==2
shutil.copyfile(r/'assets/prototype/textures/image_5.tex',dest/'whale_rice_pot_color.tex')
for name,color,fmt in [('white',(245,243,230,255),9),('normal',(128,128,255,255),7),('pot_rmt',(255,255,0,0),7),('rice_rmt',(255,0,0,0),7),('black',(0,0,0,0),7)]:
 tex=texmod.MHWTex();tex.header.width=4;tex.header.height=4;tex.header.mipCount=1;tex.header.format=fmt;tex.mipOffsetList=[0xC0];tex.mipBuffer=bytearray(bytes(color)*16)
 with (dest/f'whale_rice_{name}.tex').open('wb') as f:tex.write(f)
 verify=texmod.MHWTex()
 with (dest/f'whale_rice_{name}.tex').open('rb') as f:verify.read(f)
 assert bytes(verify.mipBuffer)==bytes(color)*16
for path in check.textureList:assert path.startswith('Assets') or (dest/(path.split('\\')[-1]+'.tex')).is_file()
(r/'research/rice/rice-material-build.json').write_text(json.dumps({'materials':['rice_pot','rice_white'],'textures':check.textureList,'all_custom_textures_bundled':True,'emissive':'disabled in inherited template','mrl_sha256':hashlib.sha256(mrl.read_bytes()).hexdigest(),'scope':'Native Uber material render requires game validation'},indent=2),encoding='utf8')
print('RICE_MATERIALS_OK')

