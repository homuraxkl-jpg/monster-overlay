import bpy, sys, json, hashlib, struct, mmap
from pathlib import Path
from mathutils import Vector
root=Path('D:/homura/codex/MHW')
# Verify the exact instruction signature on the user's executable, without running it.
sig=bytes.fromhex('48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 20 45 0F B7 50 04')
exe=Path('D:/steam/steamapps/common/Monster Hunter World/MonsterHunterWorld.exe')
with exe.open('rb') as stream, mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ) as data:
 positions=[];pos=data.find(sig)
 while pos>=0:positions.append(pos);pos=data.find(sig,pos+1)
# Steam-wrapped disk image may not contain unpacked runtime code; runtime requires a unique match.
report={'signature_file_offsets':positions,'exe_sha256':hashlib.sha256(exe.read_bytes()).hexdigest()}
def snapshot():
 return {o.name:{'co':[tuple(v.co) for v in o.data.vertices],'groups':[[ (o.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in o.data.vertices]} for o in bpy.context.scene.objects if o.type=='MESH'}
def body_hash(o):
 h=hashlib.sha256()
 for v in o.data.vertices:
  h.update(struct.pack('<3f',*v.co))
  for g in v.groups:h.update(o.vertex_groups[g.group].name.encode());h.update(struct.pack('<f',g.weight))
 for uv in o.data.uv_layers:
  for item in uv.data:h.update(struct.pack('<2f',*item.uv))
 for face in o.data.polygons:h.update(struct.pack('<'+'I'*len(face.vertices),*face.vertices))
 for m in o.data.materials:h.update(m.name.encode())
 return h.hexdigest()
variants={}
for name,file in [('blink','whale-face-blink.blend'),('happy','whale-face-happy.blend')]:
 bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working'/file));variants[name]=snapshot()
bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working/whale-palico-rig-prototype.blend'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');assert len(arm.data.bones)==129
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
face=[o for o in meshes if any(m.name in ('head','expression') for m in o.data.materials)]
body=[o for o in meshes if o not in face];assert len(face)==3
report['body_before']={o.name:body_hash(o) for o in body};states=[]
# Nine evenly spaced samples per shape; neutral is shared. No redundant 9x9 cross product.
samples=[(0.0,0.0)]+[(0.0,i/8) for i in range(1,9)]+[(i/8,0.0) for i in range(1,9)]
for state_index,(w,blink) in enumerate(samples):
 group=56000+state_index
 for index,o in enumerate(face):
  neutral=[v.co.copy() for v in o.data.vertices];happy=variants['happy'][o.name]['co'];closed=variants['blink'][o.name]['co']
  assert len(neutral)==len(happy)==len(closed)
  assert variants['happy'][o.name]['groups']==variants['blink'][o.name]['groups']
  obj=o.copy();obj.data=o.data.copy();o.users_collection[0].objects.link(obj)
  obj.name=f'Group_{group}_Sub_{index}__{o.data.materials[0].name}'
  for i,v in enumerate(obj.data.vertices):v.co=neutral[i]+(Vector(happy[i])-neutral[i])*w+(Vector(closed[i])-neutral[i])*blink
  obj.data.update()
 states.append({'group':group,'happy':w,'blink':blink})
for o in face:bpy.data.objects.remove(o,do_unlink=True)
report['body_after']={o.name:body_hash(o) for o in body};assert report['body_before']==report['body_after']
report['states']=states;report['bones']=len(arm.data.bones);report['meshes']=len([o for o in bpy.context.scene.objects if o.type=='MESH'])
import addon_utils
vendor=root/'tools/vendor';sys.path.insert(0,str(vendor));addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
prefs=bpy.context.preferences.addons['MHW_Model_Editor-main'].preferences;prefs.showConsole=False;prefs.textureCachePath=str(root/'assets/working/texture-cache')
collection=next((c for c in bpy.data.collections if c.get('~TYPE')=='MOD3'),arm.users_collection[0])
bpy.context.scene.mhw_mod3_toolpanel.exportMod3Collection=collection
bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/whale-face-runtime-v2.blend'))
out=root/'assets/prototype/face-runtime-v2/otomo000.mod3';out.parent.mkdir(parents=True,exist_ok=True)
r=bpy.ops.mhw_mod3.export_mhw_mod3(filepath=str(out),useBlenderMaterialName=True,exportAllLODs=False,invisibleMantlesModFix=False);assert 'FINISHED' in r
report['model_sha256']=hashlib.sha256(out.read_bytes()).hexdigest()
(root/'research/face/runtime-v2-build.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('FACE_RUNTIME_BUILD_OK',report['model_sha256'],report['meshes'])
