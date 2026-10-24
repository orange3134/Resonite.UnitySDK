// NeonPulse.shadergraph / AudioReactive.hlsl equations from Open Brush Unity Tools.
// Copyright The Tilt Brush Authors. Apache-2.0 (see SourceTextures/LICENSE).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OpenBrushNeonImport {
    public const string Guid="b2ffef01-eaaa-4ab5-aa64-95a2c4f5dbc6";
    // Once shader time exceeds stroke UVs the original abs(u-.75*t) is positive.
    // This periodic form preserves that steady-state direction, speed and pulse shape.
    public static float Pulse(float u,float seconds) => Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(.2f-Mathf.Repeat(.75f*seconds-u,1))*10,5));
    public static Texture2D[] Bake(Color color) {
        var frames=new Texture2D[40];var bloom=OpenBrushFireImport.BloomColor(color);
        for(int f=0;f<frames.Length;f++) {
            var pixels=new Color[2048];
            for(int x=0;x<pixels.Length;x++) {
                float p=Pulse((x+.5f)/pixels.Length,f/30f);
                pixels[x]=OpenBrushFireImport.DisplayEmission(new Color(bloom.r*p,bloom.g*p,bloom.b*p,1));
            }
            var texture=new Texture2D(pixels.Length,1,TextureFormat.RGBAFloat,true,true){name="NeonPulse_"+color+"_"+f,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
            texture.SetPixels(pixels);texture.Apply(true,false);frames[f]=texture;
        }
        return frames;
    }
    public static void Prepare(GameObject root) {
        var inputs=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled && r.sharedMaterial!=null && r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==Guid && r.sharedMaterial.GetTag("OpenBrushNeonPrepared",false,"")!="1")
            .Select(r=>new{renderer=r,mesh=r.GetComponent<MeshFilter>().sharedMesh,batches=OpenBrushAdditiveImport.Partition(r.GetComponent<MeshFilter>().sharedMesh)}).ToArray();
        var cache=new Dictionary<(Material,Color),Material>();var bodies=new Dictionary<Material,Material>();
        foreach(var input in inputs) {
            var source=input.renderer.sharedMaterial;
            if(!bodies.TryGetValue(source,out var body)) {
                body=new Material(source){name="NeonPulse - Dark Gloss Body",mainTexture=null};body.SetColor("_TintColor",Color.black);
                body.SetOverrideTag("OpenBrushNeonPrepared","1");bodies.Add(source,body);
            }
            foreach(var batch in input.batches) {
                var key=(source,batch.color);
                if(!cache.TryGetValue(key,out var material)) {
                    var frames=Bake(batch.color);material=new Material(source){name="NeonPulse - View Dependent Emission",mainTexture=frames[0]};
                    material.SetColor("_TintColor",Color.white);material.SetOverrideTag("OpenBrushNeonPrepared","1");material.SetOverrideTag("OpenBrushNeonEmission","1");
                    var driver=new GameObject("NeonPulse - Traveling Light");driver.transform.SetParent(root.transform,false);
                    var motion=driver.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=material;motion.Frames=frames;motion.FramesPerSecond=30;motion.TextureScale=Vector2.one;cache.Add(key,material);
                }
                var mesh=UnityEngine.Object.Instantiate(input.mesh);mesh.name=input.mesh.name+"_NeonEmission";
                mesh.triangles=batch.indices;mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();mesh.RecalculateBounds();
                var child=new GameObject("NeonPulse Emission");child.transform.SetParent(input.renderer.transform,false);
                child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            input.renderer.sharedMaterial=body;
        }
        if(inputs.Length>0)Debug.Log("NeonPulse: "+cache.Count+" color sequences, 40 frames at 30fps; native view-dependent emission over gloss body");
    }
}
