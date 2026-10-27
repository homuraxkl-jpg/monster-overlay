import bpy,sys,json,re,math
from pathlib import Path
from mathutils import Vector
root=Path('D:/homura/codex/MHW')
source=(root/'tools/verify_face_runtime_v3.py').read_text(encoding='utf8')
exec(source[:source.index('report={}')])
report={}
for version in ('v3','v4'):
 meshes=load(root/f'assets/prototype/face-runtime-{version}/otomo000.mod3')
 arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');assert len(arm.data.bones)==129
 selected=next(o for o in meshes if 'Group_56040_' in o.name and o.data.materials[0].name.split('.')[0]=='expression')
 bad=[]
 for p in selected.data.polygons:
  if set(p.vertices)<=set(range(24,28))|set(range(36,40)):
   if p.normal.dot(Vector((0,-1,0)))<0:bad.append(p.index)
 report[version]={'mirrored_backfacing_triangles':bad,'bone_count':129}
 if version=='v4':assert not bad
 else:assert len(bad)==4
 src=(root/'tools/verify_face_blink.py').read_text(encoding='utf8')
 setup=src[src.index('from mathutils import Vector'):src.index('import math')]
 setup=setup.replace('bpy.ops.render.render(write_still=True)','')
 exec(setup)
 # Cycles normally renders both sides. Explicitly hide expression-card backfaces to reproduce game culling.
 expression=next(m for m in bpy.data.materials if m.name.split('.')[0]=='expression')
 nodes=expression.node_tree.nodes;links=expression.node_tree.links
 out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL');surface=out.inputs['Surface'].links[0].from_socket
 geo=nodes.new('ShaderNodeNewGeometry');transparent=nodes.new('ShaderNodeBsdfTransparent');cull=nodes.new('ShaderNodeMixShader')
 links.new(geo.outputs['Backfacing'],cull.inputs[0]);links.new(surface,cull.inputs[1]);links.new(transparent.outputs[0],cull.inputs[2]);links.new(cull.outputs[0],out.inputs['Surface'])
 for o in meshes:
  group=int(re.search(r'Group_(\d+)',o.name).group(1));o.hide_render=group not in (0,56040)
 scene.render.resolution_x=800;scene.render.resolution_y=800;scene.cycles.samples=12
 center=Vector((0,-0.15,0.46));cam.data.ortho_scale=.65
 for label,x in [('front',0),('left',-1.3),('right',1.3)]:
  cam.location=center+Vector((x,-3,.35));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
  scene.render.filepath=str(root/f'research/face/relaxed-culling-{version}-{label}.png');bpy.ops.render.render(write_still=True)
 if version=='v4':bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/whale-relaxed-culling-fix.blend'))
(root/'research/face/relaxed-culling-verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('RELAXED_SINGLE_SIDED_CHECK_OK',report)
