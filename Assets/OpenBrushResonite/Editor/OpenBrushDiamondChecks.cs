using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushDiamondChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Diamond check: "+why);}
 public static void Run(){try {
  // Independent normal-incidence thin-film formula: R=4*r^2*sin^2(delta/2) /
  // ((1-r^2)^2+4*r^2*sin^2(delta/2)), r=(1-1.3)/(1+1.3).
  double r=-.3/2.3;
  foreach(float wavelength in new[]{475f,510f,650f})foreach(float thickness in new[]{250f,325f,400f}) {
   double a=4*r*r*Math.Pow(Math.Sin(2*Math.PI*1.3*thickness/wavelength),2);
   double expected=a/(Math.Pow(1-r*r,2)+a);
   Check(Math.Abs(OpenBrushDiamondImport.Reflectance(1,wavelength,thickness)-expected)<1e-6,"normal-incidence optical identity");
  }
  foreach(float c in new[]{0f,.01f,.1f,.3f,.6f,1f})foreach(float t in new[]{250f,325f,400f}) {
   var x=OpenBrushDiamondImport.Reflectance(c,510,t);
   Check(!float.IsNaN(x)&&x>=0&&x<=1&&x==OpenBrushDiamondImport.Reflectance(-c,510,t),"finite bounded two-sided reflectance");
  }
  Check(OpenBrushDiamondImport.Rim(0)==150&&OpenBrushDiamondImport.Rim(1)==0,"source grazing boost and normal-incidence zero emission");
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-diamond-complete-v1/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));
  var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  Check(manifest.materials.Length==2,"both DiamondHull and DiamondMembrane exported");
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   OpenBrushMaterialImport.Prepare(root,manifest,false);OpenBrushGrowthImport.Prepare(root,timing,null);
   var inputs=root.GetComponentsInChildren<MeshRenderer>().Where(m=>OpenBrushDiamondImport.Supports(m.sharedMaterial.GetTag("OpenBrushGuid",false,""))).ToArray();
   int triangles=inputs.Sum(m=>m.GetComponent<MeshFilter>().sharedMesh.triangles.Length);
   OpenBrushDiamondImport.Prepare(root);
   var films=root.GetComponentsInChildren<MeshRenderer>().Where(m=>m.sharedMaterial.GetTag("OpenBrushViewEmission",false,"")=="1").ToArray();
   Check(films.Sum(m=>m.GetComponent<MeshFilter>().sharedMesh.triangles.Length)==triangles,"all source faces preserved in thin-film layer");
   Check(films.All(m=>m.GetComponentInParent<OpenBrushGrowthPart>()!=null),"every film follows its parent drawing segment");
   var motions=root.GetComponentsInChildren<OpenBrushTextureMotion>();
   Check(motions.Length==10&&motions.All(m=>m.Frames.Length==OpenBrushDiamondImport.FrameCount),"five colors per brush carry complete motion sequences");
   Check(motions.All(m=>m.Frames[0]!=m.Frames[1]&&m.Frames[0].mipmapCount>1&&!m.Frames[0].isDataSRGB),"linear bounded frames with mipmaps");
   int count=root.GetComponentsInChildren<MeshRenderer>().Length;OpenBrushDiamondImport.Prepare(root);
   Check(root.GetComponentsInChildren<MeshRenderer>().Length==count,"preparation idempotent");
  }
  Debug.Log("OPENBRUSH_DIAMOND_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
