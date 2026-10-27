import json,datetime,collections
p=r'D:/steam/steamapps/common/Monster Hunter World/WhaleCompanion-p0-diagnostics.log'
events=[];trans=[];stats=[]
for line in open(p,encoding='utf-8-sig'):
 if not line.startswith('2026-10-25T23:1') and not line.startswith('2026-10-25T23:2'):continue
 for tag,dest in [('EVASION_COLLISION_CANDIDATE ',events),('EVASION_MANTLE_TRANSITION ',trans),('EVASION_COLLISION_STATS ',stats)]:
  if tag in line:
   dest.append((line.split()[0],json.loads(line.split(tag,1)[1])))
print('COUNTS',collections.Counter(x[1]['candidate']['EventKind'] for x in events))
print('STATS',stats[-1:] if stats else [])
print('MANTLE',[(t,v['change']) for t,v in trans])
print('TYPE8',[(t,v['candidate']['ExclusionType']) for t,v in events if v['candidate']['EventKind']==8])
