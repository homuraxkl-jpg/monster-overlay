import bpy,sys,importlib,zlib,json
from pathlib import Path
r=Path('D:/homura/codex/MHW');sys.path.insert(0,str(r/'tools/vendor'));import addon_utils
addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
m=importlib.import_module('MHW_Model_Editor-main.addons.MHW_Model_Editor.modules.mrl3.file_mrl3')
p=Path('D:/steam/steamapps/common/Monster Hunter World/nativePC/otomo/ot000/mod/otomo000/otomo000.mrl3');a=m.readMrl3File(str(p));names=['body','clothing','hair','head','TiePen','expression','whale_tears'];a.parser({zlib.crc32(n.encode())^0xffffffff:n for n in names})
report=[]
for mat in a.materialList:
 values={k:v for resource in mat.resourceDict.values() for k,v in resource.get('prop',{}).items() if any(term in k.lower() for term in ('satur','edge','basecolor','basemap','emissi','opacity'))}
 report.append({'name':mat.matInfo.materialName,'surface':mat.matInfo.surfaceCoef,'shader':mat.matInfo.mmtrName,'values':values})
print('APPEARANCE_MATERIALS',json.dumps(report,ensure_ascii=False))
bpy.ops.wm.open_mainfile(filepath=str(r/'assets/working/whale-material-v1-lit-preview.blend'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
from collections import Counter
print('MESHES',len(meshes),Counter(m.name for o in meshes for m in o.data.materials))
for o in meshes[:10]:print('MESH',o.name,len(o.data.vertices),[m.name for m in o.data.materials])
(r/'research/face/appearance-material-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
