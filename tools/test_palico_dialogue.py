import copy
import json
import tempfile
import unittest
from pathlib import Path

from palico_dialogue import build, export_catalog, read_json, tokens
from prepare_palico_lines import ROOT, parse


class EditableDialogueChecks(unittest.TestCase):
    def setUp(self):
        self.file = ROOT / 'research/chat/chunks/chunkG0/common/text/ot_auto_chT.gmd'
        self.data = self.file.read_bytes()
        self.catalog = export_catalog(self.data, self.file.name)

    def test_no_edits_roundtrip_both_languages(self):
        for language in ('chS', 'chT'):
            path = self.file.with_name('ot_auto_' + language + '.gmd')
            data = path.read_bytes()
            result, report = build(data, export_catalog(data, path.name), path.name)
            self.assertEqual(result, data)
            self.assertEqual(report['changed_strings'], 0)

    def test_eight_persona_replacements_preserve_other_entries(self):
        preset = json.loads((ROOT / 'research/chat/palico-lines-persona.json').read_text(encoding='utf-8'))
        replacements = {row['key']: row['replacement'] for row in preset['lines']}
        for row in self.catalog['entries']:
            row['replacement'] = replacements.get(row['key'])
        result, report = build(self.data, self.catalog, self.file.name)
        start, keys, before = parse(self.data)
        _, _, after = parse(result)
        self.assertEqual(report['changed_strings'], 8)
        self.assertEqual(report['unchanged_strings'], 232)
        self.assertEqual(self.data[:32] + self.data[36:start], result[:32] + result[36:start])
        for key, index in keys.items():
            self.assertEqual(after[index], replacements.get(key, before[index]))

    def test_wrong_source_or_language_rejected(self):
        for field, value in [('source_sha256', 'bad'), ('source_file', 'ot_auto_chS.gmd'), ('format_version', 99)]:
            catalog = copy.deepcopy(self.catalog)
            catalog[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                build(self.data, catalog, self.file.name)

    def test_immutable_fields_and_duplicate_key_rejected(self):
        for field, value in [('index', 999), ('original', 'changed'), ('key', 'unknown')]:
            catalog = copy.deepcopy(self.catalog)
            catalog['entries'][0][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                build(self.data, catalog, self.file.name)
        self.catalog['entries'].append(copy.deepcopy(self.catalog['entries'][0]))
        with self.assertRaises(ValueError):
            build(self.data, self.catalog, self.file.name)

    def test_game_tokens_preserved_or_rejected(self):
        tagged = [row for row in self.catalog['entries'] if tokens(row['original'])]
        self.assertTrue(any('<ICON NOTE>' in row['original'] for row in tagged))
        self.assertTrue(any('%s' in row['original'] for row in tagged))
        for row in tagged:
            row['replacement'] = '本鱼：' + row['original']
        build(self.data, self.catalog, self.file.name)
        tagged[0]['replacement'] = '移除了参数'
        with self.assertRaises(ValueError):
            build(self.data, self.catalog, self.file.name)
        self.assertEqual(tokens('%s %d <ICON NOTE> {name}'), ['%s', '%d', '<ICON NOTE>', '{name}'])

    def test_invalid_replacement_rejected(self):
        for value in ('', ' \n', 'bad\0text', '\x01text', '鱼' * 171, '<bad', '<ICON NEW>'):
            catalog = copy.deepcopy(self.catalog)
            catalog['entries'][0]['replacement'] = value
            with self.subTest(value=value[:12]), self.assertRaises(ValueError):
                build(self.data, catalog, self.file.name)

    def test_unicode_and_newline_roundtrip(self):
        self.catalog['entries'][0]['replacement'] = '本鱼来啦🐳\r\n慢慢来。'
        result, report = build(self.data, self.catalog, self.file.name)
        _, _, values = parse(result)
        self.assertEqual(values[0], '本鱼来啦🐳\r\n慢慢来。')
        self.assertEqual(report['changed_strings'], 1)

    def test_duplicate_json_properties_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'bad.json'
            path.write_text('{"entries": [], "entries": []}', encoding='utf-8')
            with self.assertRaises(ValueError):
                read_json(path)


if __name__ == '__main__':
    unittest.main()
