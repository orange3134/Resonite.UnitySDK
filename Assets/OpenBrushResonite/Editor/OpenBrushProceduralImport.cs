// Equations adapted from Open Brush / Tilt Brush Fire.shader and
// AudioReactive.hlsl: LightWireLegacyEmission / LightWireChase.
// Tilt Brush Authors, Apache-2.0. Native HDR gain remains separately calibrated.
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OpenBrushProceduralImport
{
    public const string WigglyGuid = "5347acf0-a8e2-47b6-8346-30c70719d763";
    public const string FireGuid = "cb92b597-94ca-4255-b017-0e3f42f12f9e";
    public const string WireGuid = "4391aaaa-df81-4396-9e33-31e4e4930b27";
    public static bool IsWiggly(string guid) => guid==WigglyGuid || guid=="e814fef1-97fd-7194-4a2f-50c2bb918be2";

    [MenuItem("Resonite SDK/Open Brush/Add Fire, LightWire and WigglyGraphite Motion to Prepared Model")]
    public static void PrepareSelected() {
        var root=Selection.activeGameObject;
        if(root==null || EditorUtility.IsPersistent(root)) throw new InvalidOperationException("Select the prepared model root.");
        Prepare(root, true);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
    }
    public static void Prepare(GameObject root, bool saveAssets=false) {
        var materials=root.GetComponentsInChildren<MeshRenderer>(true).Select(r=>r.sharedMaterial).Where(m=>m!=null).Distinct().ToArray();
        foreach(var material in materials) {
            string guid=material.GetTag("OpenBrushGuid",false,"");
            if(guid!=FireGuid && guid!=WireGuid && !IsWiggly(guid)) continue;
            if(root.GetComponentsInChildren<OpenBrushTextureMotion>(true).Any(m=>m.SourceMaterial==material)) continue;
            bool fire=guid==FireGuid, wiggly=IsWiggly(guid);
            var frames=fire ? BakeFire(material.mainTexture as Texture2D) : wiggly ? BakeWiggly(material.mainTexture as Texture2D) : BakeWire();
            if(saveAssets) {
                if(!AssetDatabase.IsValidFolder("Assets/OpenBrushGenerated")) AssetDatabase.CreateFolder("Assets","OpenBrushGenerated");
                foreach(var frame in frames) AssetDatabase.CreateAsset(frame,AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/"+frame.name+".asset"));
            }
            var child=new GameObject(wiggly ? "WigglyGraphite - Stepped Pencil Motion" : fire ? "Fire - Baked Shader Motion" : "LightWire - Chase Motion");
            child.transform.SetParent(root.transform,false);
            var motion=child.AddComponent<OpenBrushTextureMotion>();
            motion.SourceMaterial=material; motion.Frames=frames;
            motion.FramesPerSecond=wiggly ? 12 : 30; motion.EmissionOnly=!fire && !wiggly;
            // Light positions repeat every .5 UV, colors every 3 lights and chase every 7.
            motion.TextureScale=wiggly ? Vector2.Scale(material.mainTextureScale,new Vector2(1.1f,1)) : fire ? material.mainTextureScale : new Vector2(1f/10.5f,1);
        }
        if(saveAssets) AssetDatabase.SaveAssets();
    }
    public static Color FireSample(Texture2D texture,float u,float v,float seconds) {
        // Unity _Time.x = seconds / 20; normal material Scroll1=20, Scroll2=0.
        float displacement=OpenBrushTextureSampling.SampleRaw(texture,u-seconds,v).a * .1f;
        var c=OpenBrushTextureSampling.SampleRaw(texture,u-displacement,v-displacement);
        if(c.r<.01f)c.r=0; if(c.g<.01f)c.g=0; if(c.b<.01f)c.b=0;
        return c;
    }
    public static Texture2D[] BakeFire(Texture2D texture) {
        if(texture==null || !texture.isReadable) throw new InvalidOperationException("Fire requires the readable source texture.");
        // Preserve the imported texture's sampling and color profile.
        int width=Mathf.Min(texture.width,512), height=Mathf.Min(texture.height,512);
        var frames=new Texture2D[30];
        for(int frame=0;frame<frames.Length;frame++) {
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
                pixels[y*width+x]=FireSample(texture,(x+.5f)/width,(y+.5f)/height,frame/30f);
            frames[frame]=MakeFrame("FireMotion_"+frame.ToString("D2"),width,height,pixels,!texture.isDataSRGB);
            frames[frame].wrapModeU=texture.wrapModeU; frames[frame].wrapModeV=texture.wrapModeV;
        }
        return frames;
    }
    public static Color WireSample(float u,float seconds) {
        float envelope=Mathf.Sin(Mathf.Repeat(u*2,1)*Mathf.PI);
        if(envelope>=.1f)return new Color(0,0,0,1);
        float on=1-Mathf.Clamp01(Mathf.Abs(Mathf.Repeat(u*2+.5f,7)-Mathf.Repeat(seconds*3,7))*1.5f);
        int index=(int)Mathf.Repeat(u*2+.5f,3);
        var color=index==0 ? new Color(.2f,.2f,1) : index==1 ? new Color(1,.2f,.2f) : new Color(.2f,1,.2f);
        // Keep the shader's colored bulbs and sharp pulse, without its engine-specific HDR boost.
        return new Color(Mathf.Pow(color.r*on,2.2f),Mathf.Pow(color.g*on,2.2f),Mathf.Pow(color.b*on,2.2f),1);
    }
    public static Texture2D[] BakeWire() {
        var frames=new Texture2D[70]; // 7 time-index units / (_Time.w = seconds*3).
        for(int frame=0;frame<frames.Length;frame++) {
            var pixels=new Color[2048];
            for(int x=0;x<pixels.Length;x++) pixels[x]=WireSample((x+.5f)/pixels.Length*10.5f,frame/30f);
            frames[frame]=MakeFrame("LightWireMotion_"+frame.ToString("D2"),2048,1,pixels,true);
        }
        return frames;
    }
    public static Texture2D[] BakeWiggly(Texture2D texture) {
        if(texture==null || !texture.isReadable)throw new InvalidOperationException("WigglyGraphite requires a readable source texture");
        var frames=new Texture2D[6];
        // ceil(fmod(t*12,6)) yields states 1..6 between frame boundaries.
        // Keep the non-integer U scale on the material, so long/repeating UVs remain correct.
        for(int frame=0;frame<6;frame++) {
            var pixels=new Color[texture.width*texture.height];
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
                pixels[y*texture.width+x]=OpenBrushTextureSampling.SampleRaw(texture,(x+.5f)/texture.width+(frame+1)*1.1f,(y+.5f)/texture.height);
            frames[frame]=MakeFrame("WigglyGraphiteMotion_"+frame,texture.width,texture.height,pixels,!texture.isDataSRGB);
            frames[frame].wrapModeU=texture.wrapModeU;frames[frame].wrapModeV=texture.wrapModeV;
        }
        return frames;
    }
    static Texture2D MakeFrame(string name,int width,int height,Color[] pixels,bool linear) {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,linear) {
            name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat
        };
        texture.SetPixels(pixels);texture.Apply(false,false);return texture;
    }
}

