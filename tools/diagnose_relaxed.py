import bpy,json
from pathlib import Path
from collections import defaultdict
root=Path('D:/homura/codex/MHW')
reports={}
for label,file in [('neutral','whale-palico-rig-prototype.blend'),('relaxed','whale-face-relaxed.blend')]:
 bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working'/file));rows=[]
 for o in bpy.context.scene.objects:
  if o.type!='MESH' or not any(m.name in ('head','expression') for m in o.data.materials):continue
  adjacency=[set() for _ in o.data.vertices]
  for e in o.data.edges:
   a,b=e.vertices;adjacency[a].add(b);adjacency[b].add(a)
  seen=set();comps=[]
  for i in range(len(adjacency)):
   if i in seen:continue
   todo=[i];ids=[];seen.add(i)
   while todo:
    x=todo.pop();ids.append(x)
    for n in adjacency[x]:
     if n not in seen:seen.add(n);todo.append(n)
   coords=[o.data.vertices[j].co for j in ids]
   normals=[p.normal for p in o.data.polygons if p.vertices[0] in set(ids)]
   comps.append({'vertex_count':len(ids),'first':min(ids),'ids':ids,'lo':[min(c[k] for c in coords) for k in range(3)],'hi':[max(c[k] for c in coords) for k in range(3)],'normal_avg':[sum(n[k] for n in normals)/max(1,len(normals)) for k in range(3)]})
  rows.append({'name':o.name,'mat':[m.name for m in o.data.materials],'components':comps})
 reports[label]=rows
(root/'research/face/relaxed-components.json').write_text(json.dumps(reports,indent=2),encoding='utf8')
for label,rows in reports.items():
 print(label)
 for row in rows:
  print(row['name'],row['mat'])
  for c in row['components']:
   if c['vertex_count']<1000:print(c['first'],c['vertex_count'],[round(x,4) for x in c['lo']],[round(x,4) for x in c['hi']],[round(x,2) for x in c['normal_avg']])
