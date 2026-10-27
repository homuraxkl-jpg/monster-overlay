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
f=root/'assets/prototype/long-skirt/otomo000.mod3'
r=bpy.ops.mhw_mod3.import_mhw_mod3(directory=str(f.parent)+'/',files=[{'name':f.name}],loadMaterials=False)
assert 'FINISHED' in r, str(r)
bpy.context.view_layer.update()
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
assert len(arms)==1 and len(arms[0].data.bones)==129
assert len(meshes)>0
print('ROUNDTRIP_OK',len(meshes),sum(len(o.data.vertices) for o in meshes),len(arms[0].data.bones))
(root/'research/model/roundtrip.json').write_text(json.dumps({'meshes':len(meshes),'vertices':sum(len(o.data.vertices) for o in meshes),'bones':len(arms[0].data.bones)}),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/whale-long-skirt-roundtrip.blend'))
from mathutils import Vector
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.use_denoising=True
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
scene.display.shading.light='STUDIO'
scene.display.shading.color_type='MATERIAL'
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.display.shading.background_type='WORLD'
scene.world=bpy.data.worlds.new("PreviewWorld")
scene.world.color=(0.08,0.08,0.08)
points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
lo=Vector(tuple(min(v[i] for v in points) for i in range(3)));hi=Vector(tuple(max(v[i] for v in points) for i in range(3)));center=(lo+hi)/2;extent=max(hi-lo)
print('BOUNDS',tuple(lo),tuple(hi),flush=True)
bpy.ops.object.camera_add(location=center+Vector((1.4,-2.5,1.0))*extent)
cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=extent*1.4
scene.camera=cam
scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.render.filepath=str(root/'research/model/long-skirt-preview.png')
bpy.ops.render.render(write_still=True)


import math
arms[0].pose.bones['MhBone_004'].rotation_mode='XYZ';arms[0].pose.bones['MhBone_004'].rotation_euler.z=math.radians(20)
bpy.context.view_layer.update()
dg=bpy.context.evaluated_depsgraph_get();pose_points=[]
for o in meshes:
 evaluated=o.evaluated_get(dg);mesh=evaluated.to_mesh()
 for v in mesh.vertices:
  co=evaluated.matrix_world@v.co
  assert all(math.isfinite(x) for x in co)
  pose_points.append(co)
 evaluated.to_mesh_clear()
span=[max(p[i] for p in pose_points)-min(p[i] for p in pose_points) for i in range(3)]
assert max(span)<2,span
(root/'research/model/pose-smoke.json').write_text(json.dumps({'head_rotation_degrees':20,'finite':True,'span':span,'scope':'synthetic head pose only; not game animation'}),encoding='utf8')
scene.render.filepath=str(root/'research/model/long-skirt-head-preview.png');bpy.ops.render.render(write_still=True)
print('HEAD_POSE_SMOKE_OK',span)

