import bpy, json
from pathlib import Path
root=Path('D:/homura/codex/MHW')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(root/'assets/working/whale-source.glb'))
report=[]
for o in bpy.context.scene.objects:
    row={'name':o.name,'type':o.type}
    if o.type=='MESH':
        row.update(vertices=len(o.data.vertices), polygons=len(o.data.polygons), shape_keys=[k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [], vertex_groups=len(o.vertex_groups))
    if o.type=='ARMATURE': row['bones']=len(o.data.bones)
    report.append(row)
(root/'research/model/blender-import.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/whale-inspection.blend'))
print('WHALE_IMPORT_OK')
