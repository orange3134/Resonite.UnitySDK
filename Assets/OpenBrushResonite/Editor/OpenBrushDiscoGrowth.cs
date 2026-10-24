using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public static class OpenBrushDiscoGrowth {
    struct Vertex {public Vector3 p;public Color c;public float t;public static Vertex Lerp(Vertex a,Vertex b,float k){return new Vertex{p=Vector3.Lerp(a.p,b.p,k),c=Color.Lerp(a.c,b.c,k),t=Mathf.Lerp(a.t,b.t,k)};}}
    static bool Finite(float v) {return !float.IsNaN(v) && !float.IsInfinity(v);}
    // Timestamp group identity is essential: separate strokes may overlap in source time.
    public static int[] MatchStrokes(OpenBrushDiscoImport.Part part,OpenBrushGrowthImport.Stroke[] strokes,float duration) {
        if(strokes==null || strokes.Length==0 || !Finite(duration) || duration<=0 || part.times==null || part.times.Length!=part.positions.Length)
            throw new InvalidOperationException("Missing Disco vertex timestamps or stroke timing");
        if(strokes.Any(s=>!Finite(s.sourceStart) || !Finite(s.sourceEnd) || s.sourceEnd<=s.sourceStart || !Finite(s.playStart) || !Finite(s.playEnd) || s.playStart<0 || s.playEnd<=s.playStart || s.playEnd>duration))
            throw new InvalidOperationException("Invalid Disco stroke interval");
        bool groups=part.strokeStarts!=null && part.strokeEnds!=null && part.strokeStarts.Length==part.times.Length && part.strokeEnds.Length==part.times.Length;
        if(!groups && strokes.Length!=1)throw new InvalidOperationException("Regenerate Disco sidecar with per-vertex stroke endpoints");
        var result=new int[part.times.Length];
        for(int v=0;v<result.Length;v++) {
            var matches=Enumerable.Range(0,strokes.Length).Where(i=>!groups || (Mathf.Abs(strokes[i].sourceStart-part.strokeStarts[v])<.0001f && Mathf.Abs(strokes[i].sourceEnd-part.strokeEnds[v])<.0001f)).ToArray();
            if(matches.Length!=1 || !Finite(part.times[v]) || part.times[v]<strokes[matches[0]].sourceStart-.0001f || part.times[v]>strokes[matches[0]].sourceEnd+.0001f)
                throw new InvalidOperationException("Missing or ambiguous Disco vertex stroke mapping");
            result[v]=matches[0];
        }
        for(int i=0;i<part.triangles.Length;i+=3)
            if(result[part.triangles[i]]!=result[part.triangles[i+1]] || result[part.triangles[i]]!=result[part.triangles[i+2]])throw new InvalidOperationException("Disco triangle crosses strokes");
        if(result.Distinct().Count()!=strokes.Length)throw new InvalidOperationException("Disco timeline includes a stroke without geometry");
        return result;
    }
    public static void Bake(OpenBrushDiscoImport.Part part,Transform parent,OpenBrushGrowthImport.Stroke stroke,float duration,out Mesh[] frames,out Texture2D[] glints,out float[] times) {
        Bake(part,parent,new[]{stroke},duration,out frames,out glints,out times);
    }
    public static void Bake(OpenBrushDiscoImport.Part part,Transform parent,OpenBrushGrowthImport.Stroke[] strokes,float duration,out Mesh[] frames,out Texture2D[] glints,out float[] times) {
        var groups=MatchStrokes(part,strokes,duration);float start=strokes.Min(s=>s.playStart);
        int count=Mathf.CeilToInt((duration-start)*30)+1,faces=part.triangles.Length/3;
        var size=OpenBrushDiscoImport.AtlasSize(faces);
        frames=new Mesh[count];glints=new Texture2D[count];times=new float[count];
        for(int frame=0;frame<count;frame++) {
            float time=Mathf.Min(duration,start+frame/30f);times[frame]=time;
            var positions=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var uv=new List<Vector2>();var light=new Color[size.x*size.y];
            for(int face=0;face<faces;face++) {
                var stroke=strokes[groups[part.triangles[face*3]]];
                if(time<=stroke.playStart)continue;
                float cutoff=Mathf.Lerp(stroke.sourceStart,stroke.sourceEnd,Mathf.Clamp01((time-stroke.playStart)/(stroke.playEnd-stroke.playStart)));
                var tri=new Vertex[3];for(int j=0;j<3;j++){int v=part.triangles[face*3+j];tri[j]=new Vertex{p=OpenBrushDiscoImport.Deform(part.positions[v],part.normals[v],part.uvRadius[v],time),c=part.colors[v],t=part.times[v]};}
                var normal=Vector3.Cross(tri[1].p-tri[0].p,tri[2].p-tri[0].p).normalized;
                float normalY=parent.TransformDirection(normal).normalized.y;var color=(tri[0].c+tri[1].c+tri[2].c)/3;light[face]=OpenBrushDiscoImport.EmissionColor(color,normalY);
                var polygon=new List<Vertex>();for(int j=0;j<3;j++){var a=tri[j];var b=tri[(j+1)%3];bool inside=a.t<=cutoff,other=b.t<=cutoff;if(inside)polygon.Add(a);if(inside!=other)polygon.Add(Vertex.Lerp(a,b,(cutoff-a.t)/(b.t-a.t)));}
                for(int j=1;j+1<polygon.Count;j++)foreach(var vertex in new[]{polygon[0],polygon[j],polygon[j+1]}){positions.Add(vertex.p);normals.Add(normal);colors.Add(vertex.c);uv.Add(OpenBrushDiscoImport.FaceUV(face,size));}
            }
            if(positions.Count==0){for(int j=0;j<3;j++){positions.Add(part.positions[0]);normals.Add(Vector3.up);colors.Add(Color.clear);uv.Add(Vector2.zero);}}
            var mesh=new Mesh{name="DiscoGrowth_"+frame,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(Enumerable.Range(0,positions.Count).ToArray(),0);mesh.RecalculateBounds();frames[frame]=mesh;
            var texture=new Texture2D(size.x,size.y,TextureFormat.RGBAFloat,false,true){name="DiscoGrowthGlint_"+frame,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};texture.SetPixels(light);texture.Apply(false,false);glints[frame]=texture;
        }
    }
}
