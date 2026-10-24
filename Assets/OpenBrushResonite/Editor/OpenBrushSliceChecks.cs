using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushSliceChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("Slice check: "+message);}
 static void Reference() {
  var shader=Shader.Find("Hidden/OpenBrush/SliceReference");Check(shader!=null&&shader.isSupported,"source shader reference available");
  var material=new Material(shader);var rt=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
  var pixels=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);var old=RenderTexture.active;bool srgb=GL.sRGBWrite;float peak=0;
  try {foreach(float w in new[]{0f,.13f,.32f,.61f,.85f,1.19f,1.25f,20.4f}) {
   for(int y=0;y<=8;y++)for(int x=0;x<=8;x++) {
    var uvw=new Vector3(x/8f,y/8f,w);material.SetVector("_UVW",uvw);
    GL.sRGBWrite=false;Graphics.Blit(Texture2D.whiteTexture,rt,material);RenderTexture.active=rt;
    pixels.ReadPixels(new Rect(0,0,1,1),0,0,false);pixels.Apply();
    var expected=OpenBrushSliceImport.Sample(uvw);var actual=pixels.GetPixel(0,0);
    if(w==0 && (x==0||x==8) && (y==0||y==4||y==8))Debug.Log("SLICE_ORACLE uvw="+uvw+" expected="+expected+" actual="+actual);
    peak=Mathf.Max(peak,Mathf.Abs(expected.r-actual.r),Mathf.Abs(expected.g-actual.g),Mathf.Abs(expected.b-actual.b));
   }
  }}finally{RenderTexture.active=old;GL.sRGBWrite=srgb;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(material);}
  Check(peak<.0001f,"CPU fragment matches original GPU equations: "+peak);
 }
 public static void Run(){try {
  Reference();const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-slice-v1/";
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   int restored=OpenBrushSourceSlice.Restore(importer);Check(restored>0,"custom UVW survived GLB import");
   OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json")),false);
   OpenBrushGrowthImport.Prepare(root,JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json")),null);
   var originals=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<MeshFilter>()!=null&&r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushSliceImport.Guid).ToArray();
   Check(originals.SelectMany(r=>r.GetComponent<MeshFilter>().sharedMesh.colors).Distinct().Count()==5,"five original paint colors retained before Slice shader");
   OpenBrushSliceImport.Prepare(root);
   var surfaces=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Slice Color Surface").ToArray();
   Check(surfaces.Length==restored&&surfaces.All(r=>r.GetComponentInParent<OpenBrushGrowthPart>()!=null),"every Slice section follows drawing growth");
   Check(surfaces.Select(r=>r.sharedMaterial).Distinct().Count()==1,"all paint colors share source-defined color atlas");
   float maxError=0;
   foreach(var r in surfaces) {
    var mesh=r.GetComponent<MeshFilter>().sharedMesh;var uvw=new List<Vector3>();mesh.GetUVs(2,uvw);var atlas=(Texture2D)r.sharedMaterial.mainTexture;
    var uv=mesh.uv;var tri=mesh.triangles;
    for(int i=0;i<tri.Length;i+=3) {
     // Interior samples catch lost nonlinear gradients and atlas seams.
     foreach(var weights in new[]{new Vector3(.2f,.3f,.5f),new Vector3(.55f,.3f,.15f)}) {
      var at=uv[tri[i]]*weights.x+uv[tri[i+1]]*weights.y+uv[tri[i+2]]*weights.z;
      var src=uvw[tri[i]]*weights.x+uvw[tri[i+1]]*weights.y+uvw[tri[i+2]]*weights.z;
      var actual=atlas.GetPixelBilinear(at.x,at.y);var expected=OpenBrushSliceImport.Sample(src);
      maxError=Mathf.Max(maxError,Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g),Mathf.Abs(actual.b-expected.b));
     }
    }
   }
   Check(maxError<.03f,"atlas interior error bounded: "+maxError);
   Debug.Log("SLICE_FIXTURE surfaces="+surfaces.Length+" atlasError="+maxError);
  }
  Debug.Log("OPENBRUSH_SLICE_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
