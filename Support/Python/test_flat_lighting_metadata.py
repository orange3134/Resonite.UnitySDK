import importlib.util,json,tempfile,unittest
from pathlib import Path
from types import SimpleNamespace
spec=importlib.util.spec_from_file_location('animation',Path(__file__).with_name('animate-brush-glb.py'))
animation=importlib.util.module_from_spec(spec);spec.loader.exec_module(animation)
GUID='d3f3b18a-da03-f694-b838-28ba8e749a98'
def fixture(path,extras,guid=GUID):
 source=SimpleNamespace(doc={'scene':0,'scenes':[{'nodes':[],'extras':extras}],'nodes':[],
  'materials':[{'extensions':{'GOOGLE_tilt_brush_material':{'guid':guid}}}]})
 animation.Writer(source).save(path)
class LightingMetadataTests(unittest.TestCase):
 def test_original_gamma_light_values_and_export_rotation_preserved(self):
  with tempfile.TemporaryDirectory() as temp:
   p=Path(temp)/'model.glb';fixture(p,dict(TB_AmbientLightColor='0.1, 0.2, 0.3',TB_SceneLight0Color='2, 1, 0.5',TB_SceneLight0Rotation='60, -1.7E-06, 334'))
   light=animation.material_manifest(p)['flatLighting']
   self.assertEqual(light['ambient'],[.1,.2,.3]);self.assertEqual(light['mainColor'],[2,1,.5]);self.assertEqual(light['mainRotation'],[60,-1.7e-6,334])
 def test_missing_or_nonfinite_metadata_rejected(self):
  with tempfile.TemporaryDirectory() as temp:
   p=Path(temp)/'model.glb'
   for extras in ({},dict(TB_AmbientLightColor='NaN,0,0'),dict(TB_AmbientLightColor='-1,0,0')):
    fixture(p,extras)
    with self.assertRaises(ValueError):animation.material_manifest(p)
 def test_other_brush_does_not_require_source_light(self):
  with tempfile.TemporaryDirectory() as temp:
   p=Path(temp)/'model.glb';fixture(p,{},'other')
   self.assertNotIn('flatLighting',animation.material_manifest(p))
if __name__=='__main__':unittest.main()
