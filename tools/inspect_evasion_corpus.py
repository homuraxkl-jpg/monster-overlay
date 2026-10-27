"""Local code-only research: validate corpus and locate candidate field consumers.
PE unwind ranges are fragments, not proof of complete function control flow.
"""
import argparse,bisect,hashlib,json,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent/'vendor/python-research'))
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64,CS_OP_MEM

def main():
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('manifest',type=Path)
 parser.add_argument('--exe',type=Path,default=Path('D:/steam/steamapps/common/Monster Hunter World/MonsterHunterWorld.exe'))
 parser.add_argument('--field',type=lambda s:int(s,0),default=0xE168)
 args=parser.parse_args();root=args.manifest.parent.resolve()
 report=json.loads(args.manifest.read_text(encoding='utf-8-sig'))
 if report.get('revision')!='421810' or report.get('scope')!='main_exe_readonly_text_section_only' or not report.get('success') or report.get('unreadable')!=0:raise ValueError('Incomplete or unsupported code corpus')
 sections=[];total=0
 for row in report['sections']:
  path=(root/row['file']).resolve()
  if path.parent!=root:raise ValueError('Section path escapes corpus')
  code=path.read_bytes();total+=len(code)
  if total>128*1024*1024 or len(code)!=row['size'] or hashlib.sha256(code).hexdigest().upper()!=row['sha256'].upper():raise ValueError('Section integrity check failed')
  sections.append((int(row['rva'],16),code))
 pe=pefile.PE(str(args.exe),fast_load=True)
 pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY['IMAGE_DIRECTORY_ENTRY_EXCEPTION']])
 spans=sorted((entry.struct.BeginAddress,entry.struct.EndAddress) for entry in pe.DIRECTORY_ENTRY_EXCEPTION);starts=[a for a,b in spans]
 md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True
 pattern=args.field.to_bytes(4,'little');matches=[];unmapped=[];seen=set()
 for base,code in sections:
  offset=0
  while True:
   hit=code.find(pattern,offset)
   if hit<0:break
   offset=hit+1;address=base+hit;index=bisect.bisect_right(starts,address)-1
   if index<0 or not(spans[index][0]<=address<spans[index][1]):unmapped.append(hex(address));continue
   begin,end=spans[index]
   if begin<base or end>base+len(code) or end-begin>65536:unmapped.append(hex(address));continue
   found=False
   for ins in md.disasm(code[begin-base:end-base],begin):
    if ins.address>address:break
    if ins.address<=address<ins.address+ins.size and any(op.type==CS_OP_MEM and op.mem.disp==args.field for op in ins.operands):
     found=True
     if ins.address not in seen:
      seen.add(ins.address);matches.append({'rva':f'{ins.address:X}','unwind_fragment':[f'{begin:X}',f'{end:X}'],'instruction':ins.mnemonic+' '+ins.op_str,'candidate_only':True})
   if not found:unmapped.append(hex(address))
 out={'field':hex(args.field),'note':'Field offset alone does not identify object ownership or successful evasion; unwind spans may be fragments.','matches':matches,'unconfirmed_byte_hits':unmapped}
 target=root/('field-'+format(args.field,'X')+'-xrefs.json');target.write_text(json.dumps(out,indent=2),encoding='utf-8')
 print(json.dumps({'confirmed_instruction_candidates':len(matches),'unconfirmed_byte_hits':len(unmapped),'output':str(target)}))
if __name__=='__main__':main()
