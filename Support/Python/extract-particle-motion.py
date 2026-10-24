#!/usr/bin/env python3
"""Preserve the original four-vertex particle records before GLB simplification."""
import argparse
import importlib.util
import json
from pathlib import Path
import numpy as np

spec = importlib.util.spec_from_file_location('growth', Path(__file__).with_name('animate-brush-glb.py'))
growth = importlib.util.module_from_spec(spec)
spec.loader.exec_module(growth)
DOTS = '6a1cf9f9-032c-45ec-9b1d-a6680bee30f7'
STARS = '0eb4db27-3f82-408d-b5a1-19ebd7d5b711'
SNOW = 'd902ed8b-d0d1-476c-a8de-878a79e3a34c'
BUBBLES = '89d104cd-d012-426b-b5b3-bbaee63ac43c'
RISING = 'a8147ce1-005e-abe4-88e8-09a1eaadcc89'


def vector(values, keys='xyz'):
    return dict(zip(keys, map(float, values)))


def extract(reader):
    parts = []
    for node in reader.doc['nodes']:
        if 'mesh' not in node:
            continue
        mesh = reader.doc['meshes'][node['mesh']]
        for primitive in mesh['primitives']:
            material = reader.doc['materials'][primitive['material']]
            guid = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
            if guid not in (STARS, SNOW, BUBBLES, DOTS, RISING):
                continue
            attrs = primitive['attributes']
            required = ['POSITION', 'COLOR_0', '_TB_UNITY_NORMAL', '_TB_UNITY_TEXCOORD_0', '_TB_TIMESTAMP']
            if any(key not in attrs for key in required):
                raise ValueError('Particle export is missing original attributes')
            position, color, center, uv, times = [reader.accessor(attrs[key]) for key in required]
            if guid == DOTS:
                # Normal Dots ParticleVertex_t has no spread-origin channel.
                origin = center.copy()
            elif '_TB_UNITY_TEXCOORD_1' in attrs:
                origin = reader.accessor(attrs['_TB_UNITY_TEXCOORD_1'])
            else:
                raise ValueError('Particle export is missing original spread origin')
            count = len(position)
            if count % 4 or any(len(a) != count for a in [color, center, uv, origin, times]):
                raise ValueError('Expected four vertices per particle')
            indices = reader.accessor(primitive['indices']).astype(int).ravel()
            if len(indices) != count // 4 * 6 or np.any(indices < 0) or np.any(indices >= count):
                raise ValueError('Invalid particle topology')
            if any(len(set(triangle // 4)) != 1 for triangle in indices.reshape(-1, 3)):
                raise ValueError('Triangle crosses particle boundary')
            if np.any(np.bincount(indices // 4, minlength=count // 4) != 6):
                raise ValueError('Each particle must have exactly two triangles')
            records = []
            for i in range(0, count, 4):
                for data in [center, color, origin]:
                    if not np.allclose(data[i:i+4], data[i], atol=1e-6, rtol=0):
                        raise ValueError('Nonuniform particle attributes')
                radii = np.linalg.norm(position[i:i+4]-center[i], axis=1) / np.sqrt(2)
                if radii.min() <= 0 or not np.allclose(radii, radii[0], atol=1e-5, rtol=1e-4):
                    raise ValueError('Non-square particle quad')
                rect = [*uv[i:i+4, :2].min(0), *uv[i:i+4, :2].max(0)]
                # UniGLTF reflects Z. Keep GLB UV bounds; the SDK bake flips Y
                # and packs native billboard UV center/extent, not min/max.
                records.append(dict(center=vector(center[i]*[1,1,-1]), origin=vector(origin[i]*[1,1,-1]),
                    color=vector(color[i], 'rgba'), size=float(2*radii[0]), rotation=float(uv[i,2]),
                    uv=vector(rect, 'xyzw'), sourceStart=float(times[i,0]), sourceEnd=float(times[i,1]),
                    birth=float(times[i:i+4,2].mean())))
            parts.append(dict(nodeName=node['name'], brushGuid=guid, particles=records))
    if not parts:
        raise ValueError('No supported particle brushes found')
    return dict(schemaVersion=1, coordinateSystem='UnityInvertZ', parts=parts)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('source', type=Path)
    p.add_argument('output', type=Path)
    args = p.parse_args()
    if args.output.exists():
        p.error('Output exists')
    result = extract(growth.Reader(args.source))
    args.output.write_text(json.dumps(result, separators=(',', ':')), encoding='utf-8')
    print(json.dumps(dict(parts=len(result['parts']), particles=sum(len(p['particles']) for p in result['parts']))))
