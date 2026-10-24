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
// Normal Fire.shader and Brush.cginc bloomColor equations, adapted for native
// linear additive rendering without unbounded post-process bloom.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OpenBrushFireImport {
    public const string Guid="cb92b597-94ca-4255-b017-0e3f42f12f9e";
    public const string BakedTag="OpenBrushFireEmission";
    public static Color BloomColor(Color brush) {
        float floor=new Vector3(brush.r,brush.g,brush.b).magnitude*.05f;
        float gain=2*Mathf.Exp(.5f*10);
        return new Color(Mathf.Pow(Mathf.Max(brush.r,floor),2.2f)*gain,
            Mathf.Pow(Mathf.Max(brush.g,floor),2.2f)*gain,
            Mathf.Pow(Mathf.Max(brush.b,floor),2.2f)*gain,Mathf.Pow(brush.a,2.2f));
    }
    public static Color SourceEmission(Color texture,Color bloom,int copies) {
        float alpha=texture.a*bloom.a*copies;
        return new Color(texture.r*bloom.r*alpha,texture.g*bloom.g*alpha,texture.b*bloom.b*alpha,1);
    }
    public static Color DisplayEmission(Color source) => new Color(
        Mathf.GammaToLinearSpace(Mathf.Clamp01(source.r)),Mathf.GammaToLinearSpace(Mathf.Clamp01(source.g)),
        Mathf.GammaToLinearSpace(Mathf.Clamp01(source.b)),1);
    public static Texture2D[] Bake(Texture2D texture,Color brush,int copies) {
        var bloom=BloomColor(brush);var frames=new Texture2D[30];
        for(int frame=0;frame<frames.Length;frame++) {
            var pixels=new Color[texture.width*texture.height];
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++) {
                var tex=OpenBrushProceduralImport.FireSample(texture,(x+.5f)/texture.width,(y+.5f)/texture.height,frame/30f);
                pixels[y*texture.width+x]=DisplayEmission(SourceEmission(tex,bloom,copies));
            }
            var result=new Texture2D(texture.width,texture.height,TextureFormat.RGBAFloat,true,true){name="FireEmission_"+brush+"_x"+copies+"_"+frame,
                filterMode=FilterMode.Bilinear,wrapModeU=texture.wrapModeU,wrapModeV=texture.wrapModeV};
            result.SetPixels(pixels);result.Apply(true,false);frames[frame]=result;
        }
        return frames;
    }
    public static void Prepare(GameObject root) {
        var renderers=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled && r.sharedMaterial!=null && r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==Guid && r.sharedMaterial.GetTag(BakedTag,false,"")!="1").ToArray();
        if(renderers.Length==0)return;
        // Standard Fire strokes have constant RGBA. Do not flatten arbitrary imported
        // vertex gradients: those need bloom evaluated before interpolation and a chart.
        var inputs=renderers.Select(r=>new{renderer=r,mesh=r.GetComponent<MeshFilter>().sharedMesh,batches=OpenBrushAdditiveImport.Partition(r.GetComponent<MeshFilter>().sharedMesh)}).ToArray();
        var cache=new Dictionary<(Material,Color,int),Material>();long pixels=0;
        foreach(var input in inputs) {
            if(input.renderer.transform.Find("Fire Color Emission")!=null)throw new InvalidOperationException("Fire emission already exists");
            var source=input.renderer.sharedMaterial;var texture=source.mainTexture as Texture2D;
            if(texture==null || !texture.isReadable)throw new InvalidOperationException("Fire needs its readable source pattern");
            foreach(var batch in input.batches) {
                var key=(source,batch.color,batch.copies);
                if(cache.ContainsKey(key))continue;cache.Add(key,null);pixels+=(long)texture.width*texture.height*30;
            }
        }
        if(pixels>33554432)throw new InvalidOperationException("Fire color motion exceeds 32M frame pixels; split the sketch");
        cache.Clear();
        var sources=inputs.Select(i=>i.renderer.sharedMaterial).Distinct().ToArray();
        // Replace only our obsolete raw-texture animation. A second driver on the
        // same material would overwrite these color-aware frames during conversion.
        foreach(var motion in root.GetComponentsInChildren<OpenBrushTextureMotion>(true).Where(m=>sources.Contains(m.SourceMaterial)).ToArray())
            UnityEngine.Object.DestroyImmediate(motion);
        foreach(var input in inputs) {
            var group=new GameObject("Fire Color Emission");group.transform.SetParent(input.renderer.transform,false);
            foreach(var batch in input.batches) {
                var source=input.renderer.sharedMaterial;var key=(source,batch.color,batch.copies);
                if(!cache.TryGetValue(key,out var material)) {
                    var frames=Bake(source.mainTexture as Texture2D,batch.color,batch.copies);
                    material=new Material(source){name="Fire_BloomEmission_"+batch.color,mainTexture=frames[0]};
                    material.SetOverrideTag(BakedTag,"1");material.SetColor("_TintColor",Color.white);material.SetFloat("_Gain",1);
                    var driver=new GameObject("Fire - Color Aware Flow");driver.transform.SetParent(root.transform,false);
                    var motion=driver.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=material;motion.Frames=frames;
                    motion.FramesPerSecond=30;motion.TextureScale=source.mainTextureScale;cache.Add(key,material);
                }
                var mesh=UnityEngine.Object.Instantiate(input.mesh);mesh.name=input.mesh.name+"_FireUniqueFaces";
                mesh.triangles=batch.indices;mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();mesh.RecalculateBounds();
                var child=new GameObject("Fire Emission");child.transform.SetParent(group.transform,false);
                child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            input.renderer.enabled=false;
        }
        Debug.Log("Fire emission: "+cache.Count+" RGBA/copy sequences, 30 frames / second; source bloom and alpha preserved in display range");
    }
}

