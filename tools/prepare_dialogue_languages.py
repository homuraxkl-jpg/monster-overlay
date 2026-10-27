"""Build language upgrade assets without touching game or existing player edits."""
import hashlib
import json
from pathlib import Path
from prepare_palico_lines import parse
from palico_dialogue import build

ROOT = Path(__file__).resolve().parents[1]

def prepare():
    base = ROOT / 'research/chat'
    traditional = json.loads((base / 'offline-default-pack/ot_auto_chT.json').read_text(encoding='utf-8-sig'))
    source = (base / 'chunks/chunkG0/common/text/ot_auto_chS.gmd').read_bytes()
    _, keys, values = parse(source)
    assert set(keys) == {line['key'] for line in traditional['entries']}
    entries = []
    for row in traditional['entries']:
        index = keys[row['key']]
        entries.append(dict(key=row['key'], index=index, original=values[index], replacement=row['replacement']))
    # Transfer authored replacements by key, never overwrite retained speakers with another language.
    catalog = dict(traditional, source_file='ot_auto_chS.gmd', source_sha256=hashlib.sha256(source).hexdigest(), entries=entries)
    replacements = {row['key']:row['replacement'] for row in entries if row['replacement'] is not None}
    data, report = build(source, catalog, 'ot_auto_chS.gmd')
    stage = ROOT / 'dist/DialogueLanguages'
    config = stage / 'config/dialogue'
    for path in (config, config/'source', config/'generated', config/'defaults', stage/'玩家自定义', stage/'OfflineTools'):
        path.mkdir(parents=True, exist_ok=True)
    (config/'ot_auto_chS.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (config/'source/ot_auto_chS.gmd').write_bytes(source)
    (config/'generated/ot_auto_chS.gmd').write_bytes(data)
    (config/'generated/ot_auto_chS.validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    # Existing chT catalog, text and persona are deliberately not included in this upgrade overlay.
    (stage/'玩家自定义/台词语言.txt').write_text('繁體中文\n',encoding='utf-8-sig')
    print(f'Prepared {len(entries)} chS rows, {len(replacements)} authored replacements; original speakers retain chS.')

if __name__ == '__main__':
    prepare()
