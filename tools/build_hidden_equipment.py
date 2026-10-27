import bpy,sys,json
from pathlib import Path
root=Path('D:/homura/codex/MHW');p=root/'tools/vendor/MHW_Model_Editor-main';sys.path.insert(0,str(p.parent))
import addon_utils
addon_utils.enable(p.name,default_set=True,persistent=False)
prefs=bpy.context.preferences.addons[p.name].preferences;prefs.showConsole=False;prefs.textureCachePath=str(root/'assets/working/texture-cache')
paths=['otomo/ot000/mod/otomo000/otomo000_eyelens.mod3','otomo/equip/ot001/body/mod/ot_body001.mod3','otomo/equip/ot001/helm/mod/ot_helm001.mod3']
report=[]
for rel in paths:
 for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
 f=root/'assets/original/chunkG0'/rel
 bpy.ops.mhw_mod3.import_mhw_mod3(directory=str(f.parent)+'/',files=[{'name':f.name}],loadMaterials=False,importAllLODs=True)
 objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 for o in objects:
  o.vertex_groups.clear();group=o.vertex_groups.new(name=arm.data.bones[0].name)
  for v in o.data.vertices:v.co=(0,0,0);group.add([v.index],1.0,'REPLACE')
 bpy.context.scene.mhw_mod3_toolpanel.exportMod3Collection=arm.users_collection[0]
 out=root/'assets/prototype/hide'/rel;out.parent.mkdir(parents=True,exist_ok=True)
 result=bpy.ops.mhw_mod3.export_mhw_mod3(filepath=str(out),exportAllLODs=True,invisibleMantlesModFix=False)
 assert 'FINISHED' in result
 report.append({'path':rel,'meshCount':len(objects),'method':'all vertex positions collapsed; original skeleton, material names and LOD retained'})
(root/'research/model/hide-build.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('HIDE_BUILD_OK')

