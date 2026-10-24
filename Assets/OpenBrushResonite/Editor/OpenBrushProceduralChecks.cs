using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;

public static class OpenBrushProceduralChecks
{
    static void Check(bool ok,string message) { if(!ok)throw new InvalidOperationException(message); Debug.Log("Procedural check: "+message); }
    public static void Run() {
        try {
            var probe=new GameObject("Closed generic check");
            ResoniteComponent container;
            probe.AddResoniteComponent<OpenBrushTextureMultiplexer>(out container);
            Check(container.TypeName=="[FrooxEngine]FrooxEngine.AssetMultiplexer<[FrooxEngine]FrooxEngine.ITexture2D>","SDK sends the runtime-compatible closed generic type");
            UnityEngine.Object.DestroyImmediate(probe);
            // Check the displacement against an independently sampled reference on synthetic data.
            var pattern=new Texture2D(32,32,TextureFormat.RGBA32,false,true);
            var pixels=new Color[32*32];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)pixels[y*32+x]=new Color((x+.5f)/32,(y+.5f)/32,1,.5f);
            pattern.SetPixels(pixels);pattern.Apply();
            var sample=OpenBrushProceduralImport.FireSample(pattern,.5f,.5f,.2f);
            var reference=OpenBrushTextureSampling.SampleRaw(pattern,.5f-OpenBrushTextureSampling.SampleRaw(pattern,.3f,.5f).a*.1f,.5f-OpenBrushTextureSampling.SampleRaw(pattern,.3f,.5f).a*.1f);
            Check((new Vector4(sample.r,sample.g,sample.b,sample.a)-new Vector4(reference.r,reference.g,reference.b,reference.a)).sqrMagnitude<1e-10,"Fire displaces both axes, not the whole base texture");
            UnityEngine.Object.DestroyImmediate(pattern);
            var wire=OpenBrushProceduralImport.WireSample(0,1f/6);
            Check(wire.b>.99f && wire.r<.04f,"first LightWire bulb is blue at its chase peak");
            Check(OpenBrushProceduralImport.WireSample(.25f,0).maxColorComponent==0,"wire between bulbs stays unlit");
            Check(OpenBrushProceduralImport.WireSample(0,1).b==0,"bulb extinguishes after the chase passes");
            using(var data=new AutoGltfFileParser(Path.GetFullPath(".local/BrushTest-growth-v2-compact.glb")).Parse())
            using(var importer=new ImporterContext(data)) {
                var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
                var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(".local/BrushTest-growth-v2-compact.materials.json"));
                OpenBrushMaterialImport.Prepare(root,manifest,false);
                var meshes=root.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).ToArray();
                OpenBrushProceduralImport.Prepare(root);
                OpenBrushProceduralImport.Prepare(root);
                var motions=root.GetComponentsInChildren<OpenBrushTextureMotion>();
                Check(motions.Length==3,"Fire, LightWire and WigglyGraphite prepared exactly once");
                Check(root.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).SequenceEqual(meshes),"stroke meshes unchanged");
                var fire=motions.Single(m=>m.SourceMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushProceduralImport.FireGuid);var light=motions.Single(m=>m.EmissionOnly);
                Check(fire.Frames.Length==30 && light.Frames.Length==70,"30fps loops: Fire 1s and LightWire 7/3s");
                var first=fire.Frames[0].GetPixels32();var later=fire.Frames[15].GetPixels32();
                Check(first.Where((p,i)=>!p.Equals(later[i])).Count()>first.Length*.01f,"actual Fire source produces changing pixels");
                var source=(Texture2D)fire.SourceMaterial.mainTexture;
                Debug.Log("FIRE_SOURCE "+source.width+"x"+source.height+" wrap="+source.wrapModeU+","+source.wrapModeV);
                float error=0;
                for(int i=0;i<32;i++){
                    var a=OpenBrushProceduralImport.FireSample(source,(i+.5f)/32,.43f,0);
                    var b=OpenBrushProceduralImport.FireSample(source,(i+.5f)/32,.43f,1);
                    error+=Mathf.Abs(a.r-b.r)+Mathf.Abs(a.a-b.a);
                }
                Check(error<.001f,"Fire loop closes at one second");
                Check(light.Frames[0].GetPixels32().Where((p,i)=>!p.Equals(light.Frames[30].GetPixels32()[i])).Any(),"LightWire chase changes bulb positions");
                Check(ComponentConverterRepository.TryGetConverter(typeof(OpenBrushTextureMotion))?.Type==typeof(OpenBrushTextureMotionConverter),"SDK discovers native sequence conversion");
                UnityEngine.Object.DestroyImmediate(root);
            }
            OpenBrushGraphiteDiscoChecks.CheckAll();
            Debug.Log("OPENBRUSH_PROCEDURAL_CHECKS_PASSED");EditorApplication.Exit(0);
        } catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}



