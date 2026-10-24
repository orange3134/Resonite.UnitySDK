import importlib.util,json,tempfile,unittest
from pathlib import Path
import test_keijiro_motion as fixture_module
import numpy as np
from types import SimpleNamespace

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


class RepeatedRingTests(unittest.TestCase):
    extractor = 'extract-bubblewand-motion'
    def fixture(self, path, deformed=False):
        module=load(self.extractor);g=fixture_module.g
        angle=np.linspace(0,2*np.pi,9)
        ring=np.column_stack((np.cos(angle),np.sin(angle),np.zeros(9)))
        rotation=np.array([[0,0,1],[0,1,0],[-1,0,0]])
        normals=np.concatenate((ring,ring@rotation,ring))
        positions=np.concatenate((ring,ring@rotation+[3,2,1],ring+[8,0,0]))
        if deformed:positions[9:18]=(positions[9:18]-[3,2,1])*.4+[3,2,1]
        uv=np.tile(np.column_stack((np.full(9,.75),np.linspace(1,0,9),np.ones(9))),(3,1))
        timing=np.tile([1,2,1.5],(27,1))
        writer=g.Writer(SimpleNamespace(doc={'scenes':[{'nodes':[0]}],'nodes':[]}))
        attrs={name:writer.accessor(values,vertex=True) for name,values in
               [('POSITION',positions),('NORMAL',normals),('TANGENT',np.tile([0,0,1,1],(27,1))),
                ('_TB_UNITY_TEXCOORD_0',uv),('_TB_TIMESTAMP',timing),('COLOR_0',np.ones((27,4)))]}
        triangles=np.array([[o,o+i,o+i+1] for o in (0,9,18) for i in range(1,7)])
        writer.doc['materials']=[{'extensions':{'GOOGLE_tilt_brush_material':{'guid':module.GUID}}}]
        writer.doc['meshes']=[{'primitives':[dict(attributes=attrs,indices=writer.accessor(triangles.ravel(),indices=True),material=0)]}]
        writer.doc['nodes']=[dict(name='repeated_rings',mesh=0)];writer.save(path)
        return positions

    def test_same_uv_and_timestamp_at_separate_ring_centers_is_valid(self):
        with tempfile.TemporaryDirectory() as directory:
            source=Path(directory)/'in.glb';out=Path(directory)/'out.json'
            original=self.fixture(source)
            load(self.extractor).extract(source,out,Path(__file__).with_name('animate-brush-glb.py'))
            result=json.loads(out.read_text());part=result['parts'][0]
            self.assertAlmostEqual(result['rawRadiusRatioMin'],1,places=6)
            self.assertAlmostEqual(result['rawRadiusRatioMax'],1,places=6)
            actual=np.array([[v[k] for k in 'xyz'] for v in part['positions']])
            np.testing.assert_allclose(actual,original*[1,1,-1],atol=1e-6)
            self.assertEqual(len(part['positions']),27)

    def test_one_deformed_ring_cannot_hide_among_valid_repeated_rings(self):
        with tempfile.TemporaryDirectory() as directory:
            source=Path(directory)/'in.glb';out=Path(directory)/'out.json'
            self.fixture(source,deformed=True)
            with self.assertRaisesRegex(ValueError,'undeformed'):
                load(self.extractor).extract(source,out,Path(__file__).with_name('animate-brush-glb.py'))
            self.assertFalse(out.exists())


class DiscoRepeatedRingTests(RepeatedRingTests):
    extractor = 'extract-disco-motion'
