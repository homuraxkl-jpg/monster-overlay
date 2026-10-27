import bpy,json
from pathlib import Path
root=Path('D:/homura/codex/MHW');bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working/palico-original.blend'))
report=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 totals={}
 for v in o.data.vertices:
  for g in v.groups:
   name=o.vertex_groups[g.group].name;totals[name]=totals.get(name,0)+g.weight
 report.append({'mesh':o.name,'groups':sorted(totals.items(),key=lambda x:-x[1])[:8]})
(root/'research/model/palico-eye-binding.json').write_text(json.dumps(report,indent=2),encoding='utf8')
for r in report:
 if any(n in r['mesh'] for n in ['Mt__3','Mt__4','Mt__5']):print(r)
