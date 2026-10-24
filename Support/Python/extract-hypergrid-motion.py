"""Retain canvas-local HyperGrid quads and offsets before drawing slicing."""
import argparse, importlib.util, json
from pathlib import Path
import numpy as np
GUID='6a1cf9f9-032c-45ec-9b6e-a6680bee32e9'

def extract(src,dst,reader_path):
    if Path(dst).exists(): raise ValueError('Use a new HyperGrid sidecar')
    spec=importlib.util.spec_from_file_location('reader',reader_path);mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
    reader=mod.Reader(src);parts=[]
    def vec(a,keys):return [dict(zip(keys,map(float,v))) for v in a]
    for node in reader.doc['nodes']:
        if 'mesh' not in node:continue
        for prim in reader.doc['meshes'][node['mesh']]['primitives']:
            material=reader.doc['materials'][prim['material']]
            if material.get('extensions',{}).get('GOOGLE_tilt_brush_material',{}).get('guid')!=GUID:continue
            a=prim['attributes'];required=['POSITION','COLOR_0','_TB_UNITY_TEXCOORD_0','_TB_UNITY_TEXCOORD_1','_TB_TIMESTAMP']
            if any(k not in a for k in required):raise ValueError('Missing original HyperGrid attributes')
            p,c,uv,offset,t=[reader.accessor(a[k]) for k in required]
            n=len(p)
            if n==0 or n%4 or p.shape!=(n,3) or c.shape!=(n,4) or uv.shape!=(n,2) or offset.shape!=(n,4) or t.shape!=(n,3):raise ValueError('Expected original four-corner HyperGrid quads')
            if any(not np.isfinite(x).all() for x in (p,c,uv,offset,t)):raise ValueError('Nonfinite HyperGrid data')
            tri=reader.accessor(prim['indices']).astype(int).reshape(-1,3)
            if len(tri)!=n//2 or tri.min()<0 or tri.max()>=n or any(len(set(v//4))!=1 for v in tri):raise ValueError('Invalid HyperGrid quad topology')
            if np.any(np.bincount(tri.ravel()//4,minlength=n//4)!=6):raise ValueError('Expected two triangles per HyperGrid quad')
            births=[];source_births=[]
            for i in range(0,n,4):
                mids=p[i:i+4]-offset[i:i+4,:3]
                radius=np.linalg.norm(offset[i:i+4,:3],axis=1)
                if radius.min()<1e-7 or not np.allclose(radius,radius[0],atol=1e-5,rtol=1e-4) or np.max(np.linalg.norm(mids-mids.mean(0),axis=1))>2e-5:raise ValueError('HyperGrid geometry already deformed or not in the offset basis')
                if not np.allclose(t[i:i+4,:2],t[i,:2],atol=1e-6,rtol=0) or not np.allclose(offset[i:i+4,3],offset[i,3],atol=1e-6,rtol=0):raise ValueError('Quad crosses stroke or birth identity')
                births.append(float(t[i:i+4,2].mean()));source_births.append(float(offset[i,3]))
            parts.append(dict(nodeName=node['name'],positions=vec(p*[1,1,-1],'xyz'),offsets=vec(offset[:,:3]*[1,1,-1],'xyz'),uv=vec(uv,'xy'),colors=vec(c,'rgba'),births=births,sourceBirths=source_births,strokeStarts=t[::4,0].tolist(),strokeEnds=t[::4,1].tolist(),triangles=tri[:,[0,2,1]].ravel().tolist()))
    if not parts or len(set(p['nodeName'] for p in parts))!=len(parts):raise ValueError('Missing or ambiguous HyperGrid nodes')
    Path(dst).write_text(json.dumps(dict(schemaVersion=1,brushGuid=GUID,coordinateSystem='UnityInvertZ',appUnitsPerMeter=10,parts=parts),separators=(',',':')),encoding='utf-8')
    print(json.dumps(dict(parts=len(parts),quads=sum(len(p['births']) for p in parts))))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('input');p.add_argument('output');p.add_argument('--reader',required=True);a=p.parse_args();extract(a.input,a.output,a.reader)
