#!/usr/bin/env python3
"""Prepare one portable drawing-animation bundle for the customized Resonite SDK.

Input is an original Open Brush GLB with exported timestamps/raw shader channels.
No application, network connection or running Unity Editor is needed to prepare.
Requires numpy. The output directory must not already exist.
"""
import argparse
from contextlib import contextmanager
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import re
import shutil
import sys
import time
import uuid

HERE = Path(__file__).resolve().parent
SDK = HERE.parents[1]
DOTS = '6a1cf9f9-032c-45ec-9b1d-a6680bee30f7'
PARTICLES = {DOTS, '0eb4db27-3f82-408d-b5a1-19ebd7d5b711',
             'd902ed8b-d0d1-476c-a8de-878a79e3a34c',
             '89d104cd-d012-426b-b5b3-bbaee63ac43c',
             'a8147ce1-005e-abe4-88e8-09a1eaadcc89'}
KEIJIRO = 'f8ba3d18-01fc-4d7b-b2d9-b99d10b8e7cf'
DISCO = '4391aaaa-df73-4396-9e33-31e4e4930b27'
ELECTRICITY = 'f6e85de3-6dcc-4e7f-87fd-cee8c3d25d51'
DANCEFLOOR = '6a1cf9f9-032c-45ec-311e-a6680bee32e9'
HYPERGRID = '6a1cf9f9-032c-45ec-9b6e-a6680bee32e9'
LIGHTWIRE = '4391aaaa-df81-4396-9e33-31e4e4930b27'


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), HERE / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def sha256(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest() if hasattr(hashlib, 'file_digest') else hashlib.sha256(stream.read()).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False), encoding='utf-8')


@contextmanager
def staging_directory(destination):
    # Windows tempfile directories receive an owner-only ACL. Renaming such a
    # directory publishes that ACL too, preventing an ordinary Unity process
    # from reading a bundle prepared by a service/sandbox user. A normal mkdir
    # inherits the destination parent's access policy, like a normal output.
    stage = destination.parent / (destination.name + '.preparing-' + uuid.uuid4().hex)
    stage.mkdir()  # Exclusive creation; never reuse a pre-existing directory.
    try:
        yield stage
    finally:
        resolved = stage.resolve()
        if resolved.parent != destination.parent or not resolved.name.startswith(destination.name + '.preparing-'):
            raise RuntimeError('Unexpected staging cleanup path')
        if resolved.exists():
            shutil.rmtree(resolved)


def prepare(source, destination, *, sdk_root=SDK, speed=1., lead=1., hold=3.,
            max_gap=None, slices_per_second=30., max_slices=96, brush_assets=None,
            completed=False, effect_loop_seconds=8., progress=None):
    source, destination = Path(source).resolve(), Path(destination).resolve()
    if destination.exists():
        raise FileExistsError('Output directory already exists: ' + str(destination))
    if not all(math.isfinite(x) for x in (speed, lead, hold, slices_per_second, max_slices)) or speed <= 0 or lead < 0 or hold <= 0 or slices_per_second <= 0 or max_slices < 1:
        raise ValueError('Invalid playback settings')
    if max_gap is not None and (not math.isfinite(max_gap) or max_gap < 0):
        raise ValueError('Invalid maximum gap')
    if not math.isfinite(effect_loop_seconds) or not .1 <= effect_loop_seconds <= 60:
        raise ValueError('Effect loop seconds must be between 0.1 and 60')
    profiles_path = Path(sdk_root) / 'Assets/OpenBrushResonite/OpenBrushProfiles.cs'
    profiles = dict(re.findall(r'new Profile\("([0-9a-f-]+)",\s*"([^"]+)"\)', profiles_path.read_text(encoding='utf-8-sig')))
    if not profiles:
        raise ValueError('No audited SDK brush profiles found')
    if progress:
        progress('ブラシを確認中', 0, 0)
    growth = load('animate-brush-glb')
    raw = growth.Reader(source)
    brushes = set()
    for material in raw.doc.get('materials', []):
        guid = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
        if guid not in profiles:
            raise ValueError('Unsupported or missing original brush GUID: ' + str(guid) + ' (' + material.get('name', '') + ')')
        brushes.add(guid)
    if not brushes:
        raise ValueError('The sketch contains no brush materials')
    textures = load('embed-openbrush-textures')
    needs_local_textures = textures.has_remote_images(raw.doc)
    if needs_local_textures and brush_assets is None:
        raise ValueError('Remote texture references found. Add --brush-assets pointing to '
                         "Open Brush's Support/shaders/brushes directory; no textures were downloaded.")
    destination.parent.mkdir(parents=True, exist_ok=True)
    # Publish only a complete directory; cleanup owns only its new staging path.
    with staging_directory(destination) as scratch:
        stage = Path(scratch).resolve()
        if stage.parent != destination.parent or stage == destination:
            raise RuntimeError('Unexpected temporary directory location')
        files = dict(model='drawing.glb', materials='materials.json', timeline='timeline.json')
        texture_source = source
        localized_count = 0
        if needs_local_textures:
            texture_source = stage / 'textures-intermediate.glb'
            localized_count = textures.embed(source, texture_source, brush_assets)
        if progress:
            progress('ブラシ効果を抽出中', 0, 0)
        # Extract raw records BEFORE slicing or any static deformation.
        if KEIJIRO in brushes:
            files['keijiro'] = 'keijiro.json'
            load('extract-keijiro-motion').extract(source, stage / files['keijiro'], HERE / 'animate-brush-glb.py')
        if DISCO in brushes:
            files['disco'] = 'disco.json'
            load('extract-disco-motion').extract(source, stage / files['disco'], HERE / 'animate-brush-glb.py')
        if ELECTRICITY in brushes:
            files['electricity'] = 'electricity.json'
            load('extract-electricity-motion').extract(source, stage / files['electricity'], HERE / 'animate-brush-glb.py')
        if HYPERGRID in brushes:
            files['hypergrid'] = 'hypergrid.json'
            load('extract-hypergrid-motion').extract(source, stage / files['hypergrid'], HERE / 'animate-brush-glb.py')
        if DANCEFLOOR in brushes:
            files['dancefloor'] = 'dancefloor.json'
            load('extract-dancefloor-motion').extract(source, stage / files['dancefloor'], HERE / 'animate-brush-glb.py')
        if brushes & PARTICLES:
            files['particles'] = 'particles.json'
            write_json(stage / files['particles'], load('extract-particle-motion').extract(raw))
        del raw  # Raw sidecars are written; do not retain another complete GLB.
        geometry = texture_source
        if LIGHTWIRE in brushes:
            geometry = stage / 'lightwire-intermediate.glb'
            load('bake-lightwire-shape').bake(texture_source, geometry)
        report = growth.convert(geometry, stage / files['model'], speed=speed, lead=lead,
                                hold=hold, max_gap=max_gap, slices_per_second=slices_per_second,
                                max_slices=max_slices, completed=completed, progress=progress)
        if progress:
            progress('マテリアルと再生データを作成中', 0, 0)
        output = growth.Reader(stage / files['model'])
        segments = [dict(name=node['name'], start=node['extras']['drawStart'], end=node['extras']['drawEnd'])
                    for node in output.doc['nodes'] if 'drawStart' in node.get('extras', {})]
        del output  # Metadata is extracted; release BIN before material manifest read.
        if not segments or len({s['name'] for s in segments}) != len(segments) or len(segments) != report['segments']:
            raise ValueError('Incomplete or ambiguous drawing timeline')
        if completed:
            # Source endpoints still identify the raw motion records. Playback
            # endpoints are only identities; SDK disables all reveal gating.
            for stroke in report['strokes']:
                stroke['playStart'], stroke['playEnd'] = 0., min(1/60, effect_loop_seconds)
            for segment in segments:
                segment['start'], segment['end'] = 0., min(1/60, effect_loop_seconds)
        write_json(stage / files['timeline'], dict(completed=completed,
                   duration=effect_loop_seconds if completed else report['durationSeconds'],
                   segments=segments, strokes=report['strokes']))
        write_json(stage / files['materials'], growth.material_manifest(stage / files['model']))
        # Diagnostic paths must not point to the temporary staging directory.
        report['input'], report['output'] = str(source), files['model']
        report['lightWireShapeBaked'] = LIGHTWIRE in brushes
        report['localizedTextureCount'] = localized_count
        report['mode'] = 'completed' if completed else 'animated'
        report['effectLoopSeconds'] = effect_loop_seconds if completed else 0
        if completed:
            report['durationSeconds'] = 0
        write_json(stage / 'conversion-report.json', report)
        for intermediate in {geometry, texture_source} - {source}:
            intermediate.unlink()  # Only files created in this private directory.
        if progress:
            progress('変換結果を検証中', 0, 0)
        bundle = dict(schemaVersion=1, files=files, source=dict(name=source.name, sha256=sha256(source)),
                      brushes=[dict(guid=g, name=profiles[g]) for g in sorted(brushes)],
                      mode=report['mode'], effectLoopSeconds=report['effectLoopSeconds'],
                      duration=report['durationSeconds'], growthParts=0 if completed else len(segments),
                      settings=dict(speed=speed, leadIn=lead, hold=hold, maxGap=max_gap,
                                    slicesPerSecond=slices_per_second, maxSlices=max_slices),
                      hashes=[dict(path=p.name, sha256=sha256(p)) for p in sorted(stage.iterdir()) if p.is_file()])
        write_json(stage / 'bundle.json', bundle)
        # The final directory is new. All file references are relative and no
        # export-time textures or intermediate source files are needed by SDK.
        if destination.exists():
            raise FileExistsError(destination)
        stage.rename(destination)
    return bundle


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--sdk-root', type=Path, default=SDK)
    parser.add_argument('--brush-assets', type=Path, help='Local Open Brush Support/shaders/brushes directory for historical texture URLs (no network)')
    parser.add_argument('--completed', action='store_true', help='Import the completed drawing without stroke replay or buttons; keep brush effects')
    parser.add_argument('--effect-loop-seconds', type=float, default=8, help='Completed-mode finite effect loop, 0.1-60 seconds (default: 8; may have a visible seam)')
    parser.add_argument('--speed', type=float, default=1)
    parser.add_argument('--lead-in', type=float, default=1)
    parser.add_argument('--hold', type=float, default=3)
    parser.add_argument('--max-gap', type=float)
    parser.add_argument('--slices-per-second', type=float, default=30)
    parser.add_argument('--max-slices', type=int, default=96)
    parser.add_argument('--progress-json', action='store_true', help='Emit machine-readable progress lines for the desktop app')
    args = parser.parse_args()
    last_progress = [0.]
    def progress(detail, current, total):
        now = time.monotonic()
        if total and 0 < current < total and now - last_progress[0] < .25:
            return
        last_progress[0] = now
        print('OPENBRUSH_PROGRESS ' + json.dumps(dict(stage='convert', detail=detail, current=current, total=total), ensure_ascii=False), flush=True)
    try:
        bundle = prepare(args.source, args.destination, sdk_root=args.sdk_root, speed=args.speed,
                         lead=args.lead_in, hold=args.hold, max_gap=args.max_gap,
                         slices_per_second=args.slices_per_second, max_slices=args.max_slices,
                         brush_assets=args.brush_assets, completed=args.completed,
                         effect_loop_seconds=args.effect_loop_seconds, progress=progress if args.progress_json else None)
    except MemoryError:
        parser.exit(1, 'Preparation failed: Not enough memory to convert this sketch. '
                    'Use 64-bit Python, close memory-heavy applications, or split the sketch.\n')
    except (ValueError, OSError, KeyError) as error:
        parser.exit(1, 'Preparation failed: ' + str(error) + '\n')
    print(json.dumps(dict(bundle=str((args.destination/'bundle.json').resolve()),
                          brushes=[b['name'] for b in bundle['brushes']], mode=bundle['mode'],
                          effectLoopSeconds=bundle['effectLoopSeconds'],
                          duration=bundle['duration'], growthParts=bundle['growthParts'],
                          files=bundle['files']), ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
