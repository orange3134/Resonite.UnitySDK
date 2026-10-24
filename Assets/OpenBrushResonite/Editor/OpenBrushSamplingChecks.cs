using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class OpenBrushSamplingChecks {
    static Material reference;static RenderTexture target;static Texture2D read;
    static float Error(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b),Mathf.Abs(a.a-b.a));
    static Texture2D Load(string file,TextureWrapMode u=TextureWrapMode.Repeat,TextureWrapMode v=TextureWrapMode.Repeat) {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        texture.LoadImage(File.ReadAllBytes("Assets/OpenBrushResonite/SourceTextures/"+file));texture.wrapModeU=u;texture.wrapModeV=v;texture.filterMode=FilterMode.Bilinear;return texture;
    }
    static Color GPU(Texture2D texture,float u,float v,float time,int mode) {
        reference.SetVector("_UVTimeMode",new Vector4(u,v,time,mode));Graphics.Blit(texture,target,reference);
        RenderTexture.active=target;read.ReadPixels(new Rect(0,0,1,1),0,0);read.Apply();RenderTexture.active=null;return read.GetPixel(0,0);
    }
    public static void RunBaseline()=>RunChecks(false);
    public static void Run()=>RunChecks(true);
    static void RunChecks(bool enforce) {try {
        var shader=Shader.Find("Hidden/OpenBrushSamplingReference");if(shader==null || !shader.isSupported)throw new Exception("GPU reference missing");
        reference=new Material(shader);target=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();read=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        var fire=Load("SamplingFire.png");var graphite=Load("SamplingWiggly.png");var highlighter=Load("SamplingHighlighter.png");
        var pencil=OpenBrushProceduralImport.BakeWiggly(graphite);
        float fireError=0,pencilError=0,maskError=0;double fireSquared=0,pencilSquared=0,maskSquared=0;
        for(int i=0;i<256;i++) {
            float u=Mathf.Repeat(i*.6180339887f,2)-.5f,v=Mathf.Repeat(i*.371234f,1.2f)-.1f,t=i*.00731f;
            float e=Error(OpenBrushProceduralImport.FireSample(fire,u,v,t),GPU(fire,u,v,t,1));fireError=Mathf.Max(fireError,e);fireSquared+=e*e;
            int frame=i%6,x=(i*173+3)%graphite.width,y=(i*97+11)%graphite.height;
            e=Error(pencil[frame].GetPixel(x,y),GPU(graphite,(x+.5f)/graphite.width/1.1f,(y+.5f)/graphite.height,(frame+.5f)/12,2));pencilError=Mathf.Max(pencilError,e);pencilSquared+=e*e;
            var face=new OpenBrushHighlighterImport.Face{uv=new[]{new Vector2(u,v),new Vector2(u,v),new Vector2(u,v)},colors=new[]{Color.white,Color.white,Color.white},copies=1};
            OpenBrushHighlighterImport.Sample(highlighter,face,.2f,.3f,Vector2.one,Vector2.zero,out var emission,out var mask);
            e=Mathf.Abs(mask.a-GPU(highlighter,u,v,0,0).a);maskError=Mathf.Max(maskError,e);maskSquared+=e*e;
        }
        string stats=$"Fire max={fireError} rms={Math.Sqrt(fireSquared/256)}; Wiggly max={pencilError} rms={Math.Sqrt(pencilSquared/256)}; Highlighter alpha max={maskError} rms={Math.Sqrt(maskSquared/256)}";
        Debug.Log("SAMPLING_GPU_COMPARISON "+stats);
        if(enforce && (fireError>.006f || pencilError>.006f || maskError>.003f))throw new Exception("GPU sampling mismatch: "+stats);
                var profile=OpenBrushProfiles.Find(OpenBrushProceduralImport.WigglyGuid);
        if(!profile.LinearVertexColors || !profile.SpecularModel || profile.Specular!=0 || profile.Smoothness!=0)throw new Exception("Wiggly normal source uses black specular workflow with decoded vertex RGB");
        var go=new GameObject("Wiggly vertex encoding check");var filter=go.AddComponent<MeshFilter>();
        var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.colors=new[]{new Color(.12f,.06f,.18f,0),new Color(.12f,.06f,.18f,.549f),new Color(.12f,.06f,.18f,1)};mesh.triangles=new[]{0,1,2};filter.sharedMesh=mesh;
        OpenBrushVertexColors.Prepare(filter,false);var decoded=filter.sharedMesh;OpenBrushVertexColors.Prepare(filter,false);
        if(filter.sharedMesh!=decoded || Mathf.Abs(decoded.colors[0].r-Mathf.GammaToLinearSpace(.12f))>1e-7 || decoded.colors[1].a!=.549f)throw new Exception("Wiggly RGB decoded once and alpha retained");
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log(enforce ? "OPENBRUSH_SAMPLING_CHECKS_PASSED" : "OPENBRUSH_SAMPLING_BASELINE_RECORDED");EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}

