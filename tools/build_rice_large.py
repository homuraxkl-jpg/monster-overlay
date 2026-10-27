import bpy,sys,math,json,random,hashlib,struct
from pathlib import Path
from mathutils import Matrix,Vector
r=Path('D:/homura/codex/MHW');sys.path.insert(0,str(r/'tools/vendor'));import addon_utils
bpy.ops.wm.open_mainfile(filepath=str(r/'assets/working/palico-bottle-original.blend'))
addon_utils.enable('MHW_Model_Editor-main',default_set=True,persistent=False);prefs=bpy.context.preferences.addons['MHW_Model_Editor-main'].preferences;prefs.showConsole=False;prefs.textureCachePath=str(r/'assets/working/texture-cache')
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');original=[o for o in bpy.context.scene.objects if o.type=='MESH'];template=original[0];collection=template.users_collection[0]
with bpy.data.libraries.load(str(r/'assets/working/whale-head-pot-authored.blend'),link=False) as (a,b):b.objects=[name for name in a.objects if 'Group_0_Sub_3__' in name]
source=b.objects[0];assert source and source.data.materials[0].name.split('.')[0]=='TiePen'
data=source.data.copy();data.transform(source.matrix_world)
coords=[v.co.copy() for v in data.vertices];center=Vector(tuple((min(v[k] for v in coords)+max(v[k] for v in coords))/2 for k in range(3)))
rotation=Matrix.Rotation(math.pi,4,'X')
for v in data.vertices:v.co=rotation.to_3x3()@(v.co-center)*.3+Vector((0,0,-.025))
mat=bpy.data.materials.new('rice_pot');data.materials.clear();data.materials.append(mat)
pot=template.copy();pot.data=data;collection.objects.link(pot);pot.name='Group_0_Sub_0__rice_pot';pot.vertex_groups.clear();pot.vertex_groups.new(name='MhBone_000').add(list(range(len(data.vertices))),1,'REPLACE')
# Build an opaque rice mound with individual stylized grains; no runtime simulation.
bpy.ops.mesh.primitive_uv_sphere_add(segments=10,ring_count=6,radius=1);unit=bpy.context.object;uv=[v.co.copy() for v in unit.data.vertices];uf=[tuple(p.vertices) for p in unit.data.polygons];bpy.data.objects.remove(unit,do_unlink=True)
vertices=[];faces=[]
def ellipsoid(c,s,angle,normal=None):
 off=len(vertices);rot=Matrix.Rotation(angle,3,'Z')
 if normal is not None:rot=normal.to_track_quat('Z','Y').to_matrix()@rot
 for co in uv:vertices.append(tuple(rot@Vector((co.x*s[0],co.y*s[1],co.z*s[2]))+Vector(c)))
 faces.extend(tuple(i+off for i in p) for p in uf)
ellipsoid((0,0,.024),(.115,.115,.049),0)
rng=random.Random(20261024)
for i in range(320):
 u=(i+.5)/320;radius=.115*math.sqrt(1-u*u);a=(i*2.3999632297)+rng.uniform(-.12,.12);x=radius*math.cos(a);y=radius*math.sin(a);z=.024+.049*u
 ellipsoid((x,y,z),(.009*rng.uniform(.85,1.15),.0039,.0035),rng.random()*math.tau,Vector((x/.115**2,y/.115**2,(z-.024)/.049**2)).normalized())
mesh=bpy.data.meshes.new('rice_geometry');mesh.from_pydata(vertices,[],faces);mesh.update();mesh.uv_layers.new(name='UVMap')
for item in mesh.uv_layers.active.data:item.uv=(.5,.5)
riceMat=bpy.data.materials.new('rice_white');mesh.materials.append(riceMat)
rice=template.copy();rice.data=mesh;rice.name='Group_0_Sub_1__rice_white';collection.objects.link(rice);rice.vertex_groups.clear();rice.vertex_groups.new(name='MhBone_000').add(list(range(len(mesh.vertices))),1,'REPLACE')
for obj in [pot,rice]:
 for vertex in obj.data.vertices:vertex.co *= 3.5
 for p in obj.data.polygons:p.use_smooth=True
for obj in list(bpy.context.scene.objects):
 if obj not in (arm,pot,rice):bpy.data.objects.remove(obj,do_unlink=True)
# Two meshes follow original bottle-body visibility group; original cap has no replacement.
bpy.context.scene.mhw_mod3_toolpanel.exportMod3Collection=collection
out=r/'assets/prototype/rice-v2/otomo/accessory/ot_acc000/mod';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(r/'assets/working/palico-rice-large.blend'))
result=bpy.ops.mhw_mod3.export_mhw_mod3(filepath=str(out/'ot_acc000.mod3'),useBlenderMaterialName=True,exportAllLODs=False,invisibleMantlesModFix=False);assert 'FINISHED' in result
# Preserve original one-bone bytes, including signed-zero representation.
raw=bytearray((out/'ot_acc000.mod3').read_bytes());originalRaw=(r/'assets/original/chunkG0-bottle/otomo/accessory/ot_acc000/mod/ot_acc000.mod3').read_bytes()
a0,a1=struct.unpack_from('<2Q',originalRaw,48);b0,b1=struct.unpack_from('<2Q',raw,48);assert (a0,a1)==(b0,b1)
raw[b0:b1]=originalRaw[a0:a1];(out/'ot_acc000.mod3').write_bytes(raw)
# Lit design preview with the original author's pot texture; not native shader validation.
for material,metallic,rough in [(mat,.65,.3),(riceMat,0,.65)]:
 material.use_nodes=True;bs=material.node_tree.nodes.get('Principled BSDF');bs.inputs['Metallic'].default_value=metallic;bs.inputs['Roughness'].default_value=rough
 if material==mat:
  tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(r/'assets/prototype/textures/image_5.png'));material.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
 else:bs.inputs['Base Color'].default_value=(.96,.95,.89,1)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=20;s.cycles.use_denoising=True;s.world=bpy.data.worlds.new('RicePreview');s.world.color=(.25,.25,.25)
for pos,energy,size in [((.4,-.5,.6),35,.4),((-.3,.2,.35),18,.3)]:
 bpy.ops.object.light_add(type='AREA',location=Vector(pos)*3.5);o=bpy.context.object;o.data.energy=energy*3.5**2;o.data.size=size*3.5;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(1.4,-2.1,1.47));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.0525))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.4;s.camera=cam;s.render.resolution_x=900;s.render.resolution_y=900;s.render.resolution_percentage=100
s.render.filepath=str(r/'research/rice/rice-large-design.png');bpy.ops.render.render(write_still=True)
(r/'research/rice/rice-large-build.json').write_text(json.dumps({'candidate':'otomo/accessory/ot_acc000/mod/ot_acc000.mod3','uniform_scale_vs_v1':3.5,'bones':len(arm.data.bones),'pot_vertices':len(pot.data.vertices),'rice_vertices':len(rice.data.vertices),'grains':320,'group':0,'preserves_original_animation':True,'identification':'community bottle ID; self-heal usage pending game confirmation','sha256':hashlib.sha256((out/'ot_acc000.mod3').read_bytes()).hexdigest()},indent=2),encoding='utf8')
print('RICE_BUILD_OK')

