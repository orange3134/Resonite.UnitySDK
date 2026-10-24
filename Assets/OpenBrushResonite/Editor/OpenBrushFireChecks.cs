using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class OpenBrushFireChecks {
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(){try {
        var material=new Material(Shader.Find("Hidden/OpenBrushFireColorReference"));
        var rt=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
        var read=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);float worst=0;
        for(int i=0;i<40;i++) {
            var brush=new Color(Mathf.Repeat(i*.37f,1),Mathf.Repeat(i*.71f,1),Mathf.Repeat(i*.13f,1),(i%8)/7f);
            var tex=new Color(.02f+i*.013f,.2f,.63f,.4f);int copies=1+i%3;
            material.SetVector("_Brush",brush);material.SetVector("_Sample",tex);material.SetFloat("_Copies",copies);
            for(int mode=0;mode<2;mode++) {
                material.SetFloat("_Mode",mode);Graphics.Blit(Texture2D.whiteTexture,rt,material);
                RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,1,1),0,0);read.Apply();RenderTexture.active=null;
                var cpu=OpenBrushFireImport.BloomColor(brush);if(mode==1)cpu=OpenBrushFireImport.SourceEmission(tex,cpu,copies);
                var gpu=read.GetPixel(0,0);float error=Mathf.Max(Mathf.Abs(cpu.r-gpu.r),Mathf.Abs(cpu.g-gpu.g),Mathf.Abs(cpu.b-gpu.b),Mathf.Abs(cpu.a-gpu.a));worst=Mathf.Max(worst,error);
                Require(error<.001f,"GPU source bloom/alpha mismatch "+error);
            }
        }
        var saturated=OpenBrushFireImport.BloomColor(Color.red);Require(saturated.g>0 && saturated.b>0,"Saturated source colors retain channels for white highlights");
        var transparent=OpenBrushFireImport.SourceEmission(Color.white,OpenBrushFireImport.BloomColor(new Color(.8f,.1f,.2f,0)),2);Require(transparent.r==0 && transparent.g==0 && transparent.b==0,"Source vertex alpha controls emission");
        var bounded=OpenBrushFireImport.DisplayEmission(new Color(300,4,.3f,1));Require(bounded.r==1 && bounded.g==1 && bounded.b<.1f,"Display conversion bounds bloom and retains dark values");
        var root=new GameObject("FireCheck");var child=new GameObject("FireSource");child.transform.SetParent(root.transform,false);
        var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.zero,Vector3.right,Vector3.up};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.zero,Vector2.right,Vector2.up};mesh.colors=Enumerable.Repeat(new Color(.12f,.06f,.18f,1),6).ToArray();mesh.triangles=new[]{0,1,2,5,4,3};
        child.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.AddComponent<MeshRenderer>();
        var source=new Material(Shader.Find("OpenBrush/ExportedBrush"));source.SetOverrideTag("OpenBrushGuid",OpenBrushFireImport.Guid);
        var texture=new Texture2D(4,4,TextureFormat.RGBA32,false,true);texture.SetPixels(Enumerable.Repeat(new Color(.1f,.1f,.1f,1),16).ToArray());texture.Apply();source.mainTexture=texture;renderer.sharedMaterial=source;
        var old=new GameObject("Raw Fire Motion");old.transform.SetParent(root.transform,false);old.AddComponent<OpenBrushTextureMotion>().SourceMaterial=source;
        OpenBrushFireImport.Prepare(root);OpenBrushFireImport.Prepare(root);
        var motion=root.GetComponentsInChildren<OpenBrushTextureMotion>();Require(motion.Length==1 && motion[0].Frames.Length==30,"Color motion replaces the raw driver once");
        Require(!renderer.enabled && mesh.triangles.Length==6,"Original mesh preserved and renderer hidden");
        var generated=child.GetComponentsInChildren<MeshFilter>().Single(f=>f.sharedMesh!=mesh);Require(generated.sharedMesh.triangles.Length==3 && generated.sharedMesh.colors.All(c=>c==Color.white),"Exact opposite faces coalesce and are not tinted twice");
        var frame=motion[0].Frames[0];Require(frame.format==TextureFormat.RGBAFloat && !frame.isDataSRGB && frame.mipmapCount>1,"Linear float emission keeps mipmaps");
        var native=new FrooxEngine.UnlitMaterial();OpenBrushMaterialConverter.ConfigureUnlit(motion[0].SourceMaterial,OpenBrushProfiles.Find(OpenBrushFireImport.Guid),native);
        Require(native.TintColor.color.r==1,"No legacy gain on baked emission");
        Debug.Log("OPENBRUSH_FIRE_CHECKS_PASSED maxGpuError="+worst);EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
