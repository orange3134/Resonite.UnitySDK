import importlib.util,json,tempfile,unittest
from pathlib import Path
from types import SimpleNamespace
import numpy as np

def load(name):
    spec=importlib.util.spec_from_file_location(name,Path(__file__).with_name(name+'.py'))
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
g=load('animate-brush-glb');k=load('extract-keijiro-motion')

def fixture(path,deformed=False,missing=False):
    ring=np.array([[1,0,0],[0,1,0],[-1,0,0],[0,-1,0]],dtype=float)
    positions=np.concatenate([ring+[0,0,0],ring*2+[5,0,0]])
    if deformed:positions[:4]*=.4
    normals=np.concatenate([ring,ring]);uv=np.array([[.7,0,1]]*4+[[.7,0,2]]*4)
    timing=np.array([[1,2,1.5]]*4+[[3,4,3.5]]*4)
    w=g.Writer(SimpleNamespace(doc={'scenes':[{'nodes':[0]}],'nodes':[]}));attrs={name:w.accessor(values,vertex=True) for name,values in [('POSITION',positions),('NORMAL',normals),('TANGENT',np.tile([0,0,1,1],(8,1))),('_TB_UNITY_TEXCOORD_0',uv),('_TB_TIMESTAMP',timing),('COLOR_0',np.ones((8,4)))]}
    if missing:del attrs['_TB_UNITY_TEXCOORD_0']
    w.doc['materials']=[{'extensions':{'GOOGLE_tilt_brush_material':{'guid':k.GUID}}}]
    w.doc['meshes']=[{'primitives':[{'attributes':attrs,'indices':w.accessor(np.array([0,1,2,0,2,3,4,5,6,4,6,7]),indices=True),'material':0}]}]
    w.doc['nodes']=[{'name':'tube','mesh':0}];w.save(path)

class KeijiroTests(unittest.TestCase):
    def test_original_radii_colors_times_and_coordinate_basis(self):
        with tempfile.TemporaryDirectory() as directory:
            src=Path(directory)/'in.glb';dst=Path(directory)/'out.json';fixture(src)
            k.extract(src,dst,Path(__file__).with_name('animate-brush-glb.py'));data=json.loads(dst.read_text())
            self.assertAlmostEqual(data['rawRadiusRatioMin'],1);self.assertAlmostEqual(data['rawRadiusRatioMax'],1)
            part=data['parts'][0];self.assertEqual(part['triangles'][:3],[0,2,1]);self.assertEqual(part['strokeStarts'],[1]*4+[3]*4)
            self.assertEqual([v['z'] for v in part['uvRadius']],[1]*4+[2]*4)
            self.assertEqual(part['tangents'][0],dict(x=0,y=0,z=-1,w=1))
            with self.assertRaises(FileExistsError):k.extract(src,dst,Path(__file__).with_name('animate-brush-glb.py'))
    def test_rejects_already_contracted_geometry(self):
        with tempfile.TemporaryDirectory() as directory:
            src=Path(directory)/'in.glb';fixture(src,deformed=True)
            with self.assertRaisesRegex(ValueError,'undeformed'):k.extract(src,Path(directory)/'out.json',Path(__file__).with_name('animate-brush-glb.py'))
    def test_rejects_baked_export_missing_radius(self):
        with tempfile.TemporaryDirectory() as directory:
            src=Path(directory)/'in.glb';fixture(src,missing=True)
            with self.assertRaisesRegex(ValueError,'original radius'):k.extract(src,Path(directory)/'out.json',Path(__file__).with_name('animate-brush-glb.py'))

if __name__=='__main__':unittest.main()
