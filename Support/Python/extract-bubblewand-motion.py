"""Extract undeformed BubbleWand positions/radii before drawing subdivision."""
import argparse, importlib.util, json
from pathlib import Path
import numpy as np
GUID='eba3f993-f9a1-4d35-b84e-bb08f48981a4'

def extract(src,dst,reader_path):
    if Path(dst).exists():raise FileExistsError(dst)
    spec=importlib.util.spec_from_file_location('bubblewand_reader',reader_path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    reader=module.Reader(src);parts=[];ratios=[]
    def vectors(a,keys):return [dict(zip(keys,map(float,row))) for row in a]
    for node in reader.doc['nodes']:
        if 'mesh' not in node:continue
        for primitive in reader.doc['meshes'][node['mesh']]['primitives']:
            material=reader.doc['materials'][primitive['material']]
            if material.get('extensions',{}).get('GOOGLE_tilt_brush_material',{}).get('guid')!=GUID:continue
            attrs=primitive['attributes']
            if not all(k in attrs for k in ('POSITION','NORMAL','TANGENT','COLOR_0','_TB_UNITY_TEXCOORD_0','_TB_TIMESTAMP')):
                raise ValueError('BubbleWand requires original radius and timestamps, not a baked export')
            p,n,c,uv,t=[reader.accessor(attrs[k]) for k in ('POSITION','NORMAL','COLOR_0','_TB_UNITY_TEXCOORD_0','_TB_TIMESTAMP')]
            count=len(p)
            tangents=reader.accessor(attrs['TANGENT'])
            if tangents.shape!=(count,4) or np.any(np.abs(np.abs(tangents[:,3])-1)>1e-5):raise ValueError('Invalid BubbleWand tangent frame')
            if p.shape!=(count,3) or n.shape!=p.shape or uv.shape!=p.shape or t.shape!=p.shape or c.shape!=(count,4) or np.any(uv[:,2]<0):
                raise ValueError('Invalid BubbleWand source channels')
            if not all(np.isfinite(values).all() for values in (p,n,c,uv,t,tangents)):
                raise ValueError('Invalid BubbleWand non-finite source channels')
            # Cross-sections, grouped within their own stroke, must still have
            # their original radius. Applying the pulse twice must be rejected.
            ring_groups={}
            for i in range(count):ring_groups.setdefault(tuple(np.r_[t[i,:2],uv[i,0],uv[i,2]]),[]).append(i)
            verified=[]
            # UV.x is not a unique ring ID: it can repeat within one stroke.
            # The raw TubeBrush layout stores each ring consecutively, with
            # UV.y running around its circumference (including the seam).
            # Split disjoint vertex runs and UV.y wraps before fitting a radius.
            rings=[]
            for candidates in ring_groups.values():
                ids=[];direction=0
                for i in candidates:
                    step=np.sign(uv[i,1]-uv[ids[-1],1]) if ids else 0
                    if ids and (i!=ids[-1]+1 or (step and direction and step!=direction)):
                        rings.append(ids);ids=[];direction=0;step=0
                    ids.append(i)
                    if step:direction=step
                if ids:rings.append(ids)
            for ids in rings:
                radius=uv[ids[0],2]
                if len(ids)<4 or radius<=0:continue
                pp=p[ids]-p[ids].mean(0);nn=n[ids]-n[ids].mean(0)
                denom=(nn*nn).sum()
                if denom<1:continue
                ratio=float((pp*nn).sum()/denom/radius)
                verified.append(ratio)
            if not verified or max(abs(r-1) for r in verified)>.02:
                raise ValueError('BubbleWand is not verified undeformed geometry')
            ratios.extend(verified)
            tri=reader.accessor(primitive['indices']).astype(int).reshape(-1,3)[:,[0,2,1]]
            if tri.min()<0 or tri.max()>=count:raise ValueError('Invalid BubbleWand indices')
            parts.append(dict(nodeName=node['name'],positions=vectors(p*[1,1,-1],'xyz'),normals=vectors(n*[1,1,-1],'xyz'),
                uvRadius=vectors(uv,'xyz'),tangents=vectors(tangents*[1,1,-1,1],'xyzw'),colors=vectors(c,'rgba'),times=t[:,2].tolist(),strokeStarts=t[:,0].tolist(),strokeEnds=t[:,1].tolist(),triangles=tri.ravel().tolist()))
    if not parts or len({p['nodeName'] for p in parts})!=len(parts):raise ValueError('Missing or ambiguous BubbleWand nodes')
    result=dict(schemaVersion=1,brushGuid=GUID,coordinateSystem='UnityInvertZ',rawRadiusRatioMin=min(ratios),rawRadiusRatioMax=max(ratios),parts=parts)
    Path(dst).write_text(json.dumps(result,separators=(',',':'),allow_nan=False),encoding='utf-8')
    print(json.dumps(dict(parts=len(parts),vertices=sum(len(p['positions']) for p in parts),radiusRatio=[min(ratios),max(ratios)])))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('input');parser.add_argument('output');parser.add_argument('--reader',required=True)
    args=parser.parse_args();extract(args.input,args.output,args.reader)
