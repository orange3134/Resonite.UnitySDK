"""Resolve historical Tilt Brush image URLs against an explicit local asset root.

No network requests. The asset root is Open Brush's Support/shaders/brushes.
Preserves all geometry, custom attributes, material metadata and BIN bytes.
"""
import base64
import json
from pathlib import Path
import struct
from urllib.parse import unquote, urlparse


def read_glb(source):
    raw = Path(source).read_bytes()
    if len(raw) < 28 or struct.unpack_from('<III', raw) != (0x46546c67, 2, len(raw)):
        raise ValueError('Expected a complete GLB 2')
    length, kind = struct.unpack_from('<II', raw, 12)
    if kind != 0x4e4f534a or 20 + length + 8 > len(raw):
        raise ValueError('Expected GLB JSON and BIN chunks')
    doc = json.loads(raw[20:20 + length])
    size, kind = struct.unpack_from('<II', raw, 20 + length)
    if kind != 0x004e4942 or 28 + length + size != len(raw):
        raise ValueError('Expected one complete GLB BIN chunk')
    return doc, raw[28 + length:]


def has_remote_images(doc):
    return any(urlparse(im.get('uri', '')).scheme not in ('', 'data')
               or im.get('uri', '').startswith('//') for im in doc.get('images', []))


def contained_file(root, relative):
    root = Path(root).resolve()
    # Reject encoded traversal and Windows/UNC/drive paths before resolution.
    relative = unquote(relative)
    parts = relative.split('/')
    if not relative or '\\' in relative or ':' in relative or any(p in ('', '.', '..') for p in parts):
        raise ValueError('Invalid texture path: ' + relative)
    path = (root / relative).resolve()
    if not path.is_relative_to(root):
        raise ValueError('Texture path leaves the asset directory: ' + relative)
    if not path.is_file():
        raise FileNotFoundError('Local brush texture not found: ' + str(path))
    return path


def resolve_texture(source, uri, brush_assets):
    parsed = urlparse(uri)
    if parsed.scheme or parsed.netloc:
        if (parsed.scheme not in ('http', 'https')
                or parsed.netloc not in ('www.tiltbrush.com', 'tiltbrush.com')
                or parsed.query or parsed.fragment
                or not parsed.path.startswith('/shaders/brushes/')):
            raise ValueError('Unsupported texture URL; no download performed: ' + uri)
        relative = parsed.path[len('/shaders/brushes/'):]
        return contained_file(brush_assets, relative)
    if parsed.query or parsed.fragment:
        raise ValueError('Unsupported local texture URI: ' + uri)
    return contained_file(Path(source).parent, parsed.path)


def embed(source, destination, brush_assets, *, image_source=None):
    source, destination = Path(source), Path(destination)
    if source.resolve() == destination.resolve() or destination.exists():
        raise ValueError('Use a new output file for localized textures')
    root = Path(brush_assets).resolve() if brush_assets is not None else None
    if root is not None and not root.is_dir():
        raise ValueError('--brush-assets must be an existing Support/shaders/brushes directory: ' + str(root))
    doc, binary = read_glb(source)
    count = 0
    for image in doc.get('images', []):
        uri = image.get('uri', '')
        if not uri or uri.startswith('data:'):
            continue
        path = resolve_texture(image_source or source, uri, root)
        data = path.read_bytes()
        if data.startswith(b'\x89PNG\r\n\x1a\n'):
            mime = 'image/png'
        elif data.startswith(b'\xff\xd8\xff'):
            mime = 'image/jpeg'
        else:
            raise ValueError('Expected PNG or JPEG texture: ' + str(path))
        image['uri'] = 'data:' + mime + ';base64,' + base64.b64encode(data).decode('ascii')
        count += 1
    js = json.dumps(doc, separators=(',', ':'), ensure_ascii=False).encode('utf-8')
    js += b' ' * (-len(js) % 4)
    output = (struct.pack('<III', 0x46546c67, 2, 28 + len(js) + len(binary))
              + struct.pack('<II', len(js), 0x4e4f534a) + js
              + struct.pack('<II', len(binary), 0x004e4942) + binary)
    with destination.open('xb') as stream:
        stream.write(output)
    return count
