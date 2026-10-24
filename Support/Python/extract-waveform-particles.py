"""Preserve normal WaveformParticles offsets before stroke slicing."""
import argparse,importlib.util,json
from pathlib import Path
import numpy as np
GUID='6a1cf9f9-032c-45ec-9b6e-a6680bee30f7'
def extract(src,dst,reader_path):
    if Path(dst).exists():raise ValueError('Use a new output file')
    spec=importlib.util.spec_from_file_location('reader',reader_path);mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
    reader=mod.Reader(src);parts=[]
    def vec(a,k):return [dict(zip(k,map(float,row))) for row in a]
    for node in reader.doc['nodes']:
        if 'mesh' not in node:continue
        for prim in reader.doc['meshes'][node['mesh']]['primitives']:
            mat=reader.doc['materials'][prim['material']]
            if mat.get('extensions',{}).get('GOOGLE_tilt_brush_material',{}).get('guid')!=GUID:continue
            attrs=prim['attributes']; required=['POSITION','_TB_UNITY_TEXCOORD_1','_TB_UNITY_TEXCOORD_0','COLOR_0','_TB_TIMESTAMP']
            if any(k not in attrs for k in required):raise ValueError('WaveformParticles requires original offsets, birth and timestamps')
            p,o,uv,c,t=[reader.accessor(attrs[k]) for k in required];n=len(p)
            if p.shape!=(n,3) or o.shape!=(n,4) or uv.shape!=(n,2) or c.shape!=(n,4) or t.shape!=(n,3):raise ValueError('Invalid WaveformParticles channels')
            tri=reader.accessor(prim['indices']).astype(int).reshape(-1,3)[:,[0,2,1]]
            if tri.size==0 or tri.min()<0 or tri.max()>=n:raise ValueError('Invalid particle triangles')
            # Every triangle is one original rigid particle, never a billboard.
            mids=p-o[:,:3]
            if np.max(np.abs(mids[tri]-mids[tri[:,0],None]))>2e-5 or np.max(np.abs(c[tri]-c[tri[:,0],None]))>1e-6 or np.max(np.abs(o[tri,3]-o[tri[:,0],3,None]))>1e-5:raise ValueError('Particle offsets do not share an undeformed center/color/birth')
            # Keep each triangle's reveal time coherent, avoiding a stretched particle.
            birth=t[:,2].copy()
            for ids in tri:birth[ids]=t[ids,2].mean()
            parts.append(dict(nodeName=node['name'],positions=vec(p*[1,1,-1],'xyz'),offsets=vec(o[:,:3]*[1,1,-1],'xyz'),uv=vec(uv,'xy'),colors=vec(c,'rgba'),times=birth.tolist(),strokeStarts=t[:,0].tolist(),strokeEnds=t[:,1].tolist(),triangles=tri.ravel().tolist()))
    if not parts or len({p['nodeName'] for p in parts})!=len(parts):raise ValueError('Missing or ambiguous WaveformParticles batches')
    Path(dst).write_text(json.dumps(dict(schemaVersion=1,brushGuid=GUID,coordinateSystem='UnityInvertZ',parts=parts),separators=(',',':')))
    return dict(parts=len(parts),vertices=sum(len(p['positions']) for p in parts))
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('input');p.add_argument('output');p.add_argument('--reader',required=True);a=p.parse_args();print(extract(a.input,a.output,a.reader))
