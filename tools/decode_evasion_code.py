"""Decode local loaded-code export; never attaches to or modifies a process."""
from pathlib import Path
import sys,json
root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'tools/vendor/python-research'))
from capstone import Cs,CS_ARCH_X86,CS_MODE_64,CS_AC_WRITE
from capstone.x86 import X86_OP_MEM
source=Path(sys.argv[1]);report=json.loads(source.read_text(encoding='utf-8-sig'))
assert report.get('scope')=='main_exe_executable_sections' and report.get('revision')=='421810'
md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True;found={}
for row in report['rows']:
 code=bytes.fromhex(row['code']);base=int(row['rva'],16);hit=base+row['hit_offset']
 for start in range(row['hit_offset']):
  for ins in md.disasm(code[start:],base+start):
   if ins.address>hit:break
   targets=[op for op in ins.operands if op.type==X86_OP_MEM and op.mem.disp in (0xFC4,0x12608)]
   if targets:
    found[ins.address]={'rva':hex(ins.address),'end_rva':hex(ins.address+ins.size),'size':ins.size,'instruction':ins.mnemonic+' '+ins.op_str,'memory_write_candidate':any(op.access&CS_AC_WRITE for op in targets),'note':'instruction alignment and function ownership require independent verification'}
groups={}
for value in found.values():
 key=value['end_rva']
 if key not in groups or value['size']>groups[key]['size']:groups[key]=value
primary=list(groups.values())
output=source.with_name('evasion-code-decoded.json')
output.write_text(json.dumps({'candidate_only':True,'source_hits':report['hits'],'unreadable':report['unreadable'],'truncated':report['truncated'],'instructions':primary,'alignment_alternatives':list(found.values()),'alignment_selection':'longest decoding with same end; heuristic only'},indent=2),encoding='utf-8')
print(json.dumps({'decoded_candidates':len(primary),'write_candidates':sum(x['memory_write_candidate'] for x in primary),'report':str(output)}))

