using System;
using UnityEngine;
using UnityEditor;
public static class OpenBrushBubbleChecks {
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run(){try{
        var shader=Shader.Find("Hidden/OpenBrushBubbleReference");Require(shader!=null && shader.isSupported,"Reference shader");
        var material=new Material(shader);
        var rt=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
        var read=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        float maxError=0;
        for(int i=0;i<32;i++) {
            var seed=new Vector3(-7.3f+i*.49f,18+Mathf.Sin(i),Mathf.Cos(i)*.25f);float t=i*1.81f;
            material.SetVector("_SeedTime",new Vector4(seed.x,seed.y,seed.z,t));Graphics.Blit(Texture2D.whiteTexture,rt,material);
            RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,1,1),0,0);read.Apply();RenderTexture.active=null;
            var rgba=read.GetPixel(0,0);var gpu=new Vector3(rgba.r,rgba.g,rgba.b);
            float error=Vector3.Distance(gpu,OpenBrushBubbleMath.Displacement(seed,t));maxError=Mathf.Max(maxError,error);
            Require(error<.0002f,"CPU vs source GPU curl at "+t+": "+error);
        }
        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,false);tex.SetPixels(new[]{new Color(.8f,.2f,.1f,.5f),Color.clear,Color.white,Color.clear});tex.Apply();
        var go=new GameObject("BubbleChecks");var motion=go.AddComponent<OpenBrushParticleMotion>();
        motion.Kind=OpenBrushParticleMotion.BrushKind.Bubbles;motion.SourceMaterial=new Material(Shader.Find("Unlit/Texture"));motion.SourceMaterial.mainTexture=tex;
        motion.Particles=new[]{new OpenBrushParticleMotion.Particle{center=new Vector3(.1f,1.8f,0),origin=new Vector3(.1f,1.8f,0),color=Color.black,uv=new Vector4(0,0,1,1),size=.015f,playBirth=1},
            new OpenBrushParticleMotion.Particle{color=Color.red,uv=new Vector4(0,0,1,1),size=.015f}};
        OpenBrushBubbleAppearance.Prepare(motion);
        var black=motion.BakedTexture.GetPixel(2,2);var red=motion.BakedTexture.GetPixel(8,2);
        Require(Mathf.Abs(black.r-black.g)<1e-6 && black.r>.2f,"Black bubbles retain white highlights");
        Require(red.r>1 && red.g==black.g && red.b==black.b,"Red tint does not recolor highlights; HDR retained");
        Require(motion.BakedUV[0].z<motion.BakedUV[1].x,"Distinct colors use separate padded atlas tiles");
        motion.Timeline=go.AddComponent<OpenBrushTimeline>();motion.Timeline.Duration=3;motion.Bake();
        Require(motion.Frames[59].normals[0].x==0 && motion.Frames[60].normals[0].x==.015f,"Growth birth and size preserved");
        Require(motion.Frames[60].vertices[0]==motion.Particles[0].origin,"Displacement spreads from zero at birth");
        Require(motion.Frames[61].vertices[0]!=motion.Frames[160].vertices[0],"Bubbles actually move");
        Require(motion.Frames[160].colors[0]==Color.white,"Atlas color is not tinted twice");
        Debug.Log("OPENBRUSH_BUBBLE_CHECKS_PASSED maxGpuErrorMeters="+maxError);EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
