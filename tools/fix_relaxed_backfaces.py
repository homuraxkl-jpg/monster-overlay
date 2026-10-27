"""Fix only the mirrored relaxed eye/blush cards; preserve all other MOD3 bytes."""
import struct,json,hashlib,math
from pathlib import Path
root=Path('D:/homura/codex/MHW')
source=root/'assets/prototype/face-runtime-v3/otomo000.mod3'
original=source.read_bytes();data=bytearray(original)
assert hashlib.sha256(original).hexdigest()=='17331209f02ccca8d7a7af341c370456dbf448eb0f1115aff03713a6315461c9'
mesh_count=struct.unpack_from('<H',data,8)[0]
mat_off,mesh_off,vert_off,face_off=struct.unpack_from('<4Q',data,64)
meshes=[]
for i in range(mesh_count):
 off=mesh_off+i*80;vc,group,mat=struct.unpack_from('<3H',data,off+2)
 name=data[mat_off+mat*128:mat_off+(mat+1)*128].split(b'\0')[0].decode()
 block=data[off+14];sub,relative,typ,before,count,base=struct.unpack_from('<6I',data,off+16)
 meshes.append({'group':group,'mat':name,'vertices':vc,'block':block,'vertex_start':vert_off+relative+block*(sub+base),'face_start':face_off+2*before,'indices':count,'sub':sub})
neutral=next(m for m in meshes if m['group']==56000 and m['mat']=='expression')
full=next(m for m in meshes if m['group']==56040 and m['mat']=='expression')
assert neutral['vertices']==full['vertices']==56 and full['block']==32
# Discover the moved, mirrored back-facing cards from positions and packed normals.
selected=set()
for i in range(56):
 a=neutral['vertex_start']+i*32;b=full['vertex_start']+i*32
 p=struct.unpack_from('<3f',data,b);p0=struct.unpack_from('<3f',data,a)
 n=struct.unpack_from('<3b',data,b+12)
 if p[0]<0 and math.dist(p,p0)>1 and n[2]<-60:selected.add(i)
assert selected==set(range(24,28))|set(range(36,40)),selected
allowed=set();rows=[]
for mesh in meshes:
 if not(56033<=mesh['group']<=56040 and mesh['mat']=='expression'):continue
 assert mesh['vertices']==56 and mesh['block']==32
 for i in selected:
  start=mesh['vertex_start']+i*32
  n=struct.unpack_from('<3b',data,start+12)
  assert n[2]<0
  struct.pack_into('<3b',data,start+12,*[max(-127,min(127,-v)) for v in n])
  # Reversing normal direction also reverses tangent-space handedness; keep UV tangent xyz.
  handed=struct.unpack_from('<b',data,start+19)[0];assert abs(handed)==127
  struct.pack_into('<b',data,start+19,-handed)
  allowed.update(range(start+12,start+15));allowed.add(start+19)
 flipped=0
 for index in range(0,mesh['indices'],3):
  addr=mesh['face_start']+index*2;tri=struct.unpack_from('<3H',data,addr)
  local={v-mesh['sub'] for v in tri}
  if local<=selected:
   struct.pack_into('<3H',data,addr,tri[0],tri[2],tri[1]);flipped+=1
   allowed.update(range(addr,addr+6))
 assert flipped==4,(mesh['group'],flipped)
 rows.append({'group':mesh['group'],'vertices':sorted(selected),'triangles_reversed':flipped})
assert len(rows)==8
changed=[i for i,(a,b) in enumerate(zip(original,data)) if a!=b]
assert changed and set(changed)<=allowed and len(data)==len(original)
# Explicitly assert every other group's full vertex/index payload is identical.
unchanged=[]
for mesh in meshes:
 if 56033<=mesh['group']<=56040 and mesh['mat']=='expression':continue
 for start,size in [(mesh['vertex_start'],mesh['vertices']*mesh['block']),(mesh['face_start'],mesh['indices']*2)]:
  assert original[start:start+size]==data[start:start+size],(mesh['group'],mesh['mat'])
 unchanged.append((mesh['group'],mesh['mat']))
out=root/'assets/prototype/face-runtime-v4/otomo000.mod3';out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(data)
report={'source_sha256':hashlib.sha256(original).hexdigest(),'model_sha256':hashlib.sha256(data).hexdigest(),'changed_bytes':len(changed),'file_size_unchanged':True,'allowed_scope_only':True,'unchanged_mesh_payloads':len(unchanged),'corrected':rows,'other_expressions_and_body_byte_identical':True}
(root/'research/face/relaxed-fix-build.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('RELAXED_ONLY_PATCH_OK',report['model_sha256'],'changed bytes',len(changed))
