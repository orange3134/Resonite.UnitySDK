using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushFireSparksChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("FireSparks: "+message);}
 public static void Run(){try {
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-fire-sparks-v1/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();var root=instance.gameObject;
   Check(OpenBrushMaterialImport.Prepare(root,manifest,false)==70,"ten strokes retain all70 drawing sections");
   var originals=root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
   var saved=originals.Select(m=>m.vertices).ToArray();
   var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=1f;
   OpenBrushFlowImport.Prepare(root);
   var motion=root.GetComponentInChildren<OpenBrushTextureMotion>();
   Check(motion.Frames.Length==31 && motion.FrameTimes.Last()==1f && motion.Timeline==timeline,"fire-sparks pattern uses drawing clock including final endpoint");
   Check(Mathf.Abs(motion.SourceMaterial.GetColor("_TintColor").r-Mathf.Pow(16,1/2.2f))<.001f,"Fire2/Sparks retains its separately calibrated HDR range");
   Check(motion.Frames.All(t=>t.isDataSRGB && t.width==528 && t.height==396),"ten stroke atlas charts retain display color");
   Check(root.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)==70,"one animated surface per section");
   Check(originals.Select((m,i)=>m.vertices.SequenceEqual(saved[i])).All(x=>x),"atlas preparation preserves displaced geometry");
   var a=motion.Frames[0].GetPixels32();var b=motion.Frames[3].GetPixels32();
   Check(a.Zip(b,(x,y)=>!x.Equals(y)).Count(x=>x)>1000,"fire-sparks fragments change with shader time");
   Check(a.Count(c=>c.r>c.g*2 && c.r>c.b*2)>100,"red remains red");
   Check(a.Count(c=>c.b>c.r*2 && c.b>c.g*2)>100,"blue remains blue");
   File.WriteAllBytes(".local/fire-sparks-atlas-0.png",motion.Frames[0].EncodeToPNG());
   File.WriteAllBytes(".local/fire-sparks-atlas-1.png",motion.Frames[3].EncodeToPNG());
   Check(ComponentConverterRepository.TryGetConverter(typeof(OpenBrushTextureMotion))?.Type==typeof(OpenBrushTextureMotionConverter),"native texture animation converter available");
  }
  Debug.Log("OPENBRUSH_FIRE_SPARKS_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}

