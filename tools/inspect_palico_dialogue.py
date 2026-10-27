"""Read-only GMD inspection, layout reference: Fusion86/Cirilla (MIT).
Does not modify or install game resources.
"""
import struct, json, hashlib
from pathlib import Path

root = Path('D:/homura/codex/MHW/research/chat')
rows = []
for path in sorted((root / 'chunks').rglob('*_chS.gmd')):
    b = path.read_bytes()
    assert b[:4] == b'GMD\0'
    key_count, count, key_size, string_size, name_size = struct.unpack_from('<5I', b, 20)
    pos = b.index(0, 40) + 1
    table = [struct.unpack_from('<I', b, pos + i * 32)[0] for i in range(key_count)]
    pos += key_count * 32 + (2048 if key_count else 0)
    keys = {}
    for index in table:
        end = b.index(0, pos)
        keys[index] = b[pos:end].decode('utf-8')
        pos = end + 1
    start = pos
    entries = []
    for i in range(count):
        end = b.index(0, pos)
        entries.append(dict(index=i, key=keys.get(i), text=b[pos:end].decode('utf-8')))
        pos = end + 1
    assert pos - start == string_size and pos == len(b)
    rows.append(dict(file=str(path.relative_to(root)), sha256=hashlib.sha256(b).hexdigest(), entries=entries))
(root / 'palico-gmd-inventory.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
for row in rows:
    print(row['file'], len(row['entries']))
    for e in row['entries']:
        if any(term in e['text'] for term in ('回复药', '回復藥', '恢复', '治疗', '蜜虫', '休息')):
            print(e['index'], e['key'], e['text'].replace('\n', ' / '))
