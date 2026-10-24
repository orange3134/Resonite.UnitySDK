import base64
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import struct
import tempfile
import unittest
import numpy as np

spec=importlib.util.spec_from_file_location('bundle',Path(__file__).with_name('prepare-openbrush-bundle.py'))
bundle=importlib.util.module_from_spec(spec);spec.loader.exec_module(bundle)
MARKER='429ed64a-4e97-4466-84d3-145a861ef684'

def fixture(path,guids,broken=False):
    binary=bytearray();doc=dict(asset=dict(version='2.0'),buffers=[{}],bufferViews=[],accessors=[],nodes=[],meshes=[],materials=[],scenes=[dict(nodes=[])],scene=0)
    def accessor(values,kind):
        a=np.array(values,dtype=np.uint32 if kind=='SCALAR' else np.float32)
        while len(binary)%4:binary.append(0)
        offset=len(binary);binary.extend(a.tobytes());view=len(doc['bufferViews'])
        doc['bufferViews'].append(dict(buffer=0,byteOffset=offset,byteLength=a.nbytes))
        index=len(doc['accessors']);doc['accessors'].append(dict(bufferView=view,componentType=5125 if kind=='SCALAR' else 5126,count=len(a),type=kind))
        return index
    for n,guid in enumerate(guids):
        pos=[[-1,-.1,0],[-1,.1,0],[1,-.1,0],[1,.1,0]];uv=[[0,0],[0,1],[1,0],[1,1]];tri=[0,2,1,1,2,3];norm=[[0,0,1]]*4;time=[0,0,1,1];extra={}
        if guid in (bundle.DISCO,bundle.LIGHTWIRE):
            pos=[];norm=[];uv3=[];time=[];tri=[]
            for i,u in enumerate([0,.25,.5,.75,1]):
                for j,(y,z) in enumerate([(1,0),(0,1),(-1,0),(0,-1)]):
                    norm.append([0,y,z]);pos.append([u,.1*y,.1*z]);uv3.append([u,j/4,.1]);time.append(u)
                if i:
                    for j in range(4):
                        a=(i-1)*4+j;b=(i-1)*4+(j+1)%4;c=i*4+j;d=i*4+(j+1)%4;tri.extend([a,c,b,b,c,d])
            uv=[v[:2] for v in uv3];extra['_TB_UNITY_TEXCOORD_0']=accessor(uv3,'VEC3')
        elif guid==bundle.ELECTRICITY:
            offsets=[[0,-.1,0],[0,.1,0],[0,-.1,0],[0,.1,0]]
            if broken:offsets[0][0]=.5
            extra['_TB_UNITY_TEXCOORD_0']=accessor(uv,'VEC2');extra['_TB_UNITY_TEXCOORD_1']=accessor(offsets,'VEC3')
        elif guid in (bundle.HYPERGRID,bundle.DANCEFLOOR):
            pos=[[-1,-1,0],[-1,1,0],[1,-1,0],[1,1,0]]
            offsets=[[x,y,z,10] for x,y,z in pos]
            if broken:offsets[0][0]=-.5
            extra['_TB_UNITY_TEXCOORD_0']=accessor(uv,'VEC2');extra['_TB_UNITY_TEXCOORD_1']=accessor(offsets,'VEC4')
        elif guid in bundle.PARTICLES:
            pos=[[-1,-1,0],[-1,1,0],[1,-1,0],[1,1,0]]
            extra['_TB_UNITY_NORMAL']=accessor([[0,0,0]]*4,'VEC3')
            if guid!=bundle.DOTS:extra['_TB_UNITY_TEXCOORD_1']=accessor([[0,0,0]]*4,'VEC3')
            extra['_TB_UNITY_TEXCOORD_0']=accessor([[*v,0,10] for v in uv],'VEC4')
        attrs=dict(POSITION=accessor(pos,'VEC3'),NORMAL=accessor(norm,'VEC3'),COLOR_0=accessor([[.2,.3,.4,1]]*len(pos),'VEC4'),TEXCOORD_0=accessor(uv,'VEC2'),_TB_TIMESTAMP=accessor([[n*2,n*2+1,n*2+t] for t in time],'VEC3'),**extra)
        doc['materials'].append(dict(name='material_'+str(n),extensions=dict(GOOGLE_tilt_brush_material=dict(guid=guid)),pbrMetallicRoughness=dict(baseColorTexture=dict(index=0))))
        doc['meshes'].append(dict(primitives=[dict(attributes=attrs,indices=accessor(tri,'SCALAR'),material=n)]))
        doc['nodes'].append(dict(name='brush_'+str(n),mesh=n));doc['scenes'][0]['nodes'].append(n)
    # Exercise embedding of a relative texture path containing a space.
    image=b'\x89PNG\r\n\x1a\nfixture-bytes-preserved-without-decoding'
    (path.parent/'source pattern.png').write_bytes(image)
    doc['images']=[dict(uri='source%20pattern.png')];doc['textures']=[dict(source=0)]
    doc['buffers'][0]['byteLength']=len(binary);js=json.dumps(doc).encode();js+=b' '*((-len(js))%4);binary+=b'\0'*((-len(binary))%4)
    path.write_bytes(struct.pack('<III',0x46546c67,2,28+len(js)+len(binary))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(binary),0x004e4942)+binary)

class Tests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.sdk=Path(os.environ.get('OPENBRUSH_SDK_ROOT',bundle.SDK))
    def tearDown(self):self.temp.cleanup()
    def prepare(self,guids,**kwargs):
        source=self.root/'raw.glb';fixture(source,guids)
        return bundle.prepare(source,self.root/'prepared bundle',sdk_root=self.sdk,slices_per_second=4,**kwargs)
    def test_all_auxiliary_inputs_and_relocation(self):
        data=self.prepare([MARKER,bundle.LIGHTWIRE,bundle.DISCO,bundle.ELECTRICITY,sorted(bundle.PARTICLES)[0]],speed=2,lead=.3,hold=1,max_gap=.2)
        self.assertEqual(set(data['files']),{'model','materials','timeline','disco','electricity','particles'})
        folder=self.root/'prepared bundle';moved=self.root/'relocated bundle';folder.rename(moved)
        (self.root/'raw.glb').unlink();(self.root/'source pattern.png').unlink()
        for entry in data['hashes']:self.assertEqual(bundle.sha256(moved/entry['path']),entry['sha256'])
        reader=bundle.load('animate-brush-glb').Reader(moved/data['files']['model'])
        self.assertTrue(all('uri' not in image or image['uri'].startswith('data:') for image in reader.doc['images']))
        timeline=json.loads((moved/data['files']['timeline']).read_text());self.assertEqual(len(timeline['segments']),data['growthParts'])
        self.assertAlmostEqual(min(s['start'] for s in timeline['segments']),.3)
        self.assertTrue(json.loads((moved/'conversion-report.json').read_text())['lightWireShapeBaked'])
        self.assertFalse((moved/'lightwire-intermediate.glb').exists())
        electricity=json.loads((moved/'electricity.json').read_text());self.assertEqual(electricity['parts'][0]['nodeName'],'brush_3')
    def test_hypergrid_original_quad_offsets(self):
        data=self.prepare([bundle.HYPERGRID])
        self.assertIn('hypergrid',data['files'])
        p=json.loads((self.root/'prepared bundle'/'hypergrid.json').read_text())['parts'][0]
        self.assertEqual(len(p['births']),1)
        self.assertEqual(p['sourceBirths'],[10])
        self.assertEqual(p['positions'],p['offsets'])
        self.assertEqual(len(p['triangles']),6)
    def test_hypergrid_rejects_predeformed_geometry(self):
        source=self.root/'raw.glb';fixture(source,[bundle.HYPERGRID],broken=True)
        with self.assertRaisesRegex(ValueError,'deformed'):bundle.prepare(source,self.root/'out',sdk_root=self.sdk)
        self.assertFalse((self.root/'out').exists())
    def test_dancefloor_original_phase_and_normals(self):
        data=self.prepare([bundle.DANCEFLOOR])
        self.assertIn('dancefloor',data['files'])
        p=json.loads((self.root/'prepared bundle'/'dancefloor.json').read_text())['parts'][0]
        self.assertEqual(p['sourceBirths'],[10])
        self.assertEqual(p['normals'],[dict(x=0,y=0,z=-1)]*4)
        self.assertEqual(p['positions'],p['offsets'])
    def test_dancefloor_rejects_predeformed_geometry(self):
        source=self.root/'raw.glb';fixture(source,[bundle.DANCEFLOOR],broken=True)
        with self.assertRaisesRegex(ValueError,'deformed'):bundle.prepare(source,self.root/'out',sdk_root=self.sdk)
        self.assertFalse((self.root/'out').exists())
    def test_dots_original_without_spread_channel(self):
        data=self.prepare([bundle.DOTS])
        self.assertIn('particles',data['files'])
        part=json.loads((self.root/'prepared bundle'/'particles.json').read_text())['parts'][0]
        self.assertEqual(part['brushGuid'],bundle.DOTS)
        particle=part['particles'][0]
        self.assertEqual(particle['center'],particle['origin'])
        self.assertAlmostEqual(particle['size'],2)
        self.assertAlmostEqual(particle['color']['a'],1)
    def test_rising_bubbles_retains_motion_inputs_before_slicing(self):
        guid='a8147ce1-005e-abe4-88e8-09a1eaadcc89'
        data=self.prepare([guid])
        self.assertIn('particles',data['files'])
        part=json.loads((self.root/'prepared bundle'/'particles.json').read_text())['parts'][0]
        self.assertEqual(part['brushGuid'],guid)
        self.assertEqual(len(part['particles']),1)
        p=part['particles'][0]
        self.assertAlmostEqual(p['size'],2)
        self.assertAlmostEqual(p['birth'],.5)
        self.assertAlmostEqual(p['color']['r'],.2)
        self.assertAlmostEqual(p['color']['a'],1)
        # A missing spread origin must fail instead of silently moving all bubbles
        # from the center of the scene or drawing static quads.
        extract=bundle.load('extract-particle-motion')
        reader=extract.growth.Reader(self.root/'raw.glb')
        del reader.doc['meshes'][0]['primitives'][0]['attributes']['_TB_UNITY_TEXCOORD_1']
        with self.assertRaisesRegex(ValueError,'spread origin'):extract.extract(reader)
    def test_simple_brush_has_no_unneeded_sidecars_and_input_unchanged(self):
        source=self.root/'raw.glb';fixture(source,[MARKER]);before=source.read_bytes()
        data=bundle.prepare(source,self.root/'out',sdk_root=self.sdk,slices_per_second=4)
        self.assertEqual(set(data['files']),{'model','materials','timeline'});self.assertEqual(source.read_bytes(),before)
    def test_completed_preserves_full_geometry_without_growth_keys(self):
        data=self.prepare([MARKER,MARKER],completed=True,effect_loop_seconds=6)
        folder=self.root/'prepared bundle'
        reader=bundle.load('animate-brush-glb').Reader(folder/data['files']['model'])
        self.assertNotIn('animations',reader.doc)
        parts=[n for n in reader.doc['nodes'] if 'mesh' in n]
        self.assertEqual(len(parts),2)
        self.assertTrue(all(n['scale']==[1,1,1] for n in parts))
        triangle_count=0;surface_area=0
        for node in parts:
            primitive=reader.doc['meshes'][node['mesh']]['primitives'][0]
            vertices=reader.accessor(primitive['attributes']['POSITION']).reshape(-1,3,3)
            triangle_count+=len(vertices)
            surface_area+=bundle.load('animate-brush-glb').area(vertices).sum()
        self.assertEqual(triangle_count,4)
        self.assertAlmostEqual(surface_area,.8,places=6)
        self.assertEqual(data['mode'],'completed');self.assertEqual(data['growthParts'],0)
        self.assertEqual(data['duration'],0);self.assertEqual(data['effectLoopSeconds'],6)
        timeline=json.loads((folder/'timeline.json').read_text())
        self.assertTrue(timeline['completed']);self.assertEqual(timeline['duration'],6)
        self.assertEqual([s['sourceStart'] for s in timeline['strokes']],[0,2])
        for item in data['hashes']:self.assertEqual(bundle.sha256(folder/item['path']),item['sha256'])
    def test_completed_retains_raw_brush_effect_inputs(self):
        data=self.prepare([bundle.DISCO,bundle.ELECTRICITY,bundle.DANCEFLOOR,bundle.DOTS],completed=True)
        self.assertTrue({'disco','electricity','dancefloor','particles'}.issubset(data['files']))
        timeline=json.loads((self.root/'prepared bundle'/'timeline.json').read_text())
        self.assertEqual(len(timeline['strokes']),4)
        self.assertEqual(len(timeline['segments']),4)
    def test_completed_rejects_unbounded_effect_loop(self):
        for seconds in (float('nan'),0,-1,61):
            with self.assertRaisesRegex(ValueError,'Effect loop'):
                self.prepare([MARKER],completed=True,effect_loop_seconds=seconds)
        self.assertFalse((self.root/'prepared bundle').exists())
    def test_existing_directory_is_preserved(self):
        out=self.root/'prepared bundle';out.mkdir();(out/'keep').write_text('keep')
        with self.assertRaises(FileExistsError):self.prepare([MARKER])
        self.assertEqual((out/'keep').read_text(),'keep')
    def test_unsupported_brush_leaves_no_output(self):
        with self.assertRaisesRegex(ValueError,'Unsupported'):self.prepare(['00000000-0000-0000-0000-000000000000'])
        self.assertFalse((self.root/'prepared bundle').exists())
    def test_failed_auxiliary_extraction_publishes_nothing(self):
        source=self.root/'raw.glb';fixture(source,[bundle.DISCO,bundle.ELECTRICITY],broken=True)
        with self.assertRaisesRegex(ValueError,'deformed'):bundle.prepare(source,self.root/'out',sdk_root=self.sdk)
        self.assertFalse((self.root/'out').exists());self.assertFalse(list(self.root.glob('out.preparing-*')))
    def test_bad_time_settings_fail_before_output(self):
        with self.assertRaisesRegex(ValueError,'playback'):self.prepare([MARKER],speed=float('nan'))
        self.assertFalse((self.root/'prepared bundle').exists())

if __name__=='__main__':unittest.main()
