#!/usr/bin/env python3
"""Extract raw Disco geometry for SDK motion before growth slicing drops radius.
Requires numpy and animate-brush-glb.py's audited GLB reader. Coordinates are
converted to Unity (invert Z and winding); nodes retain their imported transform.
"""
import argparse, importlib.util, json
from pathlib import Path
import numpy as np
GUID='4391aaaa-df73-4396-9e33-31e4e4930b27'
def extract(src, dst, reader_path):
    if Path(dst).exists(): raise ValueError('Use a new output file')
    spec=importlib.util.spec_from_file_location('growth_reader',reader_path)
    mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
    reader=mod.Reader(src);parts=[];ratios=[]
    def vectors(a,keys):return [dict(zip(keys,map(float,row))) for row in a]
    for node in reader.doc['nodes']:
        if 'mesh' not in node:continue
        for prim in reader.doc['meshes'][node['mesh']]['primitives']:
            mat=reader.doc['materials'][prim['material']]
            guid=mat.get('extensions',{}).get('GOOGLE_tilt_brush_material',{}).get('guid') or mat.get('extras',{}).get('openBrush',{}).get('brushGuid')
            if guid!=GUID:continue
            a=prim['attributes'];p=reader.accessor(a['POSITION']);n=reader.accessor(a['NORMAL']);uv=reader.accessor(a['_TB_UNITY_TEXCOORD_0']);c=reader.accessor(a['COLOR_0'])
            if uv.shape!=(len(p),3) or np.any(uv[:,2]<0):raise ValueError('Missing raw radius')
            local_ratios=[]
            # UV.x repeats across independent strokes. Never fit different
            # centers/radii together as one cross-section.
            times=reader.accessor(a['_TB_TIMESTAMP'])
            rings={}
            for i in range(len(p)):
                rings.setdefault(tuple(np.r_[times[i,:2],uv[i,0],uv[i,2]]),[]).append(i)
            for ids in rings.values():
                if len(ids)<4 or uv[ids[0],2]<=0:continue
                pp=p[ids]-p[ids].mean(0);nn=n[ids]-n[ids].mean(0)
                if (nn*nn).sum()<1:continue
                local_ratios.append(float((pp*nn).sum()/(nn*nn).sum()/uv[ids[0],2]))
            if not local_ratios or max(abs(x-1) for x in local_ratios)>.02:raise ValueError('Disco is not verified undeformed geometry')
            ratios+=local_ratios
            tri=reader.accessor(prim['indices']).astype(int).reshape(-1,3)[:,[0,2,1]]
            if tri.min()<0 or tri.max()>=len(p):raise ValueError('Invalid indices')
            parts.append(dict(nodeName=node['name'],positions=vectors(p*[1,1,-1],'xyz'),normals=vectors(n*[1,1,-1],'xyz'),uvRadius=vectors(uv,'xyz'),colors=vectors(c,'rgba'),triangles=tri.ravel().tolist(),times=reader.accessor(a['_TB_TIMESTAMP'])[:,2].tolist(),strokeStarts=reader.accessor(a['_TB_TIMESTAMP'])[:,0].tolist(),strokeEnds=reader.accessor(a['_TB_TIMESTAMP'])[:,1].tolist()))
    if not parts or len({p['nodeName'] for p in parts})!=len(parts):raise ValueError('Missing or ambiguous Disco nodes')
    result=dict(schemaVersion=1,brushGuid=GUID,coordinateSystem='UnityInvertZ',source=str(Path(src).resolve()),rawRadiusRatioMin=min(ratios),rawRadiusRatioMax=max(ratios),parts=parts)
    Path(dst).write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
    print(json.dumps(dict(parts=len(parts),vertices=sum(len(p['positions']) for p in parts),radiusRatio=[min(ratios),max(ratios)])))
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('input');p.add_argument('output');p.add_argument('--reader',required=True);a=p.parse_args();extract(a.input,a.output,a.reader)
