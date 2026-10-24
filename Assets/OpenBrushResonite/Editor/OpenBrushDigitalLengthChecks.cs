using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushDigitalLengthChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("Digital: "+message);}
 public static void Run(){try {
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-digital-length-v1/";
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   var source=root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
   var positions=source.Select(m=>m.vertices).ToArray();var colors=source.Select(m=>m.colors).ToArray();
   var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));
   Check(OpenBrushMaterialImport.Prepare(root,manifest,false)==206,"eight variable-length strokes / 206 growth parts prepared");
   OpenBrushDigitalImport.Prepare(root);
   var targets=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
   Check(targets.Length==206 && targets.All(r=>r.name=="Digital Surface"),"one procedural surface under each growth parent");
   Check(source.Select((m,i)=>m.vertices.SequenceEqual(positions[i]) && m.colors.SequenceEqual(colors[i])).All(x=>x),"source geometry and RGBA unchanged");
   Check(targets.All(r=>r.GetComponent<MeshFilter>().sharedMesh.vertices.SequenceEqual(r.transform.parent.GetComponent<MeshFilter>().sharedMesh.vertices)),"all growth geometry retained exactly");
   var materials=targets.Select(r=>r.sharedMaterial).Distinct().ToArray();
   Check(materials.Length==2,"separate Digital and Race grids");
   foreach(var material in materials) {
    var atlas=material.mainTexture as Texture2D;
    Check(atlas!=null && atlas.isDataSRGB && atlas.width==9218 && atlas.height==384 && atlas.mipmapCount>1 && atlas.anisoLevel==8,"four frequency-sized UV charts / filtered sRGB bounded HDR atlas");
    var pixels=atlas.GetPixels32();
    Check(pixels.Count(c=>c.r>50 || c.g>50 || c.b>50)>1000,"procedural connections are visible");
    Check(pixels.Count(c=>c.a>128 && c.r<5 && c.g<5 && c.b<5)>1000,"empty cells stay dark instead of solid rectangles");
    Check(pixels.Any(c=>c.r>c.g+30 && c.r>c.b+30) && pixels.Any(c=>c.g>c.r+30 && c.g>c.b+30) && pixels.Any(c=>c.b>c.r+30 && c.b>c.g+30),"red green blue inputs survive the fragment bake");
    File.WriteAllBytes(".local/digital-length-"+material.GetTag("OpenBrushGuid",false,"")+".png",atlas.EncodeToPNG());
   }
   Check(!((Texture2D)materials[0].mainTexture).GetPixels32().SequenceEqual(((Texture2D)materials[1].mainTexture).GetPixels32()),"Race directional connection thresholds differ from Digital");
   foreach(var r in targets) {
    var original=r.transform.parent.GetComponent<MeshFilter>().sharedMesh.uv;
    var mapped=r.GetComponent<MeshFilter>().sharedMesh.uv;
    float sourceSpan=original.Max(v=>v.x)-original.Min(v=>v.x);
    float pixels=(mapped.Max(v=>v.x)-mapped.Min(v=>v.x))*r.sharedMaterial.mainTexture.width;
    Check(pixels/sourceSpan>=250,"native UV mapping retains procedural sampling density");
   }
   Check(root.GetComponentsInChildren<OpenBrushTextureMotion>().Length==0,"no unnecessary time-sequence allocation");
  }
  Debug.Log("OPENBRUSH_DIGITAL_LENGTH_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}


