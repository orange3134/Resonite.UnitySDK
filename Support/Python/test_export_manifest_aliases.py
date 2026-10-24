"""Membrane registration preserves every existing material and shader path."""
import importlib.util,json,tempfile,unittest
from pathlib import Path
spec=importlib.util.spec_from_file_location('aliases',Path(__file__).with_name('extend-openbrush-export-manifest.py'))
aliases=importlib.util.module_from_spec(spec);spec.loader.exec_module(aliases)
class ManifestAliasTests(unittest.TestCase):
 def test_preserves_content_and_existing_format(self):
  with tempfile.TemporaryDirectory() as temp:
   src=Path(temp)/'source.json';out=Path(temp)/'out.json';again=Path(temp)/'again.json'
   brushes={base:dict(guid=base,textures={'MainTex':'DiamondHull-c8313697-2563-47fc-832e-290f4c04b901-v10.0-MainTex.png'} if name=='DiamondMembrane' else {},blendMode=2 if name=='DiamondMembrane' else 0,enableCull=False,name='Hull',vertexShader='existing.glsl',floatParams={'value':1.0}) for name,base in aliases.ALIASES.values()}
   text=json.dumps({'version':'kept','brushes':brushes},indent=2)+'\n';src.write_text(text)
   self.assertEqual(len(aliases.extend(src,out)),3);result=json.loads(out.read_text())
   for guid,(name,base) in aliases.ALIASES.items():
    expected=dict(brushes[base],guid=guid,name=name);self.assertEqual(result['brushes'][guid],expected)
   for guid,original in brushes.items():self.assertEqual(result['brushes'][guid],original)
   self.assertTrue(out.read_text().startswith(text[:text.rfind('\n  }\n}')]))
   self.assertEqual(aliases.extend(out,again),[]);self.assertEqual(out.read_bytes(),again.read_bytes())
   with self.assertRaises(FileExistsError):aliases.extend(src,out)
 def test_changed_hull_material_rejected(self):
  with tempfile.TemporaryDirectory() as temp:
   src=Path(temp)/'source.json'
   brushes={base:dict(guid=base,textures={'unexpected':'texture.png'},blendMode=0,enableCull=False) for name,base in aliases.ALIASES.values()}
   src.write_text(json.dumps({'brushes':brushes},indent=2))
   with self.assertRaisesRegex(ValueError,'changed'):aliases.extend(src,Path(temp)/'out.json')
if __name__=='__main__':unittest.main()
