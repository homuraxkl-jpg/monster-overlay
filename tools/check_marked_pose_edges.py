import bpy,sys,json,math
from pathlib import Path
root=Path('D:/homura/codex/MHW');sys.path.insert(0,str(root/'tools/vendor'))
import addon_utils
addon_utils.enable('MHW-FreeKinetics-main',default_set=True,persistent=False)
bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working/whale-pose5-21.blend'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');arm.animation_data_create()
poses=json.loads((root/'research/model/original-leg-poses.json').read_text())
# Candidate: rigid shoe/toe skinning, preserve the current rest geometry.
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 for v in o.data.vertices:
  weights={o.vertex_groups[g.group].name:g.weight for g in v.groups}
  for toe,ankle in [('MhBone_017','MhBone_016'),('MhBone_021','MhBone_020')]:
   if toe in weights:
    o.vertex_groups[ankle].add([v.index],weights.get(ankle,0)+weights[toe],'REPLACE');o.vertex_groups[toe].remove([v.index])
report=[]
for pose in [{'action':'LMT::otomo_05::021','frame':f,'leg_span':None} for f in [0,362,724,1086,1447]]:
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
(root/'research/model/marked-pose-edge-check.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print("MARKED_POSE_EDGE_CHECK",[(r["frame"],r["worst_edges"][0]["ratio"]) for r in report])


