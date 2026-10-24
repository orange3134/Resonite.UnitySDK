using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushWireframeChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Wireframe check: "+why);}
 static double Area(Mesh mesh,bool redIntegral=false) {
  var p=mesh.vertices;var c=mesh.colors;var tri=mesh.triangles;double sum=0;
  for(int i=0;i<tri.Length;i+=3)sum+=Vector3.Cross(p[tri[i+1]]-p[tri[i]],p[tri[i+2]]-p[tri[i]]).magnitude*.5*(redIntegral?(c[tri[i]].r+c[tri[i+1]].r+c[tri[i+2]].r)/3:1);return sum;
 }
 public static void Run(){try {
  var quad=new Mesh();quad.vertices=new[]{Vector3.zero,Vector3.right,Vector3.one-Vector3.forward,Vector3.up};
  quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};quad.colors=Enumerable.Repeat(new Color(.1f,.2f,.3f,.05f),4).ToArray();
  quad.triangles=new[]{0,1,2,0,2,3,2,1,0,3,2,0};
  var baked=OpenBrushWireframeImport.Bake(quad,0);
  Check(Math.Abs(Area(baked)-.19)<1e-5,"unit square keeps 19 percent border area, no center fill");
  Check(Math.Abs(Area(baked,true)-.04)<1e-5,"corner contribution and two coincident faces summed before native blending, independent of alpha");
  var emission=OpenBrushWireframeImport.Emission(new Color(.12f,.06f,.18f,.01f),2,.15f);
  Check(Math.Abs(Mathf.GammaToLinearSpace(emission.r)+Mathf.GammaToLinearSpace(.15f)-Mathf.GammaToLinearSpace(.39f))<1e-6,"reference background preserves the desired source Gamma sum");
  var saved=quad.uv;quad.uv=Enumerable.Repeat(new Vector2(.05f,.5f),4).ToArray();
  Check(OpenBrushWireframeImport.Bake(quad).vertexCount==0,"strict boundary does not emit or duplicate faces");
  quad.uv=Enumerable.Repeat(new Vector2(-2,.5f),4).ToArray();
  Check(Math.Abs(Area(OpenBrushWireframeImport.Bake(quad))-1)<1e-5,"UVs outside zero to one are not wrapped");quad.uv=saved;
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-wireframe-v1/";
  var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;OpenBrushMaterialImport.Prepare(root,manifest,false);OpenBrushGrowthImport.Prepare(root,timing,null);
   var originals=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushWireframeImport.Guid).ToArray();
   Check(originals.Length==timing.segments.Length && originals.Length>5,"all five strokes retain their growth segments");
   Check(originals.All(r=>OpenBrushHighlighterImport.Faces(r.GetComponent<MeshFilter>().sharedMesh).All(f=>f.copies==2)),"clipped front and back faces align");
   OpenBrushWireframeImport.Prepare(root);
   var surfaces=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.name=="Wireframe UV Edges").ToArray();
   Check(surfaces.Length==originals.Length && surfaces.All(r=>r.transform.parent.GetComponent<OpenBrushGrowthPart>()!=null),"edge meshes stay under drawing parents");
   Check(originals.All(r=>!r.enabled),"unconverted filled surfaces disabled");
   Check(surfaces.All(r=>r.GetComponent<MeshFilter>().sharedMesh.vertexCount>0),"every growth segment has visible edges");
  }
  Debug.Log("OPENBRUSH_WIREFRAME_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
