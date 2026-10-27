import bpy,sys,json,importlib
from pathlib import Path
r=Path('D:/homura/codex/MHW');sys.path.insert(0,str(r/'tools/vendor'));import addon_utils
addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
p=bpy.context.preferences.addons['MHW_Model_Editor-main'].preferences;p.showConsole=False;p.textureCachePath=str(r/'assets/working/texture-cache')
f=r/'assets/original/chunkG0-bottle/otomo/accessory/ot_acc000/mod/ot_acc000.mod3'
bpy.ops.wm.read_factory_settings(use_empty=True);addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
p=bpy.context.preferences.addons['MHW_Model_Editor-main'].preferences;p.showConsole=False;p.textureCachePath=str(r/'assets/working/texture-cache')
bpy.ops.mhw_mod3.import_mhw_mod3(directory=str(f.parent)+'/',files=[{'name':f.name}],loadMaterials=False)
report=[]
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  pos=[o.matrix_world@v.co for v in o.data.vertices]
  report.append({'name':o.name,'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'materials':[m.name for m in o.data.materials],'bounds':[[min(v[k] for v in pos),max(v[k] for v in pos)] for k in range(3)],'groups':[(g.name,sum(1 for v in o.data.vertices for a in v.groups if a.group==g.index and a.weight>0)) for g in o.vertex_groups if any(a.group==g.index and a.weight>0 for v in o.data.vertices for a in v.groups)]})
 elif o.type=='ARMATURE':report.append({'armature':o.name,'bones':[(b.name,list(b.head_local),b.parent.name if b.parent else None) for b in o.data.bones]})
(r/'research/rice/bottle-inspection.json').write_text(json.dumps(report,indent=2),encoding='utf8');bpy.ops.wm.save_as_mainfile(filepath=str(r/'assets/working/palico-bottle-original.blend'))
mod=importlib.import_module('MHW_Model_Editor-main.addons.MHW_Model_Editor.modules.mrl3.file_mrl3')
f=r/'assets/original/chunkG7-bottle/otomo/accessory/ot_acc000/mod/ot_acc000.mrl3';m=mod.readMrl3File(str(f));m.parser()
(r/'research/rice/bottle-material.json').write_text(json.dumps({'textures':m.textureList,'materials':[{'hash':a.matInfo.materialNameHash,'shader':a.matInfo.shaderHash,'resources':a.resourceDict,'surface':a.matInfo.surfaceCoef,'alpha':a.matInfo.alphaCoef} for a in m.materialList]},indent=2,default=str),encoding='utf8')
print('BOTTLE_INSPECT_OK',report)
