import bpy,sys,json
from pathlib import Path
from mathutils import Matrix
from mathutils.kdtree import KDTree
root=Path('D:/homura/codex/MHW'); p=root/'tools/vendor/MHW_Model_Editor-main'
bpy.ops.wm.read_factory_settings(use_empty=True)
import addon_utils
sys.path.insert(0,str(p.parent));addon_utils.enable(p.name,default_set=True,persistent=False)
prefs=bpy.context.preferences.addons[p.name].preferences;prefs.showConsole=False;prefs.textureCachePath=str(root/'assets/working/texture-cache')
f=root/'assets/original/chunkG0/otomo/ot000/mod/otomo000/otomo000.mod3'
bpy.ops.mhw_mod3.import_mhw_mod3(directory=str(f.parent)+'/',files=[{'name':f.name}],loadMaterials=False)
cat=[o for o in bpy.context.scene.objects if o.type=='MESH']; arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
template=cat[0]; target=template.users_collection[0]
points=[]
for o in cat:
 for v in o.data.vertices:
  w={o.vertex_groups[g.group].name:g.weight for g in v.groups if g.weight>0 and o.vertex_groups[g.group].name in arm.data.bones}
  if w:points.append((o.matrix_world@v.co,w))
kd=KDTree(len(points))
for i,(co,w) in enumerate(points):kd.insert(co,i)
kd.balance()
before=set(bpy.context.scene.objects)
bpy.ops.import_scene.gltf(filepath=str(root/'assets/working/whale-source.glb'));bpy.context.view_layer.update()
source=[o for o in bpy.context.scene.objects if o not in before and o.type=='MESH']
print('SOURCE_MESHES',[(o.name,len(o.data.vertices)) for o in source],flush=True)
source=[o for o in source if o.data.uv_layers and any(mod.type=='ARMATURE' for mod in o.modifiers)]
for obj in list(source):
 if len(obj.data.materials)>1:
  bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='MATERIAL');bpy.ops.object.mode_set(mode='OBJECT')
source=[o for o in bpy.context.scene.objects if o not in before and o.type=='MESH' and o.data.uv_layers and any(mod.type=='ARMATURE' for mod in o.modifiers)]
created=[]; errors=[]
for index,o in enumerate(source):
 data=o.data.copy()
 if data.shape_keys:
  temp=bpy.data.objects.new('temporary',data);bpy.context.collection.objects.link(temp);temp.shape_key_clear();bpy.data.objects.remove(temp,do_unlink=True)
 transform=Matrix.Scale(0.75,4)@o.matrix_world
 data.transform(transform)
 n=template.copy();n.data=data;n.name=f'Group_0_Sub_{index}__whale_prototype';target.objects.link(n)
 n.parent=arm;n.matrix_parent_inverse=Matrix.Identity(4);n.matrix_basis=Matrix.Identity(4)
 n.vertex_groups.clear()
 for b in arm.data.bones:n.vertex_groups.new(name=b.name)
 for mod in list(n.modifiers):n.modifiers.remove(mod)
 mod=n.modifiers.new('PalicoSkeleton','ARMATURE');mod.object=arm
 for v in data.vertices:
  _,i,dist=kd.find(v.co);w=sorted(points[i][1].items(),key=lambda x:-x[1])[:8];total=sum(x[1] for x in w)
  if o.name.startswith(('head','hair')): w=[('MhBone_004',1.0)];total=1.0
  for name,weight in w:n.vertex_groups[name].add([v.index],weight/total,'REPLACE')
  errors.append(dist)
 created.append(n)
# Source authoring file remains untouched. This derivative is a geometry/rig pipeline probe.
for o in list(bpy.context.scene.objects):
 if o not in created and o!=arm:bpy.data.objects.remove(o,do_unlink=True)
bpy.context.view_layer.update()
out=root/'assets/working/whale-palico-rig-prototype.blend';bpy.ops.wm.save_as_mainfile(filepath=str(out))
report={'scale':0.75,'meshes':len(created),'vertices':sum(len(o.data.vertices) for o in created),'bones':len(arm.data.bones),'transfer':'nearest original palico vertex weights; preliminary only','distance_mean':sum(errors)/len(errors),'distance_max':max(errors),'material_status':'source Blender materials only; no game material conversion'}
(root/'research/model/rig-prototype.json').write_text(json.dumps(report,indent=2),encoding='utf8')
# Use the inherited MOD3 collection, preserve game skeleton metadata.
collection=arm.users_collection[0]
for c in bpy.data.collections:
 if c.get('~TYPE')=='MOD3':collection=c
print('COLLECTIONS',[(c.name,dict(c)) for c in bpy.data.collections])
bpy.context.scene.mhw_mod3_toolpanel.exportMod3Collection=collection
export=root/'assets/prototype/otomo000.mod3';export.parent.mkdir(parents=True,exist_ok=True)
r=bpy.ops.mhw_mod3.export_mhw_mod3(filepath=str(export),useBlenderMaterialName=True,exportAllLODs=False,invisibleMantlesModFix=False)
print('EXPORT_RESULT',r,report)



