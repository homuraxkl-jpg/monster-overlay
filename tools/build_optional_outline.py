"""Repeatable append-only outline candidate. Never rewrites original mesh payloads or the source VRM."""
import hashlib,json,math,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'assets/prototype/pot-v1/otomo000.mod3'
EXPECTED='4AF7D138E38BAF53D528FD6E38B59C10BC9C59A89097432B44D2A1426EFC09A7'
def sha(data):return hashlib.sha256(data).hexdigest().upper()
def build():
 b=BASE.read_bytes();assert sha(b)==EXPECTED,'Unrecognized approved baseline'
 count,materials=struct.unpack_from('<HH',b,8);groups=struct.unpack_from('<I',b,32)[0]
 go,mt,mo,vo,fo,ro=struct.unpack_from('<6Q',b,56)
 names=[b[mt+i*128:mt+(i+1)*128].split(b'\0')[0].decode() for i in range(materials)]
 assert set(names)=={'body','clothing','hair','head','TiePen','expression','whale_tears'}
 vertices=bytearray(b[vo:fo]);faces=bytearray(b[fo:ro]);old_vertices=bytes(vertices);old_faces=bytes(faces)
 bb_start=mo+count*80;bb_count=struct.unpack_from('<I',b,bb_start)[0];old_boxes=b[bb_start+4:vo];assert len(old_boxes)==bb_count*144
 mesh_headers=bytearray(b[mo:bb_start]);boxes=bytearray(old_boxes);group_ids=set();box_cursor=0;added_vertices=0;mapping=[]
 for i in range(count):
  header=bytearray(b[mo+i*80:mo+(i+1)*80]);n,group,material=struct.unpack_from('<3H',header,2);box_n=header[41]
  source_boxes=old_boxes[box_cursor*144:(box_cursor+box_n)*144];box_cursor+=box_n
  if names[material] not in ('body','clothing','hair','head','TiePen'):continue
  target_group=57000 if group==0 else 57001 if group==55000 else 57010+(group-56000)
  assert target_group in (57000,57001) or 57010<=target_group<=57050
  group_ids.add(target_group);stride=header[14];assert stride==32
  sub,relative=struct.unpack_from('<2I',header,16);base=struct.unpack_from('<I',header,36)[0]
  first=relative+stride*(sub+base);block=bytearray(old_vertices[first:first+n*stride]);assert len(block)==n*stride
  width=.11 if names[material]=='head' else .16
  for v in range(n):
   p=struct.unpack_from('<3f',block,v*stride);normal=struct.unpack_from('<3b',block,v*stride+12);length=math.sqrt(sum(x*x for x in normal))
   # Degenerate/zero authored normals stay undisplaced; never guess or alter the approved source.
   if length==0:length=1
   struct.pack_into('<3f',block,v*stride,*(p[k]+width*normal[k]/length for k in range(3)))
   struct.pack_into('<3b',block,v*stride+12,*(max(-127,min(127,-x)) for x in normal))
   assert block[v*stride+15:(v+1)*stride]==old_vertices[first+v*stride+15:first+(v+1)*stride], 'UV/tangent/weight bytes changed'
  before,index_count=struct.unpack_from('<2I',header,28);assert index_count%3==0
  indices=list(struct.unpack_from('<'+'H'*index_count,old_faces,before*2));indices=[x-sub for x in indices];assert all(0<=x<n for x in indices)
  for j in range(0,len(indices),3):indices[j+1],indices[j+2]=indices[j+2],indices[j+1]
  struct.pack_into('<H',header,0,0) # outline shells do not add a second character shadow
  struct.pack_into('<2H',header,4,target_group,materials)
  struct.pack_into('<2I',header,16,0,len(vertices));struct.pack_into('<2I',header,28,len(faces)//2,index_count);struct.pack_into('<I',header,36,0)
  struct.pack_into('<3H',header,42,count+len(mapping),0,n-1);struct.pack_into('<I',header,60,struct.unpack_from('<I',b,12)[0]+added_vertices)
  vertices.extend(block);faces.extend(struct.pack('<'+'H'*len(indices),*indices));mesh_headers.extend(header);boxes.extend(source_boxes);added_vertices+=n
  mapping.append({'source_mesh':i,'source_group':group,'outline_group':target_group,'material':names[material],'width_game_units':width,'vertices':n})
 group_data=bytearray(b[go:mt]);assert len(group_data)==groups*32
 # Bounds reuse approved group spheres; outlines are very thin and use the same bone-bound entries.
 for gid in sorted(group_ids):
  entry=bytearray(group_data[:32]);struct.pack_into('<I',entry,0,gid);group_data.extend(entry)
 material_data=b[mt:mo]+b'whale_outline'+bytes(128-len('whale_outline'))
 prefix=bytearray(b[:go]);new_mt=len(prefix)+len(group_data);new_mo=new_mt+len(material_data);new_vo=new_mo+len(mesh_headers)+4+len(boxes)
 if len(faces)%4:faces.extend(bytes(4-len(faces)%4))
 new_fo=new_vo+len(vertices);new_ro=new_fo+len(faces)
 struct.pack_into('<2H',prefix,8,count+len(mapping),materials+1);struct.pack_into('<I',prefix,12,struct.unpack_from('<I',b,12)[0]+added_vertices)
 struct.pack_into('<I',prefix,16,len(faces)//2);struct.pack_into('<Q',prefix,24,len(vertices));struct.pack_into('<I',prefix,32,groups+len(group_ids))
 struct.pack_into('<5Q',prefix,64,new_mt,new_mo,new_vo,new_fo,new_ro)
 result=bytes(prefix)+bytes(group_data)+material_data+bytes(mesh_headers)+struct.pack('<I',len(boxes)//144)+bytes(boxes)+bytes(vertices)+bytes(faces)+b[ro:]
 # Original geometry, skinning, faces, mesh headers, materials and skeleton are byte-identical.
 assert result[new_vo:new_vo+len(old_vertices)]==old_vertices
 assert result[new_fo:new_fo+len(old_faces)]==old_faces
 assert result[new_mo:new_mo+count*80]==b[mo:mo+count*80]
 assert result[320:go]==b[320:go]
 assert result[new_mo+(count+len(mapping))*80+4:new_vo][:len(old_boxes)]==old_boxes
 dest=ROOT/'assets/prototype/appearance-v1';dest.mkdir(parents=True,exist_ok=True);(dest/'otomo000.mod3').write_bytes(result)
 report={'baseline_sha256':sha(b),'model_sha256':sha(result),'original_meshes':count,'added_outline_meshes':len(mapping),'original_geometry_weights_uv_normals_faces_skeleton_byte_identical':True,'source_vrm_written':False,'outline_groups':sorted(group_ids),'mapping':mapping}
 (ROOT/'research/face/appearance-v1-model.json').write_text(json.dumps(report,indent=2),encoding='utf8')
 print('OUTLINE_APPEND_ONLY_OK',count,len(mapping),sha(result))
if __name__=='__main__':build()
