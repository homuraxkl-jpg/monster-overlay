"""Build numeric reference catalogs; no game binaries or workbook HTML are shipped."""
from pathlib import Path
import json,struct,re,hashlib
root=Path(__file__).resolve().parents[1]
mapping={'l_sword':'GreatSword','sword':'SwordAndShield','w_sword':'DualBlades','tachi':'LongSword','hammer':'Hammer','whistle':'HuntingHorn','lance':'Lance','g_lance':'GunLance','s_axe':'SwitchAxe','c_axe':'ChargeBlade','rod':'InsectGlaive','bow':'Bow','hbg':'HeavyBowgun','lbg':'LightBowgun'}
source=root/'research/weapons/equipment-source';latest={}
for entry in json.loads((source/'index.json').read_text()):
    for name in entry['files']:latest[Path(name).stem]=source/Path(entry['chunk']).stem/name
equipment=[];equipment_report=[]
for name,weapon in mapping.items():
    path=latest[name];data=path.read_bytes();count=struct.unpack_from('<I',data,6)[0];stride=69 if path.suffix=='.wp_dat_g' else 66
    assert len(data)==10+count*stride
    for row in range(count):
        at=10+row*stride;index=struct.unpack_from('<I',data,at)[0];assert index==row
        raw=struct.unpack_from('<H',data,at+(25 if stride==69 else 23))[0]
        if not raw:continue
        ele=data[at+(30 if stride==69 else 28)];element=struct.unpack_from('<H',data,at+(31 if stride==69 else 29))[0]
        special=struct.unpack_from('<H',data,at+39)[0] if stride==66 else data[at+37]
        equipment.append(dict(Weapon=weapon,Row=row,Raw=raw,ElementType=ele,Element=element,Special=special))
    equipment_report.append(dict(weapon=weapon,file=str(path.relative_to(root)),sha256=hashlib.sha256(data).hexdigest(),rows=count))
tables=json.loads((root/'research/weapons/motion-source/tables.json').read_text(encoding='utf-8'))
motion=[]
for table in tables:
    headings=next((r for r in table['rows'] if 'Attack Name' in r),[])
    modifier=headings.index('Modifiers') if 'Modifiers' in headings else -1
    for row in table['rows']:
        if len(row)<5 or not row[0].isdigit() or not row[2] or not (row[3] in ('Sever','Blunt','Shot','None') or row[2].startswith('Element Phial') or any(re.search(r'\d',v) for v in row[3:8])):continue
        motion.append(dict(Sheet=table['name'],Row=int(row[0]),Name=row[2],Type=row[3],RawText=row[4],FixedText=row[7] if table['name']=='Gunlance' and len(row)>7 else '',ElementText=row[modifier+1] if modifier>=0 and len(row)>modifier+1 else '',Gid=table['gid']))
out=root/'src/WhaleCompanion';(out/'equipment-damage.json').write_text(json.dumps(equipment,separators=(',',':')),encoding='utf-8');(out/'motion-values.json').write_text(json.dumps(motion,separators=(',',':')),encoding='utf-8')
report=dict(equipment_entries=len(equipment),equipment_sources=equipment_report,motion_entries=len(motion),motion_sheets={s:sum(r['Sheet']==s for r in motion) for s in dict.fromkeys(r['Sheet'] for r in motion)},source_version='MHWI Weapon Attack Tables, Fatalis v15.01.00; runtime mappings remain candidates',authors=['MoonBunnie','Deathcream'],notes='Extracted numeric facts; raw binaries and workbook cache not packaged. Weapon row is community 421810 field; base weapon stats exclude augment/skills/current attack buffs.')
(root/'research/weapons/COMBAT-CATALOG-MANIFEST.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='equipment_sources'},ensure_ascii=False))
