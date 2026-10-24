using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushFlowChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("Flow check: "+message);}
 public static void Run(){try {
  using(var data=new AutoGltfFileParser(Path.GetFullPath("D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/Flow01-growth-v1.glb")).Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText("D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/Flow01-growth-v1.materials.json"));
   OpenBrushMaterialImport.Prepare(root,manifest,false);
   var original=root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
   Check(original.Any(m=>m.colors.Select(c=>c.a).Distinct().Count()>1),"fixture retains nonuniform vertex alpha for interpolation");
   var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=1;
   OpenBrushFlowImport.Prepare(root);var motion=root.GetComponentInChildren<OpenBrushTextureMotion>();
   Check(motion!=null && motion.Timeline==timeline && motion.Frames.Length==31,"31 samples including exact endpoint share drawing clock");
   Check(motion.FrameTimes[0]==0 && motion.FrameTimes.Last()==1,"finite absolute shader time");
   Check(motion.Frames.All(t=>t.isDataSRGB && t.format==TextureFormat.RGBA32),"display-range sRGB retained for native decode");
   Check(motion.SourceMaterial.GetTag("OpenBrushBoundedFlowAtlas",false,"")=="1" && Math.Abs(motion.SourceMaterial.GetColor("_TintColor").r-Mathf.Pow(4,1/2.2f))<.001f,"bounded HDR multiplier is encoded for native Unlit without multiplying unrelated brush gains");
   Check(root.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)==160,"each growth part retains one child surface");
   Check(original.All(m=>m.colors.Any(c=>c!=Color.white)),"source meshes were not recolored");
   var a=motion.Frames[0].GetPixels32();var b=motion.Frames[15].GetPixels32();
   Check(motion.Frames[0].GetPixels(396,264,132,132).All(c=>c.r==0 && c.g==0 && c.b==0),"empty last atlas tile is at its expected UV address (render-target Y orientation)");
   Check(a.Zip(b,(x,y)=>!x.Equals(y)).Count(x=>x)>1000,"shader output actually changes with time");
   Check(a.Count(c=>c.r>c.g*2 && c.r>c.b*2)>100,"red color survives");
   Check(a.Count(c=>c.b>c.r*2 && c.b>c.g*2)>100,"blue color survives");
   File.WriteAllBytes(".local/flow-atlas-0.png",motion.Frames[0].EncodeToPNG());File.WriteAllBytes(".local/flow-atlas-05.png",motion.Frames[15].EncodeToPNG());
   Check(ComponentConverterRepository.TryGetConverter(typeof(OpenBrushTextureMotion))?.Type==typeof(OpenBrushTextureMotionConverter),"SDK discovers texture timeline converter");
  }
  Debug.Log("OPENBRUSH_FLOW_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
