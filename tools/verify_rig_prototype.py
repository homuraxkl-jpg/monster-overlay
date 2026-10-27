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
f=root/'assets/prototype/otomo000.mod3'
r=bpy.ops.mhw_mod3.import_mhw_mod3(directory=str(f.parent)+'/',files=[{'name':f.name}],loadMaterials=False)
assert 'FINISHED' in r, str(r)
bpy.context.view_layer.update()
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
assert len(arms)==1 and len(arms[0].data.bones)==129
assert len(meshes)>0
print('ROUNDTRIP_OK',len(meshes),sum(len(o.data.vertices) for o in meshes),len(arms[0].data.bones))
(root/'research/model/roundtrip.json').write_text(json.dumps({'meshes':len(meshes),'vertices':sum(len(o.data.vertices) for o in meshes),'bones':len(arms[0].data.bones)}),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/whale-palico-roundtrip.blend'))
from mathutils import Vector
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
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
scene.render.filepath=str(root/'research/model/rig-roundtrip-preview.png')
bpy.ops.render.render(write_still=True)


