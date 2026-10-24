import copy, importlib.util
from pathlib import Path
from types import SimpleNamespace
import unittest
import numpy as np

spec=importlib.util.spec_from_file_location('animation',Path(__file__).with_name('animate-brush-glb.py'))
animation=importlib.util.module_from_spec(spec);spec.loader.exec_module(animation)
GUID='8e58ceea-7830-49b4-aba9-6215104ab52a'
def fixture():
    points=[]; normals=[]; uv=[]
    for u in [0,.25,.5,.75,1]:
        for j,(y,z) in enumerate([(1,0),(0,1),(-1,0),(0,-1)]):
            r=.02*(1+u);points.append([u,r*y,r*z]);normals.append([0,y,z]);uv.append([u,j/4,r])
    arrays={0:np.array(points),1:np.array(normals,float),2:np.array(uv)}
    primitive=dict(attributes=dict(POSITION=0,NORMAL=1,_TB_UNITY_TEXCOORD_0=2),material=0)
    reader=SimpleNamespace(doc=dict(materials=[dict(extensions={'GOOGLE_tilt_brush_material':dict(guid=GUID)})],
        meshes=[dict(primitives=[primitive])]),cache=arrays)
    reader.accessor=lambda index:reader.cache[index]
    return reader

class MylarTests(unittest.TestCase):
    def test_neck_and_ends_preserve_centerline_normals_and_radius(self):
        r=fixture();original={k:v.copy() for k,v in r.cache.items()}
        animation.bake_static_brush_geometry(r)
        np.testing.assert_allclose(r.cache[0][:4],original[0][:4],atol=1e-12)
        # At U=.5 the neck has 12.7% of the original radius.
        np.testing.assert_allclose(np.linalg.norm(r.cache[0][8:12,1:],axis=1),.03*.127,atol=1e-9)
        np.testing.assert_array_equal(r.cache[0][:,0],original[0][:,0])
        for k in [1,2]:np.testing.assert_array_equal(r.cache[k],original[k])
        once=r.cache[0].copy();animation.bake_static_brush_geometry(r)
        np.testing.assert_array_equal(r.cache[0],once)
    def test_independent_strokes_with_different_radii_and_centers(self):
        r=fixture();count=len(r.cache[0]);first=r.cache[0].copy()
        r.cache[0]=np.concatenate([first,first*2+[4,3,2]])
        r.cache[1]=np.concatenate([r.cache[1],r.cache[1]])
        uv=r.cache[2];other=uv.copy();other[:,2]*=2;r.cache[2]=np.concatenate([uv,other])
        r.cache[3]=np.array([[0,1,0]]*count+[[2,3,2]]*count,dtype=float)
        r.doc['meshes'][0]['primitives'][0]['attributes']['_TB_TIMESTAMP']=3
        animation.bake_mylar_geometry(r)
        # Both centers and relative radii survive the same source squeeze.
        np.testing.assert_allclose(r.cache[0][count:],r.cache[0][:count]*2+[4,3,2],atol=1e-10)

    def test_predeformed_input_rejected_before_mutation(self):
        r=fixture();r.cache[0][8:12,1:]*=.127;before=r.cache[0].copy()
        with self.assertRaisesRegex(ValueError,'already deformed'):animation.bake_mylar_geometry(r)
        np.testing.assert_array_equal(r.cache[0],before)
    def test_shared_positions_with_other_brush_rejected(self):
        r=fixture();r.doc['materials'].append(dict(extensions={'GOOGLE_tilt_brush_material':dict(guid='other')}))
        other=copy.deepcopy(r.doc['meshes'][0]['primitives'][0]);other['material']=1;r.doc['meshes'][0]['primitives'].append(other)
        with self.assertRaisesRegex(ValueError,'shared'):animation.bake_mylar_geometry(r)
    def test_missing_radius_rejected(self):
        r=fixture();del r.doc['meshes'][0]['primitives'][0]['attributes']['_TB_UNITY_TEXCOORD_0']
        with self.assertRaisesRegex(ValueError,'radius'):animation.bake_mylar_geometry(r)

if __name__=='__main__':unittest.main()
