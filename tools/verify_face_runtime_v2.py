import bpy,sys,json,hashlib,struct,re
from pathlib import Path
root=Path('D:/homura/codex/MHW');sys.path.insert(0,str(root/'tools/vendor'));import addon_utils
addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
prefs=bpy.context.preferences.addons['MHW_Model_Editor-main'].preferences;prefs.showConsole=False;prefs.textureCachePath=str(root/'assets/working/texture-cache')
def mat(o):return o.data.materials[0].name.split('.')[0]
def mesh_hash(o):
 h=hashlib.sha256()
 for v in o.data.vertices:
  h.update(struct.pack('<3f',*v.co))
  for g in v.groups:h.update(o.vertex_groups[g.group].name.encode());h.update(struct.pack('<f',g.weight))
 for uv in o.data.uv_layers:
  for u in uv.data:h.update(struct.pack('<2f',*u.uv))
 for n in o.data.corner_normals:h.update(struct.pack('<3f',*n.vector))
 for p in o.data.polygons:h.update(struct.pack('<'+'I'*len(p.vertices),*p.vertices))
 return h.hexdigest()
def load(path):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
 prefs=bpy.context.preferences.addons['MHW_Model_Editor-main'].preferences;prefs.showConsole=False;prefs.textureCachePath=str(root/'assets/working/texture-cache')
 r=bpy.ops.mhw_mod3.import_mhw_mod3(directory=str(path.parent)+'/',files=[{'name':path.name}],loadMaterials=False);assert 'FINISHED' in r
 bpy.context.view_layer.update()
 return [o for o in bpy.context.scene.objects if o.type=='MESH']
report={}
for name,file in [('baseline',root/'assets/prototype/otomo000.mod3'),('runtime',root/'assets/prototype/face-runtime-v2/otomo000.mod3')]:
 meshes=load(file);body=sorted((mat(o),mesh_hash(o)) for o in meshes if mat(o) not in ('head','expression'))
 report[name]={'body':body,'bones':len(next(o for o in bpy.context.scene.objects if o.type=='ARMATURE').data.bones),'meshes':len(meshes)}
assert report['baseline']['body']==report['runtime']['body'],'Non-face roundtrip mismatch'
assert report['runtime']['bones']==report['baseline']['bones']==129
from collections import Counter
counts=Counter(int(re.search(r'Group_(\d+)',o.name).group(1)) for o in meshes)
assert counts[0]==6 and all(counts[g]==3 for g in range(56000,56017)),counts
report['groups']=dict(counts);report['body_unchanged']=True
(root/'research/face/runtime-v2-roundtrip.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/whale-face-runtime-v2-roundtrip.blend'))
# Reuse the established texture/camera setup, then render selected exclusive states.
src=(root/'tools/verify_face_blink.py').read_text(encoding='utf8')
setup=src[src.index('from mathutils import Vector'):src.index('import math')]
setup=setup.replace("scene.render.filepath=str(root/'research/model/face-blink-preview.png')\nbpy.ops.render.render(write_still=True)","")
# The extracted setup has one render call; remove it safely before per-state renders.
setup=setup.replace('bpy.ops.render.render(write_still=True)','')
exec(setup)
for group,label in [(56000,'neutral'),(56002,'blink-25'),(56004,'blink-50'),(56006,'blink-75'),(56008,'blink-100'),(56010,'happy-25'),(56012,'happy-50'),(56014,'happy-75'),(56016,'happy-100')]:
 for o in meshes:
  g=int(re.search(r'Group_(\d+)',o.name).group(1));o.hide_render=g not in (0,group)
 scene.render.filepath=str(root/f'research/face/runtime-v2-{label}.png');bpy.ops.render.render(write_still=True)
print('RUNTIME_ROUNDTRIP_BODY_LOCK_OK',counts)
