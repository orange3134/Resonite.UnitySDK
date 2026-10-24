using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// Source Gamma rendering sums duplicate front/back fragments before display.
// Coalesce only EXACT coincident triangles (including UV and RGBA), and bake
// their combined emission before gamma decoding. Arbitrary overlaps still use
// native linear additive blending; this is not a replacement for custom shaders.
public static class OpenBrushAdditiveImport {
    public const string Soft="accb32f5-4509-454f-93f8-1df3fd31df1b",Velvet="d229d335-c334-495a-a801-660ac8a87360";
    public static bool Supports(string guid)=>guid==Soft || guid==Velvet;
    public static Color Shade(Color texture,Color brush,int copies) {
        float coverage=texture.a*brush.a*copies;
        // Bound the source display range to avoid native HDR bloom washing out the brush.
        return new Color(Mathf.GammaToLinearSpace(Mathf.Clamp01(texture.r*brush.r*coverage)),
            Mathf.GammaToLinearSpace(Mathf.Clamp01(texture.g*brush.g*coverage)),
            Mathf.GammaToLinearSpace(Mathf.Clamp01(texture.b*brush.b*coverage)),1);
    }
    public sealed class Batch {public Color color;public int copies;public int[] indices;}
    static string Key(params float[] values)=>string.Join(",",values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));
    public static Batch[] Partition(Mesh mesh) {
        if(mesh.subMeshCount!=1 || mesh.GetTopology(0)!=MeshTopology.Triangles)throw new InvalidOperationException("Expected additive triangle mesh");
        var pos=mesh.vertices;var uv=mesh.uv;var colors=mesh.colors;
        if(uv.Length!=pos.Length || colors.Length!=pos.Length)throw new InvalidOperationException("Missing additive UV/color");
        var keys=Enumerable.Range(0,pos.Length).Select(i=>Key(pos[i].x,pos[i].y,pos[i].z,uv[i].x,uv[i].y,colors[i].r,colors[i].g,colors[i].b,colors[i].a)).ToArray();
        var unique=new Dictionary<string,List<int[]>>();var triangles=mesh.triangles;
        for(int i=0;i<triangles.Length;i+=3) {
            var t=new[]{triangles[i],triangles[i+1],triangles[i+2]};
            if(!colors[t[0]].Equals(colors[t[1]]) || !colors[t[0]].Equals(colors[t[2]]))
                throw new InvalidOperationException("Soft additive conversion currently requires uniform RGBA per triangle; gradient strokes need a fragment-color bake.");
            string key=string.Join(";",t.Select(v=>keys[v]).OrderBy(v=>v,StringComparer.Ordinal));
            if(!unique.TryGetValue(key,out var list)){list=new List<int[]>();unique.Add(key,list);}list.Add(t);
        }
        return unique.Values.GroupBy(v=>(color:colors[v[0][0]],copies:v.Count)).Select(g=>new Batch{color=g.Key.color,copies=g.Key.copies,indices=g.SelectMany(v=>v[0]).ToArray()}).ToArray();
    }
    public static Texture2D Bake(Texture2D source,Color color,int copies) {
        if(source==null || !source.isReadable)throw new InvalidOperationException("Expected readable soft brush texture");
        var pixels=source.GetPixels();for(int i=0;i<pixels.Length;i++)pixels[i]=Shade(pixels[i],color,copies);
        var output=new Texture2D(source.width,source.height,TextureFormat.RGBAFloat,true,true){name="SoftEmission_"+color+"_x"+copies,
            wrapModeU=source.wrapModeU,wrapModeV=source.wrapModeV,filterMode=source.filterMode};
        output.SetPixels(pixels);output.Apply(true,false);return output;
    }
    public static void Prepare(GameObject root) {
        var cache=new Dictionary<(Material,Color,int),Material>(); long pixels=0;
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
            var source=renderer.sharedMaterial;if(source==null || !Supports(source.GetTag("OpenBrushGuid",false,"")))continue;
            var filter=renderer.GetComponent<MeshFilter>();if(filter==null)throw new InvalidOperationException("Missing soft brush mesh");
            if(renderer.transform.Find("Gamma Additive Fragments")!=null)throw new InvalidOperationException("Soft brush already prepared");
            var batches=Partition(filter.sharedMesh);
            var group=new GameObject("Gamma Additive Fragments");group.transform.SetParent(renderer.transform,false);
            foreach(var batch in batches) {
                var key=(source,batch.color,batch.copies);
                if(!cache.TryGetValue(key,out var material)) {
                    var texture=source.mainTexture as Texture2D; if(texture==null)throw new InvalidOperationException("Missing soft texture");
                    pixels+=(long)texture.width*texture.height; if(pixels>16777216)throw new InvalidOperationException("Soft color bake exceeds 16M pixels; split the sketch.");
                    material=new Material(source);material.name=source.name+"_GammaFragments";
                    material.mainTexture=Bake(source.mainTexture as Texture2D,batch.color,batch.copies);
                    material.SetColor("_TintColor",Color.white);cache.Add(key,material);
                }
                var child=new GameObject("Emission "+batch.color);child.transform.SetParent(group.transform,false);
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=filter.sharedMesh.name+"_UniqueEmissionFaces";
                mesh.triangles=batch.indices;mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();mesh.RecalculateBounds();
                child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            renderer.enabled=false;
        }
    }
}
