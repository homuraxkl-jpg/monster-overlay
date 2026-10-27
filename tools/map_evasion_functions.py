import sys,json,collections
from pathlib import Path
sys.path.insert(0,'tools/vendor/python-research')
import pefile
from capstone import *
p=Path(r'D:/steam/steamapps/common/Monster Hunter World/nativePC/plugins/CSharp/WhaleCompanion/config/combat-feedback')
r=json.loads((p/'evasion-code-xrefs.json').read_text());d=json.loads((p/'evasion-code-decoded.json').read_text());md=Cs(CS_ARCH_X86,CS_MODE_64)
pe=pefile.PE(r'D:/steam/steamapps/common/Monster Hunter World/MonsterHunterWorld.exe');funcs=[(x.struct.BeginAddress,x.struct.EndAddress) for x in pe.DIRECTORY_ENTRY_EXCEPTION]
for item in d['instructions']:
 addr=int(item['rva'],16)
 if item['memory_write_candidate'] and '0xfc4' in item['instruction'] and 'dword' in item['instruction']:
  match=next(((a,b) for a,b in funcs if a<=addr<b),None)
  print(item['rva'],item['instruction'],'FUNCTION',tuple(hex(x) for x in match) if match else None)
print('CALLS AFTER GEAR LOAD')
for item in d['instructions']:
 if 'qword ptr' not in item['instruction'] or '0x12608' not in item['instruction']:continue
 addr=int(item['rva'],16)
 row=next((x for x in r['rows'] if int(x['rva'],16)<=addr<int(x['rva'],16)+64),None)
 if not row:continue
 base=int(row['rva'],16);code=bytes.fromhex(row['code'])
 ins=list(md.disasm(code[addr-base:],addr))
 calls=[hex(x.address)+':'+x.op_str for x in ins if x.mnemonic=='call']
 if calls:print(item['rva'],','.join(calls))
