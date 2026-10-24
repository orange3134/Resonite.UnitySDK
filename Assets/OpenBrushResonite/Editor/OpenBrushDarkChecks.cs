using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushDarkChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Dark check: "+why);}
 public static void Run(){try {
  var f=new OpenBrushHighlighterImport.Face{copies=2,uv=Enumerable.Repeat(new Vector2(.5f,.5f),3).ToArray(),colors=Enumerable.Repeat(new Color(.2f,.4f,.6f,.5f),3).ToArray()};
  var c=OpenBrushSoftFragmentImport.SampleDark(f,.2f,.3f);
  Check(Math.Abs(c.r-Mathf.GammaToLinearSpace(.8f*.868f*.868f)/Mathf.GammaToLinearSpace(.8f))<1e-6 && Math.Abs(c.g-Mathf.GammaToLinearSpace(.8f*.901f*.901f)/Mathf.GammaToLinearSpace(.8f))<1e-6 && Math.Abs(c.b-Mathf.GammaToLinearSpace(.8f*.934f*.934f)/Mathf.GammaToLinearSpace(.8f))<1e-6,"source pressure and repeated multiply coverage on reference gray");
  f.uv=Enumerable.Repeat(Vector2.zero,3).ToArray();Check(OpenBrushSoftFragmentImport.SampleDark(f,.2f,.3f)==Color.white,"ribbon edge leaves background unchanged");
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-dark-backgrounds-v2/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;OpenBrushMaterialImport.Prepare(root,manifest,false);OpenBrushGrowthImport.Prepare(root,timing,null);
   var originals=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushSoftFragmentImport.Dark).ToArray();
   Check(originals.Length==105,"all fifteen colored Dark strokes exported with seven growth parts");
   Check(originals.All(r=>OpenBrushHighlighterImport.Faces(r.GetComponent<MeshFilter>().sharedMesh).All(face=>face.copies==2)),"clipped Dark front/back copies align");
   OpenBrushSoftFragmentImport.Prepare(root);
   var surfaces=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.name=="Source Gamma Pressure").ToArray();
   Check(surfaces.Length==105 && surfaces.All(r=>r.transform.parent.GetComponent<OpenBrushGrowthPart>()!=null),"multiply surfaces retain drawing parents");
   Check(surfaces.Select(r=>r.sharedMaterial).Distinct().Count()==5,"one procedural multiplier page per paint color");
   Check(originals.All(r=>!r.enabled),"unconverted original multiply surfaces disabled");
  }
  Debug.Log("OPENBRUSH_DARK_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}

