#!/usr/bin/env python3
"""Bake the audited LightWire vertex bulge into a raw Open Brush GLB.

Run before stroke-growth splitting. Requires original _TB_UNITY_TEXCOORD_0.z
(radius) and NORMAL. Keeps every other brush, UV, color and timestamp unchanged.
The bulb geometry is static in the source shader; only emission chases over time.
Equation: Open Brush LightWireVertexDeformation.hlsl (Tilt Brush, Apache-2.0).
"""
import argparse, base64, json, math, mimetypes, struct
from pathlib import Path
from urllib.parse import unquote

GUID = '4391aaaa-df81-4396-9e33-31e4e4930b27'

def bake(input_path, output_path):
    src, dst = Path(input_path), Path(output_path)
    if src.resolve() == dst.resolve() or dst.exists():
        raise ValueError('Use a new output file; existing files are not overwritten')
    raw = src.read_bytes()
    if struct.unpack_from('<III', raw) != (0x46546c67, 2, len(raw)):
        raise ValueError('Expected a complete GLB 2')
    jslen, kind = struct.unpack_from('<II',raw,12)
    doc = json.loads(raw[20:20+jslen])
    binlen, bkind = struct.unpack_from('<II',raw,20+jslen)
    if kind != 0x4e4f534a or bkind != 0x004e4942:
        raise ValueError('Expected JSON and BIN chunks')
    binary = bytearray(raw[28+jslen:28+jslen+binlen])
    if doc.get('extras',{}).get('lightWireShapeBake'):
        raise ValueError('LightWire shape was already baked')
    def read(index):
        a=doc['accessors'][index]
        if a['componentType']!=5126 or a['type']!='VEC3' or a.get('sparse'):
            raise ValueError('Expected nonsparse FLOAT VEC3 brush data')
        view=doc['bufferViews'][a['bufferView']]
        offset=view.get('byteOffset',0)+a.get('byteOffset',0); stride=view.get('byteStride',12)
        return [struct.unpack_from('<fff',binary,offset+i*stride) for i in range(a['count'])],offset,stride
    changed=0; seen=set(); ratios=[]
    for mesh in doc['meshes']:
        for primitive in mesh['primitives']:
            mat=doc['materials'][primitive['material']]
            guid=mat.get('extensions',{}).get('GOOGLE_tilt_brush_material',{}).get('guid') or mat.get('extras',{}).get('openBrush',{}).get('brushGuid')
            if guid!=GUID: continue
            attrs=primitive['attributes']; idx=attrs['POSITION']
            if idx in seen: continue
            seen.add(idx)
            pos,offset,stride=read(idx); normals,_,_=read(attrs['NORMAL']); uv,_,_=read(attrs['_TB_UNITY_TEXCOORD_0'])
            if not len(pos)==len(normals)==len(uv): raise ValueError('Vertex attribute count mismatch')
            # Fit circular cross-sections to reject geometry already expanded by an exporter.
            groups={}
            for i,(u,v,radius) in enumerate(uv): groups.setdefault(u,[]).append(i)
            for u,ids in groups.items():
                if len(ids)<4 or uv[ids[0]][2]<=0 or math.sin(math.fmod(u*2,1)*math.pi)>=.15: continue
                pmean=[sum(pos[i][k] for i in ids)/len(ids) for k in range(3)]
                nmean=[sum(normals[i][k] for i in ids)/len(ids) for k in range(3)]
                den=sum((normals[i][k]-nmean[k])**2 for i in ids for k in range(3))
                if den<1: continue
                radius=sum((pos[i][k]-pmean[k])*(normals[i][k]-nmean[k]) for i in ids for k in range(3))/den
                ratio=radius/uv[ids[0]][2]; ratios.append(ratio)
                if abs(ratio-1)>.02: raise ValueError('LightWire is already deformed or not a supported raw tube: radius ratio '+str(ratio))
            if not ratios: raise ValueError('Cannot verify unbaked LightWire cross-sections')
            result=[]
            for i,(p,n,tex) in enumerate(zip(pos,normals,uv)):
                u,v,radius=tex
                if not all(math.isfinite(x) for x in (*p,*n,*tex)) or radius<0: raise ValueError('Invalid source geometry')
                amount=radius*.9 if math.sin(math.fmod(u*2,1)*math.pi)<.15 else 0
                q=tuple(p[k]+n[k]*amount for k in range(3))
                result.append(q)
                if amount:
                    struct.pack_into('<fff',binary,offset+i*stride,*q);changed+=1
            doc['accessors'][idx]['min']=[min(p[k] for p in result) for k in range(3)]
            doc['accessors'][idx]['max']=[max(p[k] for p in result) for k in range(3)]
    if not changed: raise ValueError('No LightWire bulb vertices found')
    # Make the intermediate GLB relocatable without changing any image bytes.
    for image in doc.get('images',[]):
        uri=image.get('uri','')
        if uri and not uri.startswith('data:'):
            if '://' in uri: raise ValueError('Remote textures are not supported')
            path=src.parent/unquote(uri)
            mime=mimetypes.guess_type(str(path))[0] or 'image/png'
            image['uri']='data:'+mime+';base64,'+base64.b64encode(path.read_bytes()).decode('ascii')
    report={'brushGuid':GUID,'changedVertices':changed,'radiusMultiplier':1.9,'sourceRadiusRatios':ratios,'mode':'static shader vertex bulge before stroke splitting'}
    doc.setdefault('extras',{})['lightWireShapeBake']=report
    js=json.dumps(doc,separators=(',',':')).encode(); js+=b' '*((-len(js))%4)
    binary+=b'\0'*((-len(binary))%4)
    dst.write_bytes(struct.pack('<III',0x46546c67,2,28+len(js)+len(binary))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(binary),0x004e4942)+binary)
    return report

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('input');parser.add_argument('output');args=parser.parse_args()
    print(json.dumps(bake(args.input,args.output),indent=2))
