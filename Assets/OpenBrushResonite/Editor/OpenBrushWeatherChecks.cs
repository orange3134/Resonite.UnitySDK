using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushWeatherChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("Weather: "+message);}
 public static void Run(){try {
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-weather-v1/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();var root=instance.gameObject;
   Check(OpenBrushMaterialImport.Prepare(root,manifest,false)==70,"ten strokes retain all70 drawing sections");
   var originals=root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
   var saved=originals.Select(m=>m.vertices).ToArray();
   var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=9.8f;
   OpenBrushFlowImport.PrepareSamples(root,new[]{0f,.25f,.5f,1f,7f,7.5f,9.8f},1,4);
   var motion=root.GetComponentInChildren<OpenBrushTextureMotion>();
   Check(motion.Frames.Length==7 && motion.FrameTimes.Last()==9.8f && motion.Timeline==timeline,"weather pattern uses drawing clock including final endpoint");
   Check(motion.Frames.All(t=>t.isDataSRGB && t.width==528 && t.height==396),"ten stroke atlas charts retain display color");
   Check(root.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)==70,"one animated surface per section");
   Check(originals.Select((m,i)=>m.vertices.SequenceEqual(saved[i])).All(x=>x),"atlas preparation preserves displaced geometry");
   var a=motion.Frames[0].GetPixels32();var b=motion.Frames[3].GetPixels32();
   Check(a.Zip(b,(x,y)=>!x.Equals(y)).Count(x=>x)>1000,"weather fragments change with shader time");
   Check(a.Count(c=>c.r>c.g*2 && c.r>c.b*2)>100,"red remains red");
   Check(a.Count(c=>c.b>c.r*2 && c.b>c.g*2)>100,"blue remains blue");
   File.WriteAllBytes(".local/weather-atlas-0.png",motion.Frames[0].EncodeToPNG());
   File.WriteAllBytes(".local/weather-atlas-1.png",motion.Frames[3].EncodeToPNG());
   Check(ComponentConverterRepository.TryGetConverter(typeof(OpenBrushTextureMotion))?.Type==typeof(OpenBrushTextureMotionConverter),"native texture animation converter available");
  }
  Debug.Log("OPENBRUSH_WEATHER_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
