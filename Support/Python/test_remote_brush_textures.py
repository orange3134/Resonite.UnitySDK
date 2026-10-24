import base64
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest

here=Path(__file__).parent
def load(name):
 spec=importlib.util.spec_from_file_location(name,here/(name+'.py'))
 module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
helper=load('embed-openbrush-textures')
bundle=load('prepare-openbrush-bundle')
fixtures=load('test_prepare_openbrush_bundle')
URI='https://www.tiltbrush.com/shaders/brushes/Example/version-MainTex.png'

def change_uri(path, uri):
 doc,binary=helper.read_glb(path)
 doc['images'][0]['uri']=uri
 js=json.dumps(doc).encode();js+=b' '*(-len(js)%4)
 path.write_bytes(struct.pack('<III',0x46546c67,2,28+len(js)+len(binary))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(binary),0x004e4942)+binary)

class Tests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
  self.source=self.root/'raw.glb';self.assets=self.root/'brush assets';(self.assets/'Example').mkdir(parents=True)
  self.png=b'\x89PNG\r\n\x1a\nexact image bytes'
  (self.assets/'Example/version-MainTex.png').write_bytes(self.png)
  fixtures.fixture(self.source,[bundle.LIGHTWIRE,fixtures.MARKER]);change_uri(self.source,URI)
 def tearDown(self):self.temp.cleanup()
 def test_embeds_images_without_geometry_changes(self):
  before=self.source.read_bytes();original,binary=helper.read_glb(self.source)
  dst=self.root/'localized.glb';self.assertEqual(helper.embed(self.source,dst,self.assets),1)
  after,new_binary=helper.read_glb(dst)
  self.assertEqual(binary,new_binary);self.assertEqual(self.source.read_bytes(),before)
  self.assertEqual(base64.b64decode(after['images'][0]['uri'].split(',')[1]),self.png)
  after['images']=original['images'];self.assertEqual(after,original)
 def test_full_bundle_with_lightwire_and_source_hash(self):
  source_hash=bundle.sha256(self.source);out=self.root/'bundle'
  result=bundle.prepare(self.source,out,brush_assets=self.assets)
  self.assertEqual(result['source']['sha256'],source_hash)
  self.assertEqual(bundle.sha256(self.source),source_hash)
  report=json.loads((out/'conversion-report.json').read_text())
  self.assertEqual(report['localizedTextureCount'],1);self.assertTrue(report['lightWireShapeBaked'])
  self.assertFalse(list(out.glob('*intermediate*')))
  reader=bundle.load('animate-brush-glb').Reader(out/'drawing.glb')
  self.assertEqual(bytes(reader.view(reader.doc['images'][0]['bufferView'])),self.png)
  for h in result['hashes']:self.assertEqual(bundle.sha256(out/h['path']),h['sha256'])
 def test_full_bundle_without_lightwire(self):
  fixtures.fixture(self.source,[fixtures.MARKER]);change_uri(self.source,URI)
  bundle.prepare(self.source,self.root/'bundle',brush_assets=self.assets)
  self.assertFalse(list((self.root/'bundle').glob('*intermediate*')))
 def test_remote_requires_explicit_asset_root(self):
  with self.assertRaisesRegex(ValueError,'--brush-assets'):bundle.prepare(self.source,self.root/'bundle')
  self.assertFalse((self.root/'bundle').exists());self.assertFalse(list(self.root.glob('*.preparing-*')))
 def test_missing_texture_does_not_publish(self):
  (self.assets/'Example/version-MainTex.png').unlink()
  with self.assertRaisesRegex(FileNotFoundError,'Local brush texture'):bundle.prepare(self.source,self.root/'bundle',brush_assets=self.assets)
  self.assertFalse((self.root/'bundle').exists());self.assertFalse(list(self.root.glob('*.preparing-*')))
 def test_rejects_unknown_hosts_and_escaping_paths(self):
  for uri in ['https://example.com/shaders/brushes/Example/version-MainTex.png',
              'https://www.tiltbrush.com.evil.test/shaders/brushes/Example/version-MainTex.png',
              'file:///C:/unrelated.png','//server/share.png',
              'https://www.tiltbrush.com/shaders/brushes/%2e%2e/private.png',
              'https://www.tiltbrush.com/shaders/brushes/%5cprivate.png',
              '../source%20pattern.png']:
   with self.subTest(uri=uri),self.assertRaises(ValueError):helper.resolve_texture(self.source,uri,self.assets)
 def test_local_relative_and_embedded_images_are_preserved(self):
  fixtures.fixture(self.source,[fixtures.MARKER])
  dst=self.root/'localized.glb';helper.embed(self.source,dst,self.assets)
  doc,binary=helper.read_glb(dst);embedded=doc['images'][0]['uri']
  dst2=self.root/'localized2.glb';self.assertEqual(helper.embed(dst,dst2,self.assets),0)
  self.assertEqual(helper.read_glb(dst2)[0]['images'][0]['uri'],embedded)

if __name__=='__main__':unittest.main()
