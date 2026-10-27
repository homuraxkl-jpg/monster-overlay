import sys,json
from pathlib import Path
sys.path.insert(0,'tools/vendor/python-research')
from capstone import *
p=Path(r'D:/steam/steamapps/common/Monster Hunter World/nativePC/plugins/CSharp/WhaleCompanion/config/combat-feedback/evasion-code-xrefs.json')
r=json.loads(p.read_text());md=Cs(CS_ARCH_X86,CS_MODE_64)
for target in [0x1190a7e,0x1190e9b,0x118f57e,0x118f620]:
 print('TARGET',hex(target))
 for row in r['rows']:
  base=int(row['rva'],16);code=bytes.fromhex(row['code'])
  if base<=target<base+len(code):
   for ins in md.disasm(code,base):print(hex(ins.address),ins.mnemonic,ins.op_str)
   break

