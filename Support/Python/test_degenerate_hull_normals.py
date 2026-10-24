"""Only proven invisible degenerate faces may receive finite fallback normals."""
import copy
import importlib.util
from pathlib import Path
from types import SimpleNamespace
import unittest
import numpy as np

spec=importlib.util.spec_from_file_location('animation',Path(__file__).with_name('animate-brush-glb.py'))
animation=importlib.util.module_from_spec(spec);spec.loader.exec_module(animation)
GUID='7ae1f880-a517-44a0-99f9-1cab654498c6'

def fixture(degenerate=True,invalid=True):
    normals=np.array([[float('nan'),0,0] if invalid else [0,0,1]]*3+[[0,0,1]]*3,dtype='<f4')
    positions=np.array([[0,0,0],[1,0,0],[2,0 if degenerate else 1,0],[0,0,0],[1,0,0],[0,1,0]],float)
    p={'attributes':{'POSITION':1,'NORMAL':0},'indices':2,'material':0}
    r=SimpleNamespace(doc={'materials':[{'extensions':{'GOOGLE_tilt_brush_material':{'guid':GUID}}}],
        'meshes':[{'primitives':[p]}],'accessors':[{'type':'VEC3','componentType':5126,'count':6,'bufferView':0}],
        'bufferViews':[{'byteLength':normals.nbytes}]},binary=normals.tobytes(),cache={1:positions,2:np.arange(6)[:,None]})
    r.accessor=lambda i:r.cache[i]
    return r

class HullRepairTests(unittest.TestCase):
    def test_degenerate_only_preserves_geometry_and_finite_normals(self):
        r=fixture();original=r.binary;positions=r.cache[1].copy();indices=r.cache[2].copy()
        report=animation.repair_degenerate_hull_normals(r)
        self.assertEqual((report[0]['vertices'],report[0]['zeroAreaTriangles']),(3,1))
        np.testing.assert_array_equal(r.cache[0][:3],np.zeros((3,3)))
        np.testing.assert_array_equal(r.cache[0][3:],[[0,0,1]]*3)
        np.testing.assert_array_equal(r.cache[1],positions);np.testing.assert_array_equal(r.cache[2],indices)
        self.assertEqual(r.binary,original)
    def test_visible_triangle_rejected(self):
        with self.assertRaisesRegex(ValueError,'nondegenerate'):
            animation.repair_degenerate_hull_normals(fixture(False))
    def test_valid_normals_untouched(self):
        r=fixture(invalid=False)
        self.assertEqual(animation.repair_degenerate_hull_normals(r),[]);self.assertNotIn(0,r.cache)
    def test_shared_invalid_accessor_rejected(self):
        r=fixture();r.doc['meshes'][0]['primitives'].append(copy.deepcopy(r.doc['meshes'][0]['primitives'][0]))
        with self.assertRaisesRegex(ValueError,'shared'):
            animation.repair_degenerate_hull_normals(r)
    def test_other_brush_and_materialless_untouched(self):
        for materialless in (False,True):
            r=fixture()
            if materialless:del r.doc['meshes'][0]['primitives'][0]['material']
            else:r.doc['materials'][0]['extensions']['GOOGLE_tilt_brush_material']['guid']='other'
            self.assertEqual(animation.repair_degenerate_hull_normals(r),[]);self.assertNotIn(0,r.cache)

if __name__=='__main__':unittest.main()
