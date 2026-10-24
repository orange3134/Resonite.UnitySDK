using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushSoftFragmentChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Soft fragment check: "+why);}
 public static void Run(){try {
  var texture=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);texture.SetPixel(0,0,new Color(.3f,.4f,.5f,.25f));texture.Apply();
  var face=new OpenBrushHighlighterImport.Face{copies=2,uv=new[]{Vector2.zero,Vector2.right,Vector2.up},colors=new[]{new Color(.2f,.4f,.6f,.2f),new Color(.6f,.2f,.3f,.8f),new Color(.1f,.7f,.4f,.5f)}};
  var sample=OpenBrushSoftFragmentImport.Sample(texture,face,.2f,.3f,Vector2.one,Vector2.zero);
  Check(Math.Abs(sample.r-Mathf.GammaToLinearSpace(.015375f))<1e-6 && Math.Abs(sample.g-Mathf.GammaToLinearSpace(.0369f))<1e-6 && Math.Abs(sample.b-Mathf.GammaToLinearSpace(.0492f))<1e-6 && sample.a==1,"interpolate Gamma paint and pressure before multiplying texture alpha and duplicate coverage");
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-additive-extra-v2/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;OpenBrushMaterialImport.Prepare(root,manifest,false);OpenBrushGrowthImport.Prepare(root,timing,null);
   var original=root.GetComponentsInChildren<MeshFilter>().Select(f=>new{Mesh=f.sharedMesh,Colors=f.sharedMesh.colors,Vertices=f.sharedMesh.vertices}).ToArray();
   Check(root.GetComponentsInChildren<MeshRenderer>().Where(r=>new[]{OpenBrushSoftFragmentImport.Feather,OpenBrushSoftFragmentImport.Drafting}.Contains(r.sharedMaterial.GetTag("OpenBrushGuid",false,""))).All(r=>OpenBrushHighlighterImport.Faces(r.GetComponent<MeshFilter>().sharedMesh).All(f=>f.copies==2)),"clipped front and back faces have uniform duplicate coverage");
   OpenBrushSoftFragmentImport.Prepare(root);
   var surfaces=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.name=="Source Gamma Pressure").ToArray();
   Check(surfaces.Length==70 && surfaces.All(r=>r.transform.parent.GetComponent<OpenBrushGrowthPart>()!=null),"all Feather and Drafting parts retain their drawing parents");
   var pages=surfaces.GroupBy(r=>r.sharedMaterial).ToArray();
   Check(pages.Length==10,"five paint colors have separate mipmapped pages for each of two brushes");
   foreach(var page in pages) {
    var paints=page.SelectMany(r=>r.transform.parent.GetComponent<MeshFilter>().sharedMesh.colors).Select(c=>new Vector3(c.r,c.g,c.b)).Distinct().ToArray();
    Check(paints.Length==1,"a mip chain cannot average unrelated stroke colors");
    var atlas=(Texture2D)page.Key.mainTexture;
    Check(atlas.mipmapCount>1 && atlas.format==TextureFormat.RGBAHalf,"keep distance filtering with half-float linear storage");
   }
   long pixels=pages.Sum(p=>(long)p.Key.mainTexture.width*p.Key.mainTexture.height);
   Check(pixels<37748736,"color isolation does not increase fixture pixel count");
   Debug.Log("SOFT_FRAGMENT_RESOURCE_PIXELS="+pixels);
   Check(original.All(s=>s.Mesh.colors.SequenceEqual(s.Colors)&&s.Mesh.vertices.SequenceEqual(s.Vertices)),"source geometry and pressure are not overwritten");
   foreach(var r in surfaces) {
    var m=r.GetComponent<MeshFilter>().sharedMesh;Check(m.colors.All(c=>c==Color.white)&&m.uv.All(v=>v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1),"atlas owns emission; generated UVs are bounded");
   }
   OpenBrushSoftFragmentImport.Prepare(root);Check(root.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name=="Source Gamma Pressure")==70,"repeat preparation is idempotent");
  }
  Debug.Log("OPENBRUSH_SOFT_FRAGMENT_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}

