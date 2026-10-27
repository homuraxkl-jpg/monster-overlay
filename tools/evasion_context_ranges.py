import sys
sys.path.insert(0,'tools/vendor/python-research')
import pefile
p=pefile.PE(r'D:/steam/steamapps/common/Monster Hunter World/MonsterHunterWorld.exe')
for target in [0x11ae374,0x2077b4a,0x118fb20,0x1f631c0]:
 for entry in p.DIRECTORY_ENTRY_EXCEPTION:
  a,b=entry.struct.BeginAddress,entry.struct.EndAddress
  if a<=target<b:print(hex(target),hex(a),hex(b),hex(b-a));break
