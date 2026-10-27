import bpy,sys,json,math
from pathlib import Path
from mathutils import Vector
root=Path('D:/homura/codex/MHW');sys.path.insert(0,str(root/'tools/vendor'))
import addon_utils
addon_utils.enable('MHW-FreeKinetics-main',default_set=True,persistent=False)
bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working/whale-long-skirt-variant.blend'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');arm.animation_data_create()
with bpy.data.libraries.load(str(root/'assets/working/whale-original-animations.blend'),link=False) as (src,dst):
 dst.actions=[name for name in src.actions if name in ['LMT::otomo_00::093','LMT::otomo_00::032','LMT::otomo_00::001']]
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.use_denoising=True
mapping=json.loads((root/'research/model/material-build.json').read_text(encoding='utf8'))
for row in mapping:
 mat=bpy.data.materials.get(row['name'])
 if mat is None: continue
 mat.use_nodes=True;nodes=mat.node_tree.nodes;nodes.clear()
 out=nodes.new('ShaderNodeOutputMaterial');em=nodes.new('ShaderNodeEmission');img=nodes.new('ShaderNodeTexImage')
 img.image=bpy.data.images.load(str(root/f"assets/prototype/textures/image_{row['image']}.png"))
 mat.node_tree.links.new(img.outputs['Color'],em.inputs['Color'])
 if row['alphaMode']=='BLEND':
  trans=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader');mat.node_tree.links.new(img.outputs['Alpha'],mix.inputs[0]);mat.node_tree.links.new(trans.outputs[0],mix.inputs[1]);mat.node_tree.links.new(em.outputs[0],mix.inputs[2]);mat.node_tree.links.new(mix.outputs[0],out.inputs[0])
 else:mat.node_tree.links.new(em.outputs[0],out.inputs[0])
scene.world=bpy.data.worlds.new('Preview');scene.world.color=(.08,.08,.08)
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';scene.camera=cam
scene.render.resolution_x=600;scene.render.resolution_y=800;scene.render.resolution_percentage=100
report=[]
for number,frame in [('001',62),('093',8),('032',7)]:
 action=next(a for a in bpy.data.actions if a.name=='LMT::otomo_00::'+number);arm.animation_data.action=action;scene.frame_set(frame);bpy.context.view_layer.update()
 dg=bpy.context.evaluated_depsgraph_get();points=[]
 for o in scene.objects:
  if o.type!='MESH':continue
  ev=o.evaluated_get(dg);mesh=ev.to_mesh();points.extend(ev.matrix_world@v.co for v in mesh.vertices);ev.to_mesh_clear()
 assert all(math.isfinite(x) for v in points for x in v)
 lo=Vector(tuple(min(v[i] for v in points) for i in range(3)));hi=Vector(tuple(max(v[i] for v in points) for i in range(3)));center=(lo+hi)/2;extent=max(hi-lo)
 cam.location=center+Vector((1.4,-3,1))*extent;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=extent*1.5
 scene.render.filepath=str(root/f'research/model/long-skirt-action-{number}.png');bpy.ops.render.render(write_still=True)
 report.append({'action':number,'frame':frame,'bounds':[list(lo),list(hi)],'finite':True})
(root/'research/model/long-skirt-animation-check.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/whale-long-skirt-animated.blend'))

