"""Regression tests for geometric and timing correctness of stroke-growth export."""
import importlib.util
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest

import numpy as np

SPEC = importlib.util.spec_from_file_location('brush_animation', Path(__file__).with_name('animate-brush-glb.py'))
animation = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(animation)


def fixture(path, timestamps=True, brush_guid=None, tangents=False):
    source = SimpleNamespace(doc={'scenes': [{'nodes': [0]}], 'scene': 0,
        'nodes': [{'name': 'Stroke', 'translation': [2, 3, 4]}],
        'materials': [{'name': 'Ink', 'pbrMetallicRoughness': {'baseColorFactor': [1, .2, .1, 1]}}]})
    w = animation.Writer(source)
    positions = [[0, 0, 0], [2, 0, 0], [2, 1, 0], [0, 1, 0]]
    attrs = {'POSITION': w.accessor(positions, vertex=True),
             'TEXCOORD_0': w.accessor([[0, 0], [1, 0], [1, 1], [0, 1]], vertex=True),
             'COLOR_0': w.accessor([[0, 0, 0, 1], [1, 0, 0, 1], [1, 1, 0, 1], [0, 1, 0, 1]], vertex=True)}
    if brush_guid:
        w.doc['materials'][0]['extensions'] = {'GOOGLE_tilt_brush_material': {'guid': brush_guid}}
    if tangents:
        attrs['NORMAL'] = w.accessor([[0, 0, 1]]*4, vertex=True)
        attrs['TANGENT'] = w.accessor([[1, 0, 0, -1]]*4, vertex=True)
    if timestamps:
        attrs['_TB_TIMESTAMP'] = w.accessor([[10, 12, 10], [10, 12, 12], [10, 12, 12], [10, 12, 10]], vertex=True)
    w.doc['nodes'][0]['mesh'] = 0
    w.doc['meshes'] = [{'name': 'TestInk', 'primitives': [{'attributes': attrs,
                           'indices': w.accessor([0, 1, 2, 0, 2, 3], indices=True), 'material': 0}]}]
    w.save(path)


def quaternion_matrix(q):
    x, y, z, w = q
    return np.array([[1-2*y*y-2*z*z, 2*x*y-2*z*w, 2*x*z+2*y*w],
                     [2*x*y+2*z*w, 1-2*x*x-2*z*z, 2*y*z-2*x*w],
                     [2*x*z-2*y*w, 2*y*z+2*x*w, 1-2*x*x-2*y*y]])


class BrushAnimationTests(unittest.TestCase):
    def test_source_bloom_survives_clipping_without_replacing_rgba(self):
        for guid in animation.SOURCE_BLOOM_BRUSHES:
            with self.subTest(guid=guid), tempfile.TemporaryDirectory() as temp:
                src = Path(temp)/'source.glb'
                fixture(src, brush_guid=guid)
                original = src.read_bytes()
                # Fixture RGB equals (u,v,0). On the bottom edge, interpolated
                # source bloom red = u, whereas bloom(interpolated RGB) = u^2.2.
                for density in (2, 19):
                    dst = Path(temp)/f'growth-{density}.glb'
                    report = animation.convert(src, dst, slices_per_second=density)
                    self.assertEqual(report['sourceBloomVertices'], 4)
                    r = animation.Reader(dst)
                    interior = 0
                    for mesh in r.doc['meshes']:
                        attrs = mesh['primitives'][0]['attributes']
                        uv = r.accessor(attrs['TEXCOORD_0'])
                        rgba = r.accessor(attrs['COLOR_0'])
                        bloom = r.accessor(attrs[animation.SOURCE_BLOOM_ATTRIBUTE])
                        np.testing.assert_allclose(rgba[:, :2], uv, atol=1e-6)
                        mask = (abs(uv[:, 1]) < 1e-6) & (uv[:, 0] > .01) & (uv[:, 0] < .99)
                        interior += int(mask.sum())
                        np.testing.assert_allclose(bloom[mask, 0], uv[mask, 0], atol=1e-6)
                        np.testing.assert_allclose(bloom[mask, 1], uv[mask, 0]*.05**2.2, atol=1e-6)
                        self.assertTrue(np.all(abs(bloom[mask, 0]-uv[mask, 0]**2.2) > .005))
                    self.assertGreater(interior, 0)
                self.assertEqual(src.read_bytes(), original)

    def test_source_bloom_alpha_and_idempotence(self):
        with tempfile.TemporaryDirectory() as temp:
            src = Path(temp)/'source.glb'
            fixture(src, brush_guid=next(iter(animation.SOURCE_BLOOM_BRUSHES)))
            r = animation.Reader(src)
            attrs = r.doc['meshes'][0]['primitives'][0]['attributes']
            rgba = r.accessor(attrs['COLOR_0'])
            rgba[:, 3] = [.1, .5, .7, 1]
            before = rgba.copy()
            self.assertEqual(animation.prepare_source_bloom(r), 4)
            count = len(r.doc['accessors'])
            self.assertEqual(animation.prepare_source_bloom(r), 4)
            self.assertEqual(len(r.doc['accessors']), count)
            np.testing.assert_array_equal(r.accessor(attrs['COLOR_0']), before)
            np.testing.assert_allclose(r.accessor(attrs[animation.SOURCE_BLOOM_ATTRIBUTE])[:, 3], before[:, 3]**2.2)

    def test_sparks_flare_uses_export_units_and_original_vertices(self):
        # .5**2.84 is evaluated on a source vertex, before linear clipping.
        guid='50e99447-3861-05f4-697d-a1b96e771b98'
        attrs={'POSITION':0,'NORMAL':1,'_TB_UNITY_TEXCOORD_0':2}
        cache={0:np.zeros((3,3)),1:np.array([[0,0,1],[0,1,0],[0,0,-1.]]),
               2:np.array([[0,0,0],[.5,.5,0],[1,1,0]])}
        reader=SimpleNamespace(doc={'materials':[{'extensions':{'GOOGLE_tilt_brush_material':{'guid':guid}}}],
            'meshes':[{'name':'Sparks','primitives':[{'attributes':attrs,'material':0}]}]},cache=cache)
        reader.accessor=lambda i:reader.cache[i]
        report=animation.bake_static_brush_geometry(reader)
        expected=np.array([[0,0,0],[0,.145*(.5**2.84),0],[0,0,-.145]])
        np.testing.assert_allclose(reader.cache[0],expected)
        self.assertEqual(animation.bake_static_brush_geometry(reader),report)
        np.testing.assert_allclose(reader.cache[0],expected)
        # Sharing a position accessor with an unrelated brush must not modify it.
        del reader.static_geometry_bakes
        reader.doc['materials'].append({})
        reader.doc['meshes'][0]['primitives'].append({'attributes':attrs,'material':1})
        with self.assertRaisesRegex(ValueError,'shared with another brush'):
            animation.bake_static_brush_geometry(reader)
        np.testing.assert_allclose(reader.cache[0],expected)

    def test_rain_displacement_before_clipping_and_idempotence(self):
        with tempfile.TemporaryDirectory() as temp:
            src, dst = Path(temp)/'rain.glb', Path(temp)/'growth.glb'
            source=SimpleNamespace(doc={'scenes':[{'nodes':[0]}],'nodes':[{'name':'Rain'}]})
            w=animation.Writer(source)
            w.doc['materials']=[{'extensions':{'GOOGLE_tilt_brush_material':{'guid':'03a529e1-f519-3dd4-582d-2d5cd92c3f4f'}}}]
            points=[[0,0,0],[2,0,0],[2,1,0],[0,1,0]]
            attrs={'POSITION':w.accessor(points,vertex=True),
                   'NORMAL':w.accessor([[0,0,1]]*4,vertex=True),
                   'TEXCOORD_0':w.accessor([[0,0],[1,0],[1,1],[0,1]],vertex=True),
                   '_TB_UNITY_TEXCOORD_0':w.accessor([[0,0,.1],[1,0,.3],[1,1,.3],[0,1,.1]],vertex=True),
                   'COLOR_0':w.accessor([[.8,.1,.2,1]]*4,vertex=True),
                   '_TB_TIMESTAMP':w.accessor([[1,3,1],[1,3,3],[1,3,3],[1,3,1]],vertex=True)}
            w.doc['nodes'][0]['mesh']=0
            w.doc['meshes']=[{'name':'Rain','primitives':[{'attributes':attrs,'indices':w.accessor([0,1,2,0,2,3],indices=True),'material':0}]}]
            w.save(src);original=src.read_bytes();reader=animation.Reader(src)
            report=animation.bake_static_brush_geometry(reader)
            expected=[[0,0,.225],[2,0,.675],[2,1,.675],[0,1,.225]]
            np.testing.assert_allclose(reader.accessor(attrs['POSITION']),expected,atol=1e-7)
            self.assertEqual(animation.bake_static_brush_geometry(reader),report)
            np.testing.assert_allclose(reader.accessor(attrs['POSITION']),expected,atol=1e-7)
            animation.convert(src,dst,slices_per_second=4)
            output=animation.Reader(dst)
            self.assertEqual(output.doc['materials'][0]['extras']['openBrush']['vertexBake'],'rain-bulge-v1')
            for node in output.doc['nodes']:
                if 'mesh' not in node:continue
                a=output.doc['meshes'][node['mesh']]['primitives'][0]['attributes']
                p=output.accessor(a['POSITION'])@quaternion_matrix(node['rotation']).T+node['translation']
                np.testing.assert_allclose(p[:,2],.225+.225*p[:,0],atol=1e-6)
            self.assertEqual(src.read_bytes(),original)
            other=animation.Reader(src)
            del other.doc['meshes'][0]['primitives'][0]['attributes']['_TB_UNITY_TEXCOORD_0']
            with self.assertRaisesRegex(ValueError,'Rain needs source'):
                animation.bake_static_brush_geometry(other)

    def test_doubleflat_default_normal_and_tangent_roundtrip(self):
        with tempfile.TemporaryDirectory() as temp:
            src, dst = Path(temp)/'double.glb', Path(temp)/'growth.glb'
            fixture(src, brush_guid='f4a0550c-332a-4e1a-9793-b71508f4a454', tangents=True)
            original = src.read_bytes()
            report = animation.convert(src, dst, slices_per_second=4)
            self.assertEqual(len(report['materialDefaults']), 1)
            output = animation.Reader(dst)
            normal = output.doc['materials'][0]['normalTexture']
            image = output.doc['images'][output.doc['textures'][normal['index']]['source']]
            self.assertEqual(image['mimeType'], 'image/png')
            self.assertTrue(output.view(image['bufferView']).startswith(b'\x89PNG'))
            for node in output.doc['nodes']:
                if 'mesh' not in node:
                    continue
                attrs = output.doc['meshes'][node['mesh']]['primitives'][0]['attributes']
                rotation = quaternion_matrix(node['rotation'])
                tangent = output.accessor(attrs['TANGENT'])
                np.testing.assert_allclose(tangent[:, :3]@rotation.T, np.tile([1, 0, 0], (len(tangent), 1)), atol=1e-6)
                np.testing.assert_array_equal(tangent[:, 3], -1)
            self.assertEqual(src.read_bytes(), original)

    def test_normal_default_is_guid_scoped_idempotent_and_preserves_explicit_texture(self):
        explicit = {'index': 17, 'texCoord': 1, 'scale': .4}
        reader = SimpleNamespace(doc={'materials': [
            {'extensions': {'GOOGLE_tilt_brush_material': {'guid': 'f4a0550c-332a-4e1a-9793-b71508f4a454'}}},
            {'extensions': {'GOOGLE_tilt_brush_material': {'guid': 'f4a0550c-332a-4e1a-9793-b71508f4a454'}}, 'normalTexture': explicit.copy()},
            {'name': 'DoubleFlat'},  # Never infer identity from a display name.
        ]})
        first = animation.restore_source_material_defaults(reader)
        self.assertEqual(animation.restore_source_material_defaults(reader), first)
        self.assertEqual(len(reader.doc['images']), 1)
        self.assertEqual(reader.doc['materials'][1]['normalTexture'], explicit)
        self.assertNotIn('normalTexture', reader.doc['materials'][2])

    def test_audited_white_normal_defaults_share_one_portable_texture(self):
        reader = SimpleNamespace(doc={'materials': [
            {'extensions': {'GOOGLE_tilt_brush_material': {'guid': guid}}}
            for guid in animation.WHITE_NORMAL_DEFAULT_BRUSHES]})
        self.assertEqual(len(animation.restore_source_material_defaults(reader)), 5)
        self.assertEqual(len(reader.doc['textures']), 1)
        self.assertTrue(all(m['normalTexture'] == {'index': 0, 'texCoord': 0} for m in reader.doc['materials']))

    def tapered_fixture(self, path, include_offsets=True):
        source = SimpleNamespace(doc={'scenes': [{'nodes': [0]}], 'nodes': [{'name': 'Taper'}]})
        w = animation.Writer(source)
        w.doc['materials'] = [{'extensions': {'GOOGLE_tilt_brush_material': {
            'guid': '0d3889f3-3ede-470c-8af4-de4813306126'}}}]
        points = [[x, y, 0] for x in (0, 1, 2) for y in (-1, 1)]
        attrs = {'POSITION': w.accessor(points, vertex=True),
                 'TEXCOORD_0': w.accessor([[x/2, (y+1)/2] for x, y, _ in points], vertex=True),
                 '_TB_TIMESTAMP': w.accessor([[10, 12, 10+x] for x, _, _ in points], vertex=True)}
        if include_offsets:
            attrs['_TB_UNITY_TEXCOORD_1'] = w.accessor([[0, y, 0] for _, y, _ in points], vertex=True)
        w.doc['meshes'] = [{'name': 'DoubleTaperedMarker', 'primitives': [{'attributes': attrs,
            'indices': w.accessor([0, 2, 3, 0, 3, 1, 2, 4, 5, 2, 5, 3], indices=True), 'material': 0}]}]
        w.doc['nodes'][0]['mesh'] = 0
        w.save(path)

    def test_taper_collapses_tips_preserves_middle_and_is_idempotent(self):
        with tempfile.TemporaryDirectory() as temp:
            src = Path(temp)/'taper.glb'
            self.tapered_fixture(src)
            original = src.read_bytes()
            reader = animation.Reader(src)
            report = animation.bake_static_brush_geometry(reader)
            attrs = reader.doc['meshes'][0]['primitives'][0]['attributes']
            expected = [[0, 0, 0], [0, 0, 0], [1, -1, 0], [1, 1, 0], [2, 0, 0], [2, 0, 0]]
            np.testing.assert_allclose(reader.accessor(attrs['POSITION']), expected, atol=1e-7)
            self.assertEqual(animation.bake_static_brush_geometry(reader), report)
            np.testing.assert_allclose(reader.accessor(attrs['POSITION']), expected, atol=1e-7)
            self.assertEqual(src.read_bytes(), original)

    def test_taper_growth_completes_to_baked_surface(self):
        with tempfile.TemporaryDirectory() as temp:
            src, dst = Path(temp)/'taper.glb', Path(temp)/'growth.glb'
            self.tapered_fixture(src)
            report = animation.convert(src, dst, slices_per_second=4)
            self.assertEqual(len(report['staticGeometryBakes']), 1)
            r = animation.Reader(dst)
            total_area = 0
            for node in r.doc['nodes']:
                if 'mesh' not in node:
                    continue
                attrs = r.doc['meshes'][node['mesh']]['primitives'][0]['attributes']
                p = r.accessor(attrs['POSITION'])@quaternion_matrix(node['rotation']).T+node['translation']
                # Diamond envelope after taper, not the raw rectangular ribbon.
                self.assertTrue(np.all(np.abs(p[:, 1]) <= 1-np.abs(p[:, 0]-1)+1e-6))
                total_area += animation.area(p.reshape(-1, 3, 3)).sum()
            self.assertAlmostEqual(total_area, 2, places=6)

    def test_taper_missing_offsets_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            src = Path(temp)/'taper.glb'
            self.tapered_fixture(src, include_offsets=False)
            with self.assertRaisesRegex(ValueError, 'original vertex offsets'):
                animation.bake_static_brush_geometry(animation.Reader(src))

    def test_clipping_preserves_surface_and_interpolated_uv(self):
        # Columns: xyz, uv, time. Time and U both depend linearly on x.
        v = np.array([[0, 0, 0, 0, 0, 0], [2, 0, 0, 1, 0, 2], [0, 2, 0, 0, 1, 0]], float)
        bands = animation.split_triangles(v, np.array([[0, 1, 2]]), 0, 2, 8)
        pieces = np.concatenate([np.array(b) for b in bands if b])
        self.assertAlmostEqual(float(animation.area(pieces).sum()), 2)
        np.testing.assert_allclose(pieces[:, :, 3], pieces[:, :, 0]/2)
        np.testing.assert_allclose(pieces[:, :, 4], pieces[:, :, 1]/2)

    def test_flat_timestamp_on_boundary_is_not_duplicated(self):
        v = np.array([[0, 0, 0, 1], [1, 0, 0, 1], [0, 1, 0, 1]], float)
        bands = animation.split_triangles(v, np.array([[0, 1, 2]]), 0, 2, 4)
        self.assertEqual(sum(len(b) for b in bands), 1)
        self.assertEqual(len(bands[2]), 1)

    def test_gap_capping_keeps_overlapping_strokes_and_draw_speeds(self):
        t = animation.Timeline([(10, 13), (11, 12), (20, 22)], 1, 1, .5)
        self.assertEqual(t(10), 1)
        self.assertEqual(t(11), 2)
        self.assertEqual(t(13), 4)
        self.assertEqual(t(20), 4.5)
        self.assertEqual(t(22), 6.5)

    def test_quaternion_reconstructs_chunk_frame(self):
        for axis in np.vstack([np.eye(3), -np.eye(3), [[1, 2, -3]]]):
            p = np.array([np.r_[np.zeros(3), 0], np.r_[axis, 1], np.r_[axis+.01, 1]])
            pivot, basis, q = animation.frame_for_band(p)
            np.testing.assert_allclose(quaternion_matrix(q), basis, atol=1e-12)

    def test_complete_export_retains_geometry_colors_uv_and_parent_transform(self):
        with tempfile.TemporaryDirectory() as temp:
            src, dst = Path(temp)/'input.glb', Path(temp)/'output.glb'
            fixture(src)
            original = src.read_bytes()
            report = animation.convert(src, dst, slices_per_second=4)
            self.assertEqual(src.read_bytes(), original)
            r = animation.Reader(dst)
            self.assertEqual(r.doc['nodes'][0]['translation'], [2, 3, 4])
            self.assertEqual(report['durationSeconds'], 6)
            total_area = 0
            for node in r.doc['nodes']:
                if 'mesh' not in node:
                    continue
                attrs = r.doc['meshes'][node['mesh']]['primitives'][0]['attributes']
                p = r.accessor(attrs['POSITION'])@quaternion_matrix(node['rotation']).T+node['translation']
                uv, color = r.accessor(attrs['TEXCOORD_0']), r.accessor(attrs['COLOR_0'])
                np.testing.assert_allclose(p[:, 2], 0, atol=1e-6)
                np.testing.assert_allclose(uv[:, 0], p[:, 0]/2, atol=1e-6)
                np.testing.assert_allclose(uv[:, 1], p[:, 1], atol=1e-6)
                np.testing.assert_allclose(color[:, :2], uv, atol=1e-6)
                total_area += animation.area(p.reshape(-1, 3, 3)).sum()
            self.assertAlmostEqual(total_area, 2, places=6)
            for sampler in r.doc['animations'][0]['samplers']:
                times = r.accessor(sampler['input']).ravel()
                values = r.accessor(sampler['output'])
                self.assertTrue(np.all(np.diff(times) > 0))
                np.testing.assert_array_equal(values[0], [0, 0, 0])
                np.testing.assert_array_equal(values[-1], [1, 1, 1])
                self.assertTrue(np.all(np.diff(values[:, 2]) >= 0))

    def test_missing_timestamps_fail_instead_of_silent_static_stroke(self):
        with tempfile.TemporaryDirectory() as temp:
            src = Path(temp)/'input.glb'
            fixture(src, timestamps=False)
            with self.assertRaisesRegex(ValueError, 'Missing _TB_TIMESTAMP'):
                animation.convert(src, Path(temp)/'output.glb')

    def test_brush_identity_survives_custom_extension_removal(self):
        source = SimpleNamespace(doc={'scenes': [{'nodes': [0]}], 'nodes': [{}],
            'materials': [{'name': 'brush_Smoke', 'extensions': {
                'GOOGLE_tilt_brush_material': {'guid': '70d79cca-b159-4f35-990c-f02193947fe8'}}}]})
        writer = animation.Writer(source)
        material = writer.doc['materials'][0]
        self.assertNotIn('extensions', material)
        self.assertEqual(material['extras']['openBrush']['brushGuid'],
                         '70d79cca-b159-4f35-990c-f02193947fe8')

    def test_batched_strokes_have_unique_targets_and_material_manifest(self):
        with tempfile.TemporaryDirectory() as temp:
            src, dst = Path(temp)/'input.glb', Path(temp)/'output.glb'
            fixture(src)
            reader = animation.Reader(src)
            writer = animation.Writer(reader)
            primitive = reader.doc['meshes'][0]['primitives'][0]
            attributes = {}
            for key, accessor in primitive['attributes'].items():
                original = reader.accessor(accessor)
                second = original.copy()
                if key == 'POSITION':
                    second[:, 1] += 2
                elif key == '_TB_TIMESTAMP':
                    second += 4
                attributes[key] = writer.accessor(np.concatenate([original, second]), vertex=True)
            writer.doc['meshes'] = [{'name': 'SameBrush', 'primitives': [{
                'attributes': attributes, 'indices': writer.accessor(
                    [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7], indices=True), 'material': 0}]}]
            writer.doc['nodes'][0]['mesh'] = 0
            writer.save(src)
            report = animation.convert(src, dst, slices_per_second=2)
            self.assertEqual(len(report['strokes']), 2)
            manifest = animation.material_manifest(dst)
            targets = manifest['materials'][0]['targets']
            self.assertEqual(len(targets), report['segments'])
            names = [target['nodeName'] for target in targets]
            self.assertEqual(len(names), len(set(names)))
            self.assertTrue(manifest['materials'][0]['usesVertexColors'])

    def test_refuses_to_overwrite_input(self):
        with self.assertRaisesRegex(ValueError, 'different files'):
            animation.convert('same.glb', 'same.glb')


if __name__ == '__main__':
    unittest.main()
