"""Inspect bounded function/caller context from the local mantle code export."""
import sys,json
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root/'tools/vendor/python-research'))
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
source=Path(sys.argv[1]);r=json.loads(source.read_text(encoding='utf-8-sig'))
assert r['revision']=='421810' and r['scope']=='main_exe_executable_sections'
md=Cs(CS_ARCH_X86,CS_MODE_64);lines=['CANDIDATE CODE ONLY: function ownership and live target address not yet validated.']
for group in ('focus','callers'):
 for row in r.get(group,[]):
  base=int(row['rva'],16);code=bytes.fromhex(row['code']);lines.append(f'\n{group.upper()} RVA {base:X}')
  # Function starts come from PE exception metadata; caller windows may be unaligned.
  if group=='focus':
   for ins in md.disasm(code,base):lines.append(f'{ins.address:X}: {ins.mnemonic} {ins.op_str}')
  else:
   lines.append('PRECEDING WINDOW '+code[:row['call_offset']].hex())
   for ins in md.disasm(code[row['call_offset']:],base+row['call_offset']):lines.append(f'{ins.address:X}: {ins.mnemonic} {ins.op_str}')
output=source.with_name('evasion-function-context.txt');output.write_text('\n'.join(lines),encoding='utf-8')
print(json.dumps({'focus_count':len(r.get('focus',[])),'caller_count':len(r.get('callers',[])),'report':str(output)}))
