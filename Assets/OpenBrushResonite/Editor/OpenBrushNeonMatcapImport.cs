// NeonPulse view/pulse bake experiment. Source equations: Open Brush Unity Tools,
// Apache-2.0, see SourceTextures/LICENSE. Native Matcap uses camera-forward normals;
// unlike source N dot V it is not perspective corrected. Keep this opt-in until
// comparisons establish whether that tradeoff is acceptable for normal viewing.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OpenBrushNeonMatcapImport {
    public const int Resolution=128, PhaseBins=512, Frames=80;
    const int MaxPixels=33554432, MaxTriangles=100000;
    struct Vertex {public Vector3 p,n;public Vector2 uv;}
    sealed class Part {public Color color;public int phase;public List<Vertex> vertices=new List<Vertex>();}
    public static int PulseKey(float p) => p<.000001f ? -1 : Mathf.RoundToInt(-Mathf.Log(Mathf.Clamp01(p),2)*32);
    public static float PulseValue(int key) => key<0 ? 0 : Mathf.Pow(2,-key/32f);
    public static Color Shade(Color color,float pulse,float normalDot) {
        var bloom=OpenBrushFireImport.BloomColor(color);
        float gain=pulse*Mathf.Pow(Mathf.Clamp01(normalDot),5);
        return OpenBrushFireImport.DisplayEmission(new Color(bloom.r*gain,bloom.g*gain,bloom.b*gain,1));
    }
    public static Texture2D Bake(Color color,int pulseKey) {
        var pixels=new Color[Resolution*Resolution];float pulse=PulseValue(pulseKey);
        for(int y=0;y<Resolution;y++)for(int x=0;x<Resolution;x++) {
            float u=2*(x+.5f)/Resolution-1,v=2*(y+.5f)/Resolution-1;
            pixels[y*Resolution+x]=Shade(color,pulse,Mathf.Sqrt(Mathf.Max(0,1-u*u-v*v)));
        }
        var texture=new Texture2D(Resolution,Resolution,TextureFormat.RGBAFloat,true,true) {
            name="NeonView_"+color+"_"+pulseKey,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        texture.SetPixels(pixels);texture.Apply(true,false);return texture;
    }
    static Vertex Mid(Vertex a,Vertex b) => new Vertex{p=(a.p+b.p)*.5f,n=(a.n+b.n).normalized,uv=(a.uv+b.uv)*.5f};
    static void AddTriangle(Vertex a,Vertex b,Vertex c,Color color,Dictionary<(Color,int),Part> parts,ref int count) {
        float ab=Mathf.Abs(a.uv.x-b.uv.x),bc=Mathf.Abs(b.uv.x-c.uv.x),ca=Mathf.Abs(c.uv.x-a.uv.x);
        if(Mathf.Max(ab,Mathf.Max(bc,ca))>1f/PhaseBins) {
            if(ab>=bc && ab>=ca){var mid=Mid(a,b);AddTriangle(a,mid,c,color,parts,ref count);AddTriangle(mid,b,c,color,parts,ref count);}
            else if(bc>=ca){var mid=Mid(b,c);AddTriangle(a,b,mid,color,parts,ref count);AddTriangle(a,mid,c,color,parts,ref count);}
            else {var mid=Mid(c,a);AddTriangle(a,b,mid,color,parts,ref count);AddTriangle(mid,b,c,color,parts,ref count);}
            return;
        }
        if(++count>MaxTriangles)throw new InvalidOperationException("NeonPulse view bake exceeds 100K triangles; split the sketch");
        int phase=Mathf.RoundToInt(Mathf.Repeat((a.uv.x+b.uv.x+c.uv.x)/3,1)*PhaseBins)%PhaseBins;
        var key=(color,phase);if(!parts.TryGetValue(key,out var part)){part=new Part{color=color,phase=phase};parts.Add(key,part);}
        part.vertices.Add(a);part.vertices.Add(b);part.vertices.Add(c);
    }
    static Part[] Partition(Mesh mesh,ref int count) {
        var pos=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
        if(normals.Length!=pos.Length)throw new InvalidOperationException("NeonPulse needs source normals");
        if(uv.Any(v=>float.IsNaN(v.x)||float.IsInfinity(v.x)))throw new InvalidOperationException("Invalid NeonPulse UV");
        var parts=new Dictionary<(Color,int),Part>();
        foreach(var batch in OpenBrushAdditiveImport.Partition(mesh)) {
            Vertex V(int i)=>new Vertex{p=pos[i],n=normals[i],uv=uv[i]};
            for(int i=0;i<batch.indices.Length;i+=3)AddTriangle(V(batch.indices[i]),V(batch.indices[i+1]),V(batch.indices[i+2]),batch.color,parts,ref count);
        }
        return parts.Values.ToArray();
    }
    public static void Prepare(GameObject root) {
        var inputs=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled && r.sharedMaterial!=null &&
            r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushNeonImport.Guid &&
            r.sharedMaterial.GetTag("OpenBrushNeonPrepared",false,"")!="1").ToArray();
        int triangleCount=0;var partitions=new Dictionary<MeshRenderer,Part[]>();
        foreach(var input in inputs)partitions.Add(input,Partition(input.GetComponent<MeshFilter>().sharedMesh,ref triangleCount));
        var textures=new Dictionary<(Color,int),Texture2D>();
        var sequences=new Dictionary<(Material,Color,int),Material>();var bodies=new Dictionary<Material,Material>();
        // Validate resource demand before changing the source hierarchy.
        foreach(var part in partitions.Values.SelectMany(v=>v))for(int f=0;f<Frames;f++)
            textures[(part.color,PulseKey(OpenBrushNeonImport.Pulse(part.phase/(float)PhaseBins,f/60f)))]=null;
        if((long)textures.Count*Resolution*Resolution>MaxPixels)throw new InvalidOperationException("NeonPulse view bake exceeds 32M pixels; split the sketch");
        foreach(var key in textures.Keys.ToArray())textures[key]=Bake(key.Item1,key.Item2);
        foreach(var input in inputs) {
            var source=input.sharedMaterial;
            if(!bodies.TryGetValue(source,out var body)) {
                body=new Material(source){name="NeonPulse - Dark Gloss Body",mainTexture=null};
                body.SetColor("_TintColor",Color.black);body.SetOverrideTag("OpenBrushNeonPrepared","1");
                body.SetOverrideTag("OpenBrushNeonViewBody","1");bodies.Add(source,body);
            }
            foreach(var part in partitions[input]) {
                var key=(source,part.color,part.phase);
                if(!sequences.TryGetValue(key,out var material)) {
                    var frames=Enumerable.Range(0,Frames).Select(f=>textures[(part.color,PulseKey(OpenBrushNeonImport.Pulse(part.phase/(float)PhaseBins,f/60f)))]).ToArray();
                    material=new Material(source){name="NeonPulse View Phase "+part.phase,mainTexture=frames[0]};
                    material.SetColor("_TintColor",Color.white);material.SetOverrideTag("OpenBrushNeonPrepared","1");material.SetOverrideTag("OpenBrushViewEmission","1");
                    var driver=new GameObject("NeonPulse View Pulse "+part.phase);driver.transform.SetParent(root.transform,false);
                    var motion=driver.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=material;motion.Frames=frames;motion.FramesPerSecond=60;motion.TextureScale=Vector2.one;
                    sequences.Add(key,material);
                }
                var mesh=new Mesh{name="NeonViewPhase"+part.phase,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                mesh.vertices=part.vertices.Select(v=>v.p).ToArray();mesh.normals=part.vertices.Select(v=>v.n).ToArray();
                mesh.uv=part.vertices.Select(v=>v.uv).ToArray();mesh.colors=Enumerable.Repeat(Color.white,part.vertices.Count).ToArray();
                mesh.triangles=Enumerable.Range(0,part.vertices.Count).ToArray();mesh.RecalculateBounds();
                var child=new GameObject("Neon View Emission "+part.phase);child.transform.SetParent(input.transform,false);
                child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            input.sharedMaterial=body;
        }
        if(inputs.Length>0)Debug.Log("NEON_VIEW_BAKE textures="+textures.Count+" phases="+sequences.Count+" triangles="+triangleCount+"; camera-forward approximation");
    }
}
