import bpy,hashlib,struct,json
from pathlib import Path
root=Path('D:/homura/codex/MHW');report=[];baseline=None
for label,file in [('neutral','whale-palico-rig-prototype.blend'),('blink','whale-face-blink.blend'),('happy','whale-face-happy.blend')]:
 bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working'/file))
 hashes={}
 for o in bpy.context.scene.objects:
  if o.type!='MESH' or any(m.name in ['head','expression'] for m in o.data.materials):continue
  h=hashlib.sha256()
  for v in o.data.vertices:
   h.update(struct.pack('<3f',*v.co))
   for g in v.groups:h.update(o.vertex_groups[g.group].name.encode());h.update(struct.pack('<f',g.weight))
  hashes[o.name]=h.hexdigest()
 if baseline is None:baseline=hashes
 else:assert hashes==baseline,('Non-face mesh changed',label)
 report.append({'expression':label,'non_face_hashes':hashes,'body_unchanged':True})
(root/'research/face/body-lock-verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('BODY_LOCK_CHECK_OK')
