import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from test_prepare_openbrush_bundle import bundle, fixture, MARKER

UNKNOWN = '00000000-0000-0000-0000-000000000000'


class UnsupportedBrushTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.source = self.root / 'mixed.glb'
        self.filtering = bundle.load('filter-unsupported-brushes')
        self.growth = bundle.load('animate-brush-glb')

    def tearDown(self):
        self.temp.cleanup()

    def edit(self, change):
        reader = self.growth.Reader(self.source)
        change(reader.doc)
        path = self.root / 'edited.glb'
        self.filtering.save(reader, path)
        self.source = path

    def test_mixed_brushes_convert_both_modes_with_portable_warning_reports(self):
        for completed in (False, True):
            with self.subTest(completed=completed):
                self.source = self.root / 'mixed.glb'
                fixture(self.source, [UNKNOWN, MARKER, UNKNOWN])
                before = self.source.read_bytes()
                warnings = []
                target = self.root / str(completed)
                data = bundle.prepare(self.source, target, completed=completed, warning=warnings.append)
                self.assertEqual(len(warnings), 2)
                self.assertEqual(data['warnings'], warnings)
                self.assertEqual(data['brushes'], [dict(guid=MARKER, name='Marker')])
                output = self.growth.Reader(target / 'drawing.glb')
                self.assertEqual(len(output.doc['materials']), 1)
                self.assertEqual(bool(output.doc.get('animations')), not completed)
                timeline = json.loads((target / 'timeline.json').read_text())
                self.assertEqual(len(timeline['strokes']), 1)
                self.assertEqual(timeline['strokes'][0]['sourceStart'], 2)
                report = json.loads((target / 'conversion-report.json').read_text())
                self.assertEqual(report['warnings'], warnings)
                self.assertEqual(self.source.read_bytes(), before)
                self.assertFalse(list(target.glob('*intermediate*')))
                for entry in data['hashes']:
                    self.assertEqual(bundle.sha256(target / entry['path']), entry['sha256'])

    def test_unknown_only_warns_then_fails_without_empty_bundle(self):
        fixture(self.source, [UNKNOWN])
        warnings = []
        with self.assertRaisesRegex(ValueError, '変換できるブラシがありません'):
            bundle.prepare(self.source, self.root / 'out', warning=warnings.append)
        self.assertEqual(len(warnings), 1)
        self.assertFalse((self.root / 'out').exists())
        self.assertFalse(list(self.root.glob('*.preparing-*')))

    def test_unsupported_remote_and_missing_timestamps_do_not_block_retained_local_texture(self):
        fixture(self.source, [UNKNOWN, MARKER])
        def change(doc):
            doc['images'].insert(0, dict(uri='https://invalid.example/unsupported.png'))
            doc['textures'].insert(0, dict(source=0))
            doc['textures'][1]['source'] = 1
            doc['materials'][1]['pbrMetallicRoughness']['baseColorTexture']['index'] = 1
            del doc['meshes'][0]['primitives'][0]['attributes']['_TB_TIMESTAMP']
        self.edit(change)
        data = bundle.prepare(self.source, self.root / 'out', completed=True)
        output = self.growth.Reader(self.root / 'out/drawing.glb')
        self.assertEqual(len(output.doc['images']), 1)
        self.assertTrue(bytes(output.view(output.doc['images'][0]['bufferView'])).startswith(b'\x89PNG'))
        self.assertEqual(output.doc['materials'][0]['pbrMetallicRoughness']['baseColorTexture']['index'], 0)
        self.assertEqual(len(data['warnings']), 1)

    def test_mixed_mesh_keeps_supported_child_and_parent_transform(self):
        fixture(self.source, [UNKNOWN, MARKER])
        reader = self.growth.Reader(self.source)
        doc = reader.doc
        # A mixed primitive parent plus a child instance of the supported mesh.
        doc['meshes'][0]['primitives'].append(copy.deepcopy(doc['meshes'][1]['primitives'][0]))
        doc['nodes'][0]['children'] = [1]
        doc['nodes'][0]['translation'] = [4, 5, 6]
        doc['scenes'][0]['nodes'] = [0]
        binary = reader.binary
        brushes, warnings = self.filtering.filter_document(doc, {MARKER: 'Marker'})
        self.assertEqual(brushes, {MARKER})
        self.assertEqual(warnings[0]['primitiveCount'], 1)
        self.assertEqual(len(doc['meshes'][0]['primitives']), 1)
        self.assertEqual(doc['nodes'][0]['children'], [1])
        self.assertEqual(doc['nodes'][0]['translation'], [4, 5, 6])
        self.assertEqual(reader.binary, binary)
        # Entirely skipped parents still retain their supported descendants.
        doc['meshes'][0]['primitives'].clear()
        self.filtering.filter_document(doc, {MARKER: 'Marker'})
        self.assertNotIn('mesh', doc['nodes'][0])
        self.assertEqual(doc['nodes'][0]['children'], [1])
        self.assertIn('mesh', doc['nodes'][1])

    def test_missing_guid_warns_without_guessing_from_name_and_uppercase_is_supported(self):
        fixture(self.source, [UNKNOWN, MARKER.upper()])
        def change(doc):
            doc['materials'][0]['extensions'].clear()
            doc['materials'][0]['name'] = 'Marker'
        self.edit(change)
        data = bundle.prepare(self.source, self.root / 'out', completed=True)
        self.assertEqual(data['warnings'][0]['code'], 'missing_brush_guid')
        self.assertEqual(data['brushes'][0]['guid'], MARKER)

    def test_unused_materials_and_meshes_do_not_trigger_warnings_or_effect_extractors(self):
        fixture(self.source, [MARKER, UNKNOWN, bundle.DISCO])
        def change(doc):
            doc['nodes'] = doc['nodes'][:1]
            doc['scenes'][0]['nodes'] = [0]
        self.edit(change)
        data = bundle.prepare(self.source, self.root / 'out', completed=True)
        self.assertEqual(data['warnings'], [])
        self.assertNotIn('disco', data['files'])
        self.assertEqual(len(data['brushes']), 1)

    def test_corrupt_supported_geometry_still_fails(self):
        fixture(self.source, [UNKNOWN, MARKER])
        self.edit(lambda doc: doc['meshes'][1]['primitives'][0]['attributes'].pop('_TB_TIMESTAMP'))
        with self.assertRaisesRegex(ValueError, 'Missing _TB_TIMESTAMP'):
            bundle.prepare(self.source, self.root / 'out')
        self.assertFalse((self.root / 'out').exists())
        self.assertFalse(list(self.root.glob('*.preparing-*')))

    def test_cli_success_emits_live_warning_and_persists_it(self):
        fixture(self.source, [MARKER, UNKNOWN])
        result = subprocess.run([sys.executable, str(bundle.HERE / 'prepare-openbrush-bundle.py'),
                                 str(self.source), str(self.root / 'out'), '--completed', '--progress-json'],
                                capture_output=True, encoding='utf-8', env=dict(__import__('os').environ, PYTHONUTF8='1'))
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('警告:', result.stderr)
        events = [json.loads(line.removeprefix('OPENBRUSH_PROGRESS ')) for line in result.stdout.splitlines()
                  if line.startswith('OPENBRUSH_PROGRESS ')]
        self.assertTrue(any(event['stage'] == 'warning' for event in events))
        self.assertTrue(json.loads((self.root / 'out/bundle.json').read_text())['warnings'])


if __name__ == '__main__':
    unittest.main()
