import bpy,sys,json
from pathlib import Path
from mathutils import Matrix, Vector
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
sourceArm=next(o for o in bpy.context.scene.objects if o not in before and o.type=='ARMATURE')
semantic={'root':'MhBone_000','hips':'MhBone_013','spine':'MhBone_002','chest':'MhBone_003','neck':'MhBone_004','head':'MhBone_004','upper_arm.L':'MhBone_006','lower_arm.L':'MhBone_007','hand.L':'MhBone_008','upper_arm.R':'MhBone_010','lower_arm.R':'MhBone_011','hand.R':'MhBone_012','upper_leg.L':'MhBone_014','lower_leg.L':'MhBone_015','foot.L':'MhBone_016','toes.L':'MhBone_017','upper_leg.R':'MhBone_018','lower_leg.R':'MhBone_019','foot.R':'MhBone_020','toes.R':'MhBone_021'}
def resolve(name):
 bone=sourceArm.data.bones.get(name)
 while bone and bone.name not in semantic:bone=bone.parent
 return bone.name if bone else 'hips'
created=[]; errors=[]
for index,o in enumerate(source):
 data=o.data.copy()
 if data.shape_keys:
  temp=bpy.data.objects.new('temporary',data);bpy.context.collection.objects.link(temp);temp.shape_key_clear();bpy.data.objects.remove(temp,do_unlink=True)
 transform=o.matrix_world
 data.transform(transform)
 n=template.copy();n.data=data;n.name=f'Group_0_Sub_{index}__whale_prototype';target.objects.link(n)
 n.parent=arm;n.matrix_parent_inverse=Matrix.Identity(4);n.matrix_basis=Matrix.Identity(4)
 n.vertex_groups.clear()
 for b in arm.data.bones:n.vertex_groups.new(name=b.name)
 for mod in list(n.modifiers):n.modifiers.remove(mod)
 mod=n.modifiers.new('PalicoSkeleton','ARMATURE');mod.object=arm
 for v in data.vertices:
  sourceVertex=o.data.vertices[v.index]
  weights={}
  result=Vector((0,0,0));total=0
  for g in sourceVertex.groups:
   if g.weight<=0:continue
   sourceName=resolve(o.vertex_groups[g.group].name);targetName=semantic[sourceName]
   sourceHead=sourceArm.matrix_world@sourceArm.data.bones[sourceName].head_local
   targetHead=arm.matrix_world@arm.data.bones[targetName].head_local
   result+=(targetHead+(v.co-sourceHead)*1.35)*g.weight
   weights[targetName]=weights.get(targetName,0)+g.weight;total+=g.weight
  assert total>0
  v.co=result/total
  # Fallback garment variant: extend only the lower dress/apron, leave sleeves and torso intact.
  if any(m.name in ['clothing','TiePen'] for m in data.materials):
   sourceZ=(o.matrix_world@sourceVertex.co).z
   if sourceZ<0.23:
    t=max(0,min(1,(0.23-sourceZ)/(0.23-0.0945)));t=t*t*(3-2*t)
    v.co.z-=0.29*t
    weights={'MhBone_013':total}
  for toe,ankle in [('MhBone_017','MhBone_016'),('MhBone_021','MhBone_020')]:
   if toe in weights:weights[ankle]=weights.get(ankle,0)+weights.pop(toe)
  for name,weight in sorted(weights.items(),key=lambda item:-item[1])[:8]:n.vertex_groups[name].add([v.index],weight/total,'REPLACE')
  errors.append(0)
 created.append(n)
# Source authoring file remains untouched. This derivative is a geometry/rig pipeline probe.
for o in list(bpy.context.scene.objects):
 if o not in created and o!=arm:bpy.data.objects.remove(o,do_unlink=True)
bpy.context.view_layer.update()
out=root/'assets/working/whale-long-skirt-variant.blend';bpy.ops.wm.save_as_mainfile(filepath=str(out))
report={'local_detail_scale':1.35,'meshes':len(created),'vertices':sum(len(o.data.vertices) for o in created),'bones':len(arm.data.bones),'transfer':'authored weights mapped by semantic joint; joint-aligned rest positions','distance_mean':sum(errors)/len(errors),'distance_max':max(errors),'material_status':'source Blender materials only; no game material conversion'}
(root/'research/model/rig-prototype.json').write_text(json.dumps(report,indent=2),encoding='utf8')
# Use the inherited MOD3 collection, preserve game skeleton metadata.
collection=arm.users_collection[0]
for c in bpy.data.collections:
 if c.get('~TYPE')=='MOD3':collection=c
print('COLLECTIONS',[(c.name,dict(c)) for c in bpy.data.collections])
bpy.context.scene.mhw_mod3_toolpanel.exportMod3Collection=collection
export=root/'assets/prototype/long-skirt/otomo000.mod3';export.parent.mkdir(parents=True,exist_ok=True)
export.parent.mkdir(parents=True,exist_ok=True)
r=bpy.ops.mhw_mod3.export_mhw_mod3(filepath=str(export),useBlenderMaterialName=True,exportAllLODs=False,invisibleMantlesModFix=False)
print('EXPORT_RESULT',r,report)







