import bpy,math,json
from pathlib import Path
root=Path('D:/homura/codex/MHW')
report=[]
for label,file in [('before','whale-before-leg-segments.blend'),('after','whale-palico-rig-prototype.blend')]:
 bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working'/file))
 arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 initial={o.name:[v.co.copy() for v in o.data.vertices] for o in meshes}
 for name,angle in [('MhBone_014',-25),('MhBone_018',-25),('MhBone_015',60),('MhBone_019',60),('MhBone_016',-35),('MhBone_020',-35)]:
  arm.pose.bones[name].rotation_mode='XYZ';arm.pose.bones[name].rotation_euler.y=math.radians(angle)
 bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();ratios=[]
 for o in meshes:
  if not any(m.name=='body' for m in o.data.materials):continue
  evaluated=o.evaluated_get(dg);mesh=evaluated.to_mesh()
  for edge in mesh.edges:
   a,b=edge.vertices;p,q=initial[o.name][a],initial[o.name][b]
   if max(p.z,q.z)>-0.12 or min(p.z,q.z)<-0.49:continue
   length=(p-q).length
   if length<0.001:continue
   ratio=(mesh.vertices[a].co-mesh.vertices[b].co).length/length
   assert math.isfinite(ratio)
   ratios.append(ratio)
  evaluated.to_mesh_clear()
 ratios.sort()
 report.append({'variant':label,'scope':'synthetic thigh/knee/ankle flexion, not captured game animation','edges':len(ratios),'max_stretch':max(ratios),'p95_stretch':ratios[int(len(ratios)*.95)]})
print(report)
(root/'research/model/leg-segment-comparison.json').write_text(json.dumps(report,indent=2),encoding='utf8')

