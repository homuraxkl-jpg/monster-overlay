"""Split authored tear cards into a dedicated material without changing any vertex attributes."""
import struct,json,hashlib
from pathlib import Path
root=Path('D:/homura/codex/MHW');src=root/'assets/prototype/face-runtime-v3/otomo000.mod3'
b=src.read_bytes();assert hashlib.sha256(b).hexdigest()=='17331209f02ccca8d7a7af341c370456dbf448eb0f1115aff03713a6315461c9'
mc,matc=struct.unpack_from('<2H',b,8);vc,fc=struct.unpack_from('<2I',b,12);vbsize=struct.unpack_from('<Q',b,24)[0]
mat_off,mesh_off,vert_off,face_off,remap_off=struct.unpack_from('<5Q',b,64)
assert matc==6 and mc==129 and mesh_off==mat_off+matc*128
bbcount=struct.unpack_from('<I',b,mesh_off+mc*80)[0];assert vert_off==mesh_off+mc*80+4+bbcount*144
old_vb=b[vert_off:vert_off+vbsize];old_bbs=b[mesh_off+mc*80+4:vert_off]
infos=[];indices=[];new_infos=[];new_indices=[];extra_vb=bytearray();extra_bbs=bytearray();bb_cursor=0;rows=[]
for i in range(mc):
 info=bytearray(b[mesh_off+i*80:mesh_off+(i+1)*80]);n,group,mat=struct.unpack_from('<3H',info,2)
 sub,vrel,typ,before,nindex,base=struct.unpack_from('<6I',info,16);assert info[14]==32
 name=b[mat_off+mat*128:mat_off+(mat+1)*128].split(b'\0')[0].decode()
 faces=list(struct.iter_unpack('<3H',b[face_off+before*2:face_off+(before+nindex)*2]))
 count=info[41];bbox=old_bbs[bb_cursor*144:(bb_cursor+count)*144];bb_cursor+=count
 if name=='expression':
  assert n==56 and count==1
  tears=[t for t in faces if all(48<=v-sub<=55 for v in t)];keep=[t for t in faces if t not in tears]
  assert len(tears)==4 and len(keep)==24
  faces=keep;struct.pack_into('<I',info,32,len(keep)*3)
  clone=bytearray(info);struct.pack_into('<H',clone,2,8);struct.pack_into('<H',clone,6,6)
  struct.pack_into('<2I',clone,16,0,vbsize+len(extra_vb));struct.pack_into('<I',clone,32,12);struct.pack_into('<I',clone,36,0)
  struct.pack_into('<3H',clone,42,mc+len(new_infos),0,7);struct.pack_into('<I',clone,60,vc+len(extra_vb)//32)
  vs=vert_off+vrel+32*(sub+base)
  for v in range(48,56):extra_vb.extend(b[vs+v*32:vs+(v+1)*32])
  new_infos.append(clone);new_indices.append([(a-sub-48,c-sub-48,d-sub-48) for a,c,d in tears]);extra_bbs.extend(bbox)
  rows.append({'group':group,'material':'whale_tears','vertices':8,'triangles':4})
 infos.append(info);indices.append(faces)
assert len(rows)==41 and bb_cursor==bbcount
infos+=new_infos;indices+=new_indices
fb=bytearray();running=0
for info,faces in zip(infos,indices):
 struct.pack_into('<I',info,28,running)
 for tri in faces:fb.extend(struct.pack('<3H',*tri))
 running+=len(faces)*3
assert running==fc
fb.extend(bytes((-len(fb))%4));assert len(fb)==remap_off-face_off
header=bytearray(b[:mat_off]);new_mat=b'material_placeholder' # overwritten below
materials=b[mat_off:mesh_off]+b'whale_tears'+bytes(128-len('whale_tears'))
new_mesh_off=mat_off+len(materials);new_vert_off=new_mesh_off+len(infos)*80+4+len(old_bbs)+len(extra_bbs)
new_face_off=new_vert_off+len(old_vb)+len(extra_vb);new_remap_off=new_face_off+len(fb)
struct.pack_into('<2H',header,8,len(infos),7);struct.pack_into('<I',header,12,vc+len(extra_vb)//32);struct.pack_into('<Q',header,24,len(old_vb)+len(extra_vb))
struct.pack_into('<4Q',header,72,new_mesh_off,new_vert_off,new_face_off,new_remap_off)
# Unused-offset zero remains zero; any post-face absolute pointer must follow the shift.
unk=struct.unpack_from('<Q',header,104)[0]
if unk>=remap_off:struct.pack_into('<Q',header,104,unk+(new_remap_off-remap_off))
out=bytes(header)+materials+b''.join(infos)+struct.pack('<I',bbcount+41)+old_bbs+extra_bbs+old_vb+extra_vb+fb+b[remap_off:]
assert out[new_vert_off:new_vert_off+vbsize]==old_vb
assert out[48:72]==b[48:72] # bone/group/material start pointers unchanged
# Original skeleton/group bytes are copied exactly. Existing material names retain their indices.
assert out[320:mat_off]==b[320:mat_off]
path=root/'assets/prototype/material-v1/otomo000.mod3';path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(out)
report={'model_sha256':hashlib.sha256(out).hexdigest(),'source':'face-runtime-v3 (authored mirrored normals restored; graphics now requires double-sided MRL3)','old_vertex_buffer_byte_identical':True,'skeleton_and_groups_byte_identical':True,'mesh_count':len(infos),'material_count':7,'tear_splits':rows,'new_vertex_bytes':len(extra_vb)}
(root/'research/face/material-v1-model.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('TEAR_SPLIT_OK',report['model_sha256'],'meshes',len(infos))
