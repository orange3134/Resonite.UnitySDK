using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushFireSparksProbe {
 public static void Run(){try {
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-fire-sparks-v1/";
  foreach(var option in new[]{new Vector2Int(1,4),new Vector2Int(2,4),new Vector2Int(1,16)}) {
   using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
    var root=importer.Load().gameObject;
    OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json")),false);
    root.AddComponent<OpenBrushTimeline>().Duration=9.8f;
    OpenBrushFlowImport.PrepareSamples(root,new[]{7f,7.5f},option.x,option.y);
    var motion=root.GetComponentInChildren<OpenBrushTextureMotion>();
    File.WriteAllBytes(".local/fire-sparks-probe-"+option.x+"-"+option.y+".png",motion.Frames[0].EncodeToPNG());
    foreach(var f in motion.Frames)UnityEngine.Object.DestroyImmediate(f);
    UnityEngine.Object.DestroyImmediate(root);
   }
  }
  Debug.Log("OPENBRUSH_FIRE_SPARKS_PROBE_DONE");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
