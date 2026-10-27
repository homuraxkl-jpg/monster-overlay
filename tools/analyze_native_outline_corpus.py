"""Reuse an audited local executable-code cache; never opens the game process or uploads code."""
from pathlib import Path
import sys,json,hashlib,re,struct
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root/'tools/vendor/python-research'))
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
capture=Path(sys.argv[1]);corpus=Path(sys.argv[2]);fresh=json.loads(capture.read_text(encoding='utf-8-sig'));manifest=json.loads((corpus/'manifest.json').read_text(encoding='utf-8-sig'))
assert fresh['revision']==manifest['revision']=='421810' and fresh['scope']=='main_exe_native_outline_code_only' and manifest['scope']=='main_exe_readonly_text_section_only'
sections=[]
for row in manifest['sections']:
 name=row['file'];assert Path(name).name==name and '/' not in name and '\\' not in name
 data=(corpus/name).read_bytes();assert len(data)==row['size'] and hashlib.sha256(data).hexdigest().upper()==row['sha256'].upper();sections.append((int(row['rva'],16),data))
def read(rva,n):
 for start,data in sections:
  if start<=rva and rva+n<=start+len(data):return data[rva-start:rva-start+n]
 raise ValueError('RVA outside code corpus')
for row in fresh['functions']:
 code=bytes.fromhex(row['code']);assert read(int(row['rva'],16),len(code))==code,'Cache does not match current outline capture'
targets={t['TableRva']:(t['Name'],'vtable') for t in fresh['types']};targets.update({t['DtiRva']:(t['Name'],'DTI') for t in fresh['types']});refs=[];md=Cs(CS_ARCH_X86,CS_MODE_64)
for start,data in sections:
 for match in re.finditer(b'[\x48\x4c]\x8d[\x05\x0d\x15\x1d\x25\x2d\x35\x3d]',data):
  i=match.start()
  if i+7>len(data):continue
  target=start+i+7+struct.unpack_from('<i',data,i+3)[0]
  if target not in targets:continue
  name,kind=targets[target];asm=[f'{ins.address:X}: {ins.mnemonic} {ins.op_str}' for ins in md.disasm(data[i:i+192],start+i)]
  refs.append({'type':name,'kind':kind,'rva':hex(start+i),'instructions_from_reference':asm,'candidate_context_only':True})
output=root/'research/face/outline-route-research/native-outline-corpus-analysis.json';output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps({'revision':'421810','current_capture_methods_matched':len(fresh['functions']),'types':fresh['types'],'references':refs,'limitations':'Only static code/metadata evidence. cpModelOutline reference tracking is not proven renderer activation. No live filters or Palico components observed by this tool.'},indent=2),encoding='utf8')
print('Matched all',len(fresh['functions']),'current methods; found',len(refs),'candidate type references; no process access')
