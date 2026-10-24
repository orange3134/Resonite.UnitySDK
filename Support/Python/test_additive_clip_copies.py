import importlib.util
from pathlib import Path
import unittest
import numpy as np
spec=importlib.util.spec_from_file_location('growth_additive',Path(__file__).with_name('animate-brush-glb.py'))
growth=importlib.util.module_from_spec(spec);spec.loader.exec_module(growth)

class AdditiveCopies(unittest.TestCase):
    def test_reverse_faces_keep_identical_clipped_fans_and_pressure(self):
        offsets={'POSITION':slice(0,3),'TEXCOORD_0':slice(3,5),'COLOR_0':slice(5,9)}
        vertices=np.array([[0,0,0,0,0,.2,.4,.6,.2,0],
                           [1,0,0,1,0,.2,.4,.6,.8,1],
                           [0,1,0,0,1,.2,.4,.6,.5,.6]],dtype=float)
        before=vertices.copy()
        triangles=growth.canonical_additive_triangles(vertices,np.array([[0,1,2],[2,1,0]]),offsets)
        np.testing.assert_array_equal(triangles[0],triangles[1])
        one=growth.split_triangles(vertices,triangles[:1],0,1,7)
        both=growth.split_triangles(vertices,triangles,0,1,7)
        for a,b in zip(one,both):
            self.assertEqual(len(b),2*len(a))
            if a:
                np.testing.assert_array_equal(b[:len(a)],b[len(a):])
        np.testing.assert_array_equal(vertices,before)

    def test_paint_and_uv_are_part_of_identity(self):
        offsets={'POSITION':slice(0,3),'TEXCOORD_0':slice(3,5),'COLOR_0':slice(5,9)}
        vertices=np.zeros((3,10));vertices[:,3]=[.9,.1,.5]
        ordered=growth.canonical_additive_triangles(vertices,np.array([[0,1,2]]),offsets)
        np.testing.assert_array_equal(ordered,[[1,2,0]])

if __name__=='__main__':unittest.main()
