import csv
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('audit_support', Path(__file__).with_name('audit-brush-support.py'))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)
GUID = 'eba3f993-f9a1-4d35-b84e-bb08f48981a4'
EXTRA = '61d2ef63-ed60-49b3-85fb-7267b7d234f2'


class SupportAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.source = Path(self.temp.name) / 'source'
        self.sdk = Path(self.temp.name) / 'sdk'
        self.write(self.source / 'Assets/Elsewhere/Bubble.asset', f'  m_Name: BubbleWand\n  m_DurableName: BubbleWand\n  m_Guid:\n    m_storage: {GUID.upper()}\n')
        self.write(self.source / 'Assets/Elsewhere/Bubble.asset.meta', 'guid: abcdef0123456789abcdef0123456789\n')
        self.write(self.source / 'Support/exportManifest.json', json.dumps({'brushes': {}}))
        self.write(self.sdk / 'Assets/OpenBrushResonite/OpenBrushProfiles.cs', f'new Profile("{GUID}", "BubbleWand")')
        self.write(self.sdk / 'Documentation/OPENBRUSH-BRUSH-SUPPORT.csv', 'ブラシ,SDK名,GUID,状態,対応内容,制約とメモ,確認根拠\n')
        self.write(self.sdk / 'Documentation/OPENBRUSH-BRUSH-SUPPORT-JA.md', '')

    def write(self, path, text):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')

    def document(self, key=GUID, name='BubbleWand', status='制約あり'):
        with (self.sdk / 'Documentation/OPENBRUSH-BRUSH-SUPPORT.csv').open('a', encoding='utf-8', newline='') as f:
            csv.writer(f).writerow([name, name, key, status, '', '', ''])
        with (self.sdk / 'Documentation/OPENBRUSH-BRUSH-SUPPORT-JA.md').open('a', encoding='utf-8') as f:
            f.write(f'| {name} | {status} | 内容 | 制約 | 根拠 |\n')

    def test_uppercase_unregistered_is_not_dropped(self):
        result = audit.audit(self.source, self.sdk)
        self.assertEqual(result['missing'], [GUID])
        self.document()
        result = audit.audit(self.source, self.sdk)
        self.assertEqual(result['missing'], [])
        self.assertEqual(result['issues'], [])
        self.assertEqual(result['summary']['descriptors'], 1)

    def test_export_only_identity_requires_its_own_row(self):
        self.document()
        self.write(self.source / 'Support/exportManifest.json', json.dumps({'brushes': {EXTRA: {'guid': EXTRA, 'name': 'CandyCane'}}}))
        self.assertEqual(audit.audit(self.source, self.sdk)['missing'], [EXTRA])

    def test_same_name_different_guid_is_not_covered(self):
        self.document()
        self.write(self.source / 'Assets/Other.asset', f'  m_DurableName: BubbleWand\n  m_Guid:\n    m_storage: {EXTRA}\n')
        self.write(self.source / 'Assets/Other.asset.meta', 'guid: 11111111111111111111111111111111\n')
        self.assertEqual(audit.audit(self.source, self.sdk)['missing'], [EXTRA])

    def test_status_and_markdown_are_checked(self):
        self.document(status='未対応')
        self.write(self.sdk / 'Documentation/OPENBRUSH-BRUSH-SUPPORT-JA.md', '')
        issues = audit.audit(self.source, self.sdk)['issues']
        self.assertTrue(any('Markdown row' in i for i in issues))
        self.assertTrue(any('status mismatch' in i for i in issues))

    def test_unresolved_manifest_brush_fails(self):
        self.document()
        self.write(self.source / 'Assets/Manifest.asset', '  Brushes:\n  - {guid: 11111111111111111111111111111111}\n')
        self.assertTrue(any('unresolved brush' in i for i in audit.audit(self.source, self.sdk)['issues']))

    def test_markdown_status_and_extra_rows_are_not_ignored(self):
        self.document()
        self.write(self.sdk / 'Documentation/OPENBRUSH-BRUSH-SUPPORT-JA.md', '| BubbleWand | 未対応 | 内容 | 制約 | 根拠 |\n| UnknownBrush | 未対応 | 内容 | 制約 | 根拠 |\n')
        issues = audit.audit(self.source, self.sdk)['issues']
        self.assertTrue(any('Markdown/CSV status mismatch' in i for i in issues))
        self.assertTrue(any('Markdown row absent from CSV' in i for i in issues))

    def test_glb_only_identity_and_empty_file_are_reported(self):
        self.document()
        folder = self.source / 'Exports'
        folder.mkdir()
        data = json.dumps({'materials': [{'name': 'CandyCane', 'extensions': {'GOOGLE_tilt_brush_material': {'guid': EXTRA}}}]}).encode()
        data += b' ' * (-len(data) % 4)
        (folder / 'drawing.glb').write_bytes(struct.pack('<5I', 0x46546C67, 2, len(data) + 20, len(data), 0x4E4F534A) + data)
        (folder / 'failed.glb').write_bytes(b'')
        result = audit.audit(self.source, self.sdk, glb_roots=[folder])
        self.assertEqual(result['missing'], [EXTRA])
        self.assertEqual(result['summary']['glbFiles'], 2)
        self.assertTrue(any('Truncated GLB' in i for i in result['issues']))


if __name__ == '__main__':
    unittest.main()
