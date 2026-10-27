import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path('D:/homura/codex/MHW')
bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working/palico-whale-comparison.blend'))
bpy.context.view_layer.update()
rows=[]
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  pts=[o.matrix_world@v.co for v in o.data.vertices]
  rows.append({'name':o.name,'verts':len(pts),'min':[min(p[i] for p in pts) for i in range(3)],'max':[max(p[i] for p in pts) for i in range(3)],'parent':o.parent.name if o.parent else None,'materials':[m.name for m in o.data.materials], 'props':list(o.keys()),'collections':[c.name for c in o.users_collection]})
(root/'research/model/geometry-detail.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
for o in bpy.context.scene.objects:
 if o.type=='ARMATURE':
  print('ARMATURE',o.name)
  for b in o.data.bones: print(b.name,tuple(round(x,3) for x in b.head_local),dict(b))
print('MESHES',json.dumps([r for r in rows if r['name'] in ['body','head.001','clothing']],ensure_ascii=False))
