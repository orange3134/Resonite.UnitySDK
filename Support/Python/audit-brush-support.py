#!/usr/bin/env python3
"""Check documentation coverage by GUID, including unlisted and export-only brushes.

Read-only unless --output is supplied. --check exits nonzero on missing rows,
invalid identities, unresolved manifest references, or inconsistent profile status.
Profile presence is NOT a visual parity test. No GLB geometry or world is modified.
"""
import argparse
import csv
import json
from pathlib import Path
import re
import struct
import uuid


def guid(value):
    return str(uuid.UUID(value.strip().strip('\"\'')))


def field(text, name):
    m = re.search(r'^[ \t]*' + re.escape(name) + r':[ \t]*(.*)$', text, re.M)
    return m[1].strip() if m else None


def glb_materials(path):
    # Read JSON only, never load the potentially huge mesh buffer.
    with path.open('rb') as f:
        head = f.read(20)
        if len(head) != 20:
            raise ValueError('Truncated GLB header')
        magic, version, length, size, kind = struct.unpack('<5I', head)
        if magic != 0x46546C67 or version != 2 or kind != 0x4E4F534A or length != path.stat().st_size or size > length - 20:
            raise ValueError('Invalid GLB 2 JSON header')
        return json.loads(f.read(size)).get('materials', [])


def audit(source, sdk, export_manifests=(), glb_roots=()):
    rows, issues, non_text_assets = {}, [], []

    def add(raw, name, kind, location):
        key = guid(raw)
        row = rows.setdefault(key, dict(guid=key, names=[], descriptors=[], manifests=[], exports=[], glbs=[], profile=None))
        if name and name not in row['names']:
            row['names'].append(name)
        if location not in row[kind]:
            row[kind].append(location)
        return row

    roots = [source / 'Assets', source / 'Packages']
    roots += sorted((source / 'Library/PackageCache').glob('com.icosa.*'))
    asset_ids = {}
    script_meta = source / 'Assets/Scripts/Brushes/BrushDescriptor.cs.meta'
    brush_script = field(script_meta.read_text(encoding='utf-8-sig'), 'guid') if script_meta.exists() else None
    for base in roots:
        for path in sorted(base.rglob('*.asset')):
            try:
                text = path.read_text(encoding='utf-8-sig')
            except UnicodeDecodeError:
                non_text_assets.append(path.relative_to(source).as_posix())
                continue
            # Also recognize the script reference, independently of m_DurableName.
            if field(text, 'm_DurableName') is None and not (brush_script and brush_script.lower() in (field(text, 'm_Script') or '').lower()):
                continue
            try:
                match = re.search(r'm_Guid:[ \t]*\r?\n[ \t]*m_storage:[ \t]*([^\r\n]*)', text)
                if not match:
                    raise ValueError('Brush descriptor has no m_Guid')
                relative = path.relative_to(source).as_posix()
                row = add(match[1], field(text, 'm_DurableName') or field(text, 'm_Name'), 'descriptors', relative)
                asset_id = guid(field(path.with_suffix('.asset.meta').read_text(encoding='utf-8-sig'), 'guid')).replace('-', '')
                asset_ids[asset_id] = row['guid']
            except (ValueError, TypeError, AttributeError, OSError) as exc:
                issues.append(f'{path}: {exc}')
    for path in sorted((source / 'Assets').glob('Manifest*.asset')):
        text = path.read_text(encoding='utf-8-sig')
        for match in re.finditer(r'^  (?:Brushes|CompatibilityBrushes):[^\r\n]*\r?\n((?:  -[^\r\n]*\r?\n)*)', text, re.M):
            for raw in re.findall(r'guid:\s*([0-9a-f]{32})', match[1], re.I):
                key = asset_ids.get(raw.lower())
                if key:
                    if path.stem not in rows[key]['manifests']:
                        rows[key]['manifests'].append(path.stem)
                else:
                    issues.append(f'{path.name}: unresolved brush asset GUID {raw}')
    manifests = [source / 'Support/exportManifest.json', *export_manifests]
    for path in dict.fromkeys(manifests):
        try:
            for key, value in json.loads(path.read_text(encoding='utf-8-sig'))['brushes'].items():
                if 'guid' in value and guid(value['guid']) != guid(key):
                    raise ValueError(f'Export key and value GUID disagree: {key}')
                add(key, value.get('name'), 'exports', str(path))
        except (ValueError, KeyError, OSError) as exc:
            issues.append(f'{path}: {exc}')
    glb_count = 0
    for base in dict.fromkeys(glb_roots):
        if not base.is_dir():
            issues.append(f'GLB directory does not exist: {base}')
            continue
        for path in sorted(base.rglob('*.glb')):
            glb_count += 1
            try:
                for material in glb_materials(path):
                    identity = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {})
                    if 'guid' in identity:
                        add(identity['guid'], material.get('name'), 'glbs', str(path))
            except (ValueError, OSError, KeyError) as exc:
                issues.append(f'{path}: {exc}')
    profiles = re.findall(r'new Profile\("([0-9a-f-]+)",\s*"([^"]+)"\)', (sdk / 'Assets/OpenBrushResonite/OpenBrushProfiles.cs').read_text(encoding='utf-8-sig'), re.I)
    for raw, name in profiles:
        key = guid(raw)
        if key not in rows:
            rows[key] = dict(guid=key, names=[name], descriptors=[], manifests=[], exports=[], glbs=[], profile=None)
        if rows[key]['profile'] is not None:
            issues.append(f'Duplicate SDK profile: {key}')
        rows[key]['profile'] = name
    documented = {}
    with (sdk / 'Documentation/OPENBRUSH-BRUSH-SUPPORT.csv').open(encoding='utf-8-sig', newline='') as f:
        for entry in csv.DictReader(f):
            key = guid(entry['GUID'])
            if key in documented:
                issues.append(f'Duplicate documentation GUID: {key}')
            documented[key] = entry
    markdown = (sdk / 'Documentation/OPENBRUSH-BRUSH-SUPPORT-JA.md').read_text(encoding='utf-8-sig')
    md_rows = [tuple(cell.strip() for cell in line.split('|')[1:3]) for line in markdown.splitlines()
               if line.startswith('| ') and len(line.split('|')) == 7
               and line.split('|')[2].strip() in ('対応あり', '制約あり', '未対応')]
    md_names = [name for name, status in md_rows]
    csv_names = {entry['ブラシ'] for entry in documented.values()}
    for name, status in md_rows:
        if name not in csv_names:
            issues.append(f'Markdown row absent from CSV: {name}')
    for key, entry in documented.items():
        if entry['状態'] not in ('対応あり', '制約あり', '未対応'):
            issues.append(f'Unknown documentation status: {entry["状態"]} ({key})')
        if md_names.count(entry['ブラシ']) != 1:
            issues.append(f'Markdown row missing or repeated: {entry["ブラシ"]} ({key})')
        elif (entry['ブラシ'], entry['状態']) not in md_rows:
            issues.append(f'Markdown/CSV status mismatch: {entry["ブラシ"]} ({key})')
        if key in rows and (entry['状態'] != '未対応') != bool(rows[key]['profile']):
            issues.append(f'Documentation/profile status mismatch: {entry["ブラシ"]} ({key})')
    for row in rows.values():
        row['documented'] = row['guid'] in documented
        if len(row['descriptors']) > 1:
            issues.append(f'Duplicate source descriptor GUID: {row["guid"]}')
    missing = sorted(set(rows) - set(documented))
    stale = sorted(set(documented) - set(rows))
    if stale:
        issues.append('Documented GUIDs absent from all audited inputs: ' + ', '.join(stale))
    return dict(schemaVersion=1, source=str(source), sdk=str(sdk), summary=dict(
        totalIdentities=len(rows), descriptors=sum(bool(r['descriptors']) for r in rows.values()),
        manifestRegistered=sum(bool(r['manifests']) for r in rows.values()), exportListed=sum(bool(r['exports']) for r in rows.values()),
        glbFiles=glb_count, glbIdentities=sum(bool(r['glbs']) for r in rows.values()),
        profiles=len(profiles), documented=len(documented), missing=len(missing), issues=len(issues)),
        missing=missing, issues=issues, nonTextAssets=non_text_assets,
        brushes=sorted(rows.values(), key=lambda r:r['guid']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--open-brush-root', type=Path, required=True)
    parser.add_argument('--sdk-root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--export-manifest', type=Path, action='append', default=[])
    parser.add_argument('--glb-root', type=Path, action='append', default=[])
    parser.add_argument('--output', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    report = audit(args.open_brush_root.resolve(), args.sdk_root.resolve(), args.export_manifest, args.glb_root)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(summary=report['summary'], missing=report['missing'], issues=report['issues']), ensure_ascii=False, indent=2))
    if args.check and (report['missing'] or report['issues']):
        raise SystemExit(1)


if __name__ == '__main__':
    main()
