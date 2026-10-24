"""Keep supported brush primitives before texture resolution and effect extraction.

Do not infer support from a material name. Preserve node hierarchy/transforms,
raw vertex channels and BIN bytes; compact material/texture/image references.
"""
import json
import struct


def filter_document(doc, profiles):
    materials = doc.get('materials', [])
    supported = {}
    for index, material in enumerate(materials):
        metadata = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {})
        guid = metadata.get('guid')
        if isinstance(guid, str) and guid.lower() in profiles:
            metadata['guid'] = guid.lower()
            supported[index] = guid.lower()
    used_meshes = {node['mesh'] for node in doc.get('nodes', []) if 'mesh' in node}
    used_materials = set()
    skipped = {}
    for mesh_index, mesh in enumerate(doc.get('meshes', [])):
        keep = []
        if mesh_index not in used_meshes:
            mesh['primitives'] = keep
            continue
        for primitive in mesh.get('primitives', []):
            index = primitive.get('material')
            if index is not None and (not isinstance(index, int) or isinstance(index, bool) or not 0 <= index < len(materials)):
                raise ValueError('Invalid primitive material index: ' + str(index))
            if index in supported:
                keep.append(primitive)
                used_materials.add(index)
                continue
            material = materials[index] if index is not None else {}
            guid = material.get('extensions', {}).get('GOOGLE_tilt_brush_material', {}).get('guid')
            guid = guid.lower() if isinstance(guid, str) else ''
            name = material.get('name') or guid or 'ブラシ不明'
            reason = 'unsupported_brush' if guid else 'missing_brush_guid'
            key = (guid, name, reason)
            entry = skipped.setdefault(key, dict(code=reason, guid=guid, name=name, primitiveCount=0))
            entry['primitiveCount'] += 1
        mesh['primitives'] = keep
    # Retain hierarchy-only nodes (including parents of supported meshes).
    for node in doc.get('nodes', []):
        if 'mesh' in node and not doc['meshes'][node['mesh']]['primitives']:
            del node['mesh']
    remap = {old: new for new, old in enumerate(sorted(used_materials))}
    for mesh in doc.get('meshes', []):
        for primitive in mesh['primitives']:
            primitive['material'] = remap[primitive['material']]
    doc['materials'] = [materials[old] for old in sorted(used_materials)]
    # TextureInfo entries occur in core materials and KHR material extensions.
    texture_infos = []
    def walk(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key.endswith('Texture') and isinstance(child, dict) and 'index' in child:
                    texture_infos.append(child)
                else:
                    walk(child)
        elif isinstance(value, list):
            for child in value:
                walk(child)
    walk(doc['materials'])
    texture_indices = sorted({info['index'] for info in texture_infos})
    textures = doc.get('textures', [])
    texture_map = {old: new for new, old in enumerate(texture_indices)}
    for info in texture_infos:
        info['index'] = texture_map[info['index']]
    doc['textures'] = [textures[old] for old in texture_indices]
    # Alternate image sources (e.g. KHR_texture_basisu) also use source indices.
    image_refs = []
    for texture in doc['textures']:
        if 'source' in texture:
            image_refs.append(texture)
        for extension in texture.get('extensions', {}).values():
            if isinstance(extension, dict) and 'source' in extension:
                image_refs.append(extension)
    image_indices = sorted({ref['source'] for ref in image_refs})
    image_map = {old: new for new, old in enumerate(image_indices)}
    images = doc.get('images', [])
    for ref in image_refs:
        ref['source'] = image_map[ref['source']]
    doc['images'] = [images[old] for old in image_indices]
    return {supported[index] for index in used_materials}, list(skipped.values())


def save(reader, destination):
    js = json.dumps(reader.doc, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
    js += b' ' * (-len(js) % 4)
    with destination.open('xb') as stream:
        stream.write(struct.pack('<III', 0x46546c67, 2, 28 + len(js) + len(reader.binary)))
        stream.write(struct.pack('<II', len(js), 0x4e4f534a))
        stream.write(js)
        stream.write(struct.pack('<II', len(reader.binary), 0x004e4942))
        stream.write(reader.binary)
