import bpy,sys,json,re,math,hashlib,struct
from pathlib import Path
from mathutils import Vector
r=Path('D:/homura/codex/MHW')
src=(r/'tools/verify_face_runtime_v3.py').read_text(encoding='utf8');exec(src[:src.index('report={}')]);signature=mesh_hash
baseline=None;report={}
for label,path in [('baseline',r/'assets/prototype/pot-v1/otomo000.mod3'),('candidate',r/'assets/prototype/appearance-v1/otomo000.mod3')]:
 meshes=load(path)
 for o in meshes:
  if mat(o)=='whale_outline':o.visible_shadow=False;o.visible_diffuse=False;o.visible_glossy=False
 originals=sorted((int(re.search(r'Group_(\d+)',o.name).group(1)),mat(o),signature(o)) for o in meshes if mat(o)!='whale_outline')
 if baseline is None:baseline=originals
 else:assert originals==baseline,'Original geometry/UV/normals/weights changed in roundtrip'
 assert len(next(o for o in bpy.context.scene.objects if o.type=='ARMATURE').data.bones)==129
 report[label]={'meshes':len(meshes),'original_meshes_unchanged':originals==baseline}
 if label=='baseline':continue
 assert len(meshes)==258
 mapping={'body':0,'clothing':1,'hair':2,'head':3,'TiePen':5,'expression':6,'whale_tears':6}
 for material in bpy.data.materials:
  name=material.name.split('.')[0]
  if name not in mapping and name!='whale_outline':continue
  material.use_nodes=True;nodes=material.node_tree.nodes;nodes.clear();links=material.node_tree.links
  out=nodes.new('ShaderNodeOutputMaterial');shader=nodes.new('ShaderNodeBsdfPrincipled');shader.inputs['Roughness'].default_value=.85;shader.inputs['Specular IOR Level'].default_value=0
  if name=='whale_outline':shader.inputs['Base Color'].default_value=(.006,.004,.01,1)
  else:
   tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(r/f'assets/prototype/textures/image_{mapping[name]}.png'))
   tint=nodes.new('ShaderNodeMixRGB');tint.name='AppearanceTint';tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(1,1,1,1);links.new(tex.outputs['Color'],tint.inputs[1]);links.new(tint.outputs[0],shader.inputs['Base Color'])
   if name in ('expression','whale_tears'):links.new(tex.outputs['Alpha'],shader.inputs['Alpha'])
  if name not in ('expression','whale_tears'):
   geo=nodes.new('ShaderNodeNewGeometry');trans=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader');links.new(geo.outputs['Backfacing'],mix.inputs[0]);links.new(shader.outputs[0],mix.inputs[1]);links.new(trans.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],out.inputs[0])
  else:links.new(shader.outputs[0],out.inputs[0])
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True;scene.world=bpy.data.worlds.new('AppearancePreview');scene.world.color=(.18,.18,.18)
 for location,energy,size in [((1,-2,2),150,1),((-1,-1,.8),65,1)]:
  bpy.ops.object.light_add(type='AREA',location=location);light=bpy.context.object;light.data.energy=energy;light.data.size=size;light.rotation_euler=(Vector((0,0,.4))-light.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add();camera=bpy.context.object;camera.data.type='ORTHO';scene.camera=camera;scene.view_settings.view_transform='Standard';scene.view_settings.exposure=-1.0
 scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
 def render(preset,outline,shape,angle,view):
  tint=(1,1,1,1) if preset=='original' else (1,.982,.978,1)
  bpy.data.materials['head'].node_tree.nodes['AppearanceTint'].inputs[2].default_value=tint
  group={'neutral':56000,'sad':56024,'relaxed':56040}[shape]
  for o in meshes:
   g=int(re.search(r'Group_(\d+)',o.name).group(1));o.hide_render=g not in (0,group) and not (outline and g in (57000,57010+group-56000))
  center=Vector((0,-.15,.44)) if view=='face' else Vector((0,0,.12));camera.data.ortho_scale=.70 if view=='face' else 2.2
  a=math.radians(angle);camera.location=center+Vector((3*math.sin(a),-3*math.cos(a),.3 if view=='face' else .6));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
  scene.render.filepath=str(r/f'research/face/appearance-{preset}-outline-{int(outline)}-{shape}-{angle}-{view}.png');bpy.ops.render.render(write_still=True)
 for preset,outline in [('original',False),('soft-rosy',False),('soft-rosy',True)]:
  render(preset,outline,'neutral',0,'face');render(preset,outline,'neutral',0,'body')
 for shape in ('sad','relaxed'):
  for angle in (-28,0,28):render('soft-rosy',True,shape,angle,'face')
 report['preview_scope']='Cycles approximation with explicit single-sided backface culling; not the native Uber_Mt renderer. No source texture edited.'
 (r/'research/face/appearance-v1-verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
 bpy.ops.wm.save_as_mainfile(filepath=str(r/'assets/working/whale-appearance-v1-preview.blend'))
print('APPEARANCE_ROUNDTRIP_AND_CULLING_PREVIEWS_OK')
