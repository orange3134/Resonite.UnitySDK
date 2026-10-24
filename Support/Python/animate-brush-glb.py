#!/usr/bin/env python3
"""Turn an Open Brush GLB with _TB_TIMESTAMP into portable stroke growth.

Requires Python 3.9+ and numpy (pip install -r requirements-brush-animation.txt).
Example:
  python animate-brush-glb.py input.glb output.glb --max-gap 0.5

Uses the exported brush geometry, UVs, colors and materials, not substitute tubes.
Triangles are clipped into time bands; each band grows along its local direction
using core glTF scale animation. The completed surface equals the source surface.
Timing inside a stroke is approximate: Open Brush's _TB_TIMESTAMP.z is a linear
resampling of control-point timestamps, not an exact vertex creation time. Hulls,
particles and shader-deformed brushes are exported-surface reveals, not a replay
of their simulation. This tool does not reproduce Unity-only shaders.
"""
import argparse
import base64
import copy
import json
import math
from pathlib import Path
import struct
from urllib.parse import unquote, urlparse

import numpy as np

DTYPES = {5120: 'i1', 5121: 'u1', 5122: '<i2', 5123: '<u2', 5125: '<u4', 5126: '<f4'}
WIDTHS = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
DROP_EXTENSIONS = {'GOOGLE_tilt_brush_material', 'EXT_blend_operations'}
# Preserve this nonlinear vertex result separately from the original RGBA.
# ChromaticWave uses both varyings; evaluating pow after clipping is incorrect.
SOURCE_BLOOM_ATTRIBUTE = '_OB_BLOOM_V1'
SOURCE_BLOOM_BRUSHES = {'0f0ff7b2-a677-45eb-a7d6-0cd7206f4816',
                       '53d753ef-083c-45e1-98e7-4459b4471219'}
# Normal m_Material uses StandardSingleSided with its implicit white _Bump.
# Keep this explicit: other shaders commonly use a flat normal when unset.
WHITE_NORMAL_DEFAULT_BRUSHES = {
    'f8ba3d18-01fc-4d7b-b2d9-b99d10b8e7cf': 'KeijiroTube',
    'f4a0550c-332a-4e1a-9793-b71508f4a454': 'DoubleFlat',
    '62fef968-e842-3224-4a0e-1fdb7cfb745c': 'Wire (Lit)',
    'f0a2298a-be80-432c-9fee-a86dcc06f4f9': 'SingleSided',
    '9568870f-8594-60f4-1b20-dfbc8a5eac0e': 'TaperedWire',
}


class Reader:
    def __init__(self, path):
        self.path = Path(path)
        data = self.path.read_bytes()
        if len(data) < 28 or struct.unpack_from('<III', data) != (0x46546C67, 2, len(data)):
            raise ValueError('Expected a complete glTF 2.0 GLB')
        size, kind = struct.unpack_from('<II', data, 12)
        if kind != 0x4E4F534A:
            raise ValueError('Missing JSON chunk')
        self.doc = json.loads(data[20:20+size])
        bsize, bkind = struct.unpack_from('<II', data, 20+size)
        if bkind != 0x004E4942 or 28+size+bsize != len(data):
            raise ValueError('Expected a single embedded BIN chunk')
        self.binary = data[28+size:]
        if len(self.doc.get('buffers', [])) != 1 or 'uri' in self.doc['buffers'][0]:
            raise ValueError('External geometry buffers are not supported')
        if self.doc.get('skins'):
            raise ValueError('Skinned input meshes are not supported')
        self.cache = {}

    def view(self, index):
        view = self.doc['bufferViews'][index]
        start = view.get('byteOffset', 0)
        return self.binary[start:start+view['byteLength']]

    def accessor(self, index):
        if index not in self.cache:
            a = self.doc['accessors'][index]
            if 'sparse' in a or 'bufferView' not in a:
                raise ValueError('Sparse accessors are not supported')
            view = self.doc['bufferViews'][a['bufferView']]
            dtype = np.dtype(DTYPES[a['componentType']])
            width = WIDTHS[a['type']]
            stride = view.get('byteStride', dtype.itemsize*width)
            arr = np.ndarray((a['count'], width), dtype=dtype, buffer=self.binary,
                offset=view.get('byteOffset', 0)+a.get('byteOffset', 0),
                strides=(stride, dtype.itemsize)).astype(np.float64)
            if a.get('normalized'):
                info = np.iinfo(dtype)
                arr = np.maximum(arr/info.max, -1) if info.min < 0 else arr/info.max
            if not np.isfinite(arr).all():
                raise ValueError('Non-finite accessor data')
            self.cache[index] = arr
        return self.cache[index]


def repair_degenerate_hull_normals(reader):
    """Repair only normals on proven zero-area ConcaveHull faces.

    Some raw exports divide a zero cross product by its length. No visible face
    depends on these values. Keep the original positions/indices/colors; reject
    nonfinite normals on any nondegenerate triangle instead of hiding corruption.
    """
    repairs=[]
    primitives=[p for m in reader.doc['meshes'] for p in m['primitives']]
    for primitive in primitives:
        mat=reader.doc.get('materials',[])[primitive['material']] if 'material' in primitive else {}
        guid=mat.get('extensions',{}).get('GOOGLE_tilt_brush_material',{}).get('guid')
        if guid!='7ae1f880-a517-44a0-99f9-1cab654498c6':continue
        attrs=primitive['attributes']
        if 'NORMAL' not in attrs:continue
        index=attrs['NORMAL'];a=reader.doc['accessors'][index]
        if a['type']!='VEC3' or a['componentType']!=5126 or a.get('sparse') or a.get('normalized'):
            raise ValueError('Expected raw ConcaveHull FLOAT VEC3 normals')
        view=reader.doc['bufferViews'][a['bufferView']]
        normals=np.ndarray((a['count'],3),dtype='<f4',buffer=reader.binary,
            offset=view.get('byteOffset',0)+a.get('byteOffset',0),strides=(view.get('byteStride',12),4)).astype(np.float64)
        bad=~np.isfinite(normals).all(axis=1)
        if not bad.any():continue
        if primitive.get('mode',4)!=4:raise ValueError('Invalid hull primitive mode')
        if any(p is not primitive and index in p['attributes'].values() for p in primitives):
            raise ValueError('Cannot repair shared invalid hull normals')
        positions=reader.accessor(attrs['POSITION'])
        if positions.shape!=normals.shape:raise ValueError('Hull position/normal count mismatch')
        triangles=(reader.accessor(primitive['indices']).astype(int).reshape(-1,3)
                   if 'indices' in primitive else np.arange(len(positions)).reshape(-1,3))
        if triangles.size and (triangles.min()<0 or triangles.max()>=len(positions)):raise ValueError('Invalid hull indices')
        affected=triangles[bad[triangles].any(axis=1)]
        cross=np.cross(positions[affected[:,1]]-positions[affected[:,0]],positions[affected[:,2]]-positions[affected[:,0]])
        if np.any(cross!=0):raise ValueError('Nonfinite hull normal belongs to a nondegenerate triangle')
        normals[bad]=0
        reader.cache[index]=normals
        repairs.append(dict(brushGuid=guid,normalAccessor=index,vertices=int(bad.sum()),zeroAreaTriangles=len(affected)))
    return repairs



SLICE_GUID = '8d1510b2-1fc5-4c90-8db7-4c07eecfd849'
SOURCE_SLICE_ATTRIBUTE = '_OB_SLICE_UVW_V1'

def prepare_source_slice(reader):
    """Preserve the original chroma/luma/distance varying through time clipping.

    Slice ignores paint RGBA. Evaluating color at its four corner vertices
    loses the nonlinear chroma/luma gradient, so retain the fragment inputs.
    """
    pending = []
    for mesh in reader.doc['meshes']:
        for primitive in mesh['primitives']:
            material = reader.doc.get('materials', [])[primitive['material']] if 'material' in primitive else {}
            if material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid') != SLICE_GUID:
                continue
            attrs = primitive['attributes']
            if SOURCE_SLICE_ATTRIBUTE in attrs:
                raise ValueError('Reserved Slice varying already present')
            if '_TB_UNITY_TEXCOORD_0' not in attrs:
                raise ValueError('Slice requires original chroma/luma/distance UVW')
            # The legacy exporter flips V even on its raw Unity-named UV channel.
            values = reader.accessor(attrs['_TB_UNITY_TEXCOORD_0']).copy()
            values[:, 1] = 1 - values[:, 1]
            if values.shape != (len(reader.accessor(attrs['POSITION'])), 3) or np.any(values < 0) or np.any(values[:, :2] > 1):
                raise ValueError('Invalid Slice UVW')
            indices = reader.accessor(primitive['indices']).astype(int).reshape(-1,3) if 'indices' in primitive else np.arange(len(values)).reshape(-1,3)
            if np.max(np.ptp(values[indices,2],axis=1),initial=0) > 1e-5:
                raise ValueError('Slice requires constant distance on each cross-section triangle')
            pending.append((attrs, values.copy()))
    for attrs,values in pending:
        index=len(reader.doc['accessors'])
        reader.doc['accessors'].append(dict(type='VEC3', componentType=5126, count=len(values)))
        reader.cache[index]=values
        attrs[SOURCE_SLICE_ATTRIBUTE]=index
    return sum(len(v) for _,v in pending)


def prepare_source_bloom(reader):
    """Evaluate the bounded part of bloomColor BEFORE triangle subdivision.

    The per-material HDR gain is linear and remains in the SDK shader. This
    custom VEC4 is a varying, not a texture coordinate or a replacement color.
    Reader.cache owns new virtual accessors until Writer emits real buffers.
    """
    if hasattr(reader, 'source_bloom_prepared'):
        return reader.source_bloom_prepared
    pending = []
    for mesh in reader.doc['meshes']:
        for primitive in mesh['primitives']:
            material = reader.doc.get('materials', [])[primitive['material']] if 'material' in primitive else {}
            guid = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
            if guid not in SOURCE_BLOOM_BRUSHES:
                continue
            attrs = primitive['attributes']
            if SOURCE_BLOOM_ATTRIBUTE in attrs:
                raise ValueError('Source already contains a reserved bloom varying')
            if 'COLOR_0' not in attrs:
                raise ValueError('Source bloom needs vertex color')
            colors = reader.accessor(attrs['COLOR_0'])
            if colors.shape[1] not in (3, 4) or np.any(colors < 0) or np.any(colors > 1):
                raise ValueError('Source bloom needs normalized RGB or RGBA')
            rgba = np.ones((len(colors), 4))
            rgba[:, :colors.shape[1]] = colors
            rgba[:, :3] = np.maximum(rgba[:, :3], np.linalg.norm(rgba[:, :3], axis=1)[:, None]*.05)
            pending.append((attrs, np.power(rgba, 2.2)))
    for attrs, values in pending:
        index = len(reader.doc['accessors'])
        reader.doc['accessors'].append({'type': 'VEC4', 'componentType': 5126, 'count': len(values)})
        reader.cache[index] = values
        attrs[SOURCE_BLOOM_ATTRIBUTE] = index
    reader.source_bloom_prepared = sum(len(values) for _, values in pending)
    return reader.source_bloom_prepared


def restore_source_material_defaults(reader):
    """Restore audited shader defaults omitted by the legacy GLB exporter.

    These brushes' normal m_Material uses StandardSingleSided's _Bump input even
    when no texture is assigned. The built shader defaults to white, which
    UnpackNormal decodes to (1,1,0), NOT the usual flat (0,0,1) normal. Encode
    that direction as a portable tangent-space normal map. Supplying it also
    keeps the real source tangents when dividing the drawing into time bands.
    Existing explicit normal maps and every other brush remain untouched.
    """
    if hasattr(reader, 'material_defaults'):
        return reader.material_defaults
    result = []
    normal_index = None
    for index, material in enumerate(reader.doc.get('materials', [])):
        guid = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
        if guid not in WHITE_NORMAL_DEFAULT_BRUSHES or 'normalTexture' in material:
            continue
        if normal_index is None:
            images = reader.doc.setdefault('images', [])
            textures = reader.doc.setdefault('textures', [])
            # 2x2 RGBA PNG: (218,218,128,255), the normalized (1,1,0)
            # direction rounded to 8-bit normal texture coordinates.
            images.append({'name': 'StandardSingleSided_source_default_normal', 'uri':
                'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAFUlEQVR4nGO8davhPwMDAwMTiABhADHCAzeHH0wnAAAAAElFTkSuQmCC'})
            normal_index = len(textures)
            textures.append({'source': len(images)-1})
        material['normalTexture'] = {'index': normal_index, 'texCoord': 0}
        result.append({'material': index, 'brushGuid': guid,
                       'operation': 'source-default-white-normal'})
    reader.material_defaults = result
    return result


def bake_static_brush_geometry(reader):
    """Apply source vertex-shader geometry before dropping custom attributes.

    Offsets have already been transformed/scaled by GeometryPool's exporter.
    Keep source normals: the original vertex shader changes positions only.
    The input binary is never modified, and calling twice on a Reader is safe.
    """
    if hasattr(reader, 'static_geometry_bakes'):
        return reader.static_geometry_bakes
    tapered = {'0d3889f3-3ede-470c-8af4-de4813306126',
               '0d3889f3-3ede-470c-8af4-f44813306126'}
    uses, plans = {}, {}
    for mesh in reader.doc.get('meshes', []):
        for primitive in mesh['primitives']:
            material = reader.doc.get('materials', [])[primitive['material']] if 'material' in primitive else {}
            guid = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
            attrs = primitive['attributes']
            position = attrs.get('POSITION')
            uses.setdefault(position, set()).add(guid)
            if guid not in tapered:
                continue
            if '_TB_UNITY_TEXCOORD_1' not in attrs or 'TEXCOORD_0' not in attrs:
                raise ValueError('DoubleTapered needs original vertex offsets: ' + mesh.get('name', ''))
            signature = (attrs['TEXCOORD_0'], attrs['_TB_UNITY_TEXCOORD_1'])
            if position in plans and plans[position][0] != signature:
                raise ValueError('Shared positions have conflicting taper attributes')
            plans[position] = (signature, mesh.get('name', ''), guid)
    baked, changes = {}, []
    for position, (signature, name, guid) in plans.items():
        if not uses[position] <= tapered:
            raise ValueError('Tapered positions shared with an untapered primitive')
        points = reader.accessor(position)
        uv, offsets = (reader.accessor(i) for i in signature)
        if points.shape[1] != 3 or offsets.shape[0] != len(points) or offsets.shape[1] < 3 or len(uv) != len(points):
            raise ValueError('Invalid taper attribute dimensions')
        # DoubleTaperedMarker.shader / DoubleTaperedFlat.shadergraph.
        envelope = np.sin(uv[:, :1] * math.pi)
        baked[position] = points - offsets[:, :3] * (1 - envelope)
        changes.append(dict(mesh=name, brushGuid=guid, vertices=len(points), operation='double-taper'))
    reader.cache.update(baked)
    changes.extend(bake_rain_geometry(reader))
    changes.extend(bake_sparks_geometry(reader))
    changes.extend(bake_toon_attributes(reader))
    changes.extend(bake_inverted_toon_geometry(reader))
    changes.extend(bake_mylar_geometry(reader))
    reader.static_geometry_bakes = changes
    return changes


def bake_mylar_geometry(reader):
    """Legacy GLB omits Mylar's shader squeeze; apply it BEFORE time clipping.

    _TB_UNITY_TEXCOORD_0.z is already a distance in export units. The shader's
    normal output is unused, so preserve source normals/tangents and paint.
    Reject previously squeezed or mixed/shared geometry instead of guessing.
    """
    guid = '8e58ceea-7830-49b4-aba9-6215104ab52a'
    uses, plans = {}, {}
    for mesh in reader.doc.get('meshes', []):
        for primitive in mesh['primitives']:
            attrs = primitive['attributes']; position = attrs.get('POSITION')
            material = reader.doc.get('materials', [])[primitive['material']] if 'material' in primitive else {}
            brush = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
            uses.setdefault(position, set()).add(brush)
            if brush != guid: continue
            if 'NORMAL' not in attrs or '_TB_UNITY_TEXCOORD_0' not in attrs:
                raise ValueError('MylarTube needs original normal and radius attributes')
            signature = (attrs['NORMAL'], attrs['_TB_UNITY_TEXCOORD_0'])
            if position in plans and plans[position][0] != signature:
                raise ValueError('Conflicting shared MylarTube shape attributes')
            plans[position] = (signature, mesh.get('name', ''))
    pending, reports = {}, []
    for position, (signature, name) in plans.items():
        if uses[position] != {guid}: raise ValueError('MylarTube positions shared with another brush')
        points = reader.accessor(position); normals, uv = (reader.accessor(i) for i in signature)
        if points.shape != normals.shape or points.shape != uv.shape or points.shape[1] != 3 or np.any(uv[:, 2] < 0):
            raise ValueError('Invalid MylarTube geometry or radius dimensions')
        ratios = []
        for u in np.unique(uv[:, 0]):
            ids = np.flatnonzero(uv[:, 0] == u)
            if len(ids) < 4 or uv[ids[0], 2] <= 0: continue
            # Grouped coincident UVs can include multiple strokes/radii. Fit
            # radius-scaled normals so each original radius remains accounted for.
            radial = normals[ids] * uv[ids, 2:3]
            a = radial-radial.mean(axis=0); b = points[ids]-points[ids].mean(axis=0)
            den = np.sum(a*a)
            if den < 1e-12: continue
            ratio = float(np.sum(a*b)/den); ratios.append(ratio)
            if abs(ratio-1) > .02: raise ValueError('MylarTube already deformed or unsupported raw tube: radius ratio '+str(ratio))
        if not ratios: raise ValueError('Cannot verify unbaked MylarTube cross sections')
        pending[position] = points-normals*uv[:, 2:3]*np.sin(uv[:, :1]*3.14159)*.873
        reports.append(dict(mesh=name, brushGuid=guid, vertices=len(points), operation='mylar-squeeze',
                            squeezeAmount=.873, sourceRadiusRatios=ratios))
    reader.cache.update(pending)
    return reports


def bake_rain_geometry(reader):
    """Rain's normal shader inflates each tube face by 2.25 * UV0.z.

    GeometryPool already scales the distance channel into export units. Apply
    the offset before time clipping, retaining the source (also backface) normal.
    Preflight shared positions so unrelated primitives cannot be displaced.
    """
    guid = '03a529e1-f519-3dd4-582d-2d5cd92c3f4f'
    plans, uses = {}, {}
    for mesh in reader.doc.get('meshes', []):
        for primitive in mesh['primitives']:
            material = reader.doc.get('materials', [])[primitive['material']] if 'material' in primitive else {}
            owner = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
            attrs = primitive['attributes']; position = attrs.get('POSITION')
            uses.setdefault(position, set()).add(owner)
            if owner != guid:
                continue
            if 'NORMAL' not in attrs or '_TB_UNITY_TEXCOORD_0' not in attrs:
                raise ValueError('Rain needs source normal and UV.z radius')
            signature = (attrs['NORMAL'], attrs['_TB_UNITY_TEXCOORD_0'])
            if position in plans and plans[position][0] != signature:
                raise ValueError('Shared Rain positions have conflicting radius/normal attributes')
            plans[position] = (signature, mesh.get('name', ''))
    baked, changes = {}, []
    for position, (signature, name) in plans.items():
        if uses[position] != {guid}:
            raise ValueError('Rain positions shared with another brush')
        points = reader.accessor(position)
        normal, uv = (reader.accessor(i) for i in signature)
        if points.shape[1] != 3 or normal.shape != points.shape or len(uv) != len(points) or uv.shape[1] < 3 or (uv[:, 2] < 0).any():
            raise ValueError('Invalid Rain normal/radius dimensions or values')
        baked[position] = points + normal * (2.25 * uv[:, 2:3])
        changes.append(dict(mesh=name, brushGuid=guid, vertices=len(points), operation='rain-bulge'))
    reader.cache.update(baked)
    return changes


def bake_sparks_geometry(reader):
    """Sparks normal shader: normal * UV.x**2.84 * 1.45 app units.

    Legacy Open Brush export uses .1 meters per app unit (App.UNITS_TO_METERS).
    The mesh is canvas-local; the exported node retains its canvas transform.
    Displace before growth clipping so nonlinear endpoints are interpolated just
    as in the original vertex shader, not re-evaluated at newly clipped points.
    """
    guid = '50e99447-3861-05f4-697d-a1b96e771b98'
    plans, uses = {}, {}
    for mesh in reader.doc.get('meshes', []):
        for primitive in mesh['primitives']:
            material = reader.doc.get('materials', [])[primitive['material']] if 'material' in primitive else {}
            owner = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
            attrs = primitive['attributes']; position = attrs.get('POSITION')
            uses.setdefault(position, set()).add(owner)
            if owner != guid:
                continue
            if 'NORMAL' not in attrs or '_TB_UNITY_TEXCOORD_0' not in attrs:
                raise ValueError('Sparks needs original normal and UV coordinates')
            signature = (attrs['NORMAL'], attrs['_TB_UNITY_TEXCOORD_0'])
            if position in plans and plans[position][0] != signature:
                raise ValueError('Shared Sparks positions have conflicting attributes')
            plans[position] = (signature, mesh.get('name', ''))
    baked, changes = {}, []
    for position, (signature, name) in plans.items():
        if uses[position] != {guid}:
            raise ValueError('Sparks positions shared with another brush')
        points = reader.accessor(position)
        normal, uv = (reader.accessor(i) for i in signature)
        if points.shape[1] != 3 or normal.shape != points.shape or len(uv) != len(points) or uv.shape[1] < 2 or (uv[:, 0] < 0).any():
            raise ValueError('Invalid Sparks normal/UV dimensions or values')
        baked[position] = points + normal * (np.power(uv[:, :1], 2.84) * .145)
        changes.append(dict(mesh=name, brushGuid=guid, vertices=len(points), operation='sparks-flare'))
    reader.cache.update(baked)
    return changes


def bake_toon_attributes(reader):
    """Bake Toon vertex shading before growth clipping interpolates its colors.

    Normal world-Y affects color, not world lighting. The export's X reflection
    preserves Y. UV0 is free (the shader does not sample its serialized texture),
    so it carries a pressure mask for the native outline approximation.
    """
    guid = '4391385a-df73-4396-9e33-31e4e4930b27'
    nodes = reader.doc.get('nodes', [])
    parents = {c: i for i, n in enumerate(nodes) for c in n.get('children', [])}
    matrices = {}
    def world(index):
        if index in matrices:
            return matrices[index]
        n = nodes[index]
        if 'matrix' in n:
            local = np.array(n['matrix']).reshape(4, 4).T
        else:
            x, y, z, w = n.get('rotation', [0, 0, 0, 1])
            local = np.eye(4)
            local[:3, :3] = np.array([[1-2*y*y-2*z*z, 2*x*y-2*z*w, 2*x*z+2*y*w],
                [2*x*y+2*z*w, 1-2*x*x-2*z*z, 2*y*z-2*x*w],
                [2*x*z-2*y*w, 2*y*z+2*x*w, 1-2*x*x-2*y*y]]) @ np.diag(n.get('scale', [1, 1, 1]))
            local[:3, 3] = n.get('translation', [0, 0, 0])
        matrices[index] = world(parents[index]) @ local if index in parents else local
        return matrices[index]
    changes, pending, uses = [], {}, {}
    for mesh in reader.doc.get('meshes', []):
        for primitive in mesh['primitives']:
            material = reader.doc.get('materials', [])[primitive['material']] if 'material' in primitive else {}
            owner = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
            for accessor in primitive['attributes'].values():
                uses.setdefault(accessor, set()).add(owner)
    for index, node in enumerate(nodes):
        if 'mesh' not in node:
            continue
        for primitive in reader.doc['meshes'][node['mesh']]['primitives']:
            material = reader.doc.get('materials', [])[primitive['material']] if 'material' in primitive else {}
            if material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid') != guid:
                continue
            attrs = primitive['attributes']
            if '_TB_UNITY_TEXCOORD_0' not in attrs:
                raise ValueError('Toon requires original UV.z radius for its outline')
            normal = reader.accessor(attrs['NORMAL']) @ np.linalg.inv(world(index)[:3, :3])
            normal /= np.maximum(np.linalg.norm(normal, axis=1), 1e-12)[:, None]
            color = reader.accessor(attrs['COLOR_0']).copy()
            color[:, :3] = np.clip(color[:, :3] + normal[:, 1:2]*.2, 0, 1)
            color[:, 3] = 1
            radius = reader.accessor(attrs['_TB_UNITY_TEXCOORD_0'])[:, 2]
            uv = np.column_stack([np.clip(radius*.4/.0035, 0, 1), np.full(len(radius), .5)])
            for accessor, value in ((attrs['COLOR_0'], color), (attrs['TEXCOORD_0'], uv)):
                if uses[accessor] != {guid}:
                    raise ValueError('Toon attributes shared with another brush require separate accessors')
                if accessor in pending and not np.allclose(pending[accessor], value, atol=1e-8):
                    raise ValueError('Shared Toon attributes have conflicting transformed shading')
                pending[accessor] = value
            changes.append(dict(mesh=node.get('name', ''), brushGuid=guid, vertices=len(color), operation='toon-world-y-shading-and-outline-mask'))
    reader.cache.update(pending)
    return changes


def bake_inverted_toon_geometry(reader):
    """Bake the normal material's black front pass and colored inflated back pass.

    Both surfaces remain in one primitive, sharing timestamps and growth frames.
    GLB brush positions are metres; the shader's 0.05 source-unit shell is 5 mm,
    divided by the scene pose scale that drives xf_I_CS at export time.
    """
    guid = '9871385a-df73-4396-9e33-31e4e4930b27'
    targets = [(m, p) for m in reader.doc.get('meshes', []) for p in m['primitives']
               if 'material' in p and reader.doc['materials'][p['material']].get('extensions', {}).get(
                   'GOOGLE_tilt_brush_material', {}).get('guid') == guid]
    if not targets:
        return []
    scene = reader.doc.get('scenes', [])[reader.doc.get('scene', 0)]
    pose_scale = float(scene.get('extras', {}).get('TB_PoseScale', 1))
    if not math.isfinite(pose_scale) or pose_scale <= 0:
        raise ValueError('TubeToonInverted needs a positive source scene pose scale')
    def add(values, kind, indices=False):
        index = len(reader.doc['accessors'])
        reader.doc['accessors'].append(dict(componentType=5125 if indices else 5126,
                                            count=len(values), type=kind))
        reader.cache[index] = values.copy()
        return index
    changes = []
    for mesh, primitive in targets:
        attrs = primitive['attributes']
        position, normal, color = (reader.accessor(attrs[k]) for k in ('POSITION', 'NORMAL', 'COLOR_0'))
        n = len(position)
        shell = position + normal * (.005 / pose_scale)
        black = np.zeros((n, 4)); black[:, 3] = 1
        painted = color.copy(); painted[:, :3] = np.clip(painted[:, :3]+normal[:, 1:2]*.2, 0, 1); painted[:, 3] = 1
        triangles = (reader.accessor(primitive['indices']).ravel().astype(int)
                     if 'indices' in primitive else np.arange(n)).reshape(-1, 3)
        combined = np.concatenate([triangles, triangles[:, ::-1]+n]).reshape(-1, 1)
        for key, accessor in list(attrs.items()):
            data = reader.accessor(accessor)
            values = (np.concatenate([position, shell]) if key == 'POSITION' else
                      np.concatenate([black, painted]) if key == 'COLOR_0' else
                      np.concatenate([normal, -normal]) if key == 'NORMAL' else
                      np.concatenate([data, data]))
            attrs[key] = add(values, reader.doc['accessors'][accessor]['type'])
        primitive['indices'] = add(combined, 'SCALAR', True)
        changes.append(dict(mesh=mesh.get('name', ''), brushGuid=guid, vertices=n*2,
                            shellMetres=.005/pose_scale, operation='inverted-toon-two-surfaces'))
    return changes


def strip_extensions(value):
    if isinstance(value, dict):
        for name in DROP_EXTENSIONS:
            value.get('extensions', {}).pop(name, None)
        if value.get('extensions') == {}:
            value.pop('extensions')
        for child in value.values():
            strip_extensions(child)
    elif isinstance(value, list):
        for child in value:
            strip_extensions(child)


class Writer:
    def __init__(self, source):
        self.binary = bytearray()
        self.doc = {'asset': {'version': '2.0', 'generator': 'Open Brush stroke growth converter v1'},
                    'scene': source.doc.get('scene', 0), 'scenes': copy.deepcopy(source.doc['scenes']),
                    'nodes': [], 'meshes': [], 'accessors': [], 'bufferViews': []}
        for node in source.doc['nodes']:
            self.doc['nodes'].append({k: copy.deepcopy(v) for k, v in node.items()
                if k in ('name', 'children', 'translation', 'rotation', 'scale', 'matrix')})
        for key in ('materials', 'textures', 'samplers'):
            if key in source.doc:
                self.doc[key] = copy.deepcopy(source.doc[key])
        # Keep brush identity even though the custom rendering extension is removed.
        # A Resonite-side material converter can use this instead of guessing from color.
        for index, material in enumerate(self.doc.get('materials', [])):
            brush = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {})
            metadata = {'sourceMaterialIndex': index, 'sourceMaterialName': material.get('name', '')}
            if brush.get('guid'):
                metadata['brushGuid'] = brush['guid']
            if brush.get('guid') == '50e99447-3861-05f4-697d-a1b96e771b98' and any(
                    b['operation'] == 'sparks-flare' for b in getattr(source, 'static_geometry_bakes', [])):
                metadata['vertexBake'] = 'sparks-flare-v1'
            if brush.get('guid') == '03a529e1-f519-3dd4-582d-2d5cd92c3f4f' and any(
                    b['operation'] == 'rain-bulge' for b in getattr(source, 'static_geometry_bakes', [])):
                metadata['vertexBake'] = 'rain-bulge-v1'
            if brush.get('guid') == '8e58ceea-7830-49b4-aba9-6215104ab52a' and any(
                    b['operation'] == 'mylar-squeeze' for b in getattr(source, 'static_geometry_bakes', [])):
                metadata['vertexBake'] = 'mylar-squeeze-v1'
            if brush.get('guid') == '4391385a-df73-4396-9e33-31e4e4930b27' and any(
                    b.get('operation') == 'toon-world-y-shading-and-outline-mask'
                    for b in getattr(source, 'static_geometry_bakes', [])):
                metadata['vertexBake'] = 'toon-world-y-v1'
            extras = material.get('extras')
            if brush.get('guid') == '9871385a-df73-4396-9e33-31e4e4930b27' and any(
                    b.get('operation') == 'inverted-toon-two-surfaces'
                    for b in getattr(source, 'static_geometry_bakes', [])):
                metadata['vertexBake'] = 'inverted-toon-two-surfaces-v1'
            if not isinstance(extras, dict):
                extras = {'sourceExtras': extras} if extras is not None else {}
            extras['openBrush'] = metadata
            material['extras'] = extras
        strip_extensions(self.doc)
        for material in self.doc.get('materials', []):
            if material.get('alphaMode', 'OPAQUE') != 'MASK':
                material.pop('alphaCutoff', None)
        if source.doc.get('images'):
            self.doc['images'] = []
        for original in source.doc.get('images', []):
            image = copy.deepcopy(original)
            if 'uri' in image:
                uri = image.pop('uri')
                if uri.startswith('data:'):
                    header, encoded = uri.split(',', 1)
                    if ';base64' not in header:
                        raise ValueError('Only base64 data images are supported')
                    raw = base64.b64decode(encoded)
                    mime = header[5:].split(';')[0]
                else:
                    if urlparse(uri).scheme:
                        raise ValueError('Download remote textures explicitly before conversion: '+uri)
                    image_path = (source.path.parent / unquote(uri)).resolve()
                    if not image_path.is_relative_to(source.path.parent.resolve()):
                        raise ValueError('Image path leaves the input directory: '+uri)
                    raw = image_path.read_bytes()
                    mime = 'image/png' if raw.startswith(b'\x89PNG') else 'image/jpeg'
                image['mimeType'] = mime
            else:
                raw = source.view(image['bufferView'])
            image['bufferView'] = self.view(raw)
            self.doc['images'].append(image)

    def view(self, data, target=None):
        self.binary.extend(b'\0'*(-len(self.binary) % 4))
        view = {'buffer': 0, 'byteOffset': len(self.binary), 'byteLength': len(data)}
        if target:
            view['target'] = target
        self.doc['bufferViews'].append(view)
        self.binary.extend(data)
        return len(self.doc['bufferViews'])-1

    def accessor(self, values, kind=None, indices=False, vertex=False):
        values = np.asarray(values)
        if values.ndim == 1:
            values = values[:, None]
        kind = kind or {1: 'SCALAR', 2: 'VEC2', 3: 'VEC3', 4: 'VEC4'}[values.shape[1]]
        dtype = '<u4' if indices else '<f4'
        values = values.astype(dtype)
        a = {'bufferView': self.view(values.tobytes(), 34963 if indices else (34962 if vertex else None)),
             'componentType': 5125 if indices else 5126, 'count': len(values), 'type': kind,
             'min': values.min(axis=0).tolist(), 'max': values.max(axis=0).tolist()}
        self.doc['accessors'].append(a)
        return len(self.doc['accessors'])-1

    def save(self, path):
        used = set()
        def walk(value):
            if isinstance(value, dict):
                used.update(value.get('extensions', {}))
                for v in value.values():
                    walk(v)
            elif isinstance(value, list):
                for v in value:
                    walk(v)
        walk(self.doc)
        if used:
            self.doc['extensionsUsed'] = sorted(used)
        self.doc['buffers'] = [{'byteLength': len(self.binary)}]
        text = json.dumps(self.doc, ensure_ascii=True, separators=(',', ':'), allow_nan=False).encode()
        text += b' '*(-len(text) % 4)
        padding = -len(self.binary) % 4
        binary_size = len(self.binary)+padding
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        # Write chunks directly instead of allocating several full GLB copies.
        with Path(path).open('wb') as stream:
            stream.write(struct.pack('<III', 0x46546C67, 2, 28+len(text)+binary_size))
            stream.write(struct.pack('<II', len(text), 0x4E4F534A))
            stream.write(text)
            stream.write(struct.pack('<II', binary_size, 0x004E4942))
            stream.write(self.binary)
            stream.write(b'\0'*padding)


def clip_polygon(poly, limit, above):
    result = []
    for a, b in zip(poly, poly[1:]+poly[:1]):
        ina = a[-1] >= limit if above else a[-1] <= limit
        inb = b[-1] >= limit if above else b[-1] <= limit
        if ina:
            result.append(a)
        if ina != inb:
            # Evaluate an edge in the same direction on both front/back faces.
            # Otherwise tiny rounding differences prevent exact coincident-face
            # detection after the animated export is imported by the SDK.
            start, end = (a, b) if a[-1] < b[-1] else (b, a)
            result.append(start+(end-start)*((limit-start[-1])/(end[-1]-start[-1])))
    return result


def area(triangles):
    return np.linalg.norm(np.cross(triangles[:, 1, :3]-triangles[:, 0, :3],
                                  triangles[:, 2, :3]-triangles[:, 0, :3]), axis=1)*.5


def align_coincident_vertex_times(vertices, selected, offsets):
    """Align exporter-estimated times for copies of one physical stroke vertex.

    Call per stroke. Ignore normals/tangents: backfaces have different normals.
    Keep UV/RGBA in the identity so unrelated overlapping marks stay separate.
    """
    columns = np.concatenate([np.arange(s.start, s.stop) for key, s in offsets.items()
                              if key == 'POSITION' or key == 'COLOR_0' or key.startswith('TEXCOORD_')])
    groups = {}
    for index in np.unique(selected):
        groups.setdefault(tuple(vertices[index, columns]), []).append(index)
    changed = 0
    for indices in groups.values():
        times = vertices[indices, -1]
        if np.ptp(times) > 0:
            vertices[indices, -1] = times.mean()
            changed += len(indices)
    return changed


def canonical_additive_triangles(vertices, triangles, offsets):
    """Give coincident double-sided unlit faces the same clipping traversal.

    Reversed windings otherwise choose different fans when clipped to a time
    band, preventing exact fragment-copy merging after subdivision. Only use
    for audited unlit, Cull Off materials; retain every input face and channel.
    """
    columns = np.concatenate([np.arange(s.start, s.stop) for key, s in offsets.items()
                              if key in ('POSITION', 'COLOR_0') or key.startswith('TEXCOORD_')])
    return np.array([sorted(triangle, key=lambda i: tuple(vertices[i, columns]))
                     for triangle in triangles], dtype=int)


def split_triangles(vertices, triangles, lo, hi, count):
    bands = [[] for _ in range(count)]
    width = (hi-lo)/count
    for indices in triangles:
        tri = vertices[indices]
        first = max(0, min(count-1, int(math.floor((tri[:, -1].min()-lo)/width))))
        last = max(0, min(count-1, int(math.floor((tri[:, -1].max()-lo)/width))))
        for index in range(first, last+1):
            polygon = clip_polygon(list(tri), lo+index*width, True)
            polygon = clip_polygon(polygon, lo+(index+1)*width, False)
            if polygon:
                # Keep winding, but choose the same fan diagonal for opposite
                # sides of the same clipped polygon. Normals must not pick it.
                anchor = min(range(len(polygon)), key=lambda n: (polygon[n][-1], *polygon[n][:3]))
                polygon = polygon[anchor:]+polygon[:anchor]
            for j in range(1, len(polygon)-1):
                piece = np.array([polygon[0], polygon[j], polygon[j+1]])
                if area(piece[None])[0] > 1e-14:
                    bands[index].append(piece)
    return bands


def frame_for_band(vertices):
    p, t = vertices[:, :3], vertices[:, -1]
    direction = ((p-p.mean(axis=0))*(t-t.mean())[:, None]).sum(axis=0)
    if np.linalg.norm(direction) < 1e-10:
        direction = np.eye(3)[np.ptp(p, axis=0).argmax()]
    z = direction/np.linalg.norm(direction)
    helper = np.eye(3)[np.abs(z).argmin()]
    x = np.cross(helper, z)
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    basis = np.column_stack([x, y, z])
    pivot = p[np.isclose(t, t.min(), rtol=0, atol=1e-7)].mean(axis=0)
    # Robust quaternion conversion, including rotations near 180 degrees.
    m = basis
    if np.trace(m) > 0:
        s = math.sqrt(1+np.trace(m))*2
        q = [(m[2,1]-m[1,2])/s, (m[0,2]-m[2,0])/s, (m[1,0]-m[0,1])/s, s/4]
    else:
        i = int(np.diag(m).argmax()); j = (i+1)%3; k = (i+2)%3
        s = math.sqrt(1+m[i,i]-m[j,j]-m[k,k])*2
        q = [0., 0., 0., 0.]
        q[i] = s/4; q[j] = (m[j,i]+m[i,j])/s; q[k] = (m[k,i]+m[i,k])/s
        q[3] = (m[k,j]-m[j,k])/s
    q = np.array(q)/np.linalg.norm(q)
    return pivot, basis, q


class Timeline:
    def __init__(self, intervals, speed, lead, max_gap):
        self.origin = min(a for a, b in intervals)
        self.speed, self.lead = speed, lead
        self.cuts = []
        end = self.origin
        for a, b in sorted(intervals):
            if max_gap is not None and a-end > max_gap:
                self.cuts.append((a, a-end-max_gap))
            end = max(end, b)
        self.source_end = end

    def __call__(self, value):
        removed = sum(amount for start, amount in self.cuts if start <= value+1e-6)
        return self.lead+(value-self.origin-removed)/self.speed


def convert(input_path, output_path, *, speed=1., lead=1., hold=3., max_gap=None,
            slices_per_second=30., max_slices=96, completed=False, progress=None):
    if not all(math.isfinite(v) for v in (speed, lead, hold, slices_per_second, max_slices)):
        raise ValueError('Playback settings must be finite')
    if speed <= 0 or lead < 0 or hold <= 0 or slices_per_second <= 0 or max_slices < 1:
        raise ValueError('Invalid playback settings')
    if max_gap is not None and (not math.isfinite(max_gap) or max_gap < 0):
        raise ValueError('max_gap must be non-negative')
    if Path(input_path).resolve() == Path(output_path).resolve():
        raise ValueError('Input and output must be different files')
    if progress:
        progress('GLBを解析中', 0, 0)
    source = Reader(input_path)
    normal_repairs = repair_degenerate_hull_normals(source)
    material_defaults = restore_source_material_defaults(source)
    geometry_bakes = bake_static_brush_geometry(source)
    bloom_vertices = prepare_source_bloom(source)
    slice_vertices = prepare_source_slice(source)
    if progress:
        progress('ストロークを抽出中', 0, 0)
    work, intervals = [], []
    coincident_timing_vertices = 0
    for nidx, node in enumerate(source.doc['nodes']):
        if 'mesh' not in node:
            continue
        mesh = source.doc['meshes'][node['mesh']]
        for pidx, primitive in enumerate(mesh['primitives']):
            if primitive.get('mode', 4) != 4 or primitive.get('targets') or primitive.get('extensions'):
                raise ValueError('Only uncompressed, non-morph triangle primitives are supported')
            attrs = primitive['attributes']
            if '_TB_TIMESTAMP' not in attrs:
                raise ValueError('Missing _TB_TIMESTAMP on '+mesh.get('name', str(nidx))+
                                 '; export with ExportStrokeTimestamp enabled')
            timestamps = source.accessor(attrs['_TB_TIMESTAMP'])
            if timestamps.shape[1] != 3:
                raise ValueError('_TB_TIMESTAMP must be VEC3')
            unique, groups = np.unique(np.round(timestamps[:, :2], 4), axis=0, return_inverse=True)
            indices = (source.accessor(primitive['indices']).ravel().astype(int)
                       if 'indices' in primitive else np.arange(len(timestamps))).reshape(-1, 3)
            if np.any(indices < 0) or np.any(indices >= len(timestamps)):
                raise ValueError('Triangle index is outside the vertex buffer')
            if np.any(groups[indices] != groups[indices[:, :1]]):
                raise ValueError('A triangle crosses stroke timestamp groups')
            keys = ['POSITION']+[k for k in attrs if k in ('NORMAL', 'TANGENT', 'COLOR_0', SOURCE_BLOOM_ATTRIBUTE, SOURCE_SLICE_ATTRIBUTE) or k.startswith('TEXCOORD_')]
            material = source.doc.get('materials', [])[primitive['material']] if 'material' in primitive else {}
            if 'normalTexture' not in material and 'TANGENT' in keys:
                keys.remove('TANGENT')
            arrays, offsets, offset = [], {}, 0
            for key in keys:
                arr = source.accessor(attrs[key])
                if key.startswith('TEXCOORD_'):
                    arr = arr[:, :2]
                if len(arr) != len(timestamps):
                    raise ValueError('Mismatched vertex attribute lengths')
                arrays.append(arr); offsets[key] = slice(offset, offset+arr.shape[1]); offset += arr.shape[1]
            for group, endpoints in enumerate(unique):
                selected = indices[groups[indices[:, 0]] == group]
                if not len(selected):
                    continue
                # Use actual float endpoint data, not rounded grouping keys.
                lo, hi = timestamps[selected[0, 0], :2]
                if hi < lo:
                    raise ValueError('Stroke has reversed timestamps')
                if hi == lo:
                    hi = lo+1/60
                # A primitive batches many strokes. Retaining every batch vertex
                # once PER stroke makes memory grow with batch size * stroke count.
                # Compact the selected indices before copying any vertex channels.
                used, remapped = np.unique(selected, return_inverse=True)
                selected = remapped.reshape(selected.shape)
                vertex_times = np.clip(timestamps[used, 2], lo, hi)
                vertices = np.column_stack([arr[used] for arr in arrays]+[vertex_times])
                guid = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
                if guid in ('accb32f5-4509-454f-93f8-1df3fd31df1b', 'd229d335-c334-495a-a801-660ac8a87360',
                            'cf019139-d41c-4eb0-a1d0-5cf54b0a42f3', 'cb92b597-94ca-4255-b017-0e3f42f12f9e',
                            'a555b809-2017-46cb-ac26-e63173d8f45e','492b36ff-b337-436a-ba5f-1e87ee86747e','61820308-bae0-48d4-8288-a7d4e6734d2f','2c1a6a63-6552-4d23-86d7-58f6fba8581b'):
                    coincident_timing_vertices += align_coincident_vertex_times(vertices, selected, offsets)
                if guid in ('a555b809-2017-46cb-ac26-e63173d8f45e','492b36ff-b337-436a-ba5f-1e87ee86747e','61820308-bae0-48d4-8288-a7d4e6734d2f','2c1a6a63-6552-4d23-86d7-58f6fba8581b'):
                    selected = canonical_additive_triangles(vertices, selected, offsets)
                intervals.append((lo, hi))
                work.append((nidx, mesh.get('name', 'Brush'), primitive, vertices,
                             selected, offsets, lo, hi))
    if not work:
        raise ValueError('No drawable strokes found')
    timeline = Timeline(intervals, speed, lead, max_gap)
    duration = timeline(timeline.source_end)+hold
    writer = Writer(source)
    animation = {'name': 'BrushGrowth', 'samplers': [], 'channels': []}
    if not completed:
        writer.doc['animations'] = [animation]
    report = {'input': str(Path(input_path).resolve()), 'output': str(Path(output_path).resolve()),
              'coincidentTimingVertices': coincident_timing_vertices,
              'durationSeconds': duration, 'speed': speed, 'leadInSeconds': lead,
              'maxGapSeconds': max_gap, 'holdSeconds': hold, 'strokes': [],
              'limitations': ['Vertex timing uses the exporter timestamp approximation.',
                              'Shader and particle simulation are not replayed.',
                              'Additive blending uses the exported alpha-blend fallback.']}
    if progress:
        progress('ストロークを変換中', 0, len(work))
    for stroke_id, (parent, name, primitive, vertices, indices, offsets, lo, hi) in enumerate(work):
        count = 1 if completed else min(max_slices, max(1, int(math.ceil((hi-lo)/speed*slices_per_second))))
        # Keep original triangles in completed mode, without clipping or growth keys.
        bands = [vertices[indices]] if completed else split_triangles(vertices, indices, lo, hi, count)
        original_area = float(area(vertices[indices]).sum())
        output_area, emitted, out_triangles = 0., 0, 0
        for index, pieces in enumerate(bands):
            if len(pieces) == 0:
                continue
            pieces = np.asarray(pieces)
            output_area += float(area(pieces).sum())
            out_triangles += len(pieces)
            verts = pieces.reshape(-1, vertices.shape[1])
            pivot, basis, rotation = frame_for_band(verts)
            local = verts.copy()
            local[:, :3] = (verts[:, :3]-pivot)@basis
            for key in ('NORMAL', 'TANGENT'):
                if key in offsets:
                    sl = offsets[key]; vectors = local[:, sl.start:sl.start+3]@basis
                    lengths = np.linalg.norm(vectors, axis=1)
                    vectors[lengths < 1e-12] = [0, 0, 1]
                    vectors /= np.maximum(np.linalg.norm(vectors, axis=1), 1e-12)[:, None]
                    local[:, sl.start:sl.start+3] = vectors
                    if key == 'TANGENT':
                        local[:, sl.stop-1] = np.where(local[:, sl.stop-1] < 0, -1, 1)
            # Float32 round-trip check: final transformed vertices retain the source geometry.
            error = np.max(np.abs(local[:, :3].astype('float32')@basis.T+pivot-verts[:, :3]))
            if error > max(1e-5, np.abs(verts[:, :3]).max()*1e-6):
                raise ValueError('Chunk coordinate precision is insufficient')
            attributes = {key: writer.accessor(local[:, sl], vertex=True) for key, sl in offsets.items()}
            mesh_id = len(writer.doc['meshes'])
            part = {'attributes': attributes, 'mode': 4}
            if 'material' in primitive:
                part['material'] = primitive['material']
            writer.doc['meshes'].append({'name': '{}_{}_{}'.format(name, stroke_id, index), 'primitives': [part]})
            start = timeline(lo+(hi-lo)*index/count)
            end = timeline(lo+(hi-lo)*(index+1)/count)
            node_id = len(writer.doc['nodes'])
            # Multiple strokes of the same brush can share one source batch. Names
            # must still be unique for importers that resolve animation targets by name.
            writer.doc['nodes'].append({'name': '{}_stroke_{:04d}_part_{:03d}'.format(name, stroke_id, index),
                'mesh': mesh_id, 'translation': pivot.tolist(), 'rotation': rotation.tolist(),
                'scale': [1, 1, 1], 'extras': {'strokeIndex': stroke_id,
                'materialIndex': primitive.get('material'), 'drawStart': start, 'drawEnd': end}})
            writer.doc['nodes'][parent].setdefault('children', []).append(node_id)
            # Future bands must be invisible even if collapsed geometry forms a plane.
            # Restore transverse dimensions in <=0.1ms, then grow only along the local axis.
            if completed:
                emitted += 1
                continue
            ramp = min(.0001, (end-start)*.01)
            times = ([0.] if start > 0 else [])+[start, start+ramp, end, duration]
            scales = ([[0, 0, 0]] if start > 0 else [])+[[0, 0, 0], [1, 1, ramp/(end-start)], [1, 1, 1], [1, 1, 1]]
            if not np.all(np.diff(np.array(times, dtype='float32')) > 0):
                raise ValueError('Timeline is too long or segments too short for float32 keys')
            sampler = len(animation['samplers'])
            animation['samplers'].append({'input': writer.accessor(times), 'output': writer.accessor(scales), 'interpolation': 'LINEAR'})
            animation['channels'].append({'sampler': sampler, 'target': {'node': node_id, 'path': 'scale'}})
            emitted += 1
        work[stroke_id] = None  # Release processed stroke arrays while the output grows.
        if progress:
            progress('ストロークを変換中', stroke_id + 1, len(work))
        relative_error = abs(output_area-original_area)/max(original_area, 1e-12)
        if relative_error > 1e-6:
            raise ValueError('Triangle splitting lost surface area: '+name)
        report['strokes'].append({'name': name, 'sourceStart': float(lo), 'sourceEnd': float(hi),
            'playStart': timeline(lo), 'playEnd': timeline(hi), 'segments': emitted,
            'sourceTriangles': len(indices), 'outputTriangles': out_triangles,
            'surfaceAreaRelativeError': relative_error})
    if not completed and not animation['channels']:
        raise ValueError('No non-degenerate brush surface to animate')
    writer.doc['extras'] = {'brushGrowth': {'durationSeconds': duration,
        'strokeCount': len(work), 'timing': 'Approximate per-vertex timing from _TB_TIMESTAMP',
        'sourceGenerator': source.doc['asset'].get('generator', '')}}
    if progress:
        progress('GLBを書き出し中', 0, 0)
    writer.save(output_path)
    report['segments'] = sum(s['segments'] for s in report['strokes'])
    report['staticGeometryBakes'] = geometry_bakes
    report['degenerateHullNormalRepairs'] = normal_repairs
    report['materialDefaults'] = material_defaults
    report['sourceBloomVertices'] = bloom_vertices
    report['sourceSliceVertices'] = slice_vertices
    report['outputBytes'] = Path(output_path).stat().st_size
    return report


def material_manifest(output_path):
    """Describe exact exported bindings for a later SDK/resoloop material pass.

    This contains observed GLB state, not guessed Resonite property values.
    The sidecar is optional at playback time; the GLB remains self-contained.
    """
    reader = Reader(output_path)
    materials = []
    for index, material in enumerate(reader.doc.get('materials', [])):
        targets = []
        vertex_colors = False
        for node_index, node in enumerate(reader.doc['nodes']):
            if 'mesh' not in node:
                continue
            for primitive in reader.doc['meshes'][node['mesh']]['primitives']:
                if primitive.get('material') == index:
                    targets.append({'nodeIndex': node_index, 'nodeName': node.get('name', '')})
                    vertex_colors |= 'COLOR_0' in primitive['attributes']
        entry = {'materialIndex': index, 'name': material.get('name', ''),
                 'openBrush': material.get('extras', {}).get('openBrush', {}),
                 'alphaMode': material.get('alphaMode', 'OPAQUE'),
                 'doubleSided': material.get('doubleSided', False),
                 'usesVertexColors': vertex_colors,
                 'pbrMetallicRoughness': material.get('pbrMetallicRoughness', {}),
                 'emissiveFactor': material.get('emissiveFactor', [0, 0, 0]),
                 'extensions': material.get('extensions', {}), 'targets': targets}
        if 'alphaCutoff' in material:
            entry['alphaCutoff'] = material['alphaCutoff']
        materials.append(entry)
    result = {'schemaVersion': 1, 'glb': Path(output_path).name,
            'purpose': 'Material bindings for a separate Resonite appearance conversion',
            'materials': materials}
    # Legacy GLB scene extras retain source Gamma lighting, with light rotations
    # already reflected into the glTF basis. Do not infer a Standard environment.
    if any(m['openBrush'].get('brushGuid') == 'd3f3b18a-da03-f694-b838-28ba8e749a98' for m in materials):
        extras = reader.doc['scenes'][reader.doc.get('scene', 0)].get('extras', {})
        values = {}
        for field, key in [('ambient', 'TB_AmbientLightColor'), ('mainColor', 'TB_SceneLight0Color'), ('mainRotation', 'TB_SceneLight0Rotation')]:
            raw = extras.get(key)
            if not isinstance(raw, str):
                raise ValueError('3D Printing source lighting is missing: ' + key)
            value = [float(v.strip()) for v in raw.split(',')]
            if len(value) != 3 or not all(math.isfinite(v) for v in value) or (field != 'mainRotation' and min(value) < 0):
                raise ValueError('Invalid source lighting: ' + key)
            values[field] = value
        result['flatLighting'] = dict(schemaVersion=1, mode='source-gamma-main-and-ambient', **values)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('input', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--speed', type=float, default=1, help='Playback multiplier; 1 preserves drawing speed')
    parser.add_argument('--lead-in', type=float, default=1)
    parser.add_argument('--hold', type=float, default=3)
    parser.add_argument('--max-gap', type=float, help='Cap idle gaps between strokes, in source seconds')
    parser.add_argument('--slices-per-second', type=float, default=30)
    parser.add_argument('--max-slices', type=int, default=96, help='Maximum mesh parts per stroke')
    args = parser.parse_args()
    report = convert(args.input, args.output, speed=args.speed, lead=args.lead_in,
                     hold=args.hold, max_gap=args.max_gap, slices_per_second=args.slices_per_second,
                     max_slices=args.max_slices)
    report_path = args.output.with_suffix('.report.json')
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
    args.output.with_suffix('.materials.json').write_text(
        json.dumps(material_manifest(args.output), indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k not in ('strokes', 'limitations')}, indent=2))


if __name__ == '__main__':
    main()



