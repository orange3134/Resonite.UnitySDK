using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushCelestialToneProbe {
 public static void Run(){try {
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-fairy-space-v1/";
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var root=importer.Load().gameObject;
   OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json")),false);
   root.AddComponent<OpenBrushTimeline>().Duration=9.8f;
   OpenBrushFlowImport.PrepareSamples(root,new[]{8.3f,8.4f},1,1,65504);
   var frame=root.GetComponentInChildren<OpenBrushTextureMotion>().Frames[0];var pixels=frame.GetPixels();
   foreach(float ceiling in new[]{1f,2f,4f}) {
    var bounded=new Color[pixels.Length];for(int i=0;i<pixels.Length;i++)bounded[i]=new Color(Mathf.Min(pixels[i].r,ceiling),Mathf.Min(pixels[i].g,ceiling),Mathf.Min(pixels[i].b,ceiling),1);
    var tex=new Texture2D(frame.width,frame.height,TextureFormat.RGBAHalf,false,true);tex.SetPixels(bounded);tex.Apply();
    File.WriteAllBytes(".local/celestial-ceiling-"+ceiling+".exr",tex.EncodeToEXR());UnityEngine.Object.DestroyImmediate(tex);
   }
  }
  Debug.Log("OPENBRUSH_CELESTIAL_TONE_PROBE_DONE");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
