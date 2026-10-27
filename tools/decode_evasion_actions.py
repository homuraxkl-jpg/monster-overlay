"""Decode only local exported action-method code windows; not a complete control-flow proof."""
from pathlib import Path
import sys,json
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root/'tools/vendor/python-research'))
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
source=Path(sys.argv[1]);report=json.loads(source.read_text(encoding='utf-8-sig'))
assert report['revision']=='421810' and report['scope']=='main_exe_executable_action_methods_only'
md=Cs(CS_ARCH_X86,CS_MODE_64);groups={}
for row in report['rows']:
 groups.setdefault(row['rva'],{'code':row['code'],'actions':[]})['actions'].append({'set':row['set'],'id':row['id'],'slot':row['slot']})
lines=['Bounded code windows only. May include adjacent methods; not all branches are necessarily included.']
for rva,value in groups.items():
 lines.append('\nMETHOD RVA '+rva+' REFERENCES '+json.dumps(value['actions']))
 for ins in md.disasm(bytes.fromhex(value['code']),int(rva,16)):lines.append(f'{ins.address:X}: {ins.mnemonic} {ins.op_str}')
for helper in report.get('helpers',[]):
 lines.append('\nCALLEE CANDIDATE RVA '+helper['rva'])
 for ins in md.disasm(bytes.fromhex(helper['code']),int(helper['rva'],16)):lines.append(f'{ins.address:X}: {ins.mnemonic} {ins.op_str}')
out=source.with_suffix('.decoded.txt');out.write_text('\n'.join(lines),encoding='utf-8');print(json.dumps({'rows':len(report['rows']),'unique_methods':len(groups),'report':str(out)}))
