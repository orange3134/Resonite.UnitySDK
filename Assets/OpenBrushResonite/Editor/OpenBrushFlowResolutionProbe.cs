using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushFlowResolutionProbe {
 public static void Run(){try {
  foreach(int ceiling in new[]{1,2,4,8}) {
   using(var data=new AutoGltfFileParser("D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/Flow01-growth-v1.glb").Parse())using(var importer=new ImporterContext(data)) {
    var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
    var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText("D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/Flow01-growth-v1.materials.json"));
    OpenBrushMaterialImport.Prepare(root,manifest,false);root.AddComponent<OpenBrushTimeline>().Duration=18.25f;
    OpenBrushFlowImport.PrepareSamples(root,new[]{16.5f,17.5f},1,ceiling);
    var motion=root.GetComponentInChildren<OpenBrushTextureMotion>();
    for(int f=0;f<2;f++)File.WriteAllBytes(".local/flow-hdr-"+ceiling+"-"+f+".png",motion.Frames[f].EncodeToPNG());
    foreach(var frame in motion.Frames)UnityEngine.Object.DestroyImmediate(frame);
    UnityEngine.Object.DestroyImmediate(root);
   }
  }
  Debug.Log("OPENBRUSH_FLOW_RESOLUTION_PROBE_DONE");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
