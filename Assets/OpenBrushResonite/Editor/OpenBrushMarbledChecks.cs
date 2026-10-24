using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushMarbledChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Marbled check: "+why);}
 public static void Run(){try {
  var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
  texture.SetPixels(new[]{new Color(.2f,.8f,.4f,.3f),Color.black,Color.white,new Color(.6f,.1f,.2f,.5f)});texture.Apply();
  var before=texture.GetPixels();var packed=OpenBrushMarbledImport.PackSpecular(texture);var pixels=packed.GetPixels();
  Check(pixels.All(c=>Math.Abs(c.a-.8f)<.002)&&Enumerable.Range(0,4).All(i=>Math.Abs(pixels[i].r-before[i].r*.21960784f)<.002&&Math.Abs(pixels[i].g-before[i].g*.21960784f)<.002&&Math.Abs(pixels[i].b-before[i].b*.21960784f)<.002),"map contains source specular tint and independent smoothness, not fallback field multipliers");
  Check(!packed.isDataSRGB && packed.mipmapCount>1,"specular parameters stay linear with mipmaps");
  Check(texture.GetPixels().SequenceEqual(before),"source texture unchanged");
  var sample=new Mesh();sample.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};sample.triangles=new[]{0,1,2};sample.colors=new[]{new Color(.2f,.4f,.6f,.1f),Color.clear,Color.white};
  var result=OpenBrushMarbledImport.TextureAlphaOnly(sample);
  Check(result.colors.All(c=>c.a==1)&&sample.colors[0].a==.1f&&result.colors[0].r==sample.colors[0].r,"texture alone controls opacity without modifying source RGB/alpha");
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-marbled-v1/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;OpenBrushMaterialImport.Prepare(root,manifest,false);OpenBrushGrowthImport.Prepare(root,timing,null);
   var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushMarbledImport.Guid).ToArray();
   var old=renderers.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray();
   OpenBrushMarbledImport.Prepare(root);
   Check(renderers.Length==timing.segments.Length && renderers.Length>5,"all five strokes retain growth segments");
   Check(renderers.Select((r,i)=>{var m=r.GetComponent<MeshFilter>().sharedMesh;return m.vertices.SequenceEqual(old[i].vertices)&&m.normals.SequenceEqual(old[i].normals)&&m.tangents.SequenceEqual(old[i].tangents)&&m.uv.SequenceEqual(old[i].uv)&&m.triangles.SequenceEqual(old[i].triangles);}).All(x=>x),"geometry, tangent basis and UV mapping retained");
   Check(renderers.All(r=>r.enabled&&r.GetComponent<OpenBrushGrowthPart>()!=null&&r.sharedMaterial.GetTexture("_BumpMap")!=null&&r.sharedMaterial.GetTexture("_MaskTex")!=null),"growth, normal map and specular map retained");
   Check(renderers.Select(r=>r.sharedMaterial).Distinct().Count()==1,"one material across five colors");
   var meshes=renderers.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray();OpenBrushMarbledImport.Prepare(root);
   Check(renderers.Select((r,i)=>r.GetComponent<MeshFilter>().sharedMesh==meshes[i]).All(x=>x),"preparation idempotent");
  }
  Debug.Log("OPENBRUSH_MARBLED_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
