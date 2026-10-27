import bpy,sys,json
from pathlib import Path
root=Path('D:/homura/codex/MHW');p=root/'tools/vendor/MHW_Model_Editor-main';sys.path.insert(0,str(p.parent))
import addon_utils
addon_utils.enable(p.name,default_set=True,persistent=False)
prefs=bpy.context.preferences.addons[p.name].preferences;prefs.showConsole=False;prefs.textureCachePath=str(root/'assets/working/texture-cache')
paths=json.loads((root/'research/model/hide-build.json').read_text())
for item in paths:
 for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
 f=root/'assets/prototype/hide'/item['path']
 result=bpy.ops.mhw_mod3.import_mhw_mod3(directory=str(f.parent)+'/',files=[{'name':f.name}],loadMaterials=False,importAllLODs=True)
 assert 'FINISHED' in result
 for o in bpy.context.scene.objects:
  if o.type!='MESH':continue
  for v in o.data.vertices:
   assert v.co.length<1e-6
   assert len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-6
 print('HIDE_ROUNDTRIP_OK',item['path'])
