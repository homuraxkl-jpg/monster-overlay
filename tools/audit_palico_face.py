import bpy, json, struct, collections
from pathlib import Path
root=Path('D:/homura/codex/MHW')
bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working/palico-original.blend'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
report={'bones':[], 'motion_lists':[]}
for b in arm.data.bones:
 report['bones'].append({'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local),'props':{k:str(v) for k,v in b.items()}})
for f in sorted((root/'assets/original/chunkG0/otomo/common/mot').glob('*/*.lmt')):
 data=f.read_bytes();magic,version,count=struct.unpack_from('<4sHH',data)
 assert magic==b'LMT\x00' and version==95
 offsets=struct.unpack_from('<'+'Q'*count,data,16)
 entries=[];facial=collections.Counter();body=collections.Counter()
 for idx,off in enumerate(offsets):
  if not off:continue
  po,n,frames=struct.unpack_from('<QII',data,off)
  ids=[]
  for j in range(n):
   typ,usage,joint,extra,bone=struct.unpack_from('<BBBBi',data,po+j*48)
   if bone>=300:facial[bone]+=1;ids.append(bone)
   else:body[bone]+=1
  entries.append({'id':idx,'frames':frames,'facial_bones':sorted(set(ids))})
 report['motion_lists'].append({'path':str(f.relative_to(root)),'slots':count,'motions':len(entries),'face_track_counts':dict(facial),'body_track_counts':dict(body),'entries':entries})
(root/'research/face/palico-face-animation-audit.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('FACE_BONES',[(b['name'],b['parent'],[round(x,3) for x in b['head']]) for b in report['bones'] if int(b['name'].split('_')[-1])>=300])
print('MOTION_FACE_COUNTS',[(m['path'],m['motions'],len(m['face_track_counts'])) for m in report['motion_lists']])
