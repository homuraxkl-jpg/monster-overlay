"""Player-editable offline dialogue catalog. Standard library only, no network.

Export from the player's GMD; edit only replacement; build into a separate GMD.
Never installs files, changes the DLL, or modifies its source GMD.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path

from prepare_palico_lines import parse


def sha(data):
    return hashlib.sha256(data).hexdigest()


def tokens(text):
    # Preserve game tags and printf/brace parameters in original order.
    pattern = r'<[^<>]*>|\{[^{}]*\}|%(?:%|(?:\d+\$)?[-+ #0]*\d*(?:\.\d+)?(?:hh|ll|[hljztL])?[diuoxXfFeEgGaAcspn])'
    return re.findall(pattern, text)


def export_catalog(data, filename):
    _, keys, values = parse(data)
    return dict(format_version=1, source_file=filename, source_sha256=sha(data),
                note='Edit replacement only; null keeps original. Preserve tags, parameters and event meaning.',
                entries=[dict(key=key, index=index, original=values[index], replacement=None)
                         for key, index in sorted(keys.items(), key=lambda pair: pair[1])])


def build(data, catalog, filename):
    if not isinstance(catalog, dict):
        raise ValueError('Catalog must be a JSON object')
    if catalog.get('format_version') != 1:
        raise ValueError('Unsupported catalog version')
    if catalog.get('source_file') != filename or catalog.get('source_sha256') != sha(data):
        raise ValueError('Source file/hash mismatch; export this game language again')
    start, keys, values = parse(data)
    original = list(values)
    seen = set()
    changed = []
    if not isinstance(catalog.get('entries'), list):
        raise ValueError('entries must be an array')
    for row in catalog['entries']:
        if not isinstance(row, dict):
            raise ValueError('Each entry must be a JSON object')
        key = row.get('key')
        if key not in keys or key in seen:
            raise ValueError('Unknown or duplicate key: ' + str(key))
        seen.add(key)
        index = keys[key]
        if row.get('index') != index or row.get('original') != original[index]:
            raise ValueError('Immutable index/original changed: ' + key)
        text = row.get('replacement')
        if text is None:
            continue
        if not isinstance(text, str) or not text.strip() or '\0' in text:
            raise ValueError('Replacement must be nonempty text without NUL: ' + key)
        if any(ord(c) < 32 and c not in '\r\n\t' for c in text):
            raise ValueError('Unsupported control character: ' + key)
        if len(text.encode('utf-8')) > 512:
            raise ValueError('Replacement exceeds conservative tool limit of 512 UTF8 bytes: ' + key)
        if tokens(text) != tokens(original[index]):
            raise ValueError('Game tags/parameters changed or reordered: ' + key)
        if ('<' in text or '>' in text) and not re.fullmatch(r'[^<>]*(?:<[^<>]*>[^<>]*)*', text):
            raise ValueError('Malformed game markup: ' + key)
        if text != original[index]:
            values[index] = text
            changed.append(key)
    block = b''.join(value.encode('utf-8') + b'\0' for value in values)
    prefix = bytearray(data[:start])
    struct.pack_into('<I', prefix, 32, len(block))
    result = bytes(prefix) + block
    new_start, new_keys, readback = parse(result)
    if new_start != start or new_keys != keys or readback != values:
        raise ValueError('Generated GMD readback failed')
    if result[:32] + result[36:start] != data[:32] + data[36:start]:
        raise ValueError('Non-string metadata changed')
    return result, dict(source_file=filename, source_sha256=sha(data), output_sha256=sha(result),
                        total_strings=len(values), changed_strings=len(changed), changed_keys=changed,
                        unchanged_strings=len(values)-len(changed), installed=False, game_validated=False)


def read_json(path):
    def unique(pairs):
        obj = {}
        for key, value in pairs:
            if key in obj:
                raise ValueError('Duplicate JSON property: ' + key)
            obj[key] = value
        return obj
    return json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=unique)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['export', 'build'])
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--catalog', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    if args.source.resolve() == args.catalog.resolve():
        parser.error('Catalog must be separate from source')
    data = args.source.read_bytes()
    if args.operation == 'export':
        if args.catalog.exists():
            parser.error('Catalog exists; preserve your edits or use a new filename')
        catalog = export_catalog(data, args.source.name)
        args.catalog.parent.mkdir(parents=True, exist_ok=True)
        args.catalog.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(f'Exported {len(catalog["entries"])} editable entries; no replacements enabled.')
    else:
        if not args.output or args.output.suffix.lower() != '.gmd':
            parser.error('Build requires --output ending in .gmd')
        if args.output.resolve() in (args.source.resolve(), args.catalog.resolve()):
            parser.error('Output must be separate from source and catalog')
        result, report = build(data, read_json(args.catalog), args.source.name)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_bytes(result)
        args.output.with_suffix('.validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
