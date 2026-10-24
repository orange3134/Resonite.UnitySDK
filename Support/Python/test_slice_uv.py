import importlib.util
from pathlib import Path
from types import SimpleNamespace
import unittest
import numpy as np

spec=importlib.util.spec_from_file_location('growth',Path(__file__).with_name('animate-brush-glb.py'))
g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g)

def fixture():
    attrs={'POSITION':0,'_TB_UNITY_TEXCOORD_0':1,'COLOR_0':2}
    arrays={0:np.array([[0.,0,0],[1,0,0],[0,1,0]]),
            1:np.array([[.1,.2,.7],[.4,.6,.7],[.8,.9,.7]]),
            2:np.array([[.2,.3,.4,1]]*3)}
    r=SimpleNamespace(doc={'materials':[{'extensions':{'GOOGLE_tilt_brush_material':{'guid':g.SLICE_GUID}}}],
        'meshes':[{'primitives':[{'attributes':attrs,'material':0}]}], 'accessors':[{}]*3},cache=arrays)
    r.accessor=lambda i:r.cache[i]
    return r,attrs

class SliceUVTests(unittest.TestCase):
    def test_preserves_fragment_inputs_and_reverses_export_v_only(self):
        r,a=fixture();raw=r.cache[1].copy();paint=r.cache[2].copy()
        self.assertEqual(g.prepare_source_slice(r),3)
        actual=r.cache[a[g.SOURCE_SLICE_ATTRIBUTE]]
        np.testing.assert_allclose(actual[:,0],raw[:,0])
        np.testing.assert_allclose(actual[:,1],1-raw[:,1])
        np.testing.assert_allclose(actual[:,2],raw[:,2])
        np.testing.assert_array_equal(r.cache[1],raw)
        np.testing.assert_array_equal(r.cache[2],paint)
    def test_missing_raw_coordinate_rejected(self):
        r,a=fixture();del a['_TB_UNITY_TEXCOORD_0']
        with self.assertRaisesRegex(ValueError,'original'):g.prepare_source_slice(r)
    def test_varying_distance_triangle_rejected(self):
        r,a=fixture();r.cache[1][1,2]+=.1
        with self.assertRaisesRegex(ValueError,'constant distance'):g.prepare_source_slice(r)
    def test_reserved_varying_not_overwritten(self):
        r,a=fixture();a[g.SOURCE_SLICE_ATTRIBUTE]=1
        with self.assertRaisesRegex(ValueError,'already present'):g.prepare_source_slice(r)
    def test_non_slice_data_untouched(self):
        r,a=fixture();r.doc['materials'][0]['extensions']['GOOGLE_tilt_brush_material']['guid']='other'
        self.assertEqual(g.prepare_source_slice(r),0)
        self.assertNotIn(g.SOURCE_SLICE_ATTRIBUTE,a)

if __name__=='__main__':unittest.main()
