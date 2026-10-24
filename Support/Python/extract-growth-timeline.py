#!/usr/bin/env python3
"""Extract SDK growth timing from animate-brush-glb output and its report."""
import argparse,json,struct
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('glb');p.add_argument('report');p.add_argument('output');a=p.parse_args()
dst=Path(a.output)
if dst.exists():raise ValueError('Use a new output file')
raw=Path(a.glb).read_bytes();size,kind=struct.unpack_from('<II',raw,12)
if kind!=0x4e4f534a:raise ValueError('Missing GLB JSON chunk')
doc=json.loads(raw[20:20+size]);report=json.loads(Path(a.report).read_text(encoding='utf-8-sig'))
segments=[dict(name=n['name'],start=n['extras']['drawStart'],end=n['extras']['drawEnd']) for n in doc['nodes'] if 'drawStart' in n.get('extras',{})]
if not segments or len({s['name'] for s in segments})!=len(segments):raise ValueError('Missing or ambiguous growth nodes')
dst.write_text(json.dumps(dict(duration=report['durationSeconds'],segments=segments,strokes=report['strokes'])),encoding='utf-8')
print(json.dumps(dict(segments=len(segments),duration=report['durationSeconds'])))
