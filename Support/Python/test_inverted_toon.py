import importlib.util
from pathlib import Path
from types import SimpleNamespace
import unittest
import numpy as np
spec=importlib.util.spec_from_file_location('growth',Path(__file__).with_name('animate-brush-glb.py'))
growth=importlib.util.module_from_spec(spec);spec.loader.exec_module(growth)
GUID='9871385a-df73-4396-9e33-31e4e4930b27'
def fixture(scale=1):
    data=[np.array([[0,0,0],[1,0,0],[0,1,0.]],float),np.array([[0,1,0.]]*3),
          np.array([[.5,.2,.1,.3]]*3),np.array([[0,1,0],[0,1,.5],[0,1,1.]]),np.array([[0],[1],[2]])]
    attrs=dict(zip(['POSITION','NORMAL','COLOR_0','_TB_TIMESTAMP'],range(4)))
    r=SimpleNamespace(doc={'scenes':[{'extras':{'TB_PoseScale':str(scale)}}],
        'materials':[{'extensions':{'GOOGLE_tilt_brush_material':{'guid':GUID}}}],
        'meshes':[{'name':'fixture','primitives':[{'material':0,'attributes':attrs,'indices':4}]}],
        'accessors':[{'type':k} for k in ['VEC3','VEC3','VEC4','VEC3','SCALAR']]},cache=dict(enumerate(data)))
    r.accessor=lambda i:r.cache[i]
    return r
class InvertedToonTests(unittest.TestCase):
    def test_surfaces_winding_color_and_shared_clock(self):
        r=fixture(2);old={i:a.copy() for i,a in r.cache.items()}
        changes=growth.bake_inverted_toon_geometry(r);p=r.doc['meshes'][0]['primitives'][0];a=p['attributes']
        v=r.accessor(a['POSITION']);c=r.accessor(a['COLOR_0'])
        np.testing.assert_allclose(v[:3],old[0]);np.testing.assert_allclose(v[3:]-v[:3],old[1]*.0025)
        np.testing.assert_allclose(c[:3],[[0,0,0,1]]*3);np.testing.assert_allclose(c[3:],[[.7,.4,.3,1]]*3)
        np.testing.assert_array_equal(r.accessor(p['indices']).ravel(),[0,1,2,5,4,3])
        np.testing.assert_array_equal(r.accessor(a['_TB_TIMESTAMP'])[:3],r.accessor(a['_TB_TIMESTAMP'])[3:])
        for i,original in old.items():np.testing.assert_array_equal(r.cache[i],original)
        self.assertEqual(changes[0]['shellMetres'],.0025)
    def test_invalid_pose_scale_rejected(self):
        for scale in [0,-1,float('nan')]:
            with self.assertRaises(ValueError):growth.bake_inverted_toon_geometry(fixture(scale))
    def test_other_brush_untouched(self):
        r=fixture();r.doc['materials'][0]['extensions']['GOOGLE_tilt_brush_material']['guid']='other'
        self.assertEqual(growth.bake_inverted_toon_geometry(r),[]);self.assertEqual(len(r.cache),5)
if __name__=='__main__':unittest.main()
