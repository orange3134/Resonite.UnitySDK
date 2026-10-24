using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushHueShiftChecks {
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("HueShift: "+message);}
 public static void Run(){try {
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-hueshift-v1/";
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   var source=root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
   var positions=source.Select(m=>m.vertices).ToArray();var colors=source.Select(m=>m.colors).ToArray();
   var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));
   Check(OpenBrushMaterialImport.Prepare(root,manifest,false)==70,"ten strokes / seventy growth parts prepared");
   OpenBrushHueShiftImport.Prepare(root);
   var targets=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
   Check(targets.Length==70 && targets.All(r=>r.name=="HueShift Surface"),"one baked surface remains under each growth parent");
   Check(targets.Select(r=>r.sharedMaterial).Distinct().Count()==2,"unlit and lit brushes keep separate material families");
   Check(source.Select((m,i)=>m.vertices.SequenceEqual(positions[i]) && m.colors.SequenceEqual(colors[i])).All(x=>x),"source geometry and RGBA remain unchanged");
   Check(targets.All(r=>r.GetComponent<MeshFilter>().sharedMesh.vertices.SequenceEqual(r.transform.parent.GetComponent<MeshFilter>().sharedMesh.vertices)),"baked surfaces keep exact drawing geometry");
   foreach(var material in targets.Select(r=>r.sharedMaterial).Distinct()) {
    var atlas=material.mainTexture as Texture2D;
    Check(atlas!=null && atlas.isDataSRGB && atlas.width==1548 && atlas.height==520,"five 512x256 charts use one static sRGB atlas");
    Check(atlas.GetPixels32().Count(c=>c.a>128)>10000,"brush color atlas contains visible content");
    File.WriteAllBytes(".local/hueshift-"+material.GetTag("OpenBrushGuid",false,"")+".png",atlas.EncodeToPNG());
   }
   var lofted=targets.First(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushHueShiftImport.Lofted).sharedMaterial;
   var profile=OpenBrushProfiles.Find(OpenBrushHueShiftImport.Lofted);
   Check(profile.SpecularModel && !profile.Unlit && profile.SpecularIsLinear && Mathf.Abs(profile.Specular-.13333334f)<1e-7 && profile.Smoothness==.4f,"Lofted retains normal graph specular and smoothness");
   Check(root.GetComponentsInChildren<OpenBrushTextureMotion>().Length==0,"static color transform does not allocate animation frame sequences");
  }
  CheckAnalyticShader();
  Debug.Log("OPENBRUSH_HUESHIFT_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static void CheckAnalyticShader() {
  var shader=Shader.Find("Hidden/OpenBrush/HueShiftBake");var material=new Material(shader);material.SetFloat("_Lofted",1);
  var input=new Color(.8f,.06f,.03f,1);
  var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.one,new Vector3(0,1,0)},uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},colors=Enumerable.Repeat(input,4).ToArray(),triangles=new[]{0,1,2,0,2,3}};
  var rt=new RenderTexture(128,16,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);var previous=RenderTexture.active;bool srgb=GL.sRGBWrite;
  var image=new Texture2D(128,16,TextureFormat.RGBA32,false,true);
  try {
   rt.Create();RenderTexture.active=rt;GL.sRGBWrite=false;GL.Clear(false,true,Color.clear);Check(material.SetPass(0),"shader pass compiled");Graphics.DrawMeshNow(mesh,Matrix4x4.identity);
   image.ReadPixels(new Rect(0,0,128,16),0,0,false);image.Apply();
   // Source hue=.8*(5+.8)=4.64 => RGB(.64,0,1), independent of incoming G/B.
   for(int x=0;x<128;x++) {
    float u=(x+.5f)/128f,w=(2*u-1)*(2*u-1);var expected=Color.Lerp(input,new Color(.64f,0,1,1),w);var actual=image.GetPixel(x,8);
    Check(Mathf.Max(Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g),Mathf.Abs(actual.b-expected.b))<.006f,"source hue/vignette pixel "+x+" expected="+expected+" actual="+actual);
   }
  }finally{RenderTexture.active=previous;GL.sRGBWrite=srgb;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(image);}
 }
}
