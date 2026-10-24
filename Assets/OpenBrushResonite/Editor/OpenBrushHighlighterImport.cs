using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// Highlighter's fragment shader thresholds texture alpha * interpolated vertex alpha,
// then emits full RGB. Native MaskMode.AlphaClip does not include vertex alpha.
// Bake the interpolated mask and emission into triangle charts, retaining the native
// clip (not a pre-thresholded texture) so filtering still occurs before the threshold.
public static class OpenBrushHighlighterImport {
    public const string Guid="cf019139-d41c-4eb0-a1d0-5cf54b0a42f3";
    const int Padding=4;
    public sealed class Face {public int[] indices;public int copies;public Vector2[] uv;public Color[] colors;public string key;}
    static string Key(params float[] v)=>string.Join(",",v.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
    static string Appearance(Vector2 uv,Color c)=>Key(uv.x,uv.y,c.r,c.g,c.b,c.a);
    public static Face[] Faces(Mesh mesh) {
        if(mesh.subMeshCount!=1 || mesh.GetTopology(0)!=MeshTopology.Triangles)throw new InvalidOperationException("Expected Highlighter triangle mesh");
        var v=mesh.vertices;var uv=mesh.uv;var c=mesh.colors;
        if(uv.Length!=v.Length || c.Length!=v.Length)throw new InvalidOperationException("Missing Highlighter UV/RGBA");
        var vertex=Enumerable.Range(0,v.Length).Select(i=>Key(v[i].x,v[i].y,v[i].z)+":"+Appearance(uv[i],c[i])).ToArray();
        var unique=new Dictionary<string,Face>();var indices=mesh.triangles;
        for(int i=0;i<indices.Length;i+=3) {
            // Sort by appearance first, so the same chart can serve translated faces.
            var t=new[]{indices[i],indices[i+1],indices[i+2]}.OrderBy(j=>Appearance(uv[j],c[j]),StringComparer.Ordinal).ThenBy(j=>vertex[j],StringComparer.Ordinal).ToArray();
            string key=string.Join(";",t.Select(j=>vertex[j]).OrderBy(k=>k,StringComparer.Ordinal));
            if(unique.TryGetValue(key,out var f)){f.copies++;continue;}
            unique.Add(key,new Face{indices=t,copies=1,uv=t.Select(j=>uv[j]).ToArray(),colors=t.Select(j=>c[j]).ToArray()});
        }
        foreach(var f in unique.Values)f.key=f.copies+":"+string.Join(";",Enumerable.Range(0,3).Select(i=>Appearance(f.uv[i],f.colors[i])));
        return unique.Values.ToArray();
    }
    public static void Sample(Texture2D texture,Face face,float s,float t,Vector2 scale,Vector2 offset,out Color emission,out Color mask) {
        s=Mathf.Max(0,s);t=Mathf.Max(0,t);
        if(s+t>1){s=Mathf.Clamp01((s-t+1)*.5f);t=1-s;}
        var uv=Vector2.Scale(face.uv[0]*(1-s-t)+face.uv[1]*s+face.uv[2]*t,scale)+offset;
        var brush=face.colors[0]*(1-s-t)+face.colors[1]*s+face.colors[2]*t;
        var tex=OpenBrushTextureSampling.SampleRaw(texture,uv.x,uv.y);
        emission=new Color(Mathf.GammaToLinearSpace(Mathf.Clamp01(tex.r*brush.r*face.copies)),
            Mathf.GammaToLinearSpace(Mathf.Clamp01(tex.g*brush.g*face.copies)),
            Mathf.GammaToLinearSpace(Mathf.Clamp01(tex.b*brush.b*face.copies)),1);
        mask=new Color(1,1,1,tex.a*brush.a);
    }
    public static OpenBrushAtlasBudget.Layout Layout(int resolution,int chartCount,int maxTextureSize) {
        if(resolution<16 || chartCount<1 || maxTextureSize<1)throw new ArgumentOutOfRangeException("Invalid Highlighter atlas layout");
        resolution=Mathf.Min(resolution,512); // Bound long/repeated UV charts before allocation.
        int columns=Mathf.CeilToInt(Mathf.Sqrt(chartCount)),rows=Mathf.CeilToInt((float)chartCount/columns);
        while(true) {
            int tile=resolution+Padding*2;
            int width=Mathf.NextPowerOfTwo(columns*tile),height=Mathf.NextPowerOfTwo(rows*tile);
            if(width<=maxTextureSize && height<=maxTextureSize && (long)width*height<=16777216)
                return new OpenBrushAtlasBudget.Layout{TileWidth=resolution,TileHeight=resolution,Width=width,Height=height,TextureWidth=width,TextureHeight=height,FramePixels=(long)width*height};
            if(resolution==16)throw new InvalidOperationException("Highlighter atlas exceeds 16M pixels even at 16 samples per edge; split the sketch. Charts="+chartCount);
            resolution=Mathf.Max(16,resolution/2);
        }
    }
    public static void Prepare(GameObject root) {
        foreach(var group in root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial!=null && r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==Guid).GroupBy(r=>r.sharedMaterial)) {
            var source=group.Key;var texture=source.GetTexture("_MaskTex") as Texture2D;
            if(texture==null || !texture.isReadable)throw new InvalidOperationException("Expected readable Highlighter source mask");
            var inputs=group.Select(r=>new {renderer=r,mesh=r.GetComponent<MeshFilter>().sharedMesh,faces=Faces(r.GetComponent<MeshFilter>().sharedMesh)}).ToArray();
            var charts=inputs.SelectMany(i=>i.faces).GroupBy(f=>f.key).Select(g=>g.First()).ToArray();
            // At least one sample per source texel along either chart edge; also resolve
            // smooth vertex-alpha gradients. Large UV spans use the reported detail fallback.
            float extent=charts.SelectMany(f=>new[]{f.uv[1]-f.uv[0],f.uv[2]-f.uv[0],f.uv[2]-f.uv[1]}).Max(d=>Mathf.Max(Mathf.Abs(d.x*source.mainTextureScale.x)*texture.width,Mathf.Abs(d.y*source.mainTextureScale.y)*texture.height));
            int resolution=Mathf.Max(64,Mathf.CeilToInt(extent)+1);
            var layout=Layout(resolution,charts.Length,SystemInfo.maxTextureSize);
            if(layout.TileWidth!=resolution) {
                string warning=$"Highlighter triangle-chart detail reduced from {resolution} to {layout.TileWidth} samples per edge for {charts.Length} charts, keeping the 512-sample edge and 16M pixel limits. Fine alpha-cutoff edges and repeated texture details may differ.";
                OpenBrushAtlasBudget.Adjustments.Add(warning);Debug.LogWarning(warning);
            }
            resolution=layout.TileWidth;
            int tile=resolution+Padding*2,columns=Mathf.CeilToInt(Mathf.Sqrt(charts.Length));
            int width=layout.Width,height=layout.Height;
            var emissionPixels=new Color[width*height];var maskPixels=new Color[width*height];
            var lookup=new Dictionary<string,int>();
            for(int i=0;i<charts.Length;i++) {
                lookup.Add(charts[i].key,i);int ox=i%columns*tile,oy=i/columns*tile;
                for(int y=0;y<tile;y++)for(int x=0;x<tile;x++) {
                    Sample(texture,charts[i],(float)(x-Padding)/(resolution-1),(float)(y-Padding)/(resolution-1),source.mainTextureScale,source.mainTextureOffset,out var color,out var mask);
                    emissionPixels[(oy+y)*width+ox+x]=color;maskPixels[(oy+y)*width+ox+x]=mask;
                }
            }
            var emission=new Texture2D(width,height,TextureFormat.RGBAFloat,true,true){name="Highlighter_GammaEmission",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var masks=new Texture2D(width,height,TextureFormat.RGBAFloat,true,true){name="Highlighter_InterpolatedMask",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            emission.SetPixels(emissionPixels);emission.Apply(true,false);masks.SetPixels(maskPixels);masks.Apply(true,false);
            var material=new Material(source){name="Highlighter_FragmentAtlas",mainTexture=emission,mainTextureScale=Vector2.one,mainTextureOffset=Vector2.zero};
            material.SetTexture("_MaskTex",masks);material.SetColor("_TintColor",Color.white);material.SetFloat("_UseMask",1);
            foreach(var input in inputs) {
                if(input.renderer.transform.Find("Highlighter Fragment Atlas")!=null)throw new InvalidOperationException("Highlighter already prepared");
                var positions=input.mesh.vertices;var vertices=new List<Vector3>();var uv=new List<Vector2>();
                foreach(var face in input.faces) {
                    int index=lookup[face.key],ox=index%columns*tile+Padding,oy=index/columns*tile+Padding;
                    vertices.AddRange(face.indices.Select(j=>positions[j]));
                    uv.Add(new Vector2((ox+.5f)/width,(oy+.5f)/height));
                    uv.Add(new Vector2((ox+resolution-.5f)/width,(oy+.5f)/height));
                    uv.Add(new Vector2((ox+.5f)/width,(oy+resolution-.5f)/height));
                }
                var mesh=new Mesh{name="Highlighter_UniqueFragments",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.colors=Enumerable.Repeat(Color.white,vertices.Count).ToArray();mesh.triangles=Enumerable.Range(0,vertices.Count).ToArray();mesh.RecalculateBounds();
                var child=new GameObject("Highlighter Fragment Atlas");child.transform.SetParent(input.renderer.transform,false);
                child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;input.renderer.enabled=false;
            }
            Debug.Log($"Highlighter baked {charts.Length} charts, {width}x{height}, {resolution} samples per edge; {inputs.Sum(i=>i.faces.Length)} unique faces");
        }
    }
}


