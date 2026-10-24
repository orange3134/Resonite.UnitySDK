using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class OpenBrushCometChecks {
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(){try {
        var pattern=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        pattern.LoadImage(File.ReadAllBytes("Assets/OpenBrushResonite/SourceTextures/CometPattern.png"));pattern.wrapMode=TextureWrapMode.Repeat;
        var gradient=OpenBrushCometImport.LoadGradient();
        var shader=Shader.Find("Hidden/OpenBrushCometReference");Require(shader!=null && shader.isSupported,"GPU reference shader");
        var mat=new Material(shader);mat.SetTexture("_Gradient",gradient);
        var rt=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
        var read=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);float maxError=0,variation=0;
        for(int i=0;i<50;i++) {
            float u=(i+.2f)/50,v=Mathf.Repeat(i*.173f,1),t=i*.057f;
            mat.SetVector("_UVTime",new Vector4(u,v,t,0));Graphics.Blit(pattern,rt,mat);
            RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,1,1),0,0);read.Apply();RenderTexture.active=null;
            float cpu=OpenBrushCometImport.Intensity(pattern,gradient,u,v,t);
            float gpu=read.GetPixel(0,0).r; maxError=Mathf.Max(maxError,Mathf.Abs(cpu-gpu));
            if(Mathf.Abs(cpu-gpu)>.01f) {
                mat.SetVector("_UVTime",new Vector4(u,v,t,1));Graphics.Blit(pattern,rt,mat);RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,1,1),0,0);read.Apply();RenderTexture.active=null;
                Debug.Log($"COMET_DIAG u={u} v={v} t={t} cpu={cpu} gpu={gpu} patternGPU={read.GetPixel(0,0)} patternCPU={pattern.GetPixelBilinear(u-t,v-t).r},{pattern.GetPixelBilinear(u-t*1.5f,v).g},{pattern.GetPixelBilinear(u-t*.5f,v).b} profile={pattern.isDataSRGB} filter={pattern.filterMode}");
            }
            Require(Mathf.Abs(cpu-OpenBrushCometImport.Intensity(pattern,gradient,u,v,t+2))<.002f,"Loop closes at 2s");
            variation+=Mathf.Abs(cpu-OpenBrushCometImport.Intensity(pattern,gradient,u,v,t+.3f));
        }
        Require(maxError<.001f,"CPU versus source GPU fragment error "+maxError);
        Require(variation>.1f,"Three channel flow changes over time");
        var dark=OpenBrushCometImport.Shade(.3f,new Color(.12f,.06f,.18f,0),1);
        Require(Mathf.Abs(dark.r-Mathf.GammaToLinearSpace(.072f)*.5f)<1e-7 && dark.a==1,"Full RGB product decoded; vertex alpha ignored");
        var duplicate=OpenBrushCometImport.Shade(.3f,new Color(.12f,.06f,.18f,1),2);
        Require(Mathf.Abs(duplicate.r-Mathf.GammaToLinearSpace(.144f)*.5f)<1e-7,"Exact duplicate emission sums before gamma decode");
        var layout=OpenBrushAtlasBudget.Select(256,256,0,1,1,60*16,1,4096,true,33554432);
        Require(layout.TileWidth==128 && layout.FramePixels<=33554432,"Many Comet colors fit combined frame pixel budget");
        var frames=OpenBrushCometImport.Bake(pattern,gradient,new Color(.2f,.4f,.6f,1),1,16,16);
        Require(frames.Length==60 && frames[0].width==16 && frames[0].mipmapCount>1,"Adaptive detail keeps all frames and mipmaps");
        var expected=OpenBrushCometImport.Shade(OpenBrushCometImport.Intensity(pattern,gradient,4.5f/16,7.5f/16,17/30f),new Color(.2f,.4f,.6f,1),1);
        var actual=frames[17].GetPixel(4,7);
        Require(Mathf.Abs(actual.r-expected.r)+Mathf.Abs(actual.g-expected.g)+Mathf.Abs(actual.b-expected.b)<1e-6,"Reduced detail samples original time and color");
        foreach(var frame in frames)UnityEngine.Object.DestroyImmediate(frame);
        Debug.Log("OPENBRUSH_COMET_CHECKS_PASSED maxGpuError="+maxError+" variation="+variation);EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}


