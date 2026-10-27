"""Read-only static FC4 xrefs. Candidate decoding is not runtime validation."""
from pathlib import Path
import sys,hashlib,json,struct
root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'tools/vendor/python-research'))
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86 import X86_OP_MEM
path=Path(r'D:/steam/steamapps/common/Monster Hunter World/MonsterHunterWorld.exe')
data=path.read_bytes(); pe=pefile.PE(data=data,fast_load=True)
md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True
found={}
for section in pe.sections:
 if not section.Characteristics&0x20000000:continue
 raw=section.get_data();pos=0
 while True:
  at=raw.find(struct.pack('<I',0xFC4),pos)
  if at<0:break
  pos=at+1
  for start in range(max(0,at-12),at):
   for ins in md.disasm(raw[start:at+20],pe.OPTIONAL_HEADER.ImageBase+section.VirtualAddress+start):
    if ins.address>pe.OPTIONAL_HEADER.ImageBase+section.VirtualAddress+at:break
    if any(op.type==X86_OP_MEM and op.mem.disp==0xFC4 for op in ins.operands):
     found[ins.address]={'rva':hex(ins.address-pe.OPTIONAL_HEADER.ImageBase),'instruction':ins.mnemonic+' '+ins.op_str,'bytes':ins.bytes.hex()}
report={'exe_sha256':hashlib.sha256(data).hexdigest(),'candidate_only':True,'xrefs':list(found.values())}
out=root/'research/weapons/evasion-fc4-static.json';out.write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({'candidate_count':len(found),'float_candidates':[v for v in found.values() if 'ss ' in v['instruction']],'report':str(out)},indent=2))
