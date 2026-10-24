"""Retain normal Electricity shader inputs before GLB growth slicing."""
import argparse,importlib.util,json
from pathlib import Path
import numpy as np
GUID='f6e85de3-6dcc-4e7f-87fd-cee8c3d25d51'
def extract(src,dst,reader_path):
    if Path(dst).exists(): raise ValueError('Use a new output file')
    spec=importlib.util.spec_from_file_location('reader',reader_path);mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
    reader=mod.Reader(src);parts=[]
    def vec(a,k):return [dict(zip(k,map(float,row))) for row in a]
    for node in reader.doc['nodes']:
        if 'mesh' not in node:continue
        for prim in reader.doc['meshes'][node['mesh']]['primitives']:
            material=reader.doc['materials'][prim['material']]
            guid=material.get('extensions',{}).get('GOOGLE_tilt_brush_material',{}).get('guid') or material.get('extras',{}).get('openBrush',{}).get('brushGuid')
            if guid!=GUID:continue
            a=prim['attributes'];p=reader.accessor(a['POSITION']);o=reader.accessor(a['_TB_UNITY_TEXCOORD_1']);uv=reader.accessor(a['_TB_UNITY_TEXCOORD_0']);c=reader.accessor(a['COLOR_0']);t=reader.accessor(a['_TB_TIMESTAMP']).copy()
            if o.shape!=p.shape or uv.shape!=(len(p),2) or t.shape!=p.shape or c.shape!=(len(p),4):raise ValueError('Missing original Electricity channels')
            if np.any(uv<-.0001) or np.any(uv>1.0001):raise ValueError('Expected unitized source UVs')
            tri=reader.accessor(prim['indices']).astype(int).reshape(-1,3)[:,[0,2,1]]
            if tri.min()<0 or tri.max()>=len(p):raise ValueError('Invalid indices')
            # Backface duplicates receive slightly different exported estimates.
            # Align their physical vertex time, while retaining both source faces.
            groups={}
            for i in range(len(p)):groups.setdefault(tuple(np.r_[p[i],o[i],uv[i],c[i],t[i,:2]]),[]).append(i)
            for ids in groups.values():t[ids,2]=t[ids,2].mean()
            # Offset pairs at one ribbon cross-section must share the midpoint.
            mids=p-o; paired=0
            for start,end in np.unique(t[:,:2],axis=0):
                indices=np.where((t[:,0]==start)&(t[:,1]==end))[0]
                for u in np.unique(uv[indices,0]):
                    ids=indices[np.isclose(uv[indices,0],u,atol=1e-7,rtol=0)]
                    if len(np.unique(uv[ids,1]))<2:continue
                    if np.max(np.linalg.norm(mids[ids]-mids[ids].mean(0),axis=1))>2e-5:raise ValueError('Electricity geometry is already deformed or offsets have a different basis')
                    paired+=1
            if not paired:raise ValueError('No verified original ribbon cross sections')
            parts.append(dict(nodeName=node['name'],positions=vec(p*[1,1,-1],'xyz'),offsets=vec(o*[1,1,-1],'xyz'),uv=vec(uv,'xy'),colors=vec(c,'rgba'),times=t[:,2].tolist(),strokeStarts=t[:,0].tolist(),strokeEnds=t[:,1].tolist(),triangles=tri.ravel().tolist(),verifiedCrossSections=paired))
    if not parts or len({p['nodeName'] for p in parts})!=len(parts):raise ValueError('Missing or ambiguous Electricity nodes')
    Path(dst).write_text(json.dumps(dict(schemaVersion=1,brushGuid=GUID,coordinateSystem='UnityInvertZ',source=str(Path(src).resolve()),parts=parts),separators=(',',':')))
    print(json.dumps(dict(parts=len(parts),vertices=sum(len(p['positions']) for p in parts),crossSections=sum(p['verifiedCrossSections'] for p in parts))))
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('input');p.add_argument('output');p.add_argument('--reader',required=True);a=p.parse_args();extract(a.input,a.output,a.reader)
