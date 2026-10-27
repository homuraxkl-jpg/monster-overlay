import bpy,math,json
from pathlib import Path
root=Path('D:/homura/codex/MHW')
report=[]
for label,file in [('before','whale-rig-before-pelvis-fix.blend'),('after','whale-palico-rig-prototype.blend')]:
 bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working'/file))
 arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 initial={o.name:[v.co.copy() for v in o.data.vertices] for o in meshes}
 for name,angle in [('MhBone_001',25),('MhBone_014',-12),('MhBone_018',12)]:
  arm.pose.bones[name].rotation_mode='XYZ';arm.pose.bones[name].rotation_euler.y=math.radians(angle)
 bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();ratios=[]
 for o in meshes:
  if not any(m.name=='body' for m in o.data.materials):continue
  evaluated=o.evaluated_get(dg);mesh=evaluated.to_mesh()
  for edge in mesh.edges:
   a,b=edge.vertices;p,q=initial[o.name][a],initial[o.name][b]
   if max(p.z,q.z)>0 or min(p.z,q.z)<-0.36:continue
   length=(p-q).length
   if length<0.001:continue
   ratio=(mesh.vertices[a].co-mesh.vertices[b].co).length/length
   assert math.isfinite(ratio)
   ratios.append(ratio)
  evaluated.to_mesh_clear()
 ratios.sort()
 report.append({'variant':label,'scope':'synthetic torso lean and thigh rotation, not captured in-game hands-on-hips','edges':len(ratios),'max_stretch':max(ratios),'p95_stretch':ratios[int(len(ratios)*.95)]})
print(report)
(root/'research/model/pelvis-pose-comparison.json').write_text(json.dumps(report,indent=2),encoding='utf8')
