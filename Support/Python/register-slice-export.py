"""Register Slice transport for the customized Resonite SDK; new output only.

Usage: python register-slice-export.py source/exportManifest.json new-manifest.json
Install the result in Open Brush's Support directory before launching it.
Requires SliceBrush UV0 semantic XyIsUv and GeometryPool's Vector3 XyIsUv no-op.
The inherited UnlitHull GLSL is a transport fallback, not a Slice web renderer.
The SDK restores the original fragment coordinates and color separately.
"""
import argparse
import copy
import json
import re
from pathlib import Path

GUID = '8d1510b2-1fc5-4c90-8db7-4c07eecfd849'
BASE = 'a8fea537-da7c-4d4b-817f-24f074725d6d'

def register(source, destination):
    source, destination = Path(source), Path(destination)
    if destination.exists():
        raise FileExistsError('Use a new output manifest')
    text = source.read_text(encoding='utf-8-sig')
    data = json.loads(text)
    if GUID in data['brushes']:
        raise ValueError('Slice already registered; inspect before changing it')
    original = data['brushes'][BASE]
    if original['guid'] != BASE or original['textures'] or original['enableCull']:
        raise ValueError('Audited UnlitHull transport material changed')
    entry = copy.deepcopy(original)
    entry.update(guid=GUID, name='Slice', blendMode=3, floatParams={}, colorParams={})
    data['brushes'][GUID] = entry
    end = re.search(r'\n  }\n}\s*\Z', text)
    if list(data)[-1] != 'brushes' or end is None:
        raise ValueError('Unexpected manifest layout')
    lines = json.dumps({GUID: entry}, indent=2).splitlines()[1:-1]
    result = text[:end.start()] + ',\n' + '\n'.join('  ' + line for line in lines) + text[end.start():]
    if json.loads(result) != data:
        raise ValueError('Manifest insertion changed existing content')
    destination.write_text(result, encoding='utf-8')
    return destination

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    args = parser.parse_args()
    print(register(args.source, args.destination))
