import bpy
from pathlib import Path
root=Path('D:/homura/codex/MHW');snap={}
for shape,file in [('neutral','whale-palico-rig-prototype.blend'),('sad','whale-face-sad.blend')]:
 bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working'/file))
 snap[shape]={o.name:[tuple(v.co) for v in o.data.vertices] for o in bpy.context.scene.objects if o.type=='MESH'}
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 moved=[i for i,(a,b) in enumerate(zip(snap['neutral'][o.name],snap['sad'][o.name])) if sum((x-y)**2 for x,y in zip(a,b))>1e-8]
 print(o.name,[m.name for m in o.data.materials],len(moved),moved if len(moved)<80 else '')
 if o.data.materials[0].name=='expression':
  for first in range(0,56,4):
   ids=range(first,first+4)
   if any(i in moved for i in ids):print('ACTIVATED',list(ids),'neutral',snap['neutral'][o.name][first],'sad',snap['sad'][o.name][first])
