import bpy,sys,json,math
from pathlib import Path
root=Path('D:/homura/codex/MHW');sys.path.insert(0,str(root/'tools/vendor'))
import addon_utils
addon_utils.enable('MHW-FreeKinetics-main',default_set=True,persistent=False)
bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working/whale-original-animations.blend'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');arm.animation_data_create()
poses=json.loads((root/'research/model/original-leg-poses.json').read_text())
report=[]
for pose in [poses[0],poses[-1]]:
 arm.animation_data.action=bpy.data.actions[pose['action']];bpy.context.scene.frame_set(int(pose['frame']));bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();edges=[]
 for o in bpy.context.scene.objects:
  if o.type!='MESH' or not any(m.name=='body' for m in o.data.materials):continue
  ev=o.evaluated_get(dg);mesh=ev.to_mesh()
  for edge in mesh.edges:
   a,b=edge.vertices;p,q=o.data.vertices[a].co,o.data.vertices[b].co
   if max(p.z,q.z)>-0.1 or min(p.z,q.z)<-0.5:continue
   length=(p-q).length
   if length<.001:continue
   ratio=(mesh.vertices[a].co-mesh.vertices[b].co).length/length
   edges.append({'ratio':ratio,'z':[p.z,q.z],'groups':[{o.vertex_groups[g.group].name:g.weight for g in o.data.vertices[v].groups} for v in [a,b]]})
  ev.to_mesh_clear()
 edges.sort(key=lambda e:e['ratio'],reverse=True)
 report.append({'action':pose['action'],'frame':pose['frame'],'span':pose['leg_span'],'worst_edges':edges[:8]})
(root/'research/model/actual-animation-edge-diagnosis.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(report)
