import bpy,sys,json,math,hashlib,struct,re
from pathlib import Path
r=Path('D:/homura/codex/MHW');sys.path.insert(0,str(r/'tools/vendor'));import addon_utils
addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False)
p=bpy.context.preferences.addons['MHW_Model_Editor-main'].preferences;p.showConsole=False;p.textureCachePath=str(r/'assets/working/texture-cache')
f=r/'assets/prototype/rice-v1/otomo/accessory/ot_acc000/mod/ot_acc000.mod3'
bpy.ops.wm.read_factory_settings(use_empty=True);addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False);p=bpy.context.preferences.addons['MHW_Model_Editor-main'].preferences;p.showConsole=False;p.textureCachePath=str(r/'assets/working/texture-cache')
bpy.ops.mhw_mod3.import_mhw_mod3(directory=str(f.parent)+'/',files=[{'name':f.name}],loadMaterials=False)
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(arm.data.bones)==1 and len(meshes)==2
assert {o.data.materials[0].name for o in meshes}=={'rice_pot','rice_white'}
assert all(int(re.search(r'Group_(\d+)',o.name).group(1))==0 for o in meshes)
for o in meshes:
 assert o.data.uv_layers and len(o.data.vertices)<65536
 for v in o.data.vertices:
  assert all(math.isfinite(x) for x in v.co)
  assert len(v.groups)==1 and o.vertex_groups[v.groups[0].group].name=='MhBone_000' and abs(v.groups[0].weight-1)<1e-5
# Exporter must preserve the original one-bone rest data exactly.
a=(r/'assets/original/chunkG0-bottle/otomo/accessory/ot_acc000/mod/ot_acc000.mod3').read_bytes();b=f.read_bytes()
def bones(data):
 start,end=struct.unpack_from('<2Q',data,48);return data[start:end]
assert bones(a)==bones(b),'Original prop skeleton differs'
poses=[]
for angle in [0,45,100]:
 arm.pose.bones[0].rotation_mode='XYZ';arm.pose.bones[0].rotation_euler.x=math.radians(angle);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
 for o in meshes:
  evaluated=o.evaluated_get(dg);mesh=evaluated.to_mesh();assert all(all(math.isfinite(x) for x in v.co) for v in mesh.vertices);evaluated.to_mesh_clear()
 poses.append(angle)
(r/'research/rice/rice-roundtrip.json').write_text(json.dumps({'mesh_count':len(meshes),'bone_count':1,'original_skeleton_bytes_identical':True,'group0_only':True,'all_vertices_root_weight_1':True,'finite_root_rotation_degrees':poses,'scope':'Prop rig test only, not actual palico hand placement or resource usage'},indent=2),encoding='utf8')
print('RICE_ROUNDTRIP_OK')
