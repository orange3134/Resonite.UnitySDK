using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushLeakyPenChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("LeakyPen check: "+why);}
 public static void Run(){try {
  var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
  texture.SetPixels(new[]{new Color(.2f,.8f,.4f,1),Color.black,Color.white,new Color(.6f,.1f,.2f,1)});texture.Apply();
  var before=texture.GetPixels();var mask=OpenBrushLeakyPenImport.BakeMask(texture);var pixels=mask.GetPixels();
  Check(pixels.All(c=>c.r==1&&c.g==1&&c.b==1)&&Enumerable.Range(0,4).All(i=>Math.Abs(pixels[i].a-before[i].r)<1e-6),"diffuse R supplies alpha while albedo stays white");
  Check(texture.GetPixels().SequenceEqual(before),"source texture unchanged");
  texture.SetPixel(0,0,new Color(1,1,1,.5f));texture.Apply();bool rejected=false;
  try{OpenBrushLeakyPenImport.BakeMask(texture);}catch(InvalidOperationException){rejected=true;}
  Check(rejected,"stale/custom alpha masks rejected rather than silently misinterpreted");
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-leakypen-v1/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;OpenBrushMaterialImport.Prepare(root,manifest,false);OpenBrushGrowthImport.Prepare(root,timing,null);
   var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushLeakyPenImport.Guid).ToArray();
   Check(renderers.Length==timing.segments.Length && renderers.Length>5,"all five strokes keep their growth segments");
   var alpha=renderers.Select(r=>r.GetComponent<MeshFilter>().sharedMesh.colors.Select(c=>c.a).ToArray()).ToArray();
   OpenBrushLeakyPenImport.Prepare(root);
   Check(renderers.All(r=>r.GetComponent<OpenBrushGrowthPart>()!=null && r.enabled),"original drawing geometry retained");
   Check(renderers.Select((r,i)=>r.GetComponent<MeshFilter>().sharedMesh.colors.Select(c=>c.a).SequenceEqual(alpha[i])).All(x=>x),"randomized vertex alpha preserved for native thresholding");
   var output=renderers[0].sharedMaterial.mainTexture as Texture2D;
   Check(output!=null && output.GetPixels().Any(c=>c.a==0) && output.GetPixels().Any(c=>c.a==1),"actual source mask has both blank and ink regions");
   Check(renderers.Select(r=>r.sharedMaterial).Distinct().Count()==1,"one shared material across five colors");
  }
  Debug.Log("OPENBRUSH_LEAKYPEN_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
