import struct,hashlib,json
from pathlib import Path
r=Path('D:/homura/codex/MHW');b=(r/'assets/prototype/material-v1/otomo000.mod3').read_bytes();a=(r/'assets/prototype/head-pot-authored/otomo000.mod3').read_bytes()
assert hashlib.sha256(b).hexdigest()=='25117018f896f8506d779dc6ac1a3c4877b9d407c487a806d4544526101677a4'
def locate(data):
 mo=struct.unpack_from('<Q',data,72)[0];mt=struct.unpack_from('<Q',data,64)[0];vo=struct.unpack_from('<Q',data,80)[0];mc=struct.unpack_from('<H',data,8)[0];box=mo+mc*80+4
 for i in range(mc):
  o=mo+i*80;n,g,m=struct.unpack_from('<3H',data,o+2);count=data[o+41]
  if data[mt+m*128:mt+(m+1)*128].split(b'\0')[0]==b'TiePen':
   sub,vr=struct.unpack_from('<2I',data,o+16);base=struct.unpack_from('<I',data,o+36)[0]
   return o,n,vo+vr+32*(sub+base),box,count
  box+=count*144
 raise AssertionError('No pot')
o,n,v,box,count=locate(b);ao,an,av,abox,acount=locate(a);assert n==an==787 and count==acount==1
d=bytearray(b);allowed=set(range(o+4,o+6))|set(range(box,box+144));struct.pack_into('<H',d,o+4,55000)
for i in range(n):
 assert b[v+i*32+20:v+(i+1)*32]==a[av+i*32+20:av+(i+1)*32],'UV/weights ordering mismatch'
 d[v+i*32:v+i*32+12]=a[av+i*32:av+i*32+12];allowed.update(range(v+i*32,v+i*32+12))
d[box:box+144]=a[abox:abox+144]
changed=[i for i,(x,y) in enumerate(zip(b,d)) if x!=y];assert all(i in allowed for i in changed)
p=r/'assets/prototype/pot-v1/otomo000.mod3';p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(d)
h=hashlib.sha256(d).hexdigest().upper()
(r/'research/face/pot-v1-build.json').write_text(json.dumps({'sha256':h,'source_sha256':hashlib.sha256(b).hexdigest(),'mesh':3,'group':55000,'authored_shape':'TiePen=1','pot_only_vertical_fit_offset_blender':0.15,'changed_byte_count':len(changed),'changes_only_pot_positions_bbox_group':True,'all_other_meshes_byte_identical':True,'uv_normals_weights_face_indices_materials_skeleton_unchanged':True},indent=2),encoding='utf8')
print(h)
