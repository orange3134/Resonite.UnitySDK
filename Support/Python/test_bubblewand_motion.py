import importlib.util,json,tempfile,unittest
from pathlib import Path
import test_keijiro_motion as fixture_module

def load(name):
    spec=importlib.util.spec_from_file_location(name,Path(__file__).with_name(name+'.py'))
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

class TubeExtractionTests(unittest.TestCase):
    def test_distinct_strokes_with_same_uv_keep_their_own_radius(self):
        for name in ('extract-bubblewand-motion','extract-disco-motion'):
            module=load(name)
            with self.subTest(name=name),tempfile.TemporaryDirectory() as directory:
                old=fixture_module.k.GUID
                try:
                    fixture_module.k.GUID=module.GUID
                    source=Path(directory)/'in.glb';fixture_module.fixture(source)
                finally:fixture_module.k.GUID=old
                out=Path(directory)/'out.json'
                module.extract(source,out,Path(__file__).with_name('animate-brush-glb.py'))
                result=json.loads(out.read_text())
                self.assertAlmostEqual(result['rawRadiusRatioMin'],1)
                self.assertAlmostEqual(result['rawRadiusRatioMax'],1)
                self.assertEqual(len(result['parts'][0]['positions']),8)

    def test_deformed_geometry_is_still_rejected(self):
        for name in ('extract-bubblewand-motion','extract-disco-motion'):
            module=load(name)
            with self.subTest(name=name),tempfile.TemporaryDirectory() as directory:
                old=fixture_module.k.GUID
                try:
                    fixture_module.k.GUID=module.GUID
                    source=Path(directory)/'in.glb';fixture_module.fixture(source,deformed=True)
                finally:fixture_module.k.GUID=old
                with self.assertRaisesRegex(ValueError,'undeformed'):
                    module.extract(source,Path(directory)/'out.json',Path(__file__).with_name('animate-brush-glb.py'))
