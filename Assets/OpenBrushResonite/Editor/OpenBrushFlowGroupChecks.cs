using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushFlowGroupChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("Flow groups: "+message);}
 public static void Run(){try {
  const string baseDir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/";
  using(var a=new AutoGltfFileParser(baseDir+"bundle-weather-v1/drawing.glb").Parse())
  using(var ia=new ImporterContext(a))
  using(var b=new AutoGltfFileParser(baseDir+"bundle-fire-sparks-v1/drawing.glb").Parse())
  using(var ib=new ImporterContext(b)) {
   var root=ia.Load().gameObject;var other=ib.Load().gameObject;
   OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(baseDir+"bundle-weather-v1/materials.json")),false);
   OpenBrushMaterialImport.Prepare(other,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(baseDir+"bundle-fire-sparks-v1/materials.json")),false);
   while(other.transform.childCount>0)other.transform.GetChild(0).SetParent(root.transform,false);
   UnityEngine.Object.DestroyImmediate(other);
   var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=1;
   OpenBrushFlowImport.Prepare(root);
   var motions=root.GetComponentsInChildren<OpenBrushTextureMotion>();
   Check(motions.Length==2 && motions.All(m=>m.Timeline==timeline && m.Frames.Length==31),"two independent atlas ranges share one drawing clock");
   Check(root.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)==140,"all mixed brush surfaces remain present");
   foreach(var motion in motions) {
    string guid=motion.SourceMaterial.GetTag("OpenBrushGuid",false,"");
    bool high=guid==OpenBrushFlowImport.Fire2 || guid==OpenBrushFlowImport.Sparks;
    Check(Mathf.Abs(motion.SourceMaterial.GetColor("_TintColor").r-Mathf.Pow(high?16:4,1/2.2f))<.001f,"original range retained for "+guid);
    Check(root.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled && r.sharedMaterial==motion.SourceMaterial)==70,"no cross-assignment between ranges");
   }
  }
  Debug.Log("OPENBRUSH_FLOW_GROUP_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
