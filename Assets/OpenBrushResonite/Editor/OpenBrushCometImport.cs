// Copyright 2020 The Tilt Brush Authors
// Copyright 2026 Open Brush Resonite converter contributors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// Sampling equations adapted from normal m_Material Comet.shader (Open Brush).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class OpenBrushCometImport {
    public const string Guid="1caa6d7d-f015-3f54-3a4b-8b5354d39f81";
    public const int FrameCount=60, FramesPerSecond=30;
    public static Texture2D LoadGradient() {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        if(!texture.LoadImage(File.ReadAllBytes("Assets/OpenBrushResonite/SourceTextures/CometGradient.png")))throw new InvalidOperationException("Cannot read Comet gradient");
        texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;return texture;
    }
    static Color Sample(Texture2D texture,float u,float v) => OpenBrushTextureSampling.SampleRaw(texture,u,v);
    public static float Intensity(Texture2D pattern,Texture2D gradient,float u,float v,float seconds) {
        float time=-seconds;
        float r=Sample(pattern,u+time,v+time).r;
        float g=Sample(pattern,u+time*1.5f,v).g;
        float b=Sample(pattern,u+time*.5f,v).b;
        float lookup=(r+g+b)/3*(1-u);
        lookup=(lookup*lookup+.125f)*3;
        float falloff=Mathf.Max((.2f-u)*5,0);
        float s=Mathf.Clamp01(lookup+falloff);
        return Sample(gradient,s,s).r;
    }
    public static Color Shade(float intensity,Color brush,int copies) {
        // Comet uses Blend One One, ignoring vertex alpha. Decode the full product,
        // not independent RGB factors; this matters for dark selected colors.
        // Comet is a tube: normally front and back contribute two fragments. Calibrate
        // these two equal contributions before decoding. Different V samples or self-
        // intersections remain approximate; native blending itself is still linear.
        return new Color(Mathf.GammaToLinearSpace(Mathf.Clamp01(intensity*brush.r*copies*2))*.5f,
            Mathf.GammaToLinearSpace(Mathf.Clamp01(intensity*brush.g*copies*2))*.5f,
            Mathf.GammaToLinearSpace(Mathf.Clamp01(intensity*brush.b*copies*2))*.5f,1);
    }
    public static Texture2D[] Bake(Texture2D pattern,Texture2D gradient,Color color,int copies,int width=0,int height=0) {
        if(pattern==null || !pattern.isReadable)throw new InvalidOperationException("Comet requires readable source texture");
        if(width==0)width=pattern.width;if(height==0)height=pattern.height;
        if(width<1 || height<1 || width>pattern.width || height>pattern.height)throw new ArgumentOutOfRangeException("Invalid Comet sample size");
        var frames=new Texture2D[FrameCount];
        for(int f=0;f<frames.Length;f++) {
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                pixels[y*width+x]=Shade(Intensity(pattern,gradient,(x+.5f)/width,(y+.5f)/height,f/(float)FramesPerSecond),color,copies);
            var frame=new Texture2D(width,height,TextureFormat.RGBAFloat,true,true){name="Comet_"+color+"_x"+copies+"_"+f,
                wrapModeU=TextureWrapMode.Clamp,wrapModeV=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
            frame.SetPixels(pixels);frame.Apply(true,false);frames[f]=frame;
        }
        return frames;
    }
    public static void Prepare(GameObject root) {
        var renderers=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial!=null && r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==Guid).ToArray();
        if(renderers.Length==0)return;
        // Preflight before mutating renderers. Standard Open Brush strokes have a
        // constant RGB. Arbitrary imported triangle gradients need a chart bake.
        var inputs=renderers.Select(r=>new{renderer=r,mesh=r.GetComponent<MeshFilter>().sharedMesh,batches=OpenBrushAdditiveImport.Partition(r.GetComponent<MeshFilter>().sharedMesh)}).ToArray();
        var cache=new Dictionary<(Material,Color,int),Material>();
        foreach(var input in inputs) {
            if(input.renderer.transform.Find("Comet Flow")!=null)throw new InvalidOperationException("Comet already prepared");
            var material=input.renderer.sharedMaterial;var texture=material.mainTexture as Texture2D;
            if(texture==null || !texture.isReadable || material.mainTextureScale!=Vector2.one || material.mainTextureOffset!=Vector2.zero)
                throw new InvalidOperationException("Comet requires its normal material UV transform and readable pattern");
            foreach(var batch in input.batches) {
                var key=(material,batch.color,batch.copies);
                if(!cache.ContainsKey(key))cache.Add(key,null);
            }
        }
        var layouts=new Dictionary<(Material,Color,int),OpenBrushAtlasBudget.Layout>();
        var recorded=new HashSet<(int,int,int,int)>();
        foreach(var key in cache.Keys) {
            var texture=key.Item1.mainTexture;
            var layout=OpenBrushAtlasBudget.Select(texture.width,texture.height,0,1,1,checked(FrameCount*cache.Count),1,SystemInfo.maxTextureSize,true,33554432);
            layouts.Add(key,layout);
            if(recorded.Add((texture.width,texture.height,layout.TileWidth,layout.TileHeight)))
                OpenBrushAtlasBudget.Record("Comet",layout,texture.width,texture.height,FrameCount);
        }
        var gradient=LoadGradient();cache.Clear();
        foreach(var input in inputs) {
            var group=new GameObject("Comet Flow");group.transform.SetParent(input.renderer.transform,false);
            foreach(var batch in input.batches) {
                var source=input.renderer.sharedMaterial;var key=(source,batch.color,batch.copies);
                if(!cache.TryGetValue(key,out var material)) {
                    var layout=layouts[key];
                    var frames=Bake(source.mainTexture as Texture2D,gradient,batch.color,batch.copies,layout.TileWidth,layout.TileHeight);
                    material=new Material(source){name="Comet_GammaFlow_"+batch.color,mainTexture=frames[0]};material.SetColor("_TintColor",Color.white);
                    var driver=new GameObject("Comet - Three Channel Flow");driver.transform.SetParent(root.transform,false);
                    var motion=driver.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=material;motion.Frames=frames;motion.FramesPerSecond=FramesPerSecond;
                    cache.Add(key,material);
                }
                var mesh=UnityEngine.Object.Instantiate(input.mesh);mesh.name=input.mesh.name+"_CometEmission";
                mesh.triangles=batch.indices;mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();mesh.RecalculateBounds();
                var child=new GameObject("Comet Emission");child.transform.SetParent(group.transform,false);
                child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            input.renderer.enabled=false;
        }
        UnityEngine.Object.DestroyImmediate(gradient);
        Debug.Log("Comet: "+cache.Count+" color sequences, "+FrameCount+" frames / 2 second seamless loop");
    }
}



