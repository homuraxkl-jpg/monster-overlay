"""Offline GMD fallback regression checks; no game process or API access."""
import json
import struct
import unittest

from prepare_palico_lines import ROOT, parse, patch


class PalicoLineChecks(unittest.TestCase):
    def setUp(self):
        self.config = json.loads((ROOT / 'research/chat/palico-lines-persona.json').read_text(encoding='utf-8'))
        self.data = (ROOT / self.config['source']).read_bytes()

    def test_all_extracted_resources_roundtrip_unchanged(self):
        files = list((ROOT / 'research/chat/chunks').rglob('*_chS.gmd'))
        self.assertEqual(len(files), 7)
        for file in files:
            with self.subTest(file=file.name):
                data = file.read_bytes()
                result, _, changed = patch(data, {})
                self.assertEqual(result, data)
                self.assertEqual(changed, 0)

    def test_selected_values_and_other_bytes(self):
        replacements = {row['key']: row['replacement'] for row in self.config['lines']}
        result, count, changed = patch(self.data, replacements)
        start, keys, original = parse(self.data)
        new_start, new_keys, values = parse(result)
        self.assertEqual((count, changed), (240, 8))
        self.assertEqual((start, keys), (new_start, new_keys))
        self.assertEqual(result[:32] + result[36:start], self.data[:32] + self.data[36:start])
        for key, index in keys.items():
            self.assertEqual(values[index], replacements.get(key, original[index]))

    def test_utf8_length_and_newline(self):
        text = '本鱼来啦！🐳\r\n别逞强。' * 40
        result, _, _ = patch(self.data, {'OT_Talk_AUTO_002_OT': text})
        start, keys, values = parse(result)
        self.assertEqual(values[keys['OT_Talk_AUTO_002_OT']], text)
        self.assertEqual(struct.unpack_from('<I', result, 32)[0], len(result) - start)

    def test_bad_replacements_rejected(self):
        for replacements in ({'unknown': 'a'}, {'OT_Talk_AUTO_002_OT': ''},
                             {'OT_Talk_AUTO_002_OT': 'a\0b'}, {'OT_Talk_AUTO_002_OT': '<STYL x>'}):
            with self.subTest(replacements=replacements), self.assertRaises(ValueError):
                patch(self.data, replacements)

    def test_corrupt_size_and_truncation_rejected(self):
        damaged = bytearray(self.data)
        struct.pack_into('<I', damaged, 32, 1)
        for data in (b'bad', bytes(damaged), self.data[:-1], self.data + b'extra'):
            with self.subTest(length=len(data)), self.assertRaises(ValueError):
                parse(data)


if __name__ == '__main__':
    unittest.main()
