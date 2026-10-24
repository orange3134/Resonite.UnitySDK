using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushSolidExtraChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Solid extra check: "+why);}
 public static void Run(){try {
  var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,new Vector3(0,.8f,-.6f),Vector3.up},triangles=new[]{0,1,2,0,3,1},colors=new[]{Color.red,Color.green,Color.blue,new Color(.2f,.3f,.4f,.6f)},uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up}};
  var flat=OpenBrushTriangleImport.Build(mesh,Matrix4x4.identity,false);var axis=OpenBrushTriangleImport.Build(mesh,Matrix4x4.identity,true);
  Check(flat.vertexCount==6 && flat.vertices.SequenceEqual(mesh.triangles.Select(i=>mesh.vertices[i])) && flat.colors.SequenceEqual(mesh.triangles.Select(i=>mesh.colors[i])) && flat.uv.SequenceEqual(mesh.triangles.Select(i=>mesh.uv[i])),"deindex preserves positions, RGBA and UV; no mesh mutation");
  Check(Vector3.Distance(flat.normals[0],new Vector3(0,.6f,.8f))<1e-6 && flat.normals[0]==flat.normals[1] && flat.normals[3]==Vector3.back,"independent flat normals on shared original vertices");
  Check(Math.Abs(axis.colors[0].g-.6f)<1e-6 && Math.Abs(axis.colors[0].b-.8f)<1e-6 && axis.colors[0]==axis.colors[1] && axis.colors[3]==new Color(0,0,0,1) && axis.colors[4].a==.6f,"signed original-canvas normal replaces paint RGB, while alpha survives");
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-solid-extra-v1/";
  var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   root.transform.SetPositionAndRotation(new Vector3(302,0,2),Quaternion.Euler(0,180,0));
   var filters=root.GetComponentsInChildren<MeshFilter>();var source=filters.Select(f=>f.sharedMesh).ToArray();
   var positions=source.Select(m=>m.vertices).ToArray();var colors=source.Select(m=>m.colors).ToArray();
   var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json"));
   Check(OpenBrushMaterialImport.Prepare(root,manifest,false)==timing.segments.Length && manifest.materials.Length==5,"five fully bound brush profiles");
   Check(source.Select((m,i)=>m.vertices.SequenceEqual(positions[i]) && m.colors.SequenceEqual(colors[i])).All(v=>v),"source meshes retained unmodified");
   OpenBrushGrowthImport.Prepare(root,timing,null);OpenBrushTriangleImport.Prepare(root);
   var rendered=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && r.sharedMaterial.GetTag("OpenBrushGuid",false,"")!="").ToArray();
   Check(rendered.Length==timing.segments.Length,"one rendered surface per drawing part");
   foreach(var r in rendered) {
    var profile=OpenBrushProfiles.Find(r.sharedMaterial.GetTag("OpenBrushGuid",false,""));
    if(profile.Guid==OpenBrushTriangleImport.Printing || profile.Guid==OpenBrushTriangleImport.Faceted) {
     var original=r.transform.parent.GetComponent<MeshFilter>().sharedMesh;var current=r.GetComponent<MeshFilter>().sharedMesh;
     Check(r.transform.parent.GetComponent<OpenBrushGrowthPart>()!=null && !r.transform.parent.GetComponent<MeshRenderer>().enabled && current.vertices.SequenceEqual(original.triangles.Select(i=>original.vertices[i])),profile.Name+" follows growth transform without changing triangle positions");
    }else Check(r.GetComponent<OpenBrushGrowthPart>()!=null,profile.Name+" retains direct growth control");
   }
   var materials=rendered.Select(r=>r.sharedMaterial).Distinct().ToArray();Check(materials.Length==5,"five material families");
   foreach(var material in materials) {
    var profile=OpenBrushProfiles.Find(material.GetTag("OpenBrushGuid",false,""));
    if(profile.Unlit){var native=new FrooxEngine.UnlitMaterial();OpenBrushMaterialConverter.ConfigureUnlit(material,profile,native);Check(native.Sidedness==FrooxEngine.Sidedness.Front && native.BlendMode==FrooxEngine.BlendMode.Opaque && material.mainTexture==null,"Faceted ignores unused texture and stale tint, opaque front faces");}
    else{var native=new FrooxEngine.PBS_VertexColorSpecular();OpenBrushMaterialConverter.ConfigureSpecular(material,profile,native);Check(native.AlphaHandling==FrooxEngine.AlphaHandling.Opaque && native.Culling==(profile.SingleSided?FrooxEngine.Culling.Back:FrooxEngine.Culling.Off),profile.Name+" original culling and opacity");}
   }
   foreach(var material in materials) {
    var p=OpenBrushProfiles.Find(material.GetTag("OpenBrushGuid",false,""));
    if(p.Name!="ConcaveHull" && p.Name!="MatteMembrane" && p.Name!="ShinyMembrane")continue;
    var native=new FrooxEngine.PBS_VertexColorSpecular();OpenBrushMaterialConverter.ConfigureSpecular(material,p,native);
    float gain=p.Name=="ConcaveHull"?1.5f:1.3f;
    Check(native.AlbedoColor.profile==ColorProfile.Linear && Math.Abs(native.AlbedoColor.color.r-gain)<1e-5,"calibrated diffuse intensity stays linear: "+p.Name);
    if(p.Name=="ShinyMembrane")Check(native.SpecularColor.profile==ColorProfile.Linear && Math.Abs(native.SpecularColor.color.r-.2f)<1e-5 && Math.Abs(native.SpecularColor.color.a-.743f)<1e-5,"source ShinyMembrane specular is not gamma-decoded twice");
   }
   OpenBrushTriangleImport.Prepare(root);Check(root.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled && r.sharedMaterial.GetTag("OpenBrushGuid",false,"")!="")==rendered.Length,"repeat preparation does not duplicate surfaces");
  }
  Debug.Log("OPENBRUSH_SOLID_EXTRA_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}

