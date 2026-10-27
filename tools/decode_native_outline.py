"""Decode the narrow native-outline code capture; never opens a process."""
from pathlib import Path
import json,sys
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root/'tools/vendor/python-research'))
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86 import X86_OP_MEM
source=Path(sys.argv[1]);data=json.loads(source.read_text(encoding='utf-8-sig'))
assert data.get('scope')=='main_exe_native_outline_code_only' and data.get('revision')=='421810'
md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True;rows=[]
for entry in data['functions']:
 code=bytes.fromhex(entry['code']);assert len(code)<=512
 instructions=[]
 for ins in md.disasm(code,int(entry['rva'],16)):
  fields=[{'base':ins.reg_name(op.mem.base),'offset':hex(op.mem.disp),'access':op.access} for op in ins.operands if op.type==X86_OP_MEM]
  instructions.append({'rva':hex(ins.address),'asm':ins.mnemonic+' '+ins.op_str,'memory_operands':fields})
  if ins.mnemonic in ('ret','retf','int3'):break
 rows.append({'type':entry['type'],'slot':entry['slot'],'rva':entry['rva'],'instructions':instructions,'note':'candidate method; not an approved callable interface or field definition'})
out=root/'research/face/outline-route-research/native-outline-decoded.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps({'types':data['types'],'methods':rows,'callers':data.get('callers',[])},indent=2),encoding='utf8');print('Decoded',len(rows),'candidate methods; no process access')
