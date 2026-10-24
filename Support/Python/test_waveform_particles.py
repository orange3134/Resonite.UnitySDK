import importlib.util,json,tempfile,unittest
from pathlib import Path
from types import SimpleNamespace
import numpy as np

def load(name):
    s=importlib.util.spec_from_file_location(name,Path(__file__).with_name(name+'.py'));m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
g=load('animate-brush-glb');p=load('extract-waveform-particles')

def fixture(path,missing=False,deformed=False):
    offset=np.array([[-.01,-.01,0],[.01,-.01,0],[0,.01,0]])
    position=offset+[1,2,3]
    if deformed:position[0,0]+=.01
    w=g.Writer(SimpleNamespace(doc={'scenes':[{'nodes':[0]}],'nodes':[]}))
    pairs=[('POSITION',position),('_TB_UNITY_TEXCOORD_1',np.c_[offset,np.full(3,1234)]),('_TB_UNITY_TEXCOORD_0',[[0,0],[1,0],[.5,1]]),('COLOR_0',[[.2,.5,.8,.7]]*3),('_TB_TIMESTAMP',[[4,5,4],[4,5,4.5],[4,5,5]])]
    attrs={k:w.accessor(v,vertex=True) for k,v in pairs}
    if missing:del attrs['_TB_UNITY_TEXCOORD_1']
    w.doc['materials']=[{'extensions':{'GOOGLE_tilt_brush_material':{'guid':p.GUID}}}]
    w.doc['meshes']=[{'primitives':[dict(attributes=attrs,indices=w.accessor([0,1,2],indices=True),material=0)]}];w.doc['nodes']=[dict(name='particles',mesh=0)];w.save(path)

class WaveformParticleTests(unittest.TestCase):
    def test_retains_rigid_triangle_size_color_and_basis(self):
        with tempfile.TemporaryDirectory() as d:
            src=Path(d)/'source.glb';dst=Path(d)/'data.json';fixture(src)
            p.extract(src,dst,Path(__file__).with_name('animate-brush-glb.py'));data=json.loads(dst.read_text());q=data['parts'][0]
            self.assertEqual(q['triangles'],[0,2,1]);self.assertEqual(q['times'],[4.5]*3)
            self.assertEqual(q['strokeStarts'],[4]*3);self.assertEqual(q['strokeEnds'],[5]*3)
            for pos,off in zip(q['positions'],q['offsets']):
                np.testing.assert_allclose([pos[k]-off[k] for k in 'xyz'],[1,2,-3],atol=1e-6)
            self.assertAlmostEqual(q['colors'][0]['a'],.7,places=6)
    def test_refuses_missing_original_channel(self):
        with tempfile.TemporaryDirectory() as d:
            src=Path(d)/'source.glb';fixture(src,missing=True)
            with self.assertRaisesRegex(ValueError,'original offsets'):p.extract(src,Path(d)/'data.json',Path(__file__).with_name('animate-brush-glb.py'))
    def test_refuses_geometry_already_deformed(self):
        with tempfile.TemporaryDirectory() as d:
            src=Path(d)/'source.glb';fixture(src,deformed=True)
            with self.assertRaisesRegex(ValueError,'undeformed center'):p.extract(src,Path(d)/'data.json',Path(__file__).with_name('animate-brush-glb.py'))

if __name__=='__main__':unittest.main()
