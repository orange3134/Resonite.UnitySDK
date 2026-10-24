using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushCelestialChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("Celestial check: "+message);}
 static void Fixture(float ceiling) {
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-fairy-space-v1/";
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json")),false);
   var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
   OpenBrushGrowthImport.Prepare(root,timing,null);
   var sources=root.GetComponentsInChildren<MeshRenderer>().Where(r=>OpenBrushFlowImport.Supports(r.sharedMaterial.GetTag("OpenBrushGuid",false,""))).ToArray();
   int count=sources.Length;
   Check(sources.Select(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")).Distinct().Count()==2,"two normal brush materials");
   var original=sources.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray();
   var colors=original.SelectMany(m=>m.colors).Select(c=>new Vector3(c.r,c.g,c.b)).Distinct().ToArray();
   Check(colors.Length==5,"five paint colors before bake");
   OpenBrushFlowImport.PrepareSamples(root,new[]{0f,.5f,1.25f,3f},1,1,ceiling);
   var motions=root.GetComponentsInChildren<OpenBrushTextureMotion>();
   Check(motions.Length==1,"one shared HDR atlas/clock for both brushes and five colors");
   var motion=motions[0];
   Check(motion.Timeline==root.GetComponent<OpenBrushTimeline>()&&motion.FrameTimes.SequenceEqual(new[]{0f,.5f,1.25f,3f}),"absolute shader time shares drawing playback");
   Check(motion.Frames.All(t=>t.format==TextureFormat.RGBAHalf&&!t.isDataSRGB),"linear HDR frames survive texture transport");
   Check(motion.SourceMaterial.GetColor("_TintColor")==Color.white,"no double HDR multiplier");
   var pixels=motion.Frames[0].GetPixels();var next=motion.Frames[1].GetPixels();
   Check(pixels.All(c=>float.IsFinite(c.r)&&float.IsFinite(c.g)&&float.IsFinite(c.b)),"no NaNs from procedural hue/noise");
   float peak=pixels.Max(c=>Mathf.Max(c.r,Mathf.Max(c.g,c.b)));
   Check(ceiling>16?peak>16:peak<=ceiling+.001f && peak>1,"requested HDR range preserved");
   Check(pixels.Zip(next,(a,b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)).Count(d=>d>.02f)>1000,"source fragments change with time");
   Check(sources.All(r=>!r.enabled&&r.transform.Find("Timed Flow Fragments")!=null),"static originals replaced once");
   var children=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial==motion.SourceMaterial).ToArray();
   Check(children.Length==count&&children.All(r=>r.GetComponentInParent<OpenBrushGrowthPart>()!=null),"all fragment surfaces follow their drawing segment");
   Check(original.SelectMany(m=>m.colors).Select(c=>new Vector3(c.r,c.g,c.b)).Distinct().SequenceEqual(colors),"source vertex colors unchanged");
   File.WriteAllBytes(".local/fairy-space-atlas-0.exr",motion.Frames[0].EncodeToEXR());
   Debug.Log("CELESTIAL_FIXTURE parts="+count+" peak="+peak+" atlas="+motion.Frames[0].width+"x"+motion.Frames[0].height);
  }
 }
 public static void Run(){try {
  Fixture(65504);Fixture(OpenBrushFlowImport.CelestialCeiling);
  Debug.Log("OPENBRUSH_CELESTIAL_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
