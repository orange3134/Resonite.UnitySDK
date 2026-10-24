import json,tempfile,unittest
from pathlib import Path
import test_prepare_openbrush_bundle as base

# These identities came from the unregistered normal-material audit. Exercise
# the complete Python pipeline, not just profile parsing, in both user modes.
VARIANTS=['280c0a7a-aad8-416c-a7d2-df63d129ca70','3ca16e2f-bdcd-4da2-8631-dcef342f40f1','4465b5ef-3605-bec4-2b3e-6b04508ddb6b','4f48a0b1-3f88-c0b4-b9e3-c7ceea47d00b','66ec961b-e5ea-5464-4a97-c601ac31047c','759f1ebd-20cd-4720-8d41-234e0da63716','79168f10-6961-464a-8be1-57ed364c5600','7a1c8107-50c5-4b70-9a39-421576d6617e','c0012095-3ffd-4040-8ee1-fc180d346eaa','c515dad7-4393-4681-81ad-162ef052241b','c8c352e1-593f-1b94-1869-6896e28a4184','c8ccb53d-ae13-45ef-8afb-b730d81394eb','dea67637-cd1a-27e4-c9b1-52f4bbcb84e5','e814fef1-97fd-7194-4a2f-50c2bb918be2','ec4fa4d9-212e-df34-0afb-99330c2392d6','fdf0326a-c0d1-4fed-b101-9db0ff6d071f']
class CoverageBundleTests(unittest.TestCase):
    def test_new_identities_preserve_geometry_and_binding_in_both_modes(self):
        for completed in [False,True]:
            with self.subTest(completed=completed),tempfile.TemporaryDirectory() as d:
                root=Path(d);src=root/'source.glb';base.fixture(src,VARIANTS)
                result=base.bundle.prepare(src,root/'bundle',completed=completed,slices_per_second=4)
                manifest=json.loads((root/'bundle/materials.json').read_text())
                self.assertEqual({m['openBrush']['brushGuid'] for m in manifest['materials']},set(VARIANTS))
                reader=base.bundle.load('animate-brush-glb').Reader(root/'bundle/drawing.glb')
                self.assertGreater(sum(len(m['primitives']) for m in reader.doc['meshes']),0)
                self.assertEqual(bool(reader.doc.get('animations')),not completed)
                for item in result['hashes']:self.assertEqual(base.bundle.sha256(root/'bundle'/item['path']),item['sha256'])
if __name__=='__main__':unittest.main()
