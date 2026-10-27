import numpy as np

def smooth_leg_weights(objects, iterations=18):
 names=['MhBone_014','MhBone_015','MhBone_016','MhBone_018','MhBone_019','MhBone_020']
 for o in objects:
  if o.type!='MESH' or not any(m.name=='body' for m in o.data.materials):continue
  lookup={g.index:g.name for g in o.vertex_groups};n=len(o.data.vertices);weights=np.zeros((n,6));adj=[[] for _ in range(n)]
  for v in o.data.vertices:
   for g in v.groups:
    name=lookup[g.group]
    if name in names:weights[v.index,names.index(name)]=g.weight
  for e in o.data.edges:
   a,b=e.vertices;adj[a].append(b);adj[b].append(a)
  original=weights.copy();mass=weights.sum(axis=1)
  mask=np.array([-.49<v.co.z<-.10 and mass[v.index]>.8 for v in o.data.vertices])
  for _ in range(iterations):
   result=weights.copy()
   for i in np.flatnonzero(mask):
    neighbors=[j for j in adj[i] if mass[j]>.8]
    if neighbors:result[i]=weights[i]*.5+weights[neighbors].mean(axis=0)*.4+original[i]*.1
   totals=result.sum(axis=1);weights=result*np.divide(mass,totals,out=np.zeros_like(mass),where=totals>0)[:,None]
  for i in np.flatnonzero(mask):
   for j,name in enumerate(names):
    group=o.vertex_groups.get(name)
    if group:
     if weights[i,j]>1e-6:group.add([int(i)],float(weights[i,j]),'REPLACE')
     else:group.remove([int(i)])
