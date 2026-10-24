using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class OpenBrushAdditiveChecks {
    static void Require(bool v,string m){if(!v)throw new Exception(m);}
    public static void Run(){try {
        var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.zero,Vector3.right,Vector3.up};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.zero,Vector2.right,Vector2.up};
        var paint=new Color(.12f,.06f,.18f,1);mesh.colors=Enumerable.Repeat(paint,6).ToArray();mesh.triangles=new[]{0,1,2,5,4,3};
        var batches=OpenBrushAdditiveImport.Partition(mesh);Require(batches.Length==1 && batches[0].copies==2 && batches[0].indices.Length==3,"Coalesce identical front/back triangles");
        var tex=new Color(.5f,.5f,.5f,.25f);var actual=OpenBrushAdditiveImport.Shade(tex,paint,2);
        Require(Mathf.Abs(actual.r-Mathf.GammaToLinearSpace(.03f))<1e-7,"Alpha and duplicates combine BEFORE decoding");
        Require(actual.a==1,"Native alpha must not attenuate baked emission twice");
        var uv=mesh.uv;uv[3]=Vector2.one;mesh.uv=uv;
        Require(OpenBrushAdditiveImport.Partition(mesh).Length==1 && OpenBrushAdditiveImport.Partition(mesh)[0].copies==1,"Different UVs must not coalesce");
        var colors=mesh.colors;colors[1]=Color.red;mesh.colors=colors;bool rejected=false;
        try {OpenBrushAdditiveImport.Partition(mesh);}catch(InvalidOperationException){rejected=true;}
        Require(rejected,"Unsupported gradients must not silently lose colors");
        var source=new Texture2D(2,2,TextureFormat.RGBAFloat,false,true);source.SetPixels(Enumerable.Repeat(new Color(1,1,1,.02f),4).ToArray());source.Apply();
        var result=OpenBrushAdditiveImport.Bake(source,paint,2);Require(result.GetPixel(0,0).r>0 && result.GetPixel(0,0).r<.001,"Faint colored edges survive quantization");
        Debug.Log("OPENBRUSH_ADDITIVE_CHECKS_PASSED");EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
