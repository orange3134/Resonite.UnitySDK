using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushMylarChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Mylar check: "+why);}
 public static void Run(){try {
  Check(OpenBrushMylarImport.Rim(0)==0&&OpenBrushMylarImport.Rim(1)==0,"source emission vanishes at tangent and normal incidence");
  Check(Mathf.Abs(OpenBrushMylarImport.Rim(.5f)-.484375f)<1e-6f,"source view-weight polynomial");
  var pattern=new Texture2D(2,2,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Repeat};
  pattern.SetPixels(Enumerable.Repeat(Color.white,4).ToArray());pattern.Apply();
  var shade=OpenBrushMylarImport.Shade(pattern,new Color(.8f,.2f,.05f,1),.5f,0);
  float rim=.484375f;
  Check(Mathf.Abs(shade.r-Mathf.GammaToLinearSpace(rim*(.25f*rim+.6f)))<1e-6f,"complete gamma emission product decoded once");
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-mylar-v2/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));
  var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   OpenBrushMaterialImport.Prepare(root,manifest,false);OpenBrushGrowthImport.Prepare(root,timing,null);
   var inputs=root.GetComponentsInChildren<MeshRenderer>().Where(m=>m.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushMylarImport.Guid).ToArray();
   int triangles=inputs.Sum(m=>m.GetComponent<MeshFilter>().sharedMesh.triangles.Length);
   var source=inputs[0].sharedMaterial.mainTexture as Texture2D;
   var a=OpenBrushMylarImport.Shade(source,Color.red,.4f,0);var b=OpenBrushMylarImport.Shade(source,Color.red,.4f,20);
   Check((new Vector3(a.r,a.g,a.b)-new Vector3(b.r,b.g,b.b)).sqrMagnitude<1e-9,"source scrolling wraps at 20 seconds");
   OpenBrushMylarImport.Prepare(root);
   var films=root.GetComponentsInChildren<MeshRenderer>().Where(m=>m.sharedMaterial.GetTag("OpenBrushViewEmission",false,"")=="1").ToArray();
   Check(films.Sum(m=>m.GetComponent<MeshFilter>().sharedMesh.triangles.Length)==triangles,"source winding and all faces preserved once");
   Check(films.All(m=>m.GetComponentInParent<OpenBrushGrowthPart>()!=null),"every animated layer follows its drawing segment");
   var motions=root.GetComponentsInChildren<OpenBrushTextureMotion>();
   Check(motions.Length==5&&motions.All(m=>m.Frames.Length==120&&m.FramesPerSecond==6),"five colors retain complete 20-second motion sequences");
   Check(motions.All(m=>!m.Frames[0].GetPixels32().SequenceEqual(m.Frames[30].GetPixels32())),"pattern changes over time for every color");
   Check(motions.All(m=>m.Frames[0].mipmapCount>1&&!m.Frames[0].isDataSRGB),"linear emission frames with mipmaps");
   int count=root.GetComponentsInChildren<MeshRenderer>().Length;OpenBrushMylarImport.Prepare(root);
   Check(root.GetComponentsInChildren<MeshRenderer>().Length==count,"preparation is idempotent");
  }
  Debug.Log("OPENBRUSH_MYLAR_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}

