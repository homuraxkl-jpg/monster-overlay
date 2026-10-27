import bpy,sys,importlib,json,struct,hashlib
from pathlib import Path
root=Path('D:/homura/codex/MHW');vendor=root/'tools/vendor';sys.path.insert(0,str(vendor));import addon_utils
addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
mod=importlib.import_module('MHW_Model_Editor-main.addons.MHW_Model_Editor.modules.mrl3.file_mrl3')
vrm=(root/'蓝色小肥鱼.vrm').read_bytes();j=json.loads(vrm[20:20+struct.unpack_from('<I',vrm,12)[0]])
names={(__import__('zlib').crc32(m['name'].encode())^0xffffffff):m['name'] for m in j['materials']}
file=Path('D:/steam/steamapps/common/Monster Hunter World/nativePC/otomo/ot000/mod/otomo000/otomo000.mrl3')
data=file.read_bytes();parsed=mod.readMrl3File(str(file));parsed.parser(names)
report={'installed_mrl3_sha256':hashlib.sha256(data).hexdigest(),'matches_earlier_prototype':data==(root/'assets/prototype/otomo000.mrl3').read_bytes(),'materials':[]}
for m in parsed.materialList:
 values={}
 for r in m.resourceDict.values():
  if not r['name'].startswith('CB') or not r.get('match'):continue
  offset=m.matInfo.blockOffset+r['value']
  for name,info in r.get('prop',{}).items():
   kind=info[1];count=int(kind.split('[')[1][:-1]) if '[' in kind else 1
   if any(x.lower() in name.lower() for x in ('metalic','specular','roughness','alpha','opacity','transluc','fresnel','emissive')):
    values[name]=list(struct.unpack_from('<' + ('I' if 'bool' in kind.lower() or 'uint' in kind.lower() else 'f')*count,data,offset))
   offset+=4*count
 source=next(x for x in j['materials'] if x['name']==m.matInfo.materialName)
 report['materials'].append({'name':m.matInfo.materialName,'game_shader':m.matInfo.mmtrName,'surfaceCoef':m.matInfo.surfaceCoef,'alphaCoef':m.matInfo.alphaCoef,'game_constants':values,'source_pbr':source.get('pbrMetallicRoughness',{}),'source_alphaMode':source.get('alphaMode','OPAQUE'),'source_doubleSided':source.get('doubleSided',False),'source_mtoon':source.get('extensions',{}).get('VRMC_materials_mtoon',{})})
(root/'research/face/material-fidelity-audit.json').write_text(json.dumps(report,indent=2),encoding='utf8')
for row in report['materials']:
 print(row['name'],'source',row['source_alphaMode'],'source_pbr',row['source_pbr'],'game values',{k:v for k,v in row['game_constants'].items() if any(x in k for x in ('Specular','Roughness','Metalic','Alpha','Opacity'))})
print('MRL3_EQUALS_EARLIER_PROTOTYPE',report['matches_earlier_prototype'])
