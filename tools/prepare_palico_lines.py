"""Prepare an OFFLINE static-persona fallback; never install or hook the game.

GMD layout reference: Fusion86/Cirilla, MIT, pinned in THIRD-PARTY.md.
Preserve all key/hash tables and unselected strings, changing only UTF8 values
and StringBlockSize. This is not a dynamic AI dialogue interface.
"""
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def parse(data):
    if len(data) < 40 or data[:4] != b'GMD\0':
        raise ValueError('Invalid GMD header')
    key_count, count, key_size, string_size, name_size = struct.unpack_from('<5I', data, 20)
    pos = data.index(0, 40) + 1
    if pos - 41 != name_size:
        raise ValueError('Filename length mismatch')
    if key_count > count or pos + key_count * 32 + (2048 if key_count else 0) > len(data):
        raise ValueError('Invalid key table bounds')
    indices = [struct.unpack_from('<I', data, pos + i * 32)[0] for i in range(key_count)]
    if len(set(indices)) != len(indices) or any(i >= count for i in indices):
        raise ValueError('Invalid string indices')
    pos += key_count * 32 + (2048 if key_count else 0)
    key_start = pos
    keys = {}
    for index in indices:
        end = data.index(0, pos)
        key = data[pos:end].decode('utf-8', errors='strict')
        if key in keys:
            raise ValueError('Duplicate key')
        keys[key] = index
        pos = end + 1
    if pos - key_start != key_size:
        raise ValueError('Key block size mismatch')
    start = pos
    values = []
    for _ in range(count):
        end = data.index(0, pos)
        values.append(data[pos:end].decode('utf-8', errors='strict'))
        pos = end + 1
    if pos != len(data) or pos - start != string_size:
        raise ValueError('String block size mismatch')
    return start, keys, values


def patch(data, replacements):
    start, keys, values = parse(data)
    old = list(values)
    for key, text in replacements.items():
        if key not in keys:
            raise ValueError('Unknown key: ' + key)
        if not isinstance(text, str) or not text.strip() or '\0' in text:
            raise ValueError('Invalid replacement: ' + key)
        # This first fallback has no game tags or parameter substitutions.
        if '<' in text or '>' in text:
            raise ValueError('Game markup is not supported')
        values[keys[key]] = text
    block = b''.join(value.encode('utf-8') + b'\0' for value in values)
    header = bytearray(data[:start])
    struct.pack_into('<I', header, 32, len(block))
    result = bytes(header) + block
    new_start, new_keys, readback = parse(result)
    assert new_start == start and new_keys == keys and readback == values
    assert result[:32] == data[:32] and result[36:start] == data[36:start]
    changed = {i for i, (a, b) in enumerate(zip(old, readback)) if a != b}
    expected = {keys[k] for k, v in replacements.items() if old[keys[k]] != v}
    assert changed == expected
    return result, len(values), len(changed)


def main():
    config = json.loads((ROOT / 'research/chat/palico-lines-persona.json').read_text(encoding='utf-8'))
    source = ROOT / config['source']
    data = source.read_bytes()
    if hashlib.sha256(data).hexdigest() != config['source_sha256']:
        raise ValueError('Source hash mismatch; review target before rebuilding')
    replacements = {row['key']: row['replacement'] for row in config['lines']}
    if len(replacements) != len(config['lines']):
        raise ValueError('Duplicate configured key')
    result, count, changed = patch(data, replacements)
    # Dedicated research folder; no game-path or arbitrary output argument.
    output = ROOT / 'research/chat/static-persona-candidate'
    output.mkdir(exist_ok=True)
    (output / 'ot_auto_chS.gmd').write_bytes(result)
    report = dict(stage='offline_only', dynamic_ai=False, installed=False,
                  source_sha256=hashlib.sha256(data).hexdigest(),
                  output_sha256=hashlib.sha256(result).hexdigest(),
                  total_strings=count, changed_strings=changed,
                  untouched_strings=count-changed,
                  keys_and_tables_preserved=True, game_validation=False)
    (output / 'validation.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
