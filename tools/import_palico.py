import bpy, importlib.util, sys, json
from pathlib import Path
root=Path('D:/homura/codex/MHW')
p=root/'tools/vendor/MHW_Model_Editor-main'
bpy.ops.wm.read_factory_settings(use_empty=True)
import addon_utils
sys.path.insert(0,str(p.parent))
addon_utils.enable(p.name, default_set=True, persistent=False)
assert p.name in bpy.context.preferences.addons
bpy.context.preferences.addons[p.name].preferences.textureCachePath=str(root/'assets/working/texture-cache')
bpy.context.preferences.addons[p.name].preferences.showConsole=False
f=root/'assets/original/chunkG0/otomo/ot000/mod/otomo000/otomo000.mod3'
r=bpy.ops.mhw_mod3.import_mhw_mod3(directory=str(f.parent)+'/',files=[{'name':f.name}],loadMaterials=False)
assert 'FINISHED' in r, str(r)
data=[]
for o in bpy.context.scene.objects:
 if o.type=='ARMATURE': data.append({'name':o.name,'bones':[{'name':b.name,'head':list(b.head_local),'tail':list(b.tail_local),'parent':b.parent.name if b.parent else None} for b in o.data.bones]})
(root/'research/model/palico-bones.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/palico-original.blend'))
print('PALICO_IMPORT_OK', [(d['name'],len(d['bones'])) for d in data])





cat_objects=set(bpy.context.scene.objects)
bpy.ops.import_scene.gltf(filepath=str(root/'assets/working/whale-source.glb'))
whale_objects=set(bpy.context.scene.objects)-cat_objects
from mathutils import Vector
bounds={}
for label,objects in [('palico',cat_objects),('whale',whale_objects)]:
 points=[o.matrix_world@Vector(c) for o in objects if o.type=='MESH' for c in o.bound_box]
 bounds[label]={'min':[min(v[i] for v in points) for i in range(3)],'max':[max(v[i] for v in points) for i in range(3)]}
(root/'research/model/comparison-bounds.json').write_text(json.dumps(bounds,indent=2),encoding='utf-8')
# Preserve source transforms: this file is a reference scene, not a retargeted export.
bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/palico-whale-comparison.blend'))
print('COMPARISON_OK',bounds)
