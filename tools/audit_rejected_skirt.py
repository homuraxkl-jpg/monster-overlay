import bpy,json
from pathlib import Path
root=Path('D:/homura/codex/MHW')
bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working/whale-long-skirt-roundtrip.blend'))
report=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH' or not any(m.name in ['clothing','TiePen'] for m in o.data.materials):continue
 totals={};lower={};unweighted=0
 for v in o.data.vertices:
  if not v.groups:unweighted+=1
  for g in v.groups:
   name=o.vertex_groups[g.group].name;totals[name]=totals.get(name,0)+g.weight
   if v.co.z<-.15:lower[name]=lower.get(name,0)+g.weight
 report.append({'mesh':o.name,'weights':totals,'lower_skirt_weights':lower,'unweighted':unweighted})
(root/'research/model/rejected-long-skirt-weights.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(report)
