"""Large batched sketches must not copy the entire batch once per stroke."""
from pathlib import Path
from types import SimpleNamespace
import tempfile
import tracemalloc
import unittest
import numpy as np
from test_animate_brush_glb import animation, quaternion_matrix


def batched_fixture(path, count):
    source=SimpleNamespace(doc={'scenes':[{'nodes':[0]}], 'nodes':[{'name':'Batch'}],
                               'materials':[{'name':'Ink'}]})
    writer=animation.Writer(source)
    # Interleave strokes in the vertex buffer to exercise index remapping.
    corners=np.array([[0,0,0],[2,0,0],[2,1,0],[0,1,0]],dtype=float)
    positions=np.repeat(corners,count,axis=0)
    positions[:,0]+=np.tile(np.arange(count)*3,4)
    birth=np.tile(np.arange(count)*2,4)
    times=np.column_stack([birth,birth+1,birth+np.repeat([0,1,1,0],count)])
    uv=np.repeat([[0,0],[1,0],[1,1],[0,1]],count,axis=0)
    colors=np.column_stack([uv,np.zeros(4*count),np.ones(4*count)])
    normals=np.tile([0,0,1],(4*count,1))
    indices=np.array([[i,i+count,i+2*count,i,i+2*count,i+3*count] for i in range(count)]).ravel()
    attrs={key:writer.accessor(values,vertex=True) for key,values in
           [('POSITION',positions),('NORMAL',normals),('TEXCOORD_0',uv),('COLOR_0',colors),('_TB_TIMESTAMP',times)]}
    writer.doc['nodes'][0]['mesh']=0
    writer.doc['meshes']=[{'name':'Batch','primitives':[{'attributes':attrs,'material':0,
                            'indices':writer.accessor(indices,indices=True)}]}]
    writer.save(path)


class StrokeMemoryTests(unittest.TestCase):
    def test_many_strokes_in_one_batch_are_bounded_and_preserve_geometry(self):
        with tempfile.TemporaryDirectory() as folder:
            source=Path(folder)/'source.glb';batched_fixture(source,600)
            original=source.read_bytes()
            for completed in (True,False):
                with self.subTest(completed=completed):
                    target=Path(folder)/f'{completed}.glb'
                    tracemalloc.start()
                    try:
                        report=animation.convert(source,target,completed=completed,max_slices=1)
                        peak=tracemalloc.get_traced_memory()[1]
                    finally:
                        tracemalloc.stop()
                    # The old retained batch copies alone exceed 140 MiB here.
                    # Leave generous space for JSON, output and allocator variation.
                    self.assertLess(peak,48*1024*1024,f'Peak traced allocation: {peak}')
                    self.assertEqual(len(report['strokes']),600)
                    reader=animation.Reader(target)
                    nodes=[n for n in reader.doc['nodes'] if 'mesh' in n]
                    self.assertEqual(len(nodes),600)
                    for i in (0,299,599):
                        n=nodes[i];attrs=reader.doc['meshes'][n['mesh']]['primitives'][0]['attributes']
                        v=reader.accessor(attrs['POSITION'])
                        world=v@quaternion_matrix(n['rotation']).T+n['translation']
                        np.testing.assert_allclose(world.min(axis=0),[i*3,0,0],atol=1e-5)
                        np.testing.assert_allclose(world.max(axis=0),[i*3+2,1,0],atol=1e-5)
                        self.assertAlmostEqual(animation.area(world.reshape(-1,3,3)).sum(),2,places=5)
                        uv=reader.accessor(attrs['TEXCOORD_0'])
                        np.testing.assert_allclose(reader.accessor(attrs['COLOR_0'])[:,:2],uv)
                        self.assertEqual(report['strokes'][i]['sourceStart'],i*2)
                    self.assertEqual('animations' in reader.doc,not completed)
                    self.assertEqual(source.read_bytes(),original)


if __name__=='__main__':unittest.main()
