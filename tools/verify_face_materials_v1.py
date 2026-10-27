import bpy,sys,json,re,math
from pathlib import Path
from mathutils import Vector
root=Path('D:/homura/codex/MHW')
src=(root/'tools/verify_face_runtime_v3.py').read_text(encoding='utf8');exec(src[:src.index('report={}')]);mesh_signature=mesh_hash
report={};baseline=None
for version,file in [('before',root/'assets/prototype/face-runtime-v4/otomo000.mod3'),('after',root/'assets/prototype/material-v1/otomo000.mod3')]:
 meshes=load(file)
 body=sorted((o.data.materials[0].name.split('.')[0],mesh_signature(o)) for o in meshes if o.data.materials[0].name.split('.')[0] not in ('head','expression','whale_tears'))
 if baseline is None:baseline=body
 else:assert body==baseline,'Locked body changed'
 assert len(next(o for o in bpy.context.scene.objects if o.type=='ARMATURE').data.bones)==129
 from collections import Counter
 counts=Counter(int(re.search(r'Group_(\d+)',o.name).group(1)) for o in meshes)
 if version=='after':assert counts[0]==6 and all(counts[g]==4 for g in range(56000,56041)),counts
 report[version]={'body_hashes':body,'meshes':len(meshes),'groups':dict(counts)}
 # A lit approximation for reflection/opacity comparison, not a native Uber_Mt renderer.
 mapping=json.loads((root/'research/model/material-build.json').read_text(encoding='utf8'));images={r['name']:r['image'] for r in mapping};images['whale_tears']=6
 for material in bpy.data.materials:
  name=material.name.split('.')[0]
  if name not in images:continue
  material.use_nodes=True;nodes=material.node_tree.nodes;nodes.clear();links=material.node_tree.links
  out=nodes.new('ShaderNodeOutputMaterial');bsdf=nodes.new('ShaderNodeBsdfPrincipled');tex=nodes.new('ShaderNodeTexImage')
  tex.image=bpy.data.images.load(str(root/f'assets/prototype/textures/image_{images[name]}.png'));links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
  rough=.85;spec=0;alpha=1
  if version=='after' and name=='expression':rough=.7;spec=.04
  if name=='whale_tears':rough=.18;spec=.55;alpha=.72
  bsdf.inputs['Roughness'].default_value=rough;bsdf.inputs['Metallic'].default_value=0;bsdf.inputs['Specular IOR Level'].default_value=spec
  if name in ('expression','whale_tears'):
   a=nodes.new('ShaderNodeMath');a.operation='MULTIPLY';a.inputs[1].default_value=alpha;links.new(tex.outputs['Alpha'],a.inputs[0]);links.new(a.outputs[0],bsdf.inputs['Alpha'])
  links.new(bsdf.outputs[0],out.inputs[0])
  if version=='before' and name=='expression':
   geo=nodes.new('ShaderNodeNewGeometry');trans=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader');links.new(geo.outputs['Backfacing'],mix.inputs[0]);links.new(bsdf.outputs[0],mix.inputs[1]);links.new(trans.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],out.inputs[0])
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=True
 scene.world=bpy.data.worlds.new('MaterialPreview');scene.world.color=(.12,.12,.12)
 bpy.ops.object.light_add(type='AREA',location=(1.0,-2,2));light=bpy.context.object;light.data.energy=180;light.data.shape='DISK';light.data.size=.4;light.rotation_euler=(Vector((0,0,.4))-light.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.light_add(type='AREA',location=(-1,-1,.8));light=bpy.context.object;light.data.energy=55;light.data.size=1;light.rotation_euler=(Vector((0,0,.4))-light.location).to_track_quat('-Z','Y').to_euler()
 center=Vector((0,-.15,.44));bpy.ops.object.camera_add(location=center+Vector((.7,-3,.3)));cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=.62;scene.camera=cam
 scene.view_settings.view_transform='Standard';scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
 for group,label in [(56024,'sad'),(56040,'relaxed')]:
  for o in meshes:o.hide_render=int(re.search(r'Group_(\d+)',o.name).group(1)) not in (0,group)
  scene.render.filepath=str(root/f'research/face/material-v1-{version}-{label}.png');bpy.ops.render.render(write_still=True)
 if version=='after':bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/whale-material-v1-lit-preview.blend'))
report['body_unchanged']=True;report['scope']='Lit Principled approximation only; native Uber_Mt blending/reflection require game validation'
(root/'research/face/material-v1-verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('MATERIAL_V1_BODY_AND_GROUPS_OK')
