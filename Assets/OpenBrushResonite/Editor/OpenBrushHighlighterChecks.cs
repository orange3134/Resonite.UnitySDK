using System;
using UnityEngine;
using UnityEditor;
public static class OpenBrushHighlighterChecks {
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(){try {
        var small=OpenBrushHighlighterImport.Layout(64,2,8192);
        Require(small.TileWidth==64 && small.Width==256 && small.Height==128,"Small atlases preserve resolution and original packing");
        var large=OpenBrushHighlighterImport.Layout(64,8192,8192);
        Require(large.TileWidth==32 && large.Width==4096 && large.Height==4096,"Large atlases fit original 16M limit");
        var longUv=OpenBrushHighlighterImport.Layout(2049,1,8192);
        Require(longUv.TileWidth==512 && longUv.Width==1024 && longUv.Height==1024,"Long UV charts cap sampling without increasing memory limits");
        var longCrowded=OpenBrushHighlighterImport.Layout(2049,8192,8192);
        Require(longCrowded.TileWidth==32 && longCrowded.FramePixels<=16777216,"Long UV fallback also respects total atlas budget");
        var limited=OpenBrushHighlighterImport.Layout(64,100,512);
        Require(limited.Width<=512 && limited.Height<=512,"Hardware texture limit respected");
        bool rejected=false;try{OpenBrushHighlighterImport.Layout(64,65536,8192);}catch(InvalidOperationException){rejected=true;}
        Require(rejected,"Too many charts cannot bypass minimum resolution or budget");
        var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.zero,Vector3.right,Vector3.up};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.zero,Vector2.right,Vector2.up};
        var a=new Color(.1f,.2f,.3f,0);var b=new Color(.1f,.2f,.3f,1);
        mesh.colors=new[]{a,b,b,a,b,b};mesh.triangles=new[]{0,1,2,5,4,3};
        var faces=OpenBrushHighlighterImport.Faces(mesh);
        Require(faces.Length==1 && faces[0].copies==2,"Opposite coincident gradient faces coalesce");
        var texture=new Texture2D(2,2,TextureFormat.RGBAFloat,false,true);
        texture.SetPixels(new[]{new Color(1,1,1,.5f),new Color(1,1,1,.5f),new Color(1,1,1,.5f),new Color(1,1,1,.5f)});texture.Apply();
        var face=new OpenBrushHighlighterImport.Face{uv=new[]{Vector2.zero,Vector2.right,Vector2.up},colors=new[]{a,b,b},copies=2};
        OpenBrushHighlighterImport.Sample(texture,face,.1f,.14f,Vector2.one,Vector2.zero,out var emission,out var mask);
        Require(Mathf.Abs(mask.a-.12f)<1e-6,"Texture times interpolated alpha preserves the source cutoff boundary");
        Require(Mathf.Abs(emission.r-Mathf.GammaToLinearSpace(.2f))<1e-7 && emission.a==1,"Emission is full RGB above cutoff, not multiplied by alpha");
        OpenBrushHighlighterImport.Sample(texture,face,0,0,Vector2.one,Vector2.zero,out var zeroEmission,out var zeroMask);
        Require(zeroMask.a==0 && zeroEmission==emission,"Zero alpha belongs only to mask, not a second attenuation");
        var uv=mesh.uv;uv[3]=Vector2.one;mesh.uv=uv;
        Require(OpenBrushHighlighterImport.Faces(mesh).Length==2,"Different UV faces never coalesce");
        var c=mesh.colors;c[1]=new Color(.5f,.1f,.2f,1);mesh.colors=c;
        Require(OpenBrushHighlighterImport.Faces(mesh).Length==2,"Within-triangle color gradients are retained");
        Debug.Log("OPENBRUSH_HIGHLIGHTER_CHECKS_PASSED");EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
