import bpy,sys,json,re,math
from pathlib import Path
root=Path('D:/homura/codex/MHW')
src=(root/'tools/verify_face_runtime_v3.py').read_text(encoding='utf8');exec(src[:src.index('report={}')]);signature=mesh_hash
baseline=None
for file in ['material-v1','pot-v1']:
 meshes=load(root/f'assets/prototype/{file}/otomo000.mod3')
 hashes=sorted((mat(o),0 if int(re.search(r'Group_(\d+)',o.name).group(1))==55000 else int(re.search(r'Group_(\d+)',o.name).group(1)),signature(o)) for o in meshes if mat(o)!='TiePen')
 if baseline is None:baseline=hashes
 else:assert hashes==baseline,'Geometry or weights changed'
 assert len(meshes)==170 and len(next(o for o in bpy.context.scene.objects if o.type=='ARMATURE').data.bones)==129
pot=[o for o in meshes if int(re.search(r'Group_(\d+)',o.name).group(1))==55000]
assert len(pot)==1 and mat(pot[0])=='TiePen'
from mathutils import Vector
src=(root/'tools/verify_face_blink.py').read_text(encoding='utf8');setup=src[src.index('from mathutils import Vector'):src.index('import math')].replace('bpy.ops.render.render(write_still=True)','');exec(setup)
# Accessory visibility preview only, not a material fidelity test.
center=Vector((0,0,.5));cam.data.ortho_scale=1.5
for on in [True,False]:
 for o in meshes:
  group=int(re.search(r'Group_(\d+)',o.name).group(1));o.hide_render=group not in (0,56000) and not (on and group==55000)
 for angle,label in [(0,'front'),(45,'side')]:
  a=math.radians(angle);cam.location=center+Vector((3*math.sin(a),-3*math.cos(a),1.3));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
  scene.render.filepath=str(root/f'research/face/pot-{"on" if on else "off"}-{label}.png');bpy.ops.render.render(write_still=True)
(root/'research/face/pot-v1-roundtrip.json').write_text(json.dumps({'all_non_pot_mesh_geometry_uv_normals_weights_and_faces_unchanged':True,'meshes':170,'bones':129,'pot_mesh_count':1,'pot_group':55000,'scope':'Only accessory visibility preview; no new facial shapes'},indent=2),encoding='utf8')
print('POT_ROUNDTRIP_OK')
