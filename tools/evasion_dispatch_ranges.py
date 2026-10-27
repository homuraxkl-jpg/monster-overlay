import sys,struct
sys.path.insert(0,'tools/vendor/python-research')
import pefile
p=pefile.PE(r'D:/steam/steamapps/common/Monster Hunter World/MonsterHunterWorld.exe');base=p.OPTIONAL_HEADER.ImageBase
for rva in [0x34ac838]:
 b=p.get_data(rva-8,80);vals=struct.unpack('<10Q',b);print('VTABLE',hex(rva),[hex(x) for x in vals])
 col=vals[0]-base
 if 0<=col<p.OPTIONAL_HEADER.SizeOfImage:
  info=struct.unpack('<6I',p.get_data(col,24));print('COL',info)
  if info[0]==1:
   print('TYPE',p.get_data(info[3]+16,160).split(b'\0')[0])
for target in [0x2080810,0x20804b0,0x19a5610,0x12dbe50,0x2079e30]:
 hit=next(((x.struct.BeginAddress,x.struct.EndAddress) for x in p.DIRECTORY_ENTRY_EXCEPTION if x.struct.BeginAddress<=target<x.struct.EndAddress),None)
 print('FUNCTION',hex(target),tuple(hex(x) for x in hit) if hit else None)
